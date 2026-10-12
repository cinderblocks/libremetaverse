/*
 * Copyright (c) 2026, Sjofn LLC.
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


using System.Collections.Generic;

namespace LibreMetaverse
{
    /// <summary>
    /// The sequence numbers of the reliable packets received lately, for noticing one that arrives twice. It holds the
    /// most recent <c>capacity - 1</c> of them and forgets the oldest first.
    /// </summary>
    public sealed class IncomingPacketIDCollection
    {
        private readonly uint[] _items;
        private readonly HashSet<uint> hashSet;
        private int first;
        private int next;
        private readonly int capacity;

        public IncomingPacketIDCollection(int capacity)
        {
            this.capacity = capacity;
            _items = new uint[capacity];
            hashSet = new HashSet<uint>();
        }

        public bool TryEnqueue(uint ack)
        {
            lock (hashSet)
            {
                if (hashSet.Add(ack))
                {
                    _items[next] = ack;
                    next = (next + 1) % capacity;
                    if (next == first)
                    {
                        hashSet.Remove(_items[first]);
                        first = (first + 1) % capacity;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
