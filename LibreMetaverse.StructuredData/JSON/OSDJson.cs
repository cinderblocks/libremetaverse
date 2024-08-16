﻿using System;
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
            if (json == null) return new OSD();

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
                    for (int i = 0; i < json.Count; i++)
                        array.Add(DeserializeJson(json[i]));
                    return array;
                case JsonType.Object:
                    OSDMap map = new OSDMap(json.Count);
                    IDictionaryEnumerator e = ((IOrderedDictionary)json).GetEnumerator();
                    while (e.MoveNext())
                        map.Add((string)e.Key, DeserializeJson((JsonData)e.Value));
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
                    JsonData jsonbinarray = new JsonData();
                    jsonbinarray.SetJsonType(JsonType.Array);
                    foreach (byte t in binary)
                        jsonbinarray.Add(new JsonData(t));
                    return jsonbinarray;
                case OSDType.Array:
                    JsonData jsonarray = new JsonData();
                    jsonarray.SetJsonType(JsonType.Array);
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        jsonarray.Add(SerializeJson(t, preserveDefaults));
                    return jsonarray;
                case OSDType.Map:
                    JsonData jsonmap = new JsonData();
                    jsonmap.SetJsonType(JsonType.Object);
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        var data = preserveDefaults 
                            ? SerializeJson(kvp.Value, preserveDefaults) 
                            : SerializeJsonNoDefaults(kvp.Value);

                        if (data != null)
                            jsonmap[kvp.Key] = data;
                    }
                    return jsonmap;
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

                    JsonData jsonbinarray = new JsonData();
                    jsonbinarray.SetJsonType(JsonType.Array);
                    foreach (byte t in binary)
                        jsonbinarray.Add(new JsonData(t));
                    return jsonbinarray;
                case OSDType.Array:
                    JsonData jsonarray = new JsonData();
                    jsonarray.SetJsonType(JsonType.Array);
                    OSDArray array = (OSDArray)osd;
                    foreach (OSD t in array)
                        jsonarray.Add(SerializeJson(t, false));
                    return jsonarray;
                case OSDType.Map:
                    JsonData jsonmap = new JsonData();
                    jsonmap.SetJsonType(JsonType.Object);
                    OSDMap map = (OSDMap)osd;
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        JsonData data = SerializeJsonNoDefaults(kvp.Value);
                        if (data != null)
                            jsonmap[kvp.Key] = data;
                    }
                    return jsonmap;
                case OSDType.Unknown:
                default:
                    return null;
            }
        }
    }
}
