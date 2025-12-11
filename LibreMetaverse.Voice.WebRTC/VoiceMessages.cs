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

using OpenMetaverse.Interfaces;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Voice.WebRTC
{
    internal class LocalVoiceProvisionRequest : IMessage
    {
        public string Sdp;
        public int ParcelId = -1;

        public LocalVoiceProvisionRequest(string sdp)
        {
            Sdp = sdp;
        }

        public LocalVoiceProvisionRequest(string sdp, int parcelId)
        {
            Sdp = sdp;
            ParcelId = parcelId;
        }

        public OSDMap Serialize()
        {
            var map = new OSDMap(1);
            var jsep = new OSDMap(5)
            {
                { "type", "offer" },
                { "sdp", Sdp },
            };
            if (ParcelId > -1)
            {
                jsep["parcel_location_id"] = ParcelId;
            }
            map.Add("jsep", jsep);
            map.Add("channel_type", "local");
            map.Add("voice_server_type", "webrtc");

            return map;
        }

        public void Deserialize(OSDMap map)
        {
            var jsep = (OSDMap)map["jsep"];
            Sdp = jsep["sdp"].AsString();
            ParcelId = jsep["parcel_location_id"].AsInteger();
        }
    }

    internal class MultiAgentVoiceProvisionRequest : IMessage
    {
        public string Sdp;
        public string ChannelId;
        public string ChannelCredentials;

        public MultiAgentVoiceProvisionRequest(string sdp)
        {
            Sdp = sdp;
        }

        public OSDMap Serialize()
        {
            var map = new OSDMap(1);
            var jsep = new OSDMap(5)
            {
                { "type", "offer" },
                { "sdp", Sdp },
            };
            map.Add("jsep", jsep);
            map.Add("channel", ChannelId);
            map.Add("credentials", ChannelCredentials);
            map.Add("channel_type", "multiagent");
            map.Add("voice_server_type", "webrtc");


            return map;
        }

        public void Deserialize(OSDMap map)
        {
            var jesp = (OSDMap)map["jesp"];
            Sdp = jesp["sdp"].AsString();
            ChannelId = map["channel"].AsString();
            ChannelCredentials = map["credentials"].AsString();
        }
    }
}
