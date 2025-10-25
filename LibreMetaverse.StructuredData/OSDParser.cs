/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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
using System.IO;
using System.Text;

namespace OpenMetaverse.StructuredData
{
    public partial class OSDParser
    {
        private const string LLSD_BINARY_HEADER = "<? llsd/binary ?>";
        private const string LLSD_XML_HEADER = "<llsd>";
        private const string LLSD_XML_ALT_HEADER = "<?xml";
        private const string LLSD_XML_ALT2_HEADER = "<? llsd/xml ?>";

        public static OSD Deserialize(byte[] data)
        {
            string header = Encoding.ASCII.GetString(data, 0, data.Length >= 17 ? 17 : data.Length);

            try
            {
                string uHeader = Encoding.UTF8.GetString(data, 0, data.Length >= 17 ? 17 : data.Length).TrimStart();
                if (uHeader.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                    uHeader.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                    uHeader.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
                {
                    return DeserializeLLSDXml(data);
                }
            }
            catch { }

            if (header.StartsWith(LLSD_BINARY_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDBinary(data);
            }
            if (header.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                header.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                header.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }
            return DeserializeJson(Encoding.UTF8.GetString(data));
        }

        public static OSD Deserialize(string data)
        {
            if (data.StartsWith(LLSD_BINARY_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDBinary(Encoding.UTF8.GetBytes(data));
            }
            if (data.StartsWith(LLSD_XML_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.InvariantCultureIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.InvariantCultureIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }
            return DeserializeJson(data);
        }

        public static OSD Deserialize(Stream stream)
        {
            if (!stream.CanSeek) { throw new OSDException("Cannot deserialize structured data from unseekable streams"); }

            byte[] headerData = new byte[14];
            int read = stream.Read(headerData, 0, 14);
            if (read == 0) { throw new System.IO.EndOfStreamException(); }

            stream.Seek(0, SeekOrigin.Begin);
            string header = Encoding.ASCII.GetString(headerData);

            if (header.StartsWith(LLSD_BINARY_HEADER))
                return DeserializeLLSDBinary(stream);
            if (header.StartsWith(LLSD_XML_HEADER) || header.StartsWith(LLSD_XML_ALT_HEADER) || header.StartsWith(LLSD_XML_ALT2_HEADER))
                return DeserializeLLSDXml(stream);
            return DeserializeJson(stream);
        }
    }
}