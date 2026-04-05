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
using System.Collections;
using System.Reflection;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Packets
{
    public abstract partial class Packet
    {
        #region Serialization/Deserialization

        public static string ToXmlString(Packet packet)
        {
            return OSDParser.SerializeLLSDXmlString(GetLLSD(packet));
        }

        public static OSD GetLLSD(Packet packet)
        {
            OSDMap body = new OSDMap();
            Type type = packet.GetType();

            foreach (FieldInfo field in type.GetFields())
            {
                if (field.IsPublic)
                {
                    Type blockType = field.FieldType;

                    if (blockType.IsArray)
                    {
                        var blockArray = field.GetValue(packet) as Array;
                        if (blockArray == null)
                        {
                            body[field.Name] = new OSDArray();
                        }
                        else
                        {
                            OSDArray blockList = new OSDArray(blockArray.Length);
                            foreach (var item in blockArray)
                            {
                                blockList.Add(BuildLLSDBlock(item));
                            }
                            body[field.Name] = blockList;
                        }
                    }
                    else
                    {
                        var block = field.GetValue(packet);
                        body[field.Name] = BuildLLSDBlock(block);
                    }
                }
            }

            return body;
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

        public static Packet? FromLLSD(OSD osd)
        {
            // FIXME: Need the inverse of the reflection magic above done here
            throw new NotImplementedException();
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
            Assembly assembly = Assembly.GetExecutingAssembly();

            // Check if we have a subclass of packet with the same name as this event
            Type? type = assembly.GetType("OpenMetaverse.Packets." + capsEventName + "Packet", false);
            if (type == null)
                return null;

            Packet? packet = null;

            try
            {
                // Create an instance of the object
                packet = Activator.CreateInstance(type) as Packet;

                // Iterate over all fields in the packet class, looking for matches in the LLSD
                foreach (FieldInfo field in type.GetFields())
                {
                    if (body.ContainsKey(field.Name))
                    {
                        Type blockType = field.FieldType;

                        if (blockType.IsArray)
                        {
                            var arrOsd = body[field.Name] as OSDArray;
                            if (arrOsd == null) continue;
                            Type? elementType = blockType.GetElementType();
                            if (elementType != null)
                            {
                                object[] blockArray = (object[])Array.CreateInstance(elementType, arrOsd.Count);

                                for (int i = 0; i < arrOsd.Count; i++)
                                {
                                    OSDMap? map = arrOsd[i] as OSDMap;
                                    if (map == null) continue;
                                    blockArray[i] = ParseLLSDBlock(map, elementType);
                                }

                                field.SetValue(packet, blockArray);
                            }
                        }
                        else
                        {
                            var arr = body[field.Name] as OSDArray;
                            if (arr == null || arr.Count == 0) continue;
                            OSDMap? map = arr[0] as OSDMap;
                            if (map == null) continue;
                            field.SetValue(packet, ParseLLSDBlock(map, blockType));
                        }
                    }
                }
            }
            catch (Exception)
            {
                //FIXME Logger.Error(e.Message, e);
            }

            return packet;
        }

        private static object ParseLLSDBlock(OSDMap blockData, Type blockType)
        {
            object block = Activator.CreateInstance(blockType)!;

            // Iterate over each field and set the value if a match was found in the LLSD
            foreach (FieldInfo field in blockType.GetFields())
            {
                if (blockData.ContainsKey(field.Name))
                {
                    Type fieldType = field.FieldType;

                    // Retrieve value once and guard nullability for the compiler
                    var osdValue = blockData[field.Name];
                    if (osdValue == null) continue;

                    if (fieldType == typeof(ulong))
                    {
                        // ulongs come in as a byte array, convert it manually here
                        byte[] bytes = osdValue.AsBinary();
                        ulong value = Utils.BytesToUInt64(bytes);
                        field.SetValue(block, value);
                    }
                    else if (fieldType == typeof(uint))
                    {
                        // uints come in as a byte array, convert it manually here
                        byte[] bytes = osdValue.AsBinary();
                        uint value = Utils.BytesToUInt(bytes);
                        field.SetValue(block, value);
                    }
                    else if (fieldType == typeof(ushort))
                    {
                        // Just need a bit of manual typecasting love here
                        field.SetValue(block, (ushort)osdValue.AsInteger());
                    }
                    else if (fieldType == typeof(byte))
                    {
                        // Just need a bit of manual typecasting love here
                        field.SetValue(block, (byte)osdValue.AsInteger());
                    }
                    else if (fieldType == typeof(sbyte))
                    {
                        field.SetValue(block, (sbyte)osdValue.AsInteger());
                    }
                    else if (fieldType == typeof(short))
                    {
                        field.SetValue(block, (short)osdValue.AsInteger());
                    }
                    else if (fieldType == typeof(string))
                    {
                        field.SetValue(block, osdValue.AsString());
                    }
                    else if (fieldType == typeof(bool))
                    {
                        field.SetValue(block, osdValue.AsBoolean());
                    }
                    else if (fieldType == typeof(float))
                    {
                        field.SetValue(block, (float)osdValue.AsReal());
                    }
                    else if (fieldType == typeof(double))
                    {
                        field.SetValue(block, blockData[field.Name].AsReal());
                    }
                    else if (fieldType == typeof(int))
                    {
                        field.SetValue(block, blockData[field.Name].AsInteger());
                    }
                    else if (fieldType == typeof(UUID))
                    {
                        field.SetValue(block, blockData[field.Name].AsUUID());
                    }
                    else if (fieldType == typeof(Vector3))
                    {
                        Vector3 vec = ((OSDArray)blockData[field.Name]).AsVector3();
                        field.SetValue(block, vec);
                    }
                    else if (fieldType == typeof(Vector4))
                    {
                        Vector4 vec = ((OSDArray)blockData[field.Name]).AsVector4();
                        field.SetValue(block, vec);
                    }
                    else if (fieldType == typeof(Quaternion))
                    {
                        Quaternion quat = ((OSDArray)blockData[field.Name]).AsQuaternion();
                        field.SetValue(block, quat);
                    }
                    else if (fieldType == typeof(byte[]) && blockData[field.Name].Type == OSDType.String)
                    {
                        field.SetValue(block, Utils.StringToBytes(blockData[field.Name]));
                    }
                }
            }

            // Additional fields come as properties, Handle those as well.
            foreach (PropertyInfo property in blockType.GetProperties())
            {
                if (blockData.ContainsKey(property.Name))
                {
                    var propOSD = blockData[property.Name];
                    OSDType proptype = propOSD.Type;
                    MethodInfo? set = property.GetSetMethod();
                    if (set == null) continue;

                    if (proptype.Equals(OSDType.Binary))
                    {
                        set.Invoke(block, new object[] { propOSD.AsBinary() ?? Utils.EmptyBytes });
                    }
                    else
                    {
                        set.Invoke(block, new object[] { Utils.StringToBytes(propOSD.AsString() ?? string.Empty) });
                    }
                }
            }

            return block;
        }

        private static OSD BuildLLSDBlock(object? block)
        {
            if (block == null)
                return new OSDMap();

            OSDMap map = new OSDMap();
            Type blockType = block.GetType();

            foreach (FieldInfo field in blockType.GetFields())
            {
                if (field.IsPublic)
                {
                    var val = field.GetValue(block);
                    map[field.Name] = OSD.FromObject(val ?? Utils.EmptyBytes);
                }
            }

            foreach (PropertyInfo property in blockType.GetProperties())
            {
                if (property.Name != "Length")
                {
                    var val = property.GetValue(block, null);
                    map[property.Name] = OSD.FromObject(val ?? string.Empty);
                }
            }

            return map;
        }
    }
}

