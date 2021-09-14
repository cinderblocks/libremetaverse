/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021, Sjofn LLC.
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
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;

namespace OpenMetaverse.StructuredData
{
    /// <summary>
    /// 
    /// </summary>
    public static partial class OSDParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlStream"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(Stream xmlStream)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                CheckCharacters = false,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                DtdProcessing = DtdProcessing.Prohibit
            };
            using (XmlReader xrd = XmlReader.Create(xmlStream))
                return DeserializeLLSDXml(xrd);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(byte[] xmlData)
        {
            return DeserializeLLSDXml(new MemoryStream(xmlData, false));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(string xmlData)
        {
            byte[] bytes = Utils.StringToBytes(xmlData);
            return DeserializeLLSDXml(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlData"></param>
        /// <returns></returns>
        public static OSD DeserializeLLSDXml(XmlReader xmlData)
        {
            try
            {
                xmlData.Read();
                SkipWhitespace(xmlData);

                xmlData.Read();
                OSD ret = ParseLLSDXmlElement(xmlData);

                return ret;
            }
            catch
            {
                return new OSD();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] SerializeLLSDXmlBytes(OSD data)
        {
            return Encoding.UTF8.GetBytes(SerializeLLSDXmlString(data));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string SerializeLLSDXmlString(OSD data)
        {
            StringWriter sw = new StringWriter();
            using(XmlTextWriter writer = new XmlTextWriter(sw))
            {
                writer.Formatting = Formatting.None;

                writer.WriteStartElement(string.Empty, "llsd", string.Empty);
                SerializeLLSDXmlElement(writer, data);
                writer.WriteEndElement();

                return sw.ToString();
            }
        }

        public static string SerializeLLSDInnerXmlString(OSD data)
        {
            StringWriter sw = new StringWriter();
            using (XmlTextWriter writer = new XmlTextWriter(sw))
            {
                writer.Formatting = Formatting.None;

                SerializeLLSDXmlElement(writer, data);

                return sw.ToString();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="data"></param>
        public static void SerializeLLSDXmlElement(XmlWriter writer, OSD data)
        {
            switch (data.Type)
            {
                case OSDType.Unknown:
                    writer.WriteStartElement(string.Empty, "undef", string.Empty);
                    writer.WriteEndElement();
                    break;
                case OSDType.Boolean:
                    writer.WriteStartElement(string.Empty, "boolean", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.Integer:
                    writer.WriteStartElement(string.Empty, "integer", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.Real:
                    writer.WriteStartElement(string.Empty, "real", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.String:
                    writer.WriteStartElement(string.Empty, "string", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.UUID:
                    writer.WriteStartElement(string.Empty, "uuid", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.Date:
                    writer.WriteStartElement(string.Empty, "date", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.URI:
                    writer.WriteStartElement(string.Empty, "uri", string.Empty);
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.Binary:
                    writer.WriteStartElement(string.Empty, "binary", string.Empty);
                        writer.WriteStartAttribute(string.Empty, "encoding", string.Empty);
                        writer.WriteString("base64");
                        writer.WriteEndAttribute();
                    writer.WriteString(data.AsString());
                    writer.WriteEndElement();
                    break;
                case OSDType.Map:
                    OSDMap map = (OSDMap)data;
                    writer.WriteStartElement(string.Empty, "map", string.Empty);
                    foreach (KeyValuePair<string, OSD> kvp in map)
                    {
                        writer.WriteStartElement(string.Empty, "key", string.Empty);
                        writer.WriteString(kvp.Key);
                        writer.WriteEndElement();

                        SerializeLLSDXmlElement(writer, kvp.Value);
                    }
                    writer.WriteEndElement();
                    break;
                case OSDType.Array:
                    OSDArray array = (OSDArray)data;
                    writer.WriteStartElement(string.Empty, "array", string.Empty);
                    foreach (var element in array)
                    {
                        SerializeLLSDXmlElement(writer, element);
                    }
                    writer.WriteEndElement();
                    break;
                case OSDType.LlsdXml:
                    writer.WriteRaw(data.AsString());
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static OSD ParseLLSDXmlElement(XmlReader reader)
        {
            SkipWhitespace(reader);

            if (reader.NodeType != XmlNodeType.Element)
                throw new OSDException("Expected an element");

            string type = reader.LocalName;
            OSD ret;

            switch (type)
            {
                case "undef":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return new OSD();
                    }

                    reader.Read();
                    SkipWhitespace(reader);
                    ret = new OSD();
                    break;
                case "boolean":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromBoolean(false);
                    }

                    if (reader.Read())
                    {
                        string s = reader.ReadString().Trim();

                        if (!string.IsNullOrEmpty(s) && (s == "true" || s == "1"))
                        {
                            ret = OSD.FromBoolean(true);
                            break;
                        }
                    }

                    ret = OSD.FromBoolean(false);
                    break;
                case "integer":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromInteger(0);
                    }

                    if (reader.Read())
                    {
                        int.TryParse(reader.ReadString().Trim(), out var value);
                        ret = OSD.FromInteger(value);
                        break;
                    }

                    ret = OSD.FromInteger(0);
                    break;
                case "real":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromReal(0d);
                    }

                    if (reader.Read())
                    {
                        double value = 0d;
                        string str = reader.ReadString().Trim().ToLower();

                        if (str == "nan")
                            value = double.NaN;
                        else
                            Utils.TryParseDouble(str, out value);

                        ret = OSD.FromReal(value);
                        break;
                    }

                    ret = OSD.FromReal(0d);
                    break;
                case "uuid":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromUUID(UUID.Zero);
                    }

                    if (reader.Read())
                    {
                        UUID.TryParse(reader.ReadString().Trim(), out var value);
                        ret = OSD.FromUUID(value);
                        break;
                    }

                    ret = OSD.FromUUID(UUID.Zero);
                    break;
                case "date":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromDate(Utils.Epoch);
                    }

                    if (reader.Read())
                    {
                        DateTime.TryParse(reader.ReadString().Trim(), out var value);
                        ret = OSD.FromDate(value);
                        break;
                    }

                    ret = OSD.FromDate(Utils.Epoch);
                    break;
                case "string":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromString(string.Empty);
                    }

                    if (reader.Read())
                    {
                        ret = OSD.FromString(reader.ReadString());
                        break;
                    }

                    ret = OSD.FromString(string.Empty);
                    break;
                case "binary":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromBinary(Utils.EmptyBytes);
                    }

                    if (reader.GetAttribute("encoding") != null && reader.GetAttribute("encoding") != "base64")
                        throw new OSDException("Unsupported binary encoding: " + reader.GetAttribute("encoding"));

                    if (reader.Read())
                    {
                        try
                        {
                            ret = OSD.FromBinary(Convert.FromBase64String(reader.ReadString().Trim()));
                            break;
                        }
                        catch (FormatException ex)
                        {
                            throw new OSDException("Binary decoding exception: " + ex.Message);
                        }
                    }

                    ret = OSD.FromBinary(Utils.EmptyBytes);
                    break;
                case "uri":
                    if (reader.IsEmptyElement)
                    {
                        reader.Read();
                        return OSD.FromUri(new Uri(string.Empty, UriKind.RelativeOrAbsolute));
                    }

                    if (reader.Read())
                    {
                        ret = OSD.FromUri(new Uri(reader.ReadString(), UriKind.RelativeOrAbsolute));
                        break;
                    }

                    ret = OSD.FromUri(new Uri(string.Empty, UriKind.RelativeOrAbsolute));
                    break;
                case "map":
                    return ParseLLSDXmlMap(reader);
                case "array":
                    return ParseLLSDXmlArray(reader);
                default:
                    reader.Read();
                    ret = null;
                    break;
            }

            if (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != type)
            {
                throw new OSDException("Expected </" + type + ">");
            }

            reader.Read();
            return ret;
        }

        private static OSDMap ParseLLSDXmlMap(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "map")
                throw new NotImplementedException("Expected <map>");

            OSDMap map = new OSDMap();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return map;
            }

            if (reader.Read())
            {
                while (true)
                {
                    SkipWhitespace(reader);

                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "map")
                    {
                        reader.Read();
                        break;
                    }

                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "key")
                        throw new OSDException("Expected <key>");

                    string key = reader.ReadString();

                    if (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != "key")
                        throw new OSDException("Expected </key>");

                    if (reader.Read())
                        map[key] = ParseLLSDXmlElement(reader);
                    else
                        throw new OSDException("Failed to parse a value for key " + key);
                }
            }

            return map;
        }

        private static OSDArray ParseLLSDXmlArray(XmlReader reader)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "array")
                throw new OSDException("Expected <array>");

            OSDArray array = new OSDArray();

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return array;
            }

            if (reader.Read())
            {
                while (true)
                {
                    SkipWhitespace(reader);

                    if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "array")
                    {
                        reader.Read();
                        break;
                    }

                    array.Add(ParseLLSDXmlElement(reader));
                }
            }

            return array;
        }        

        private static void SkipWhitespace(XmlReader reader)
        {
            while (
                reader.NodeType == XmlNodeType.Comment ||
                reader.NodeType == XmlNodeType.Whitespace ||
                reader.NodeType == XmlNodeType.SignificantWhitespace ||
                reader.NodeType == XmlNodeType.XmlDeclaration)
            {
                reader.Read();
            }
        }
    }
}
