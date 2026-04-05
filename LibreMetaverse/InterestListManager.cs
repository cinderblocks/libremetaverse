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
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>
    /// Manages the InterestList capability, which controls how the simulator culls object
    /// updates sent to the viewer.
    /// Corresponds to LLViewerRegion::setInterestListMode / resetInterestList and
    /// LLAgent::changeInterestListMode in the SL C++ viewer (llviewerregion.cpp / llagent.cpp).
    /// </summary>
    public class InterestListManager : IDisposable
    {
        private readonly GridClient Client;
        private bool _disposed;
        private volatile InterestListMode _currentMode = InterestListMode.Default;

        /// <summary>
        /// The current interest list mode that will be applied to all connected simulators
        /// and any simulators that connect in the future.
        /// Defaults to <see cref="InterestListMode.Default"/>.
        /// </summary>
        public InterestListMode CurrentMode => _currentMode;

        /// <summary>Creates a new instance of the InterestListManager</summary>
        /// <param name="client">The GridClient instance</param>
        public InterestListManager(GridClient client)
        {
            Client = client;
            Client.Network.SimConnected += OnSimConnected;
        }

        /// <summary>
        /// Automatically sends the current interest list mode to a newly connected simulator
        /// once its capabilities are available.
        /// Corresponds to LLViewerRegion::setCapabilitiesReceived calling setInterestListMode
        /// in the SL C++ viewer.
        /// </summary>
        private void OnSimConnected(object? sender, SimConnectedEventArgs e)
        {
            _ = SetModeOnSimAsync(e.Simulator, _currentMode, CancellationToken.None);
        }

        /// <summary>
        /// Sets the interest list mode for all currently connected simulators and stores the
        /// mode for any future simulator connections.
        /// Corresponds to LLAgent::changeInterestListMode in the SL C++ viewer.
        /// </summary>
        /// <param name="mode">The mode to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task SetModeAsync(InterestListMode mode, CancellationToken cancellationToken = default)
        {
            _currentMode = mode;

            List<Task> tasks;
            lock (Client.Network.Simulators)
            {
                tasks = new List<Task>(Client.Network.Simulators.Count);
                foreach (var sim in Client.Network.Simulators)
                {
                    if (sim.Caps != null)
                        tasks.Add(SetModeOnSimAsync(sim, mode, cancellationToken));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the interest list mode on a single simulator via a POST to the InterestList capability.
        /// Corresponds to LLViewerRegion::setInterestListMode in the SL C++ viewer.
        /// </summary>
        /// <param name="simulator">The simulator to update</param>
        /// <param name="mode">The mode to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the POST succeeded, false otherwise</returns>
        public async Task<bool> SetModeOnSimAsync(Simulator simulator, InterestListMode mode,
            CancellationToken cancellationToken = default)
        {
            if (simulator == null) throw new ArgumentNullException(nameof(simulator));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = simulator.Caps?.CapabilityURI("InterestList");
                if (cap == null)
                {
                    Logger.DebugLog($"InterestList capability not available on {simulator}.", Client);
                    return false;
                }

                var http = Client.HttpCapsClient;
                var msg = new InterestListMessage { InterestListMode = mode };
                bool success = false;
                await http.PostRequestAsync(cap, OSDFormat.Xml, msg.Serialize(), cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            Logger.Warn($"InterestList POST mode={msg.Mode} on {simulator} failed: {error.Message}", Client);
                            return;
                        }
                        success = response?.IsSuccessStatusCode ?? false;
                        if (!success)
                        {
                            Logger.Warn($"InterestList POST mode={msg.Mode} on {simulator} non-success: {response?.StatusCode}", Client);
                        }
                        else
                        {
                            Logger.DebugLog($"InterestList mode set to '{msg.Mode}' on {simulator}", Client);
                        }
                    }).ConfigureAwait(false);
                return success;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed setting InterestList mode on {simulator}", ex, Client);
                return false;
            }
        }

        /// <summary>
        /// Resets the interest list on all currently connected simulators to the server default
        /// by sending a DELETE (no body) to the InterestList capability, and resets
        /// <see cref="CurrentMode"/> to <see cref="InterestListMode.Default"/>.
        /// Corresponds to LLViewerRegion::resetInterestList in the SL C++ viewer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            _currentMode = InterestListMode.Default;

            List<Task> tasks;
            lock (Client.Network.Simulators)
            {
                tasks = new List<Task>(Client.Network.Simulators.Count);
                foreach (var sim in Client.Network.Simulators)
                {
                    if (sim.Caps != null)
                        tasks.Add(ResetOnSimAsync(sim, cancellationToken));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the interest list on a single simulator to the server default
        /// by sending a DELETE (no body) to the InterestList capability.
        /// Corresponds to LLViewerRegion::resetInterestList in the SL C++ viewer.
        /// </summary>
        /// <param name="simulator">The simulator to reset</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the DELETE succeeded, false otherwise</returns>
        public async Task<bool> ResetOnSimAsync(Simulator simulator,
            CancellationToken cancellationToken = default)
        {
            if (simulator == null) throw new ArgumentNullException(nameof(simulator));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Uri? cap = simulator.Caps?.CapabilityURI("InterestList");
                if (cap == null)
                {
                    Logger.DebugLog($"InterestList capability not available on {simulator}.", Client);
                    return false;
                }

                using var request = new HttpRequestMessage(HttpMethod.Delete, cap);
                using var response = await Client.HttpCapsClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                bool success = response.IsSuccessStatusCode;
                if (!success)
                {
                    Logger.Warn($"InterestList DELETE on {simulator} non-success: {response.StatusCode}", Client);
                }
                else
                {
                    Logger.DebugLog($"InterestList reset on {simulator}", Client);
                }
                return success;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed resetting InterestList on {simulator}", ex, Client);
                return false;
            }
        }

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Dispose managed resources</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Client.Network.SimConnected -= OnSimConnected;
                }
                _disposed = true;
            }
        }

        #endregion IDisposable
    }
}
