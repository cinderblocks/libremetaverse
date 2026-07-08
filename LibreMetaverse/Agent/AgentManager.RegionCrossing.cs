/*
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    /// <summary>
    /// AgentManager partial class - Region Crossing State Machine
    /// </summary>
    public partial class AgentManager
    {
        #region Region Crossing State Machine

        /// <summary>
        /// States for the region crossing state machine
        /// </summary>
        public enum CrossingState
        {
            /// <summary>Not currently crossing</summary>
            Idle,
            /// <summary>Received crossing notification, preparing to cross</summary>
            PreparingCross,
            /// <summary>Connecting to new region</summary>
            Connecting,
            /// <summary>Waiting for AgentMovementComplete</summary>
            WaitingForComplete,
            /// <summary>Successfully completed crossing</summary>
            Completed,
            /// <summary>Crossing failed</summary>
            Failed,
            /// <summary>Recovering from failed crossing</summary>
            Recovering
        }

        /// <summary>
        /// Reasons for crossing failure
        /// </summary>
        public enum CrossingFailureReason
        {
            /// <summary>Unknown failure</summary>
            Unknown,
            /// <summary>Connection to new simulator failed</summary>
            ConnectionFailed,
            /// <summary>Timeout waiting for response</summary>
            Timeout,
            /// <summary>Maximum retries exceeded</summary>
            MaxRetriesExceeded,
            /// <summary>Simulator rejected connection</summary>
            SimulatorRejected,
            /// <summary>Network error</summary>
            NetworkError,
            /// <summary>Invalid crossing data</summary>
            InvalidData
        }

        /// <summary>
        /// Information about a pending region crossing
        /// </summary>
        private class CrossingInfo
        {
            public CrossingState State;
            public DateTime StartTime;
            public Simulator? OldSimulator;
            public Simulator? NewSimulator;
            public ulong RegionHandle;
            public IPEndPoint? EndPoint;
            public Uri? SeedCapability;
            public Vector3 Position;
            public Vector3 LookAt;
            public uint RegionSizeX;
            public uint RegionSizeY;
            public int RetryCount;
            public const int MaxRetries = 3;
            public CrossingFailureReason FailureReason;
            public string FailureMessage = string.Empty;
            public Exception? LastException;
            public bool HasRestoredOldSim;
            // Cancelled when this crossing is superseded or the state machine should abort
            public readonly CancellationTokenSource WorkCts = new CancellationTokenSource();
        }

        // _currentCrossing is read from monitoring methods without the semaphore — volatile ensures
        // they always see the latest reference without a torn read.
        private volatile CrossingInfo? _currentCrossing;
        // Single semaphore protects all mutations of _currentCrossing and its State field.
        // Not held across awaits — acquired briefly, released before any async work begins.
        private readonly SemaphoreSlim _stateLock = new SemaphoreSlim(1, 1);
        private Timer? _crossingTimeoutTimer;
        private const int CrossingTimeoutMs = 30000;
        private const int RetryDelayMs = 1000;

        private void InitializeCrossingStateMachine()
        {
            _currentCrossing = null;
            _crossingTimeoutTimer = new Timer(CrossingTimeoutCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private bool BeginRegionCrossing(Simulator? oldSim, ulong regionHandle, IPEndPoint endPoint,
            Uri seedCap, Vector3 position, Vector3 lookAt,
            uint regionSizeX = Simulator.DefaultRegionSizeX, uint regionSizeY = Simulator.DefaultRegionSizeY)
        {
            if (endPoint == null || seedCap == null)
            {
                Logger.Error("Invalid crossing parameters: endPoint or seedCap is null", Client);
                return false;
            }

            if (!_stateLock.Wait(0))
            {
                Logger.Warn("Region crossing state machine is busy; ignoring new crossing request", Client);
                return false;
            }

            CrossingInfo crossing;
            try
            {
                var existing = _currentCrossing;
                if (existing != null)
                {
                    var s = existing.State;
                    if (s != CrossingState.Idle && s != CrossingState.Failed && s != CrossingState.Completed)
                    {
                        if (s == CrossingState.Recovering)
                        {
                            Logger.Info("Aborting recovery to begin new crossing", Client);
                            existing.WorkCts.Cancel();
                        }
                        else
                        {
                            Logger.Warn($"Cannot begin crossing: already in state {s}", Client);
                            return false;
                        }
                    }
                }

                crossing = new CrossingInfo
                {
                    State = CrossingState.PreparingCross,
                    StartTime = Client.UtcNow,
                    OldSimulator = oldSim,
                    RegionHandle = regionHandle,
                    EndPoint = endPoint,
                    SeedCapability = seedCap,
                    Position = position,
                    LookAt = lookAt,
                    RegionSizeX = regionSizeX,
                    RegionSizeY = regionSizeY,
                };
                _currentCrossing = crossing;
                _crossingTimeoutTimer?.Change(CrossingTimeoutMs, Timeout.Infinite);
                Logger.Info($"Beginning region crossing from {oldSim?.Name ?? "unknown"} to {endPoint}", Client);
            }
            finally { _stateLock.Release(); }

            _ = TransitionCrossingStateAsync(CrossingState.Connecting, crossing.WorkCts.Token);
            return true;
        }

        // Acquires _stateLock briefly to update State, then releases before doing any async work.
        private async Task<bool> TransitionCrossingStateAsync(CrossingState newState, CancellationToken workToken)
        {
            CrossingInfo? crossing;
            try { await _stateLock.WaitAsync(workToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }

            try
            {
                crossing = _currentCrossing;
                if (crossing == null || workToken.IsCancellationRequested) return false;

                if (!IsValidCrossingStateTransition(crossing.State, newState))
                {
                    Logger.Warn($"Invalid region crossing state transition: {crossing.State} -> {newState}", Client);
                    return false;
                }

                Logger.Debug($"Region crossing state transition: {crossing.State} -> {newState}", Client);
                crossing.State = newState;
            }
            finally { _stateLock.Release(); }

            if (workToken.IsCancellationRequested) return false;

            return newState switch
            {
                CrossingState.Connecting       => await AttemptConnectionAsync(crossing!, workToken).ConfigureAwait(false),
                CrossingState.Recovering       => await AttemptRecoveryAsync(crossing!, workToken).ConfigureAwait(false),
                CrossingState.Completed        => OnCrossingCompleted(crossing!),
                CrossingState.Failed           => OnCrossingFailed(crossing!),
                _                              => true
            };
        }

        private static bool IsValidCrossingStateTransition(CrossingState from, CrossingState to)
        {
            return from switch
            {
                CrossingState.Idle             => to == CrossingState.PreparingCross,
                CrossingState.PreparingCross   => to is CrossingState.Connecting or CrossingState.Failed,
                CrossingState.Connecting       => to is CrossingState.WaitingForComplete or CrossingState.Failed or CrossingState.Recovering,
                CrossingState.WaitingForComplete => to is CrossingState.Completed or CrossingState.Failed or CrossingState.Connecting or CrossingState.Recovering,
                CrossingState.Recovering       => to is CrossingState.Idle or CrossingState.Failed or CrossingState.Connecting,
                CrossingState.Completed or CrossingState.Failed => to is CrossingState.Idle or CrossingState.PreparingCross,
                _                              => false
            };
        }

        private async Task<bool> AttemptConnectionAsync(CrossingInfo crossing, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Logger.Info($"Connecting to new region: {crossing.EndPoint} (attempt {crossing.RetryCount + 1}/{CrossingInfo.MaxRetries})", Client);

                if (crossing.EndPoint == null)
                {
                    crossing.FailureReason = CrossingFailureReason.InvalidData;
                    crossing.FailureMessage = "EndPoint is null";
                    Logger.Error("Cannot connect: EndPoint is null", Client);
                    return await TransitionCrossingStateAsync(CrossingState.Failed, token).ConfigureAwait(false);
                }

                if (crossing.SeedCapability == null)
                {
                    crossing.FailureReason = CrossingFailureReason.InvalidData;
                    crossing.FailureMessage = "SeedCapability is null";
                    Logger.Error("Cannot connect: SeedCapability is null", Client);
                    return await TransitionCrossingStateAsync(CrossingState.Failed, token).ConfigureAwait(false);
                }

                Simulator? newSim;
                try
                {
                    newSim = Client?.Network?.Connect(
                        crossing.EndPoint, crossing.RegionHandle, true, crossing.SeedCapability,
                        crossing.RegionSizeX, crossing.RegionSizeY);
                }
                catch (Exception ex)
                {
                    crossing.LastException = ex;
                    crossing.FailureReason = CrossingFailureReason.NetworkError;
                    crossing.FailureMessage = $"Exception during connection: {ex.Message}";
                    Logger.Error($"Exception during region crossing connection: {ex.Message}", ex, Client);
                    return await TransitionCrossingStateAsync(CrossingState.Recovering, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested) return false;

                if (newSim != null)
                {
                    crossing.NewSimulator = newSim;
                    relativePosition = crossing.Position;
                    LastPositionUpdate = Client!.UtcNow;
                    Movement.Camera.LookDirection(crossing.LookAt);

                    if (crossing.OldSimulator != null && crossing.OldSimulator != newSim)
                        crossing.OldSimulator.AgentMovementComplete = false;

                    Logger.Info($"Successfully connected to new region: {newSim.Name}", Client);
                    return await TransitionCrossingStateAsync(CrossingState.WaitingForComplete, token).ConfigureAwait(false);
                }

                crossing.FailureReason = CrossingFailureReason.ConnectionFailed;
                crossing.FailureMessage = $"Failed to connect to {crossing.EndPoint}";
                Logger.Warn(crossing.FailureMessage, Client);

                if (crossing.RetryCount < CrossingInfo.MaxRetries)
                {
                    crossing.RetryCount++;
                    Logger.Info($"Retrying region crossing (attempt {crossing.RetryCount}/{CrossingInfo.MaxRetries})", Client);
                    try { await Task.Delay(RetryDelayMs * crossing.RetryCount, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return false; }
                    // Loop to retry
                }
                else
                {
                    crossing.FailureReason = CrossingFailureReason.MaxRetriesExceeded;
                    crossing.FailureMessage += $" after {CrossingInfo.MaxRetries} attempts";
                    Logger.Error("Exceeded max retry attempts for region crossing", Client);
                    return await TransitionCrossingStateAsync(CrossingState.Recovering, token).ConfigureAwait(false);
                }
            }
            return false;
        }

        private async Task<bool> AttemptRecoveryAsync(CrossingInfo crossing, CancellationToken token)
        {
            Logger.Info("Attempting to recover from failed crossing by restoring old simulator connection", Client);

            if (crossing.OldSimulator == null)
            {
                Logger.Warn("Cannot recover: Old simulator is null", Client);
                return await TransitionCrossingStateAsync(CrossingState.Failed, token).ConfigureAwait(false);
            }

            if (crossing.HasRestoredOldSim)
            {
                Logger.Warn("Already attempted recovery, transitioning to Failed state", Client);
                return await TransitionCrossingStateAsync(CrossingState.Failed, token).ConfigureAwait(false);
            }

            crossing.HasRestoredOldSim = true;

            try
            {
                if (!crossing.OldSimulator.AgentMovementComplete)
                {
                    Logger.Info($"Restoring connection to old simulator: {crossing.OldSimulator.Name}", Client);
                    crossing.OldSimulator.AgentMovementComplete = true;

                    if (Client.Network.CurrentSim != crossing.OldSimulator)
                        Client.Network.CurrentSim = crossing.OldSimulator;

                    CompleteAgentMovement(crossing.OldSimulator);
                }

                if (crossing.NewSimulator != null)
                {
                    Logger.Info($"Disconnecting from partially connected new simulator: {crossing.NewSimulator.Name}", Client);
                    try { Client.Network.DisconnectSim(crossing.NewSimulator, false); }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Error disconnecting from new simulator during recovery: {ex.Message}", Client);
                    }
                }

                Logger.Info("Recovery completed, remaining in old simulator", Client);
                await Task.Delay(500, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                Logger.Error($"Exception during recovery: {ex.Message}", ex, Client);
            }

            return await TransitionCrossingStateAsync(CrossingState.Failed, token).ConfigureAwait(false);
        }

        private bool OnCrossingCompleted(CrossingInfo crossing)
        {
            _crossingTimeoutTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            var duration = Client.UtcNow - crossing.StartTime;
            Logger.Info($"Region crossing completed successfully in {duration.TotalSeconds:F2} seconds", Client);

            if (m_RegionCrossed != null)
                OnRegionCrossed(new RegionCrossedEventArgs(crossing.OldSimulator, crossing.NewSimulator));

            crossing.State = CrossingState.Idle;
            Logger.Debug($"Crossing cleanup: Old sim {crossing.OldSimulator?.Name ?? "null"}, New sim {crossing.NewSimulator?.Name ?? "null"}", Client);
            return true;
        }

        private bool OnCrossingFailed(CrossingInfo crossing)
        {
            _crossingTimeoutTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            var duration = Client.UtcNow - crossing.StartTime;
            string failureDetails = $"Region crossing failed after {duration.TotalSeconds:F2} seconds. Reason: {crossing.FailureReason}";

            if (!string.IsNullOrEmpty(crossing.FailureMessage))
                failureDetails += $" - {crossing.FailureMessage}";
            if (crossing.LastException != null)
                failureDetails += $" - Exception: {crossing.LastException.Message}";

            Logger.Warn(failureDetails, Client);

            if (crossing.OldSimulator != null)
            {
                Logger.Info(crossing.HasRestoredOldSim
                    ? $"Recovered by restoring connection to old simulator: {crossing.OldSimulator.Name}"
                    : $"Remaining in old simulator: {crossing.OldSimulator.Name}", Client);
            }
            else
            {
                Logger.Error("Critical: No valid simulator connection after failed crossing", Client);
            }

            if (m_RegionCrossed != null)
                OnRegionCrossed(new RegionCrossedEventArgs(crossing.OldSimulator, null));

            crossing.State = CrossingState.Idle;
            return false;
        }

        private void NotifyMovementComplete(Simulator simulator)
        {
            var crossing = _currentCrossing;
            if (crossing == null || crossing.State != CrossingState.WaitingForComplete) return;

            _ = Task.Run(async () =>
            {
                if (crossing.NewSimulator == simulator)
                {
                    Logger.Debug($"Received MovementComplete for new simulator: {simulator?.Name}", Client);
                    await TransitionCrossingStateAsync(CrossingState.Completed, crossing.WorkCts.Token).ConfigureAwait(false);
                }
                else if (crossing.OldSimulator == simulator)
                {
                    Logger.Warn("Received MovementComplete from old simulator while waiting for new simulator", Client);
                    crossing.FailureReason = CrossingFailureReason.SimulatorRejected;
                    crossing.FailureMessage = "Received MovementComplete from old simulator instead of new one";
                    await TransitionCrossingStateAsync(CrossingState.Recovering, crossing.WorkCts.Token).ConfigureAwait(false);
                }
                else
                {
                    Logger.Warn($"Received MovementComplete from unexpected simulator: {simulator?.Name}", Client);
                }
            });
        }

        private void CrossingTimeoutCallback(object? state)
        {
            var crossing = _currentCrossing;
            if (crossing == null) return;
            var s = crossing.State;
            if (s is CrossingState.Completed or CrossingState.Failed or CrossingState.Idle) return;

            _ = Task.Run(async () =>
            {
                crossing.FailureReason = CrossingFailureReason.Timeout;
                crossing.FailureMessage = $"Timeout in state {s}";
                Logger.Warn($"Region crossing timed out in state {s}", Client);

                var next = s == CrossingState.Recovering ? CrossingState.Failed : CrossingState.Recovering;
                await TransitionCrossingStateAsync(next, crossing.WorkCts.Token).ConfigureAwait(false);
            });
        }

        /// <summary>Get the current crossing state</summary>
        public CrossingState GetCrossingState()
            => _currentCrossing?.State ?? CrossingState.Idle;

        /// <summary>Get the current crossing failure reason, if any</summary>
        public CrossingFailureReason GetCrossingFailureReason()
            => _currentCrossing?.FailureReason ?? CrossingFailureReason.Unknown;

        /// <summary>Get detailed diagnostic information about the current crossing</summary>
        public string GetCrossingDetails()
        {
            _stateLock.Wait();
            try
            {
                var crossing = _currentCrossing;
                if (crossing == null) return "No active crossing";
                var duration = Client.UtcNow - crossing.StartTime;
                return $"State: {crossing.State}, " +
                       $"Duration: {duration.TotalSeconds:F2}s, " +
                       $"Retries: {crossing.RetryCount}/{CrossingInfo.MaxRetries}, " +
                       $"Old Sim: {crossing.OldSimulator?.Name ?? "null"}, " +
                       $"New Sim: {crossing.NewSimulator?.Name ?? "null"}, " +
                       $"Target: {crossing.EndPoint}, " +
                       $"Failure: {crossing.FailureReason} - {crossing.FailureMessage}";
            }
            finally { _stateLock.Release(); }
        }

        /// <summary>Check if a region crossing is currently in progress</summary>
        public bool IsCrossing()
        {
            var s = _currentCrossing?.State ?? CrossingState.Idle;
            return s is not (CrossingState.Idle or CrossingState.Completed or CrossingState.Failed);
        }

        /// <summary>Force cancel the current crossing</summary>
        public void CancelCrossing()
        {
            var crossing = _currentCrossing;
            if (crossing == null || !IsCrossing()) return;

            Logger.Warn("Force cancelling region crossing", Client);
            crossing.FailureMessage = "Crossing was manually cancelled";
            _ = TransitionCrossingStateAsync(CrossingState.Recovering, crossing.WorkCts.Token);
        }

        #endregion Region Crossing State Machine
    }
}
