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

using System;
using System.IO;

namespace OpenMetaverse.StructuredData
{
    /// <summary>
    /// Protobuf serialization for OSD
    /// </summary>
    public static partial class OSDParser
    {
        private const string PROTOBUF_HEADER = "<? llsd/protobuf ?>";
        private static readonly byte[] ProtobufHeaderBytes = System.Text.Encoding.ASCII.GetBytes(PROTOBUF_HEADER);

        // Protobuf wire type constants
        private const byte WIRE_TYPE_VARINT = 0;
        private const byte WIRE_TYPE_FIXED64 = 1;
        private const byte WIRE_TYPE_LENGTH_DELIMITED = 2;
        private const byte WIRE_TYPE_FIXED32 = 5;

        // Field number assignments for OSD protobuf schema
        private const int FIELD_TYPE = 1;
        private const int FIELD_BOOLEAN = 2;
        private const int FIELD_INTEGER = 3;
        private const int FIELD_REAL = 4;
        private const int FIELD_STRING = 5;
        private const int FIELD_UUID = 6;
        private const int FIELD_DATE = 7;
        private const int FIELD_URI = 8;
        private const int FIELD_BINARY = 9;
        private const int FIELD_MAP_ENTRIES = 10;
        private const int FIELD_ARRAY_ELEMENTS = 11;
        private const int FIELD_MAP_KEY = 1;
        private const int FIELD_MAP_VALUE = 2;

        /// <summary>
        /// Deserializes OSD from protobuf format
        /// </summary>
        /// <param name="data">Byte array containing protobuf-encoded OSD</param>
        /// <returns>Deserialized OSD object</returns>
        public static OSD DeserializeLLSDProtobuf(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return DeserializeLLSDProtobuf(stream);
            }
        }

        /// <summary>
        /// Deserializes OSD from a protobuf stream
        /// </summary>
        /// <param name="stream">Stream containing protobuf-encoded OSD</param>
        /// <returns>Deserialized OSD object</returns>
        public static OSD DeserializeLLSDProtobuf(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            // Skip header if present
            byte[] headerCheck = new byte[ProtobufHeaderBytes.Length + 1]; // +1 for potential newline
            int bytesRead = stream.Read(headerCheck, 0, headerCheck.Length);

            bool hasHeader = bytesRead >= ProtobufHeaderBytes.Length;
            if (hasHeader)
            {
                for (int i = 0; i < ProtobufHeaderBytes.Length; i++)
                {
                    if (headerCheck[i] != ProtobufHeaderBytes[i])
                    {
                        hasHeader = false;
                        break;
                    }
                }
            }

            if (hasHeader)
            {
                // Skip the newline if present
                if (bytesRead > ProtobufHeaderBytes.Length && headerCheck[ProtobufHeaderBytes.Length] == (byte)'\n')
                {
                    // Header + newline found, continue from current position
                }
                else
                {
                    // Just header found, rewind one byte
                    stream.Position = ProtobufHeaderBytes.Length;
                }
            }
            else
            {
                // No header, rewind to start
                stream.Position = 0;
            }

            return ReadProtobufValue(stream);
        }

        /// <summary>
        /// Serializes an OSD object to protobuf format
        /// </summary>
        /// <param name="osd">OSD object to serialize</param>
        /// <param name="prependHeader">Whether to prepend the LLSD header</param>
        /// <returns>Byte array containing protobuf-encoded data</returns>
        public static byte[] SerializeLLSDProtobuf(OSD osd, bool prependHeader = true)
        {
            using (var stream = new MemoryStream())
            {
                if (prependHeader)
                {
                    stream.Write(ProtobufHeaderBytes, 0, ProtobufHeaderBytes.Length);
                    stream.WriteByte((byte)'\n');
                }

                WriteProtobufValue(stream, osd);
                return stream.ToArray();
            }
        }

