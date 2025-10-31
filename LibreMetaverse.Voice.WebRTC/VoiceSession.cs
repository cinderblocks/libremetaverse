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
using Logger = OpenMetaverse.Logger;

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceSession
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
        private RTCPeerConnection PeerConnection;

        public event Action OnPeerConnectionClosed;
        public event Action OnPeerConnectionReady;
        public event Action OnDataChannelReady;

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
        private readonly Queue<RTCIceCandidate> PendingCandidates = new Queue<RTCIceCandidate>();
        private readonly CancellationTokenSource Cts = new CancellationTokenSource();

        private CancellationTokenSource iceTrickleCts;
        private Task iceTrickleTask;
        // Prevent concurrent reprovision attempts
        private readonly SemaphoreSlim reprovisionLock = new SemaphoreSlim(1, 1);
        private volatile bool reprovisionScheduled = false;

        // Multi-agent channel fields
        public string ChannelId { get; set; }
        public string ChannelCredentials { get; set; }

        // Track peers and their last known positions
        private readonly ConcurrentDictionary<UUID, OSDMap> Peers = new ConcurrentDictionary<UUID, OSDMap>();

        private System.Timers.Timer positionTimer;

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
        private long lastRtpReceivedTicks = 0;
        private long lastRtcpReceivedTicks = 0;
        private long lastRtcpSentTicks = 0;
        private MediaStreamTrack _remoteAudioTrack;
        internal VoiceSession(Sdl3Audio audioDevice, ESessionType type, GridClient client)
        {
            Client = client;
            AudioDevice = audioDevice;
            SessionType = type;
            SessionId = UUID.Zero;
        }
        public async void Start()
        {
            PeerConnection = await CreatePeerConnection();
            iceTrickleTask = IceTrickleStart();

            // Start the microphone capture after creating the connection
            if (AudioDevice?.Source != null)
            {
                AudioDevice.StartRecording();
            }
        }

        // Helper that posts to caps and returns deserialized OSD, with retries and timeout handling
        private async Task<OSD> PostCapsWithRetries(Uri cap, OSD payload, int maxAttempts = 3, TimeSpan? timeout = null)
        {
            if (cap == null) throw new VoiceException("Capability URI is null.");
            if (timeout == null) timeout = TimeSpan.FromSeconds(10);

            int attempt = 0;
            Exception lastEx = null;
            while (attempt < maxAttempts)
            {
                attempt++;
                var tcs = new TaskCompletionSource<(byte[] data, Exception err)>();
                try
                {
                    Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, Cts.Token, (response, data, error) =>
                    {
                        tcs.TrySetResult((data, error));
                    });

                    var delayTask = Task.Delay(timeout.Value, Cts.Token);
                    var completed = await Task.WhenAny(tcs.Task, delayTask);
                    if (completed == delayTask)
                    {
                        lastEx = new TimeoutException($"POST to {cap} timed out.");
                    }
                    else
                    {
                        var (data, err) = await tcs.Task;
                        if (err != null)
                        {
                            lastEx = err;
                        }
                        else
                        {
                            var osd = OSDParser.Deserialize(data);
                            return osd;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }

                // Backoff
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), Cts.Token);
            }

            throw new VoiceException($"Failed to POST to capability {cap}: {lastEx?.Message}");
        }

        public async Task<RTCPeerConnection> CreatePeerConnection()
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
                    Logger.Log($"Added TURN server from environment: {turnUrl}", Helpers.LogLevel.Info, Client);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read TURN environment variables: {ex.Message}", Helpers.LogLevel.Warning, Client);
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
               // Logger.Log($"📦 RTP: {rtpPacket.Payload.Length} bytes, PT={rtpPacket.Header.PayloadType}, Seq={rtpPacket.Header.SequenceNumber}, TS={rtpPacket.Header.Timestamp}", Helpers.LogLevel.Debug, Client);
                if (mediaType == SDPMediaTypesEnum.audio)
                {
                  //  Logger.Log($"📦 RTP Audio: {rtpPacket.Payload.Length} bytes, " +
                        //      $"PT={rtpPacket.Header.PayloadType}, " +
 // $"Seq={rtpPacket.Header.SequenceNumber}, " +
                    //          $"TS={rtpPacket.Header.Timestamp}",
                    //    Helpers.LogLevel.Debug, Client);

                    if (AudioDevice?.EndPoint != null)
                    {
                        AudioDevice.AudioSource_OnAudioSourceEncodedSample(
                            rtpPacket.Header.Timestamp,
                            rtpPacket.Payload);
                    }
                    else
                    {
                        Logger.Log("❌ Cannot play audio: EndPoint is null (SDL2 not initialized)",
                            Helpers.LogLevel.Warning, Client);
                    }
                }
            };

            pc.oniceconnectionstatechange += (state) =>
            {
                Logger.Log($"🧊 ICE connection state: {state}",
                    Helpers.LogLevel.Debug, Client);

                // If ICE fails, the connection will fail
                if (state == RTCIceConnectionState.failed)
                {
                    Logger.Log("❌ ICE connection failed - possible NAT/firewall issue",
                        Helpers.LogLevel.Warning, Client);
                }
            };

            // Create data channel BEFORE negotiation
            var dc = await pc.createDataChannel("SLData", new RTCDataChannelInit { ordered = true });

            dc.onopen += () =>
            {
                Logger.Log("✅ Data channel opened", Helpers.LogLevel.Info, Client);

                // Only send join message per spec
                TrySendDataChannelString("{\"j\":{\"p\":true}}");

                // Start position updates AFTER a small delay to ensure join is processed
                Task.Delay(100).ContinueWith(_ => StartPositionTimer());

                // Start data channel keepalive
                StartDataChannelKeepAliveTimer();

                OnDataChannelReady?.Invoke();
            };

            dc.onclose += () =>
            {
                StopPositionTimer();
                StopDataChannelKeepAliveTimer();
                OnDataChannelReady?.Invoke();
            };
            dc.onmessage += (channel, type, data) =>
            {
                var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                Logger.Log($"📨 Data channel message received: {msg}",
                    Helpers.LogLevel.Debug, Client);
                Task.Run(() => HandleDataChannelMessage(msg));
            };

            // SDP negotiation
            var audioTrack = new MediaStreamTrack(AudioDevice.Source.GetAudioSourceFormats());
            pc.addTrack(audioTrack);
            var offer = pc.createOffer();
            var rawSdp = offer.sdp.ToString();
            var processedSdp = ProcessLocalSdp(rawSdp);

            // Assign the mangled SDP string directly
            offer.sdp = processedSdp;

            await pc.setLocalDescription(offer);


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



            // Request provisioning from the simulator by sending the local SDP offer.
            // This causes the region to return a remote SDP answer so audio can flow.
            // Note: Provisioning is now handled externally by calling RequestProvision()

            //  Logger.Log($"📨 Final local SDP offer:\n{pc.localDescription.sdp}", Helpers.LogLevel.Debug, Client);
            pc.localDescription.sdp.SessionName = "LibreMetaVoice";

            // ICE and connection state handlers
            pc.onicecandidate += (candidate) =>
            {
                if (candidate != null)
                {
                    lock (PendingCandidates)
                    {
                        PendingCandidates.Enqueue(candidate);
                    }
                }
            };

            // ICE gathering state handler - ensure we send the ICE "completed" message when gathering finishes
            pc.onicegatheringstatechange += async (state) =>
            {
                try
                {
                    if (state == RTCIceGatheringState.complete)
                    {
                        Logger.Log("ICE gathering state completed", Helpers.LogLevel.Debug, Client);
                        await IceTrickleStop();
                    }
                    else if (state == RTCIceGatheringState.gathering)
                    {
                        Logger.Log("ICE gathering state has commenced.", Helpers.LogLevel.Debug, Client);
                        // Ensure trickle loop is running
                        _ = IceTrickleStart();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Exception in onicegatheringstatechange handler: {ex.Message}", Helpers.LogLevel.Warning, Client);
                }
            };
            pc.ondatachannel += (channel) =>
            {
                Logger.Log($"📥 Server created data channel: {channel.label} (id: {channel.id})",
                    Helpers.LogLevel.Info, Client);

                channel.onopen += () =>
                {
                    Logger.Log($"✅ Inbound channel '{channel.label}' opened", Helpers.LogLevel.Info, Client);
                };

                channel.onmessage += (ch, type, data) =>
                {
                    var msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                    //Logger.Log($"📨 Received on '{channel.label}': {msg}", Helpers.LogLevel.Info, Client);
                    Task.Run(() => HandleDataChannelMessage(msg));
                };

                channel.onclose += () =>
                {
                    Logger.Log($"⚠️ Inbound channel '{channel.label}' closed", Helpers.LogLevel.Warning, Client);
                };

                channel.onerror += (error) =>
                {
                    Logger.Log($"❌ Inbound channel '{channel.label}' error: {error}",
                        Helpers.LogLevel.Error, Client);
                };
            };
            pc.onconnectionstatechange += async (state) =>
            {
                Logger.Log($"Peer connection state changed to {state}.", Helpers.LogLevel.Debug, Client);
                if (state == RTCPeerConnectionState.connected)
                {
                    if (AudioDevice?.EndPoint != null)
                    {
                        await AudioDevice.EndPoint.StartAudioSink();
                        Logger.Log("✅ Audio sink started", Helpers.LogLevel.Info, Client);
                    }
                    else
                    {
                        // Attempt to recreate endpoint if possible
                        var got = AudioDevice?.EnsureEndpoint() ?? false;
                        if (got)
                        {
                            try
                            {
                                await AudioDevice.EndPoint.StartAudioSink();
                                Logger.Log("✅ Audio sink started after EnsureEndpoint", Helpers.LogLevel.Info, Client);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Failed to start audio sink after EnsureEndpoint: {ex.Message}", Helpers.LogLevel.Warning, Client);
                            }
                        }
                        else
                        {
                            Logger.Log("⚠️ Audio sink not started: SDL2 EndPoint is null (SDL2 not initialized or no devices found)", Helpers.LogLevel.Warning, Client);
                        }
                    }
                    OnPeerConnectionReady?.Invoke();
                }
                else if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.closed)
                {
                    if (AudioDevice?.EndPoint != null)
                    {
                        try { await AudioDevice.EndPoint.CloseAudioSink(); } catch { }
                    }
                    OnPeerConnectionClosed?.Invoke();
                }
            };
            AudioDevice.Source.OnAudioSourceEncodedSample += (duration, sample) =>
            {
                pc.SendAudio(duration, sample);
            };

            return pc;
        }

        private async void FlushPendingIceCandidates()
        {
            if (!answerReceived || PendingCandidates.Count == 0) return;

            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
            if (cap == null) return;

            var candidatesArray = new OSDArray();
            while (PendingCandidates.Count > 0)
            {
                var candidate = PendingCandidates.Dequeue();
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
                await PostCapsWithRetries(cap, payload);
                Logger.Log($"✅ Sent {candidatesArray.Count} ICE candidates", Helpers.LogLevel.Info, Client);
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Failed to send ICE candidates: {ex.Message}", Helpers.LogLevel.Warning, Client);
            }
        }

        // Send any pending ICE candidates to the voice signaling capability
        private async Task SendVoiceSignalingRequest()
        {
            if (!answerReceived)
            {
                Logger.Log("Skipping ICE send - no answer received yet",
                    Helpers.LogLevel.Debug, Client);
                return;
            }

            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);
            if (cap == null)
            {
                Logger.Log("Voice signaling cap not available", Helpers.LogLevel.Warning, Client);
                return;
            }

            var payload = new OSDMap
            {
                { "voice_server_type", "webrtc" },
                { "viewer_session", SessionId }
            };

            var canArray = new OSDArray();

            lock (PendingCandidates)
            {
                if (PendingCandidates.Count == 0) return; // Nothing to send

                Logger.Log($"Sending {PendingCandidates.Count} ICE candidates",
                    Helpers.LogLevel.Debug, Client);

                while (PendingCandidates.Count > 0)
                {
                    var candidate = PendingCandidates.Dequeue();
                    var map = new OSDMap
                    {
                        { "sdpMid", candidate.sdpMid },
                        { "sdpMLineIndex", candidate.sdpMLineIndex },
                        { "candidate", candidate.candidate }
                    };
                    canArray.Add(map);
                }
            }

            payload["candidates"] = canArray;

            try
            {
                var resp = await PostCapsWithRetries(cap, payload);
                Logger.Log($"✅ Sent {canArray.Count} ICE candidates successfully",
                    Helpers.LogLevel.Info, Client);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send ICE candidates: {ex.Message}",
                    Helpers.LogLevel.Warning, Client);
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
            Logger.Log($"Sending ICE Signaling Complete for {SessionId}", Helpers.LogLevel.Debug, Client);
            if (cap == null)
            {
                Logger.Log("Voice signaling capability not available for complete request.", Helpers.LogLevel.Warning, Client);
                return;
            }
            try
            {
                var resp = await PostCapsWithRetries(cap, payload);
                try
                {
                    if (resp is OSDMap respMap)
                    {
                        Logger.Log($"Voice signaling complete response: {OSDParser.SerializeJsonString(respMap, true)}", Helpers.LogLevel.Debug, Client);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Logger.Log($"Sending ICE Signaling Complete failed for {SessionId}: {ex.Message}", Helpers.LogLevel.Warning, Client);
            }
        }

        // Simple handler implementing viewer<->datachannel JSON protocol
        private void HandleDataChannelMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return;
            try
            {

                // Prefer parsing with LitJson directly to avoid issues in OSDParser.DeserializeJson
                JsonData root = null;
                try
                {
                    root = JsonMapper.ToObject(msg);
                }
                catch (Exception litEx)
                {
                    Logger.Log($"LitJson parsing failed: {litEx.Message}", Helpers.LogLevel.Debug, Client);
                }

                if (root == null || !root.IsObject)
                {
                    // Fall back to OSDParser for simpler payloads
                    var osd = OSDParser.DeserializeJson(msg);
                    if (!(osd is OSDMap map))
                    {
                        Logger.Log($"Data channel payload is not an OSDMap (type={osd?.GetType().Name}). Raw: {msg}", Helpers.LogLevel.Debug, Client);
                        return;
                    }

                    // convert OSDMap to a temporary variable for downstream logic
                    // Use existing code path by assigning to 'map' variable below via a small wrapper
                    // We'll continue with handling using OSDMap as before
                }

                // If we have a LitJson root, parse it directly (preferred path)
                if (root != null && root.IsObject)
                {
                    var jd = root;
                    var jdDict = jd as System.Collections.IDictionary;

                    // Helper functions (available to entire Json path)
                    int? ToInt(JsonData d)
                    {
                        try
                        {
                            if (d == null) return null;
                            if (d.IsInt) return (int)d;
                            if (d.IsLong) return (int)(long)d;
                            if (d.IsDouble) return (int)(double)d;
                            var s = d.ToString(); if (int.TryParse(s, out var v)) return v;
                        }
                        catch { }
                        return null;
                    }

                    bool? ToBool(JsonData d)
                    {
                        try
                        {
                            if (d == null) return null;
                            if (d.IsBoolean) return (bool)d;
                            var s = d.ToString().Trim('"');
                            if (bool.TryParse(s, out var b)) return b;
                            if (int.TryParse(s, out var i)) return i != 0;
                        }
                        catch { }
                        return null;
                    }

                    // Determine if this is a mixer->client per-peer update: keys are UUIDs
                    bool allKeysAreUuid = true;
                    if (jdDict != null)
                    {
                        foreach (var kObj in jdDict.Keys)
                        {
                            var k = kObj as string;
                            if (string.IsNullOrEmpty(k) || !UUID.TryParse(k, out _)) { allKeysAreUuid = false; break; }
                        }
                    }

                    if (allKeysAreUuid && jdDict != null)
                    {
                        foreach (var kObj in jdDict.Keys)
                        {
                            var key = kObj as string;
                            if (!UUID.TryParse(key, out var peerId)) continue;
                            var val = jd[key];
                            if (val == null || !val.IsObject) continue;
                            var peerMap = val as JsonData;
                            var peerDict = peerMap as System.Collections.IDictionary;

                            var state = new PeerAudioState();

                            if (peerDict != null && peerDict.Contains("p")) state.Power = ToInt(peerMap["p"]);
                            if (peerDict != null && peerDict.Contains("V")) state.VoiceActive = ToBool(peerMap["V"]);
                            else if (peerDict != null && peerDict.Contains("v")) state.VoiceActive = ToBool(peerMap["v"]);

                            if (peerDict != null && peerDict.Contains("j") && peerMap["j"].IsObject)
                            {
                                var jmap = peerMap["j"] as JsonData;
                                var jdict = jmap as System.Collections.IDictionary;
                                if (jdict != null && jdict.Contains("p")) state.JoinedPrimary = ToBool(jmap["p"]);
                                OnPeerJoined?.Invoke(peerId);
                            }

                            if (peerDict != null && peerDict.Contains("l") && ToBool(peerMap["l"]) == true)
                            {
                                state.Left = true;
                                OnPeerLeft?.Invoke(peerId);
                            }

                            OnPeerAudioUpdated?.Invoke(peerId, state);
                        }

                        return;
                    }

                    // Non-per-peer messages: handle 'j','l','sp','sh','lp','lh','m','ug','av','a'
                    // Helper functions
                    int? JInt(JsonData d) => ToInt(d);
                    bool? JBool(JsonData d) => ToBool(d);
                    string JStr(JsonData d) { try { return d?.ToString().Trim('"'); } catch { return null; } }

                    var contains = new Func<System.Collections.IDictionary, string, bool>((dict, key) => dict != null && dict.Contains(key));

                    var jdContains = jdDict;

                    // Join
                    if (contains(jdContains, "j") && jd["j"].IsObject)
                    {
                        UUID peerId = SessionId;
                        var joinMap = jd["j"] as JsonData;
                        var joinDict = joinMap as System.Collections.IDictionary;
                        var idStr = JStr(joinDict != null && joinDict.Contains("id") ? joinMap["id"] : null);
                        if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                        Peers.TryAdd(peerId, new OSDMap());
                        OnPeerJoined?.Invoke(peerId);
                    }

                    // Leave
                    if (contains(jdContains, "l") && JBool(jd["l"]) == true)
                    {
                        UUID peerId = SessionId;
                        var idStr = JStr(jdContains.Contains("id") ? jd["id"] : null);
                        if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                        Peers.TryRemove(peerId, out _);
                        OnPeerLeft?.Invoke(peerId);
                    }

                    // Positions
                    var avatarPos = new AvatarPosition { AgentId = SessionId };
                    bool posChanged = false;

                    if (contains(jdContains, "sp") && jd["sp"].IsObject)
                    {
                        var sp = jd["sp"] as JsonData;
                        var spDict = sp as System.Collections.IDictionary;
                        var x = JInt(spDict != null && spDict.Contains("x") ? sp["x"] : null);
                        var y = JInt(spDict != null && spDict.Contains("y") ? sp["y"] : null);
                        var z = JInt(spDict != null && spDict.Contains("z") ? sp["z"] : null);
                        if (x.HasValue && y.HasValue && z.HasValue) { avatarPos.SenderPosition = new Int3 { X = x.Value, Y = y.Value, Z = z.Value }; posChanged = true; }
                    }

                    if (jdContains != null && contains(jdContains, "sh") && jd["sh"].IsObject)
                    {
                        var sh = jd["sh"] as JsonData;
                        var shDict = sh as System.Collections.IDictionary;
                        var x = JInt(shDict != null && shDict.Contains("x") ? sh["x"] : null);
                        var y = JInt(shDict != null && shDict.Contains("y") ? sh["y"] : null);
                        var z = JInt(shDict != null && shDict.Contains("z") ? sh["z"] : null);
                        var w = JInt(shDict != null && shDict.Contains("w") ? sh["w"] : null);
                        if (x.HasValue && y.HasValue && z.HasValue && w.HasValue) { avatarPos.SenderHeading = new Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value }; posChanged = true; }
                    }

                    if (jdContains != null && contains(jdContains, "lp") && jd["lp"].IsObject)
                    {
                        var lp = jd["lp"] as JsonData;
                        var lpDict = lp as System.Collections.IDictionary;
                        var x = JInt(lpDict != null && lpDict.Contains("x") ? lp["x"] : null);
                        var y = JInt(lpDict != null && lpDict.Contains("y") ? lp["y"] : null);
                        var z = JInt(lpDict != null && lpDict.Contains("z") ? lp["z"] : null);
                        if (x.HasValue && y.HasValue && z.HasValue) { avatarPos.ListenerPosition = new Int3 { X = x.Value, Y = y.Value, Z = z.Value }; posChanged = true; }
                    }

                    if (jdContains != null && contains(jdContains, "lh") && jd["lh"].IsObject)
                    {
                        var lh = jd["lh"] as JsonData;
                        var lhDict = lh as System.Collections.IDictionary;
                        var x = JInt(lhDict != null && lhDict.Contains("x") ? lh["x"] : null);
                        var y = JInt(lhDict != null && lhDict.Contains("y") ? lh["y"] : null);
                        var z = JInt(lhDict != null && lhDict.Contains("z") ? lh["z"] : null);
                        var w = JInt(lhDict != null && lhDict.Contains("w") ? lh["w"] : null);
                        if (x.HasValue && y.HasValue && z.HasValue && w.HasValue) { avatarPos.ListenerHeading = new Int4 { X = x.Value, Y = y.Value, Z = z.Value, W = w.Value }; posChanged = true; }
                    }

                    if (posChanged)
                    {
                        UUID peerId = SessionId;
                        var idStr = JStr(jdContains != null && jdContains.Contains("id") ? jd["id"] : null);
                        if (!string.IsNullOrEmpty(idStr)) UUID.TryParse(idStr, out peerId);
                        avatarPos.AgentId = peerId;
                        OnPeerPositionUpdatedTyped?.Invoke(peerId, avatarPos);
                        // Convert positions into an OSDMap for legacy handler
                        var osdMap = new OSDMap();
                        // Minimal conversion: include sp/lp/sh/lh if present
                        if (avatarPos.SenderPosition.HasValue)
                        {
                            var spm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.SenderPosition.Value.X), ["y"] = OSD.FromInteger(avatarPos.SenderPosition.Value.Y), ["z"] = OSD.FromInteger(avatarPos.SenderPosition.Value.Z) };
                            osdMap["sp"] = spm;
                        }
                        if (avatarPos.SenderHeading.HasValue)
                        {
                            var shm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.SenderHeading.Value.X), ["y"] = OSD.FromInteger(avatarPos.SenderHeading.Value.Y), ["z"] = OSD.FromInteger(avatarPos.SenderHeading.Value.Z), ["w"] = OSD.FromInteger(avatarPos.SenderHeading.Value.W) };
                            osdMap["sh"] = shm;
                        }
                        if (avatarPos.ListenerPosition.HasValue)
                        {
                            var lpm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.X), ["y"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.Y), ["z"] = OSD.FromInteger(avatarPos.ListenerPosition.Value.Z) };
                            osdMap["lp"] = lpm;
                        }
                        if (avatarPos.ListenerHeading.HasValue)
                        {
                            var lhm = new OSDMap { ["x"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.X), ["y"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.Y), ["z"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.Z), ["w"] = OSD.FromInteger(avatarPos.ListenerHeading.Value.W) };
                            osdMap["lh"] = lhm;
                        }
                        OnPeerPositionUpdated?.Invoke(peerId, osdMap);
                        Peers.AddOrUpdate(peerId, osdMap, (k, v) => osdMap);
                    }

                    // Mute map 'm'
                    if (jdContains != null && contains(jdContains, "m") && jd["m"].IsObject)
                    {
                        var muteMap = jd["m"] as JsonData;
                        var muteDict = muteMap as System.Collections.IDictionary;
                        var dict = new Dictionary<UUID, bool>();
                        if (muteDict != null)
                        {
                            foreach (var keyObj in muteDict.Keys)
                            {
                                var key = keyObj as string;
                                if (UUID.TryParse(key, out var id))
                                {
                                    var b = JBool(muteMap[key]);
                                    if (b.HasValue) dict[id] = b.Value;
                                }
                            }
                        }
                        if (dict.Count > 0) OnMuteMapReceived?.Invoke(dict);
                    }

                    // Gain map 'ug'
                    if (jdContains != null && contains(jdContains, "ug") && jd["ug"].IsObject)
                    {
                        var gainMap = jd["ug"] as JsonData;
                        var gainDict = gainMap as System.Collections.IDictionary;
                        var dict = new Dictionary<UUID, int>();
                        if (gainDict != null)
                        {
                            foreach (var keyObj in gainDict.Keys)
                            {
                                var key = keyObj as string;
                                if (UUID.TryParse(key, out var id))
                                {
                                    var gi = JInt(gainMap[key]);
                                    if (gi.HasValue) dict[id] = gi.Value;
                                }
                            }
                        }
                        if (dict.Count > 0) OnGainMapReceived?.Invoke(dict);
                    }

                    // Avatar list handling
                    if (jdContains != null && contains(jdContains, "av") && jd["av"].IsArray)
                    {
                        var arr = jd["av"] as JsonData;
                        var list = new List<UUID>();
                        for (int i = 0; i < arr.Count; i++)
                        {
                            var item = arr[i];
                            var s = JStr(item);
                            if (!string.IsNullOrEmpty(s) && UUID.TryParse(s, out var id)) list.Add(id);
                        }
                        OnPeerListUpdated?.Invoke(list);
                    }
                    else if (jdContains != null && contains(jdContains, "a") && jd["a"].IsObject)
                    {
                        var amap = jd["a"] as JsonData;
                        var amapDict = amap as System.Collections.IDictionary;
                        var list = new List<UUID>();
                        if (amapDict != null)
                        {
                            foreach (var keyObj in amapDict.Keys)
                            {
                                var key = keyObj as string;
                                if (UUID.TryParse(key, out var id)) list.Add(id);
                            }
                        }
                        OnPeerListUpdated?.Invoke(list);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to parse data channel json: {ex.GetType().Name}: {ex.Message}\nRaw message: {msg}\nStack: {ex.StackTrace}", Helpers.LogLevel.Warning, Client);
            }
        }
        // Public method to safely send string messages over the data channel
        public bool TrySendDataChannelString(string str)
        {
            try
            {
                var dc = DataChannel;
                if (dc == null) return false;
                // RTCDataChannel in sipsorcery supports sending string payloads directly
                dc.send(str);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to send data channel message: {ex.Message}", Helpers.LogLevel.Warning, Client);
                return false;
            }
        }

        // Public method to set mute status for a peer
        public void SetPeerMute(UUID peerId, bool mute)
        {
            string json = $"{{\"m\": {{\"{peerId}\": {mute.ToString().ToLower()}}}}}";  // Changed \"\" to \"
            Logger.Log($"Setting mute for peer {peerId} to {mute}", Helpers.LogLevel.Info, Client);
            TrySendDataChannelString(json);
        }

        // Public method to set gain for a peer  
        public void SetPeerGain(UUID peerId, int gain)
        {
            string json = $"{{\"ug\": {{\"{peerId}\": {gain}}}}}";  // Changed \"\" to \"
            Logger.Log($"Setting gain for peer {peerId} to {gain}", Helpers.LogLevel.Info, Client);
            TrySendDataChannelString(json);
        }
        private Task IceTrickleStart()
        {
            // If a trickle task is already running, just return it
            if (iceTrickleTask != null
                && (iceTrickleTask.Status == TaskStatus.Running
                || iceTrickleTask.Status == TaskStatus.WaitingToRun
                || iceTrickleTask.Status == TaskStatus.WaitingForActivation))
            {
                return iceTrickleTask;

            }

            iceTrickleCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token);

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
                            poll();
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            Logger.Log($"IceTrickle loop poll exception: {ex.Message}", Helpers.LogLevel.Debug, Client);
                        }

                        try { await Task.Delay(TimeSpan.FromMilliseconds(25), token); } catch (OperationCanceledException) { break; }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"IceTrickle loop terminated with exception: {ex.Message}", Helpers.LogLevel.Warning, Client);
                }
            }

            iceTrickleTask = Task.Run(Loop, token);

            return iceTrickleTask;

            async void poll()
            {
                if (Client.Network.Connected && !SessionId.Equals(UUID.Zero) && PendingCandidates.Count > 0)
                {
                    await SendVoiceSignalingRequest();
                }
            }
        }

        private async Task IceTrickleStop()
        {
            while (true)
            {
                if (PendingCandidates.Count > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                else
                {
                    break;
                }
            }

            if (PeerConnection.iceGatheringState == RTCIceGatheringState.complete)
            {
                iceTrickleCts?.Cancel();
                await SendVoiceSignalingCompleteRequest();
            }
        }

        private string SanitizeRemoteSdp(string sdp)
        {
            if (string.IsNullOrEmpty(sdp)) return sdp;
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
                        Logger.Log($"Dropping remote ICE candidate with port 0: {line}", Helpers.LogLevel.Debug, Client);
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
                Logger.Log("Reprovision already in progress, skipping.", Helpers.LogLevel.Info, Client);
                return;
            }

            try
            {
                Logger.Log($"Starting reprovision for session {SessionId}", Helpers.LogLevel.Info, Client);

                try
                {
                    // Close existing peer connection if present
                    if (PeerConnection != null)
                    {
                        try { PeerConnection.Close("Reprovision"); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error closing peer connection during reprovision: {ex.Message}", Helpers.LogLevel.Warning, Client);
                }

                // Cancel any existing trickle loop
                try { iceTrickleCts?.Cancel(); } catch { }

                // Create a new peer connection
                RTCPeerConnection newPc = null;
                try
                {
                    newPc = await CreatePeerConnection();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to create new RTCPeerConnection during reprovision: {ex.Message}", Helpers.LogLevel.Warning, Client);
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
                            Logger.Log($"Reprovision completed, new session {SessionId}", Helpers.LogLevel.Info, Client);
                        else
                            Logger.Log("Reprovision failed: client not connected to network.", Helpers.LogLevel.Warning, Client);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Reprovision RequestProvision failed: {ex.Message}", Helpers.LogLevel.Warning, Client);
                    }
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
                Logger.Log("⚠️ Global position unavailable — returning zeroed map", Helpers.LogLevel.Warning, Client);
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
            int posX = (int)(pos.X * 100);
            int posY = (int)(pos.Y * 100);
            int posZ = (int)(pos.Z);

            // Multiply quaternion by 100 and convert to int
            int headX = (int)(h.X * 100);
            int headY = (int)(h.Y * 100);
            int headZ = (int)(h.Z);
            int headW = (int)(h.W * 100);

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

            /*  jw.WritePropertyName("sh");
             jw.WriteObjectStart();
             jw.WritePropertyName("x");
             jw.Write(headX);  // INTEGER, not float
             jw.WritePropertyName("y");
             jw.Write(headY);
             jw.WritePropertyName("z");
             jw.Write(headZ);
             jw.WritePropertyName("w");  // DON'T FORGET W!
             jw.Write(headW);
             jw.WriteObjectEnd();*/

            jw.WritePropertyName("lp");
            jw.WriteObjectStart();
            jw.WritePropertyName("x");
            jw.Write(posX);
            jw.WritePropertyName("y");
            jw.Write(posY);
            jw.WritePropertyName("z");
            jw.Write(posZ);
            jw.WriteObjectEnd();

            /*   jw.WritePropertyName("lh");
               jw.WriteObjectStart();
               jw.WritePropertyName("x");
               jw.Write(headX);
               jw.WritePropertyName("y");
               jw.Write(headY);
               jw.WritePropertyName("z");
               jw.Write(headZ);
               jw.WritePropertyName("w");  // DON'T FORGET W!
               jw.Write(headW);
               jw.WriteObjectEnd();*/

            jw.WriteObjectEnd();

            return jw.ToString();
        }

        // In VoiceSession.cs
        private void StartPositionTimer()
        {
            if (positionTimer != null) return;

            // Run every 100ms to match viewer behavior: detect changes and decide whether to send
            positionTimer = new System.Timers.Timer(100);
            positionTimer.Elapsed += (s, e) =>
            {
                try
                {
                    var dc = DataChannel;
                    if (dc == null || dc.readyState != RTCDataChannelState.open) return;

                    var globalPos = Client?.Self?.GlobalPosition ?? Vector3.Zero;
                    var heading = Client?.Self?.RelativeRotation ?? Quaternion.Identity;

                    if (globalPos == Vector3.Zero) return;

                    // Detect changes since last observed values
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
                catch (Exception ex)
                {
                    Logger.Log($"❌ Position/Timer error: {ex.Message}", Helpers.LogLevel.Error, Client);
                }
            };
            positionTimer.Start();
        }

        private void SendPositionUpdate(RTCDataChannel dc, Vector3d globalPos, Quaternion heading)
        {
            // Only send when flagged dirty
            if (!mSpatialCoordsDirty) return;

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

                var json = "{" +
                           "\"sp\":{" +
                           "\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "}" +
                           ",\"sh\":{" +
                           "\"x\":" + headX + ",\"y\":" + headY + ",\"z\":" + headZ + ",\"w\":" + headW + "}" +
                           ",\"lp\":{" +
                           "\"x\":" + posX + ",\"y\":" + posY + ",\"z\":" + posZ + "}" +
                           ",\"lh\":{" +
                           "\"x\":" + headX + ",\"y\":" + headY + ",\"z\":" + headZ + ",\"w\":" + headW + "}" +
                           "}";

                Logger.Log($"Sending Position: {json}", Helpers.LogLevel.Debug, Client);
                TrySendDataChannelString(json);

                // Update last sent and clear dirty flag
                lastSentGlobalPos = globalPos;
                lastSentHeading = heading;
                mSpatialCoordsDirty = false;
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Position send error: {ex.Message}", Helpers.LogLevel.Error, Client);
            }
        }

        private void StopPositionTimer()
        {

            try
            {
                positionTimer?.Stop();
                positionTimer?.Dispose();
            }
            catch { }
            finally
            {
                positionTimer = null;
            }
        }

        private System.Timers.Timer dataChannelKeepAliveTimer;

        private void StartDataChannelKeepAliveTimer()
        {
            if (dataChannelKeepAliveTimer != null) return;

            dataChannelKeepAliveTimer = new System.Timers.Timer(5000); // Send keepalive every 5 seconds
            dataChannelKeepAliveTimer.Elapsed += (s, e) =>
            {
                try
                {
                    var dc = DataChannel;
                    if (dc == null || dc.readyState != RTCDataChannelState.open) return;

                    // Send a simple keepalive message
                    TrySendDataChannelString("{\"ping\":true}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"❌ Data channel keepalive error: {ex.Message}", Helpers.LogLevel.Error, Client);
                }
            };
            dataChannelKeepAliveTimer.Start();
        }

        private void StopDataChannelKeepAliveTimer()
        {
            try
            {
                dataChannelKeepAliveTimer?.Stop();
                dataChannelKeepAliveTimer?.Dispose();
            }
            catch { }
            finally
            {
                dataChannelKeepAliveTimer = null;
            }
        }

        // Minimal implementation of RequestProvision to satisfy callers.
        // The real implementation is more involved; this stub returns false.
        public async Task<bool> RequestProvision()
        {
            if (Client?.Network == null || !Client.Network.Connected) { return false; }
            Logger.Log("Requesting voice capability...", Helpers.LogLevel.Info, Client);

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
            Logger.Log("==> Attempting to POST for voice provision...", Helpers.LogLevel.Debug, Client);
            var osd = await PostCapsWithRetries(cap, payload);
            Logger.Log($"<== Received provisioning response: {OSDParser.SerializeJsonString(osd)}", Helpers.LogLevel.Debug, Client);
            if (osd is OSDMap osdMap)
            {
                if (!osdMap.ContainsKey("jsep"))
                {
                    throw new VoiceException($"Region '{Client.Network.CurrentSim.Name}' does not support WebRtc.");
                }

                var jsep = (OSDMap)osdMap["jsep"];
                if (jsep.ContainsKey("type") && jsep["type"].AsString() != "answer")
                {
                    throw new VoiceException($"jsep returned is not an answer.");
                }

                var sdpString = jsep["sdp"].AsString();
                Logger.Log($"<<<< INCOMING REMOTE SDP <<<<\n{sdpString}", Helpers.LogLevel.Debug, Client);
                sdpString = SanitizeRemoteSdp(sdpString);
                var sessionId = osdMap.ContainsKey("viewer_session")
                    ? osdMap["viewer_session"].AsUUID()
                    : UUID.Zero;

                SessionId = sessionId;

                // Set remote description
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
                await SendVoiceSignalingRequest();

                // Store channel info if present
                if (osdMap.ContainsKey("channel"))
                    ChannelId = osdMap["channel"].AsString();
                if (osdMap.ContainsKey("credentials"))
                    ChannelCredentials = osdMap["credentials"].AsString();

                Logger.Log($"Local voice provisioned: session={sessionId}",
                    Helpers.LogLevel.Info, Client);
            }
        }

        private async Task RequestMultiAgentVoiceProvision(Uri cap)
        {
            var req = new MultiAgentVoiceProvisionRequest(SdpLocal);
            // include channel/credentials if present on this session
            if (!string.IsNullOrEmpty(ChannelId)) req.ChannelId = ChannelId;
            if (!string.IsNullOrEmpty(ChannelCredentials)) req.ChannelCredentials = ChannelCredentials;

            var payload = req.Serialize();
            var osd = await PostCapsWithRetries(cap, payload);
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
                answerReceived = true;
                FlushPendingIceCandidates();
                if (set != SetDescriptionResultEnum.OK)
                {
                    PeerConnection.Close("Failed to set remote description (multiagent).");
                    throw new VoiceException("Failed to set remote description (multiagent).");
                }
                SessionId = sessionId;

                if (osdMap.ContainsKey("channel")) ChannelId = osdMap["channel"].AsString();
                if (osdMap.ContainsKey("credentials")) ChannelCredentials = osdMap["credentials"].AsString();

                // Flush any ICE candidates gathered before the answer (ICE trickling)
                try
                {
                }
                catch (Exception ex)
                {
                    Logger.Log($"Immediate ICE trickle failed (multiagent): {ex.Message}", Helpers.LogLevel.Warning, Client);
                }
            }
        }

        private async Task SendCloseSessionRequest()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(PROVISION_VOICE_ACCOUNT_CAP);

            Logger.Log($"Closing voice session {SessionId}", Helpers.LogLevel.Info, Client);
            var payload = new OSDMap
             {
                 { "logout", true },
                 { "voice_server_type", "webrtc" },
                 { "viewer_session", SessionId }
             };
            try
            {
                await PostCapsWithRetries(cap, payload);
            }
            catch (Exception ex)
            {
                Logger.Log($"Close session request failed: {ex.Message}", Helpers.LogLevel.Warning, Client);
            }
        }

        // Close and cleanup session resources
        public void CloseSession()
        {
            try
            {
                Cts.Cancel();
            }
            catch { }

            try
            {
                StopPositionTimer();
                StopDataChannelKeepAliveTimer();
            }
            catch { }

            try
            {
                if (PeerConnection != null)
                {
                    try { PeerConnection.Close("ClientClose"); } catch { }
                }
            }
            catch { }

            // Best-effort notify server — send complete/close in background
            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendVoiceSignalingCompleteRequest();
                    }
                    catch { }
                });
            }
            catch { }
        }
    }
}
