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
 * CONSEQUENTIAL DAMAGES ( INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using LibreMetaverse.StructuredData;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceSession : IDisposable
    {
        public const string PROVISION_VOICE_ACCOUNT_CAP = "ProvisionVoiceAccountRequest";
        public const string VOICE_SIGNALING_CAP = "VoiceSignalingRequest";

        public enum ESessionType
        {
            LOCAL,
            MULTIAGENT
        }

        private readonly GridClient _client;
        private readonly AudioDevice _audioDevice;
        private readonly IVoiceLogger _log;
        private RTCPeerConnection? _peerConnection;

        public event Action? OnPeerConnectionClosed;
        public event Action? OnPeerConnectionReady;
        public event Action? OnDataChannelReady;

        // Reprovision events
        public event Action? OnReprovisionSucceeded;
        public event Action<Exception>? OnReprovisionFailed;

        // New events for data-channel protocol
        public event Action<UUID>? OnPeerJoined;
        public event Action<UUID>? OnPeerLeft;
        public event Action<UUID, OSDMap>? OnPeerPositionUpdated;
        public event Action<List<UUID>>? OnPeerListUpdated;

        public class PeerAudioState
        {
            public int? Power { get; set; }
            public bool? VoiceActive { get; set; }
            public bool? JoinedPrimary { get; set; }
            public bool Left { get; set; }
            /// <summary>
            /// Moderator-muted flag. SL data channel "m" per-peer field.
            /// When true the server has muted this participant's voice.
            /// </summary>
            public bool? ModeratorMuted { get; set; }
        }
        public event Action<UUID, PeerAudioState>? OnPeerAudioUpdated;
        private bool _answerReceived = false;
        // Typed position/heading structures according to PDF (integers)
        public struct Int3 { public int X; public int Y; public int Z; }
        public struct Int4 { public int X; public int Y; public int Z; public int W; }
        public class AvatarPosition
        {
            public UUID AgentId { get; set; }
            public Int3? SenderPosition { get; set; }
            public Int4? SenderHeading { get; set; }
            public Int3? ListenerPosition { get; set; }
            public Int4? ListenerHeading { get; set; }
        }
        #pragma warning disable CS0414 // event is part of the public API surface but not yet raised internally
        public event Action<UUID, AvatarPosition>? OnPeerPositionUpdatedTyped;
#pragma warning restore CS0414
        public event Action<Dictionary<UUID, bool>>? OnMuteMapReceived;
        public event Action<Dictionary<UUID, int>>? OnGainMapReceived;

        public UUID SessionId { get; private set; }
        public string SdpLocal => _peerConnection?.localDescription?.sdp?.ToString() ?? string.Empty;
        public string SdpRemote => _peerConnection?.remoteDescription?.sdp?.ToString() ?? string.Empty;

        public bool Connected => _peerConnection?.connectionState == RTCPeerConnectionState.connected;
        public RTCDataChannel? DataChannel => _peerConnection?.DataChannels?.FirstOrDefault();
        private ESessionType SessionType { get; }
        private readonly ConcurrentQueue<RTCIceCandidate> _pendingCandidates = new ConcurrentQueue<RTCIceCandidate>();
        // Fast approximate count to avoid expensive ConcurrentQueue.Count in hot paths
        private int _pendingCandidateCount = 0;
        // Lock to keep queue and counter consistent for batch operations
        private readonly object _candidateLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private CancellationTokenSource? _iceTrickleCts;
        private Task? _iceTrickleTask;
        // Cancels the ICE-checking watchdog when the connection succeeds or the session is torn down.
        private CancellationTokenSource? _iceCheckingWatchdogCts;
        // Prevent concurrent reprovision attempts
        private readonly SemaphoreSlim _reprovisionLock = new SemaphoreSlim(1, 1);
        private int _reprovisionScheduledInt = 0; // 0=idle, 1=scheduled; use Interlocked

        // Multi-agent channel fields
        public string? ChannelId { get; set; }
        public string? ChannelCredentials { get; set; }

        /// <summary>
        /// The simulator this session provisions against.  Defaults to CurrentSim;
        /// set to a neighbour simulator for estate-voice neighbour connections.
        /// </summary>
        public Simulator? TargetSimulator { get; set; }

        /// <summary>
        /// True when this is the primary (home-region) connection for estate voice.
        /// Neighbour sessions must be non-primary: they receive audio but never transmit.
        /// Matches SL C++ <c>mPrimary</c> / <c>setMuteMic(true)</c> for neighbour connections.
        /// </summary>
        public bool IsPrimary { get; set; } = true;

        /// <summary>
        /// Parcel local ID to include in the ProvisionVoiceAccountRequest body.
        /// Set to -1 (INVALID_PARCEL_ID) for estate voice; set to the parcel's LocalID
        /// for parcel-specific voice channels.  Mirrors SL C++ <c>mParcelLocalID</c>.
        /// </summary>
        public int ParcelLocalId { get; set; } = -1;

        // Centralized peer and SSRC management helper
        private readonly PeerManager _peerManager;
        private readonly DataChannelProcessor _dataChannelProcessor;

        /// <summary>Returns <see cref="TargetSimulator"/> if set, otherwise <c>Client.Network.CurrentSim</c>.</summary>
        private Simulator? EffectiveSim => TargetSimulator ?? _client.Network?.CurrentSim;

        public List<UUID> GetKnownPeers()
        {
            try
            {
                return _peerManager.GetKnownPeers();
            }
            catch
            {
                return new List<UUID>();
            }
        }

        // Position and keepalive loop cancellation/tasks
        private CancellationTokenSource? _positionLoopCts;
        private Task? _positionLoopTask;

        // Whether spatial coords changed since last send — only send updates when this is true
        private volatile bool _spatialCoordsDirty = true;
        // Last observed values (used to detect changes)
        private Vector3d _lastObservedGlobalPos = Vector3d.Zero;
        private Quaternion _lastObservedHeading = Quaternion.Identity;
        private Vector3d _lastObservedCameraGlobalPos = Vector3d.Zero;
        private Quaternion _lastObservedCameraHeading = Quaternion.Identity;
        // Last sent values (used to track last send for keepalive)
        private Vector3d _lastSentGlobalPos = Vector3d.Zero;
        private Quaternion _lastSentHeading = Quaternion.Identity;
        private Vector3d _lastSentCameraGlobalPos = Vector3d.Zero;
        private Quaternion _lastSentCameraHeading = Quaternion.Identity;
        // Timestamp (ms, from DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) of the last successful position send
        private long _lastPositionSentMs = 0;
        // SL C++ setAvatarPosition: dist_vec_squared(old, new) > 0.01 (i.e. total 3D distance > 10cm)
        private const double POSITION_CHANGE_THRESHOLD_DIST_SQ = 0.01;
        // SL C++ setAvatarPosition: only update rotation when |dot(old,new)| < cos(2°) = MINUSCULE_ANGLE_COS
        // cos(0.5 * 4_degrees) = cos(2 * PI / 90) ~= 0.9994
        private const double HEADING_DOT_THRESHOLD = 0.9994;
        // Maximum interval between position sends regardless of movement.
        // The SL Janus server stops sending ICE STUN consent checks when the viewer is
        // silent for ~9 s (matching SIPSorcery's ICE_CONNECTED_NO_COMMUNICATION_TIMEOUT).
        // Sending position at least this often keeps the server-side ICE alive.
        // SL C++ sends position on every voice processing frame (~100 ms) regardless of change.
        private const long POSITION_KEEPALIVE_MS = 3000;


        internal VoiceSession(AudioDevice audioDeviceDevice, ESessionType type, GridClient client, IVoiceLogger? logger = null)
        {
            _client = client;
            _audioDevice = audioDeviceDevice;
            SessionType = type;
            SessionId = UUID.Zero;
            _log = logger ?? new LibreMetaverseVoiceLogger();

            // Initialize peer manager and forward its events to existing VoiceSession events
            _peerManager = new PeerManager(_audioDevice, _client, _log);
            _peerManager.PeerJoined += id => { try { OnPeerJoined?.Invoke(id); } catch { } };
            _peerManager.PeerLeft += id => { try { OnPeerLeft?.Invoke(id); } catch { } };
            _peerManager.PeerPositionUpdated += (id, map) =>
            {
                try { OnPeerPositionUpdated?.Invoke(id, map); } catch { }
                try { ApplyDistanceAttenuation(id, map); } catch { }
            };
            _peerManager.PeerListUpdated += list => { try { OnPeerListUpdated?.Invoke(list); } catch { } };
            _peerManager.PeerAudioUpdated += (id, state) => { try { OnPeerAudioUpdated?.Invoke(id, state); } catch { } };
            _peerManager.MuteMapReceived += m => { try { OnMuteMapReceived?.Invoke(m); } catch { } };
            _peerManager.GainMapReceived += g => { try { OnGainMapReceived?.Invoke(g); } catch { } };
            _dataChannelProcessor = new DataChannelProcessor(_peerManager, _client, _log, TrySendDataChannelString);
        }
        public async Task StartAsync(CancellationToken ct = default)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct))
            {
                var token = linked.Token;

                _peerConnection = await CreatePeerConnectionAsync(token).ConfigureAwait(false);
                _iceTrickleTask = IceTrickleStart(token);

                // Do not start recording here. Recording should follow connection state to avoid
                // capturing audio before the peer connection is established.
            }
        }

        // Helper that posts to caps and returns deserialized OSD, with retries and timeout handling
        private async Task<OSD> PostCapsWithRetries(Uri cap, OSD payload, int maxAttempts = 10, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (cap == null) throw new VoiceException("Capability URI is null.");
            if (timeout == null) timeout = TimeSpan.FromSeconds(10);

            // Capture the token once before the retry loop to avoid ObjectDisposedException
            // if _cts is disposed concurrently (e.g. Dispose() called while retrying).
            CancellationToken token;
            try { token = ct == CancellationToken.None ? _cts.Token : ct; }
            catch (ObjectDisposedException) { throw new OperationCanceledException(); }

            int attempt = 0;
            Exception? lastEx = null;
            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    var postTask = _client.HttpCapsClient.PostAsync(cap, OSDFormat.Xml, payload, token);
                    var delayTask = Task.Delay(timeout.Value, token);
                    var completed = await Task.WhenAny(postTask, delayTask).ConfigureAwait(false);
                    if (completed == delayTask)
                    {
                        lastEx = new TimeoutException($"POST to {cap} timed out.");
                    }
                    else
                    {
                        var (response, data) = await postTask.ConfigureAwait(false);
                        try
                        {
                            // Attempt to deserialize LLSD/OSD. If parsing fails, capture raw response for diagnostics.
                            var osd = OSDParser.Deserialize(data ?? Array.Empty<byte>());
                            return osd;
                        }
                        catch (Exception parseEx)
                        {
                            string respText = string.Empty;
                            try { respText = data != null ? Encoding.UTF8.GetString(data) : string.Empty; } catch { }

                            int? statusCode = (int)response.StatusCode;
                            string? statusText = response.ReasonPhrase;

                                var preview = respText;
                                if (preview != null && preview.Length > 1000) preview = preview.Substring(0, 1000) + "...";



                                // Detect common non-OSD responses that are non-retryable
                                var lower = (respText ?? string.Empty).ToLowerInvariant();
                                bool looksLikeHtml = lower.Contains("<html") || lower.Contains("<!doctype") || lower.Contains("<head") || lower.Contains("<body");
                                bool containsUnauthorized = lower.Contains("unauthorized") || lower.Contains("forbidden") || lower.Contains("401") || lower.Contains("403") || lower.Contains("unknown session");
                                bool containsUnknownConference = lower.Contains("unknown conference");
                                // Detect credential/channel expiry or invalid credentials errors commonly returned by mixers
                                bool containsCredentialIssue = (lower.Contains("credential") || lower.Contains("credentials") || lower.Contains("channel")) && (lower.Contains("expired") || lower.Contains("invalid") || lower.Contains("unknown") || lower.Contains("denied"));

                                if (statusCode.HasValue && statusCode.Value >= 400)
                                {
                                    // HTTP error - do not retry
                                    throw new VoiceException($"HTTP {(statusCode.Value)} when POSTing to {cap}: {statusText ?? ""}. Response preview: {preview}");
                                }

                                if (containsUnauthorized)
                                {
                                    throw new VoiceException($"Authorization error when POSTing to {cap}: {preview}");
                                }

                                if (containsUnknownConference)
                                {
                                    throw new VoiceException($"Server reported unknown conference when POSTing to {cap}: {preview}");
                                }

                                if (containsCredentialIssue)
                                {
                                    // Credential/channel problems should trigger reprovision flow. Surface a clear error so callers can schedule reprovision.
                                    throw new VoiceException($"Channel/credential error when POSTing to {cap}: {preview}");
                                }

                                if (looksLikeHtml)
                                {
                                    throw new VoiceException($"Received HTML error page when POSTing to {cap}. Response preview: {preview}");
                                }

                                // Otherwise keep the parsing exception for possible retry, but include the raw response for diagnostics
                                lastEx = new Exception($"Failed to parse capability response: {parseEx.Message}. Raw response: {preview}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If we threw a VoiceException above (non-retryable), rethrow immediately
                    if (ex is VoiceException) throw;
                    lastEx = ex;
                }

                // Slight backoff before retrying: 200ms * attempt, capped at 2000ms
                var backoffMs = Math.Min(200 * attempt, 2000);
                try
                {
                    await Task.Delay(backoffMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }

            throw new VoiceException($"Failed to POST to capability {cap}: {lastEx?.Message}");
        }

        public async Task<RTCPeerConnection> CreatePeerConnectionAsync(CancellationToken ct = default)
        {
            // SL C++ getConnectionOptions(): use grid-specific STUN servers only.
            // num_servers = 3 for agni, 2 for all other grids (e.g. aditi/beta).
            // Adding public STUN servers causes ICE candidate IP mismatches because the
            // SL WebRTC server validates candidates against its own external IP.
            var loginServer = _client?.Settings?.Connection.LoginServer ?? string.Empty;
            var gridId = loginServer.Contains(".agni.", StringComparison.OrdinalIgnoreCase) ? "agni"
                       : loginServer.Contains(".aditi.", StringComparison.OrdinalIgnoreCase) ? "aditi"
                       : "agni"; // default to agni for unknown grids
            int numStunServers = gridId == "agni" ? 3 : 2;
            var iceServers = new List<RTCIceServer>();
            for (int i = 1; i <= numStunServers; i++)
                iceServers.Add(new RTCIceServer { urls = $"stun:stun{i}.{gridId}.secondlife.io:3478" });

            try
            {
                var turnUrl = Environment.GetEnvironmentVariable("LIBREMETAVERSE_TURN_URL");
                var turnUser = Environment.GetEnvironmentVariable("LIBREMETAVERSE_TURN_USER");
                var turnPass = Environment.GetEnvironmentVariable("LIBREMETAVERSE_TURN_PASS");
                if (!string.IsNullOrWhiteSpace(turnUrl))
                {
                    var turnServer = new RTCIceServer { urls = turnUrl };
                    if (!string.IsNullOrEmpty(turnUser)) turnServer.username = turnUser;
                    if (!string.IsNullOrEmpty(turnPass)) turnServer.credential = turnPass;
                    iceServers.Add(turnServer);
                    _log.Debug($"Added TURN server from environment: {turnUrl}", _client);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to read TURN environment variables: {ex.Message}", _client);
            }

            var pc = new RTCPeerConnection(new RTCConfiguration
            {
                X_ICEIncludeAllInterfaceAddresses = true,
                iceServers = iceServers
            }, 0, new PortRange(49152, 65535));

            _peerConnection = pc; // assign to field early

            // Add this enhanced logging to your VoiceSession.cs CreatePeerConnectionAsync method:

            pc.OnRtpPacketReceived += (IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket) =>
            {
                // Logger.Log($"OnRtpPacketReceived fired for mediaType: {mediaType}", Helpers.LogLevel.Info, _client);
                // Logger.Log($"RTP: {rtpPacket.Payload.Length} bytes, PT={rtpPacket.Header.PayloadType}, Seq={rtpPacket.Header.SequenceNumber}, TS={rtpPacket.Header.Timestamp}", Helpers.LogLevel.Debug, _client);
                if (mediaType == SDPMediaTypesEnum.audio)
                {
                    //  Logger.Log($"RTP Audio: {rtpPacket.Payload.Length} bytes, " +
                    //      $"PT={rtpPacket.Header.PayloadType}, " +
                    // $"Seq={rtpPacket.Header.SequenceNumber}, " +
                    //          $"TS={rtpPacket.Header.Timestamp}",
                    //    Helpers.LogLevel.Debug, _client);

                    if (_audioDevice?.EndPoint != null)
                    {
                        try
                        {
                            uint ssrcVal = rtpPacket.Header.SyncSource;
                            _audioDevice.PlayRtpPacket(ssrcVal, rtpPacket.Payload);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn($"Failed to play RTP packet: {ex.Message}", _client);
                        }
                    }
                    else
                    {
                        _log.Warn("Cannot play audio: EndPoint is null (audio backend not available)", _client);
                    }
                }
            };

            pc.oniceconnectionstatechange += (state) =>
            {
                _log.Debug($"ICE connection state: {state}", _client);
                if (state == RTCIceConnectionState.checking)
                {
                    // Janus is sometimes not ready when we first provision, so ICE spends 16 s
                    // in 'checking' before SIPSorcery declares failure. Start a watchdog so we
                    // reprovision early rather than waiting the full 16 s - but not so early that
                    // we repeatedly abort negotiations that would have succeeded given more time.
                    // A 5 s cutoff was too aggressive: real-world ICE (NAT traversal, TURN relay)
                    // routinely takes longer than that on the first attempt, so the watchdog kept
                    // tearing down and restarting negotiation in a loop that rarely settled long
                    // enough for the connected state to reach the UI.
                    if (!ReferenceEquals(pc, _peerConnection)) return;
                    try { _iceCheckingWatchdogCts?.Cancel(); } catch { }
                    var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    _iceCheckingWatchdogCts = watchdogCts;
                    // Capture the specific PC and session so a later reprovision replacing
                    // _peerConnection/SessionId does not cause this watchdog to tear down the
                    // new (already-healthy) connection — same pattern as the 60s post-answer
                    // watchdogs in RequestLocalVoiceProvision/RequestMultiAgentVoiceProvision.
                    // Relying solely on ReferenceEquals(pc, _peerConnection) at fire time is not
                    // enough: it was observed firing well after a subsequent reprovision had
                    // already succeeded and reassigned _peerConnection, tearing down a working
                    // connection because _iceCheckingWatchdogCts is a single field shared across
                    // every peer-connection generation for this session.
                    var watchedPc = pc;
                    var watchedSession = SessionId;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(12), watchdogCts.Token).ConfigureAwait(false);
                            if (SessionId != watchedSession) return;
                            if (!ReferenceEquals(watchedPc, _peerConnection)) return;
                            if (watchedPc.connectionState != RTCPeerConnectionState.connected &&
                                watchedPc.connectionState != RTCPeerConnectionState.failed &&
                                watchedPc.connectionState != RTCPeerConnectionState.closed)
                            {
                                _log.Warn($"ICE still in 'checking' after 12 s — triggering early reprovision", _client);
                                ScheduleReprovisionWithBackoff();
                            }
                        }
                        catch (OperationCanceledException) { }
                    }, watchdogCts.Token);
                }
                else if (state == RTCIceConnectionState.connected)
                {
                    // Cancel the watchdog — connection succeeded.
                    try { _iceCheckingWatchdogCts?.Cancel(); } catch { }
                }
                else if (state == RTCIceConnectionState.failed)
                {
                    try { _iceCheckingWatchdogCts?.Cancel(); } catch { }
                    _log.Warn("ICE connection state failed — waiting for peer connection state verdict", _client);
                }
            };
            // Create data channel BEFORE negotiation
            var dc = await pc.createDataChannel("SLData", new RTCDataChannelInit { ordered = true }).ConfigureAwait(false);

            dc.onopen += () =>
            {
                _log.Debug($"Data channel opened (primary={IsPrimary})", _client);

                // Non-primary (neighbour) sessions join without the "p" flag — matches SL C++ sendJoin()
                var joinMsg = IsPrimary ? "{\"j\":{\"p\":true}}" : "{\"j\":{}}";
                TrySendDataChannelString(joinMsg);

                // Start position updates AFTER a small delay to ensure join is processed
                Task.Delay(100, ct).ContinueWith(_ => StartPositionLoop(), TaskScheduler.Default);

                // Start data channel keepalive
                StartKeepAliveLoop();

                OnDataChannelReady?.Invoke();
            };

            dc.onclose += () =>
            {
                // Only react to close events from the *current* peer connection.
                if (!ReferenceEquals(pc, _peerConnection)) return;
                // Stop loops here early (data channel closed means no more spatial sends).
                // Do NOT fire OnPeerConnectionClosed — onconnectionstatechange(closed) is the
                // sole owner of that event. Firing it here too causes a double-invocation that
                // confuses VoiceManager/VoiceViewModel (SL C++ has a single OnPeerConnectionClosed
                // callback from the WebRTC library, never duplicated via the data channel).
                StopPositionLoop();
                StopKeepAliveLoop();
            };
            dc.onmessage += (channel, type, data) =>
            {
                var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                _log.Debug($"Data channel message received: {msg}", _client);
                Task.Run(() => HandleDataChannelMessage(msg), ct);
            };

            // SDP negotiation
            // Ensure we have a recording source; attempt to create a default one if missing so GetAudioSourceFormats() won't NRE.
            if (_audioDevice == null)
            {
                _log.Error("_audioDevice is null in VoiceSession.CreatePeerConnectionAsync", _client);
                throw new VoiceException("Internal error: _audioDevice is null.");
            }
            if (_audioDevice.Source == null)
            {
                _log.Warn("Recording is null. Attempting to create default recording source.", _client);
                try
                {
                    _audioDevice.SetRecordingDevice(null);
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create default recording source: {ex.Message}", _client);
                }

                // If we still don't have a source, fail early with a clear error instead of passing null to MediaStreamTrack
                if (_audioDevice.Source == null)
                {
                    _log.Error("No audio recording source available after attempts to create one.", _client);
                    throw new VoiceException("No audio recording source available.");
                }
            }

            var audioTrack = new MediaStreamTrack(_audioDevice?.Source?.GetAudioSourceFormats());

            pc.addTrack(audioTrack);

            // Wire ICE handlers BEFORE setLocalDescription so that ICE gathering events
            // that fire synchronously during or immediately after setLocalDescription are
            // never missed. In fast reprovision paths (e.g. post-teleport) ICE gathering
            // can complete before the handlers below would otherwise be registered, causing
            // IceTrickleStop / signaling-complete to never be sent and a permanent 'checking'
            // loop until the 5-second watchdog fires and keeps reprovisioning forever.
            pc.onicecandidate += (candidate) =>
            {
                if (candidate == null) return;
                EnqueueCandidate(candidate);
            };

            pc.onicegatheringstatechange += async (state) =>
            {
                try
                {
                    if (state == RTCIceGatheringState.complete)
                    {
                        _log.Debug("ICE gathering state completed", _client);
                        await IceTrickleStop().ConfigureAwait(false);
                    }
                    else if (state == RTCIceGatheringState.gathering)
                    {
                        _log.Debug("ICE gathering state has commenced.", _client);
                        // Ensure trickle loop is running
                        _ = IceTrickleStart(ct);
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"Exception in onicegatheringstatechange handler: {ex.Message}", _client);
                }
            };

            var offer = pc.createOffer();
            var rawSdp = offer.sdp.ToString();
            var processedSdp = ProcessLocalSdp(rawSdp);

            // Assign the mangled SDP string directly
            offer.sdp = processedSdp;

            await pc.setLocalDescription(offer).ConfigureAwait(false);

            // Safety net: if ICE gathering already completed before setLocalDescription returned
            // (possible when the OS ICE stack is already warm), the onicegatheringstatechange
            // callback may not fire again. Kick IceTrickleStop manually in that case.
            if (pc.iceGatheringState == RTCIceGatheringState.complete)
            {
                _log.Debug("ICE gathering already complete after setLocalDescription — triggering IceTrickleStop", _client);
                _ = IceTrickleStop();
            }

            // Parse the local SDP to get the Opus payload type for manual RTP sending
            string localSdp = pc.localDescription.sdp.ToString();
            var sdpLines = localSdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int opusPayloadType = -1;
            foreach (var line in sdpLines)
            {
                if (line.Contains("opus/48000"))
                {
                    var match = Regex.Match(line, @"a=rtpmap:(\d+)\s+opus");
                    if (match.Success)
                    {
                        opusPayloadType = int.Parse(match.Groups[1].Value);
                        break;
                    }
                }
            }

            //  Logger.Log($"Final local SDP offer:\n{pc.localDescription.sdp}", Helpers.LogLevel.Debug, _client);
            pc.localDescription.sdp.SessionName = "LibreMetaVoice";
            pc.ondatachannel += (channel) =>
            {
                _log.Debug($"Server created data channel: {channel.label} (id: {channel.id})", _client);

                channel.onopen += () =>
                {
                    _log.Debug($"Inbound channel '{channel.label}' opened", _client);
                };

                channel.onmessage += (ch, type, data) =>
                {
                    var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                    // Logger.Log($"📨 Received on '{channel.label}': {msg}", Helpers.LogLevel.Info, _client);
                    Task.Run(() => HandleDataChannelMessage(msg), ct);
                };

                channel.onclose += () =>
                {
                    _log.Debug($"Inbound channel '{channel.label}' closed", _client);
                };

                channel.onerror += (error) =>
                {
                    _log.Error($"Inbound channel '{channel.label}' error: {error}", _client);
                };
            };
            pc.onconnectionstatechange += async (state) =>
            {
                // Guard: ignore state changes from a peer connection that has already been
                // superseded by a reprovision. Stale handlers would otherwise stop audio on
                // the *new* (healthy) connection when the old PC finally closes.
                if (!ReferenceEquals(pc, _peerConnection)) return;

                _log.Debug($"Peer connection state changed to {state}.", _client);
                if (state == RTCPeerConnectionState.connected)
                {
                    if (_audioDevice?.EndPoint != null)
                    {
                        try
                        {
                            await _audioDevice.StartPlaybackAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Debug($"Failed to start playback via _audioDevice: {ex.Message}", _client);
                            // Fallback: attempt to start endpoint directly
                            try { if (_audioDevice.EndPoint != null) await _audioDevice.EndPoint.StartAudioSink().ConfigureAwait(false); } catch { }
                        }
                        _log.Debug("Playback started", _client);
                    }
                    else
                    {
                        // Attempt to recreate endpoint if possible
                        var got = _audioDevice!.EnsureEndpoint();
                        if (got)
                        {
                            try
                            {
                                await _audioDevice!.StartPlaybackAsync().ConfigureAwait(false);
                                _log.Debug("Playback started after EnsureEndpoint", _client!);
                            }
                            catch (Exception ex)
                            {
                                _log.Debug($"Failed to start playback after EnsureEndpoint: {ex.Message}", _client!);
                            }
                        }
                        else
                        {
                            _log.Debug("Playback not started: EndPoint is null (audio backend not available or no devices found)", _client!);
                        }
                    }
                    // Do NOT auto-start recording here. The UI layer (VoiceViewModel.OnConnectionReady)
                    // owns mic-mute state: in PTT mode it keeps recording stopped until the Talk
                    // button is pressed; in open-mic mode it unmutes immediately. Auto-starting here
                    // races with that handler and leaves PTT mode permanently recording-stopped.
                    OnPeerConnectionReady?.Invoke();
                }
                else if (state == RTCPeerConnectionState.disconnected)
                {
                    // 'disconnected' is transient — ICE is attempting to recover the connection.
                    // Do NOT stop audio or trigger reprovision here; wait for 'connected' or 'failed'.
                    _log.Debug("Peer connection disconnected (transient) — waiting for ICE recovery", _client);
                }
                else if (state == RTCPeerConnectionState.failed)
                {
                    // 'failed' is terminal for this peer connection — stop audio and reprovision.
                    _log.Warn("Peer connection failed — stopping audio and scheduling reprovision", _client);
                    StopPositionLoop();
                    StopKeepAliveLoop();
                    if (_audioDevice != null)
                    {
                        try { await _audioDevice.StopPlaybackAsync().ConfigureAwait(false); } catch { }
                        try { _audioDevice.StopRecording(); } catch { }
                    }
                    try { ScheduleReprovisionWithBackoff(); } catch { }
                }
                else if (state == RTCPeerConnectionState.closed)
                {
                    StopPositionLoop();
                    StopKeepAliveLoop();
                    if (_audioDevice != null)
                    {
                        try { await _audioDevice.StopPlaybackAsync().ConfigureAwait(false); } catch { }
                        try { _audioDevice.StopRecording(); } catch { }
                    }
                    OnPeerConnectionClosed?.Invoke();
                }
            };

            // Detach any existing handler to prevent duplicates on reprovision
            if (_audioDevice?.Source != null)
            {
                try { _audioDevice.Source.OnAudioSourceEncodedSample -= pc.SendAudio; } catch { }

                // Neighbour sessions must never transmit this agent's mic — peers would hear echo
                // from the same voice simultaneously on multiple region connections.
                // Matches SL C++: LLVoiceWebRTCSpatialConnection::setMuteMic() always mutes non-primary.
                if (IsPrimary)
                {
                    _audioDevice.Source.OnAudioSourceEncodedSample += (duration, sample) =>
                    {
                        // Sending before DTLS/SRTP completes is pure waste (SIPSorcery just logs
                        // "SendRtpPacket cannot be called on a secure session" and drops it) — with
                        // two concurrent sessions (primary + neighbour) both encoding mic audio at
                        // ~50fps while a handshake is stuck, this was generating hundreds of no-op
                        // calls/sec. Skip the call entirely until the connection is actually usable.
                        if (pc.connectionState != RTCPeerConnectionState.connected) return;
                        pc.SendAudio(duration, sample);
                    };
                }
            }

            // Also wire the _audioDevice level event for file playback and other non-source audio
            if (_audioDevice != null)
            {
                try { _audioDevice.OnAudioSourceEncodedSample -= pc.SendAudio; } catch { }
                if (IsPrimary)
                {
                    _audioDevice.OnAudioSourceEncodedSample += (duration, sample) =>
                    {
                        if (pc.connectionState != RTCPeerConnectionState.connected) return;
                        pc.SendAudio(duration, sample);
                    };
                }
            }

            return pc;
        }

        private void StartPositionLoop()
        {
            if (_positionLoopTask != null && !_positionLoopTask.IsCompleted) return;
            _positionLoopCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var token = _positionLoopCts.Token;
            _positionLoopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var dc = DataChannel;
                        if (dc == null || dc.readyState != RTCDataChannelState.open) { await Task.Delay(100, token).ConfigureAwait(false); continue; }

                        var globalPos = _client?.Self?.GlobalPosition ?? Vector3d.Zero;
                        var heading = _client?.Self?.RelativeRotation ?? Quaternion.Identity;

                        if (globalPos == Vector3d.Zero) { await Task.Delay(100, token).ConfigureAwait(false); continue; }

                        // SL C++ updatePosition: bump avatar position up to head height.
                        // avatar_pos += LLVector3d(0.f, 0.f, 1.f);
                        globalPos = new Vector3d(globalPos.X, globalPos.Y, globalPos.Z + 1.0);

                        // Camera (listener) position: sim-local camera position + sim global offset
                        var cameraGlobalPos = globalPos; // fallback to avatar pos
                        var cameraHeading = heading;     // fallback to avatar heading
                        try
                        {
                            var cam = _client?.Self?.Movement?.Camera;
                            if (cam != null && _client?.Network?.CurrentSim != null)
                            {
                                Utils.LongToUInts(_client.Network.CurrentSim.Handle, out var gx, out var gy);
                                cameraGlobalPos = new Vector3d(gx + cam.Position.X, gy + cam.Position.Y, cam.Position.Z);
                                // Build a quaternion from the camera's forward (AtAxis) and up (UpAxis) axes
                                cameraHeading = Quaternion.CreateFromRotationMatrix(Matrix4.CreateWorld(Vector3.Zero, cam.AtAxis, cam.UpAxis));
                            }
                        }
                        catch { }

                        // SL C++ enforceTether: clamp camera/listener to within MAX_AUDIO_DIST (50m) of avatar.
                        // Prevents eavesdropping beyond 50m and matches server-side enforcement.
                        const double maxAudioDist = 50.0;
                        var cameraOffset = new Vector3d(
                            cameraGlobalPos.X - globalPos.X,
                            cameraGlobalPos.Y - globalPos.Y,
                            cameraGlobalPos.Z - globalPos.Z);
                        double cameraDist = Math.Sqrt(cameraOffset.X * cameraOffset.X + cameraOffset.Y * cameraOffset.Y + cameraOffset.Z * cameraOffset.Z);
                        if (cameraDist > maxAudioDist)
                        {
                            double scale = maxAudioDist / cameraDist;
                            cameraGlobalPos = new Vector3d(
                                globalPos.X + cameraOffset.X * scale,
                                globalPos.Y + cameraOffset.Y * scale,
                                globalPos.Z + cameraOffset.Z * scale);
                        }

                        // SL C++: dist_vec_squared(old, pos) > 0.01 (total 3D distance > 10cm)
                        double pdx = globalPos.X - _lastObservedGlobalPos.X;
                        double pdy = globalPos.Y - _lastObservedGlobalPos.Y;
                        double pdz = globalPos.Z - _lastObservedGlobalPos.Z;
                        bool posChanged = (pdx * pdx + pdy * pdy + pdz * pdz) > POSITION_CHANGE_THRESHOLD_DIST_SQ;

                        // SL C++: |dot(old, new)| < MINUSCULE_ANGLE_COS (~0.9994, i.e. change > ~2deg)
                        double hDot = Math.Abs(heading.X * _lastObservedHeading.X + heading.Y * _lastObservedHeading.Y
                                               + heading.Z * _lastObservedHeading.Z + heading.W * _lastObservedHeading.W);
                        bool headingChanged = (heading != _lastObservedHeading) && (hDot < HEADING_DOT_THRESHOLD);

                        double cpdx = cameraGlobalPos.X - _lastObservedCameraGlobalPos.X;
                        double cpdy = cameraGlobalPos.Y - _lastObservedCameraGlobalPos.Y;
                        double cpdz = cameraGlobalPos.Z - _lastObservedCameraGlobalPos.Z;
                        bool cameraPosChanged = (cpdx * cpdx + cpdy * cpdy + cpdz * cpdz) > POSITION_CHANGE_THRESHOLD_DIST_SQ;

                        double chDot = Math.Abs(cameraHeading.X * _lastObservedCameraHeading.X + cameraHeading.Y * _lastObservedCameraHeading.Y
                                                + cameraHeading.Z * _lastObservedCameraHeading.Z + cameraHeading.W * _lastObservedCameraHeading.W);
                        bool cameraHeadingChanged = (cameraHeading != _lastObservedCameraHeading) && (chDot < HEADING_DOT_THRESHOLD);

                        if (posChanged || headingChanged || cameraPosChanged || cameraHeadingChanged)
                        {
                            _spatialCoordsDirty = true;
                            _lastObservedGlobalPos = globalPos;
                            _lastObservedHeading = heading;
                            _lastObservedCameraGlobalPos = cameraGlobalPos;
                            _lastObservedCameraHeading = cameraHeading;
                        }

                        // Force a periodic send even when stationary so the SL Janus server
                        // keeps sending ICE STUN consent checks (server keepalive).
                        // SL C++ always sends position on every voice frame regardless of change.
                        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (!_spatialCoordsDirty && nowMs - _lastPositionSentMs >= POSITION_KEEPALIVE_MS)
                        {
                            _spatialCoordsDirty = true;
                        }

                        // Attempt to send update; SendPositionUpdate will short-circuit if not dirty
                        SendPositionUpdate(dc, globalPos, heading, cameraGlobalPos, cameraHeading);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _log.Error($"Position loop error: {ex.Message}", _client);
                    }
                    try { await Task.Delay(100, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }, token);
        }

        private void StopPositionLoop()
        {
            try
            {
                _positionLoopCts?.Cancel();
                _positionLoopTask?.Wait(500);
            }
            catch { }
            finally
            {
                _positionLoopCts?.Dispose();
                _positionLoopCts = null;
                _positionLoopTask = null;
            }
        }

        private void StartKeepAliveLoop()
        {
            // SL C++ has no data-channel ping/pong. The WebRTC transport layer handles
            // keepalive via ICE STUN binding requests automatically. No application-level
            // keepalive is needed or expected by the SL voice server.
        }

        private static void StopKeepAliveLoop() { /* no-op: SL C++ has no data-channel ping/pong */ }

        // Helper methods to keep _pendingCandidates and _pendingCandidateCount consistent
        private void EnqueueCandidate(RTCIceCandidate candidate)
        {
            if (candidate == null) return;
            lock (_candidateLock)
            {
                _pendingCandidates.Enqueue(candidate);
                Interlocked.Increment(ref _pendingCandidateCount);
            }
        }

        private List<RTCIceCandidate> DequeueAllCandidates()
        {
            var list = new List<RTCIceCandidate>();
            lock (_candidateLock)
            {
                while (_pendingCandidates.TryDequeue(out var c))
                {
                    list.Add(c);
                    Interlocked.Decrement(ref _pendingCandidateCount);
                }
            }
            return list;
        }

        private int GetPendingCandidateCount()
        {
            return Interlocked.CompareExchange(ref _pendingCandidateCount, 0, 0);
        }

        private async Task FlushPendingIceCandidates()
        {
            if (!_answerReceived || GetPendingCandidateCount() == 0) { return; }

            var cap = EffectiveSim?.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
            if (cap == null) { return; }

            var dequeued = DequeueAllCandidates();
            if (dequeued.Count == 0) return;

            var candidatesArray = new OSDArray();
            foreach (var candidate in dequeued)
            {
                var map = new OSDMap
                {
                    ["candidate"] = candidate.candidate,
                    ["sdpMid"] = candidate.sdpMid,
                    ["sdpMLineIndex"] = candidate.sdpMLineIndex
                };
                candidatesArray.Add(map);
            }

            var payload = new OSDMap
            {
                ["voice_server_type"] = "webrtc",
                ["viewer_session"] = SessionId,
                ["candidates"] = candidatesArray
            };

            try
            {
                if (cap != null)
                    await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
                _log.Debug($"Sent {candidatesArray.Count} ICE candidates", _client);
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send ICE candidates: {ex.Message}", _client);
            }
        }

        // Send any pending ICE candidates to the voice signaling capability
        private async Task SendVoiceSignalingRequest()
        {
            if (!_answerReceived)
            {
                _log.Debug("Skipping ICE send - no answer received yet", _client);
                return;
            }

            var cap = EffectiveSim?.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
            if (cap == null)
            {
                _log.Debug("Voice signaling cap not available", _client);
                return;
            }

            var payload = new OSDMap
            {
                { "voice_server_type", "webrtc" },
                { "viewer_session", SessionId }
            };

            var canArray = new OSDArray();

            // Drain queue atomically and get accurate count
            var dequeued = DequeueAllCandidates();
            if (dequeued.Count == 0) return; // Nothing to send

            _log.Debug($"Sending {dequeued.Count} ICE candidates", _client);

            foreach (var candidate in dequeued)
            {
                var map = new OSDMap
                {
                    { "sdpMid", candidate.sdpMid },
                    { "sdpMLineIndex", candidate.sdpMLineIndex },
                    { "candidate", candidate.candidate }
                };
                canArray.Add(map);
            }

            payload["candidates"] = canArray;

            try
            {
                var resp = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
                _log.Debug($"Sent {canArray.Count} ICE candidates successfully", _client);
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send ICE candidates: {ex.Message}", _client);
            }
        }

        private async Task SendVoiceSignalingCompleteRequest(CancellationToken ct = default)
        {
            var cap = EffectiveSim?.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);

            var payload = new OSDMap
             {

                 { "voice_server_type", "webrtc" },
                 { "viewer_session", SessionId }
             };

            // SL C++ uses a singular "candidate" key with a map value { "completed": true }
            // NOT a "candidates" array. Sending the wrong key leaves the server conference
            // in an inconsistent state, causing subsequent requests to return "Unknown conference".
            // Reference: LLVoiceWebRTCConnection::processIceUpdatesCoro in llvoicewebrtc.cpp:
            //   body["candidate"] = body_candidate;  (where body_candidate["completed"] = true)
            var completedMap = new OSDMap { { "completed", true } };
            payload["candidate"] = completedMap; // singular "candidate", map value — matches SL C++
            _log.Debug($"Sending ICE Signaling Complete for {SessionId}", _client);
            if (cap == null)
            {
                _log.Debug("Voice signaling capability not available for complete request.", _client);
                return;
            }
            try
            {
                var resp = await PostCapsWithRetries(cap, payload, ct: ct).ConfigureAwait(false);
                try
                {
                    if (resp is OSDMap respMap)
                    {
                        _log.Debug($"Voice signaling complete response: {OSDParser.SerializeJsonString(respMap, true)}", _client);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                _log.Warn($"Sending ICE Signaling Complete failed for {SessionId}: {ex.Message}", _client);
            }
        }

        // Simple handler implementing viewer<->datachannel JSON protocol
        private void HandleDataChannelMessage(string msg)
        {
            _dataChannelProcessor?.ProcessMessage(msg, SessionId);
        }

        // Public method to safely send string messages over the data channel
        public bool TrySendDataChannelString(string str)
        {
            // Keep this as an implementation the processor will use
            try
            {
                var dc = DataChannel;
                if (dc == null) { return false; }
                dc.send(str);
                return true;
            }
            catch (Exception ex)
            {
                _log.Debug($"Failed to send data channel message: {ex.Message}", _client);
                return false;
            }
        }

        // Public facade methods that forward to the _dataChannelProcessor
        public bool SetPeerMute(UUID peerId, bool mute) => _dataChannelProcessor.SetPeerMute(peerId, mute);
        public bool SetPeerGain(UUID peerId, int gain) => _dataChannelProcessor.SetPeerGain(peerId, gain);

        public bool SendJoin(bool primary = true) => _dataChannelProcessor.SendJoin(primary);
        public bool SendLeave() => _dataChannelProcessor.SendLeave();

        /// <summary>
        /// Send avatar body pose (<c>sp</c>/<c>sh</c>) and camera/listener pose (<c>lp</c>/<c>lh</c>).
        /// Pass separate <paramref name="cameraPos"/>/<paramref name="cameraHeading"/> so the
        /// listener position matches the camera, not the avatar body.
        /// </summary>
        public bool SendPosition(Vector3d globalPos, Quaternion heading,
                                  Vector3d cameraPos, Quaternion cameraHeading)
        {
            var ok = _dataChannelProcessor.SendPosition(globalPos, heading, cameraPos, cameraHeading);
            if (ok)
            {
                _lastSentGlobalPos = globalPos;
                _lastSentHeading = heading;
                _lastSentCameraGlobalPos = cameraPos;
                _lastSentCameraHeading = cameraHeading;
                _spatialCoordsDirty = false;
            }
            return ok;
        }

        /// <summary>Legacy single-pose overload. Prefer the four-argument overload.</summary>
        public bool SendPosition(Vector3d globalPos, Quaternion heading)
            => SendPosition(globalPos, heading, globalPos, heading);

        public bool SendAvatarArray(List<UUID> avatars) => _dataChannelProcessor.SendAvatarArray(avatars);
        public bool SendAvatarMap(IEnumerable<UUID> avatars) => _dataChannelProcessor.SendAvatarMap(avatars);
        public bool SendMuteMap(Dictionary<UUID, bool> muteMap) => _dataChannelProcessor.SendMuteMap(muteMap);
        public bool SendGainMap(Dictionary<UUID, int> gainMap) => _dataChannelProcessor.SendGainMap(gainMap);

        // --- end data-channel message helpers ---

        private Task IceTrickleStart(CancellationToken external = default)
        {
            // If a trickle task is already running, just return it
            if (_iceTrickleTask != null
                && (_iceTrickleTask.Status == TaskStatus.Running
                || _iceTrickleTask.Status == TaskStatus.WaitingToRun
                || _iceTrickleTask.Status == TaskStatus.WaitingForActivation))
            {
                return _iceTrickleTask;

            }

            _iceTrickleCts = external == CancellationToken.None ? CancellationTokenSource.CreateLinkedTokenSource(_cts.Token) : CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, external);

            // Create a repeating task locally instead of calling Repeat.Interval to avoid
            // cross-assembly MethodAccessException. This loop polls every 25ms and invokes
            // the poll action when appropriate. The created task is returned so callers
            // can await or inspect its status if needed.
            var token = _iceTrickleCts.Token;
            async Task Loop()
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // Run the poll body.
                            await poll().ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _log.Warn($"IceTrickle loop poll exception: {ex.Message}", _client);
                        }

                        try { await Task.Delay(TimeSpan.FromMilliseconds(25), token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"IceTrickle loop terminated with exception: {ex.Message}", _client);
                }
            }

            _iceTrickleTask = Task.Run(Loop, token);

            return _iceTrickleTask;

            async Task poll()
            {
                if (_client.Network.Connected && !SessionId.Equals(UUID.Zero) && Interlocked.CompareExchange(ref _pendingCandidateCount, 0, 0) > 0)
                {
                    try
                    {
                        await SendVoiceSignalingRequest().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Swallow - cancellation will be handled by the loop's token checks
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"SendVoiceSignalingRequest failed in poll: {ex.Message} — scheduling reprovision (matches SL C++ SESSION_RETRY on ICE trickle failure)", _client);
                        try { ScheduleReprovisionWithBackoff(); } catch { }
                    }
                }
            }
        }

        private async Task IceTrickleStop()
        {
            // Wait for the provisioning answer before sending 'completed'.
            // ICE gathering can finish before the ProvisionVoiceAccountRequest response arrives.
            // Sending 'completed' without first sending candidates (which require _answerReceived
            // and a valid SessionId) leaves the server in an inconsistent state.
            using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            stopCts.CancelAfter(TimeSpan.FromSeconds(30)); // hard cap
            try
            {
                while (!stopCts.Token.IsCancellationRequested)
                {
                    // Wait until provisioning has completed and all pending candidates have been sent
                    if (!_answerReceived || GetPendingCandidateCount() > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(200), stopCts.Token).ConfigureAwait(false);
                        continue;
                    }
                    break;
                }
            }
            catch (OperationCanceledException) { }

            // Stop the periodic trickle loop — we're about to send the terminal 'completed' marker
            _iceTrickleCts?.Cancel();

            // Only send 'completed' when we have a valid session (i.e., provisioning succeeded)
            if (_answerReceived && !SessionId.Equals(UUID.Zero)
                && _peerConnection != null && _peerConnection.iceGatheringState == RTCIceGatheringState.complete)
            {
                await SendVoiceSignalingCompleteRequest().ConfigureAwait(false);
            }
        }

        // Schedule a reprovision attempt using exponential backoff. Prevents immediate retry storms
        // and adds diagnostic logging for each attempt.
        private void ScheduleReprovisionWithBackoff()
        {
            // Use CAS to prevent two concurrent callers from both scheduling a reprovision
            if (Interlocked.CompareExchange(ref _reprovisionScheduledInt, 1, 0) != 0) return;

            // Capture token before entering the lambda — _cts may be disposed by the time
            // the thread-pool picks up the task, which would cause ObjectDisposedException
            // when accessing _cts.Token inside the lambda.
            CancellationToken reprovisionToken;
            try { reprovisionToken = _cts.Token; }
            catch (ObjectDisposedException) { Interlocked.Exchange(ref _reprovisionScheduledInt, 0); return; }

            _ = Task.Run(async () =>
            {
                // Tracks whether the loop exited because of deliberate cancellation (session
                // torn down / disposed) rather than genuine exhaustion, so we don't fire a
                // misleading "reconnect failed" event during an intentional Disconnect().
                bool cancelled = false;
                try
                {
                    int attempt = 0;
                    const int maxAttempts = 6; // up to ~1m total backoff
                    // The trigger that got us here (watchdog timeout or an explicit failure
                    // callback) already establishes that something is wrong, so the first
                    // attempt fires immediately — only attempts after a *repeated* failure
                    // back off, to avoid retry storms without adding a full extra second to
                    // every single reprovision cycle (SL C++ has no equivalent pre-delay).
                    int delayMs = 0;

                    while (!reprovisionToken.IsCancellationRequested && attempt < maxAttempts)
                    {
                        attempt++;
                        if (delayMs > 0)
                        {
                            _log.Debug($"Reprovision scheduled attempt {attempt}/{maxAttempts} in {delayMs}ms", _client);
                            try { await Task.Delay(delayMs, reprovisionToken).ConfigureAwait(false); } catch (OperationCanceledException) { cancelled = true; break; }
                        }
                        else
                        {
                            _log.Debug($"Reprovision scheduled attempt {attempt}/{maxAttempts} (immediate)", _client);
                        }

                        try
                        {
                            _log.Debug($"Starting scheduled reprovision attempt {attempt}", _client);
                            // AttemptReprovisionAsync() reports failure via its bool return (and
                            // an OnReprovisionFailed event for this specific attempt), not by
                            // throwing — it never lets an exception escape its own try/catch
                            // blocks. Treating "didn't throw" as "succeeded" here previously
                            // meant this loop always stopped after exactly one attempt no matter
                            // the outcome, so a persistently failing reprovision (bad network,
                            // stuck ICE) would never back off or give up — each new watchdog
                            // trigger (12s/60s) just restarted the cycle, forever.
                            bool ok = await AttemptReprovisionAsync().ConfigureAwait(false);
                            if (ok)
                            {
                                _log.Debug($"Scheduled reprovision attempt {attempt} succeeded", _client);
                                return;
                            }
                            _log.Warn($"Scheduled reprovision attempt {attempt} failed", _client);
                        }
                        catch (OperationCanceledException) { cancelled = true; break; }
                        catch (Exception ex)
                        {
                            _log.Warn($"Scheduled reprovision attempt {attempt} threw: {ex.Message}", _client);
                        }

                        // Exponential backoff for next attempt (first retry after the immediate
                        // attempt waits 1s, then doubles as before).
                        delayMs = delayMs == 0 ? 1000 : Math.Min(delayMs * 2, 60000);
                    }

                    if (!cancelled)
                    {
                        _log.Warn($"Scheduled reprovision exhausted after {attempt} attempts, giving up", _client);
                        try { OnReprovisionFailed?.Invoke(new VoiceException($"Voice reconnection failed after {attempt} attempts")); } catch { }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _reprovisionScheduledInt, 0);
                }
            }, reprovisionToken);
        }
        private string SanitizeRemoteSdp(string sdp)
        {
            if (string.IsNullOrEmpty(sdp)) { return sdp; }
            var sb = new StringBuilder();
            var lines = sdp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // Drop candidate lines that contain a literal port 0 (invalid)
                if (line.StartsWith("a=candidate:", StringComparison.OrdinalIgnoreCase))
                {
                    // Regex to find whitespace + port number + whitespace before typ or end
                    // Candidate format contains the port as the 5th token. Simpler check for ' 0 ' is sufficient.
                    if (Regex.IsMatch(line, "\\s0\\s"))
                    {
                        _log.Debug($"Dropping remote ICE candidate with port 0: {line}", _client);
                        continue;
                    }
                }

                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        private string ProcessLocalSdp(string sdp)
        {
            var lines = sdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var output = new List<string>();
            string? opusPayload = null;
            bool inAudioSection = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("m=audio"))
                {
                    inAudioSection = true;
                    output.Add(line);
                    continue;
                }

                if (inAudioSection && line.StartsWith("m="))
                {
                    inAudioSection = false;
                }

                if (line.Contains("opus/48000"))
                {
                    var match = Regex.Match(line, @"a=rtpmap:(\d+)\s+opus");
                    if (match.Success)
                    {
                        opusPayload = match.Groups[1].Value;
                        output.Add($"a=rtpmap:{opusPayload} opus/48000/2");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(opusPayload) && line.StartsWith($"a=fmtp:{opusPayload}"))
                {
                    // EXACT parameters from spec - no extras
                    var fmtp = "minptime=10;useinbandfec=1;stereo=1;sprop-stereo=1;maxplaybackrate=48000";
                    output.Add($"a=fmtp:{opusPayload} {fmtp}");
                    continue;
                }

                output.Add(line);
            }

            return string.Join("\r\n", output);
        }

        // Returns true if this attempt reconnected successfully, false otherwise. The caller
        // (ScheduleReprovisionWithBackoff) relies on this to know whether to retry — every
        // failure branch below reports its outcome via OnReprovisionFailed but does not throw,
        // so a bool return (rather than an exception) is the only reliable signal.
        private async Task<bool> AttemptReprovisionAsync()
        {
            // Ensure only one reprovision runs at a time
            if (!await _reprovisionLock.WaitAsync(0))
            {
                _log.Debug("Reprovision already in progress, skipping.", _client);
                return true;
            }

            try
            {
                _log.Debug($"Starting reprovision for session {SessionId}", _client);

                // Cancel any pending ICE-checking watchdog for the outgoing PC.
                try { _iceCheckingWatchdogCts?.Cancel(); } catch { }

                // Store recording state before closing
                bool wasRecording = _audioDevice?.Source != null && _audioDevice.RecordingActive;
                bool wasPlaybackActive = _audioDevice?.EndPoint != null && _audioDevice.PlaybackActive;

                try
                {
                    // Stop audio before closing peer connection
                    if (_audioDevice != null)
                    {
                        try { _audioDevice.StopRecording(); } catch { }
                        try { _audioDevice.StopPlaybackAsync().Wait(250); } catch { }
                    }

                    // Close existing peer connection if present
                    if (_peerConnection != null)
                    {
                        // Detach audio source handler before closing
                        if (_audioDevice?.Source != null)
                        {
                            try { _audioDevice.Source.OnAudioSourceEncodedSample -= _peerConnection.SendAudio; } catch { }
                        }
                        // Detach _audioDevice level handler
                        if (_audioDevice != null)
                        {
                            try { _audioDevice.OnAudioSourceEncodedSample -= _peerConnection.SendAudio; } catch { }
                        }
                        try { _peerConnection.Close("Reprovision"); } catch { }
                    }

                    // Tell the SL server to close the old session before we re-provision.
                    // Matches SL C++ breakVoiceConnectionCoro — without this the server-side
                    // Janus session is still "occupied" and the new ICE exchange fails.
                    if (!SessionId.Equals(UUID.Zero))
                    {
                        try { await SendCloseSessionRequest().ConfigureAwait(false); } catch { }
                        SessionId = UUID.Zero;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"Error closing peer connection during reprovision: {ex.Message}", _client);
                }

                // Cancel any existing trickle loop
                try { _iceTrickleCts?.Cancel(); } catch { }

                // Reset answer flag to ensure ICE candidates wait for new answer
                _answerReceived = false;

                // Reset keepalive timestamp so the first position send after reconnect
                // is not delayed by the old send time.
                _lastPositionSentMs = 0;
                _spatialCoordsDirty = true;

                // Clear all known peers and audio state before reprovisioning
                try { _peerManager.ClearAllPeers(); } catch { }

                // Create a new peer connection
                RTCPeerConnection? newPc = null;
                try
                {
                    newPc = await CreatePeerConnectionAsync();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create new RTCPeerConnection during reprovision: {ex.Message}", _client);
                }

                if (newPc != null)
                {
                    _peerConnection = newPc;

                    // Start trickle loop
                    _ = IceTrickleStart();

                    // Re-request provisioning from the simulator
                    try
                    {
                        var ok = await RequestProvisionAsync();
                        if (ok)
                        {
                            _log.Debug($"Reprovision completed, new session {SessionId}", _client);

                            // Restore audio state after successful reprovision
                            if (_audioDevice != null)
                            {
                                // Wait a moment for peer connection to stabilize
                                await Task.Delay(500).ConfigureAwait(false);

                                // Do not restore recording here — OnPeerConnectionReady fires
                                // OnConnectionReady in the UI which owns mic/PTT state.
                                // Blindly calling StartRecording() here would fight PTT mode.
                                if (wasPlaybackActive)
                                {
                                    try { await _audioDevice.StartPlaybackAsync().ConfigureAwait(false); } catch (Exception ex) { _log.Warn($"Failed to restart playback after reprovision: {ex.Message}", _client); }
                                }
                            }

                            try { OnReprovisionSucceeded?.Invoke(); } catch { }
                            return true;
                        }
                        else
                        {
                            _log.Warn("Reprovision failed: _client not connected to network.", _client);
                            try { OnReprovisionFailed?.Invoke(new Exception("_client not connected to network")); } catch { }
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Reprovision RequestProvisionAsync failed: {ex.Message}", _client);
                        try { OnReprovisionFailed?.Invoke(ex); } catch { }
                        return false;
                    }
                }
                else
                {
                    var ex = new Exception("Failed to create new RTCPeerConnection during reprovision");
                    try { OnReprovisionFailed?.Invoke(ex); } catch { }
                    return false;
                }
            }
            finally
            {
                if (!_disposed)
                {
                    try { _reprovisionLock.Release(); } catch (ObjectDisposedException) { }
                }
            }
        }

        // Sends a position update over the data channel (keeps last-sent tracking)
        private void SendPositionUpdate(RTCDataChannel dc, Vector3d globalPos, Quaternion heading,
            Vector3d cameraGlobalPos, Quaternion cameraHeading)
        {
            // Only send when flagged dirty (set when values change or keepalive interval elapses)
            if (!_spatialCoordsDirty) { return; }

            // Track time of last send for keepalive purposes
            _lastPositionSentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                // Avatar body position/heading — sp/sh
                int spX = (int)Math.Round(globalPos.X * 100);
                int spY = (int)Math.Round(globalPos.Y * 100);
                int spZ = (int)Math.Round(globalPos.Z * 100);
                int shX = (int)Math.Round(heading.X * 100);
                int shY = (int)Math.Round(heading.Y * 100);
                int shZ = (int)Math.Round(heading.Z * 100);
                int shW = (int)Math.Round(heading.W * 100);

                // Camera (listener) position/heading — lp/lh
                int lpX = (int)Math.Round(cameraGlobalPos.X * 100);
                int lpY = (int)Math.Round(cameraGlobalPos.Y * 100);
                int lpZ = (int)Math.Round(cameraGlobalPos.Z * 100);
                int lhX = (int)Math.Round(cameraHeading.X * 100);
                int lhY = (int)Math.Round(cameraHeading.Y * 100);
                int lhZ = (int)Math.Round(cameraHeading.Z * 100);
                int lhW = (int)Math.Round(cameraHeading.W * 100);

                var ms = new MemoryStream();
                using var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = true });
                jw.WriteStartObject();

                jw.WriteStartObject("sp");
                jw.WriteNumber("x", spX); jw.WriteNumber("y", spY); jw.WriteNumber("z", spZ);
                jw.WriteEndObject();

                jw.WriteStartObject("sh");
                jw.WriteNumber("x", shX); jw.WriteNumber("y", shY); jw.WriteNumber("z", shZ); jw.WriteNumber("w", shW);
                jw.WriteEndObject();

                jw.WriteStartObject("lp");
                jw.WriteNumber("x", lpX); jw.WriteNumber("y", lpY); jw.WriteNumber("z", lpZ);
                jw.WriteEndObject();

                jw.WriteStartObject("lh");
                jw.WriteNumber("x", lhX); jw.WriteNumber("y", lhY); jw.WriteNumber("z", lhZ); jw.WriteNumber("w", lhW);
                jw.WriteEndObject();

                jw.WriteEndObject();
                jw.Flush();

                var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

                //_log.Debug($"Sending Position: {json}", _client);
                TrySendDataChannelString(json);

                // Update last sent and clear dirty flag
                _lastSentGlobalPos = globalPos;
                _lastSentHeading = heading;
                _lastSentCameraGlobalPos = cameraGlobalPos;
                _lastSentCameraHeading = cameraHeading;
                _spatialCoordsDirty = false;
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send position on data channel: {ex.Message}", _client);
            }
        }

        // Computes distance from the local camera to a remote peer and adjusts their SSRC gain.
        // Uses a simple inverse-distance rolloff matching SL C++ falloff (clamped at 1–50m range).
        // SL C++: MAX_AUDIO_DIST = 50.0f, PEER_GAIN_CONVERSION_FACTOR = 220.
        private void ApplyDistanceAttenuation(UUID peerId, OSDMap map)
        {
            if (_audioDevice == null) return;
            if (!_peerManager.TryGetSsrc(peerId, out var ssrc)) return;

            // Peer's speaker position in global coords (centimeters → meters)
            if (!map.TryGetValue("sp", out var spOsd) || spOsd is not OSDMap sp) return;
            double peerX = sp["x"].AsReal() / 100.0;
            double peerY = sp["y"].AsReal() / 100.0;
            double peerZ = sp["z"].AsReal() / 100.0;

            // Our listener (camera) global position
            var listenerPos = _client?.Self?.GlobalPosition ?? Vector3d.Zero;
            try
            {
                var cam = _client?.Self?.Movement?.Camera;
                if (cam != null && _client?.Network?.CurrentSim != null)
                {
                    Utils.LongToUInts(_client.Network.CurrentSim.Handle, out var gx, out var gy);
                    listenerPos = new Vector3d(gx + cam.Position.X, gy + cam.Position.Y, cam.Position.Z);
                }
            }
            catch { }

            if (listenerPos == Vector3d.Zero) return;

            double dx = peerX - listenerPos.X;
            double dy = peerY - listenerPos.Y;
            double dz = peerZ - listenerPos.Z;
            double distanceMeters = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // SL C++ voice: MAX_AUDIO_DIST = 50m, full gain ≤ 1m, silence at 50m.
            // Per-peer gain on the wire uses 0–220 range (PEER_GAIN_CONVERSION_FACTOR = 220).
            const double minDist = 1.0;   // full volume within 1m
            const double maxDist = 50.0;  // silence at 50m (matches SL MAX_AUDIO_DIST)
            // 220 is SL's PEER_GAIN_CONVERSION_FACTOR: volume 0.0–1.0 maps to 0–220
            const int gainMax = 220;
            int gainValue;
            if (distanceMeters <= minDist)
                gainValue = gainMax;
            else if (distanceMeters >= maxDist)
                gainValue = 0;
            else
                gainValue = (int)Math.Round(gainMax * (maxDist - distanceMeters) / (maxDist - minDist));

            // Store as percent for SDL3 (0–220 → 0.0–2.2 linear gain; SDL3 API accepts 0–200%)
            int gainPercent = (int)Math.Round(gainValue / 2.20);
            _audioDevice.SetSsrcGainPercent(ssrc, gainPercent);
        }

        // Minimal implementation of RequestProvisionAsync to satisfy callers.
        // The real implementation is more involved; this stub returns false.
        public async Task<bool> RequestProvisionAsync()
        {
            if (_client?.Network == null || !_client.Network.Connected) { return false; }
            _log.Debug("Requesting voice capability...", _client);

            var cap = EffectiveSim?.Caps?.CapabilityURI(PROVISION_VOICE_ACCOUNT_CAP);
            if (cap == null)
            {
                throw new VoiceException($"No {PROVISION_VOICE_ACCOUNT_CAP} capability available.");
            }

            switch (SessionType)
            {
                case ESessionType.LOCAL:
                    await RequestLocalVoiceProvision(cap);
                    break;
                case ESessionType.MULTIAGENT:
                    await RequestMultiAgentVoiceProvision(cap);
                    break;
            }

            return true;
        }

        private async Task RequestLocalVoiceProvision(Uri cap)
        {
            // Use the parcel local ID set by VoiceManager based on parcel flags.
            // SL C++: body["parcel_local_id"] = mParcelLocalID  (only when != INVALID_PARCEL_ID)
            var payload = new LocalVoiceProvisionRequest(SdpLocal, ParcelLocalId).Serialize();
            _log.Debug("==> Attempting to POST for voice provision...", _client!);
            if (_client?.HttpCapsClient == null)
            {
                _log.Error("HttpCapsClient is null; cannot post provisioning request", _client!);
                throw new VoiceException("Internal error: HttpCapsClient is null.");
            }
            var osd = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
            _log.Debug("Received provisioning response", _client!);
            if (osd is OSDMap osdMap)
            {
                if (!osdMap.TryGetValue("jsep", out var j))
                {
                    var simName = _client.Network?.CurrentSim?.Name ?? "(unknown)";
                    throw new VoiceException($"Region '{simName}' does not support WebRtc.");
                }

                var jsep = (OSDMap)j;
                // Detect server-provided error codes/messages that indicate credentials/channel expired
                try
                {
                    if (osdMap.ContainsKey("error") || osdMap.ContainsKey("errmsg") || osdMap.ContainsKey("message"))
                    {
                        var emsg = osdMap.ContainsKey("error") ? osdMap["error"].AsString() : osdMap.ContainsKey("errmsg") ? osdMap["errmsg"].AsString() : osdMap.ContainsKey("message") ? osdMap["message"].AsString() : null;
                        if (!string.IsNullOrEmpty(emsg))
                        {
                            var lower = emsg!.ToLowerInvariant();
                            if (lower.Contains("credential") || lower.Contains("credentials") || lower.Contains("expired") || lower.Contains("invalid") || lower.Contains("channel") || lower.Contains("denied"))
                            {
                                _log.Warn($"Provisioning response indicated credential/channel problem: {emsg}", _client);
                                // Clear local channel state and schedule reprovision
                                ChannelId = null;
                                ChannelCredentials = null;
                                ScheduleReprovisionWithBackoff();
                                throw new VoiceException($"Provisioning failed due to credentials/channel issue: {emsg}");
                            }
                        }
                    }
                }
                catch { }

                var sdpString = jsep["sdp"].AsString();
                sdpString = SanitizeRemoteSdp(sdpString);
                var sessionId = osdMap.ContainsKey("viewer_session")
                    ? osdMap["viewer_session"].AsUUID()
                    : UUID.Zero;

                SessionId = sessionId;

                // Set remote description
                if (_peerConnection == null)
                {
                    _log.Error("_peerConnection unexpectedly null during RequestLocalVoiceProvision", _client);
                    throw new VoiceException("Internal error: _peerConnection was null.");
                }
                var set = _peerConnection.SetRemoteDescription(
                    SdpType.answer,
                    SDP.ParseSDPDescription(sdpString)
                );

                if (set != SetDescriptionResultEnum.OK)
                {
                    _peerConnection.Close("Failed to set remote description.");
                    throw new VoiceException("Failed to set remote description.");
                }

                // CRITICAL: Set flag BEFORE flushing candidates
                _answerReceived = true;

                // Now flush any pending candidates
                await SendVoiceSignalingRequest().ConfigureAwait(false);

                // Watchdog: 60s timeout to match SL's ICE negotiation tolerance.
                // Capture the specific PC so a later reprovision replacing _peerConnection does not
                // cause this watchdog to tear down the new (already-healthy) connection.
                var watchedPc = _peerConnection;
                var watchedSession = SessionId;
                // Capture token before the lambda — _cts may be disposed before the 60s elapses.
                CancellationToken watchdogToken;
                try { watchdogToken = _cts.Token; }
                catch (ObjectDisposedException) { watchdogToken = CancellationToken.None; }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), watchdogToken).ConfigureAwait(false);
                        // Only act if this session is still the active one; an earlier reprovision
                        // may have already replaced it with a healthy connection.
                        if (SessionId != watchedSession) return;
                        if (watchedPc != null && watchedPc.connectionState != RTCPeerConnectionState.connected)
                        {
                            _log.Warn($"Peer connection for session {watchedSession} did not become connected after 60s (state={watchedPc.connectionState}); scheduling reprovision.", _client);
                            try { ScheduleReprovisionWithBackoff(); } catch { }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }, watchdogToken);

                // Store channel info if present
                if (osdMap.ContainsKey("channel")) ChannelId = osdMap["channel"].AsString() ?? string.Empty;
                if (osdMap.ContainsKey("credentials")) ChannelCredentials = osdMap["credentials"].AsString() ?? string.Empty;

                _log.Debug($"Local voice provisioned: session={sessionId}", _client);
            }
        }

        private async Task RequestMultiAgentVoiceProvision(Uri cap)
        {
            var req = new MultiAgentVoiceProvisionRequest(SdpLocal);
            // include channel/credentials if present on this session
                if (!string.IsNullOrEmpty(ChannelId)) req.ChannelId = ChannelId!;
            if (!string.IsNullOrEmpty(ChannelCredentials)) req.ChannelCredentials = ChannelCredentials!;

            var payload = req.Serialize();
            var osd = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
            if (osd is OSDMap osdMap)
            {
                if (!osdMap.ContainsKey("jsep"))
                {
                    var simName = _client.Network?.CurrentSim?.Name ?? "(unknown)";
                    throw new VoiceException($"Region '{simName}' does not support WebRtc.");
                }
                var jsep = (OSDMap)osdMap["jsep"];
                if (jsep.ContainsKey("type") && jsep["type"].AsString() != "answer")
                {
                    var simName = _client.Network?.CurrentSim?.Name ?? "(unknown)";
                    throw new VoiceException($"jsep returned from '{simName}' is not an answer.");
                }
                var sdpString = jsep["sdp"].AsString();
                sdpString = SanitizeRemoteSdp(sdpString);
                var sessionId = osdMap.ContainsKey("viewer_session") ? osdMap["viewer_session"].AsUUID() : UUID.Zero;

                if (_peerConnection == null)
                {
                    _log.Error("_peerConnection unexpectedly null during RequestMultiAgentVoiceProvision", _client);
                    throw new VoiceException("Internal error: _peerConnection was null.");
                }
                var desc = SDP.ParseSDPDescription(sdpString);
                var set = _peerConnection.SetRemoteDescription(SdpType.answer, desc);
                if (set != SetDescriptionResultEnum.OK)
                {
                    _peerConnection.Close("Failed to set remote description (multiagent).");
                    throw new VoiceException("Failed to set remote description (multiagent).");
                }

                // Ensure the session id is stored before we start flushing/trickling ICE candidates
                SessionId = sessionId;

                // Mark that we've received the answer and flush any pending ICE candidates
                _answerReceived = true;
                _ = FlushPendingIceCandidates();

                // Watchdog: 60s timeout — STUN negotiation through NAT can legitimately take >30s.
                // Capture the specific PC so a later reprovision replacing _peerConnection does not
                // cause this watchdog to tear down the new (already-healthy) connection.
                var watchedPc = _peerConnection;
                var watchedSession = SessionId;
                // Capture token before the lambda — _cts may be disposed before the 60s elapses.
                CancellationToken watchdogToken;
                try { watchdogToken = _cts.Token; }
                catch (ObjectDisposedException) { watchdogToken = CancellationToken.None; }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), watchdogToken).ConfigureAwait(false);
                        // Only act if this session is still the active one.
                        if (SessionId != watchedSession) return;
                        if (watchedPc != null && watchedPc.connectionState != RTCPeerConnectionState.connected)
                        {
                            _log.Warn($"Multi-agent peer connection for session {watchedSession} did not become connected after 60s (state={watchedPc.connectionState}); scheduling reprovision.", _client);
                            try { ScheduleReprovisionWithBackoff(); } catch { }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }, watchdogToken);

                if (osdMap.ContainsKey("channel")) ChannelId = osdMap["channel"].AsString();
                if (osdMap.ContainsKey("credentials")) ChannelCredentials = osdMap["credentials"].AsString();

                _log.Debug($"Multi-agent voice provisioned: session={sessionId}", _client);
            }
        }

        private async Task SendCloseSessionRequest(CancellationToken ct = default)
        {
            var cap = EffectiveSim?.Caps?.CapabilityURI(PROVISION_VOICE_ACCOUNT_CAP);

            _log.Debug($"Closing voice session {SessionId}", _client);
            var payload = new OSDMap
              {
                  { "logout", true },
                  { "voice_server_type", "webrtc" },
                  { "viewer_session", SessionId }
              };

            // If this session is multi-agent, and we have a channel, include it so the mixer
            // can close the channel/credentials server-side as part of logout.
            try
            {
                if (!string.IsNullOrEmpty(ChannelId))
                {
                    payload["channel"] = ChannelId!;
                    if (!string.IsNullOrEmpty(ChannelCredentials)) payload["credentials"] = ChannelCredentials!;
                }

                if (cap != null)
                {
                    await PostCapsWithRetries(cap, payload, ct: ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Close session request failed: {ex.Message}", _client);
            }

            // Clear local channel state after attempting to close server-side
            ChannelId = null;
            ChannelCredentials = null;
        }

        // Close and cleanup session resources
        public async Task CloseSessionAsync()
        {
            // Stop loops
            StopPositionLoop();
            StopKeepAliveLoop();

            // Clear peer and SSRC state
            try { _peerManager.ClearAllPeers(); } catch { }

            _peerConnection?.Close("ClientClose");

            // Use a dedicated short-lived token for the close-sequence network calls so that
            // they never touch _cts.Token.  Dispose() may cancel and dispose _cts concurrently
            // (e.g. when CloseSessionAsync is fire-and-forgotten), which would otherwise cause an
            // ObjectDisposedException inside PostCapsWithRetries.
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await SendCloseSessionRequest(shutdownCts.Token).ConfigureAwait(false);
            // Do NOT send ICE signaling complete here — the server-side session is already
            // closed by SendCloseSessionRequest, so posting signaling-complete afterward
            // always yields "Unknown session". SL C++ breakVoiceConnectionCoro never sends
            // signaling-complete as part of teardown either.

            // Cancel internal token source to stop any background work.
            // Guard against a concurrent Dispose() having already cancelled and disposed _cts.
            if (!_disposed)
            {
                try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }

        private volatile bool _disposed = false;

        // IDisposable implementation for safe cleanup
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // Stop position and keepalive loops
                try { StopPositionLoop(); } catch { }
                try { StopKeepAliveLoop(); } catch { }

                // Cancel and dispose ice trickle loop
                try
                {
                    _iceTrickleCts?.Cancel();
                    try { _iceTrickleTask?.Wait(250); } catch { }
                    _iceTrickleCts?.Dispose();
                    _iceTrickleCts = null;
                    _iceTrickleTask = null;
                }
                catch { }

                // Cancel ICE-checking watchdog
                try { _iceCheckingWatchdogCts?.Cancel(); _iceCheckingWatchdogCts?.Dispose(); _iceCheckingWatchdogCts = null; } catch { }

                // Detach audio handlers
                try
                {
                    if (_audioDevice?.Source != null && _peerConnection != null)
                    {
                        try { _audioDevice.Source.OnAudioSourceEncodedSample -= _peerConnection.SendAudio; } catch { }
                    }
                    if (_audioDevice != null && _peerConnection != null)
                    {
                        try { _audioDevice.OnAudioSourceEncodedSample -= _peerConnection.SendAudio; } catch { }
                    }
                }
                catch { }

                // Stop audio
                try
                {
                    if (_audioDevice != null)
                    {
                        try { _audioDevice.StopRecording(); } catch { }
                        try { _audioDevice.StopPlaybackAsync().Wait(250); } catch { }
                    }
                }
                catch { }

                // Clear peers and SSRC state
                try { _peerManager.ClearAllPeers(); } catch { }

                // Close peer connection
                try
                {
                    if (_peerConnection != null)
                    {
                        try { _peerConnection.Close("Dispose"); } catch { }
                        // If RTCPeerConnection exposes Dispose, call it (best-effort)
                        try { (_peerConnection as IDisposable)?.Dispose(); } catch { }
                        _peerConnection = null;
                    }
                }
                catch { }

                // Cancel main _cts
                try { _cts.Cancel(); } catch { }
                try { _cts.Dispose(); } catch { }

                // Dispose other token sources
                try { _positionLoopCts?.Dispose(); } catch { }
                _positionLoopCts = null;

                // Release semaphore
                try { _reprovisionLock.Dispose(); } catch { }

                // Null out delegates to help GC
                try
                {
                    OnPeerConnectionClosed = null;
                    OnPeerConnectionReady = null;
                    OnDataChannelReady = null;
                    OnReprovisionSucceeded = null;
                    OnReprovisionFailed = null;
                    OnPeerJoined = null;
                    OnPeerLeft = null;
                    OnPeerPositionUpdated = null;
                    OnPeerListUpdated = null;
                    OnPeerAudioUpdated = null;
                    OnPeerPositionUpdatedTyped = null;
                    OnMuteMapReceived = null;
                    OnGainMapReceived = null;
                }
                catch { }
            }
        }

        ~VoiceSession()
        {
            Dispose(false);
        }

    }
}
