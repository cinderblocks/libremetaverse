/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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

namespace MapGenerator
{
    internal class MapGenerator
    {
        private static void WriteFieldMember(TextWriter writer, MapField field)
        {
            var type = string.Empty;

            switch (field.Type)
            {
                case FieldType.BOOL:
                    type = "bool";
                    break;
                case FieldType.F32:
                    type = "float";
                    break;
                case FieldType.F64:
                    type = "double";
                    break;
                case FieldType.IPPORT:
                case FieldType.U16:
                    type = "ushort";
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    type = "uint";
                    break;
                case FieldType.LLQuaternion:
                    type = "Quaternion";
                    break;
                case FieldType.LLUUID:
                    type = "UUID";
                    break;
                case FieldType.LLVector3:
                    type = "Vector3";
                    break;
                case FieldType.LLVector3d:
                    type = "Vector3d";
                    break;
                case FieldType.LLVector4:
                    type = "Vector4";
                    break;
                case FieldType.S16:
                    type = "short";
                    break;
                case FieldType.S32:
                    type = "int";
                    break;
                case FieldType.S8:
                    type = "sbyte";
                    break;
                case FieldType.U64:
                    type = "ulong";
                    break;
                case FieldType.U8:
                    type = "byte";
                    break;
                case FieldType.Fixed:
                    type = "byte[]";
                    break;
            }
            if (field.Type != FieldType.Variable)
            {
                //writer.WriteLine("            /// <summary>" + field.Name + " field</summary>");
                writer.WriteLine("            public " + type + " " + field.Name + ";");
            }
            else
            {
                writer.WriteLine("            public byte[] " + field.Name + ";");

                //writer.WriteLine("            private byte[] _" + field.Name.ToLower() + ";");
                ////writer.WriteLine("            /// <summary>" + field.Name + " field</summary>");
                //writer.WriteLine("            public byte[] " + field.Name + Environment.NewLine + "            {");
                //writer.WriteLine("                get { return _" + field.Name.ToLower() + "; }");
                //writer.WriteLine("                set" + Environment.NewLine + "                {");
                //writer.WriteLine("                    if (value == null) { _" +
                //    field.Name.ToLower() + " = null; return; }");
                //writer.WriteLine("                    if (value.Length > " +
                //    ((field.Count == 1) ? "255" : "1100") + ") { throw new OverflowException(" +
                //    "\"Value exceeds " + ((field.Count == 1) ? "255" : "1100") + " characters\"); }");
                //writer.WriteLine("                    else { _" + field.Name.ToLower() +
                //    " = new byte[value.Length]; Buffer.BlockCopy(value, 0, _" +
                //    field.Name.ToLower() + ", 0, value.Length); }");
                //writer.WriteLine("                }" + Environment.NewLine + "            }");
            }
        }

        private static void WriteFieldFromBytes(TextWriter writer, MapField field)
        {
            switch (field.Type)
            {
                case FieldType.BOOL:
                    writer.WriteLine("                    " +
                        field.Name + " = (bytes[i++] != 0) ? (bool)true : (bool)false;");
                    break;
                case FieldType.F32:
                    writer.WriteLine("                    " +
                        field.Name + " = Utils.BytesToFloat(bytes, i); i += 4;");
                    break;
                case FieldType.F64:
                    writer.WriteLine("                    " +
                        field.Name + " = Utils.BytesToDouble(bytes, i); i += 8;");
                    break;
                case FieldType.Fixed:
                    writer.WriteLine("                    " + field.Name + " = new byte[" + field.Count + "];");
                    writer.WriteLine("                    Buffer.BlockCopy(bytes, i, " + field.Name +
                        ", 0, " + field.Count + "); i += " + field.Count + ";");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    writer.WriteLine("                    " + field.Name +
                        " = (uint)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));");
                    break;
                case FieldType.IPPORT:
                    // IPPORT is big endian while U16/S16 are little endian. Go figure
                    writer.WriteLine("                    " + field.Name +
                        " = (ushort)((bytes[i++] << 8) + bytes[i++]);");
                    break;
                case FieldType.U16:
                    writer.WriteLine("                    " + field.Name +
                        " = (ushort)(bytes[i++] + (bytes[i++] << 8));");
                    break;
                case FieldType.LLQuaternion:
                    writer.WriteLine("                    " + field.Name + ".FromBytes(bytes, i, true); i += 12;");
                    break;
                case FieldType.LLUUID:
                    writer.WriteLine("                    " + field.Name + ".FromBytes(bytes, i); i += 16;");
                    break;
                case FieldType.LLVector3:
                    writer.WriteLine("                    " + field.Name + ".FromBytes(bytes, i); i += 12;");
                    break;
                case FieldType.LLVector3d:
                    writer.WriteLine("                    " + field.Name + ".FromBytes(bytes, i); i += 24;");
                    break;
                case FieldType.LLVector4:
                    writer.WriteLine("                    " + field.Name + ".FromBytes(bytes, i); i += 16;");
                    break;
                case FieldType.S16:
                    writer.WriteLine("                    " + field.Name +
                        " = (short)(bytes[i++] + (bytes[i++] << 8));");
                    break;
                case FieldType.S32:
                    writer.WriteLine("                    " + field.Name +
                        " = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));");
                    break;
                case FieldType.S8:
                    writer.WriteLine("                    " + field.Name +
                        " = (sbyte)bytes[i++];");
                    break;
                case FieldType.U64:
                    writer.WriteLine("                    " + field.Name +
                        " = (ulong)((ulong)bytes[i++] + ((ulong)bytes[i++] << 8) + " +
                        "((ulong)bytes[i++] << 16) + ((ulong)bytes[i++] << 24) + " +
                        "((ulong)bytes[i++] << 32) + ((ulong)bytes[i++] << 40) + " +
                        "((ulong)bytes[i++] << 48) + ((ulong)bytes[i++] << 56));");
                    break;
                case FieldType.U8:
                    writer.WriteLine("                    " + field.Name +
                        " = (byte)bytes[i++];");
                    break;
                case FieldType.Variable:
                    if (field.Count == 1)
                    {
                        writer.WriteLine("                    length = bytes[i++];");
                    }
                    else
                    {
                        writer.WriteLine("                    length = (bytes[i++] + (bytes[i++] << 8));");
                    }
                    writer.WriteLine("                    " + field.Name + " = new byte[length];");
                    writer.WriteLine("                    Buffer.BlockCopy(bytes, i, " + field.Name + ", 0, length); i += length;");
                    break;
                default:
                    writer.WriteLine("!!! ERROR: Unhandled FieldType: " + field.Type + " !!!");
                    break;
            }
        }

