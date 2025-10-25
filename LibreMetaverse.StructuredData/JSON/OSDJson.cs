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
                return DeserializeJson(JsonMapper.ToObject(reader));
            }
        }

        public static OSD DeserializeJson(string json)
        {
            return DeserializeJson(JsonMapper.ToObject(json));
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
                    foreach (KeyValuePair<string, JsonData> e in json as IOrderedDictionary)
                    {
                        map.Add(e.Key, DeserializeJson(e.Value));
                    }
                    return map;
                case JsonType.None:
                default:
                    return new OSD();
            }
        }

        public static string SerializeJsonString(OSD osd)
        {
            return SerializeJson(osd, false).ToJson();
        }

        public static string SerializeJsonString(OSD osd, bool preserveDefaults)
        {
            return SerializeJson(osd, preserveDefaults).ToJson();
        }

        public static void SerializeJsonString(OSD osd, bool preserveDefaults, ref JsonWriter writer)
        {
            SerializeJson(osd, preserveDefaults).ToJson(writer);
        }

        public static JsonData SerializeJson(OSD osd, bool preserveDefaults)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    return new JsonData(osd.AsBoolean());
                case OSDType.Integer:
                    return new JsonData(osd.AsInteger());
                case OSDType.Real:
                    return new JsonData(osd.AsReal());
                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                case OSDType.UUID:
                    return new JsonData(osd.AsString());
                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    JsonData jsonBinArray = new JsonData();
                    jsonBinArray.SetJsonType(JsonType.Array);
                    foreach (byte t in binary)
                        jsonBinArray.Add(new JsonData(t));
                    return jsonBinArray;
                case OSDType.Array:
                    JsonData jsonArray = new JsonData();
                    jsonArray.SetJsonType(JsonType.Array);
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        jsonArray.Add(SerializeJson(t, preserveDefaults));
                    return jsonArray;
                case OSDType.Map:
                    JsonData jsonMap = new JsonData();
                    jsonMap.SetJsonType(JsonType.Object);
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        var data = preserveDefaults 
                            ? SerializeJson(kvp.Value, preserveDefaults) 
                            : SerializeJsonNoDefaults(kvp.Value);

                        if (data != null) { jsonMap[kvp.Key] = data; }
                    }
                    return jsonMap;
                case OSDType.Unknown:
                default:
                    return new JsonData(null);
            }
        }

        private static JsonData SerializeJsonNoDefaults(OSD osd)
        {
            switch (osd.Type)
            {
                case OSDType.Boolean:
                    bool b = osd.AsBoolean();
                    return !b ? null : new JsonData(b);

                case OSDType.Integer:
                    int v = osd.AsInteger();
                    return v == 0 ? null : new JsonData(v);

                case OSDType.Real:
                    double d = osd.AsReal();
                    return d == 0.0d ? null : new JsonData(d);

                case OSDType.String:
                case OSDType.Date:
                case OSDType.URI:
                    string str = osd.AsString();
                    return string.IsNullOrEmpty(str) ? null : new JsonData(str);

                case OSDType.UUID:
                    UUID uuid = osd.AsUUID();
                    return uuid == UUID.Zero ? null : new JsonData(uuid.ToString());

                case OSDType.Binary:
                    byte[] binary = osd.AsBinary();
                    if (binary == Utils.EmptyBytes)
                        return null;

                    JsonData jsonBinArray = new JsonData();
                    jsonBinArray.SetJsonType(JsonType.Array);
                    foreach (byte t in binary)
                        jsonBinArray.Add(new JsonData(t));
                    return jsonBinArray;
                case OSDType.Array:
                    JsonData jsonArray = new JsonData();
                    jsonArray.SetJsonType(JsonType.Array);
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        jsonArray.Add(SerializeJson(t, false));
                    return jsonArray;
                case OSDType.Map:
                    JsonData jsonMap = new JsonData();
                    jsonMap.SetJsonType(JsonType.Object);
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        JsonData data = SerializeJsonNoDefaults(kvp.Value);
                        if (data != null)
                            jsonMap[kvp.Key] = data;
                    }
                    return jsonMap;
                case OSDType.Unknown:
                default:
                    return null;
            }
        }
    }
}
