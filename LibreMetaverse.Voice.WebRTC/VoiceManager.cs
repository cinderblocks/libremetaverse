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

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceManager
    {
        private readonly GridClient Client;
        public readonly Sdl2Audio AudioDevice;
        private VoiceSession CurrentSession;
        private bool _enabled = true;

        // Store channel info from parcel voice info
        private string _channelId;
        private string _channelCredentials;

        // Expose SDP and connection state safely
        public string sdpLocal => CurrentSession?.SdpLocal;
        public string sdpRemote => CurrentSession?.SdpRemote;
        public bool connected => CurrentSession?.Connected ?? false;

        // Expose session/channel info
        public UUID SessionId => CurrentSession?.SessionId ?? UUID.Zero;
        public string ChannelId => CurrentSession?.ChannelId ?? _channelId;
        public string ChannelCredentials => CurrentSession?.ChannelCredentials ?? _channelCredentials;

        // Expose data-channel / voice events to clients (raw)
        public event Action<UUID> PeerJoined;
        public event Action<UUID> PeerLeft;
        public event Action<UUID, OSDMap> PeerPositionUpdated;
        public event Action<List<UUID>> PeerListUpdated;

        // Expose typed events per PDF
        public event Action<UUID, VoiceSession.PeerAudioState> PeerAudioUpdated;
        public event Action<UUID, VoiceSession.AvatarPosition> PeerPositionUpdatedTyped;
        public event Action<Dictionary<UUID, bool>> MuteMapReceived;
        public event Action<Dictionary<UUID, int>> GainMapReceived;
        public event Action<string, int, string> OnParcelVoiceInfo;

        public VoiceManager(GridClient client)
        {
            Client = client;
            AudioDevice = new Sdl2Audio();
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);
        }

        public async Task<bool> ConnectPrimaryRegion()
        {
            Console.WriteLine("[WebRTC] ConnectPrimaryRegion started");
            if (!Client.Network.Connected) { return false; }
            if (!_enabled) 
            {
                Logger.Log("WebRTC voice is disabled due to unsupported voice version", Helpers.LogLevel.Warning, Client);
                return false;
            }

            // Request parcel voice info to determine if we need multi-agent or local
            var parcelInfoSuccess = await RequestParcelVoiceInfo();
            var sessionType = VoiceSession.ESessionType.LOCAL;
            if (parcelInfoSuccess && !string.IsNullOrEmpty(ChannelId))
            {
                sessionType = VoiceSession.ESessionType.MUTLIAGENT;
                Logger.Log("Using multi-agent voice for private parcel", Helpers.LogLevel.Info, Client);
            }
            else
            {
                Logger.Log("Using local voice for region", Helpers.LogLevel.Info, Client);
            }

            CurrentSession = new VoiceSession(AudioDevice, sessionType, Client);

            // Set channel info if available
            if (!string.IsNullOrEmpty(ChannelId))
            {
                CurrentSession.ChannelId = ChannelId;
                CurrentSession.ChannelCredentials = ChannelCredentials;
            }

            // wire internal events
            CurrentSession.OnDataChannelReady += CurrentSessionOnOnDataChannelReady;
            CurrentSession.OnPeerConnectionReady += () => { /* reserved for future */ };
            CurrentSession.OnPeerConnectionClosed += () => { /* reserved for future */ };

            // forward session events (raw)
            CurrentSession.OnPeerJoined += (id) => PeerJoined?.Invoke(id);
            CurrentSession.OnPeerLeft += (id) => PeerLeft?.Invoke(id);
            CurrentSession.OnPeerPositionUpdated += (id, map) => PeerPositionUpdated?.Invoke(id, map);
            CurrentSession.OnPeerListUpdated += (list) => PeerListUpdated?.Invoke(list);

            // forward typed events
            CurrentSession.OnPeerAudioUpdated += (id, state) => PeerAudioUpdated?.Invoke(id, state);
            CurrentSession.OnPeerPositionUpdatedTyped += (id, pos) => PeerPositionUpdatedTyped?.Invoke(id, pos);
            CurrentSession.OnMuteMapReceived += (m) => MuteMapReceived?.Invoke(m);
            CurrentSession.OnGainMapReceived += (g) => GainMapReceived?.Invoke(g);

            CurrentSession.Start();
            var provisioned = await CurrentSession.RequestProvision();
            return provisioned;
        }

        public void Disconnect()
        {
            if (CurrentSession == null) return;

            // detach handlers
            CurrentSession.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
            // Note: events forwarded via lambda can't be individually removed; rely on session disposal.

            CurrentSession.CloseSession();
            CurrentSession = null;
            _channelId = null;
            _channelCredentials = null;
        }

        public void SendGlobalPosition()
        {
           // Console.WriteLine("[WebRTC] sending position");
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
          //  Logger.DebugLog($"Sending position with {jw}");

            if (CurrentSession != null)
            {
                // safe send
                _ = CurrentSession.TrySendDataChannelString(jw.ToString());
            }
        }

        private void CurrentSessionOnOnDataChannelReady()
        {
            Console.WriteLine("[WebRTC] data channel ready");
            if (CurrentSession == null) return;

            CurrentSession.OnDataChannelReady -= CurrentSessionOnOnDataChannelReady;
            JsonWriter jw = new JsonWriter();
            jw.WriteObjectStart();
            jw.WritePropertyName("j");
            jw.WriteObjectStart();
            jw.WritePropertyName("p");
            jw.Write(true);
            jw.WriteObjectEnd();
            jw.WriteObjectEnd();
            Logger.Log($"Joining voice on {CurrentSession.SessionId} with {jw}", Helpers.LogLevel.Debug, Client);
            _ = CurrentSession.TrySendDataChannelString(jw.ToString());
            Console.WriteLine("[WebRTC] join sent");
            SendGlobalPosition();
        }

        // Allow callers to send arbitrary data channel messages
        public bool SendDataChannelMessage(string message)
        {
            if (CurrentSession == null) return false;
            return CurrentSession.TrySendDataChannelString(message);
        }

        // Public methods to set peer mute and gain
        public void SetPeerMute(UUID peerId, bool mute)
        {
            CurrentSession?.SetPeerMute(peerId, mute);
        }

        public void SetPeerGain(UUID peerId, int gain)
        {
            CurrentSession?.SetPeerGain(peerId, gain);
        }

        public async Task<bool> RequestParcelVoiceInfo()
        {
            var cap = Client.Network.CurrentSim.Caps?.CapabilityURI("ParcelVoiceInfoRequest");
            if (cap == null)
            {
                Logger.Log("ParcelVoiceInfoRequest capability not available", Helpers.LogLevel.Warning, Client);
                return false;
            }

            var tcs = new TaskCompletionSource<OSD>();
            Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, new OSD(), CancellationToken.None, (response, data, error) =>
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
                    if (CurrentSession != null)
                    {
                        CurrentSession.ChannelId = channelURI;
                        CurrentSession.ChannelCredentials = credentials;
                    }
                    _channelId = channelURI;
                    _channelCredentials = credentials;

                    OnParcelVoiceInfo?.Invoke(regionName, localID, channelURI);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to request parcel voice info: {ex.Message}", Helpers.LogLevel.Warning, Client);
            }
            return false;
        }

        private void RequiredVoiceVersionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            var msg = (RequiredVoiceVersionMessage)message;
            if (msg.MajorVersion != 2)
            {
                Logger.Log($"WebRTC voice version mismatch! Got {msg.MajorVersion}.{msg.MinorVersion}, expecting 1.x. Disabling WebRTC voice manager", Helpers.LogLevel.Error, Client);
                _enabled = false;
            }
            else
            {
                Logger.Log($"WebRTC voice version {msg.MajorVersion}.{msg.MinorVersion} supported", Helpers.LogLevel.Info, Client);
            }
        }
    }
}
