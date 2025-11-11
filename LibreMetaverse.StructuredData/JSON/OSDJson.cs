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

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using LitJson;

namespace OpenMetaverse.StructuredData
{
    public static partial class OSDParser
    {
        public static OSD DeserializeJson(Stream json)
        {
            using (StreamReader streamReader = new StreamReader(json))
            {
                JsonReader reader = new JsonReader(streamReader);
                return DeserializeJson(reader);
            }
        }

        public static OSD DeserializeJson(string json)
        {
            JsonReader reader = new JsonReader(json);
            return DeserializeJson(reader);
        }

        // Streaming reader-based deserialize to avoid creating JsonData trees
        private static OSD DeserializeJson(JsonReader reader)
        {
            if (reader == null) { throw new OSDException("Json reader is null"); }

            return !reader.Read() ? new OSD() : ReadJsonValue(reader);
        }

        private static OSD ReadJsonValue(JsonReader reader)
        {
            switch (reader.Token)
            {
                case JsonToken.ArrayStart:
                    {
                        OSDArray array = new OSDArray();
                        while (reader.Read())
                        {
                            if (reader.Token == JsonToken.ArrayEnd) { break; }
                            array.Add(ReadJsonValue(reader));
                        }
                        return array;
                    }
                case JsonToken.ObjectStart:
                    {
                        OSDMap map = new OSDMap();

                        while (reader.Read())
                        {
                            if (reader.Token == JsonToken.ObjectEnd) { break; }

                            if (!(reader.Value is string propName))
                            {
                                throw new OSDException("Expected property name in JSON object");
                            }

                            // Read the value token for this property
                            if (!reader.Read())
                            {
                                throw new OSDException("Unexpected end of JSON while reading object property value");
                            }
                            map.Add(propName, ReadJsonValue(reader));
                        }

                        return map;
                    }
                case JsonToken.String:
                    {
                        string s = reader.Value as string;
                        return string.IsNullOrEmpty(s) ? new OSD() : OSD.FromString(s);
                    }
                case JsonToken.Double:
                    return OSD.FromReal((double)reader.Value);
                case JsonToken.Int:
                    return OSD.FromInteger((int)reader.Value);
                case JsonToken.Long:
                    return OSD.FromLong((long)reader.Value);
                case JsonToken.Boolean:
                    return OSD.FromBoolean((bool)reader.Value);
                case JsonToken.Null:
                case JsonToken.None:
                    return new OSD();
                default:
                    // For any other token types return empty OSD
                    return new OSD();
            }
        }

        public static OSD DeserializeJson(JsonData json)
        {
            if (json == null) { return new OSD(); }

            switch (json.GetJsonType())
            {
                case JsonType.Boolean:
                    return OSD.FromBoolean((bool)json);
                case JsonType.Int:
                    return OSD.FromInteger((int)json);
                case JsonType.Long:
                    return OSD.FromLong((long)json);
                case JsonType.Double:
                    return OSD.FromReal((double)json);
                case JsonType.String:
                    string str = (string)json;
                    return string.IsNullOrEmpty(str) ? new OSD() : OSD.FromString(str);
                case JsonType.Array:
                    OSDArray array = new OSDArray(json.Count);
                    foreach (JsonData e in json as IList)
                    {
                        array.Add(DeserializeJson(e));
                    }
                    return array;
                case JsonType.Object:
                    OSDMap map = new OSDMap(json.Count);
                    var ordered = json as IOrderedDictionary;
                    foreach (DictionaryEntry de in ordered)
                    {
                        string key = de.Key as string;
                        JsonData val = de.Value as JsonData;
                        map.Add(key ?? string.Empty, DeserializeJson(val));
                    }
                    return map;
                case JsonType.None:
                default:
                    return new OSD();
            }
        }

        public static string SerializeJsonString(OSD osd, bool preserveDefaults = false)
        {
            var writer = new JsonWriter
            {
                PrettyPrint = false,
                Validate = false
            };

            if (preserveDefaults)
            {
                WriteJsonWithDefaults(osd, writer);
            }
            else
            {
                WriteJsonNoDefaultsRoot(osd, writer);
            }

            return writer.ToString();
        }

        public static void SerializeJsonString(OSD osd, bool preserveDefaults, ref JsonWriter writer)
        {
            if (preserveDefaults)
                WriteJsonWithDefaults(osd, writer);
            else
                WriteJsonNoDefaultsRoot(osd, writer);
        }

