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

using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// Throttles the network traffic for various different traffic types.
    /// Access this class through GridClient.Throttle
    /// </summary>
    public class AgentThrottle : ThrottleRates
    {
        private readonly GridClient? Client;

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
        public AgentThrottle(byte[] data, int pos) : base(data, pos)
        {
        }

        /// <summary>
        /// Send an AgentThrottle packet to the current server using the 
        /// current values
        /// </summary>
        public void Set()
        {
            var sim = Client?.Network?.CurrentSim;
            if (sim == null) return;
            Set(sim);
        }

        /// <summary>
        /// Send an AgentThrottle packet to the specified server using the
        /// current values
        /// </summary>
        public void Set(Simulator? simulator)
        {
            if (Client == null || simulator == null) return;

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

            // Synchronise the outgoing UDP throttle with the new rates.
            Client.Network.UpdateUdpThrottle(this);
        }
    }
}
