/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.Linq;

namespace OpenMetaverse.Stats
{
    public enum Type
    {
        Packet,
        Message
    }

    public class UtilizationStatistics
    {
        public class Stat
        {
            public Type Type { get; set; }
            public long TxCount { get; set; }
            public long RxCount { get; set; }
            public long TxBytes { get; set; }
            public long RxBytes { get; set; }
        }

        private readonly Dictionary<string, Stat> m_StatsCollection;

        public UtilizationStatistics()
        {
            m_StatsCollection = new Dictionary<string, Stat>();
        }

        internal void Update(string key, Type type, long txBytes, long rxBytes)
        {
            lock (m_StatsCollection)
            {
                Stat stat;

                if (m_StatsCollection.ContainsKey(key))
                {
                    stat = m_StatsCollection[key];
                }
                else
                {
                    stat = new Stat()
                    {
                        Type = type
                    };
                    m_StatsCollection.Add(key, stat);
                }

                if (rxBytes > 0)
                {
                    stat.RxCount += 1;
                    stat.RxBytes += rxBytes;
                }

                if (txBytes > 0)
                {
                    stat.TxCount += 1;
                    stat.TxBytes += txBytes;
                }
            }
        }

        public Dictionary<string, Stat> GetStatistics()
        {
            lock (m_StatsCollection)
            {
                return m_StatsCollection.ToDictionary(
                    e => e.Key,
                    e => new Stat()
                    {
                        Type = e.Value.Type,
                        RxBytes = e.Value.RxBytes,
                        RxCount = e.Value.RxCount,
                        TxBytes = e.Value.TxBytes,
                        TxCount = e.Value.TxCount
                    });
            }
        }
    }
}