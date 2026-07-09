/*
 * Copyright (c) 2025, Sjofn LLC
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
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Interfaces;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceManager
    {
        // Internal class to track a voice session for a specific region
        private class RegionVoiceSession
        {
            public ulong RegionHandle { get; set; }
            public VoiceSession? Session { get; set; }
            public bool IsPrimary { get; set; }
            public DateTime LastActivity { get; set; }
        }

        // Internal class to track a group voice session
        private class GroupVoiceSession
        {
            public UUID GroupId { get; set; }
            public VoiceSession? Session { get; set; }
            public string? ChannelId { get; set; }
            public string? ChannelCredentials { get; set; }
            public DateTime JoinedAt { get; set; }
            public DateTime LastActivity { get; set; }
        }

        // Internal class to track a P2P voice call session
        private class P2PVoiceSession
        {
            public UUID AgentId { get; set; }
            public VoiceSession? Session { get; set; }
            public string? ChannelId { get; set; }
            public string? ChannelCredentials { get; set; }
            public DateTime CallStarted { get; set; }
            public DateTime LastActivity { get; set; }
            public bool IsOutgoing { get; set; }
        }

        private readonly GridClient Client;
        public readonly AudioDevice AudioDevice;
        private readonly IVoiceLogger _log;

        // Track primary region session
        private RegionVoiceSession? _primarySession;

        // Track group voice sessions
        private readonly ConcurrentDictionary<UUID, GroupVoiceSession> _groupSessions = new ConcurrentDictionary<UUID, GroupVoiceSession>();

        // Track P2P voice call sessions
        private readonly ConcurrentDictionary<UUID, P2PVoiceSession> _p2pSessions = new ConcurrentDictionary<UUID, P2PVoiceSession>();

        // Neighbour-region estate voice sessions keyed by region handle.
        // SL C++ maintains up to 8 simultaneous spatial connections for cross-region voice.
        private readonly ConcurrentDictionary<ulong, VoiceSession> _neighborSessions =
            new ConcurrentDictionary<ulong, VoiceSession>();

        // Tracks how long each currently-in-range neighbour session has been continuously
        // disconnected. A neighbour session runs its own internal ICE/reprovision watchdogs
        // (see VoiceSession.ScheduleReprovisionWithBackoff), but nothing here ever re-checks an
        // *existing* dictionary entry once created — ReconcileNeighborSessions only adds sessions
        // for newly in-range regions and removes ones that fell out of range. If a neighbour
        // session's own reprovisioning eventually gives up for good (persistent network issue),
        // it would otherwise sit dead in _neighborSessions for as long as the agent stays in
        // range of that region, since nothing ever replaces it.
        private readonly ConcurrentDictionary<ulong, DateTime> _neighborDisconnectedSince =
            new ConcurrentDictionary<ulong, DateTime>();

        // Comfortably longer than a neighbour session's own worst-case reprovision backoff
        // (6 attempts, delays doubling from 1s up to a 60s cap ≈ 63s total) so we don't replace
        // a session that's still legitimately retrying on its own.
        private const int NEIGHBOR_STALL_TIMEOUT_MS = 90_000;

        // True while in estate-voice mode (as opposed to parcel-private voice).
        private bool _estateVoiceActive = false;

        // Background task that periodically reconciles neighbour sessions with agent proximity.
        private CancellationTokenSource? _neighborLoopCts;
        private Task? _neighborLoopTask;

        // Debounce: minimum ms between neighbour-set reconciliations.
        private const int NEIGHBOR_UPDATE_INTERVAL_MS = 2000;

        // SL C++ MAX_AUDIO_DIST = 50 m; probe radius = 2 × that (100 m) to handle camera offset.
        private const double NEIGHBOR_PROBE_DIST = 100.0;

        private bool _enabled = true;

        // Store channel info from parcel voice info
        private string? _channelId;
        private string? _channelCredentials;

        // Track current region to detect changes
        private ulong _currentRegionHandle = 0;
        private readonly SemaphoreSlim _regionTransitionLock = new SemaphoreSlim(1, 1);
        private bool _isTransitioning = false;

        // Prevent concurrent ConnectPrimaryRegionAsync calls
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        // Expose session/channel info for primary session
        public UUID SessionId => _primarySession?.Session?.SessionId ?? UUID.Zero;
        public string ChannelId => _primarySession?.Session?.ChannelId ?? _channelId ?? string.Empty;
        public string ChannelCredentials => _primarySession?.Session?.ChannelCredentials ?? _channelCredentials ?? string.Empty;
        
        // Expose SDP and connection state for primary session
        public string SdpLocal    => _primarySession?.Session?.SdpLocal  ?? string.Empty;
        public string SdpRemote   => _primarySession?.Session?.SdpRemote ?? string.Empty;
        public bool   Connected   => _primarySession?.Session?.Connected  ?? false;

        // Cross-region events
        public event Action? OnRegionChangeDetected;
        public event Action? OnRegionTransitionCompleted;
        public event Action<Exception>? OnRegionTransitionFailed;

        // Reprovision events (fired when the dead-channel watchdog triggers a session rebuild)
        public event Action? OnReprovisionSucceeded;
        public event Action<Exception>? OnReprovisionFailed;

        // Group voice events
        public event Action<UUID>? OnGroupVoiceJoined;
        public event Action<UUID>? OnGroupVoiceLeft;
        public event Action<UUID, Exception>? OnGroupVoiceJoinFailed;

        // P2P voice call events
        public event Action<UUID>? OnP2PCallStarted;
        public event Action<UUID>? OnP2PCallEnded;
        public event Action<UUID, Exception>? OnP2PCallFailed;
        public event Action<UUID>? OnP2PCallIncoming;
        public event Action<UUID>? OnP2PCallAccepted;
        public event Action<UUID>? OnP2PCallDeclined;

        // Expose data-channel / voice events to clients (raw)
        public event Action<UUID>? PeerJoined;
        public event Action<UUID>? PeerLeft;
        public event Action<UUID, OSDMap>? PeerPositionUpdated;
        public event Action<List<UUID>>? PeerListUpdated;
        // Expose connection events to clients
        public event Action? PeerConnectionReady;
        public event Action? PeerConnectionClosed;

        // Expose typed events per PDF
        public event Action<UUID, VoiceSession.PeerAudioState>? PeerAudioUpdated;
        public event Action<UUID, VoiceSession.AvatarPosition>? PeerPositionUpdatedTyped;
        public event Action<Dictionary<UUID, bool>>? MuteMapReceived;
        public event Action<Dictionary<UUID, int>>? GainMapReceived;
        public event Action<string, int, string>? OnParcelVoiceInfo;

        public VoiceManager(GridClient client, IVoiceLogger? logger = null)
        {
            Client = client;
            AudioDevice = new AudioDevice();
            _log = logger ?? new LibreMetaverseVoiceLogger();
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);

            // Register for P2P voice call invitations via ChatterBox
            Client.Network.RegisterEventCallback("ChatterBoxInvitation", OnChatterBoxInvitationForVoice);
            Client.Network.RegisterEventCallback("ChatterBoxSessionStartReply", OnChatterBoxSessionStartReply);
            Client.Network.RegisterEventCallback("ForceCloseChatterBoxSession", OnForceCloseChatterBoxSession);

            // Register for region change events
            Client.Network.SimChanged += OnSimChanged;
            Client.Self.TeleportProgress += OnTeleport;
        }

        private void OnSimChanged(object? sender, SimChangedEventArgs e)
        {
            _ = HandleRegionChange(e.PreviousSimulator);
        }

        private void OnTeleport(object? sender, TeleportEventArgs e)
        {
            if (e.Status == TeleportStatus.Finished)
                _ = HandleRegionChange(null);
        }

        private async Task HandleRegionChange(Simulator? previousSim)
        {
            if (!_enabled || _primarySession == null) return;

            var newRegionHandle = Client.Network.CurrentSim?.Handle ?? 0;
            
            // Detect if we've actually changed regions
            if (_currentRegionHandle != 0 && _currentRegionHandle == newRegionHandle)
            {
                return; // Same region, no action needed
            }

            if (_isTransitioning)
            {
                _log.Debug("Region transition already in progress, skipping duplicate", Client);
                return;
            }

            // Acquire lock to prevent concurrent transitions
            if (!await _regionTransitionLock.WaitAsync(0))
            {
                _log.Debug("Region transition lock held, skipping", Client);
                return;
            }

            try
            {
                _isTransitioning = true;
                var oldRegionHandle = _currentRegionHandle;
                _currentRegionHandle = newRegionHandle;

                _log.Info($"Region change detected: {oldRegionHandle:X} -> {newRegionHandle:X}", Client);

                try
                {
                    OnRegionChangeDetected?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Exception in OnRegionChangeDetected handler: {ex.Message}", Client);
                }

                // Reprovision the voice session for the new region.
                // DetermineParcelVoiceContext() inside ReprovisionForNewRegion() will read
                // the new parcel flags directly — no ParcelVoiceInfoRequest needed for WebRTC spatial.
                await ReprovisionForNewRegion();

                try
                {
                    OnRegionTransitionCompleted?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Exception in OnRegionTransitionCompleted handler: {ex.Message}", Client);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Region transition failed: {ex.Message}", Client);
                
                try
                {
                    OnRegionTransitionFailed?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    _log.Warn($"Exception in OnRegionTransitionFailed handler: {handlerEx.Message}", Client);
                }
            }
            finally
            {
                _isTransitioning = false;
                _regionTransitionLock.Release();
            }
        }

        private async Task ReprovisionForNewRegion()
        {
            if (_primarySession == null) return;

            _log.Info("Reprovisioning voice session for new region", Client);

            // Store audio state
            bool wasPlaybackActive = AudioDevice?.EndPoint != null && AudioDevice.PlaybackActive;

            try
            {
                // Stop audio before closing session
                if (AudioDevice != null)
                {
                    try { AudioDevice.StopRecording(); } catch { }
                    try { await AudioDevice.StopPlaybackAsync().ConfigureAwait(false); } catch { }
                }

                // Tear down neighbour sessions from the old region
                StopNeighborLoop();
                TearDownAllNeighborSessions();

                // Close the current primary session
                if (_primarySession?.Session != null)
                {
                    try
                    {
                        try { _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady; } catch { }
                        await _primarySession.Session.CloseSessionAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Error closing session during reprovision: {ex.Message}", Client);
                    }
                }

                // Determine parcel context for new region using parcel flags (SL C++ approach).
                // Spatial voice is always channel_type="local"; MULTIAGENT is only for group/P2P.
                DetermineParcelVoiceContext(out var parcelLocalId, out _estateVoiceActive);
                _log.Info(_estateVoiceActive
                    ? "Using estate voice for new region (neighbour connections enabled)"
                    : $"Using parcel voice for new region parcel_id={parcelLocalId}", Client);

                // Create new primary session
                var session = new VoiceSession(AudioDevice!, VoiceSession.ESessionType.LOCAL, Client, _log)
                {
                    IsPrimary = true,
                    ParcelLocalId = parcelLocalId
                };

                _primarySession = new RegionVoiceSession
                {
                    RegionHandle = Client.Network.CurrentSim?.Handle ?? 0,
                    Session = session,
                    IsPrimary = true,
                    LastActivity = DateTime.UtcNow
                };

                // Wire events
                session.OnDataChannelReady += CurrentSessionOnOnDataChannelReady;
                WirePrimarySessionEvents(session);

                // Start new session
                await session.StartAsync().ConfigureAwait(false);
                await session.RequestProvisionAsync().ConfigureAwait(false);

                // Do not restore recording here — OnPeerConnectionReady fires OnConnectionReady
                // in the UI which owns mic/PTT state. Restoring blindly fights PTT mode.
                await Task.Delay(500).ConfigureAwait(false);

                if (wasPlaybackActive && AudioDevice?.EndPoint != null)
                {
                    try { await AudioDevice.StartPlaybackAsync().ConfigureAwait(false); } catch (Exception ex) 
                    { 
                        _log.Warn($"Failed to restart playback after region change: {ex.Message}", Client); 
                    }
                }

                // Restart neighbour loop for estate voice
                if (_estateVoiceActive)
                    StartNeighborLoop();

                _log.Info("Voice session reprovisioned successfully for new region", Client);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to reprovision voice session: {ex.Message}", Client);
                throw;
            }
        }


        public async Task<bool> ConnectPrimaryRegionAsync()
        {
            _log.Debug("ConnectPrimaryRegionAsync started", Client);
            if (!Client.Network.Connected) { return false; }
            if (!_enabled) 
            {
                _log.Warn("WebRTC voice is disabled due to unsupported voice version", Client);
                return false;
            }

            // Prevent concurrent calls from racing each other.
            // Non-blocking: if a connect is already in progress, drop this call.
            if (!await _connectLock.WaitAsync(0).ConfigureAwait(false))
            {
                _log.Debug("ConnectPrimaryRegionAsync already in progress, dropping duplicate call", Client);
                return false;
            }

            try
            {
                // Close any existing primary session before creating a new one.
                // Without this the old Janus session stays live on the server and
                // the new ICE exchange fails immediately.
                if (_primarySession?.Session != null)
                {
                    _log.Debug("ConnectPrimaryRegionAsync: closing existing primary session before reconnect", Client);
                    StopNeighborLoop();
                    try { _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady; } catch { }
                    try { await _primarySession.Session.CloseSessionAsync().ConfigureAwait(false); } catch { }
                    _primarySession = null;
                }

                // Initialize current region handle
                _currentRegionHandle = Client.Network.CurrentSim?.Handle ?? 0;

                // SL C++ WebRTC spatial voice is ALWAYS channel_type="local".
                // MULTIAGENT is only used for group/P2P adhoc channels, never spatial.
                // Parcel-specific vs estate voice is determined by parcel flags, not ParcelVoiceInfoRequest.
                // Determine parcel context from flags to set parcel_local_id correctly.
                DetermineParcelVoiceContext(out var parcelLocalId, out _estateVoiceActive);
                _log.Info(_estateVoiceActive
                    ? "Using estate voice (parcel uses estate channel)"
                    : $"Using parcel voice for parcel local_id={parcelLocalId}", Client);

                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.LOCAL, Client, _log)
                {
                    IsPrimary = true,
                    ParcelLocalId = parcelLocalId
                };

                // Create primary session tracker
                _primarySession = new RegionVoiceSession
                {
                    RegionHandle = _currentRegionHandle,
                    Session = session,
                    IsPrimary = true,
                    LastActivity = DateTime.UtcNow
                };

                // wire internal events
                session.OnDataChannelReady += CurrentSessionOnOnDataChannelReady;
                WirePrimarySessionEvents(session);

                await session.StartAsync().ConfigureAwait(false);
                var provisioned = await session.RequestProvisionAsync().ConfigureAwait(false);

                // Start estate-voice neighbour loop if applicable
                if (_estateVoiceActive)
                    StartNeighborLoop();

                return provisioned;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public void Disconnect()
        {
            // Close all P2P voice calls
            foreach (var kvp in _p2pSessions)
            {
                try
                {
                    _ = kvp.Value.Session?.CloseSessionAsync();
                    try { kvp.Value.Session?.Dispose(); } catch { }
                }
                catch { }
            }
            _p2pSessions.Clear();

            // Close all group voice sessions
            foreach (var kvp in _groupSessions)
            {
                try
                {
                    _ = kvp.Value.Session?.CloseSessionAsync();
                    try { kvp.Value.Session?.Dispose(); } catch { }
                }
                catch { }
            }
            _groupSessions.Clear();

            // Close primary session
            if (_primarySession != null)
            {
                try
                {
                    if (_primarySession.Session != null)
                    {
                        try { _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady; } catch { }
                        _ = _primarySession.Session.CloseSessionAsync();
                        try { _primarySession.Session.Dispose(); } catch { }
                    }
                }
                catch { }
                _primarySession = null;
            }

            // Stop neighbour-region estate-voice loop and close all neighbour sessions
            StopNeighborLoop();
            TearDownAllNeighborSessions();

            // Unregister handlers
            try
            {
                Client.Network.SimChanged -= OnSimChanged;
                Client.Self.TeleportProgress -= OnTeleport;
                Client.Network.UnregisterEventCallback("ChatterBoxInvitation", OnChatterBoxInvitationForVoice);
                    try { Client.Network.UnregisterEventCallback("ChatterBoxSessionStartReply", OnChatterBoxSessionStartReply); } catch { }
                    try { Client.Network.UnregisterEventCallback("ForceCloseChatterBoxSession", OnForceCloseChatterBoxSession); } catch { }
            }
            catch { }

            _channelId = null;
            _channelCredentials = null;
            _currentRegionHandle = 0;
        }

        /// <summary>
        /// Wires all forwarding event handlers from a primary <see cref="VoiceSession"/> to the
        /// manager-level events consumed by the UI. Centralises what was previously repeated in
        /// <see cref="ConnectPrimaryRegionAsync"/> and <see cref="ReprovisionForNewRegion"/>.
        /// </summary>
        private void WirePrimarySessionEvents(VoiceSession session)
        {
            session.OnPeerConnectionReady  += () => { try { PeerConnectionReady?.Invoke(); } catch { } };
            session.OnPeerConnectionClosed += () => { try { PeerConnectionClosed?.Invoke(); } catch { } };

            session.OnPeerJoined               += id          => { try { PeerJoined?.Invoke(id); } catch { } };
            session.OnPeerLeft                 += id          => { try { PeerLeft?.Invoke(id); } catch { } };
            session.OnPeerPositionUpdated      += (id, map)   => { try { PeerPositionUpdated?.Invoke(id, map); } catch { } };
            session.OnPeerListUpdated          += list        => { try { PeerListUpdated?.Invoke(list); } catch { } };
            session.OnPeerAudioUpdated         += (id, state) => { try { PeerAudioUpdated?.Invoke(id, state); } catch { } };
            session.OnPeerPositionUpdatedTyped += (id, pos)   => { try { PeerPositionUpdatedTyped?.Invoke(id, pos); } catch { } };
            session.OnMuteMapReceived          += m           => { try { MuteMapReceived?.Invoke(m); } catch { } };
            session.OnGainMapReceived          += g           => { try { GainMapReceived?.Invoke(g); } catch { } };

            // Surface session-level reprovision outcomes to the UI layer
            session.OnReprovisionSucceeded += () => { try { OnReprovisionSucceeded?.Invoke(); } catch { } };
            session.OnReprovisionFailed    += ex => { try { OnReprovisionFailed?.Invoke(ex); } catch { } };
        }

        #region Estate-voice neighbour-region session management

        /// <summary>
        /// Determines whether the agent is in a parcel-specific voice channel or the estate channel,
        /// mirroring SL C++ spatial-voice channel selection logic.
        /// </summary>
        /// <param name="parcelLocalId">
        /// Set to the current parcel's LocalID if on a parcel-specific voice channel,
        /// or -1 (INVALID_PARCEL_ID) if on the estate channel.
        /// </param>
        /// <param name="useEstateVoice">
        /// <c>true</c> if the estate channel should be used; <c>false</c> for parcel-specific voice.
        /// </param>
        private void DetermineParcelVoiceContext(out int parcelLocalId, out bool useEstateVoice)
        {
            parcelLocalId = -1;   // INVALID_PARCEL_ID
            useEstateVoice = true;

            var parcel = Client.Parcels.CurrentParcel;
            if (parcel == null || parcel.LocalID <= 0) return;

            // Check parcel flags (mirrors SL C++ parcel->getParcelFlagAllowVoice()
            // and parcel->getParcelFlagUseEstateVoiceChannel())
            const LibreMetaverse.ParcelFlags allowVoice      = LibreMetaverse.ParcelFlags.AllowVoiceChat;
            const LibreMetaverse.ParcelFlags useEstateFlag   = LibreMetaverse.ParcelFlags.UseEstateVoiceChan;

            if (!parcel.Flags.HasFlag(allowVoice))
            {
                // Voice disabled on this parcel — still use estate so the session can connect
                // (the server will handle muting); SL C++ sets voiceEnabled=false but we stay
                // connected for neighbour awareness.
                return;
            }

            if (!parcel.Flags.HasFlag(useEstateFlag))
            {
                // Parcel-specific voice channel
                parcelLocalId = parcel.LocalID;
                useEstateVoice = false;
            }
            // else: parcel says use estate channel → useEstateVoice stays true
        }

        /// <summary>
        /// Starts a background loop that periodically reconciles neighbour-region voice sessions
        /// against the agent's current position, mirroring SL C++ <c>updateNeighboringRegions()</c>
        /// and <c>estateSessionState::processConnectionStates()</c>.
        /// </summary>
        private void StartNeighborLoop()
        {
            StopNeighborLoop();
            _neighborLoopCts = new CancellationTokenSource();
            var ct = _neighborLoopCts.Token;
            _neighborLoopTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await ReconcileNeighborSessions(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        try { _log.Warn($"Neighbour voice loop error: {ex.Message}", Client); } catch { }
                    }
                    try { await Task.Delay(NEIGHBOR_UPDATE_INTERVAL_MS, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }, ct);
        }

        private void StopNeighborLoop()
        {
            try { _neighborLoopCts?.Cancel(); } catch { }
            _neighborLoopCts = null;
        }

        /// <summary>
        /// Shuts down and removes every neighbour session immediately.
        /// Called on region change and disconnect.
        /// </summary>
        private void TearDownAllNeighborSessions()
        {
            foreach (var kvp in _neighborSessions)
            {
                try { _ = kvp.Value.CloseSessionAsync(); } catch { }
                try { kvp.Value.Dispose(); } catch { }
            }
            _neighborSessions.Clear();
            _neighborDisconnectedSince.Clear();
        }

        /// <summary>
        /// Determines which neighbouring regions should have an active voice session based on
        /// the agent's current world position and region size, then starts sessions for newly
        /// discovered regions and tears down sessions for regions that are no longer in range.
        /// 
        /// Mirrors SL C++ <c>updateNeighboringRegions()</c> + 
        /// <c>estateSessionState::processConnectionStates()</c>.
        /// </summary>
        private async Task ReconcileNeighborSessions(CancellationToken ct)
        {
            var homeSim = Client.Network?.CurrentSim;
            if (homeSim == null) return;

            // Agent position in region-local coords
            var agentPos = Client.Self.SimPosition;
            uint homeSize = homeSim.SizeX > 0 ? homeSim.SizeX : 256;

            // World-space position of agent
            Utils.LongToUInts(homeSim.Handle, out var homeGx, out var homeGy);
            double worldX = homeGx + agentPos.X;
            double worldY = homeGy + agentPos.Y;

            // Collect the set of simulator handles that are within probe range (SL: 2 × MAX_AUDIO_DIST).
            // We iterate the known connected simulators exactly as SL C++ iterates region objects.
            var desired = new HashSet<ulong>();
            List<Simulator> allSims;
            lock (Client.Network!.Simulators)
            {
                allSims = new List<Simulator>(Client.Network.Simulators);
            }

            foreach (var sim in allSims)
            {
                if (sim.Handle == homeSim.Handle) continue; // primary handles its own session
                if (!IsSimWebRTCEnabled(sim)) continue;

                Utils.LongToUInts(sim.Handle, out var gx, out var gy);
                uint sx = sim.SizeX > 0 ? sim.SizeX : 256;
                uint sy = sim.SizeY > 0 ? sim.SizeY : 256;

                // Closest point on the remote region's AABB to the agent's world position
                double clampX = Math.Max(gx, Math.Min(gx + sx, worldX));
                double clampY = Math.Max(gy, Math.Min(gy + sy, worldY));
                double dist = Math.Sqrt(Math.Pow(worldX - clampX, 2) + Math.Pow(worldY - clampY, 2));

                if (dist <= NEIGHBOR_PROBE_DIST)
                    desired.Add(sim.Handle);
            }

            // Tear down sessions for regions no longer in range
            var stale = new List<ulong>();
            foreach (var handle in _neighborSessions.Keys)
            {
                if (!desired.Contains(handle))
                    stale.Add(handle);
            }
            foreach (var handle in stale)
            {
                if (_neighborSessions.TryRemove(handle, out var old))
                {
                    _log.Debug($"Closing neighbour voice session for region handle {handle}", Client);
                    try { _ = old.CloseSessionAsync(); } catch { }
                    try { old.Dispose(); } catch { }
                }
                _neighborDisconnectedSince.TryRemove(handle, out _);
            }

            // Replace neighbour sessions that are still in range but have been continuously
            // disconnected long enough that their own reprovision backoff must have exhausted.
            // Without this, a neighbour session that permanently dies (as opposed to a transient
            // reconnect) would sit dead in _neighborSessions for as long as the agent stays near
            // that region, since the loops above only ever add/remove based on range.
            var stalled = new List<ulong>();
            foreach (var handle in desired)
            {
                if (!_neighborSessions.TryGetValue(handle, out var session)) continue;

                if (session.Connected)
                {
                    _neighborDisconnectedSince.TryRemove(handle, out _);
                    continue;
                }

                var disconnectedSince = _neighborDisconnectedSince.GetOrAdd(handle, DateTime.UtcNow);
                if ((DateTime.UtcNow - disconnectedSince).TotalMilliseconds >= NEIGHBOR_STALL_TIMEOUT_MS)
                    stalled.Add(handle);
            }
            foreach (var handle in stalled)
            {
                if (_neighborSessions.TryRemove(handle, out var dead))
                {
                    _log.Warn($"Neighbour voice session for region handle {handle} stalled disconnected for " +
                              $"{NEIGHBOR_STALL_TIMEOUT_MS / 1000}s, recreating", Client);
                    try { _ = dead.CloseSessionAsync(); } catch { }
                    try { dead.Dispose(); } catch { }
                }
                _neighborDisconnectedSince.TryRemove(handle, out _);
            }

            // Start sessions for newly in-range regions
            foreach (var handle in desired)
            {
                if (_neighborSessions.ContainsKey(handle)) continue;
                ct.ThrowIfCancellationRequested();

                var sim = allSims.Find(s => s.Handle == handle);
                if (sim == null) continue;

                _log.Debug($"Creating neighbour voice session for {sim.Name} (handle={handle})", Client);
                try
                {
                    var nbSession = new VoiceSession(AudioDevice, VoiceSession.ESessionType.LOCAL, Client, _log)
                    {
                        IsPrimary = false,
                        TargetSimulator = sim
                    };
                    _neighborSessions[handle] = nbSession;
                    await nbSession.StartAsync().ConfigureAwait(false);
                    await nbSession.RequestProvisionAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to start neighbour voice session for {sim.Name}: {ex.Message}", Client);
                    _neighborSessions.TryRemove(handle, out _);
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if a simulator advertises WebRTC voice support via its simulator
        /// features, mirroring SL C++ <c>isRegionWebRTCEnabled()</c>.
        /// </summary>
        private static bool IsSimWebRTCEnabled(Simulator sim)
        {
            var features = sim.Features;
            if (features == null) return false;
            var vst = features.Get("VoiceServerType");
            return string.Equals(vst?.AsString(), "webrtc", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        private void CurrentSessionOnOnDataChannelReady()
        {
            _log.Debug("[WebRTC] data channel ready", Client);
            if (_primarySession?.Session == null) return;

            _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
            // VoiceSession.dc.onopen already sends the join message and starts the position loop;
            // no additional join or position send is needed from VoiceManager.
            _log.Debug($"Voice data channel ready for session {_primarySession.Session.SessionId}", Client);
        }

        public bool SendDataChannelMessage(string message)
        {
            return _primarySession?.Session?.TrySendDataChannelString(message) ?? false;
        }

        public void SetPeerMute(UUID peerId, bool mute)
        {
            _primarySession?.Session?.SetPeerMute(peerId, mute);
        }

        public void SetPeerGain(UUID peerId, int gain)
        {
            _primarySession?.Session?.SetPeerGain(peerId, gain);
        }

        public List<UUID> GetKnownPeers()
        {
            try { return _primarySession?.Session?.GetKnownPeers() ?? new List<UUID>(); } catch { return new List<UUID>(); }
        }

        // Get aggregated peer list from all sessions (primary + groups + P2P)
        public List<UUID> GetAllKnownPeers()
        {
            var peers = new HashSet<UUID>();

            if (_primarySession != null)
            {
                try
                {
                    foreach (var peer in _primarySession?.Session?.GetKnownPeers() ?? new List<UUID>())
                    {
                        peers.Add(peer);
                    }
                }
                catch { }
            }

            foreach (var session in _groupSessions.Values)
            {
                try
                {
                    foreach (var peer in session.Session?.GetKnownPeers() ?? new List<UUID>())
                    {
                        peers.Add(peer);
                    }
                }
                catch { }
            }

            foreach (var session in _p2pSessions.Values)
            {
                try
                {
                    foreach (var peer in session.Session?.GetKnownPeers() ?? new List<UUID>())
                    {
                        peers.Add(peer);
                    }
                }
                catch { }
            }

            return peers.ToList();
        }

        public async Task<bool> RequestParcelVoiceInfoAsync()
        {
            var cap = Client.Network?.CurrentSim?.Caps?.CapabilityURI("ParcelVoiceInfoRequest");
            if (cap == null)
            {
                _log.Warn("ParcelVoiceInfoRequest capability not available", Client!);
                return false;
            }

            try
            {
                var (_, responseData) = await Client.HttpCapsClient.PostAsync(cap, OSDFormat.Xml, new OSD(), CancellationToken.None);
                var osd = responseData != null ? OSDParser.Deserialize(responseData) : null;
                if (osd is OSDMap map)
                {
                    var regionName = map.ContainsKey("region_name") ? map["region_name"].AsString() : "";
                    var localID = map.ContainsKey("parcel_local_id") ? map["parcel_local_id"].AsInteger() : -1;
                    var channelURI = "";
                    var credentials = "";

                    if (map.TryGetValue("voice_credentials", out var cred) && cred is OSDMap credMap)
                    {
                        channelURI = credMap.ContainsKey("channel_uri") ? credMap["channel_uri"].AsString() : "";
                        credentials = credMap.ContainsKey("channel_credentials") ? credMap["channel_credentials"].AsString() : "";
                    }

                    // Set on current session if exists
                    if (_primarySession?.Session != null)
                    {
                        _primarySession.Session.ChannelId = channelURI;
                        _primarySession.Session.ChannelCredentials = credentials;
                    }
                    _channelId = channelURI;
                    _channelCredentials = credentials;

                    OnParcelVoiceInfo?.Invoke(regionName, localID, channelURI);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to request parcel voice info: {ex.Message}", Client);
            }
            return false;
        }

        private void RequiredVoiceVersionEventHandler(String capsKey, IMessage message, Simulator simulator)
        {
            var msg = (RequiredVoiceVersionMessage)message;
            if (msg.MajorVersion != 2)
            {
                _log.Error($"WebRTC voice version mismatch! Got {msg.MajorVersion}.{msg.MinorVersion}, expecting 2.x. Disabling WebRTC voice manager", Client);
                _enabled = false;
            }
            else
            {
                _log.Debug($"WebRTC voice version {msg.MajorVersion}.{msg.MinorVersion} supported", Client);
            }
        }

        /// <summary>
        /// Handles ChatterBoxInvitation events that carry a voice invitation (msg.Voice == true).
        /// Extracts the channel URI and credentials from the invitation and raises
        /// <see cref="OnP2PCallIncoming"/> so the UI layer can offer the user an Accept/Decline prompt.
        /// </summary>
        /// <summary>
        /// Handle ChatterBoxSessionStartReply for voice sessions.
        /// SL C++ LLViewerChatterBoxSessionStartReply: on success, populate voice channel info;
        /// on failure for a P2P or group session, fire the appropriate failure event.
        /// </summary>
        private void OnChatterBoxSessionStartReply(string capsKey, IMessage message, Simulator simulator)
        {
            if (!(message is ChatterBoxSessionStartReplyMessage msg)) return;
            if (msg.Success) return;

            var sessionId = msg.SessionID;

            // Find a P2P session whose session-id matches (outgoing call whose server start failed)
            foreach (var kvp in _p2pSessions)
            {
                if (kvp.Value.Session?.SessionId == sessionId)
                {
                    _log.Warn($"P2P voice session start failed for agent {kvp.Key} (session {sessionId})", Client);
                    _ = EndP2PCallAsync(kvp.Key);
                    try { OnP2PCallFailed?.Invoke(kvp.Key, new Exception($"Session start rejected by server")); } catch { }
                    return;
                }
            }

            // Check group sessions
            foreach (var kvp in _groupSessions)
            {
                if (kvp.Value.Session?.SessionId == sessionId)
                {
                    _log.Warn($"Group voice session start failed for group {kvp.Key} (session {sessionId})", Client);
                    _ = LeaveGroupVoiceAsync(kvp.Key);
                    try { OnGroupVoiceJoinFailed?.Invoke(kvp.Key, new Exception($"Session start rejected by server")); } catch { }
                    return;
                }
            }
        }

        /// <summary>
        /// Handle ForceCloseChatterBoxSession — the server is kicking us from a voice/chat session.
        /// SL C++ LLViewerForceCloseChatterBoxSession: close the matching voice session and notify UI.
        /// </summary>
        private void OnForceCloseChatterBoxSession(string capsKey, IMessage message, Simulator simulator)
        {
            if (!(message is ForceCloseChatterBoxSessionMessage msg)) return;

            var sessionId = msg.SessionID;
            _log.Info($"ForceCloseChatterBoxSession: session {sessionId} closed by server (reason: {msg.Reason})", Client);

            // Match against P2P sessions by XOR session id or by stored session object id
            foreach (var kvp in _p2pSessions)
            {
                var p2p = kvp.Value;
                // Compute expected XOR session id for this peer
                var expectedId = Client.Self.AgentID ^ kvp.Key;
                if (p2p.Session?.SessionId == sessionId || expectedId == sessionId)
                {
                    _log.Info($"Force-closing P2P voice session with {kvp.Key}", Client);
                    _ = EndP2PCallAsync(kvp.Key);
                    return;
                }
            }

            // Match against group sessions
            foreach (var kvp in _groupSessions)
            {
                if (kvp.Value.Session?.SessionId == sessionId || kvp.Key == sessionId)
                {
                    _log.Info($"Force-closing group voice session for {kvp.Key}", Client);
                    _ = LeaveGroupVoiceAsync(kvp.Key);
                    return;
                }
            }

            // Pending invite that was forcibly rejected before we answered
            foreach (var kvp in _pendingP2PInvites)
            {
                if (kvp.Value.SessionId == sessionId)
                {
                    _pendingP2PInvites.TryRemove(kvp.Key, out _);
                    _log.Info($"Pending P2P invite from {kvp.Key} force-closed by server", Client);
                    try { OnP2PCallDeclined?.Invoke(kvp.Key); } catch { }
                    return;
                }
            }
        }

        private void OnChatterBoxInvitationForVoice(string capsKey, IMessage message, Simulator simulator)
        {
            if (!(message is ChatterBoxInvitationMessage msg) || !msg.Voice) return;
            if (!_enabled) return;

            var callerId = msg.FromAgentID;
            if (callerId == UUID.Zero) return;

            var channelUri = msg.VoiceChannelUri;
            var credentials = msg.VoiceChannelCredentials;

            _log.Info($"Incoming P2P voice call from {callerId} (session {msg.IMSessionID})", Client);

            // Cache credentials and session-id so accept/decline can use them without a separate round-trip.
            // Keyed by caller UUID; the UI calls AcceptIncomingP2PCallAsync/DeclineIncomingP2PCall.
            _pendingP2PInvites[callerId] = (channelUri, credentials, msg.IMSessionID);

            try { OnP2PCallIncoming?.Invoke(callerId); } catch { }
        }

        // Pending voice invitations: callerId -> (channelUri, credentials, sessionId)
        private readonly ConcurrentDictionary<UUID, (string ChannelUri, string Credentials, UUID SessionId)> _pendingP2PInvites
            = new ConcurrentDictionary<UUID, (string, string, UUID)>();

        /// <summary>
        /// Accept a pending incoming P2P voice call that was signalled via <see cref="OnP2PCallIncoming"/>.
        /// Sends the SL-protocol "accept invitation" CAP POST before connecting the WebRTC session.
        /// </summary>
        public async Task<bool> AcceptIncomingP2PCallAsync(UUID callerId)
        {
            if (!_pendingP2PInvites.TryRemove(callerId, out var invite))
            {
                _log.Warn($"AcceptIncomingP2PCallAsync: no pending invite found for {callerId}", Client);
                return false;
            }

            // Mirror SL's chatterBoxInvitationCoro: POST "accept invitation" to ChatSessionRequest
            // so the server knows we accepted and sends us the agent list for the session.
            await PostChatSessionRequestAsync("accept invitation", invite.SessionId).ConfigureAwait(false);

            return await AcceptP2PCallAsync(callerId, invite.ChannelUri, invite.Credentials).ConfigureAwait(false);
        }

        /// <summary>
        /// Decline a pending incoming P2P voice call.
        /// Sends the SL-protocol "decline p2p voice" CAP POST to notify the server/caller.
        /// </summary>
        public void DeclineIncomingP2PCall(UUID callerId)
        {
            if (_pendingP2PInvites.TryRemove(callerId, out var invite))
            {
                // Mirror SL's processCallResponse(1) for P2P: POST "decline p2p voice"
                _ = PostChatSessionRequestAsync("decline p2p voice", invite.SessionId);
            }
            DeclineP2PCall(callerId);
        }

        // Start playing a WAV file and use it as the microphone/recording source
        public void PlayWavAsMic(string path, bool loop = false)
        {
            try
            {
                AudioDevice.StartFilePlayback(path, loop);
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to start WAV playback as mic: {ex.Message}", Client);
                throw;
            }
        }

        // Stop any active WAV file playback and restore recording source behavior
        public void StopWavAsMic()
        {
            try
            {
                AudioDevice.StopFilePlayback();
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to stop WAV playback as mic: {ex.Message}", Client);
                throw;
            }
        }

        // Fields to track a raw file stream opened by the manager
        private FileStream? _rawFileStream;
        private bool _rawFileStreamActive = false;

        /// <summary>
        /// Play a raw PCM file or WAV as the microphone source. If sampleRate is 48000 the helper will use the internal file-loop
        /// which auto-detects WAV headers; if a different sampleRate is provided the file is opened and streamed with that rate.
        /// </summary>
        public void PlayRawFile(string path, int channels = 1, int sampleRate = 48000, bool loop = true)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException(path);

            try
            {
                // If the caller requests the default 48k path, let the audio layer manage opening/looping (it detects WAV headers)
                if (sampleRate == 48000)
                {
                    AudioDevice.StartRawPcmFileLoop(path, channels, loop);
                    _rawFileStreamActive = false;
                }
                else
                {
                    // Open and pass the stream to the audio device; we must keep the stream open until stopped
                    StopRawFile();
                    _rawFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    AudioDevice.StartRawPcmStream(_rawFileStream, channels, sampleRate);
                    _rawFileStreamActive = true;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to start raw file playback: {ex.Message}", Client);
                throw;
            }
        }

        /// <summary>
        /// Stop playback started by PlayRawFile. Closes any FileStream opened by PlayRawFile when necessary.
        /// </summary>
        public void StopRawFile()
        {
            try
            {
                // Stop any streaming regardless of how it was started
                AudioDevice.StopRawPcmStream();
                AudioDevice.StopFilePlayback();

                if (_rawFileStreamActive && _rawFileStream != null)
                {
                    try { _rawFileStream.Dispose(); } catch { }
                    _rawFileStream = null;
                    _rawFileStreamActive = false;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to stop raw file playback: {ex.Message}", Client);
                throw;
            }
        }

        /// <summary>
        /// Join a group voice channel. This creates a new voice session specifically for the group.
        /// </summary>
        /// <summary>
        /// Joins voice for a conference (ad-hoc multi-agent) session.
        /// Conference sessions use the same MULTIAGENT voice provisioning as group voice.
        /// </summary>
        /// <param name="sessionId">The UUID of the conference session</param>
        /// <returns>True if the join was successful, false otherwise</returns>
        public Task<bool> JoinConferenceVoiceAsync(UUID sessionId) => JoinGroupVoiceAsync(sessionId);

        /// <summary>Leaves voice for a conference (ad-hoc multi-agent) session.</summary>
        public Task LeaveConferenceVoiceAsync(UUID sessionId) => LeaveGroupVoiceAsync(sessionId);

        /// <param name="groupId">The UUID of the group to join voice for</param>
        /// <returns>True if the join was successful, false otherwise</returns>
        public async Task<bool> JoinGroupVoiceAsync(UUID groupId)
        {
            if (groupId == UUID.Zero)
            {
                _log.Warn("Cannot join group voice: invalid group ID", Client);
                return false;
            }

            if (!_enabled)
            {
                _log.Warn("WebRTC voice is disabled", Client);
                return false;
            }

            if (_groupSessions.ContainsKey(groupId))
            {
                _log.Info($"Already in group voice session for {groupId}", Client);
                return true;
            }

            try
            {
                _log.Info($"Joining group voice for {groupId}", Client);

                // Request group voice credentials from the simulator
                var (channelUri, credentials) = await RequestGroupVoiceInfo(groupId).ConfigureAwait(false);

                if (string.IsNullOrEmpty(channelUri) || string.IsNullOrEmpty(credentials))
                {
                    _log.Warn($"Failed to get group voice credentials for {groupId}", Client);
                    try { OnGroupVoiceJoinFailed?.Invoke(groupId, new Exception("No voice credentials received")); } catch { }
                    return false;
                }

                // Create a new multi-agent voice session for the group
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MULTIAGENT, Client, _log);
                session.ChannelId = channelUri;
                session.ChannelCredentials = credentials;

                var groupSession = new GroupVoiceSession
                {
                    GroupId = groupId,
                    Session = session,
                    ChannelId = channelUri,
                    ChannelCredentials = credentials,
                    JoinedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };

                // Wire up events (aggregate events from all sessions)
                session.OnPeerJoined += (id) => PeerJoined?.Invoke(id);
                session.OnPeerLeft += (id) => PeerLeft?.Invoke(id);
                session.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
                session.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);
                session.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
                session.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
                session.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
                session.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

                // Store the session
                _groupSessions[groupId] = groupSession;

                // Start and provision the session
                await session.StartAsync().ConfigureAwait(false);
                var provisioned = await session.RequestProvisionAsync().ConfigureAwait(false);

                if (provisioned)
                {
                    _log.Info($"Successfully joined group voice for {groupId}", Client);
                    try { OnGroupVoiceJoined?.Invoke(groupId); } catch { }
                    return true;
                }
                else
                {
                    _log.Warn($"Failed to provision group voice session for {groupId}", Client);
                    _groupSessions.TryRemove(groupId, out _);
                    try { OnGroupVoiceJoinFailed?.Invoke(groupId, new Exception("Provisioning failed")); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception joining group voice for {groupId}: {ex.Message}", Client);
                _groupSessions.TryRemove(groupId, out _);
                try { OnGroupVoiceJoinFailed?.Invoke(groupId, ex); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Leave a group voice channel.
        /// </summary>
        /// <param name="groupId">The UUID of the group to leave voice for</param>
        public async Task LeaveGroupVoiceAsync(UUID groupId)
        {
            if (!_groupSessions.TryRemove(groupId, out var groupSession))
            {
                _log.Debug($"Not in group voice session for {groupId}", Client);
                return;
            }

            try
            {
                _log.Info($"Leaving group voice for {groupId}", Client);

                // Close and dispose the session
                if (groupSession?.Session != null)
                {
                    await groupSession.Session.CloseSessionAsync().ConfigureAwait(false);
                    groupSession.Session.Dispose();
                }

                try { OnGroupVoiceLeft?.Invoke(groupId); } catch { }
            }
            catch (Exception ex)
            {
                _log.Error($"Error leaving group voice for {groupId}: {ex.Message}", Client);
            }
        }

        /// <summary>
        /// Check if currently connected to a group voice channel.
        /// </summary>
        /// <param name="groupId">The UUID of the group to check</param>
        /// <returns>True if connected to the group voice channel</returns>
        public bool IsInGroupVoice(UUID groupId)
        {
            return _groupSessions.ContainsKey(groupId);
        }

        /// <summary>
        /// Get a list of all active group voice sessions.
        /// </summary>
        /// <returns>List of group UUIDs with active voice sessions</returns>
        public List<UUID> GetActiveGroupVoiceSessions()
        {
            return _groupSessions.Keys.ToList();
        }

        /// <summary>
        /// Request group voice channel information from the simulator.
        /// </summary>
        private async Task<(string? channelUri, string? credentials)> RequestGroupVoiceInfo(UUID groupId)
        {
            var cap = Client.Network?.CurrentSim?.Caps?.CapabilityURI("ChatSessionRequest");
            if (cap == null)
            {
                _log.Warn("ChatSessionRequest capability not available for group voice", Client!);
                return (null, null);
            }

            var payload = new OSDMap
            {
                ["method"] = "get voice channel info",
                ["session-id"] = groupId
            };

            try
            {
                var (_, responseData) = await Client.HttpCapsClient.PostAsync(cap, OSDFormat.Xml, payload, CancellationToken.None);
                var osd = responseData != null ? OSDParser.Deserialize(responseData) : null;
                if (osd is OSDMap map)
                {
                    var channelUri = "";
                    var credentials = "";

                    if (map.TryGetValue("voice_credentials", out var cred) && cred is OSDMap credMap)
                    {
                        channelUri = credMap.ContainsKey("channel_uri") ? credMap["channel_uri"].AsString() : "";
                        credentials = credMap.ContainsKey("channel_credentials") ? credMap["channel_credentials"].AsString() : "";
                    }

                    _log.Debug($"Group voice credentials received for {groupId}: {channelUri}", Client!);
                    return (channelUri, credentials);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to request group voice info for {groupId}: {ex.Message}", Client!);
            }

            return (null, null);
        }

        /// <summary>
        /// Get known peers in a specific group voice session.
        /// </summary>
        /// <param name="groupId">The UUID of the group</param>
        /// <returns>List of peer UUIDs in the group voice session, or empty list if not in group voice</returns>
        public List<UUID> GetGroupVoicePeers(UUID groupId)
        {
            if (_groupSessions.TryGetValue(groupId, out var groupSession))
            {
                try
                {
                    return groupSession.Session?.GetKnownPeers() ?? new List<UUID>();
                }
                catch { }
            }
            return new List<UUID>();
        }

        /// <summary>
        /// Set peer mute state in a specific group voice session.
        /// </summary>
        /// <param name="groupId">The UUID of the group</param>
        /// <param name="peerId">The UUID of the peer to mute/unmute</param>
        /// <param name="mute">True to mute, false to unmute</param>
        /// <returns>True if the mute state was set successfully</returns>
        public bool SetGroupVoicePeerMute(UUID groupId, UUID peerId, bool mute)
        {
            if (_groupSessions.TryGetValue(groupId, out var groupSession))
            {
                return groupSession.Session?.SetPeerMute(peerId, mute) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Set peer gain level in a specific group voice session.
        /// </summary>
        /// <param name="groupId">The UUID of the group</param>
        /// <param name="peerId">The UUID of the peer</param>
        /// <param name="gain">Gain level (0-100)</param>
        /// <returns>True if the gain was set successfully</returns>
        public bool SetGroupVoicePeerGain(UUID groupId, UUID peerId, int gain)
        {
            if (_groupSessions.TryGetValue(groupId, out var groupSession))
            {
                return groupSession.Session?.SetPeerGain(peerId, gain) ?? false;
            }
            return false;
        }

        #region P2P Voice Calls

        /// <summary>
        /// Initiate a P2P voice call with another agent.
        /// </summary>
        /// <param name="agentId">The UUID of the agent to call</param>
        /// <returns>True if the call was initiated successfully</returns>
        public async Task<bool> StartP2PCallAsync(UUID agentId)
        {
            if (agentId == UUID.Zero)
            {
                _log.Warn("Cannot start P2P call: invalid agent ID", Client);
                return false;
            }

            if (!_enabled)
            {
                _log.Warn("WebRTC voice is disabled", Client);
                return false;
            }

            if (_p2pSessions.ContainsKey(agentId))
            {
                _log.Info($"Already in P2P call with {agentId}", Client);
                return true;
            }

            try
            {
                _log.Info($"Starting P2P voice call with {agentId}", Client);

                // Request P2P voice credentials from the simulator
                var (channelUri, credentials, sessionId) = await RequestP2PVoiceInfo(agentId).ConfigureAwait(false);

                if (string.IsNullOrEmpty(channelUri) || string.IsNullOrEmpty(credentials))
                {
                    _log.Warn($"Failed to get P2P voice credentials for {agentId}", Client);
                    try { OnP2PCallFailed?.Invoke(agentId, new Exception("No voice credentials received")); } catch { }
                    return false;
                }

                // Create a new multi-agent voice session for the P2P call
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MULTIAGENT, Client, _log);
                session.ChannelId = channelUri;
                session.ChannelCredentials = credentials;

                var p2pSession = new P2PVoiceSession
                {
                    AgentId = agentId,
                    Session = session,
                    ChannelId = channelUri,
                    ChannelCredentials = credentials,
                    CallStarted = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsOutgoing = true
                };

                // Wire up events (aggregate events from all sessions)
                session.OnPeerJoined += (id) => 
                {
                    PeerJoined?.Invoke(id);
                    if (id == agentId)
                    {
                        try { OnP2PCallAccepted?.Invoke(agentId); } catch { }
                    }
                };
                session.OnPeerLeft += (id) => 
                {
                    PeerLeft?.Invoke(id);
                    if (id == agentId)
                    {
                        _ = EndP2PCallAsync(agentId);
                    }
                };
                session.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
                session.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);
                session.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
                session.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
                session.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
                session.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

                // Store the session
                _p2pSessions[agentId] = p2pSession;

                // Start and provision the session
                await session.StartAsync().ConfigureAwait(false);
                var provisioned = await session.RequestProvisionAsync().ConfigureAwait(false);

                if (provisioned)
                {
                    // Mirror SL's chatterBoxInvitationCoro: the outgoing caller also sends
                    // "accept invitation" so the server adds us as a session participant.
                    await PostChatSessionRequestAsync("accept invitation", sessionId).ConfigureAwait(false);

                    _log.Info($"Successfully started P2P call with {agentId}", Client);
                    try { OnP2PCallStarted?.Invoke(agentId); } catch { }
                    return true;
                }
                else
                {
                    _log.Warn($"Failed to provision P2P call session for {agentId}", Client);
                    _p2pSessions.TryRemove(agentId, out _);
                    try { OnP2PCallFailed?.Invoke(agentId, new Exception("Provisioning failed")); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception starting P2P call with {agentId}: {ex.Message}", Client);
                _p2pSessions.TryRemove(agentId, out _);
                try { OnP2PCallFailed?.Invoke(agentId, ex); } catch { }
                return false;
            }
        }

        /// <summary>
        /// End a P2P voice call with another agent.
        /// </summary>
        /// <param name="agentId">The UUID of the agent to end the call with</param>
        public async Task EndP2PCallAsync(UUID agentId)
        {
            if (!_p2pSessions.TryRemove(agentId, out var p2pSession))
            {
                _log.Debug($"Not in P2P call with {agentId}", Client);
                return;
            }

            try
            {
                _log.Info($"Ending P2P voice call with {agentId}", Client);

                // Close and dispose the session
                if (p2pSession?.Session != null)
                {
                    await p2pSession.Session.CloseSessionAsync().ConfigureAwait(false);
                    p2pSession.Session.Dispose();
                }

                try { OnP2PCallEnded?.Invoke(agentId); } catch { }
            }
            catch (Exception ex)
            {
                _log.Error($"Error ending P2P call with {agentId}: {ex.Message}", Client);
            }
        }

        /// <summary>
        /// Accept an incoming P2P voice call.
        /// </summary>
        /// <param name="agentId">The UUID of the agent calling</param>
        /// <param name="channelUri">The channel URI from the call invitation</param>
        /// <param name="credentials">The credentials from the call invitation</param>
        /// <returns>True if the call was accepted successfully</returns>
        public async Task<bool> AcceptP2PCallAsync(UUID agentId, string channelUri, string credentials)
        {
            if (agentId == UUID.Zero || string.IsNullOrEmpty(channelUri) || string.IsNullOrEmpty(credentials))
            {
                _log.Warn("Cannot accept P2P call: invalid parameters", Client);
                return false;
            }

            if (_p2pSessions.ContainsKey(agentId))
            {
                _log.Info($"Already in P2P call with {agentId}", Client);
                return true;
            }

            try
            {
                _log.Info($"Accepting P2P voice call from {agentId}", Client);

                // Create a new multi-agent voice session for the P2P call
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MULTIAGENT, Client, _log);
                session.ChannelId = channelUri;
                session.ChannelCredentials = credentials;

                var p2pSession = new P2PVoiceSession
                {
                    AgentId = agentId,
                    Session = session,
                    ChannelId = channelUri,
                    ChannelCredentials = credentials,
                    CallStarted = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsOutgoing = false
                };

                // Wire up events
                session.OnPeerJoined += (id) => PeerJoined?.Invoke(id);
                session.OnPeerLeft += (id) => 
                {
                    PeerLeft?.Invoke(id);
                    if (id == agentId)
                    {
                        _ = EndP2PCallAsync(agentId);
                    }
                };
                session.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
                session.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);
                session.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
                session.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
                session.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
                session.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

                // Store the session
                _p2pSessions[agentId] = p2pSession;

                // Start and provision the session
                await session.StartAsync().ConfigureAwait(false);
                var provisioned = await session.RequestProvisionAsync().ConfigureAwait(false);

                if (provisioned)
                {
                    _log.Info($"Successfully accepted P2P call from {agentId}", Client);
                    try { OnP2PCallAccepted?.Invoke(agentId); } catch { }
                    try { OnP2PCallStarted?.Invoke(agentId); } catch { }
                    return true;
                }
                else
                {
                    _log.Warn($"Failed to provision P2P call session from {agentId}", Client);
                    _p2pSessions.TryRemove(agentId, out _);
                    try { OnP2PCallFailed?.Invoke(agentId, new Exception("Provisioning failed")); } catch { }
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Exception accepting P2P call from {agentId}: {ex.Message}", Client);
                _p2pSessions.TryRemove(agentId, out _);
                try { OnP2PCallFailed?.Invoke(agentId, ex); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Decline an incoming P2P voice call.
        /// </summary>
        /// <param name="agentId">The UUID of the agent calling</param>
        public void DeclineP2PCall(UUID agentId)
        {
            _log.Info($"Declining P2P voice call from {agentId}", Client);
            try { OnP2PCallDeclined?.Invoke(agentId); } catch { }
        }

        /// <summary>
        /// Check if currently in a P2P voice call with an agent.
        /// </summary>
        /// <param name="agentId">The UUID of the agent</param>
        /// <returns>True if in a P2P call with the agent</returns>
        public bool IsInP2PCall(UUID agentId)
        {
            return _p2pSessions.ContainsKey(agentId);
        }

        /// <summary>
        /// Get a list of all active P2P voice calls.
        /// </summary>
        /// <returns>List of agent UUIDs with active P2P calls</returns>
        public List<UUID> GetActiveP2PCalls()
        {
            return _p2pSessions.Keys.ToList();
        }

        /// <summary>
        /// Get information about a P2P call.
        /// </summary>
        /// <param name="agentId">The UUID of the agent</param>
        /// <returns>Call information or null if not in call</returns>
        public (bool IsActive, bool IsOutgoing, DateTime? CallStarted) GetP2PCallInfo(UUID agentId)
        {
            if (_p2pSessions.TryGetValue(agentId, out var session))
            {
                return (true, session.IsOutgoing, session.CallStarted);
            }
            return (false, false, null);
        }

        /// <summary>
        /// Set mute state for a P2P call.
        /// </summary>
        /// <param name="agentId">The UUID of the agent</param>
        /// <param name="mute">True to mute, false to unmute</param>
        /// <returns>True if the mute state was set successfully</returns>
        public bool SetP2PCallMute(UUID agentId, bool mute)
        {
            if (_p2pSessions.TryGetValue(agentId, out var p2pSession))
            {
                return p2pSession?.Session?.SetPeerMute(agentId, mute) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Set volume/gain for a P2P call.
        /// </summary>
        /// <param name="agentId">The UUID of the agent</param>
        /// <param name="gain">Gain level (0-100)</param>
        /// <returns>True if the gain was set successfully</returns>
        public bool SetP2PCallGain(UUID agentId, int gain)
        {
            if (_p2pSessions.TryGetValue(agentId, out var p2pSession))
            {
                return p2pSession?.Session?.SetPeerGain(agentId, gain) ?? false;
            }
            return false;
        }

        /// <summary>
        /// POST a method call to the ChatSessionRequest capability.
        /// Mirrors SL's chatterBoxInvitationCoro / inline postData patterns.
        /// </summary>
        private async Task PostChatSessionRequestAsync(string method, UUID sessionId)
        {
            var cap = Client.Network?.CurrentSim?.Caps?.CapabilityURI("ChatSessionRequest");
            if (cap == null)
            {
                _log.Warn($"ChatSessionRequest cap not available for method={method}", Client);
                return;
            }

            var payload = new OSDMap
            {
                ["method"]     = method,
                ["session-id"] = sessionId
            };

            try
            {
                await Client.HttpCapsClient.PostAsync(cap, OSDFormat.Xml, payload, CancellationToken.None).ConfigureAwait(false);
                _log.Debug($"ChatSessionRequest {method} OK", Client);
            }
            catch (Exception ex) { _log.Warn($"ChatSessionRequest {method} exception: {ex.Message}", Client); }
        }

        /// <summary>
        /// Request P2P voice channel information from the simulator.
        /// Mirrors SL's startP2PVoiceCoro: session-id = selfAgent XOR otherAgent,
        /// params = otherParticipantId, alt_params.voice_server_type = "webrtc".
        /// Returns (channelUri, credentials, sessionId) or nulls on failure.
        /// </summary>
        private async Task<(string? channelUri, string? credentials, UUID sessionId)> RequestP2PVoiceInfo(UUID agentId)
        {
            var cap = Client.Network?.CurrentSim?.Caps?.CapabilityURI("ChatSessionRequest");
            if (cap == null)
            {
                _log.Warn("ChatSessionRequest capability not available for P2P voice", Client);
                return (null, null, UUID.Zero);
            }

            // SL computes the P2P session UUID as selfAgentId XOR otherAgentId
            var selfId = Client.Self.AgentID;
            var sessionId = selfId ^ agentId;

            var altParams = new OSDMap { ["voice_server_type"] = "webrtc" };
            var payload = new OSDMap
            {
                ["method"]     = "start p2p voice",
                ["session-id"] = sessionId,
                ["params"]     = agentId,
                ["alt_params"] = altParams
            };

            try
            {
                var (_, responseData) = await Client.HttpCapsClient.PostAsync(cap, OSDFormat.Xml, payload, CancellationToken.None);
                var osd = responseData != null ? OSDParser.Deserialize(responseData) : null;
                if (osd is OSDMap map)
                {
                    var channelUri = "";
                    var credentials = "";

                    if (map.TryGetValue("voice_credentials", out var cred) && cred is OSDMap credMap)
                    {
                        channelUri = credMap.ContainsKey("channel_uri") ? credMap["channel_uri"].AsString() : "";
                        credentials = credMap.ContainsKey("channel_credentials") ? credMap["channel_credentials"].AsString() : "";
                    }

                    _log.Debug($"P2P voice credentials received for {agentId}: {channelUri}", Client);
                    return (channelUri, credentials, sessionId);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to request P2P voice info for {agentId}: {ex.Message}", Client);
            }

            return (null, null, UUID.Zero);
        }

        #endregion
    }
}