        private static OSD ReadProtobufValue(Stream stream)
        {
            OSDType type = OSDType.Unknown;
            object? value = null;
            OSDMap? mapEntries = null;
            OSDArray? arrayElements = null;
            long startPos = stream.Position;
            long endPos = stream.Length;

            // If we're reading from a sub-stream with specific length, respect that
            if (stream is MemoryStream ms && ms.Capacity > 0)
            {
                endPos = stream.Length;
            }

            while (stream.Position < endPos)
            {
                ulong tag = ReadVarint(stream);
                int fieldNumber = (int)(tag >> 3);
                int wireType = (int)(tag & 0x7);

                switch (fieldNumber)
                {
                    case FIELD_TYPE:
                        if (wireType != WIRE_TYPE_VARINT)
                            throw new OSDException($"Expected varint wire type for type field, got {wireType}");
                        type = (OSDType)ReadVarint(stream);
                        break;
                    case FIELD_BOOLEAN:
                        if (wireType != WIRE_TYPE_VARINT)
                            throw new OSDException($"Expected varint wire type for boolean field, got {wireType}");
                        value = ReadVarint(stream) != 0;
                        break;
                    case FIELD_INTEGER:
                        if (wireType != WIRE_TYPE_VARINT)
                            throw new OSDException($"Expected varint wire type for integer field, got {wireType}");
                        value = (int)ReadSignedVarint(stream);
                        break;
                    case FIELD_REAL:
                        if (wireType != WIRE_TYPE_FIXED64)
                            throw new OSDException($"Expected fixed64 wire type for real field, got {wireType}");
                        value = ReadDouble(stream);
                        break;
                    case FIELD_STRING:
                    case FIELD_URI:
                        if (wireType != WIRE_TYPE_LENGTH_DELIMITED)
                            throw new OSDException($"Expected length-delimited wire type for string/uri field, got {wireType}");
                        value = ReadString(stream);
                        break;
                    case FIELD_UUID:
                        if (wireType != WIRE_TYPE_LENGTH_DELIMITED)
                            throw new OSDException($"Expected length-delimited wire type for UUID field, got {wireType}");
                        int uuidLen = (int)ReadVarint(stream);
                        if (uuidLen != 16)
                            throw new OSDException($"Expected 16 bytes for UUID, got {uuidLen}");
                        value = new UUID(ReadBytes(stream, 16), 0);
                        break;
                    case FIELD_DATE:
                        if (wireType != WIRE_TYPE_FIXED64)
                            throw new OSDException($"Expected fixed64 wire type for date field, got {wireType}");
                        double timestamp = ReadDouble(stream);
                        value = Utils.UnixTimeToDateTime((uint)timestamp);
                        break;
                    case FIELD_BINARY:
                        if (wireType != WIRE_TYPE_LENGTH_DELIMITED)
                            throw new OSDException($"Expected length-delimited wire type for binary field, got {wireType}");
                        value = ReadLengthDelimited(stream);
                        break;
                    case FIELD_MAP_ENTRIES:
                        if (wireType != WIRE_TYPE_LENGTH_DELIMITED)
                            throw new OSDException($"Expected length-delimited wire type for map entry field, got {wireType}");
                        if (mapEntries == null) mapEntries = new OSDMap();
                        ReadMapEntry(stream, mapEntries);
                        break;
                    case FIELD_ARRAY_ELEMENTS:
                        if (wireType != WIRE_TYPE_LENGTH_DELIMITED)
                            throw new OSDException($"Expected length-delimited wire type for array element field, got {wireType}");
                        if (arrayElements == null) arrayElements = new OSDArray();
                        int elementLength = (int)ReadVarint(stream);
                        byte[] elementBytes = ReadBytes(stream, elementLength);
                        using (var elementStream = new MemoryStream(elementBytes))
                        {
                            arrayElements.Add(ReadProtobufValue(elementStream));
                        }
                        break;
                    default:
                        // Skip unknown fields
                        SkipField(stream, wireType);
                        break;
                }
            }

            // Construct the appropriate OSD type based on what was read
            switch (type)
            {
                case OSDType.Boolean:
                    return value != null ? OSD.FromBoolean((bool)value) : new OSD();
                case OSDType.Integer:
                    return value != null ? OSD.FromInteger((int)value) : new OSD();
                case OSDType.Real:
                    return value != null ? OSD.FromReal((double)value) : new OSD();
                case OSDType.String:
                    return value != null ? OSD.FromString((string)value) : OSD.FromString(string.Empty);
                case OSDType.UUID:
                    return value != null ? OSD.FromUUID((UUID)value) : OSD.FromUUID(UUID.Zero);
                case OSDType.Date:
                    return value != null ? OSD.FromDate((DateTime)value) : OSD.FromDate(Utils.Epoch);
                case OSDType.URI:
                    return value != null ? OSD.FromUri(new Uri((string)value, UriKind.RelativeOrAbsolute)) : OSD.FromUri(new Uri(string.Empty, UriKind.RelativeOrAbsolute));
                case OSDType.Binary:
                    return value != null ? OSD.FromBinary((byte[])value) : OSD.FromBinary(Utils.EmptyBytes);
                case OSDType.Map:
                    return mapEntries ?? new OSDMap();
                case OSDType.Array:
                    return arrayElements ?? new OSDArray();
                default:
                    return new OSD();
            }
        }

