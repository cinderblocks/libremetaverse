/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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

using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    /// <summary>
    /// Throttles the network traffic for various different traffic types.
    /// Access this class through GridClient.Throttle
    /// </summary>
    public class AgentThrottle
    {
        /// <summary>Maximum bits per second for resending unacknowledged packets</summary>
        public float Resend
        {
            get => resend;
            set
            {
                if (value > 150000.0f) resend = 150000.0f;
                else if (value < 10000.0f) resend = 10000.0f;
                else resend = value;
            }
        }
        /// <summary>Maximum bits per second for LayerData terrain</summary>
        public float Land
        {
            get => land;
            set
            {
                if (value > 170000.0f) land = 170000.0f;
                else if (value < 0.0f) land = 0.0f; // We don't have control of these so allow throttling to 0
                else land = value;
            }
        }
        /// <summary>Maximum bits per second for LayerData wind data</summary>
        public float Wind
        {
            get => wind;
            set
            {
                if (value > 34000.0f) wind = 34000.0f;
                else if (value < 0.0f) wind = 0.0f; // We don't have control of these so allow throttling to 0
                else wind = value;
            }
        }
        /// <summary>Maximum bits per second for LayerData clouds</summary>
        public float Cloud
        {
            get => cloud;
            set
            {
                if (value > 34000.0f) cloud = 34000.0f;
                else if (value < 0.0f) cloud = 0.0f; // We don't have control of these so allow throttling to 0
                else cloud = value;
            }
        }
        /// <summary>Unknown, includes object data</summary>
        public float Task
        {
            get => task;
            set
            {
                if (value > 446000.0f*3) task = 446000.0f*3;
                else if (value < 4000.0f) task = 4000.0f;
                else task = value;
            }
        }
        /// <summary>Maximum bits per second for textures</summary>
        public float Texture
        {
            get => texture;
            set
            {
                if (value > 446000.0f) texture = 446000.0f;
                else if (value < 4000.0f) texture = 4000.0f;
                else texture = value;
            }
        }
        /// <summary>Maximum bits per second for downloaded assets</summary>
        public float Asset
        {
            get => asset;
            set
            {
                if (value > 220000.0f) asset = 220000.0f;
                else if (value < 10000.0f) asset = 10000.0f;
                else asset = value;
            }
        }

        /// <summary>Maximum bits per second the entire connection, divided up
        /// between individual streams using default multipliers</summary>
        public float Total
        {
            get => Resend + Land + Wind + Cloud + Task + Texture + Asset;
            set
            {
                // Sane initial values
                Resend = (value * 0.1f);
                Land = (float)(value * 0.52f / 3f);
                Wind = (float)(value * 0.05f);
                Cloud = (float)(value * 0.05f);
                Task = (float)(value * 0.704f / 3f);
                Texture = (float)(value * 0.704f / 3f);
                Asset = (float)(value * 0.484f / 3f);
            }
        }

        private readonly GridClient Client;
        private float resend;
        private float land;
        private float wind;
        private float cloud;
        private float task;
        private float texture;
        private float asset;

        /// <summary>
        /// Default constructor, uses a default high total of 1500 KBps (1536000)
        /// </summary>
        public AgentThrottle(GridClient client)
        {
            Client = client;
            Total = 1536000.0f;
        }

        /// <summary>
        /// Constructor that decodes an existing AgentThrottle packet in to
        /// individual values
        /// </summary>
        /// <param name="data">Reference to the throttle data in an AgentThrottle
        /// packet</param>
        /// <param name="pos">Offset position to start reading at in the 
        /// throttle data</param>
        /// <remarks>This is generally not needed in clients as the server will
        /// never send a throttle packet to the client</remarks>
        public AgentThrottle(byte[] data, int pos)
        {
            // Decode 7 little-endian floats from the provided byte array
            Resend = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Land = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Wind = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Cloud = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Task = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Texture = Utils.ReadSingleLittleEndian(data, pos); pos += 4;
            Asset = Utils.ReadSingleLittleEndian(data, pos);
        }

        /// <summary>
        /// Send an AgentThrottle packet to the current server using the 
        /// current values
        /// </summary>
        public void Set()
        {
            Set(Client.Network.CurrentSim);
        }

        /// <summary>
        /// Send an AgentThrottle packet to the specified server using the 
        /// current values
        /// </summary>
        public void Set(Simulator simulator)
        {
            AgentThrottlePacket throttle = new AgentThrottlePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    CircuitCode = Client.Network.CircuitCode
                },
                Throttle =
                {
                    GenCounter = 0,
                    Throttles = ToBytes()
                }
            };

            Client.Network.SendPacket(throttle, simulator);
        }

        /// <summary>
        /// Convert the current throttle values to a byte array that can be put
        /// in an AgentThrottle packet
        /// </summary>
        /// <returns>Byte array containing all the throttle values</returns>
        public byte[] ToBytes()
        {
            var data = new byte[7 * 4];
            int i = 0;

            Utils.WriteSingleLittleEndian(data, i, Resend); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Land); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Wind); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Cloud); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Task); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Texture); i += 4;
            Utils.WriteSingleLittleEndian(data, i, Asset); i += 4;

            return data;
        }
    }
}