        private static void WriteFieldToBytes(TextWriter writer, MapField field)
        {
            writer.Write("                ");

            switch (field.Type)
            {
                case FieldType.BOOL:
                    writer.WriteLine("bytes[i++] = (byte)((" + field.Name + ") ? 1 : 0);");
                    break;
                case FieldType.F32:
                    writer.WriteLine("Utils.FloatToBytes(" + field.Name + ", bytes, i); i += 4;");
                    break;
                case FieldType.F64:
                    writer.WriteLine("Utils.DoubleToBytes(" + field.Name + ", bytes, i); i += 8;");
                    break;
                case FieldType.Fixed:
                    writer.WriteLine("Buffer.BlockCopy(" + field.Name + ", 0, bytes, i, " + field.Count + ");" +
                        "i += " + field.Count + ";");
                    break;
                case FieldType.IPPORT:
                    // IPPORT is big endian while U16/S16 is little endian. Go figure
                    writer.WriteLine("bytes[i++] = (byte)((" + field.Name + " >> 8) % 256);");
                    writer.WriteLine("                bytes[i++] = (byte)(" + field.Name + " % 256);");
                    break;
                case FieldType.U16:
                case FieldType.S16:
                    writer.WriteLine("bytes[i++] = (byte)(" + field.Name + " % 256);");
                    writer.WriteLine("                bytes[i++] = (byte)((" + field.Name + " >> 8) % 256);");
                    break;
                case FieldType.LLQuaternion:
                case FieldType.LLVector3:
                    writer.WriteLine(field.Name + ".ToBytes(bytes, i); i += 12;");
                    break;
                case FieldType.LLUUID:
                case FieldType.LLVector4:
                    writer.WriteLine(field.Name + ".ToBytes(bytes, i); i += 16;");
                    break;
                case FieldType.LLVector3d:
                    writer.WriteLine(field.Name + ".ToBytes(bytes, i); i += 24;");
                    break;
                case FieldType.U8:
                    writer.WriteLine("bytes[i++] = " + field.Name + ";");
                    break;
                case FieldType.S8:
                    writer.WriteLine("bytes[i++] = (byte)" + field.Name + ";");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    writer.WriteLine("Utils.UIntToBytes(" + field.Name + ", bytes, i); i += 4;");
                    break;
                case FieldType.S32:
                    writer.WriteLine("Utils.IntToBytes(" + field.Name + ", bytes, i); i += 4;");
                    break;
                case FieldType.U64:
                    writer.WriteLine("Utils.UInt64ToBytes(" + field.Name + ", bytes, i); i += 8;");
                    break;
                case FieldType.Variable:
                    //writer.WriteLine("if(" + field.Name + " == null) { Console.WriteLine(\"Warning: " + field.Name + " is null, in \" + this.GetType()); }");
                    //writer.Write("                ");
                    if (field.Count == 1)
                    {
                        writer.WriteLine("bytes[i++] = (byte)" + field.Name + ".Length;");
                    }
                    else
                    {
                        writer.WriteLine("bytes[i++] = (byte)(" + field.Name + ".Length % 256);");
                        writer.WriteLine("                bytes[i++] = (byte)((" +
                            field.Name + ".Length >> 8) % 256);");
                    }
                    writer.WriteLine("                Buffer.BlockCopy(" + field.Name + ", 0, bytes, i, " +
                        field.Name + ".Length); " + "i += " + field.Name + ".Length;");
                    break;
                default:
                    writer.WriteLine("!!! ERROR: Unhandled FieldType: " + field.Type + " !!!");
                    break;
            }
        }

