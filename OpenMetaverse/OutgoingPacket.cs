/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using OpenMetaverse;
using System.Net;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    /// <summary>
    /// Holds a simulator client reference and a serialized packet, 
    /// along with some network statistics and related info.
    /// </summary>
    public sealed class OutgoingPacket
    {
        /// <summary>Reference to the simulator this packet is destined for</summary>
        public readonly object Client;
        /// <summary>Packet that needs to be sent</summary>
        public readonly UDPPacketBuffer Buffer;
        /// <summary>Sequence number of the wrapped packet</summary>
        public uint SequenceNumber;
        /// <summary>Number of times this packet has been resent</summary>
        public int ResendCount;
        /// <summary>Environment.TickCount when this packet was last sent over the wire</summary>
        public int TickCount;
        /// <summary>Type of the packet</summary>
        public PacketType Type;
        /// <summary>
        /// This is a caller-managed field representing the number of bytes to use/used in Buffer
        /// </summary>
        public int DataSize;

        /// <summary>
        /// Reference count to be used on this packet to know when it's safe to return to the pool
        /// </summary>
        private int _refCount;
        /// <summary>
        /// Lock to protect the reference count
        /// </summary>
        private object _refCountLock = new object();

        /// <summary>Category this packet belongs to</summary>
        public int Category;

        private bool _fromPool = false;
        public bool BufferFromPool
        {
            get { return _fromPool; }
        }

        public OutgoingPacket(Simulator simulator, UDPPacketBuffer buffer, PacketType type)
        {
            Client = simulator;
            Buffer = buffer;
            this.Type = type;
        }

        public Simulator Simulator
        {
            get { return Client is Simulator ? Client as Simulator : null; }
        }

        /// <summary>
        /// The endpoint this data is headed for
        /// </summary>
        public IPEndPoint Destination
        {
            get { return Buffer.RemoteEndPoint is IPEndPoint ? Buffer.RemoteEndPoint as IPEndPoint : null; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public OutgoingPacket(object client, byte[] buffer, int category, int dataSize, IPEndPoint destination,
            bool fromBufferPool, Packets.PacketType type)
        {
            SequenceNumber = 0;
            ResendCount = 0;
            TickCount = 0;

            Client = client;
            Category = category;
            _fromPool = fromBufferPool;
            Type = type;
            DataSize = dataSize;
            Buffer = new UDPPacketBuffer(buffer, dataSize, destination, category,  fromBufferPool);
        }

        public void AddRef()
        {
            if (BufferFromPool)
            {
                lock (_refCountLock)
                {
                    ++_refCount;
                }
            }
        }

        public void DecRef(Interfaces.IByteBufferPool returnPool)
        {
            if (BufferFromPool)
            {
                bool returnToBuffer = false;
                lock (_refCountLock)
                {
                    if (--_refCount <= 0)
                    {
                        returnToBuffer = true;
                    }
                }

                if (returnToBuffer)
                {
                    Array data = Buffer.Data;
                    returnPool.ReturnBytes(Buffer.Data);
                }
            }
        }
    }
}
