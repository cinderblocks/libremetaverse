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
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using SIPSorcery.Net;

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceManager
    {
        private readonly GridClient Client;
        private VoiceSession CurrentSession;

        public string sdpLocal => CurrentSession.SdpLocal;
        public string sdpRemote => CurrentSession.SdpRemote;
        public bool connected { get; private set; }

        private CancellationTokenSource Cts = new CancellationTokenSource();

        public VoiceManager(GridClient client)
        {
            Client = client;
            Client.Network.RegisterEventCallback("RequiredVoiceVersion", RequiredVoiceVersionEventHandler);
        }

        public bool ConnectPrimaryRegion()
        {
            if (!Client.Network.Connected) { return false; }

            CurrentSession = new VoiceSession(VoiceSession.ESessionType.LOCAL, Client);
            Task task = CurrentSession.RequestProvision();
            task.Wait();

            return true;
        }

        public void Disconnect()
        {
            CurrentSession.CloseSession();
        }
        private void RequiredVoiceVersionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            var msg = (RequiredVoiceVersionMessage)message;
            Logger.DebugLog($"Voice version {msg.MajorVersion}.{msg.MinorVersion} required", Client);
        }
    }
}