        private static int GetFieldLength(TextWriter writer, MapField field)
        {
            switch (field.Type)
            {
                case FieldType.BOOL:
                case FieldType.U8:
                case FieldType.S8:
                    return 1;
                case FieldType.U16:
                case FieldType.S16:
                case FieldType.IPPORT:
                    return 2;
                case FieldType.U32:
                case FieldType.S32:
                case FieldType.F32:
                case FieldType.IPADDR:
                    return 4;
                case FieldType.U64:
                case FieldType.F64:
                    return 8;
                case FieldType.LLVector3:
                case FieldType.LLQuaternion:
                    return 12;
                case FieldType.LLUUID:
                case FieldType.LLVector4:
                    return 16;
                case FieldType.LLVector3d:
                    return 24;
                case FieldType.Fixed:
                    return field.Count;
                case FieldType.Variable:
                    return 0;
                default:
                    writer.WriteLine("!!! ERROR: Unhandled FieldType " + field.Type + " !!!");
                    return 0;
            }
        }

        private static void WriteBlockClass(TextWriter writer, MapBlock block, MapPacket packet)
        {
            var variableFieldCountBytes = 0;

            //writer.WriteLine("        /// <summary>" + block.Name + " block</summary>");
            writer.WriteLine("        /// <exclude/>");
            writer.WriteLine("        public sealed class " + block.Name + "Block : PacketBlock" + Environment.NewLine + "        {");

            foreach (var field in block.Fields)
            {
                WriteFieldMember(writer, field);
                if (field.Type == FieldType.Variable) { variableFieldCountBytes += field.Count; }
            }

            // Length property
            writer.WriteLine("");
            //writer.WriteLine("            /// <summary>Length of this block serialized in bytes</summary>");
            writer.WriteLine("            public override int Length" + Environment.NewLine +
                             "            {" + Environment.NewLine +
                             "                get" + Environment.NewLine +
                             "                {");
            var length = variableFieldCountBytes;

            // Figure out the length of this block
            foreach (var field in block.Fields)
            {
                length += GetFieldLength(writer, field);
            }

            if (variableFieldCountBytes == 0)
            {
                writer.WriteLine("                    return " + length + ";");
            }
            else
            {
                writer.WriteLine("                    int length = " + length + ";");

                foreach (var field in block.Fields)
                {
                    if (field.Type == FieldType.Variable)
                    {
                        writer.WriteLine("                    if (" + field.Name +
                            " != null) { length += " + field.Name + ".Length; }");
                    }
                }

                writer.WriteLine("                    return length;");
            }

            writer.WriteLine("                }" + Environment.NewLine + "            }" + Environment.NewLine);

            // Default constructor
            //writer.WriteLine("            /// <summary>Default constructor</summary>");
            writer.WriteLine("            public " + block.Name + "Block() { }");

            // Constructor for building the class from bytes
            //writer.WriteLine("            /// <summary>Constructor for building the block from a byte array</summary>");
            writer.WriteLine("            public " + block.Name + "Block(byte[] bytes, ref int i)" + Environment.NewLine +
                "            {" + Environment.NewLine +
                "                FromBytes(bytes, ref i);" + Environment.NewLine +
                "            }" + Environment.NewLine);

            // Initiates instance variables from a byte message
            writer.WriteLine("            public override void FromBytes(byte[] bytes, ref int i)" + Environment.NewLine +
                "            {");

            // Declare a length variable if we need it for variable fields in this constructor
            if (variableFieldCountBytes > 0) { writer.WriteLine("                int length;"); }

            // Start of the try catch block
            writer.WriteLine("                try" + Environment.NewLine + "                {");

            foreach (var field in block.Fields)
            {
                WriteFieldFromBytes(writer, field);
            }

            writer.WriteLine("                }" + Environment.NewLine +
                "                catch (Exception)" + Environment.NewLine +
                "                {" + Environment.NewLine +
                "                    throw new MalformedDataException();" + Environment.NewLine +
                "                }" + Environment.NewLine + "            }" + Environment.NewLine);

            // ToBytes() function
            //writer.WriteLine("            /// <summary>Serialize this block to a byte array</summary>");
            writer.WriteLine("            public override void ToBytes(byte[] bytes, ref int i)" + Environment.NewLine +
                "            {");

            foreach (var field in block.Fields)
            {
                WriteFieldToBytes(writer, field);
            }

            writer.WriteLine("            }" + Environment.NewLine);
            writer.WriteLine("        }" + Environment.NewLine);
        }

