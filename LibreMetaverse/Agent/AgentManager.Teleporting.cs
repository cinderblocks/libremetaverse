/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2026, Sjofn LLC
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
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// AgentManager partial class - Teleporting
    /// </summary>
    public partial class AgentManager
    {
        #region Teleporting

        private bool _requestedMaps = false;

        /// <summary>Teleports agent to their stored home location</summary>
        public Task<bool> GoHomeAsync(CancellationToken cancellationToken = default)
            => TeleportAsync(UUID.Zero, cancellationToken);

        /// <summary>Teleport agent to a landmark</summary>
        public async Task<bool> TeleportAsync(UUID landmark, CancellationToken cancellationToken = default)
        {
            if (teleportStatus == TeleportStatus.Progress)
            {
                Logger.Info("Teleport already in progress while attempting to teleport.", Client);
                return false;
            }

            teleportStatus = TeleportStatus.None;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _teleportTcs = tcs;

            TeleportLandmarkRequestPacket p = new TeleportLandmarkRequestPacket
            {
                Info = new TeleportLandmarkRequestPacket.InfoBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    LandmarkID = landmark
                }
            };
            Client.Network.SendPacket(p);

            return await WaitForTeleportAsync(tcs, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Teleport agent to another region</summary>
        public Task<bool> TeleportAsync(ulong regionHandle, Vector3 position, CancellationToken cancellationToken = default)
            => TeleportAsync(regionHandle, position, new Vector3(0.0f, 1.0f, 0.0f), cancellationToken);

        /// <summary>Teleport agent to another region</summary>
        public async Task<bool> TeleportAsync(ulong regionHandle, Vector3 position, Vector3 lookAt, CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null ||
                Client.Network.CurrentSim.Caps == null ||
                !Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                var queueTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var curSim = Client.Network.CurrentSim;
                curSim?.Caps?.EventQueue?.Start();

                EventHandler<EventQueueRunningEventArgs> queueCallback = null!;
                queueCallback = (sender, e) =>
                {
                    if (e.Simulator == Client.Network.CurrentSim)
                        queueTcs.TrySetResult(true);
                };

                Client.Network.EventQueueRunning += queueCallback;
                try
                {
                    await Task.WhenAny(queueTcs.Task,
                        Task.Delay(TimeSpan.FromSeconds(10), cancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    Client.Network.EventQueueRunning -= queueCallback;
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            teleportStatus = TeleportStatus.None;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _teleportTcs = tcs;

            RequestTeleport(regionHandle, position, lookAt, true);

            return await WaitForTeleportAsync(tcs, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Look up a simulator by name and teleport to it</summary>
        public Task<bool> TeleportAsync(string simName, Vector3 position, CancellationToken cancellationToken = default)
            => TeleportAsync(simName, position, new Vector3(0, 1.0f, 0), cancellationToken);

        /// <summary>Look up a simulator by name and teleport to it</summary>
        public async Task<bool> TeleportAsync(string simName, Vector3 position, Vector3 lookAt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(simName))
            {
                TeleportMessage = "Invalid simulator name";
                teleportStatus = TeleportStatus.Failed;
                OnTeleport(new TeleportEventArgs(TeleportMessage, teleportStatus, TeleportFlags.Default));
                Logger.Warn("Teleport failed; " + TeleportMessage, Client);
                return false;
            }

            if (Client.Network.CurrentSim == null)
            {
                TeleportMessage = $"Not in a current simulator, cannot teleport to {simName}";
                teleportStatus = TeleportStatus.Failed;
                OnTeleport(new TeleportEventArgs(TeleportMessage, teleportStatus, TeleportFlags.Default));
                Logger.Warn("Teleport failed; " + TeleportMessage, Client);
                return false;
            }

            if (teleportStatus == TeleportStatus.Progress)
            {
                Logger.Info($"Teleport already in progress while attempting to teleport to {simName}.", Client);
                return false;
            }

            teleportStatus = TeleportStatus.None;

            ulong handle;
            if (string.Equals(simName, Client.Network.CurrentSim.Name, StringComparison.OrdinalIgnoreCase))
            {
                handle = Client.Network.CurrentSim.Handle;
            }
            else
            {
                var region = await Client.Grid.GetGridRegionAsync(simName, GridLayerType.Objects, cancellationToken).ConfigureAwait(false);
                if (region == null && !_requestedMaps)
                {
                    _requestedMaps = true;
                    Client.Grid.RequestMainlandSims(GridLayerType.Objects);
                    // Give mainland map data time to arrive, then retry
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                    region = await Client.Grid.GetGridRegionAsync(simName, GridLayerType.Objects, cancellationToken).ConfigureAwait(false);
                }
                if (region == null)
                {
                    TeleportMessage = $"Unable to resolve simulator named: {simName}";
                    teleportStatus = TeleportStatus.Failed;
                    OnTeleport(new TeleportEventArgs(TeleportMessage, teleportStatus, TeleportFlags.Default));
                    Logger.Warn("Teleport failed; " + TeleportMessage, Client);
                    return false;
                }
                handle = region.Value.RegionHandle;
            }

            return await TeleportAsync(handle, position, lookAt, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> WaitForTeleportAsync(TaskCompletionSource<bool> tcs, CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(Client.Settings.Timing.TeleportTimeout);

            var completed = await Task.WhenAny(tcs.Task,
                Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token)).ConfigureAwait(false);

            _teleportTcs = null;

            if (completed == tcs.Task)
                return tcs.Task.Result;

            cancellationToken.ThrowIfCancellationRequested();

            TeleportMessage = "Teleport timed out.";
            teleportStatus = TeleportStatus.Failed;
            Logger.Info("Teleport has timed out.", Client);
            return false;
        }

        /// <summary>
        /// Request teleport to another simulator
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><see cref="Vector3"/> position in destination sim to teleport to</param>
        public void RequestTeleport(ulong regionHandle, Vector3 position)
        {
            RequestTeleport(regionHandle, position, new Vector3(0.0f, 1.0f, 0.0f));
        }

        /// <summary>
        /// Request teleport to another region
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><see cref="Vector3"/> position in destination sim to teleport to</param>
        /// <param name="lookAt"><see cref="Vector3"/> direction in destination sim agent will look at</param>
        /// <param name="ignoreCapsStatus">Ignores the connection state of <see cref="Simulator.Caps" /></param>
        public void RequestTeleport(ulong regionHandle, Vector3 position, Vector3 lookAt, bool ignoreCapsStatus = false)
        {
            if (ignoreCapsStatus || (Client.Network?.CurrentSim?.Caps != null &&
                Client.Network.CurrentSim.Caps.IsEventQueueRunning))
            {
                TeleportLocationRequestPacket teleport = new TeleportLocationRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Info =
                    {
                        LookAt = lookAt,
                        Position = position,
                        RegionHandle = regionHandle
                    }
                };

                Logger.Info($"Requesting teleport to region handle {regionHandle}", Client);

                Client.Network.SendPacket(teleport);
            }
            else
            {
                Logger.Info("Event queue is not running, teleport abandoned.", Client);
                TeleportMessage = "CAPS event queue is not running";
                teleportStatus = TeleportStatus.Failed;
                _teleportTcs?.TrySetResult(false);
            }
        }

        /// <summary>
        /// Teleport agent to a landmark
        /// </summary>
        /// <param name="landmark"><see cref="UUID"/> of the landmark to teleport agent to</param>
        public void RequestTeleport(UUID landmark)
        {
            TeleportLandmarkRequestPacket p = new TeleportLandmarkRequestPacket
            {
                Info = new TeleportLandmarkRequestPacket.InfoBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    LandmarkID = landmark
                }
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Send a teleport lure to another avatar with default "Join me in ..." invitation message
        /// </summary>
        /// <param name="targetID">target avatars <see cref="UUID"/> to lure</param>
        public void SendTeleportLure(UUID targetID)
        {
            var regionName = Client?.Network?.CurrentSim?.Name ?? "unknown";
            SendTeleportLure(targetID, $"Join me in {regionName}!");
        }

        /// <summary>
        /// Send a teleport lure to another avatar with custom invitation message
        /// </summary>
        /// <param name="targetID">target avatars <see cref="UUID"/> to lure</param>
        /// <param name="message">custom message to send with invitation</param>
        public void SendTeleportLure(UUID targetID, string message)
        {
            StartLurePacket p = new StartLurePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Info =
                {
                    LureType = 0,
                    Message = Utils.StringToBytes(message)
                },
                TargetData = new[] {new StartLurePacket.TargetDataBlock()}
            };
            p.TargetData[0].TargetID = targetID;
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Respond to a teleport lure by either accepting it and initiating
        /// the teleport, or denying it
        /// </summary>
        /// <param name="requesterID"><see cref="UUID"/> of the avatar sending the lure</param>
        /// <param name="sessionID">IM session ID from the group invitation message</param>
        /// <param name="accept">true to accept the lure, false to decline it</param>
        public void TeleportLureRespond(UUID requesterID, UUID sessionID, bool accept)
        {
            if (accept)
            {
                TeleportLureRequestPacket lure = new TeleportLureRequestPacket
                {
                    Info =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        LureID = sessionID,
                        TeleportFlags = (uint) TeleportFlags.ViaLure
                    }
                };

                Client.Network.SendPacket(lure);
            }
            else
            {
                InstantMessage(Name, requesterID, string.Empty, sessionID, InstantMessageDialog.DenyTeleport,
                    InstantMessageOnline.Offline, SimPosition, UUID.Zero, Utils.EmptyBytes);
            }
        }

        /// <summary>
        /// Request a teleport lure from another agent
        /// </summary>
        /// <param name="targetID"><see cref="UUID"/> of the avatar lure is being requested from</param>
        /// <param name="sessionID">IM session ID from the incoming lure request</param>
        /// <param name="message">message to send with request</param>
        public void SendTeleportLureRequest(UUID targetID, UUID sessionID, string message)
        {
            if (targetID != AgentID)
            {
                InstantMessage(Name, targetID, message, sessionID, InstantMessageDialog.RequestLure,
                InstantMessageOnline.Offline, SimPosition, UUID.Zero, Utils.EmptyBytes);
            }
        }

        /// <summary>Request a teleport lure from another agent with a custom message and explicit session ID</summary>
        /// <param name="targetID">UUID of the avatar to request a lure from</param>
        /// <param name="message">Message text to include with the request</param>
        public void SendTeleportLureRequest(UUID targetID, string message)
        {
            SendTeleportLureRequest(targetID, targetID, message);
        }

        /// <summary>Request a teleport lure from another agent using a default message</summary>
        /// <param name="targetID">UUID of the avatar to request a lure from</param>
        public void SendTeleportLureRequest(UUID targetID)
        {
            SendTeleportLureRequest(targetID, "Let me join you in your location");
        }

        #endregion Teleporting
    }
}