        private static void WriteProtobufValue(Stream stream, OSD osd)
        {
            // Write type field
            WriteVarint(stream, MakeTag(FIELD_TYPE, WIRE_TYPE_VARINT));
            WriteVarint(stream, (ulong)osd.Type);

            switch (osd.Type)
            {
                case OSDType.Boolean:
                    WriteVarint(stream, MakeTag(FIELD_BOOLEAN, WIRE_TYPE_VARINT));
                    WriteVarint(stream, osd.AsBoolean() ? 1UL : 0UL);
                    break;

                case OSDType.Integer:
                    WriteVarint(stream, MakeTag(FIELD_INTEGER, WIRE_TYPE_VARINT));
                    WriteSignedVarint(stream, osd.AsInteger());
                    break;

                case OSDType.Real:
                    WriteVarint(stream, MakeTag(FIELD_REAL, WIRE_TYPE_FIXED64));
                    WriteDouble(stream, osd.AsReal());
                    break;

                case OSDType.String:
                    WriteVarint(stream, MakeTag(FIELD_STRING, WIRE_TYPE_LENGTH_DELIMITED));
                    WriteString(stream, osd.AsString());
                    break;

                case OSDType.UUID:
                    WriteVarint(stream, MakeTag(FIELD_UUID, WIRE_TYPE_LENGTH_DELIMITED));
                    byte[] uuidBytes = osd.AsUUID().GetBytes();
                    WriteVarint(stream, (ulong)uuidBytes.Length);
                    stream.Write(uuidBytes, 0, uuidBytes.Length);
                    break;

                case OSDType.Date:
                    WriteVarint(stream, MakeTag(FIELD_DATE, WIRE_TYPE_FIXED64));
                    WriteDouble(stream, Utils.DateTimeToUnixTime(osd.AsDate()));
                    break;

                case OSDType.URI:
                    WriteVarint(stream, MakeTag(FIELD_URI, WIRE_TYPE_LENGTH_DELIMITED));
                    WriteString(stream, osd.AsString());
                    break;

                case OSDType.Binary:
                    WriteVarint(stream, MakeTag(FIELD_BINARY, WIRE_TYPE_LENGTH_DELIMITED));
                    byte[] binary = osd.AsBinary();
                    WriteVarint(stream, (ulong)binary.Length);
                    stream.Write(binary, 0, binary.Length);
                    break;

                case OSDType.Map:
                    OSDMap map = (OSDMap)osd;
                    foreach (System.Collections.Generic.KeyValuePair<string, OSD> kvp in map)
                    {
                        WriteVarint(stream, MakeTag(FIELD_MAP_ENTRIES, WIRE_TYPE_LENGTH_DELIMITED));
                        using (var entryStream = new MemoryStream())
                        {
                            WriteMapEntry(entryStream, kvp.Key, kvp.Value);
                            byte[] entryBytes = entryStream.ToArray();
                            WriteVarint(stream, (ulong)entryBytes.Length);
                            stream.Write(entryBytes, 0, entryBytes.Length);
                        }
                    }
                    break;

                case OSDType.Array:
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD element in array)
                    {
                        WriteVarint(stream, MakeTag(FIELD_ARRAY_ELEMENTS, WIRE_TYPE_LENGTH_DELIMITED));
                        using (var elementStream = new MemoryStream())
                        {
                            WriteProtobufValue(elementStream, element);
                            byte[] elementBytes = elementStream.ToArray();
                            WriteVarint(stream, (ulong)elementBytes.Length);
                            stream.Write(elementBytes, 0, elementBytes.Length);
                        }
                    }
                    break;
            }
        }

        private static void ReadMapEntry(Stream stream, OSDMap map)
        {
            int entryLength = (int)ReadVarint(stream);
            using (var entryStream = new MemoryStream(ReadBytes(stream, entryLength)))
            {
                string? key = null;
                OSD? value = null;

                while (entryStream.Position < entryStream.Length)
                {
                    ulong tag = ReadVarint(entryStream);
                    int fieldNumber = (int)(tag >> 3);

                    if (fieldNumber == FIELD_MAP_KEY)
                    {
                        key = ReadString(entryStream);
                    }
                    else if (fieldNumber == FIELD_MAP_VALUE)
                    {
                        int valueLength = (int)ReadVarint(entryStream);
                        using (var valueStream = new MemoryStream(ReadBytes(entryStream, valueLength)))
                        {
                            value = ReadProtobufValue(valueStream);
                        }
                    }
                }

                if (key != null && value != null)
                {
                    map[key] = value;
                }
            }
        }

        private static void WriteMapEntry(Stream stream, string key, OSD value)
        {
            WriteVarint(stream, MakeTag(FIELD_MAP_KEY, WIRE_TYPE_LENGTH_DELIMITED));
            WriteString(stream, key);

            WriteVarint(stream, MakeTag(FIELD_MAP_VALUE, WIRE_TYPE_LENGTH_DELIMITED));
            using (var valueStream = new MemoryStream())
            {
                WriteProtobufValue(valueStream, value);
                byte[] valueBytes = valueStream.ToArray();
                WriteVarint(stream, (ulong)valueBytes.Length);
                stream.Write(valueBytes, 0, valueBytes.Length);
            }
        }

        #region Protobuf Encoding/Decoding Primitives

        private static ulong MakeTag(int fieldNumber, int wireType)
        {
            return (((ulong)(uint)fieldNumber) << 3) | (ulong)(uint)wireType;
        }

        private static ulong ReadVarint(Stream stream)
        {
            ulong result = 0;
            int shift = 0;

            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0)
                    break;

                shift += 7;
            }

            return result;
        }

        private static void WriteVarint(Stream stream, ulong value)
        {
            while (value > 0x7F)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        private static long ReadSignedVarint(Stream stream)
        {
            ulong value = ReadVarint(stream);
            // ZigZag decode
            return (long)(value >> 1) ^ -(long)(value & 1);
        }

        private static void WriteSignedVarint(Stream stream, int value)
        {
            // ZigZag encode
            ulong zigzag = (ulong)((value << 1) ^ (value >> 31));
            WriteVarint(stream, zigzag);
        }

        private static double ReadDouble(Stream stream)
        {
            byte[] buffer = new byte[8];
            if (stream.Read(buffer, 0, 8) != 8)
                throw new EndOfStreamException();
            return BitConverter.ToDouble(buffer, 0);
        }

        private static void WriteDouble(Stream stream, double value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static string ReadString(Stream stream)
        {
            int length = (int)ReadVarint(stream);
            byte[] buffer = new byte[length];
            if (stream.Read(buffer, 0, length) != length)
                throw new EndOfStreamException();
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        private static void WriteString(Stream stream, string value)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(value);
            WriteVarint(stream, (ulong)buffer.Length);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static byte[] ReadLengthDelimited(Stream stream)
        {
            int length = (int)ReadVarint(stream);
            return ReadBytes(stream, length);
        }

        private static byte[] ReadBytes(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (stream.Read(buffer, 0, count) != count)
                throw new EndOfStreamException();
            return buffer;
        }

        private static void SkipField(Stream stream, int wireType)
        {
            switch (wireType)
            {
                case WIRE_TYPE_VARINT:
                    ReadVarint(stream);
                    break;
                case WIRE_TYPE_FIXED64:
                    stream.Seek(8, SeekOrigin.Current);
                    break;
                case WIRE_TYPE_LENGTH_DELIMITED:
                    int length = (int)ReadVarint(stream);
                    stream.Seek(length, SeekOrigin.Current);
                    break;
                case WIRE_TYPE_FIXED32:
                    stream.Seek(4, SeekOrigin.Current);
                    break;
                default:
                    throw new OSDException($"Unknown wire type: {wireType}");
            }
        }

        #endregion
    }
}