        private static void WritePacketClass(TextWriter writer, MapPacket packet)
        {
            var hasVariableBlocks = false;
            string sanitizedName;

            //writer.WriteLine("    /// <summary>" + packet.Name + " packet</summary>");
            writer.WriteLine("    /// <exclude/>");
            writer.WriteLine("    public sealed class " + packet.Name + "Packet : Packet" + Environment.NewLine + "    {");

            // Write out each block class
            foreach (var block in packet.Blocks)
            {
                WriteBlockClass(writer, block, packet);
            }

            // Length member
            writer.WriteLine("        public override int Length" + Environment.NewLine +
                "        {" + Environment.NewLine + "            get" + Environment.NewLine +
                "            {");

            var length = 0;
            if (packet.Frequency == PacketFrequency.Low) { length = 10; }
            else if (packet.Frequency == PacketFrequency.Medium) { length = 8; }
            else { length = 7; }

            foreach (var block in packet.Blocks)
            {
                if (block.Count == -1)
                {
                    hasVariableBlocks = true;
                    ++length;
                }
            }

            writer.WriteLine("                int length = " + length + ";");

            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == -1)
                {
                    // Variable count block
                    writer.WriteLine("                for (int j = 0; j < " + sanitizedName + ".Length; j++)");
                    writer.WriteLine("                    length += " + sanitizedName + "[j].Length;");
                }
                else if (block.Count == 1)
                {
                    writer.WriteLine("                length += " + sanitizedName + ".Length;");
                }
                else
                {
                    // Multiple count block
                    writer.WriteLine("                for (int j = 0; j < " + block.Count + "; j++)");
                    writer.WriteLine("                    length += " + sanitizedName + "[j].Length;");
                }
            }
            writer.WriteLine("                return length;");
            writer.WriteLine("            }" + Environment.NewLine + "        }");

            // Block members
            foreach (var block in packet.Blocks)
            {
                // TODO: More thorough name blacklisting
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                //writer.WriteLine("        /// <summary>" + block.Name + " block</summary>");
                writer.WriteLine("        public " + block.Name + "Block" +
                    ((block.Count != 1) ? "[]" : "") + " " + sanitizedName + ";");
            }

            writer.WriteLine("");