        // Write routines
        private static void WriteJsonWithDefaults(OSD osd, JsonWriter writer)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    writer.Write(osd.AsBoolean());
                    break;
                case OSDType.Integer:
                    writer.Write(osd.AsInteger());
                    break;
                case OSDType.Real:
                    writer.Write(osd.AsReal());
                    break;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                case OSDType.UUID:
                    writer.Write(osd.AsString());
                    break;
                case OSDType.Binary:
                    {
                        byte[] binary = osd.AsBinary();
                        writer.WriteArrayStart();
                        foreach (byte b in binary)
                            writer.Write(b);
                        writer.WriteArrayEnd();
                    }
                    break;
                case OSDType.Array:
                    writer.WriteArrayStart();
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        WriteJsonWithDefaults(t, writer);
                    writer.WriteArrayEnd();
                    break;
                case OSDType.Map:
                    writer.WriteObjectStart();
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        writer.WritePropertyName(kvp.Key);
                        WriteJsonWithDefaults(kvp.Value, writer);
                    }
                    writer.WriteObjectEnd();
                    break;
                case OSDType.Unknown:
                default:
                    writer.Write(null);
                    break;
            }
        }

        private static void WriteJsonNoDefaultsRoot(OSD osd, JsonWriter writer)
        {
            if (osd == null)
            {
                writer.Write(null);
                return;
            }

            switch (osd.Type)
            {
                case OSDType.Boolean:
                    if (!osd.AsBoolean()) writer.Write(null); else writer.Write(true);
                    break;
                case OSDType.Integer:
                    if (osd.AsInteger() == 0) writer.Write(null); else writer.Write(osd.AsInteger());
                    break;
                case OSDType.Real:
                    if (osd.AsReal() == 0.0d) writer.Write(null); else writer.Write(osd.AsReal());
                    break;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    string s = osd.AsString();
                    if (string.IsNullOrEmpty(s)) writer.Write(null); else writer.Write(s);
                    break;
                case OSDType.UUID:
                    UUID uuid = osd.AsUUID();
                    if (uuid == UUID.Zero) writer.Write(null); else writer.Write(uuid.ToString());
                    break;
                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    if (binary == Utils.EmptyBytes) writer.Write(null);
                    else
                    {
                        writer.WriteArrayStart();
                        foreach (byte b in binary) writer.Write(b);
                        writer.WriteArrayEnd();
                    }
                    break;
                case OSDType.Array:
                    // Arrays are always serialized; elements may be null
                    writer.WriteArrayStart();
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                    {
                        if (!WriteJsonNoDefaultsElement(t, writer))
                            writer.Write(null);
                    }
                    writer.WriteArrayEnd();
                    break;
                case OSDType.Map:
                    // Maps are serialized as objects. Only include properties whose
                    // values would not be represented as null by the no-default rules.
                    writer.WriteObjectStart();
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        if (ShouldSerializeNoDefaults(kvp.Value))
                        {
                            writer.WritePropertyName(kvp.Key);
                            WriteJsonNoDefaultsRoot(kvp.Value, writer);
                        }
                    }
                    writer.WriteObjectEnd();
                    break;
                case OSDType.Unknown:
                default:
                    writer.Write(null);
                    break;
            }
        }

        private static bool WriteJsonNoDefaultsElement(OSD osd, JsonWriter writer)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    if (!osd.AsBoolean()) return false;
                    writer.Write(true);
                    return true;
                case OSDType.Integer:
                    if (osd.AsInteger() == 0) return false;
                    writer.Write(osd.AsInteger());
                    return true;
                case OSDType.Real:
                    if (osd.AsReal() == 0.0d) return false;
                    writer.Write(osd.AsReal());
                    return true;
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    string s = osd.AsString();
                    if (string.IsNullOrEmpty(s)) return false;
                    writer.Write(s);
                    return true;
                case OSDType.UUID:
                    UUID uuid = osd.AsUUID();
                    if (uuid == UUID.Zero) return false;
                    writer.Write(uuid.ToString());
                    return true;
                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    if (binary == Utils.EmptyBytes) return false;
                    writer.WriteArrayStart();
                    foreach (byte b in binary) writer.Write(b);
                    writer.WriteArrayEnd();
                    return true;
                case OSDType.Array:
                    writer.WriteArrayStart();
                    OSDArray arr = (OSDArray)osd;
                    foreach (OSD t in arr)
                    {
                        if (!WriteJsonNoDefaultsElement(t, writer))
                            writer.Write(null);
                    }
                    writer.WriteArrayEnd();
                    return true;
                case OSDType.Map:
                    writer.WriteObjectStart();
                    OSDMap m = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in m)
                    {
                        if (ShouldSerializeNoDefaults(kvp.Value))
                        {
                            writer.WritePropertyName(kvp.Key);
                            WriteJsonNoDefaultsRoot(kvp.Value, writer);
                        }
                    }
                    writer.WriteObjectEnd();
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
                    return true; // arrays are always serialized
                case OSDType.Map:
                    return true; // maps are always serialized (might be empty)
                case OSDType.Unknown:
                default:
                    return false;
            }
        }
    }
}
