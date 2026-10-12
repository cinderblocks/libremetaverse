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

using System;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Packets
{
    public abstract partial class Packet
    {
        #region Serialization/Deserialization

        public static string ToXmlString(Packet packet)
        {
            return OSDParser.SerializeLLSDXmlString(GetLLSD(packet));
        }

        /// <summary>
        /// Serializes a packet to OSD. The returned map has the packet type name as its single
        /// key and the block data map as its value, matching the LL caps event envelope format.
        /// </summary>
        public static OSD GetLLSD(Packet packet)
        {
            var wrapper = new OSDMap(1);
            wrapper[packet.Type.ToString()] = packet.PacketToOSD();
            return wrapper;
        }

        public static byte[] ToBinary(Packet packet)
        {
            return OSDParser.SerializeLLSDBinary(GetLLSD(packet));
        }

        public static Packet? FromXmlString(string xml)
        {
            System.Xml.XmlTextReader reader =
                new System.Xml.XmlTextReader(new System.IO.MemoryStream(Utils.StringToBytes(xml)));

            return FromLLSD(OSDParser.DeserializeLLSDXml(reader));
        }

        public static Packet? FromBinary(byte[] binary)
        {
            return FromLLSD(OSDParser.DeserializeLLSDBinary(binary));
        }

        /// <summary>
        /// Reconstructs a <see cref="Packet"/> from OSD produced by <see cref="GetLLSD"/>.
        /// The OSD must be a map whose single key is the packet type name and whose value is
        /// the block data map.
        /// </summary>
        public static Packet? FromLLSD(OSD osd)
        {
            if (osd is not OSDMap wrapper) return null;

            foreach (var key in wrapper.Keys)
            {
                if (wrapper[key] is OSDMap body)
                    return BuildPacketFromOSD(key, body);
            }

            return null;
        }

        #endregion Serialization/Deserialization

        /// <summary>
        /// Attempts to convert an LLSD structure to a known Packet type
        /// </summary>
        /// <param name="capsEventName">Event name, this must match an actual
        /// packet name for a Packet to be successfully built</param>
        /// <param name="body">LLSD to convert to a Packet</param>
        /// <returns>A Packet on success, otherwise null</returns>
        public static Packet? BuildPacket(string capsEventName, OSDMap body)
        {
            return BuildPacketFromOSD(capsEventName, body);
        }
    }
}