            // Default constructor
            //writer.WriteLine("        /// <summary>Default constructor</summary>");
            writer.WriteLine("        public " + packet.Name + "Packet()" + Environment.NewLine + "        {");
            writer.WriteLine("            HasVariableBlocks = " + hasVariableBlocks.ToString().ToLowerInvariant() + ";");
            writer.WriteLine("            Type = PacketType." + packet.Name + ";");
            writer.WriteLine("            Header = new Header();");
            writer.WriteLine("            Header.Frequency = PacketFrequency." + packet.Frequency + ";");
            writer.WriteLine("            Header.ID = " + packet.ID + ";");
            writer.WriteLine("            Header.Reliable = true;"); // Turn the reliable flag on by default
            if (packet.Encoded) { writer.WriteLine("            Header.Zerocoded = true;"); }
            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == 1)
                {
                    // Single count block
                    writer.WriteLine("            " + sanitizedName + " = new " + block.Name + "Block();");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    writer.WriteLine("            " + sanitizedName + " = null;");
                }
                else
                {
                    // Multiple count block
                    writer.WriteLine("            " + sanitizedName + " = new " + block.Name + "Block[" + block.Count + "];");
                }
            }
            writer.WriteLine("        }" + Environment.NewLine);

            // Constructor that takes a byte array and beginning position only (no prebuilt header)
            var seenVariable = false;
            //writer.WriteLine("        /// <summary>Constructor that takes a byte array and beginning position (no prebuilt header)</summary>");
            writer.WriteLine("        public " + packet.Name + "Packet(byte[] bytes, ref int i) : this()" + Environment.NewLine +
                "        {" + Environment.NewLine +
                "            int packetEnd = bytes.Length - 1;" + Environment.NewLine +
                "            FromBytes(bytes, ref i, ref packetEnd, null);" + Environment.NewLine +
                "        }" + Environment.NewLine);

            writer.WriteLine("        override public void FromBytes(byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer)" + Environment.NewLine + "        {");
            writer.WriteLine("            Header.FromBytes(bytes, ref i, ref packetEnd);");
            writer.WriteLine("            if (Header.Zerocoded && zeroBuffer != null)");
            writer.WriteLine("            {");
            writer.WriteLine("                packetEnd = Helpers.ZeroDecode(bytes, packetEnd + 1, zeroBuffer) - 1;");
            writer.WriteLine("                bytes = zeroBuffer;");
            writer.WriteLine("            }");

            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == 1)
                {
                    // Single count block
                    writer.WriteLine("            " + sanitizedName + ".FromBytes(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    if (!seenVariable)
                    {
                        writer.WriteLine("            int count = (int)bytes[i++];");
                        seenVariable = true;
                    }
                    else
                    {
                        writer.WriteLine("            count = (int)bytes[i++];");
                    }
                    writer.WriteLine("            if(" + sanitizedName + " == null || " + sanitizedName + ".Length != " + block.Count + ") {");
                    writer.WriteLine("                " + sanitizedName + " = new " + block.Name + "Block[count];");
                    writer.WriteLine("                for(int j = 0; j < count; j++)");
                    writer.WriteLine("                { " + sanitizedName + "[j] = new " + block.Name + "Block(); }");
                    writer.WriteLine("            }");
                    writer.WriteLine("            for (int j = 0; j < count; j++)");
                    writer.WriteLine("            { " + sanitizedName + "[j].FromBytes(bytes, ref i); }");
                }
                else
                {
                    // Multiple count block
                    writer.WriteLine("            if(" + sanitizedName + " == null || " + sanitizedName + ".Length != " + block.Count + ") {");
                    writer.WriteLine("                " + sanitizedName + " = new " + block.Name + "Block[" + block.Count + "];");
                    writer.WriteLine("                for(int j = 0; j < " + block.Count + "; j++)");
                    writer.WriteLine("                { " + sanitizedName + "[j] = new " + block.Name + "Block(); }");
                    writer.WriteLine("            }");
                    writer.WriteLine("            for (int j = 0; j < " + block.Count + "; j++)");
                    writer.WriteLine("            { " + sanitizedName + "[j].FromBytes(bytes, ref i); }");
                }
            }
            writer.WriteLine("        }" + Environment.NewLine);

            seenVariable = false;

            // Constructor that takes a byte array and a prebuilt header
            //writer.WriteLine("        /// <summary>Constructor that takes a byte array and a prebuilt header</summary>");
            writer.WriteLine("        public " + packet.Name + "Packet(Header head, byte[] bytes, ref int i): this()" + Environment.NewLine +
                "        {" + Environment.NewLine +
                "            int packetEnd = bytes.Length - 1;" + Environment.NewLine +
                "            FromBytes(head, bytes, ref i, ref packetEnd);" + Environment.NewLine +
                "        }" + Environment.NewLine);

            writer.WriteLine("        override public void FromBytes(Header header, byte[] bytes, ref int i, ref int packetEnd)" + Environment.NewLine +
                "        {");
            writer.WriteLine("            Header = header;");
            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == 1)
                {
                    // Single count block
                    writer.WriteLine("            " + sanitizedName + ".FromBytes(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    // Variable count block
                    if (!seenVariable)
                    {
                        writer.WriteLine("            int count = (int)bytes[i++];");
                        seenVariable = true;
                    }
                    else
                    {
                        writer.WriteLine("            count = (int)bytes[i++];");
                    }
                    writer.WriteLine("            if(" + sanitizedName + " == null || " + sanitizedName + ".Length != count) {");
                    writer.WriteLine("                " + sanitizedName + " = new " + block.Name + "Block[count];");
                    writer.WriteLine("                for(int j = 0; j < count; j++)");
                    writer.WriteLine("                { " + sanitizedName + "[j] = new " + block.Name + "Block(); }");
                    writer.WriteLine("            }");
                    writer.WriteLine("            for (int j = 0; j < count; j++)");
                    writer.WriteLine("            { " + sanitizedName + "[j].FromBytes(bytes, ref i); }");
                }
                else
                {
                    // Multiple count block
                    writer.WriteLine("            if(" + sanitizedName + " == null || " + sanitizedName + ".Length != " + block.Count + ") {");
                    writer.WriteLine("                " + sanitizedName + " = new " + block.Name + "Block[" + block.Count + "];");
                    writer.WriteLine("                for(int j = 0; j < " + block.Count + "; j++)");
                    writer.WriteLine("                { " + sanitizedName + "[j] = new " + block.Name + "Block(); }");
                    writer.WriteLine("            }");
                    writer.WriteLine("            for (int j = 0; j < " + block.Count + "; j++)");
                    writer.WriteLine("            { " + sanitizedName + "[j].FromBytes(bytes, ref i); }");
                }
            }
            writer.WriteLine("        }" + Environment.NewLine);

            #region ToBytes() Function

            //writer.WriteLine("        /// <summary>Serialize this packet to a byte array</summary><returns>A byte array containing the serialized packet</returns>");
            writer.WriteLine("        public override byte[] ToBytes()" + Environment.NewLine + "        {");

            writer.Write("            int length = ");
            if (packet.Frequency == PacketFrequency.Low) { writer.WriteLine("10;"); }
            else if (packet.Frequency == PacketFrequency.Medium) { writer.WriteLine("8;"); }
            else { writer.WriteLine("7;"); }

            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == 1)
                {
                    // Single count block
                    writer.WriteLine("            length += " + sanitizedName + ".Length;");
                }
            }

            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == -1)
                {
                    writer.WriteLine("            length++;");
                    writer.WriteLine("            for (int j = 0; j < " + sanitizedName +
                        ".Length; j++) { length += " + sanitizedName + "[j].Length; }");
                }
                else if (block.Count > 1)
                {
                    writer.WriteLine("            for (int j = 0; j < " + block.Count +
                        "; j++) { length += " + sanitizedName + "[j].Length; }");
                }
            }

            writer.WriteLine("            if (Header.AckList != null && Header.AckList.Length > 0) { length += Header.AckList.Length * 4 + 1; }");
            writer.WriteLine("            byte[] bytes = new byte[length];");
            writer.WriteLine("            int i = 0;");
            writer.WriteLine("            Header.ToBytes(bytes, ref i);");
            foreach (var block in packet.Blocks)
            {
                if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                else { sanitizedName = block.Name; }

                if (block.Count == -1)
                {
                    // Variable count block
                    writer.WriteLine("            bytes[i++] = (byte)" + sanitizedName + ".Length;");
                    writer.WriteLine("            for (int j = 0; j < " + sanitizedName +
                        ".Length; j++) { " + sanitizedName + "[j].ToBytes(bytes, ref i); }");
                }
                else if (block.Count == 1)
                {
                    writer.WriteLine("            " + sanitizedName + ".ToBytes(bytes, ref i);");
                }
                else
                {
                    // Multiple count block
                    writer.WriteLine("            for (int j = 0; j < " + block.Count +
                        "; j++) { " + sanitizedName + "[j].ToBytes(bytes, ref i); }");
                }
            }

            writer.WriteLine("            if (Header.AckList != null && Header.AckList.Length > 0) { Header.AcksToBytes(bytes, ref i); }");
            writer.WriteLine("            return bytes;" + Environment.NewLine + "        }" + Environment.NewLine);

            #endregion ToBytes() Function

            WriteToBytesMultiple(writer, packet);

            writer.WriteLine("    }" + Environment.NewLine);
        }

        private static void WriteToBytesMultiple(TextWriter writer, MapPacket packet)
        {
            writer.WriteLine(
                "        public override byte[][] ToBytesMultiple()" + Environment.NewLine +
                "        {");

            // Check if there are any variable blocks
            var hasVariable = false;
            var cannotSplit = false;
            foreach (var block in packet.Blocks)
            {
                if (block.Count == -1)
                {
                    hasVariable = true;
                }
                else if (hasVariable)
                {
                    // A fixed or single block showed up after a variable count block.
                    // Our automatic splitting algorithm won't work for this packet
                    cannotSplit = true;
                    break;
                }
            }

            if (hasVariable && !cannotSplit)
            {
                writer.WriteLine(
                    "            System.Collections.Generic.List<byte[]> packets = new System.Collections.Generic.List<byte[]>();");
                writer.WriteLine(
                    "            int i = 0;");
                writer.Write(
                    "            int fixedLength = ");
                if (packet.Frequency == PacketFrequency.Low) { writer.WriteLine("10;"); }
                else if (packet.Frequency == PacketFrequency.Medium) { writer.WriteLine("8;"); }
                else { writer.WriteLine("7;"); }
                writer.WriteLine();

                // ACK serialization
                writer.WriteLine("            byte[] ackBytes = null;");
                writer.WriteLine("            int acksLength = 0;");
                writer.WriteLine("            if (Header.AckList != null && Header.AckList.Length > 0) {");
                writer.WriteLine("                Header.AppendedAcks = true;");
                writer.WriteLine("                ackBytes = new byte[Header.AckList.Length * 4 + 1];");
                writer.WriteLine("                Header.AcksToBytes(ackBytes, ref acksLength);");
                writer.WriteLine("            }");
                writer.WriteLine();

                // Count fixed blocks
                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == 1)
                    {
                        // Single count block
                        writer.WriteLine("            fixedLength += " + sanitizedName + ".Length;");
                    }
                    else if (block.Count > 0)
                    {
                        // Fixed count block
                        writer.WriteLine("            for (int j = 0; j < " + block.Count + "; j++) { fixedLength += " + sanitizedName + "[j].Length; }");
                    }
                }

                // Serialize fixed blocks
                writer.WriteLine(
                    "            byte[] fixedBytes = new byte[fixedLength];");
                writer.WriteLine(
                    "            Header.ToBytes(fixedBytes, ref i);");
                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == 1)
                    {
                        // Single count block
                        writer.WriteLine("            " + sanitizedName + ".ToBytes(fixedBytes, ref i);");
                    }
                    else if (block.Count > 0)
                    {
                        // Fixed count block
                        writer.WriteLine("            for (int j = 0; j < " + block.Count + "; j++) { " + sanitizedName + "[j].ToBytes(fixedBytes, ref i); }");
                    }
                }

                var variableCountBlock = 0;
                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        // Variable count block
                        ++variableCountBlock;
                    }
                }
                writer.WriteLine("            fixedLength += " + variableCountBlock + ";");
                writer.WriteLine();

                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        // Variable count block
                        writer.WriteLine("            int " + sanitizedName + "Start = 0;");
                    }
                }

                writer.WriteLine("            do");
                writer.WriteLine("            {");

                // Count how many variable blocks can go in this packet
                writer.WriteLine("                int variableLength = 0;");

                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        // Variable count block
                        writer.WriteLine("                int " + sanitizedName + "Count = 0;");
                    }
                }
                writer.WriteLine();

                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        // Variable count block
                        writer.WriteLine("                i = " + sanitizedName + "Start;");
                        writer.WriteLine("                while (fixedLength + variableLength + acksLength < Packet.MTU && i < " + sanitizedName + ".Length) {");
                        writer.WriteLine("                    int blockLength = " + sanitizedName + "[i].Length;");
                        writer.WriteLine("                    if (fixedLength + variableLength + blockLength + acksLength <= MTU || i == " + sanitizedName + "Start) {");
                        writer.WriteLine("                        variableLength += blockLength;");
                        writer.WriteLine("                        ++" + sanitizedName + "Count;");
                        writer.WriteLine("                    }");
                        writer.WriteLine("                    else { break; }");
                        writer.WriteLine("                    ++i;");
                        writer.WriteLine("                }");
                        writer.WriteLine();
                    }
                }

                // Create the packet
                writer.WriteLine("                byte[] packet = new byte[fixedLength + variableLength + acksLength];");
                writer.WriteLine("                int length = fixedBytes.Length;");
                writer.WriteLine("                Buffer.BlockCopy(fixedBytes, 0, packet, 0, length);");
                // Remove the appended ACKs flag from subsequent packets
                writer.WriteLine("                if (packets.Count > 0) { packet[0] = (byte)(packet[0] & ~0x10); }");
                writer.WriteLine();

                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        writer.WriteLine("                packet[length++] = (byte)" + sanitizedName + "Count;");
                        writer.WriteLine("                for (i = " + sanitizedName + "Start; i < " + sanitizedName + "Start + "
                            + sanitizedName + "Count; i++) { " + sanitizedName + "[i].ToBytes(packet, ref length); }");
                        writer.WriteLine("                " + sanitizedName + "Start += " + sanitizedName + "Count;");
                        writer.WriteLine();
                    }
                }

                // ACK appending
                writer.WriteLine("                if (acksLength > 0) {");
                writer.WriteLine("                    Buffer.BlockCopy(ackBytes, 0, packet, length, acksLength);");
                writer.WriteLine("                    acksLength = 0;");
                writer.WriteLine("                }");
                writer.WriteLine();

                writer.WriteLine("                packets.Add(packet);");
                
                writer.WriteLine("            } while (");
                var first = true;
                foreach (var block in packet.Blocks)
                {
                    string sanitizedName;
                    if (block.Name == "Header") { sanitizedName = "_" + block.Name; }
                    else { sanitizedName = block.Name; }

                    if (block.Count == -1)
                    {
                        if (first) first = false;
                        else writer.WriteLine(" ||");

                        // Variable count block
                        writer.Write("                " + sanitizedName + "Start < " + sanitizedName + ".Length");
                    }
                }
                writer.WriteLine(");");
                writer.WriteLine();
                writer.WriteLine("            return packets.ToArray();");
                writer.WriteLine("        }");
            }
            else
            {
                writer.WriteLine("            return new byte[][] { ToBytes() };");
                writer.WriteLine("        }");
            }
        }

        private static int Main(string[] args)
        {
            ProtocolManager protocol;
            var unused = new List<string>();
            StreamWriter writer;

            try
            {
                if (args.Length != 4)
                {
                    Console.WriteLine("Usage: [message_template.msg] [template.cs] [unusedpackets.txt] [_Packets_.cs]");
                    return -1;
                }

                writer = new StreamWriter(args[3]);
                protocol = new ProtocolManager(args[0]);

                // Build a list of unused packets
                using (var unusedReader = new StreamReader(args[2]))
                {
                    while (unusedReader.Peek() >= 0)
                    {
                        unused.Add(unusedReader.ReadLine()?.Trim());
                    }
                }

                // Read in the template.cs file and write it to our output
                var reader = new StreamReader(args[1]);
                writer.WriteLine(reader.ReadToEnd());
                reader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return -2;
            }


            // Prune all the unused packets out of the protocol
            var i = 0;
            foreach (var packet in protocol.LowMaps)
            {
                if (packet != null && unused.Contains(packet.Name))
                    protocol.LowMaps[i] = null;
                i++;
            }
            i = 0;
            foreach (var packet in protocol.MediumMaps)
            {
                if (packet != null && unused.Contains(packet.Name))
                    protocol.MediumMaps[i] = null;
                i++;
            }
            i = 0;
            foreach (var packet in protocol.HighMaps)
            {
                if (packet != null && unused.Contains(packet.Name))
                    protocol.HighMaps[i] = null;
                i++;
            }


            // Write the PacketType enum
            writer.WriteLine("    public enum PacketType" + Environment.NewLine + "    {" + Environment.NewLine +
                "        /// <summary>A generic value, not an actual packet type</summary>" + Environment.NewLine +
                "        Default,");
            foreach (var packet in protocol.LowMaps)
                if (packet != null)
                    writer.WriteLine("        " + packet.Name + " = " + (0x10000 | packet.ID) + ",");
            foreach (var packet in protocol.MediumMaps)
                if (packet != null)
                    writer.WriteLine("        " + packet.Name + " = " + (0x20000 | packet.ID) + ",");
            foreach (var packet in protocol.HighMaps)
                if (packet != null)
                    writer.WriteLine("        " + packet.Name + " = " + (0x30000 | packet.ID) + ",");
            writer.WriteLine("    }" + Environment.NewLine);

            // Write the base Packet class
            writer.WriteLine(
                "    public abstract partial class Packet" + Environment.NewLine + "    {" + Environment.NewLine +
                "        public const int MTU = 1200;" + Environment.NewLine +
                Environment.NewLine +
                "        public Header Header;" + Environment.NewLine +
                "        public bool HasVariableBlocks;" + Environment.NewLine +
                "        public PacketType Type;" + Environment.NewLine +
                "        public abstract int Length { get; }" + Environment.NewLine +
                "        public abstract void FromBytes(byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer);" + Environment.NewLine +
                "        public abstract void FromBytes(Header header, byte[] bytes, ref int i, ref int packetEnd);" + Environment.NewLine +
                "        public abstract byte[] ToBytes();" + Environment.NewLine +
                "        public abstract byte[][] ToBytesMultiple();"
            );
            writer.WriteLine();

            // Write the Packet.GetType() function
            writer.WriteLine(
                "        public static PacketType GetType(ushort id, PacketFrequency frequency)" + Environment.NewLine +
                "        {" + Environment.NewLine +
                "            switch (frequency)" + Environment.NewLine +
                "            {" + Environment.NewLine +
                "                case PacketFrequency.Low:" + Environment.NewLine +
                "                    switch (id)" + Environment.NewLine +
                "                    {");
            foreach (var packet in protocol.LowMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return PacketType." + packet.Name + ";");
            writer.WriteLine("                    }" + Environment.NewLine +
                "                    break;" + Environment.NewLine +
                "                case PacketFrequency.Medium:" + Environment.NewLine +
                "                    switch (id)" + Environment.NewLine + "                    {");
            foreach (var packet in protocol.MediumMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return PacketType." + packet.Name + ";");
            writer.WriteLine("                    }" + Environment.NewLine +
                "                    break;" + Environment.NewLine +
                "                case PacketFrequency.High:" + Environment.NewLine +
                "                    switch (id)" + Environment.NewLine + "                    {");
            foreach (var packet in protocol.HighMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return PacketType." + packet.Name + ";");
            writer.WriteLine("                    }" + Environment.NewLine +
                "                    break;" + Environment.NewLine + "            }" + Environment.NewLine + Environment.NewLine +
                "            return PacketType.Default;" + Environment.NewLine + "        }" + Environment.NewLine);

            // Write the Packet.BuildPacket(PacketType) function
            writer.WriteLine("        public static Packet BuildPacket(PacketType type)");
            writer.WriteLine("        {");
            foreach (var packet in protocol.HighMaps)
                if (packet != null)
                    writer.WriteLine("            if(type == PacketType." + packet.Name + ") return new " + packet.Name + "Packet();");
            foreach (var packet in protocol.MediumMaps)
                if (packet != null)
                    writer.WriteLine("            if(type == PacketType." + packet.Name + ") return new " + packet.Name + "Packet();");
            foreach (var packet in protocol.LowMaps)
                if (packet != null)
                    writer.WriteLine("            if(type == PacketType." + packet.Name + ") return new " + packet.Name + "Packet();");
            writer.WriteLine("            return null;" + Environment.NewLine);
            writer.WriteLine("        }");

            // Write the Packet.BuildPacket() function
            writer.WriteLine(@"
        public static Packet BuildPacket(byte[] packetBuffer, ref int packetEnd, byte[] zeroBuffer)
        {
            byte[] bytes;
            int i = 0;
            Header header = Header.BuildHeader(packetBuffer, ref i, ref packetEnd);
            if (header.Zerocoded)
            {
                packetEnd = Helpers.ZeroDecode(packetBuffer, packetEnd + 1, zeroBuffer) - 1;
                bytes = zeroBuffer;
            }
            else
            {
                bytes = packetBuffer;
            }
            Array.Clear(bytes, packetEnd + 1, bytes.Length - packetEnd - 1);

            switch (header.Frequency)
            {
                case PacketFrequency.Low:
                    switch (header.ID)
                    {");
            foreach (var packet in protocol.LowMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return new " + packet.Name + "Packet(header, bytes, ref i);");
            writer.WriteLine(@"
                    }
                    break;
                case PacketFrequency.Medium:
                    switch (header.ID)
                    {");
            foreach (var packet in protocol.MediumMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return new " + packet.Name + "Packet(header, bytes, ref i);");
            writer.WriteLine(@"
                    }
                    break;
                case PacketFrequency.High:
                    switch (header.ID)
                    {");
            foreach (var packet in protocol.HighMaps)
                if (packet != null)
                    writer.WriteLine("                        case " + packet.ID + ": return new " + packet.Name + "Packet(header, bytes, ref i);");
            writer.WriteLine(@"
                    }
                    break;
            }

            throw new MalformedDataException(""Unknown packet ID "" + header.Frequency + "" "" + header.ID);
        }
    }");

            // Write the packet classes
            foreach (var packet in protocol.LowMaps)
                if (packet != null) { WritePacketClass(writer, packet); }
            foreach (var packet in protocol.MediumMaps)
                if (packet != null) { WritePacketClass(writer, packet); }
            foreach (var packet in protocol.HighMaps)
                if (packet != null) { WritePacketClass(writer, packet); }


            // Finish up
            writer.WriteLine("}");
            writer.Close();
            return 0;
        }
    }
}
