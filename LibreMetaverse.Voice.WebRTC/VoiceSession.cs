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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;

using Logger = OpenMetaverse.Logger;

namespace LibreMetaverse.Voice.WebRTC
{
    class VoiceSession 
    {
        public const string PROVISION_VOICE_ACCOUNT_CAP = "ProvisionVoiceAccountRequest";
        public const string VOICE_SIGNALING_CAP = "VoiceSignalingRequest";

        public enum ESessionType
        {
            LOCAL,
            MUTLIAGENT
        }

        private readonly GridClient Client;
        private readonly RTCPeerConnection PeerConnection;
        private readonly Sdl2Audio AudioDevice = new Sdl2Audio();

        public bool Connected => PeerConnection?.connectionState == RTCPeerConnectionState.connected;

        private ESessionType SessionType { get; }
        public UUID SessionId { get; private set; }
        public string SdpLocal => PeerConnection?.localDescription.sdp.ToString();
        public string SdpRemote => PeerConnection?.remoteDescription.sdp.ToString();

        private readonly object _candidateLock = new object();
        private readonly List<RTCIceCandidate> PendingCandidates = new List<RTCIceCandidate>();

        readonly CancellationTokenSource Cts = new CancellationTokenSource();

        public event Action OnPeerConnectionClosed;

        public VoiceSession(ESessionType type, GridClient client)
        {
            Client = client;
            SessionType = type;
            SessionId = UUID.Zero;
            PeerConnection = CreatePeerConnection();
        }

        public RTCPeerConnection CreatePeerConnection()
        {
            var pc = new RTCPeerConnection(null);
            var audioTrack = new MediaStreamTrack(OpusAudioEncoder.MEDIA_FORMAT_OPUS, MediaStreamStatusEnum.SendRecv);
            pc.addTrack(audioTrack);
            pc.createDataChannel("SLData");
            pc.setLocalDescription(pc.createOffer());
            pc.localDescription.sdp.SessionName = "LibreMetaVoice";

            #region ICE_ACTIONS
            pc.onicegatheringstatechange += (state) =>
            {
                if (state == RTCIceGatheringState.complete)
                {
                    Logger.Log("ICE gathering state completed", Helpers.LogLevel.Debug);
                    SendVoiceSignalingCompleteRequest().Wait();
                }
                else
                {
                    Logger.Log($"ICE gathering state change to {state}.", Helpers.LogLevel.Debug);
                }
            };
            pc.onicecandidate += (candidate) =>
            {
                Logger.Log($"ICE candidate received: {candidate.candidate}", Helpers.LogLevel.Debug);
                lock (_candidateLock)
                {
                    PendingCandidates.Add(candidate);
                }

                if (pc.canTrickleIceCandidates)
                {
                    TrickleCandidates();
                }
            };
            pc.onicecandidateerror += (candidate, error) =>
            {
                Logger.Log($"Error adding ICE candidate. {error} {candidate}", Helpers.LogLevel.Warning);
            };
            #endregion ICE_ACTIONS
            #region DEBUGS
            //pc.OnReceiveReport += (re, media, rr) => Logger.Log($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}", Helpers.LogLevel.Debug);
            //pc.OnSendReport += (media, sr) => Logger.Log($"RTCP Send for {media}\n{sr.GetDebugSummary()}", Helpers.LogLevel.Debug);
            //pc.GetRtpChannel().OnStunMessageSent += (msg, ep, isRelay) => Logger.Log($"STUN {msg.Header.MessageType} sent to {ep}.", Helpers.LogLevel.Debug);
            //pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) => Logger.Log($"STUN {msg.Header.MessageType} received from {ep}.", Helpers.LogLevel.Debug);
            pc.OnRtcpBye += (reason) => Logger.Log($"RTCP BYE receive, reason: {(string.IsNullOrWhiteSpace(reason) ? "<none>" : reason)}.", Helpers.LogLevel.Debug);
            pc.ondatachannel += (channel) => Logger.Log($"ondatachannel {channel.label}", Helpers.LogLevel.Debug);
            pc.onsignalingstatechange += () => Logger.Log("Signaling state changed", Helpers.LogLevel.Debug);
            pc.oniceconnectionstatechange += (state) => Logger.Log($"ICE connection state changed to {state}.", Helpers.LogLevel.Debug);
            pc.OnTimeout += (mediaType) => Logger.Log($"Timeout on {mediaType} media.", Helpers.LogLevel.Debug);
            pc.onnegotiationneeded += () => Logger.Log("Negotiation needed", Helpers.LogLevel.Debug);
            #endregion DEBUGS
            pc.OnAudioFormatsNegotiated += (formats) =>
            {
                Logger.Log($"Received list of {formats.Count} formats", Helpers.LogLevel.Debug);
            };
            pc.onconnectionstatechange += (state) =>
            {
                Logger.Log($"Peer connection state changed to {state}.", Helpers.LogLevel.Debug);
                switch (state)
                {
                    case RTCPeerConnectionState.connecting:
                        Logger.Log("RTC peer connecting", Helpers.LogLevel.Debug);
                        break;
                    case RTCPeerConnectionState.connected:
                        AudioDevice.EndPoint.StartAudioSink();
                        pc.OnRtpPacketReceived += (rep, media, rtpPkt) =>
                        {
                            if (media == SDPMediaTypesEnum.audio && pc.AudioDestinationEndPoint != null)
                            {
                                Logger.Log($"Forwarding {media} RTP packet. Timestamp: {rtpPkt.Header.Timestamp}.", Helpers.LogLevel.Debug);
                                AudioDevice.EndPoint.GotAudioRtp(rep, rtpPkt.Header.SyncSource, 
                                    rtpPkt.Header.SequenceNumber, rtpPkt.Header.Timestamp, 
                                    rtpPkt.Header.PayloadType, rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                            }
                        };
                        pc.OnRtpClosed += (reason) =>
                        {
                            pc.Close(reason);
                            Task task = SendCloseSessionRequest();
                            task.Wait();
                        };
                        break;
                    case RTCPeerConnectionState.closed:
                    case RTCPeerConnectionState.disconnected:
                        AudioDevice.EndPoint.CloseAudioSink();
                        goto case RTCPeerConnectionState.failed;
                    case RTCPeerConnectionState.failed:
                        OnPeerConnectionClosed?.Invoke();
                        break;
                }
            };
            pc.OnStarted += () =>
            {
                Logger.Log($"========== Voice session started! ============", Helpers.LogLevel.Debug);
                TrickleCandidates();
            };

            return pc;
        }

        public async Task<bool> RequestProvision()
        {
            if (!Client.Network.Connected) { return false; }
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

        public void CloseSession()
        {
            Cts.Cancel();
            PeerConnection.close();
        }

        private async Task RequestLocalVoiceProvision(Uri cap)
        {
            var payload = new LocalVoiceProvisionRequest(SdpLocal);

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                Cts.Token, (response, data, error) =>
                {
                    if (error != null)
                    {
                        throw new VoiceException($"Local voice provisioning failed with error: {error}");
                    }
                    var result = OSDParser.Deserialize(data);
                    if (result is OSDMap osdMap)
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
                        var sessionId = osdMap["viewer_session"].AsUUID();

                        var desc = SDP.ParseSDPDescription(sdpString);
                        PeerConnection.SetRemoteDescription(SdpType.answer, desc);
                        SessionId = sessionId;
                    }
                });
        }

