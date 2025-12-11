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
using LitJson;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
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
            public VoiceSession Session { get; set; }
            public bool IsPrimary { get; set; }
            public DateTime LastActivity { get; set; }
        }

        // Internal class to track a group voice session
        private class GroupVoiceSession
        {
            public UUID GroupId { get; set; }
            public VoiceSession Session { get; set; }
            public string ChannelId { get; set; }
            public string ChannelCredentials { get; set; }
            public DateTime JoinedAt { get; set; }
            public DateTime LastActivity { get; set; }
        }

        // Internal class to track a P2P voice call session
        private class P2PVoiceSession
        {
            public UUID AgentId { get; set; }
            public VoiceSession Session { get; set; }
            public string ChannelId { get; set; }
            public string ChannelCredentials { get; set; }
            public DateTime CallStarted { get; set; }
            public DateTime LastActivity { get; set; }
            public bool IsOutgoing { get; set; }
        }

        private readonly GridClient Client;
        public readonly Sdl3Audio AudioDevice;
        private readonly IVoiceLogger _log;
        
        // Track primary and adjacent region sessions
        private RegionVoiceSession _primarySession;
        private readonly ConcurrentDictionary<ulong, RegionVoiceSession> _adjacentSessions = new ConcurrentDictionary<ulong, RegionVoiceSession>();
        
        // Track group voice sessions
        private readonly ConcurrentDictionary<UUID, GroupVoiceSession> _groupSessions = new ConcurrentDictionary<UUID, GroupVoiceSession>();
        
        // Track P2P voice call sessions
        private readonly ConcurrentDictionary<UUID, P2PVoiceSession> _p2pSessions = new ConcurrentDictionary<UUID, P2PVoiceSession>();
        
        private bool _enabled = true;

        // Store channel info from parcel voice info
        private string _channelId;
        private string _channelCredentials;

        // Track current region to detect changes
        private ulong _currentRegionHandle = 0;
        private readonly SemaphoreSlim _regionTransitionLock = new SemaphoreSlim(1, 1);
        private bool _isTransitioning = false;

        // Background task for managing adjacent regions
        private CancellationTokenSource _adjacentRegionCts;
        private Task _adjacentRegionTask;

        // Expose session/channel info for primary session
        public UUID SessionId => _primarySession?.Session?.SessionId ?? UUID.Zero;
        public string ChannelId => _primarySession?.Session?.ChannelId ?? _channelId;
        public string ChannelCredentials => _primarySession?.Session?.ChannelCredentials ?? _channelCredentials;
        
        // Expose SDP and connection state for primary session
        public string sdpLocal => _primarySession?.Session?.SdpLocal;
        public string sdpRemote => _primarySession?.Session?.SdpRemote;
        public bool connected => _primarySession?.Session?.Connected ?? false;

        // Cross-region events
        public event Action OnRegionChangeDetected;
        public event Action OnRegionTransitionCompleted;
        public event Action<Exception> OnRegionTransitionFailed;

        // Adjacent region events
        public event Action<ulong> OnAdjacentRegionConnected;
        public event Action<ulong> OnAdjacentRegionDisconnected;

        // Group voice events
        public event Action<UUID> OnGroupVoiceJoined;
        public event Action<UUID> OnGroupVoiceLeft;
        public event Action<UUID, Exception> OnGroupVoiceJoinFailed;

        // P2P voice call events
        public event Action<UUID> OnP2PCallStarted;
        public event Action<UUID> OnP2PCallEnded;
        public event Action<UUID, Exception> OnP2PCallFailed;
        public event Action<UUID> OnP2PCallIncoming;
        public event Action<UUID> OnP2PCallAccepted;
        public event Action<UUID> OnP2PCallDeclined;

        // Expose data-channel / voice events to clients (raw)
        public event Action<UUID> PeerJoined;
        public event Action<UUID> PeerLeft;
        public event Action<UUID, OSDMap> PeerPositionUpdated;
        public event Action<List<UUID>> PeerListUpdated;
        // Expose connection events to clients
        public event Action PeerConnectionReady;
        public event Action PeerConnectionClosed;

        // Expose typed events per PDF
        public event Action<UUID, VoiceSession.PeerAudioState> PeerAudioUpdated;
        public event Action<UUID, VoiceSession.AvatarPosition> PeerPositionUpdatedTyped;
        public event Action<Dictionary<UUID, bool>> MuteMapReceived;
        public event Action<Dictionary<UUID, int>> GainMapReceived;
        public event Action<string, int, string> OnParcelVoiceInfo;

        public VoiceManager(GridClient client, IVoiceLogger logger = null)
        {
            Client = client;
            AudioDevice = new Sdl3Audio();
            _log = logger ?? new OpenMetaverseVoiceLogger();
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);
            
            // Register for region change events
            Client.Network.SimChanged += OnSimChanged;
            Client.Self.TeleportProgress += OnTeleport;
            
            // Register for adjacent region detection
            Client.Network.SimConnected += OnSimConnected;
            Client.Network.SimDisconnected += OnSimDisconnected;
        }

        private void OnSimChanged(object sender, SimChangedEventArgs e)
        {
            _ = HandleRegionChange(e.PreviousSimulator);
        }

        private void OnTeleport(object sender, TeleportEventArgs e)
        {
            if (e.Status == TeleportStatus.Finished)
            {
                _ = HandleRegionChange(null);
            }
        }

        private async Task HandleRegionChange(Simulator previousSim)
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

                // Store current session type and channel info before reprovisioning
                var wasMultiAgent = !string.IsNullOrEmpty(_primarySession.Session.ChannelId);
                var preservedChannelId = _primarySession.Session.ChannelId;
                var preservedCredentials = _primarySession.Session.ChannelCredentials;

                // Request new parcel voice info for the new region
                var parcelInfoSuccess = await RequestParcelVoiceInfo();
                
                // For cross-region multi-agent voice, preserve channel credentials if the new
                // region doesn't provide new ones (allows voice continuity across regions)
                if (wasMultiAgent && string.IsNullOrEmpty(ChannelId) && !string.IsNullOrEmpty(preservedChannelId))
                {
                    _log.Info("Preserving channel credentials for cross-region continuity", Client);
                    _channelId = preservedChannelId;
                    _channelCredentials = preservedCredentials;
                }

                // Reprovision the voice session for the new region
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
            bool wasRecording = AudioDevice?.Source != null && AudioDevice.RecordingActive;
            bool wasPlaybackActive = AudioDevice?.EndPoint != null && AudioDevice.PlaybackActive;

            try
            {
                // Stop audio before closing session
                if (AudioDevice != null)
                {
                    try { AudioDevice.StopRecording(); } catch { }
                    try { await AudioDevice.StopPlaybackAsync().ConfigureAwait(false); } catch { }
                }

                // Close all adjacent sessions (they're for the old region's neighbors)
                foreach (var kvp in _adjacentSessions)
                {
                    try
                    {
                        await kvp.Value.Session.CloseSession().ConfigureAwait(false);
                        kvp.Value.Session.Dispose();
                    }
                    catch { }
                }
                _adjacentSessions.Clear();

                // Close the current primary session
                if (_primarySession != null)
                {
                    try
                    {
                        _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
                        await _primarySession.Session.CloseSession().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Error closing session during reprovision: {ex.Message}", Client);
                    }
                }

                // Determine session type for new region
                var sessionType = VoiceSession.ESessionType.LOCAL;
                if (!string.IsNullOrEmpty(ChannelId))
                {
                    sessionType = VoiceSession.ESessionType.MUTLIAGENT;
                    _log.Info("Using multi-agent voice for new region", Client);
                }
                else
                {
                    _log.Info("Using local voice for new region", Client);
                }

                // Create new primary session
                var session = new VoiceSession(AudioDevice, sessionType, Client, _log);

                // Set preserved or new channel info
                if (!string.IsNullOrEmpty(ChannelId))
                {
                    session.ChannelId = ChannelId;
                    session.ChannelCredentials = ChannelCredentials;
                }

                _primarySession = new RegionVoiceSession
                {
                    RegionHandle = Client.Network.CurrentSim?.Handle ?? 0,
                    Session = session,
                    IsPrimary = true,
                    LastActivity = DateTime.UtcNow
                };

                // Wire events
                session.OnDataChannelReady += CurrentSessionOnOnDataChannelReady;
                session.OnPeerConnectionReady += () => { try { PeerConnectionReady?.Invoke(); } catch { } };
                session.OnPeerConnectionClosed += () => { try { PeerConnectionClosed?.Invoke(); } catch { } };

                // Forward session events
                session.OnPeerJoined += (id) => PeerJoined?.Invoke(id);
                session.OnPeerLeft += (id) => PeerLeft?.Invoke(id);
                session.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
                session.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);

                // Forward typed events
                session.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
                session.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
                session.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
                session.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

                // Start new session
                await session.StartAsync().ConfigureAwait(false);
                await session.RequestProvision().ConfigureAwait(false);

                // Restore audio state
                await Task.Delay(500).ConfigureAwait(false);

                if (wasRecording && AudioDevice?.Source != null)
                {
                    try { AudioDevice.StartRecording(); } catch (Exception ex) 
                    { 
                        _log.Warn($"Failed to restart recording after region change: {ex.Message}", Client); 
                    }
                }
                
                if (wasPlaybackActive && AudioDevice?.EndPoint != null)
                {
                    try { await AudioDevice.StartPlaybackAsync().ConfigureAwait(false); } catch (Exception ex) 
                    { 
                        _log.Warn($"Failed to restart playback after region change: {ex.Message}", Client); 
                    }
                }

                _log.Info("Voice session reprovisioned successfully for new region", Client);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to reprovision voice session: {ex.Message}", Client);
                throw;
            }
        }

        private void OnSimConnected(object sender, SimConnectedEventArgs e)
        {
            // Check if this is an adjacent region (not the current sim)
            if (e.Simulator != Client.Network.CurrentSim && _primarySession != null)
            {
                _ = HandleAdjacentRegionConnected(e.Simulator);
            }
        }

        private void OnSimDisconnected(object sender, SimDisconnectedEventArgs e)
        {
            // Clean up voice session for disconnected adjacent region
            if (e.Simulator != null && _adjacentSessions.TryRemove(e.Simulator.Handle, out var session))
            {
                _log.Info($"Cleaning up voice session for disconnected adjacent region {e.Simulator.Handle:X}", Client);
                try
                {
                    _ = session.Session.CloseSession();
                    session.Session.Dispose();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Error disposing adjacent session: {ex.Message}", Client);
                }
                
                try
                {
                    OnAdjacentRegionDisconnected?.Invoke(e.Simulator.Handle);
                }
                catch { }
            }
        }

        private async Task HandleAdjacentRegionConnected(Simulator simulator)
        {
            if (simulator == null || !_enabled) return;

            try
            {
                _log.Info($"Adjacent region detected: {simulator.Name} ({simulator.Handle:X})", Client);

                // Create voice session for adjacent region
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.LOCAL, Client, _log);
                
                var regionSession = new RegionVoiceSession
                {
                    RegionHandle = simulator.Handle,
                    Session = session,
                    IsPrimary = false,
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

                // Store session
                _adjacentSessions[simulator.Handle] = regionSession;

                // Start and provision the session
                await session.StartAsync().ConfigureAwait(false);
                await session.RequestProvision().ConfigureAwait(false);

                _log.Info($"Adjacent region voice session established: {simulator.Name}", Client);
                
                try
                {
                    OnAdjacentRegionConnected?.Invoke(simulator.Handle);
                }
                catch { }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to establish voice for adjacent region: {ex.Message}", Client);
            }
        }

        public async Task<bool> ConnectPrimaryRegion()
        {
            Console.WriteLine("[WebRTC] ConnectPrimaryRegion started");
            if (!Client.Network.Connected) { return false; }
            if (!_enabled) 
            {
                _log.Warn("WebRTC voice is disabled due to unsupported voice version", Client);
                return false;
            }

            // Initialize current region handle
            _currentRegionHandle = Client.Network.CurrentSim?.Handle ?? 0;

            // Request parcel voice info to determine if we need multi-agent or local
            var parcelInfoSuccess = await RequestParcelVoiceInfo();
            var sessionType = VoiceSession.ESessionType.LOCAL;
            if (parcelInfoSuccess && !string.IsNullOrEmpty(ChannelId))
            {
                sessionType = VoiceSession.ESessionType.MUTLIAGENT;
                _log.Info("Using multi-agent voice for private parcel", Client);
            }
            else
            {
                _log.Info("Using local voice for region", Client);
            }

            var session = new VoiceSession(AudioDevice, sessionType, Client, _log);

            // Set channel info if available
            if (!string.IsNullOrEmpty(ChannelId))
            {
                session.ChannelId = ChannelId;
                session.ChannelCredentials = ChannelCredentials;
            }

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
            session.OnPeerConnectionReady += () => { try { PeerConnectionReady?.Invoke(); } catch { } };
            session.OnPeerConnectionClosed += () => { try { PeerConnectionClosed?.Invoke(); } catch { } };

            // forward session events (raw)
            session.OnPeerJoined += (id) => PeerJoined?.Invoke(id);
            session.OnPeerLeft += (id) => PeerLeft?.Invoke(id);
            session.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
            session.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);

            // forward typed events
            session.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
            session.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
            session.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
            session.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

            await session.StartAsync().ConfigureAwait(false);
            var provisioned = await session.RequestProvision().ConfigureAwait(false);
            
            if (provisioned)
            {
                // Start adjacent region management task
                StartAdjacentRegionManagement();
            }
            
            return provisioned;
        }

        private void StartAdjacentRegionManagement()
        {
            if (_adjacentRegionTask != null && !_adjacentRegionTask.IsCompleted) return;

            _adjacentRegionCts = new CancellationTokenSource();
            var token = _adjacentRegionCts.Token;

            _adjacentRegionTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(5000, token).ConfigureAwait(false);
                        await ManageAdjacentRegions().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _log.Error($"Adjacent region management error: {ex.Message}", Client);
                    }
                }
            }, token);
        }

        private async Task ManageAdjacentRegions()
        {
            if (_primarySession == null) return;

            // Get list of currently connected simulators (excluding primary)
            var adjacentHandles = new HashSet<ulong>();
            lock (Client.Network.Simulators)
            {
                foreach (var sim in Client.Network.Simulators)
                {
                    if (sim != Client.Network.CurrentSim && sim.Connected)
                    {
                        adjacentHandles.Add(sim.Handle);
                    }
                }
            }

            // Clean up sessions for regions that are no longer adjacent
            var staleSessions = _adjacentSessions.Keys.Where(h => !adjacentHandles.Contains(h)).ToList();
            foreach (var handle in staleSessions)
            {
                if (_adjacentSessions.TryRemove(handle, out var session))
                {
                    _log.Info($"Removing voice session for no-longer-adjacent region {handle:X}", Client);
                    try
                    {
                        await session.Session.CloseSession().ConfigureAwait(false);
                        session.Session.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Error disposing stale adjacent session: {ex.Message}", Client);
                    }
                    
                    try
                    {
                        OnAdjacentRegionDisconnected?.Invoke(handle);
                    }
                    catch { }
                }
            }

            // Ensure voice sessions exist for all adjacent regions
            foreach (var handle in adjacentHandles)
            {
                if (!_adjacentSessions.ContainsKey(handle))
                {
                    var sim = Client.Network.Simulators.FirstOrDefault(s => s.Handle == handle);
                    if (sim != null)
                    {
                        await HandleAdjacentRegionConnected(sim).ConfigureAwait(false);
                    }
                }
            }
        }

        public void Disconnect()
        {
            // Stop adjacent region management
            try
            {
                _adjacentRegionCts?.Cancel();
                _adjacentRegionTask?.Wait(500);
                _adjacentRegionCts?.Dispose();
                _adjacentRegionCts = null;
                _adjacentRegionTask = null;
            }
            catch { }

            // Close all P2P voice calls
            foreach (var kvp in _p2pSessions)
            {
                try
                {
                    _ = kvp.Value.Session.CloseSession();
                    kvp.Value.Session.Dispose();
                }
                catch { }
            }
            _p2pSessions.Clear();

            // Close all group voice sessions
            foreach (var kvp in _groupSessions)
            {
                try
                {
                    _ = kvp.Value.Session.CloseSession();
                    kvp.Value.Session.Dispose();
                }
                catch { }
            }
            _groupSessions.Clear();

            // Close all adjacent sessions
            foreach (var kvp in _adjacentSessions)
            {
                try
                {
                    _ = kvp.Value.Session.CloseSession();
                    kvp.Value.Session.Dispose();
                }
                catch { }
            }
            _adjacentSessions.Clear();

            // Close primary session
            if (_primarySession != null)
            {
                try
                {
                    _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
                    _ = _primarySession.Session.CloseSession();
                    _primarySession.Session.Dispose();
                }
                catch { }
                _primarySession = null;
            }

            // Unregister region change handlers
            try
            {
                Client.Network.SimChanged -= OnSimChanged;
                Client.Self.TeleportProgress -= OnTeleport;
                Client.Network.SimConnected -= OnSimConnected;
                Client.Network.SimDisconnected -= OnSimDisconnected;
            }
            catch { }

            _channelId = null;
            _channelCredentials = null;
            _currentRegionHandle = 0;
        }

        // Helpers that forward to primary session
        public void SendGlobalPosition()
        {
            var pos = Client.Self.GlobalPosition;
            var h = Client.Self.RelativeRotation * 100;
            JsonWriter jw = new JsonWriter();
            jw.WriteObjectStart();
            jw.WritePropertyName("sp");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(pos.X);
            jw.WritePropertyName("y");
            jw.Write(pos.Y);
            jw.WritePropertyName("z");
            jw.Write(pos.Z);
            jw.WriteObjectEnd();
            jw.WritePropertyName("sh");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(h.X);
            jw.WritePropertyName("y");
            jw.Write(h.Y);
            jw.WritePropertyName("z");
            jw.Write(h.Z);
            jw.WriteObjectEnd();
            jw.WritePropertyName("lp");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(pos.X);
            jw.WritePropertyName("y");
            jw.Write(pos.Y);
            jw.WritePropertyName("z");
            jw.Write(pos.Z);
            jw.WriteObjectEnd();
            jw.WritePropertyName("lh");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(h.X);
            jw.WritePropertyName("y");
            jw.Write(h.Y);
            jw.WritePropertyName("z");
            jw.Write(h.Z);
            jw.WriteObjectEnd();
            jw.WriteObjectEnd();

            if (_primarySession != null)
            {
                _ = _primarySession.Session.TrySendDataChannelString(jw.ToString());
            }
        }

        private void CurrentSessionOnOnDataChannelReady()
        {
            Console.WriteLine("[WebRTC] data channel ready");
            if (_primarySession == null) return;

            _primarySession.Session.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
            JsonWriter jw = new JsonWriter();
            jw.WriteObjectStart();
            jw.WritePropertyName("j");
            jw.WriteObjectStart();
            jw.WritePropertyName("p");
            jw.Write(true);
            jw.WriteObjectEnd();
            jw.WriteObjectEnd();
            _log.Debug($"Joining voice on {_primarySession.Session.SessionId} with {jw}", Client);
            _ = _primarySession.Session.TrySendDataChannelString(jw.ToString());
            Console.WriteLine("[WebRTC] join sent");
            SendGlobalPosition();
        }

        public bool SendDataChannelMessage(string message)
        {
            if (_primarySession == null) return false;
            return _primarySession.Session.TrySendDataChannelString(message);
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
            if (_primarySession == null) return new List<UUID>();
            try { return _primarySession.Session.GetKnownPeers(); } catch { return new List<UUID>(); }
        }

        // Get aggregated peer list from all sessions (primary + adjacent + groups + P2P)
        public List<UUID> GetAllKnownPeers()
        {
            var peers = new HashSet<UUID>();
            
            if (_primarySession != null)
            {
                try
                {
                    foreach (var peer in _primarySession.Session.GetKnownPeers())
                    {
                        peers.Add(peer);
                    }
                }
                catch { }
            }

            foreach (var session in _adjacentSessions.Values)
            {
                try
                {
                    foreach (var peer in session.Session.GetKnownPeers())
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
                    foreach (var peer in session.Session.GetKnownPeers())
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
                    foreach (var peer in session.Session.GetKnownPeers())
                    {
                        peers.Add(peer);
                    }
                }
                catch { }
            }

            return peers.ToList();
        }

        public async Task<bool> RequestParcelVoiceInfo()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ParcelVoiceInfoRequest");
            if (cap == null)
            {
                _log.Warn("ParcelVoiceInfoRequest capability not available", Client);
                return false;
            }

            var tcs = new TaskCompletionSource<OSD>();
            _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, new OSD(), CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    tcs.TrySetException(error);
                }
                else
                {
                    var osd = OSDParser.Deserialize(data);
                    tcs.TrySetResult(osd);
                }
            });

            try
            {
                var osd = await tcs.Task;
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
                    if (_primarySession != null)
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
                _log.Error($"WebRTC voice version mismatch! Got {msg.MajorVersion}.{msg.MinorVersion}, expecting 1.x. Disabling WebRTC voice manager", Client);
                _enabled = false;
            }
            else
            {
                _log.Debug($"WebRTC voice version {msg.MajorVersion}.{msg.MinorVersion} supported", Client);
            }
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
        private FileStream _rawFileStream;
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
        /// <param name="groupId">The UUID of the group to join voice for</param>
        /// <returns>True if the join was successful, false otherwise</returns>
        public async Task<bool> JoinGroupVoice(UUID groupId)
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
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MUTLIAGENT, Client, _log);
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
                var provisioned = await session.RequestProvision().ConfigureAwait(false);

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
        public async Task LeaveGroupVoice(UUID groupId)
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
                await groupSession.Session.CloseSession().ConfigureAwait(false);
                groupSession.Session.Dispose();

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
        private async Task<(string channelUri, string credentials)> RequestGroupVoiceInfo(UUID groupId)
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ChatSessionRequest");
            if (cap == null)
            {
                _log.Warn("ChatSessionRequest capability not available for group voice", Client);
                return (null, null);
            }

            var payload = new OSDMap
            {
                ["method"] = "get voice channel info",
                ["session-id"] = groupId
            };

            var tcs = new TaskCompletionSource<OSD>();
            _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    tcs.TrySetException(error);
                }
                else
                {
                    var osd = OSDParser.Deserialize(data);
                    tcs.TrySetResult(osd);
                }
            });

            try
            {
                var osd = await tcs.Task;
                if (osd is OSDMap map)
                {
                    var channelUri = "";
                    var credentials = "";

                    if (map.TryGetValue("voice_credentials", out var cred) && cred is OSDMap credMap)
                    {
                        channelUri = credMap.ContainsKey("channel_uri") ? credMap["channel_uri"].AsString() : "";
                        credentials = credMap.ContainsKey("channel_credentials") ? credMap["channel_credentials"].AsString() : "";
                    }

                    _log.Debug($"Group voice credentials received for {groupId}: {channelUri}", Client);
                    return (channelUri, credentials);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to request group voice info for {groupId}: {ex.Message}", Client);
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
                    return groupSession.Session.GetKnownPeers();
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
                return groupSession.Session.SetPeerMute(peerId, mute);
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
                return groupSession.Session.SetPeerGain(peerId, gain);
            }
            return false;
        }

        #region P2P Voice Calls

        /// <summary>
        /// Initiate a P2P voice call with another agent.
        /// </summary>
        /// <param name="agentId">The UUID of the agent to call</param>
        /// <returns>True if the call was initiated successfully</returns>
        public async Task<bool> StartP2PCall(UUID agentId)
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
                var (channelUri, credentials) = await RequestP2PVoiceInfo(agentId).ConfigureAwait(false);

                if (string.IsNullOrEmpty(channelUri) || string.IsNullOrEmpty(credentials))
                {
                    _log.Warn($"Failed to get P2P voice credentials for {agentId}", Client);
                    try { OnP2PCallFailed?.Invoke(agentId, new Exception("No voice credentials received")); } catch { }
                    return false;
                }

                // Create a new multi-agent voice session for the P2P call
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MUTLIAGENT, Client, _log);
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
                        _ = EndP2PCall(agentId);
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
                var provisioned = await session.RequestProvision().ConfigureAwait(false);

                if (provisioned)
                {
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
        public async Task EndP2PCall(UUID agentId)
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
                await p2pSession.Session.CloseSession().ConfigureAwait(false);
                p2pSession.Session.Dispose();

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
        public async Task<bool> AcceptP2PCall(UUID agentId, string channelUri, string credentials)
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
                var session = new VoiceSession(AudioDevice, VoiceSession.ESessionType.MUTLIAGENT, Client, _log);
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
                        _ = EndP2PCall(agentId);
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
                var provisioned = await session.RequestProvision().ConfigureAwait(false);

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
            // Note: The simulator should be notified of the decline via a capability request
            // This would be implementation-specific based on the grid's protocol
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
                return p2pSession.Session.SetPeerMute(agentId, mute);
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
                return p2pSession.Session.SetPeerGain(agentId, gain);
            }
            return false;
        }

        /// <summary>
        /// Request P2P voice channel information from the simulator.
        /// </summary>
        private async Task<(string channelUri, string credentials)> RequestP2PVoiceInfo(UUID agentId)
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ChatSessionRequest");
            if (cap == null)
            {
                _log.Warn("ChatSessionRequest capability not available for P2P voice", Client);
                return (null, null);
            }

            var payload = new OSDMap
            {
                ["method"] = "start p2p voice",
                ["session-id"] = agentId
            };

            var tcs = new TaskCompletionSource<OSD>();
            _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, CancellationToken.None, (response, data, error) =>
            {
                if (error != null)
                {
                    tcs.TrySetException(error);
                }
                else
                {
                    var osd = OSDParser.Deserialize(data);
                    tcs.TrySetResult(osd);
                }
            });

            try
            {
                var osd = await tcs.Task;
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
                    return (channelUri, credentials);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to request P2P voice info for {agentId}: {ex.Message}", Client);
            }

            return (null, null);
        }

        #endregion
    }
}
