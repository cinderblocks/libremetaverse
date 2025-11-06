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

        private const int HEADER_PROBE_LENGTH = 17;

        public static OSD Deserialize(byte[] data)
        {
            int probeLen = Math.Min(data.Length, HEADER_PROBE_LENGTH);
            string headerAscii = Encoding.ASCII.GetString(data, 0, probeLen);

            // Probe for XML-ish headers using UTF8 to handle BOMs and whitespace
            string headerUtf8 = Encoding.UTF8.GetString(data, 0, probeLen).TrimStart();
            if (headerUtf8.StartsWith(LLSD_XML_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerUtf8.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerUtf8.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }

            if (headerAscii.StartsWith(LLSD_BINARY_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDBinary(data);
            }

            if (headerAscii.StartsWith(LLSD_XML_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerAscii.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerAscii.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }

            return DeserializeJson(Encoding.UTF8.GetString(data));
        }

        public static OSD Deserialize(string data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (data.StartsWith(LLSD_BINARY_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDBinary(Encoding.UTF8.GetBytes(data));
            }
            if (data.StartsWith(LLSD_XML_HEADER, StringComparison.OrdinalIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.OrdinalIgnoreCase) ||
                data.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDXml(data);
            }
            return DeserializeJson(data);
        }

        public static OSD Deserialize(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek) { throw new OSDException("Cannot deserialize structured data from unseekable streams"); }

            byte[] headerData = new byte[HEADER_PROBE_LENGTH];
            int read = stream.Read(headerData, 0, HEADER_PROBE_LENGTH);
            if (read == 0) { throw new EndOfStreamException(); }

            stream.Seek(0, SeekOrigin.Begin);

            string headerAscii = Encoding.ASCII.GetString(headerData, 0, read);
            string headerUtf8 = Encoding.UTF8.GetString(headerData, 0, read).TrimStart();

            if (headerUtf8.StartsWith(LLSD_XML_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerUtf8.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.OrdinalIgnoreCase) ||
                headerUtf8.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return DeserializeLLSDXml(stream);
            }

            if (headerAscii.StartsWith(LLSD_BINARY_HEADER, StringComparison.OrdinalIgnoreCase))
                return DeserializeLLSDBinary(stream);
            if (headerAscii.StartsWith(LLSD_XML_HEADER, StringComparison.OrdinalIgnoreCase) || headerAscii.StartsWith(LLSD_XML_ALT_HEADER, StringComparison.OrdinalIgnoreCase) || headerAscii.StartsWith(LLSD_XML_ALT2_HEADER, StringComparison.OrdinalIgnoreCase))
                return DeserializeLLSDXml(stream);
            return DeserializeJson(stream);
        }
    }
}