        private async Task RequestMultiAgentVoiceProvision(Uri cap)
        {
            var payload = new MultiAgentVoiceProvisionRequest(SdpLocal);

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(),
                Cts.Token, (response, data, error) =>
                {
                    if (error != null)
                    {
                        throw new VoiceException($"Multiagent voice provisioning failed with error: {error}");
                    }
                    OSD result = OSDParser.Deserialize(data);
                    if (result is OSDMap osdMap)
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
                        var sessionId = osdMap["viewer_session"].AsUUID();

                        var desc = SDP.ParseSDPDescription(sdpString);
                        PeerConnection.SetRemoteDescription(SdpType.answer, desc);
                        SessionId = sessionId;
                    }
                });
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
            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload,
                Cts.Token, null);
        }

        private async Task SendVoiceSignalingRequest()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);

            var payload = new OSDMap
            {
                { "voice_server_type", "webrtc" },
                { "viewer_session", SessionId }
            };
            var canArray = new OSDArray();
            payload["candidates"] = canArray;

            lock (_candidateLock)
            {
                Logger.Log($"Sending {PendingCandidates.Count} ICE candidates for {SessionId}", Helpers.LogLevel.Debug);
                foreach (var map in PendingCandidates.Select(candidate => new OSDMap
                         {
                             { "sdpMid", candidate.sdpMid },
                             { "sdpMLineIndex", candidate.sdpMLineIndex },
                             { "candidate", candidate.candidate }
                         }))
                {
                    canArray.Add(map);
                }
                PendingCandidates.Clear();
            }

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload,
                Cts.Token, null);
        }

        private async Task SendVoiceSignalingCompleteRequest()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI(VOICE_SIGNALING_CAP);

            var payload = new OSDMap
            {

                { "voice_server_type", "webrtc" },
                { "viewer_session", SessionId }
            };
            var candidates = new OSDMap
            {
                { "completed", true }
            };
            payload["candidate"] = candidates;

            Logger.Log($"Sending ICE Signaling Complete for {SessionId}", Helpers.LogLevel.Debug);
            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload,
                Cts.Token, null);
        }

        private void TrickleCandidates()
        {
            if ((PendingCandidates == null || PendingCandidates.Count > 0) && Connected)
            {
                SendVoiceSignalingRequest().Wait();
            }
        }
    }
}
