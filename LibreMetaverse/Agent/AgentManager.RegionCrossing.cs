/**
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

namespace OpenMetaverse
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
            public Simulator OldSimulator;
            public Simulator NewSimulator;
            public ulong RegionHandle;
            public IPEndPoint EndPoint;
            public Uri SeedCapability;
            public Vector3 Position;
            public Vector3 LookAt;
            public int RetryCount;
            public const int MaxRetries = 3;
            public CrossingFailureReason FailureReason;
            public string FailureMessage;
            public Exception LastException;
            public readonly object SyncLock = new object();
            
            /// <summary>
            /// Track if we've already restored the old simulator connection
            /// </summary>
            public bool HasRestoredOldSim;
        }

        private CrossingInfo _currentCrossing;
        private readonly object _crossingLock = new object();
        private Timer _crossingTimeoutTimer;
        private const int CrossingTimeoutMs = 30000; // 30 second timeout
        private const int RetryDelayMs = 1000; // 1 second delay between retries
        private const int RecoveryTimeoutMs = 10000; // 10 second recovery timeout

        /// <summary>
        /// Initialize the region crossing state machine
        /// </summary>
        private void InitializeCrossingStateMachine()
        {
            _currentCrossing = null;
            _crossingTimeoutTimer = new Timer(CrossingTimeoutCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Start a region crossing
        /// </summary>
        private bool BeginRegionCrossing(Simulator oldSim, ulong regionHandle, IPEndPoint endPoint, 
            Uri seedCap, Vector3 position, Vector3 lookAt)
        {
            lock (_crossingLock)
            {
                // Check if we're already crossing
                if (_currentCrossing != null && _currentCrossing.State != CrossingState.Idle &&
                    _currentCrossing.State != CrossingState.Failed && _currentCrossing.State != CrossingState.Completed)
                {
                    Logger.Warn($"Attempted to begin region crossing while already in state {_currentCrossing.State}", Client);
                    
                    // If we're recovering from a previous crossing, allow the new crossing to proceed
                    if (_currentCrossing.State == CrossingState.Recovering)
                    {
                        Logger.Info("Aborting recovery to begin new crossing", Client);
                    }
                    else
                    {
                        return false;
                    }
                }

                // Validate crossing parameters
                if (endPoint == null || seedCap == null)
                {
                    Logger.Error("Invalid crossing parameters: endPoint or seedCap is null", Client);
                    return false;
                }

                // Create new crossing info
                _currentCrossing = new CrossingInfo
                {
                    State = CrossingState.PreparingCross,
                    StartTime = DateTime.UtcNow,
                    OldSimulator = oldSim,
                    RegionHandle = regionHandle,
                    EndPoint = endPoint,
                    SeedCapability = seedCap,
                    Position = position,
                    LookAt = lookAt,
                    RetryCount = 0,
                    FailureReason = CrossingFailureReason.Unknown,
                    FailureMessage = string.Empty,
                    LastException = null,
                    HasRestoredOldSim = false
                };

                Logger.Info($"Beginning region crossing from {oldSim?.Name ?? "unknown"} to {endPoint}", Client);

                // Start timeout timer
                _crossingTimeoutTimer.Change(CrossingTimeoutMs, Timeout.Infinite);

                // Proceed to connection phase
                return TransitionCrossingState(CrossingState.Connecting);
            }
        }

        /// <summary>
        /// Transition to a new crossing state
        /// </summary>
        private bool TransitionCrossingState(CrossingState newState)
        {
            lock (_crossingLock)
            {
                if (_currentCrossing == null)
                    return false;

                lock (_currentCrossing.SyncLock)
                {
                    CrossingState oldState = _currentCrossing.State;

                    // Validate state transitions
                    if (!IsValidCrossingStateTransition(oldState, newState))
                    {
                        Logger.Warn($"Invalid region crossing state transition: {oldState} -> {newState}", Client);
                        return false;
                    }

                    Logger.Debug($"Region crossing state transition: {oldState} -> {newState}", Client);
                    _currentCrossing.State = newState;

                    // Handle state-specific actions
                    switch (newState)
                    {
                        case CrossingState.Connecting:
                            return AttemptConnection();

                        case CrossingState.WaitingForComplete:
                            // Nothing to do, waiting for MovementComplete packet
                            break;

                        case CrossingState.Completed:
                            OnCrossingCompleted();
                            break;

                        case CrossingState.Failed:
                            OnCrossingFailed();
                            break;

                        case CrossingState.Recovering:
                            return AttemptRecovery();
                    }

                    return true;
                }
            }
        }

        /// <summary>
        /// Check if a state transition is valid
        /// </summary>
        private bool IsValidCrossingStateTransition(CrossingState from, CrossingState to)
        {
            switch (from)
            {
                case CrossingState.Idle:
                    return to == CrossingState.PreparingCross;

                case CrossingState.PreparingCross:
                    return to == CrossingState.Connecting || to == CrossingState.Failed;

                case CrossingState.Connecting:
                    return to == CrossingState.WaitingForComplete || to == CrossingState.Failed || to == CrossingState.Recovering;

                case CrossingState.WaitingForComplete:
                    return to == CrossingState.Completed || to == CrossingState.Failed || 
                           to == CrossingState.Connecting || to == CrossingState.Recovering; // Allow retry

                case CrossingState.Recovering:
                    return to == CrossingState.Idle || to == CrossingState.Failed || to == CrossingState.Connecting;

                case CrossingState.Completed:
                case CrossingState.Failed:
                    return to == CrossingState.Idle || to == CrossingState.PreparingCross;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Attempt to connect to the new simulator
        /// </summary>
        private bool AttemptConnection()
        {
            if (_currentCrossing == null)
                return false;

            lock (_currentCrossing.SyncLock)
            {
                try
                {
                    Logger.Info($"Connecting to new region: {_currentCrossing.EndPoint} (attempt {_currentCrossing.RetryCount + 1}/{CrossingInfo.MaxRetries})", Client);

                    // Validate connection parameters
                    if (_currentCrossing.EndPoint == null)
                    {
                        _currentCrossing.FailureReason = CrossingFailureReason.InvalidData;
                        _currentCrossing.FailureMessage = "EndPoint is null";
                        Logger.Error("Cannot connect: EndPoint is null", Client);
                        return TransitionCrossingState(CrossingState.Failed);
                    }

                    if (_currentCrossing.SeedCapability == null)
                    {
                        _currentCrossing.FailureReason = CrossingFailureReason.InvalidData;
                        _currentCrossing.FailureMessage = "SeedCapability is null";
                        Logger.Error("Cannot connect: SeedCapability is null", Client);
                        return TransitionCrossingState(CrossingState.Failed);
                    }

                    // Attempt the connection
                    Simulator newSim = Client.Network.Connect(
                        _currentCrossing.EndPoint,
                        _currentCrossing.RegionHandle,
                        true,
                        _currentCrossing.SeedCapability);

                    if (newSim != null)
                    {
                        _currentCrossing.NewSimulator = newSim;

                        // Update position and look direction
                        relativePosition = _currentCrossing.Position;
                        LastPositionUpdate = DateTime.UtcNow;
                        Movement.Camera.LookDirection(_currentCrossing.LookAt);

                        // Mark old sim as no longer current
                        if (_currentCrossing.OldSimulator != null && _currentCrossing.OldSimulator != newSim)
                        {
                            _currentCrossing.OldSimulator.AgentMovementComplete = false;
                        }

                        Logger.Info($"Successfully connected to new region: {newSim.Name}", Client);
                        return TransitionCrossingState(CrossingState.WaitingForComplete);
                    }
                    else
                    {
                        _currentCrossing.FailureReason = CrossingFailureReason.ConnectionFailed;
                        _currentCrossing.FailureMessage = $"Failed to connect to {_currentCrossing.EndPoint}";
                        Logger.Warn(_currentCrossing.FailureMessage, Client);

                        // Retry if we haven't exceeded max retries
                        if (_currentCrossing.RetryCount < CrossingInfo.MaxRetries)
                        {
                            _currentCrossing.RetryCount++;
                            Logger.Info($"Retrying region crossing (attempt {_currentCrossing.RetryCount}/{CrossingInfo.MaxRetries})", Client);

                            // Wait a bit before retrying
                            Thread.Sleep(RetryDelayMs * _currentCrossing.RetryCount); // Exponential backoff
                            return AttemptConnection();
                        }
                        else
                        {
                            _currentCrossing.FailureReason = CrossingFailureReason.MaxRetriesExceeded;
                            _currentCrossing.FailureMessage += $" after {CrossingInfo.MaxRetries} attempts";
                            Logger.Error($"Exceeded max retry attempts for region crossing", Client);
                            
                            // Try to recover by restoring connection to old simulator
                            return TransitionCrossingState(CrossingState.Recovering);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _currentCrossing.LastException = ex;
                    _currentCrossing.FailureReason = CrossingFailureReason.NetworkError;
                    _currentCrossing.FailureMessage = $"Exception during connection: {ex.Message}";
                    Logger.Error($"Exception during region crossing connection: {ex.Message}", ex, Client);
                    
                    // Try to recover instead of immediately failing
                    return TransitionCrossingState(CrossingState.Recovering);
                }
            }
        }

        /// <summary>
        /// Attempt to recover from a failed crossing by restoring connection to old simulator
        /// </summary>
        private bool AttemptRecovery()
        {
            if (_currentCrossing == null)
                return false;

            lock (_currentCrossing.SyncLock)
            {
                try
                {
                    Logger.Info("Attempting to recover from failed crossing by restoring old simulator connection", Client);

                    // Check if we have an old simulator to restore
                    if (_currentCrossing.OldSimulator == null)
                    {
                        Logger.Warn("Cannot recover: Old simulator is null", Client);
                        return TransitionCrossingState(CrossingState.Failed);
                    }

                    // Avoid multiple recovery attempts
                    if (_currentCrossing.HasRestoredOldSim)
                    {
                        Logger.Warn("Already attempted recovery, transitioning to Failed state", Client);
                        return TransitionCrossingState(CrossingState.Failed);
                    }

                    _currentCrossing.HasRestoredOldSim = true;

                    // Restore the old simulator as current
                    if (!_currentCrossing.OldSimulator.AgentMovementComplete)
                    {
                        Logger.Info($"Restoring connection to old simulator: {_currentCrossing.OldSimulator.Name}", Client);
                        _currentCrossing.OldSimulator.AgentMovementComplete = true;
                        
                        // Update Network's CurrentSim if needed
                        if (Client.Network.CurrentSim != _currentCrossing.OldSimulator)
                        {
                            Client.Network.CurrentSim = _currentCrossing.OldSimulator;
                        }

                        // Send a CompleteAgentMovement to ensure we're properly connected
                        CompleteAgentMovement(_currentCrossing.OldSimulator);
                    }

                    // If we have a new simulator that partially connected, disconnect it
                    if (_currentCrossing.NewSimulator != null)
                    {
                        Logger.Info($"Disconnecting from partially connected new simulator: {_currentCrossing.NewSimulator.Name}", Client);
                        try
                        {
                            Client.Network.DisconnectSim(_currentCrossing.NewSimulator, false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Error disconnecting from new simulator during recovery: {ex.Message}", Client);
                        }
                    }

                    Logger.Info("Recovery completed, remaining in old simulator", Client);
                    
                    // Give the recovery a moment to stabilize before marking as failed
                    Thread.Sleep(500);
                    
                    return TransitionCrossingState(CrossingState.Failed);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception during recovery: {ex.Message}", ex, Client);
                    return TransitionCrossingState(CrossingState.Failed);
                }
            }
        }

        /// <summary>
        /// Called when crossing is completed successfully
        /// </summary>
        private void OnCrossingCompleted()
        {
            if (_currentCrossing == null)
                return;

            lock (_currentCrossing.SyncLock)
            {
                // Stop timeout timer
                _crossingTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);

                TimeSpan duration = DateTime.UtcNow - _currentCrossing.StartTime;
                Logger.Info($"Region crossing completed successfully in {duration.TotalSeconds:F2} seconds", Client);

                // Fire the RegionCrossed event
                if (m_RegionCrossed != null)
                {
                    OnRegionCrossed(new RegionCrossedEventArgs(_currentCrossing.OldSimulator, _currentCrossing.NewSimulator));
                }

                // Clean up crossing info
                var oldSim = _currentCrossing.OldSimulator;
                var newSim = _currentCrossing.NewSimulator;
                
                // Reset state
                _currentCrossing.State = CrossingState.Idle;
                
                Logger.Debug($"Crossing cleanup: Old sim {oldSim?.Name ?? "null"}, New sim {newSim?.Name ?? "null"}", Client);
            }
        }

        /// <summary>
        /// Called when crossing fails
        /// </summary>
        private void OnCrossingFailed()
        {
            if (_currentCrossing == null)
                return;

            lock (_currentCrossing.SyncLock)
            {
                // Stop timeout timer
                _crossingTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);

                TimeSpan duration = DateTime.UtcNow - _currentCrossing.StartTime;
                
                // Build detailed failure message
                string failureDetails = $"Region crossing failed after {duration.TotalSeconds:F2} seconds. " +
                                      $"Reason: {_currentCrossing.FailureReason}";
                
                if (!string.IsNullOrEmpty(_currentCrossing.FailureMessage))
                {
                    failureDetails += $" - {_currentCrossing.FailureMessage}";
                }
                
                if (_currentCrossing.LastException != null)
                {
                    failureDetails += $" - Exception: {_currentCrossing.LastException.Message}";
                }

                Logger.Warn(failureDetails, Client);

                // Log the fallback behavior
                if (_currentCrossing.OldSimulator != null)
                {
                    if (_currentCrossing.HasRestoredOldSim)
                    {
                        Logger.Info($"Recovered by restoring connection to old simulator: {_currentCrossing.OldSimulator.Name}", Client);
                    }
                    else
                    {
                        Logger.Info($"Remaining in old simulator: {_currentCrossing.OldSimulator.Name}", Client);
                    }
                }
                else
                {
                    Logger.Error("Critical: No valid simulator connection after failed crossing", Client);
                }

                // Fire RegionCrossed event with null new simulator to indicate failure
                if (m_RegionCrossed != null)
                {
                    OnRegionCrossed(new RegionCrossedEventArgs(_currentCrossing.OldSimulator, null));
                }

                // Reset state
                _currentCrossing.State = CrossingState.Idle;
            }
        }

        /// <summary>
        /// Called when movement complete is received
        /// </summary>
        private void NotifyMovementComplete(Simulator simulator)
        {
            lock (_crossingLock)
            {
                if (_currentCrossing != null && _currentCrossing.State == CrossingState.WaitingForComplete)
                {
                    if (_currentCrossing.NewSimulator == simulator)
                    {
                        Logger.Debug($"Received MovementComplete for new simulator: {simulator?.Name}", Client);
                        TransitionCrossingState(CrossingState.Completed);
                    }
                    else if (_currentCrossing.OldSimulator == simulator)
                    {
                        // We received MovementComplete from the old simulator while waiting for the new one
                        // This might indicate the crossing failed and we're being kept in the old sim
                        Logger.Warn($"Received MovementComplete from old simulator while waiting for new simulator", Client);
                        
                        _currentCrossing.FailureReason = CrossingFailureReason.SimulatorRejected;
                        _currentCrossing.FailureMessage = "Received MovementComplete from old simulator instead of new one";
                        TransitionCrossingState(CrossingState.Recovering);
                    }
                    else
                    {
                        Logger.Warn($"Received MovementComplete from unexpected simulator: {simulator?.Name}", Client);
                    }
                }
            }
        }

        /// <summary>
        /// Timeout callback for region crossing
        /// </summary>
        private void CrossingTimeoutCallback(object state)
        {
            lock (_crossingLock)
            {
                if (_currentCrossing != null && _currentCrossing.State != CrossingState.Completed &&
                    _currentCrossing.State != CrossingState.Failed && _currentCrossing.State != CrossingState.Idle)
                {
                    _currentCrossing.FailureReason = CrossingFailureReason.Timeout;
                    _currentCrossing.FailureMessage = $"Timeout in state {_currentCrossing.State}";
                    Logger.Warn($"Region crossing timed out in state {_currentCrossing.State}", Client);
                    
                    // Try to recover instead of immediately failing
                    if (_currentCrossing.State != CrossingState.Recovering)
                    {
                        TransitionCrossingState(CrossingState.Recovering);
                    }
                    else
                    {
                        // Already recovering and timed out, just fail
                        TransitionCrossingState(CrossingState.Failed);
                    }
                }
            }
        }

        /// <summary>
        /// Get the current crossing state (for debugging/monitoring)
        /// </summary>
        public CrossingState GetCrossingState()
        {
            lock (_crossingLock)
            {
                return _currentCrossing?.State ?? CrossingState.Idle;
            }
        }

        /// <summary>
        /// Get the current crossing failure reason (if any)
        /// </summary>
        public CrossingFailureReason GetCrossingFailureReason()
        {
            lock (_crossingLock)
            {
                return _currentCrossing?.FailureReason ?? CrossingFailureReason.Unknown;
            }
        }

        /// <summary>
        /// Get detailed information about the current crossing (for debugging)
        /// </summary>
        public string GetCrossingDetails()
        {
            lock (_crossingLock)
            {
                if (_currentCrossing == null)
                    return "No active crossing";

                lock (_currentCrossing.SyncLock)
                {
                    var duration = DateTime.UtcNow - _currentCrossing.StartTime;
                    return $"State: {_currentCrossing.State}, " +
                           $"Duration: {duration.TotalSeconds:F2}s, " +
                           $"Retries: {_currentCrossing.RetryCount}/{CrossingInfo.MaxRetries}, " +
                           $"Old Sim: {_currentCrossing.OldSimulator?.Name ?? "null"}, " +
                           $"New Sim: {_currentCrossing.NewSimulator?.Name ?? "null"}, " +
                           $"Target: {_currentCrossing.EndPoint}, " +
                           $"Failure: {_currentCrossing.FailureReason} - {_currentCrossing.FailureMessage}";
                }
            }
        }

        /// <summary>
        /// Check if currently in the middle of a region crossing
        /// </summary>
        public bool IsCrossing()
        {
            lock (_crossingLock)
            {
                return _currentCrossing != null && 
                       _currentCrossing.State != CrossingState.Idle &&
                       _currentCrossing.State != CrossingState.Completed &&
                       _currentCrossing.State != CrossingState.Failed;
            }
        }

        /// <summary>
        /// Force cancel the current crossing (use with caution)
        /// </summary>
        public void CancelCrossing()
        {
            lock (_crossingLock)
            {
                if (_currentCrossing != null && IsCrossing())
                {
                    Logger.Warn("Force cancelling region crossing", Client);
                    _currentCrossing.FailureReason = CrossingFailureReason.Unknown;
                    _currentCrossing.FailureMessage = "Crossing was manually cancelled";
                    TransitionCrossingState(CrossingState.Recovering);
                }
            }
        }

        #endregion Region Crossing State Machine
    }
}
