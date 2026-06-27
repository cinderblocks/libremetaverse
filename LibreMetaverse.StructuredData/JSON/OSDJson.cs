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

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LibreMetaverse.StructuredData
{
    public static partial class OSDParser
    {
        public static OSD DeserializeJson(Stream json)
        {
            using var doc = JsonDocument.Parse(json);
            return ConvertElement(doc.RootElement);
        }

        public static OSD DeserializeJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return ConvertElement(doc.RootElement);
        }

        private static OSD ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return OSD.FromBoolean(true);
                case JsonValueKind.False:
                    return OSD.FromBoolean(false);
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i)) return OSD.FromInteger(i);
                    if (element.TryGetInt64(out var l)) return OSD.FromLong(l);
                    return OSD.FromReal(element.GetDouble());
                case JsonValueKind.String:
                    string? s = element.GetString();
                    return string.IsNullOrEmpty(s) ? new OSD() : OSD.FromString(s!);
                case JsonValueKind.Array:
                    var arr = new OSDArray(element.GetArrayLength());
                    foreach (var child in element.EnumerateArray())
                        arr.Add(ConvertElement(child));
                    return arr;
                case JsonValueKind.Object:
                    var map = new OSDMap();
                    foreach (var prop in element.EnumerateObject())
                        map.Add(prop.Name, ConvertElement(prop.Value));
                    return map;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    return new OSD();
            }
        }

        public static string SerializeJsonString(OSD osd, bool preserveDefaults = false)
        {
            var ms = new MemoryStream();
            var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { SkipValidation = true });
            if (preserveDefaults)
                WriteJsonWithDefaults(osd, writer);
            else
                WriteJsonNoDefaultsRoot(osd, writer);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        private static void WriteJsonWithDefaults(OSD osd, Utf8JsonWriter writer)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    writer.WriteBooleanValue(osd.AsBoolean());
                    break;
                case OSDType.Integer:
                    writer.WriteNumberValue(osd.AsInteger());
                    break;
                case OSDType.Real:
                    writer.WriteNumberValue(osd.AsReal());
                    break;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                case OSDType.UUID:
                    writer.WriteStringValue(osd.AsString());
                    break;
                case OSDType.Binary:
                    {
                        byte[] binary = osd.AsBinary();
                        writer.WriteStartArray();
                        foreach (byte b in binary)
                            writer.WriteNumberValue(b);
                        writer.WriteEndArray();
                    }
                    break;
                case OSDType.Array:
                    writer.WriteStartArray();
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        WriteJsonWithDefaults(t, writer);
                    writer.WriteEndArray();
                    break;
                case OSDType.Map:
                    writer.WriteStartObject();
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        writer.WritePropertyName(kvp.Key);
                        WriteJsonWithDefaults(kvp.Value, writer);
                    }
                    writer.WriteEndObject();
                    break;
                case OSDType.Unknown:
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static void WriteJsonNoDefaultsRoot(OSD osd, Utf8JsonWriter writer)
        {
            if (osd == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (osd.Type)
            {
                case OSDType.Boolean:
                    if (!osd.AsBoolean()) writer.WriteNullValue(); else writer.WriteBooleanValue(true);
                    break;
                case OSDType.Integer:
                    if (osd.AsInteger() == 0) writer.WriteNullValue(); else writer.WriteNumberValue(osd.AsInteger());
                    break;
                case OSDType.Real:
                    if (osd.AsReal() == 0.0d) writer.WriteNullValue(); else writer.WriteNumberValue(osd.AsReal());
                    break;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    string s = osd.AsString();
                    if (string.IsNullOrEmpty(s)) writer.WriteNullValue(); else writer.WriteStringValue(s);
                    break;
                case OSDType.UUID:
                    UUID uuid = osd.AsUUID();
                    if (uuid == UUID.Zero) writer.WriteNullValue(); else writer.WriteStringValue(uuid.ToString());
                    break;
                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    if (binary == Utils.EmptyBytes) writer.WriteNullValue();
                    else
                    {
                        writer.WriteStartArray();
                        foreach (byte b in binary) writer.WriteNumberValue(b);
                        writer.WriteEndArray();
                    }
                    break;
                case OSDType.Array:
                    writer.WriteStartArray();
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                    {
                        if (!WriteJsonNoDefaultsElement(t, writer))
                            writer.WriteNullValue();
                    }
                    writer.WriteEndArray();
                    break;
                case OSDType.Map:
                    writer.WriteStartObject();
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        if (ShouldSerializeNoDefaults(kvp.Value))
                        {
                            writer.WritePropertyName(kvp.Key);
                            WriteJsonNoDefaultsRoot(kvp.Value, writer);
                        }
                    }
                    writer.WriteEndObject();
                    break;
                case OSDType.Unknown:
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static bool WriteJsonNoDefaultsElement(OSD osd, Utf8JsonWriter writer)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    if (!osd.AsBoolean()) return false;
                    writer.WriteBooleanValue(true);
                    return true;
                case OSDType.Integer:
                    if (osd.AsInteger() == 0) return false;
                    writer.WriteNumberValue(osd.AsInteger());
                    return true;
                case OSDType.Real:
                    if (osd.AsReal() == 0.0d) return false;
                    writer.WriteNumberValue(osd.AsReal());
                    return true;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    string s = osd.AsString();
                    if (string.IsNullOrEmpty(s)) return false;
                    writer.WriteStringValue(s);
                    return true;
                case OSDType.UUID:
                    UUID uuid = osd.AsUUID();
                    if (uuid == UUID.Zero) return false;
                    writer.WriteStringValue(uuid.ToString());
                    return true;
                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    if (binary == Utils.EmptyBytes) return false;
                    writer.WriteStartArray();
                    foreach (byte b in binary) writer.WriteNumberValue(b);
                    writer.WriteEndArray();
                    return true;
                case OSDType.Array:
                    writer.WriteStartArray();
                    OSDArray arr = (OSDArray)osd;
                    foreach (OSD t in arr)
                    {
                        if (!WriteJsonNoDefaultsElement(t, writer))
                            writer.WriteNullValue();
                    }
                    writer.WriteEndArray();
                    return true;
                case OSDType.Map:
                    writer.WriteStartObject();
                    OSDMap m = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in m)
                    {
                        if (ShouldSerializeNoDefaults(kvp.Value))
                        {
                            writer.WritePropertyName(kvp.Key);
                            WriteJsonNoDefaultsRoot(kvp.Value, writer);
                        }
                    }
                    writer.WriteEndObject();
                    return true;
                case OSDType.Unknown:
                default:
                    return false;
            }
        }

        private static bool ShouldSerializeNoDefaults(OSD osd)
        {
            if (osd == null) { return false; }

            switch (osd.Type)
            {
                case OSDType.Boolean:
                    return osd.AsBoolean();
                case OSDType.Integer:
                    return osd.AsInteger() != 0;
                case OSDType.Real:
                    return osd.AsReal() != 0.0d;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    return !string.IsNullOrEmpty(osd.AsString());
                case OSDType.UUID:
                    return osd.AsUUID() != UUID.Zero;
                case OSDType.Binary:
                    return osd.AsBinary() != Utils.EmptyBytes;
                case OSDType.Array:
                    return true;
                case OSDType.Map:
                    return true;
                case OSDType.Unknown:
                default:
                    return false;
            }
        }
    }
}
