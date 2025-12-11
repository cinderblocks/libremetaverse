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

using LitJson;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;
using System;
using System.Collections.Concurrent;
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
            MUTLIAGENT
        }

        private readonly GridClient Client;
        private readonly Sdl3Audio AudioDevice;
        private readonly IVoiceLogger _log;
        private RTCPeerConnection PeerConnection;

        public event Action OnPeerConnectionClosed;
        public event Action OnPeerConnectionReady;
        public event Action OnDataChannelReady;

        // Reprovision events
        public event Action OnReprovisionSucceeded;
        public event Action<Exception> OnReprovisionFailed;

        // New events for data-channel protocol
        public event Action<UUID> OnPeerJoined;
        public event Action<UUID> OnPeerLeft;
        public event Action<UUID, OSDMap> OnPeerPositionUpdated;
        public event Action<List<UUID>> OnPeerListUpdated;

        public class PeerAudioState
        {
            public int? Power { get; set; }
            public bool? VoiceActive { get; set; }
            public bool? JoinedPrimary { get; set; }
            public bool Left { get; set; }
        }
        public event Action<UUID, PeerAudioState> OnPeerAudioUpdated;
        private bool answerReceived = false;
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
        public event Action<UUID, AvatarPosition> OnPeerPositionUpdatedTyped;
        public event Action<Dictionary<UUID, bool>> OnMuteMapReceived;
        public event Action<Dictionary<UUID, int>> OnGainMapReceived;

        public UUID SessionId { get; private set; }
        public string SdpLocal => PeerConnection?.localDescription.sdp.ToString();
        public string SdpRemote => PeerConnection?.remoteDescription.sdp.ToString();

        public bool Connected => PeerConnection?.connectionState == RTCPeerConnectionState.connected;
        public RTCDataChannel DataChannel => PeerConnection?.DataChannels?.FirstOrDefault();
        private ESessionType SessionType { get; }
        private readonly ConcurrentQueue<RTCIceCandidate> PendingCandidates = new ConcurrentQueue<RTCIceCandidate>();
        // Fast approximate count to avoid expensive ConcurrentQueue.Count in hot paths
        private int pendingCandidateCount = 0;
        // Lock to keep queue and counter consistent for batch operations
        private readonly object _candidateLock = new object();
        private readonly CancellationTokenSource Cts = new CancellationTokenSource();

        private CancellationTokenSource iceTrickleCts;
        private Task iceTrickleTask;
        // Prevent concurrent reprovision attempts
        private readonly SemaphoreSlim reprovisionLock = new SemaphoreSlim(1, 1);
        private volatile bool reprovisionScheduled = false;

        // Multi-agent channel fields
        public string ChannelId { get; set; }
        public string ChannelCredentials { get; set; }

        // Centralized peer and SSRC management helper
        private readonly PeerManager peerManager;
        private readonly DataChannelProcessor dataChannelProcessor;

        // Return a snapshot list of known peer UUIDs
        public List<UUID> GetKnownPeers()
        {
            try
            {
                return peerManager.GetKnownPeers();
            }
            catch
            {
                return new List<UUID>();
            }
        }

        // Position and keepalive loop cancellation/tasks
        private CancellationTokenSource positionLoopCts;
        private Task positionLoopTask;
        private CancellationTokenSource keepAliveLoopCts;
        private Task keepAliveLoopTask;

        // Whether spatial coords changed since last send — only send updates when this is true
        private volatile bool mSpatialCoordsDirty = true;
        // Last observed values (used to detect changes)
        private Vector3d lastObservedGlobalPos = Vector3d.Zero;
        private Quaternion lastObservedHeading = Quaternion.Identity;
        // Last sent values (used to avoid resending identical payloads)
        private Vector3d lastSentGlobalPos = Vector3d.Zero;
        private Quaternion lastSentHeading = Quaternion.Identity;
        // Thresholds for detecting meaningful changes
        private const double POSITION_CHANGE_THRESHOLD_METERS = 0.01; // 1cm
        private const double HEADING_CHANGE_THRESHOLD = 0.0005; // small quaternion delta

        // RTP/RTCP diagnostic fields
        private readonly long lastRtpReceivedTicks = 0;
        private readonly long lastRtcpReceivedTicks = 0;
        private readonly long lastRtcpSentTicks = 0;
        private readonly MediaStreamTrack _remoteAudioTrack;

        internal VoiceSession(Sdl3Audio audioDevice, ESessionType type, GridClient client, IVoiceLogger logger = null)
        {
            Client = client;
            AudioDevice = audioDevice;
            SessionType = type;
            SessionId = UUID.Zero;
            _log = logger ?? new OpenMetaverseVoiceLogger();

            // Initialize peer manager and forward its events to existing VoiceSession events
            peerManager = new PeerManager(AudioDevice, Client, _log);
            peerManager.PeerJoined += id => { try { OnPeerJoined?.Invoke(id); } catch { } };
            peerManager.PeerLeft += id => { try { OnPeerLeft?.Invoke(id); } catch { } };
            peerManager.PeerPositionUpdated += (id, map) => { try { OnPeerPositionUpdated?.Invoke(id, map); } catch { } };
            peerManager.PeerListUpdated += list => { try { OnPeerListUpdated?.Invoke(list); } catch { } };
            peerManager.PeerAudioUpdated += (id, state) => { try { OnPeerAudioUpdated?.Invoke(id, state); } catch { } };
            peerManager.MuteMapReceived += m => { try { OnMuteMapReceived?.Invoke(m); } catch { } };
            peerManager.GainMapReceived += g => { try { OnGainMapReceived?.Invoke(g); } catch { } };

            dataChannelProcessor = new DataChannelProcessor(peerManager, Client, _log, TrySendDataChannelString);
        }
        public async Task StartAsync(CancellationToken ct = default)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, ct))
            {
                var token = linked.Token;

                PeerConnection = await CreatePeerConnection(token).ConfigureAwait(false);
                iceTrickleTask = IceTrickleStart(token);

                // Do not start recording here. Recording should follow connection state to avoid
                // capturing audio before the peer connection is established.
            }
        }

        // Helper that posts to caps and returns deserialized OSD, with retries and timeout handling
        private async Task<OSD> PostCapsWithRetries(Uri cap, OSD payload, int maxAttempts = 10, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (cap == null) throw new VoiceException("Capability URI is null.");
            if (timeout == null) timeout = TimeSpan.FromSeconds(10);

            int attempt = 0;
            Exception lastEx = null;
            while (attempt < maxAttempts)
            {
                attempt++;
                var tcs = new TaskCompletionSource<(object response, byte[] data, Exception err)>();
                try
                {
                    var token = ct == CancellationToken.None ? Cts.Token : ct;
                    _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, token, (response, data, error) =>
                    {
                        tcs.TrySetResult((response, data, error));
                    });

                    var delayTask = Task.Delay(timeout.Value, token);
                    var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                    if (completed == delayTask)
                    {
                        lastEx = new TimeoutException($"POST to {cap} timed out.");
                    }
                    else
                    {
                        var (response, data, err) = await tcs.Task.ConfigureAwait(false);
                        if (err != null)
                        {
                            lastEx = err;
                        }
                        else
                        {
                            try
                            {
                                // Attempt to deserialize LLSD/OSD. If parsing fails, capture raw response for diagnostics.
                                var osd = OSDParser.Deserialize(data);
                                return osd;
                            }
                            catch (Exception parseEx)
                            {
                                string respText = string.Empty;
                                try { respText = Encoding.UTF8.GetString(data); } catch { }

                                // Try to infer HTTP status code or status text from the response object if available
                                int? statusCode = null;
                                string statusText = null;
                                try
                                {
                                    if (response != null)
                                    {
                                        var respType = response.GetType();
                                        var statusProp = respType.GetProperty("StatusCode") ?? respType.GetProperty("Status");
                                        if (statusProp != null)
                                        {
                                            var val = statusProp.GetValue(response);
                                            if (val != null)
                                            {
                                                statusText = val.ToString();
                                                if (int.TryParse(statusText, out var si)) statusCode = si;
                                            }
                                        }

                                        // Some HttpResponseMessage-like types expose ReasonPhrase
                                        var reasonProp = respType.GetProperty("ReasonPhrase") ?? respType.GetProperty("StatusDescription");
                                        if (reasonProp != null && statusText == null)
                                        {
                                            var rv = reasonProp.GetValue(response);
                                            if (rv != null) statusText = rv.ToString();
                                        }
                                    }
                                }
                                catch { }

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
                    await Task.Delay(backoffMs, ct == CancellationToken.None ? Cts.Token : ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }

            throw new VoiceException($"Failed to POST to capability {cap}: {lastEx?.Message}");
        }

        public async Task<RTCPeerConnection> CreatePeerConnection(CancellationToken ct = default)
        {
            var iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun1.agni.secondlife.io:3478" },
                new RTCIceServer { urls = "stun:stun2.agni.secondlife.io:3478" },
                new RTCIceServer { urls = "stun:stun3.agni.secondlife.io:3478" },
                new RTCIceServer { urls = "stun:stun1.agni.secondlife.io:3478" },
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun2.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun.nextcloud.com:443" },
                new RTCIceServer { urls = "stun:stun.twilio.com:3478" }
            };

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
                    _log.Debug($"Added TURN server from environment: {turnUrl}", Client);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to read TURN environment variables: {ex.Message}", Client);
            }

            var pc = new RTCPeerConnection(new RTCConfiguration
            {
                X_ICEIncludeAllInterfaceAddresses = false,
                iceServers = iceServers
            }, 0, new PortRange(49152, 65535));

            PeerConnection = pc; // assign to field early

            // Add this enhanced logging to your VoiceSession.cs CreatePeerConnection method:

            pc.OnRtpPacketReceived += (IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket) =>
            {
                // Logger.Log($"OnRtpPacketReceived fired for mediaType: {mediaType}", Helpers.LogLevel.Info, Client);
                // Logger.Log($"RTP: {rtpPacket.Payload.Length} bytes, PT={rtpPacket.Header.PayloadType}, Seq={rtpPacket.Header.SequenceNumber}, TS={rtpPacket.Header.Timestamp}", Helpers.LogLevel.Debug, Client);
                if (mediaType == SDPMediaTypesEnum.audio)
                {
                    //  Logger.Log($"RTP Audio: {rtpPacket.Payload.Length} bytes, " +
                    //      $"PT={rtpPacket.Header.PayloadType}, " +
                    // $"Seq={rtpPacket.Header.SequenceNumber}, " +
                    //          $"TS={rtpPacket.Header.Timestamp}",
                    //    Helpers.LogLevel.Debug, Client);

                    if (AudioDevice?.EndPoint != null)
                    {
                        try
                        {
                            uint ssrcVal = 0;
                            try
                            {
                                var hdr = rtpPacket.Header;
                                var hdrType = hdr.GetType();
                                // try many common names to be robust across versions
                                var candidateNames = new[] { "SynchronizationSourceIdentifier", "synchronizationSourceIdentifier", "SSRC", "Ssrc", "ssrc", "SynchronizationSourceId", "SourceIdentifier", "SynchronizationSource" };
                                foreach (var name in candidateNames)
                                {
                                    try
                                    {
                                        var pi = hdrType.GetProperty(name);
                                        if (pi != null)
                                        {
                                            var v = pi.GetValue(hdr);
                                            if (v != null) { ssrcVal = Convert.ToUInt32(v); break; }
                                        }
                                    }
                                    catch { }
                                    try
                                    {
                                        var fi = hdrType.GetField(name);
                                        if (fi != null)
                                        {
                                            var v = fi.GetValue(hdr);
                                            if (v != null) { ssrcVal = Convert.ToUInt32(v); break; }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            AudioDevice.PlayRtpPacket(ssrcVal, rtpPacket.Payload);
                        }
                        catch (Exception ex)
                        {
                            _log.Warn($"Failed to play RTP packet: {ex.Message}", Client);
                        }
                    }
                    else
                    {
                        _log.Warn("Cannot play audio: EndPoint is null (SDL3 not initialized)", Client);
                    }
                }
            };

            pc.oniceconnectionstatechange += (state) =>
            {
                _log.Debug($"ICE connection state: {state}", Client);

                // If ICE fails, the connection will fail
                if (state == RTCIceConnectionState.failed)
                {
                    _log.Warn("ICE connection failed - possible NAT/firewall issue", Client);

                    // Schedule automatic reprovision with exponential backoff to recover
                    try
                    {
                        ScheduleReprovisionWithBackoff();
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Failed to schedule automatic reprovision: {ex.Message}", Client);
                    }
                }
            };
            // Create data channel BEFORE negotiation
            var dc = await pc.createDataChannel("SLData", new RTCDataChannelInit { ordered = true }).ConfigureAwait(false);

            dc.onopen += () =>
            {
                _log.Debug("Data channel opened", Client);

                // Only send join message per spec
                TrySendDataChannelString("{\"j\":{\"p\":true}}");

                // Start position updates AFTER a small delay to ensure join is processed
                Task.Delay(100, ct).ContinueWith(_ => StartPositionLoop(), TaskScheduler.Default);

                // Start data channel keepalive
                StartKeepAliveLoop();

                OnDataChannelReady?.Invoke();
            };

            dc.onclose += () =>
            {
                StopPositionLoop();
                StopKeepAliveLoop();
                OnDataChannelReady?.Invoke();
            };
            dc.onmessage += (channel, type, data) =>
            {
                var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                _log.Debug($"Data channel message received: {msg}", Client);
                Task.Run(() => HandleDataChannelMessage(msg), ct);
            };

            // SDP negotiation
            // Ensure we have a recording source; attempt to create a default one if missing so GetAudioSourceFormats() won't NRE.
            if (AudioDevice == null)
            {
                _log.Error("AudioDevice is null in VoiceSession.CreatePeerConnection", Client);
                throw new VoiceException("Internal error: AudioDevice is null.");
            }
            if (AudioDevice.Source == null)
            {
                _log.Warn("Recording is null. Attempting to create default recording source.", Client);
                try
                {
                    AudioDevice.SetRecordingDevice(null);
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create default recording source: {ex.Message}", Client);
                }

                // If we still don't have a source, fail early with a clear error instead of passing null to MediaStreamTrack
                if (AudioDevice.Source == null)
                {
                    _log.Error("No audio recording source available after attempts to create one.", Client);
                    throw new VoiceException("No audio recording source available.");
                }
            }

            var audioTrack = new MediaStreamTrack(AudioDevice?.Source?.GetAudioSourceFormats());

            pc.addTrack(audioTrack);
            var offer = pc.createOffer();
            var rawSdp = offer.sdp.ToString();
            var processedSdp = ProcessLocalSdp(rawSdp);

            // Assign the mangled SDP string directly
            offer.sdp = processedSdp;

            await pc.setLocalDescription(offer).ConfigureAwait(false);


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



            //  Logger.Log($"Final local SDP offer:\n{pc.localDescription.sdp}", Helpers.LogLevel.Debug, Client);
            pc.localDescription.sdp.SessionName = "LibreMetaVoice";

            // ICE and connection state handlers
            pc.onicecandidate += (candidate) =>
            {
                if (candidate == null) return;
                EnqueueCandidate(candidate);
            };

            // ICE gathering state handler - ensure we send the ICE "completed" message when gathering finishes
            pc.onicegatheringstatechange += async (state) =>
            {
                try
                {
                    if (state == RTCIceGatheringState.complete)
                    {
                        _log.Debug("ICE gathering state completed", Client);
                        await IceTrickleStop().ConfigureAwait(false);
                    }
                    else if (state == RTCIceGatheringState.gathering)
                    {
                        _log.Debug("ICE gathering state has commenced.", Client);
                        // Ensure trickle loop is running
                        _ = IceTrickleStart(ct);
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"Exception in onicegatheringstatechange handler: {ex.Message}", Client);
                }
            };
            pc.ondatachannel += (channel) =>
            {
                _log.Debug($"Server created data channel: {channel.label} (id: {channel.id})", Client);

                channel.onopen += () =>
                {
                    _log.Debug($"Inbound channel '{channel.label}' opened", Client);
                };

                channel.onmessage += (ch, type, data) =>
                {
                    var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                    // Logger.Log($"📨 Received on '{channel.label}': {msg}", Helpers.LogLevel.Info, Client);
                    Task.Run(() => HandleDataChannelMessage(msg), ct);
                };

                channel.onclose += () =>
                {
                    _log.Debug($"Inbound channel '{channel.label}' closed", Client);
                };

                channel.onerror += (error) =>
                {
                    _log.Error($"Inbound channel '{channel.label}' error: {error}", Client);
                };
            };
            pc.onconnectionstatechange += async (state) =>
            {
                _log.Debug($"Peer connection state changed to {state}.", Client);
                if (state == RTCPeerConnectionState.connected)
                {
                    if (AudioDevice?.EndPoint != null)
                    {
                        try
                        {
                            await AudioDevice.StartPlaybackAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Debug($"Failed to start playback via AudioDevice: {ex.Message}", Client);
                            // Fallback: attempt to start endpoint directly
                            try { await AudioDevice.EndPoint.StartAudioSink().ConfigureAwait(false); } catch { }
                        }
                        _log.Debug("Playback started", Client);
                    }
                    else
                    {
                        // Attempt to recreate endpoint if possible
                        var got = AudioDevice?.EnsureEndpoint() ?? false;
                        if (got)
                        {
                            try
                            {
                                await AudioDevice.StartPlaybackAsync().ConfigureAwait(false);
                                _log.Debug("Playback started after EnsureEndpoint", Client);
                            }
                            catch (Exception ex)
                            {
                                _log.Debug($"Failed to start playback after EnsureEndpoint: {ex.Message}", Client);
                            }
                        }
                        else
                        {
                            _log.Debug("Playback not started: SDL3 EndPoint is null (SDL3 not initialized or no devices found)", Client);
                        }
                    }
                    // Start recording only when the connection is fully established
                    try
                    {
                        if (AudioDevice?.Source != null)
                        {
                            AudioDevice.StartRecording();
                            _log.Debug("Recording started on connection", Client);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Failed to start recording on connection: {ex.Message}", Client);
                    }
                    OnPeerConnectionReady?.Invoke();
                }
                else if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.closed)
                {
                    // Stop playback and recording on disconnect/failure
                    if (AudioDevice != null)
                    {
                        try { await AudioDevice.StopPlaybackAsync().ConfigureAwait(false); } catch { }
                        try { AudioDevice.StopRecording(); } catch { }
                    }

                    // For failed/disconnected states, log but don't immediately trigger OnPeerConnectionClosed
                    // to allow the reprovision logic to handle recovery
                    if (state == RTCPeerConnectionState.closed)
                    {
                        OnPeerConnectionClosed?.Invoke();
                    }
                }
            };

            // Detach any existing handler to prevent duplicates on reprovision
            if (AudioDevice?.Source != null)
            {
                try { AudioDevice.Source.OnAudioSourceEncodedSample -= pc.SendAudio; } catch { }
            }

            AudioDevice.Source.OnAudioSourceEncodedSample += (duration, sample) =>
            {
                pc.SendAudio(duration, sample);
            };

            // Also wire the AudioDevice level event for file playback and other non-source audio
            if (AudioDevice != null)
            {
                try { AudioDevice.OnAudioSourceEncodedSample -= pc.SendAudio; } catch { }
                AudioDevice.OnAudioSourceEncodedSample += (duration, sample) =>
                {
                    pc.SendAudio(duration, sample);
                };
            }

            return pc;
        }

        private void StartPositionLoop()
        {
            if (positionLoopTask != null && !positionLoopTask.IsCompleted) return;
            positionLoopCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token);
            var token = positionLoopCts.Token;
            positionLoopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var dc = DataChannel;
                        if (dc == null || dc.readyState != RTCDataChannelState.open) { await Task.Delay(100, token).ConfigureAwait(false); continue; }

                        var globalPos = Client?.Self?.GlobalPosition ?? Vector3.Zero;
                        var heading = Client?.Self?.RelativeRotation ?? Quaternion.Identity;

                        if (globalPos == Vector3.Zero) { await Task.Delay(100, token).ConfigureAwait(false); continue; }

                        bool posChanged = (Math.Abs(globalPos.X - lastObservedGlobalPos.X) > POSITION_CHANGE_THRESHOLD_METERS)
                                          || (Math.Abs(globalPos.Y - lastObservedGlobalPos.Y) > POSITION_CHANGE_THRESHOLD_METERS)
                                          || (Math.Abs(globalPos.Z - lastObservedGlobalPos.Z) > POSITION_CHANGE_THRESHOLD_METERS);

                        bool headingChanged = (Math.Abs(heading.X - lastObservedHeading.X) > HEADING_CHANGE_THRESHOLD)
                                              || (Math.Abs(heading.Y - lastObservedHeading.Y) > HEADING_CHANGE_THRESHOLD)
                                              || (Math.Abs(heading.Z - lastObservedHeading.Z) > HEADING_CHANGE_THRESHOLD)
                                              || (Math.Abs(heading.W - lastObservedHeading.W) > HEADING_CHANGE_THRESHOLD);

                        if (posChanged || headingChanged)
                        {
                            mSpatialCoordsDirty = true;
                            lastObservedGlobalPos = globalPos;
                            lastObservedHeading = heading;
                        }

                        // Attempt to send update; SendPositionUpdate will short-circuit if not dirty
                        SendPositionUpdate(dc, globalPos, heading);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _log.Error($"Position loop error: {ex.Message}", Client);
                    }
                    try { await Task.Delay(100, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }, token);
        }

        private void StopPositionLoop()
        {
            try
            {
                positionLoopCts?.Cancel();
                positionLoopTask?.Wait(500);
            }
            catch { }
            finally
            {
                positionLoopCts?.Dispose();
                positionLoopCts = null;
                positionLoopTask = null;
            }
        }

        private void StartKeepAliveLoop()
        {
            if (keepAliveLoopTask != null && !keepAliveLoopTask.IsCompleted) return;
            keepAliveLoopCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token);
            var token = keepAliveLoopCts.Token;
            keepAliveLoopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var dc = DataChannel;
                        if (dc != null && dc.readyState == RTCDataChannelState.open)
                        {
                            TrySendDataChannelString("{\"ping\":true}");
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _log.Error($"Keepalive loop error: {ex.Message}", Client);
                    }
                    try { await Task.Delay(5000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }, token);
        }

        private void StopKeepAliveLoop()
        {
            try
            {
                keepAliveLoopCts?.Cancel();
                keepAliveLoopTask?.Wait(500);
            }
            catch { }
            finally
            {
                keepAliveLoopCts?.Dispose();
                keepAliveLoopCts = null;
                keepAliveLoopTask = null;
            }
        }

        // Helper methods to keep PendingCandidates and pendingCandidateCount consistent
        private void EnqueueCandidate(RTCIceCandidate candidate)
        {
            if (candidate == null) return;
            lock (_candidateLock)
            {
                PendingCandidates.Enqueue(candidate);
                Interlocked.Increment(ref pendingCandidateCount);
            }
        }

        private List<RTCIceCandidate> DequeueAllCandidates()
        {
            var list = new List<RTCIceCandidate>();
            lock (_candidateLock)
            {
                while (PendingCandidates.TryDequeue(out var c))
                {
                    list.Add(c);
                    Interlocked.Decrement(ref pendingCandidateCount);
                }
            }
            return list;
        }

        private int GetPendingCandidateCount()
        {
            return Interlocked.CompareExchange(ref pendingCandidateCount, 0, 0);
        }

        private async Task FlushPendingIceCandidates()
        {
            if (!answerReceived || GetPendingCandidateCount() == 0) { return; }

            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
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
                await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
                _log.Debug($"Sent {candidatesArray.Count} ICE candidates", Client);
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send ICE candidates: {ex.Message}", Client);
            }
        }

        // Send any pending ICE candidates to the voice signaling capability
        private async Task SendVoiceSignalingRequest()
        {
            if (!answerReceived)
            {
                _log.Debug("Skipping ICE send - no answer received yet", Client);
                return;
            }

            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
            if (cap == null)
            {
                _log.Debug("Voice signaling cap not available", Client);
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

            _log.Debug($"Sending {dequeued.Count} ICE candidates", Client);

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
                _log.Debug($"Sent {canArray.Count} ICE candidates successfully", Client);
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send ICE candidates: {ex.Message}", Client);
            }
        }

        private async Task SendVoiceSignalingCompleteRequest()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);

            var payload = new OSDMap
             {

                 { "voice_server_type", "webrtc" },
                 { "viewer_session", SessionId }
             };

            // Use the same 'candidates' array shape as SendVoiceSignalingRequest; include completed marker
            var canArray = new OSDArray();
            var completedMap = new OSDMap { { "completed", true } };
            canArray.Add(completedMap);
            payload["candidates"] = canArray; // Use "candidates" key
            _log.Debug($"Sending ICE Signaling Complete for {SessionId}", Client);
            if (cap == null)
            {
                _log.Debug("Voice signaling capability not available for complete request.", Client);
                return;
            }
            try
            {
                var resp = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
                try
                {
                    if (resp is OSDMap respMap)
                    {
                        _log.Debug($"Voice signaling complete response: {OSDParser.SerializeJsonString(respMap, true)}", Client);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                _log.Warn($"Sending ICE Signaling Complete failed for {SessionId}: {ex.Message}", Client);
            }
        }

        // Simple handler implementing viewer<->datachannel JSON protocol
        private void HandleDataChannelMessage(string msg)
        {
            dataChannelProcessor?.ProcessMessage(msg, SessionId);
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
                _log.Debug($"Failed to send data channel message: {ex.Message}", Client);
                return false;
            }
        }

        // Public facade methods that forward to the DataChannelProcessor
        public bool SetPeerMute(UUID peerId, bool mute) => dataChannelProcessor.SetPeerMute(peerId, mute);
        public bool SetPeerGain(UUID peerId, int gain) => dataChannelProcessor.SetPeerGain(peerId, gain);

        public bool SendJoin(bool primary = true) => dataChannelProcessor.SendJoin(primary);
        public bool SendLeave() => dataChannelProcessor.SendLeave();

        public bool SendPosition(Vector3d globalPos, Quaternion heading)
        {
            var ok = dataChannelProcessor.SendPosition(globalPos, heading);
            if (ok)
            {
                lastSentGlobalPos = globalPos;
                lastSentHeading = heading;
                mSpatialCoordsDirty = false;
            }
            return ok;
        }

        public bool SendAvatarArray(List<UUID> avatars) => dataChannelProcessor.SendAvatarArray(avatars);
        public bool SendAvatarMap(IEnumerable<UUID> avatars) => dataChannelProcessor.SendAvatarMap(avatars);
        public bool SendMuteMap(Dictionary<UUID, bool> muteMap) => dataChannelProcessor.SendMuteMap(muteMap);
        public bool SendGainMap(Dictionary<UUID, int> gainMap) => dataChannelProcessor.SendGainMap(gainMap);
        public bool SendPing() => dataChannelProcessor.SendPing();
        public bool SendPong() => dataChannelProcessor.SendPong();

        // --- end data-channel message helpers ---

        private Task IceTrickleStart(CancellationToken external = default)
        {
            // If a trickle task is already running, just return it
            if (iceTrickleTask != null
                && (iceTrickleTask.Status == TaskStatus.Running
                || iceTrickleTask.Status == TaskStatus.WaitingToRun
                || iceTrickleTask.Status == TaskStatus.WaitingForActivation))
            {
                return iceTrickleTask;

            }

            iceTrickleCts = external == CancellationToken.None ? CancellationTokenSource.CreateLinkedTokenSource(Cts.Token) : CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, external);

            // Create a repeating task locally instead of calling Repeat.Interval to avoid
            // cross-assembly MethodAccessException. This loop polls every 25ms and invokes
            // the poll action when appropriate. The created task is returned so callers
            // can await or inspect its status if needed.
            var token = iceTrickleCts.Token;
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
                            _log.Warn($"IceTrickle loop poll exception: {ex.Message}", Client);
                        }

                        try { await Task.Delay(TimeSpan.FromMilliseconds(25), token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"IceTrickle loop terminated with exception: {ex.Message}", Client);
                }
            }

            iceTrickleTask = Task.Run(Loop, token);

            return iceTrickleTask;

            async Task poll()
            {
                if (Client.Network.Connected && !SessionId.Equals(UUID.Zero) && Interlocked.CompareExchange(ref pendingCandidateCount, 0, 0) > 0)
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
                        _log.Warn($"SendVoiceSignalingRequest failed in poll: {ex.Message}", Client);
                    }
                }
            }
        }

        private async Task IceTrickleStop()
        {
            while (true)
            {
                if (GetPendingCandidateCount() > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                else
                {
                    break;
                }
            }

            if (PeerConnection != null && PeerConnection.iceGatheringState == RTCIceGatheringState.complete)
            {
                iceTrickleCts?.Cancel();
                await SendVoiceSignalingCompleteRequest().ConfigureAwait(false);
            }
        }

        // Schedule a reprovision attempt using exponential backoff. Prevents immediate retry storms
        // and adds diagnostic logging for each attempt.
        private void ScheduleReprovisionWithBackoff()
        {
            if (reprovisionScheduled) return;
            reprovisionScheduled = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    int attempt = 0;
                    const int maxAttempts = 6; // up to ~1m total backoff
                    int delayMs = 1000;

                    while (!Cts.Token.IsCancellationRequested && attempt < maxAttempts)
                    {
                        attempt++;
                        _log.Debug($"Reprovision scheduled attempt {attempt}/{maxAttempts} in {delayMs}ms", Client);
                        try { await Task.Delay(delayMs, Cts.Token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }

                        try
                        {
                            _log.Debug($"Starting scheduled reprovision attempt {attempt}", Client);
                            await AttemptReprovisionAsync().ConfigureAwait(false);
                            _log.Debug($"Scheduled reprovision attempt {attempt} succeeded", Client);
                            reprovisionScheduled = false;
                            return;
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _log.Warn($"Scheduled reprovision attempt {attempt} failed: {ex.Message}", Client);
                        }

                        // Exponential backoff for next attempt
                        delayMs = Math.Min(delayMs * 2, 60000);
                    }

                    _log.Warn($"Scheduled reprovision exhausted after {attempt} attempts", Client);
                }
                finally
                {
                    reprovisionScheduled = false;
                }
            }, Cts.Token);
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
                        _log.Debug($"Dropping remote ICE candidate with port 0: {line}", Client);
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
            string opusPayload = null;
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

        private async Task AttemptReprovisionAsync()
        {
            // Ensure only one reprovision runs at a time
            if (!await reprovisionLock.WaitAsync(0))
            {
                _log.Debug("Reprovision already in progress, skipping.", Client);
                return;
            }

            try
            {
                _log.Debug($"Starting reprovision for session {SessionId}", Client);

                // Store recording state before closing
                bool wasRecording = AudioDevice?.Source != null && AudioDevice.RecordingActive;
                bool wasPlaybackActive = AudioDevice?.EndPoint != null && AudioDevice.PlaybackActive;

                try
                {
                    // Stop audio before closing peer connection
                    if (AudioDevice != null)
                    {
                        try { AudioDevice.StopRecording(); } catch { }
                        try { AudioDevice.StopPlaybackAsync().Wait(250); } catch { }
                    }

                    // Close existing peer connection if present
                    if (PeerConnection != null)
                    {
                        // Detach audio source handler before closing
                        if (AudioDevice?.Source != null)
                        {
                            try { AudioDevice.Source.OnAudioSourceEncodedSample -= PeerConnection.SendAudio; } catch { }
                        }
                        // Detach AudioDevice level handler
                        if (AudioDevice != null)
                        {
                            try { AudioDevice.OnAudioSourceEncodedSample -= PeerConnection.SendAudio; } catch { }
                        }
                        try { PeerConnection.Close("Reprovision"); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    _log.Warn($"Error closing peer connection during reprovision: {ex.Message}", Client);
                }

                // Cancel any existing trickle loop
                try { iceTrickleCts?.Cancel(); } catch { }

                // Reset answer flag to ensure ICE candidates wait for new answer
                answerReceived = false;

                // Clear all known peers and audio state before reprovisioning
                try { peerManager.ClearAllPeers(); } catch { }

                // Create a new peer connection
                RTCPeerConnection newPc = null;
                try
                {
                    newPc = await CreatePeerConnection();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Failed to create new RTCPeerConnection during reprovision: {ex.Message}", Client);
                }

                if (newPc != null)
                {
                    PeerConnection = newPc;

                    // Start trickle loop
                    _ = IceTrickleStart();

                    // Re-request provisioning from the simulator
                    try
                    {
                        var ok = await RequestProvision();
                        if (ok)
                        {
                            _log.Debug($"Reprovision completed, new session {SessionId}", Client);

                            // Restore audio state after successful reprovision
                            if (AudioDevice != null)
                            {
                                // Wait a moment for peer connection to stabilize
                                await Task.Delay(500).ConfigureAwait(false);


                                if (wasRecording)
                                {
                                    try { AudioDevice.StartRecording(); } catch (Exception ex) { _log.Warn($"Failed to restart recording after reprovision: {ex.Message}", Client); }
                                }
                                if (wasPlaybackActive)
                                {
                                    try { await AudioDevice.StartPlaybackAsync().ConfigureAwait(false); } catch (Exception ex) { _log.Warn($"Failed to restart playback after reprovision: {ex.Message}", Client); }
                                }
                            }

                            try { OnReprovisionSucceeded?.Invoke(); } catch { }
                        }
                        else
                        {
                            _log.Warn("Reprovision failed: client not connected to network.", Client);
                            try { OnReprovisionFailed?.Invoke(new Exception("Client not connected to network")); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"Reprovision RequestProvision failed: {ex.Message}", Client);
                        try { OnReprovisionFailed?.Invoke(ex); } catch { }
                    }
                }
                else
                {
                    var ex = new Exception("Failed to create new RTCPeerConnection during reprovision");
                    try { OnReprovisionFailed?.Invoke(ex); } catch { }
                }
            }
            finally
            {
                reprovisionLock.Release();
            }
        }

        private OSDMap GetWorldPosition()
        {
            var pos = Client?.Self?.GlobalPosition ?? Vector3.Zero;
            var heading = Client?.Self?.RelativeRotation * 100;

            if (pos == Vector3.Zero)
            {
                _log.Warn("Global position unavailable — returning zeroed map", Client);
                var zero = new OSDMap
                {
                    ["x"] = 0,
                    ["y"] = 0,
                    ["z"] = 0
                };

                return new OSDMap
                {
                    ["sp"] = zero,
                    ["lp"] = zero,
                    ["sh"] = zero,
                    ["lh"] = zero
                };
            }

            var sp = new OSDMap
            {
                ["x"] = pos.X,
                ["y"] = pos.Y,
                ["z"] = pos.Z
            };

            var sh = new OSDMap
            {
                ["x"] = heading.Value.X,
                ["y"] = heading.Value.Y,
                ["z"] = heading.Value.Z
            };

            return new OSDMap
            {
                ["sp"] = sp,
                ["lp"] = sp,
                ["sh"] = sh,
                ["lh"] = sh
            };
        }
        // Alternative using the existing SendGlobalPosition method - FIXED VERSION
        public string SendGlobalPosition()
        {
            var pos = Client.Self.GlobalPosition;
            var h = Client.Self.RelativeRotation;

            // Convert to centimeters and integers
            int posX = (int)Math.Round(pos.X * 100);
            int posY = (int)Math.Round(pos.Y * 100);
            int posZ = (int)Math.Round(pos.Z * 100);

            // Multiply quaternion by 100 and convert to int
            int headX = (int)Math.Round(h.X * 100);
            int headY = (int)Math.Round(h.Y * 100);
            int headZ = (int)Math.Round(h.Z * 100);
            int headW = (int)Math.Round(h.W * 100);

            JsonWriter jw = new JsonWriter();
            jw.WriteObjectStart();

            jw.WritePropertyName("sp");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(posX);  // INTEGER, not float
            jw.WritePropertyName("y");
            jw.Write(posY);
            jw.WritePropertyName("z");
            jw.Write(posZ);
            jw.WriteObjectEnd();

            jw.WritePropertyName("lp");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(posX);
            jw.WritePropertyName("y");
            jw.Write(posY);
            jw.WritePropertyName("z");
            jw.Write(posZ);
            jw.WriteObjectEnd();

            jw.WriteObjectEnd();

            return jw.ToString();
        }



        // Sends a position update over the data channel (keeps last-sent tracking)
        private void SendPositionUpdate(RTCDataChannel dc, Vector3d globalPos, Quaternion heading)
        {
            // Only send when flagged dirty
            if (!mSpatialCoordsDirty) { return; }

            // Avoid resending identical payloads
            if (globalPos == lastSentGlobalPos && heading == lastSentHeading)
            {
                mSpatialCoordsDirty = false; // nothing new
                return;
            }

            try
            {
                // Convert to integers per spec
                int posX = (int)Math.Round(globalPos.X * 100);
                int posY = (int)Math.Round(globalPos.Y * 100);
                int posZ = (int)Math.Round(globalPos.Z * 100);

                int headX = (int)Math.Round(heading.X * 100);
                int headY = (int)Math.Round(heading.Y * 100);
                int headZ = (int)Math.Round(heading.Z * 100);
                int headW = (int)Math.Round(heading.W * 100);

                // Build JSON using JsonWriter to avoid manual concatenation issues
                var jw = new JsonWriter();
                jw.WriteObjectStart();

                jw.WritePropertyName("sp");
                jw.WriteObjectStart();
                jw.WritePropertyName("x"); jw.Write(posX);
                jw.WritePropertyName("y"); jw.Write(posY);
                jw.WritePropertyName("z"); jw.Write(posZ);
                jw.WriteObjectEnd();

                jw.WritePropertyName("sh");
                jw.WriteObjectStart();
                jw.WritePropertyName("x"); jw.Write(headX);
                jw.WritePropertyName("y"); jw.Write(headY);
                jw.WritePropertyName("z"); jw.Write(headZ);
                jw.WritePropertyName("w"); jw.Write(headW);
                jw.WriteObjectEnd();

                jw.WritePropertyName("lp");
                jw.WriteObjectStart();
                jw.WritePropertyName("x"); jw.Write(posX);
                jw.WritePropertyName("y"); jw.Write(posY);
                jw.WritePropertyName("z"); jw.Write(posZ);
                jw.WriteObjectEnd();

                jw.WritePropertyName("lh");
                jw.WriteObjectStart();
                jw.WritePropertyName("x"); jw.Write(headX);
                jw.WritePropertyName("y"); jw.Write(headY);
                jw.WritePropertyName("z"); jw.Write(headZ);
                jw.WritePropertyName("w"); jw.Write(headW);
                jw.WriteObjectEnd();

                jw.WriteObjectEnd();

                var json = jw.ToString();

                _log.Debug($"Sending Position: {json}", Client);
                TrySendDataChannelString(json);

                // Update last sent and clear dirty flag
                lastSentGlobalPos = globalPos;
                lastSentHeading = heading;
                mSpatialCoordsDirty = false;
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to send position on data channel: {ex.Message}", Client);
            }
        }

        // Minimal implementation of RequestProvision to satisfy callers.
        // The real implementation is more involved; this stub returns false.
        public async Task<bool> RequestProvision()
        {
            if (Client?.Network == null || !Client.Network.Connected) { return false; }
            _log.Debug("Requesting voice capability...", Client);

            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(PROVISION_VOICE_ACCOUNT_CAP);
            if (cap == null)
            {
                throw new VoiceException($"No {PROVISION_VOICE_ACCOUNT_CAP} capability available.");
            }

            switch (SessionType)
            {
                case ESessionType.LOCAL:
                    await RequestLocalVoiceProvision(cap);
                    break;
                case ESessionType.MUTLIAGENT:
                    await RequestMultiAgentVoiceProvision(cap);
                    break;
            }

            return true;
        }

        private async Task RequestLocalVoiceProvision(Uri cap)
        {
            var parcelId = Client.Parcels.CurrentParcel?.LocalID ?? -1;
            var payload = new LocalVoiceProvisionRequest(SdpLocal, parcelId).Serialize();
            _log.Debug("==> Attempting to POST for voice provision...", Client);
            if (Client?.HttpCapsClient == null)
            {
                _log.Error("HttpCapsClient is null; cannot post provisioning request", Client);
                throw new VoiceException("Internal error: HttpCapsClient is null.");
            }
            var osd = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
            _log.Debug("Received provisioning response", Client);
            if (osd is OSDMap osdMap)
            {
                if (!osdMap.TryGetValue("jsep", out var j))
                {
                    throw new VoiceException($"Region '{Client.Network.CurrentSim.Name}' does not support WebRtc.");
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
                            var lower = emsg.ToLowerInvariant();
                            if (lower.Contains("credential") || lower.Contains("credentials") || lower.Contains("expired") || lower.Contains("invalid") || lower.Contains("channel") || lower.Contains("denied"))
                            {
                                _log.Warn($"Provisioning response indicated credential/channel problem: {emsg}", Client);
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
                if (PeerConnection == null)
                {
                    _log.Error("PeerConnection unexpectedly null during RequestLocalVoiceProvision", Client);
                    throw new VoiceException("Internal error: PeerConnection was null.");
                }
                var set = PeerConnection.SetRemoteDescription(
                    SdpType.answer,
                    SDP.ParseSDPDescription(sdpString)
                );

                if (set != SetDescriptionResultEnum.OK)
                {
                    PeerConnection.Close("Failed to set remote description.");
                    throw new VoiceException("Failed to set remote description.");
                }

                // CRITICAL: Set flag BEFORE flushing candidates
                answerReceived = true;

                // Now flush any pending candidates
                await SendVoiceSignalingRequest().ConfigureAwait(false);

                // Start a short watchdog: if the peer connection does not become connected
                // within a short timeout, schedule a reprovision attempt. This handles
                // cases where ICE/connectivity stalls after provisioning.
                _ =Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                        if (PeerConnection == null || PeerConnection.connectionState != RTCPeerConnectionState.connected)
                        {
                            _log.Warn("PeerConnection did not become connected after provisioning; scheduling reprovision.", Client);
                            try { ScheduleReprovisionWithBackoff(); } catch { }
                        }
                    }
                    catch { }
                });

                // Store channel info if present
                if (osdMap.ContainsKey("channel")) ChannelId = osdMap["channel"].AsString();
                if (osdMap.ContainsKey("credentials")) ChannelCredentials = osdMap["credentials"].AsString();

                _log.Debug($"Local voice provisioned: session={sessionId}", Client);
            }
        }

        private async Task RequestMultiAgentVoiceProvision(Uri cap)
        {
            var req = new MultiAgentVoiceProvisionRequest(SdpLocal);
            // include channel/credentials if present on this session
            if (!string.IsNullOrEmpty(ChannelId)) req.ChannelId = ChannelId;
            if (!string.IsNullOrEmpty(ChannelCredentials)) req.ChannelCredentials = ChannelCredentials;

            var payload = req.Serialize();
            var osd = await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
            if (osd is OSDMap osdMap)
            {
                if (!osdMap.ContainsKey("jsep"))
                {
                    throw new VoiceException($"Region '{Client.Network.CurrentSim.Name}' does not support WebRtc.");
                }
                var jsep = (OSDMap)osdMap["jsep"];
                if (jsep.ContainsKey("type") && jsep["type"].AsString() != "answer")
                {
                    throw new VoiceException($"jsep returned from '{Client.Network.CurrentSim.Name}' is not an answer.");
                }
                var sdpString = jsep["sdp"].AsString();
                sdpString = SanitizeRemoteSdp(sdpString);
                var sessionId = osdMap.ContainsKey("viewer_session") ? osdMap["viewer_session"].AsUUID() : UUID.Zero;

                var desc = SDP.ParseSDPDescription(sdpString);
                var set = PeerConnection.SetRemoteDescription(SdpType.answer, desc);
                if (set != SetDescriptionResultEnum.OK)
                {
                    PeerConnection.Close("Failed to set remote description (multiagent).");
                    throw new VoiceException("Failed to set remote description (multiagent).");
                }

                // Ensure the session id is stored before we start flushing/trickling ICE candidates
                SessionId = sessionId;

                // Mark that we've received the answer and flush any pending ICE candidates
                answerReceived = true;
                _ = FlushPendingIceCandidates();

                // Start a short watchdog for multi-agent provisioning similar to the local path.
                // If the peer connection doesn't reach connected within the timeout, schedule reprovision.
                _ =Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(false);
                        if (PeerConnection == null || PeerConnection.connectionState != RTCPeerConnectionState.connected)
                        {
                            _log.Warn("Multi-agent PeerConnection did not become connected after provisioning; scheduling reprovision.", Client);
                            try { ScheduleReprovisionWithBackoff(); } catch { }
                        }
                    }
                    catch { }
                });

                if (osdMap.ContainsKey("channel")) ChannelId = osdMap["channel"].AsString();
                if (osdMap.ContainsKey("credentials")) ChannelCredentials = osdMap["credentials"].AsString();

                _log.Debug($"Multi-agent voice provisioned: session={sessionId}", Client);
            }
        }

        private async Task SendCloseSessionRequest()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(PROVISION_VOICE_ACCOUNT_CAP);

            _log.Debug($"Closing voice session {SessionId}", Client);
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
                    payload["channel"] = ChannelId;
                    if (!string.IsNullOrEmpty(ChannelCredentials)) payload["credentials"] = ChannelCredentials;
                }

                await PostCapsWithRetries(cap, payload).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error($"Close session request failed: {ex.Message}", Client);
            }

            // Clear local channel state after attempting to close server-side
            ChannelId = null;
            ChannelCredentials = null;
        }

        // Close and cleanup session resources
        public async Task CloseSession()
        {
            // Stop loops
            StopPositionLoop();
            StopKeepAliveLoop();

            // Clear peer and SSRC state
            try { peerManager.ClearAllPeers(); } catch { }

            PeerConnection?.Close("ClientClose");

            await SendCloseSessionRequest().ConfigureAwait(false);
            await SendVoiceSignalingCompleteRequest().ConfigureAwait(false);

            // Cancel internal token source to stop any background work
            try { Cts.Cancel(); } catch { }
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
                    iceTrickleCts?.Cancel();
                    try { iceTrickleTask?.Wait(250); } catch { }
                    iceTrickleCts?.Dispose();
                    iceTrickleCts = null;
                    iceTrickleTask = null;
                }
                catch { }

                // Detach audio handlers
                try
                {
                    if (AudioDevice?.Source != null)
                    {
                        try { AudioDevice.Source.OnAudioSourceEncodedSample -= PeerConnection.SendAudio; } catch { }
                    }
                    if (AudioDevice != null)
                    {
                        try { AudioDevice.OnAudioSourceEncodedSample -= PeerConnection.SendAudio; } catch { }
                    }
                }
                catch { }

                // Stop audio
                try
                {
                    if (AudioDevice != null)
                    {
                        try { AudioDevice.StopRecording(); } catch { }
                        try { AudioDevice.StopPlaybackAsync().Wait(250); } catch { }
                    }
                }
                catch { }

                // Clear peers and SSRC state
                try { peerManager.ClearAllPeers(); } catch { }

                // Close peer connection
                try
                {
                    if (PeerConnection != null)
                    {
                        try { PeerConnection.Close("Dispose"); } catch { }
                        // If RTCPeerConnection exposes Dispose, call it (best-effort)
                        try { (PeerConnection as IDisposable)?.Dispose(); } catch { }
                        PeerConnection = null;
                    }
                }
                catch { }

                // Cancel main CTS
                try { Cts.Cancel(); } catch { }
                try { Cts.Dispose(); } catch { }

                // Dispose other token sources
                try { positionLoopCts?.Dispose(); } catch { }
                positionLoopCts = null;
                try { keepAliveLoopCts?.Dispose(); } catch { }
                keepAliveLoopCts = null;

                // Release semaphore
                try { reprovisionLock.Dispose(); } catch { }

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
