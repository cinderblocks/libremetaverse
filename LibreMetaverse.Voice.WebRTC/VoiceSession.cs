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
using SIPSorcery.Sys;
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
        private readonly Sdl2Audio AudioDevice;
        private RTCPeerConnection PeerConnection;

        public event Action OnPeerConnectionClosed;
        public event Action OnPeerConnectionReady;
        public event Action OnDataChannelReady;

        public UUID SessionId { get; private set; }
        public string SdpLocal => PeerConnection?.localDescription.sdp.ToString();
        public string SdpRemote => PeerConnection?.remoteDescription.sdp.ToString();

        public bool Connected => PeerConnection?.connectionState == RTCPeerConnectionState.connected;
        public RTCDataChannel DataChannel => PeerConnection.DataChannels.FirstOrDefault();
        private ESessionType SessionType { get; }
        private readonly Queue<RTCIceCandidate> PendingCandidates = new Queue<RTCIceCandidate>();
        private readonly CancellationTokenSource Cts = new CancellationTokenSource();

        private CancellationTokenSource iceTrickleCts;
        private Task iceTrickleTask;

        public VoiceSession(Sdl2Audio audioDevice, ESessionType type, GridClient client)
        {
            Client = client;
            AudioDevice = audioDevice;
            SessionType = type;
            SessionId = UUID.Zero;
        }
        public void Start()
        {
            PeerConnection = CreatePeerConnection().Result;
            iceTrickleTask = IceTrickleStart();
        }

        public async Task<RTCPeerConnection> CreatePeerConnection()
        {
            var pc = new RTCPeerConnection(new RTCConfiguration {X_ICEIncludeAllInterfaceAddresses = true }, 0, new PortRange(60000, 60100));

            #region ICE_ACTIONS
            pc.oniceconnectionstatechange += (state) =>
            {
                Logger.Log($"ICE connection state changed to {state}.", Helpers.LogLevel.Debug, Client);
            };
            pc.onicegatheringstatechange += async (state) =>
            {
                if (state == RTCIceGatheringState.complete)
                {
                    Logger.Log("ICE gathering state completed", Helpers.LogLevel.Debug, Client);
                    await IceTrickleStop();
                }
                else if (state == RTCIceGatheringState.gathering)
                {
                    Logger.Log($"ICE gathering state has commenced.", Helpers.LogLevel.Debug, Client);
                    await IceTrickleStart();
                }
            };
            pc.onicecandidate += (candidate) =>
            {
                Logger.Log($"ICE candidate received: {candidate.candidate}", Helpers.LogLevel.Debug, Client);
                lock (PendingCandidates)
                {
                    PendingCandidates.Enqueue(candidate);
                }
            };
            pc.onicecandidateerror += (candidate, error) =>
            {
                Logger.Log($"Error adding ICE candidate. {error} {candidate}", Helpers.LogLevel.Warning, Client);
            };
            #endregion ICE_ACTIONS
            #region DEBUGS
            pc.OnReceiveReport += (re, media, rr) =>
            {
                if (rr.Bye != null)
                {
                    Logger.Log($"RTCP recv BYE {media}", Helpers.LogLevel.Debug, Client);
                } else if (rr.ReceiverReport != null)
                {
                    var report = rr.ReceiverReport.ReceptionReports?.FirstOrDefault();
                    if (report != null)
                    {
                        Logger.Log($"RTCP {media} Receiver Report SSRC {report.SSRC}: pkts lost {report.PacketsLost}, delay since SR {report.DelaySinceLastSenderReport}.", 
                            Helpers.LogLevel.Debug, Client);
                    }
                    else
                    {
                        Logger.Log($"RTCP {media} Receiver Report SSRC {rr.ReceiverReport.SSRC} empty.",
                            Helpers.LogLevel.Debug, Client);
                    }
                }
            };
            pc.OnSendReport += (media, sr) =>
            {
                if (sr.Bye != null)
                {
                    Logger.Log($"RTCP sent BYE {media}", Helpers.LogLevel.Debug, Client);
                }
                else if (sr.SenderReport != null)
                {
                    var report = sr.SenderReport;
                    Logger.Log($"RTCP sent SR {media}, SSRC {report.SSRC} pkts {report.PacketCount}, bytes {report.OctetCount}", 
                        Helpers.LogLevel.Debug, Client);
                }
                else
                {
                    var rr = sr.ReceiverReport.ReceptionReports?.FirstOrDefault();
                    if (rr != null)
                    {
                        Logger.Log($"RTCP sent RR {media}, SSRC {rr.SSRC}, seqnum {rr.ExtendedHighestSequenceNumber}, pkts lost {rr.PacketsLost}", 
                            Helpers.LogLevel.Debug, Client);
                    }
                }
            };
            //pc.GetRtpChannel().OnStunMessageSent += (msg, ep, isRelay) => Logger.Log($"STUN {msg.Header.MessageType} sent to {ep}.", Helpers.LogLevel.Debug, Client);
            //pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) => Logger.Log($"STUN {msg.Header.MessageType} received from {ep}.", Helpers.LogLevel.Debug, Client);
            pc.OnRtcpBye += (reason) => Logger.Log($"RTCP BYE receive, reason: {(string.IsNullOrWhiteSpace(reason) ? "<none>" : reason)}.", Helpers.LogLevel.Debug, Client);
            pc.OnTimeout += (mediaType) => Logger.Log($"Timeout on {mediaType} media.", Helpers.LogLevel.Debug, Client);
            pc.onnegotiationneeded += () => Logger.Log("Negotiation needed", Helpers.LogLevel.Debug, Client);
            #endregion DEBUGS

            pc.OnAudioFormatsNegotiated += (formats) =>
            {
                Logger.Log($"Negotiated {formats.Count} formats, selecting '{formats.First().FormatName}/{formats.First().ClockRate}/{formats.First().ChannelCount}'",
                    Helpers.LogLevel.Debug, Client);
            };
            pc.onsignalingstatechange += () =>
            {
                Logger.Log($"Signaling state changed to {pc.signalingState}", Helpers.LogLevel.Debug, Client);
                if (pc.signalingState == RTCSignalingState.have_local_offer)
                {
                    //Logger.Log($"Offer SDP:\n{PeerConnection.localDescription.sdp}:", Helpers.LogLevel.Debug);
                } else if (pc.signalingState == RTCSignalingState.have_remote_offer ||
                           pc.signalingState == RTCSignalingState.stable)
                {
                    //Logger.Log($"Answer SDP:\n{PeerConnection.remoteDescription.sdp}", Helpers.LogLevel.Debug);
                }
            };
            pc.onconnectionstatechange += async (state) =>
            {
                Logger.Log($"Peer connection state changed to {state}.", Helpers.LogLevel.Debug, Client);
                switch (state)
                {
                    case RTCPeerConnectionState.connecting:
                        break;
                    case RTCPeerConnectionState.connected:
                        await AudioDevice.EndPoint.StartAudioSink();
                        pc.OnRtpPacketReceived += (rep, media, rtpPkt) =>
                        {
                            Logger.Log($"RTP {media} Packet {rtpPkt.Header}", Helpers.LogLevel.Debug, Client);
                            if (media == SDPMediaTypesEnum.audio && pc.AudioDestinationEndPoint != null)
                            {
                                Logger.Log($"Forwarding {media} RTP packet. Timestamp: {rtpPkt.Header.Timestamp}.", Helpers.LogLevel.Debug, Client);
                                AudioDevice.EndPoint.GotAudioRtp(rep, rtpPkt.Header.SyncSource, 
                                    rtpPkt.Header.SequenceNumber, rtpPkt.Header.Timestamp, 
                                    rtpPkt.Header.PayloadType, rtpPkt.Header.MarkerBit == 1, rtpPkt.Payload);
                            }
                        };
                        pc.OnRtpClosed += async (reason) =>
                        {
                            Logger.Log($"RTP is closed.", Helpers.LogLevel.Debug, Client);
                            pc.Close(reason);
                            await SendCloseSessionRequest();
                        };
                        OnPeerConnectionReady?.Invoke();
                        break;
                    case RTCPeerConnectionState.closed:
                        goto case RTCPeerConnectionState.disconnected;
                    case RTCPeerConnectionState.disconnected:
                        await AudioDevice.EndPoint.CloseAudioSink();
                        goto case RTCPeerConnectionState.failed;
                    case RTCPeerConnectionState.failed:
                        OnPeerConnectionClosed?.Invoke();
                        break;
                }
            };
            pc.OnStarted += async () =>
            {
                Logger.Log("WebRTC session started.", Helpers.LogLevel.Debug, Client);
            };

            #region DATA CHANNEL
            pc.ondatachannel += (rdc) =>
            {
                Logger.Log($"Remote data channel created: '{rdc.label}'", Helpers.LogLevel.Debug, Client);
                rdc.onerror += (error) => Logger.Log($"Data channel {rdc.label} encountered error: {error}", Helpers.LogLevel.Debug, Client);
                rdc.onopen += () =>
                {
                    Logger.Log($"Data channel {rdc.label} opened.", Helpers.LogLevel.Debug, Client);
                    OnDataChannelReady?.Invoke();
                };
                rdc.onclose += () =>
                {
                    Logger.Log($"Data channel {rdc.label} closed.", Helpers.LogLevel.Debug, Client);
                };
                rdc.onmessage += (channel, type, data) =>
                {
                    switch (type)
                    {
                        case DataChannelPayloadProtocols.WebRTC_Binary_Empty:
                        case DataChannelPayloadProtocols.WebRTC_String_Empty:
                            Logger.Log($"Data channel '{channel.label}' empty message type {type}.",
                                Helpers.LogLevel.Debug, Client);
                            break;
                        case DataChannelPayloadProtocols.WebRTC_Binary:
                            Logger.Log($"Data channel '{channel.label}' received {data.Length} bytes.", Helpers.LogLevel.Debug, Client);
                            break;
                        case DataChannelPayloadProtocols.WebRTC_String:
                            var msg = System.Text.Encoding.UTF8.GetString(data);
                            Logger.Log($"Data channel '{channel.label}' message {type} received: {msg}.", Helpers.LogLevel.Debug, Client);
                            break;
                    }
                };
            };
            var dc = await pc.createDataChannel("SLData", new RTCDataChannelInit{ordered = true, negotiated = true});
            dc.onerror += (error) => Logger.Log($"Data channel {dc.label} encountered error: {error}", Helpers.LogLevel.Debug, Client);
            dc.onopen += () =>
            {
                Logger.Log($" Data channel {dc.label} opened.", Helpers.LogLevel.Debug, Client);
                OnDataChannelReady?.Invoke();
            };
            dc.onclose += () =>
            {
                Logger.Log($"Data channel {dc.label} closed.", Helpers.LogLevel.Debug, Client);
            };
            dc.onmessage += (channel, type, data) =>
            {
                Logger.Log($"Data channel {dc.label} message;", Helpers.LogLevel.Debug, Client);
                switch (type)
                {
                    case DataChannelPayloadProtocols.WebRTC_Binary_Empty:
                    case DataChannelPayloadProtocols.WebRTC_String_Empty:
                        Logger.Log($"Data channel '{channel.label}' empty message type {type}.",
                            Helpers.LogLevel.Debug, Client);
                        break;
                    case DataChannelPayloadProtocols.WebRTC_Binary:
                        Logger.Log($"Data channel '{channel.label}' received {data.Length} bytes.", Helpers.LogLevel.Debug, Client);
                        break;
                    case DataChannelPayloadProtocols.WebRTC_String:
                        var msg = System.Text.Encoding.UTF8.GetString(data);
                        Logger.Log($"Data channel '{channel.label}' message {type} received: {msg}.", Helpers.LogLevel.Debug, Client);
                        break;
                }
            };


            #endregion DATA CHANNEL

            var audioTrack = new MediaStreamTrack(OpusAudioEncoder.MEDIA_FORMAT_OPUS);
            pc.addTrack(audioTrack);

            var offer = pc.createOffer();
            await pc.setLocalDescription(offer);
            pc.localDescription.sdp.SessionName = "LibreMetaVoice";
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

        #region Private Capability Requests

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

                        SessionId = sessionId;
                        var set = PeerConnection.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(sdpString));
                        if (set != SetDescriptionResultEnum.OK)
                        {
                            PeerConnection.Close("Failed to set remote description.");
                            throw new VoiceException($"Failed to set remote description: {result}");
                        } 
                        Logger.Log($"Local voice provisioned under session {sessionId}", Helpers.LogLevel.Debug,
                                Client);
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

            lock (PendingCandidates)
            {
                Logger.Log($"Sending {PendingCandidates.Count} ICE candidates for {SessionId}", Helpers.LogLevel.Debug, Client);
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

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload,
                Cts.Token, (response, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log($"Sending ICE candidates failed. Server responded: {error.Message}",
                            Helpers.LogLevel.Warning, Client);
                    }
                });
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

            Logger.Log($"Sending ICE Signaling Complete for {SessionId}", Helpers.LogLevel.Debug, Client);
            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload,
                Cts.Token, (response, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log(
                            $"Sending ICE Signaling Complete failed for {SessionId}. Server responded: {error.Message}",
                            Helpers.LogLevel.Warning, Client);
                    }
                });
        }

        private async Task IceTrickleStart()
        {
            if (iceTrickleTask != null
                && (iceTrickleTask.Status == TaskStatus.Running
                || iceTrickleTask.Status == TaskStatus.WaitingToRun
                || iceTrickleTask.Status == TaskStatus.WaitingForActivation))
            {
                return;

            }

            iceTrickleCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token);
            await Repeat.Interval(TimeSpan.FromMilliseconds(25), poll, iceTrickleCts.Token);

            return;
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

        #endregion Private Capability Requests
    }
}
