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

using System.Threading.Tasks;
using LitJson;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceManager
    {
        private readonly GridClient Client;
        private readonly Sdl2Audio AudioDevice;
        private VoiceSession CurrentSession;

        public string sdpLocal => CurrentSession.SdpLocal;
        public string sdpRemote => CurrentSession.SdpRemote;
        public bool connected => CurrentSession.Connected;

        public VoiceManager(GridClient client)
        {
            Client = client;
            AudioDevice = new Sdl2Audio();
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);
        }

        public async Task<bool> ConnectPrimaryRegion()
        {
            if (!Client.Network.Connected) { return false; }

            CurrentSession = new VoiceSession(AudioDevice, VoiceSession.ESessionType.LOCAL, Client);
            CurrentSession.OnDataChannelReady += CurrentSessionOnOnDataChannelReady;

            CurrentSession.Start();
            var provisioned = await CurrentSession.RequestProvision();
            return provisioned;
        }

        public void Disconnect()
        {
            CurrentSession.CloseSession();
        }

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
            Logger.DebugLog($"Sending position with {jw}");
            CurrentSession.DataChannel.send(jw.ToString());
            
        }

        private void CurrentSessionOnOnDataChannelReady()
        {
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
            CurrentSession.DataChannel.send(jw.ToString());
            SendGlobalPosition();
        }

        private void RequiredVoiceVersionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            var msg = (RequiredVoiceVersionMessage)message;
            Logger.DebugLog($"Voice version {msg.MajorVersion}.{msg.MinorVersion} required", Client);
        }
    }
}
