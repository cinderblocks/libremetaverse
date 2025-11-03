/*
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

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PacketSourceGenerator
{
    [Generator]
    public class PacketSourceGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor MissingTemplate = new(
            "PG001",
            "message_template.msg not found",
            "No AdditionalFile named 'message_template.msg' was found. Add it under <AdditionalFiles> in the project file.",
            "PacketGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var templateFiles = context.AdditionalTextsProvider
                .Where(at => Path.GetFileName(at.Path).Equals("message_template.msg", StringComparison.OrdinalIgnoreCase));

            var parsed = templateFiles.Select((at, ct) =>
            {
                var text = at.GetText(ct);
                return text?.ToString();
            });

            // If no file found, produce a diagnostic; we still register but generation will be skipped.
            context.RegisterSourceOutput(parsed.Collect(), (spc, files) =>
            {
                if (files == null || files.Length == 0 || string.IsNullOrWhiteSpace(files[0]))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MissingTemplate, Location.None));
                    return;
                }

                var content = files[0];
                var protocol = ProtocolParser.Parse(content!);
                var source = GeneratedSourceBuilder.Build(protocol);
                spc.AddSource("Packets.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }
    }

    // --- Simple model / parser ---
    internal enum PacketFrequency { Low, Medium, High }

    internal enum FieldType
    {
        U8, U16, U32, U64, S8, S16, S32, F32, F64,
        LLUUID, BOOL, LLVector3, LLVector3d, LLVector4, LLQuaternion,
        IPADDR, IPPORT, Variable, Fixed, Single, Multiple
    }

    internal class MapField { public string Name = ""; public FieldType Type; public int Count; }
    internal class MapBlock { public string Name = ""; public int Count; public List<MapField> Fields = []; }
    internal class MapPacket { public ushort ID; public string Name = ""; public PacketFrequency Frequency; public bool Trusted; public bool Encoded; public List<MapBlock> Blocks =
        []; }

    internal class ParsedProtocol
    {
        public List<MapPacket> LowMaps { get; } = [];
        public List<MapPacket> MediumMaps { get; } = [];
        public List<MapPacket> HighMaps { get; } = [];
    }

    internal static class ProtocolParser
    {
        public static ParsedProtocol Parse(string content)
        {
            var protocol = new ParsedProtocol();
            if (string.IsNullOrEmpty(content)) return protocol;

            var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
            bool inPacket = false, inBlock = false;
            MapPacket? currentPacket = null;
            MapBlock? currentBlock = null;
            var trimChars = new char[] { ' ', '\t' };

            foreach (var newline in lines)
            {
                var trimmed = System.Text.RegularExpressions.Regex.Replace(newline, @"\s+", " ").Trim(trimChars);
                if (!inPacket)
                {
                    if (trimmed == "{") { inPacket = true; }
                    continue;
                }

                if (!inBlock)
                {
                    if (trimmed == "{") { inBlock = true; continue; }
                    if (trimmed == "}") { inPacket = false; currentPacket = null; continue; }
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//")) continue;

                    var tokens = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 2) continue;

                    var name = tokens[0];
                    var freqToken = tokens[1];
                    // ID may be at tokens[2] or tokens[3] depending on line form; find first token that looks like a number or 0x...
                    string idToken = tokens.Skip(2).FirstOrDefault(t => t.Length > 0 && (char.IsDigit(t[0]) || t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))) ?? "0";
                    string trusted = tokens.Skip(2).FirstOrDefault(t => t == "Trusted" || t == "NotTrusted") ?? "NotTrusted";
                    string encoded = tokens.FirstOrDefault(t => t == "Zerocoded" || t == "Unencoded") ?? "Unencoded";

                    uint packetID = 0;
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        if (idToken.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!uint.TryParse(idToken.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out packetID)) packetID = 0;
                        }
                        else
                        {
                            uint.TryParse(idToken, out packetID);
                        }
                    }

                    var packet = new MapPacket
                    {
                        ID = (ushort)(packetID & 0xFFFF),
                        Name = name,
                        Trusted = (trusted == "Trusted"),
                        Encoded = (encoded == "Zerocoded")
                    };

                    if (freqToken == "Fixed" || freqToken == "Low") { packet.Frequency = PacketFrequency.Low; protocol.LowMaps.Add(packet); }
                    else if (freqToken == "Medium" || freqToken == "Mid") { packet.Frequency = PacketFrequency.Medium; protocol.MediumMaps.Add(packet); }
                    else if (freqToken == "High") { packet.Frequency = PacketFrequency.High; protocol.HighMaps.Add(packet); }
                    else { packet.Frequency = PacketFrequency.Low; protocol.LowMaps.Add(packet); }

                    currentPacket = packet;
                }
                else
                {
                    if (trimmed.StartsWith("{"))
                    {
                        // field line: { Name Type [Count] }
                        var tokens = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 3 && currentBlock != null)
                        {
                            var f = new MapField
                            {
                                Name = tokens[1],
                                Type = Enum.TryParse<FieldType>(tokens[2], true, out var ft) ? ft : FieldType.Variable
                            };

                            if (tokens.Length > 3 && tokens[3] != "}") { if (!int.TryParse(tokens[3], out var c)) c = 1; f.Count = c; } else f.Count = 1;
                            currentBlock.Fields.Add(f);
                        }
                    }
                    else if (trimmed == "}")
                    {
                        inBlock = false;
                        currentBlock = null;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("//"))
                    {
                        // block header: Name Single|Multiple N|Variable
                        var tokens = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        currentBlock = new MapBlock { Name = tokens[0] };
                        if (tokens.Length > 1)
                        {
                            currentBlock.Count = tokens[1] switch
                            {
                                "Single" => 1,
                                "Variable" => -1,
                                "Multiple" when tokens.Length > 2 && int.TryParse(tokens[2], out var m) => m,
                                _ => 1
                            };
                        }
                        currentPacket?.Blocks.Add(currentBlock);
                    }
                }
            }

            return protocol;
        }
    }

    // --- Code generator for the parsed protocol ---
    internal static class GeneratedSourceBuilder
    {
        // small helpers to keep emission readable
        private static void Append(StringBuilder sb, int indent, string line)
            => sb.Append(new string(' ', indent * 4)).AppendLine(line);

        private static void AppendLines(StringBuilder sb, int indent, IEnumerable<string> lines)
        {
            foreach (var l in lines) Append(sb, indent, l);
        }

        public static string Build(ParsedProtocol protocol)
        {
            var sb = new StringBuilder();

            Append(sb, 0, "// <auto-generated/>");
            Append(sb, 0, "using System;");
            Append(sb, 0, "using System.Collections.Generic;");
            Append(sb, 0, "using OpenMetaverse;");
            Append(sb, 0, "using OpenMetaverse.Interfaces;");
            Append(sb, 0, "");
            Append(sb, 0, "#pragma warning disable CS0168");
            Append(sb, 0, "namespace OpenMetaverse.Packets");
            Append(sb, 0, "{");

            // GeneratedEnsure
            Append(sb, 1, "internal static class GeneratedEnsure");
            Append(sb, 1, "{");
            Append(sb, 2, "public static void EnsureRemaining(byte[] bytes, int i, int need, string field)");
            Append(sb, 2, "{");
            Append(sb, 3, "if (bytes.Length - i < need) throw new InvalidOperationException($\"ToBytes overflow writing {field}: need {need} bytes but have {bytes.Length - i}\");");
            Append(sb, 2, "}");
            Append(sb, 1, "}");
            Append(sb, 0, "");

            // PacketType
            Append(sb, 1, "public enum PacketType");
            Append(sb, 1, "{");
            Append(sb, 2, "Default,");
            foreach (var p in protocol.LowMaps) Append(sb, 2, $"{SafeName(p.Name)} = {0x10000 | p.ID},");
            foreach (var p in protocol.MediumMaps) Append(sb, 2, $"{SafeName(p.Name)} = {0x20000 | p.ID},");
            foreach (var p in protocol.HighMaps) Append(sb, 2, $"{SafeName(p.Name)} = {0x30000 | p.ID},");
            Append(sb, 1, "}");
            Append(sb, 0, "");

            // Packet base class (minimal)
            Append(sb, 1, "public abstract partial class Packet");
            Append(sb, 1, "{");
            Append(sb, 2, "public const int MTU = 1200;");
            Append(sb, 2, "public Header Header;");
            Append(sb, 2, "public bool HasVariableBlocks;");
            Append(sb, 2, "public PacketType Type;");
            Append(sb, 2, "public abstract int Length { get; }");
            Append(sb, 2, "public virtual bool UsesBufferPooling { get { return false; } }");
            Append(sb, 2, "");
            Append(sb, 2, "public abstract void FromBytes(byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer);");
            Append(sb, 2, "public abstract void FromBytes(Header header, byte[] bytes, ref int i, ref int packetEnd);");
            Append(sb, 2, "public abstract byte[] ToBytes();");
            Append(sb, 2, "public abstract byte[][] ToBytesMultiple();");
            Append(sb, 2, "");
            Append(sb, 2, "public virtual byte[] ToBytes(IByteBufferPool pool, ref int size) => ToBytes();");
            Append(sb, 2, "public virtual byte[][] ToBytesMultiple(IByteBufferPool pool, out int[] sizes)");
            Append(sb, 2, "{");
            Append(sb, 3, "var packets = ToBytesMultiple();");
            Append(sb, 3, "sizes = new int[packets.Length];");
            Append(sb, 3, "for (int j = 0; j < packets.Length; j++) sizes[j] = packets[j].Length;");
            Append(sb, 3, "return packets;");
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // GetType, BuildPacket utilities (kept straightforward)
            Append(sb, 2, "public static PacketType GetType(ushort id, PacketFrequency frequency)");
            Append(sb, 2, "{");
            Append(sb, 3, "switch (frequency)");
            Append(sb, 3, "{");
            Append(sb, 4, "case PacketFrequency.Low:");
            Append(sb, 4, "switch (id)");
            Append(sb, 4, "{");
            foreach (var p in protocol.LowMaps) Append(sb, 5, $"case {p.ID}: return PacketType.{SafeName(p.Name)};");
            Append(sb, 4, "}");
            Append(sb, 4, "break;");
            Append(sb, 4, "case PacketFrequency.Medium:");
            Append(sb, 4, "switch (id)");
            Append(sb, 4, "{");
            foreach (var p in protocol.MediumMaps) Append(sb, 5, $"case {p.ID}: return PacketType.{SafeName(p.Name)};");
            Append(sb, 4, "}");
            Append(sb, 4, "break;");
            Append(sb, 4, "case PacketFrequency.High:");
            Append(sb, 4, "switch (id)");
            Append(sb, 4, "{");
            foreach (var p in protocol.HighMaps) Append(sb, 5, $"case {p.ID}: return PacketType.{SafeName(p.Name)};");
            Append(sb, 4, "}");
            Append(sb, 4, "break;");
            Append(sb, 3, "}");
            Append(sb, 3, "return PacketType.Default;");
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // BuildPacket(PacketType)
            Append(sb, 2, "public static Packet BuildPacket(PacketType type)");
            Append(sb, 2, "{");
            foreach (var p in protocol.HighMaps) Append(sb, 3, $"if (type == PacketType.{SafeName(p.Name)}) return new {SafeName(p.Name)}Packet();");
            foreach (var p in protocol.MediumMaps) Append(sb, 3, $"if (type == PacketType.{SafeName(p.Name)}) return new {SafeName(p.Name)}Packet();");
            foreach (var p in protocol.LowMaps) Append(sb, 3, $"if (type == PacketType.{SafeName(p.Name)}) return new {SafeName(p.Name)}Packet();");
            Append(sb, 3, "return null;");
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // BuildPacket by buffer (header-first) - kept as before
            Append(sb, 2, "public static Packet BuildPacket(byte[] packetBuffer, ref int packetEnd, byte[] zeroBuffer)");
            Append(sb, 2, "{");
            Append(sb, 3, "byte[] bytes;");
            Append(sb, 3, "int i = 0;");
            Append(sb, 3, "Header header = Header.BuildHeader(packetBuffer, ref i, ref packetEnd);");
            Append(sb, 3, "if (header.Zerocoded)");
            Append(sb, 3, "{");
            Append(sb, 4, "packetEnd = Helpers.ZeroDecode(packetBuffer, packetEnd + 1, zeroBuffer) - 1;");
            Append(sb, 4, "bytes = zeroBuffer;");
            Append(sb, 3, "}");
            Append(sb, 3, "else");
            Append(sb, 3, "{");
            Append(sb, 4, "bytes = packetBuffer;");
            Append(sb, 3, "}");
            Append(sb, 3, "Array.Clear(bytes, packetEnd + 1, bytes.Length - packetEnd - 1);");
            Append(sb, 3, "");
            Append(sb, 3, "switch (header.Frequency)");
            Append(sb, 3, "{");
            Append(sb, 4, "case PacketFrequency.Low:");
            Append(sb, 5, "switch (header.ID)");
            Append(sb, 5, "{");
            foreach (var p in protocol.LowMaps) Append(sb, 6, $"case {p.ID}: return new {SafeName(p.Name)}Packet(header, bytes, ref i);");
            Append(sb, 5, "}");
            Append(sb, 5, "break;");
            Append(sb, 4, "case PacketFrequency.Medium:");
            Append(sb, 5, "switch (header.ID)");
            Append(sb, 5, "{");
            foreach (var p in protocol.MediumMaps) Append(sb, 6, $"case {p.ID}: return new {SafeName(p.Name)}Packet(header, bytes, ref i);");
            Append(sb, 5, "}");
            Append(sb, 5, "break;");
            Append(sb, 4, "case PacketFrequency.High:");
            Append(sb, 5, "switch (header.ID)");
            Append(sb, 5, "{");
            foreach (var p in protocol.HighMaps) Append(sb, 6, $"case {p.ID}: return new {SafeName(p.Name)}Packet(header, bytes, ref i);");
            Append(sb, 5, "}");
            Append(sb, 5, "break;");
            Append(sb, 3, "}");
            Append(sb, 3, "throw new MalformedDataException(\"Unknown packet ID \" + header.Frequency + \" \" + header.ID);");
            Append(sb, 2, "}");

            Append(sb, 1, "}");
            Append(sb, 0, "");

            // Emit packets
            foreach (var p in protocol.LowMaps) EmitPacket(sb, p);
            foreach (var p in protocol.MediumMaps) EmitPacket(sb, p);
            foreach (var p in protocol.HighMaps) EmitPacket(sb, p);

            Append(sb, 0, "#pragma warning restore CS0168");
            Append(sb, 0, "}");
            return sb.ToString();
        }

        private static void EmitPacket(StringBuilder sb, MapPacket packet)
        {
            var className = SafeName(packet.Name) + "Packet";
            Append(sb, 1, $"public sealed class {className} : Packet");
            Append(sb, 1, "{");

            // nested blocks
            foreach (var b in packet.Blocks) EmitBlock(sb, b);

            // Length property
            Append(sb, 2, "public override int Length");
            Append(sb, 2, "{");
            Append(sb, 3, "get");
            Append(sb, 3, "{");
            var baseLen = packet.Frequency == PacketFrequency.Low ? 10 : packet.Frequency == PacketFrequency.Medium ? 8 : 7;
            int varBlockCount = packet.Blocks.Count(b => b.Count == -1);
            Append(sb, 4, $"int length = {baseLen + (varBlockCount > 0 ? varBlockCount : 0)};");
            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == -1)
                {
                    Append(sb, 4, $"if ({n} != null) {{ length += 1; for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) length += {n}[j].Length; }} else {{ length += 1; }}");
                }
                else if (block.Count == 1)
                {
                    Append(sb, 4, $"if ({n} != null) length += {n}.Length;");
                }
                else
                {
                    Append(sb, 4, $"if ({n} != null) for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) length += {n}[j].Length;");
                }
            }
            Append(sb, 4, "return length;");
            Append(sb, 3, "}");
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // members + ctor
            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                Append(sb, 2, $"public {block.Name}Block{(block.Count != 1 ? "[]" : "")} {n};");
            }
            Append(sb, 2, "");
            Append(sb, 2, $"public {className}()");
            Append(sb, 2, "{");
            Append(sb, 3, $"HasVariableBlocks = {(packet.Blocks.Any(b => b.Count == -1) ? "true" : "false")};");
            Append(sb, 3, $"Type = PacketType.{SafeName(packet.Name)};");
            Append(sb, 3, "Header = new Header();");
            Append(sb, 3, $"Header.Frequency = PacketFrequency.{packet.Frequency};");
            Append(sb, 3, $"Header.ID = {packet.ID};");
            Append(sb, 3, "Header.Reliable = true;");
            Append(sb, 3, $"Header.Zerocoded = {(packet.Encoded ? "true" : "false")};");

            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == 1)
                    Append(sb, 3, $"{n} = new {block.Name}Block();");
                else if (block.Count == -1)
                    Append(sb, 3, $"{n} = Array.Empty<{block.Name}Block>();");
                else
                {
                    Append(sb, 3, $"{n} = new {block.Name}Block[{block.Count}];");
                    Append(sb, 3, $"for (int j = 0; j < {block.Count}; j++) {n}[j] = new {block.Name}Block();");
                }
            }
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // convenience constructors and From/ToBytes (kept simple and consistent)
            Append(sb, 2, $"public {className}(byte[] bytes, ref int i) : this() {{ int packetEnd = bytes.Length - 1; FromBytes(bytes, ref i, ref packetEnd, null); }}");
            Append(sb, 2, $"public {className}(Header head, byte[] bytes, ref int i) : this() {{ int packetEnd = bytes.Length - 1; FromBytes(head, bytes, ref i, ref packetEnd); }}");
            Append(sb, 2, "");

            // FromBytes variants
            Append(sb, 2, "public override void FromBytes(byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer)");
            Append(sb, 2, "{");
            Append(sb, 3, "int count;");
            Append(sb, 3, "Header.FromBytes(bytes, ref i, ref packetEnd);");
            Append(sb, 3, "if (Header.Zerocoded && zeroBuffer != null) { packetEnd = Helpers.ZeroDecode(bytes, packetEnd + 1, zeroBuffer) - 1; bytes = zeroBuffer; }");

            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == 1)
                {
                    Append(sb, 3, $"if ({n} == null) {n} = new {block.Name}Block();");
                    Append(sb, 3, $"{n}.FromBytes(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    Append(sb, 3, "count = (int)bytes[i++];");
                    Append(sb, 3, $"if ({n} == null || {n}.Length != count) {{ {n} = new {block.Name}Block[count]; for (int j = 0; j < count; j++) {n}[j] = new {block.Name}Block(); }}");
                    Append(sb, 3, $"for (int j = 0; j < count; j++) {n}[j].FromBytes(bytes, ref i);");
                }
                else
                {
                    Append(sb, 3, $"if ({n} == null || {n}.Length != {block.Count}) {{ {n} = new {block.Name}Block[{block.Count}]; for (int j = 0; j < {block.Count}; j++) {n}[j] = new {block.Name}Block(); }}");
                    Append(sb, 3, $"for (int j = 0; j < {block.Count}; j++) {n}[j].FromBytes(bytes, ref i);");
                }
            }

            Append(sb, 2, "}");
            Append(sb, 2, "");

            Append(sb, 2, "public override void FromBytes(Header header, byte[] bytes, ref int i, ref int packetEnd)");
            Append(sb, 2, "{");
            Append(sb, 3, "int count;");
            Append(sb, 3, "Header = header;");
            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == 1)
                {
                    Append(sb, 3, $"if ({n} == null) {n} = new {block.Name}Block();");
                    Append(sb, 3, $"{n}.FromBytes(bytes, ref i);");
                }
                else if (block.Count == -1)
                {
                    Append(sb, 3, "count = (int)bytes[i++];");
                    Append(sb, 3, $"if ({n} == null || {n}.Length != count) {{ {n} = new {block.Name}Block[count]; for (int j = 0; j < count; j++) {n}[j] = new {block.Name}Block(); }}");
                    Append(sb, 3, $"for (int j = 0; j < count; j++) {n}[j].FromBytes(bytes, ref i);");
                }
                else
                {
                    Append(sb, 3, $"if ({n} == null || {n}.Length != {block.Count}) {{ {n} = new {block.Name}Block[{block.Count}]; for (int j = 0; j < {block.Count}; j++) {n}[j] = new {block.Name}Block(); }}");
                    Append(sb, 3, $"for (int j = 0; j < {block.Count}; j++) {n}[j].FromBytes(bytes, ref i);");
                }
            }
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // ToBytes (single packet) - the generator already wrote correct guarded code; keep compact
            Append(sb, 2, "public override byte[] ToBytes()");
            Append(sb, 2, "{");
            Append(sb, 3, $"int length = {(packet.Frequency == PacketFrequency.Low ? 10 : packet.Frequency == PacketFrequency.Medium ? 8 : 7)};");
            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == 1)
                    Append(sb, 3, $"if ({n} != null) length += {n}.Length;");
                else if (block.Count == -1)
                    Append(sb, 3, $"if ({n} != null) {{ length += 1; for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) length += {n}[j].Length; }} else {{ length += 1; }}");
                else
                    Append(sb, 3, $"if ({n} != null) for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) length += {n}[j].Length;");
            }
            Append(sb, 3, "if (Header.AckList != null && Header.AckList.Length > 0) length += Header.AckList.Length * 4 + 1;");
            Append(sb, 3, "byte[] bytes = new byte[length];");
            Append(sb, 3, "int i = 0;");
            Append(sb, 3, "Header.ToBytes(bytes, ref i);");
            foreach (var block in packet.Blocks)
            {
                var n = block.Name == "Header" ? "_" + block.Name : block.Name;
                if (block.Count == -1)
                {
                    Append(sb, 3, $"if ({n} != null) {{ bytes[i++] = (byte){n}.Length; for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) {n}[j].ToBytes(bytes, ref i); }} else {{ bytes[i++] = 0; }}");
                }
                else if (block.Count == 1)
                {
                    Append(sb, 3, $"if ({n} != null) {n}.ToBytes(bytes, ref i);");
                }
                else
                {
                    Append(sb, 3, $"if ({n} != null) {{ for (int j = 0; j < {n}.Length; j++) if ({n}[j] != null) {n}[j].ToBytes(bytes, ref i); }}");
                }
            }
            Append(sb, 3, "if (Header.AckList != null && Header.AckList.Length > 0) Header.AcksToBytes(bytes, ref i);");
            Append(sb, 3, "return bytes;");
            Append(sb, 2, "}");
            Append(sb, 2, "");

            // ToBytesMultiple (may split packets if variable-count blocks present and safe to split)
            {
                // Decide at generation time whether packet has variable blocks and whether splitting is possible
                bool hasVariable = packet.Blocks.Any(b => b.Count == -1);
                bool cannotSplit = false;
                if (hasVariable)
                {
                    bool seenVariable = false;
                    foreach (var b in packet.Blocks)
                    {
                        if (b.Count == -1) seenVariable = true;
                        else if (seenVariable)
                        {
                            // fixed or single block appears after a variable block => cannot split
                            cannotSplit = true;
                            break;
                        }
                    }
                }

                if (hasVariable && !cannotSplit)
                {
                    Append(sb, 2, "public override byte[][] ToBytesMultiple()");
                    Append(sb, 2, "{");
                    Append(sb, 3, "System.Collections.Generic.List<byte[]> packets = new System.Collections.Generic.List<byte[]>();");
                    Append(sb, 3, "int i = 0;");
                    Append(sb, 3, $"int fixedLength = {(packet.Frequency == PacketFrequency.Low ? 10 : packet.Frequency == PacketFrequency.Medium ? 8 : 7)};");
                    Append(sb, 3, "");
                    Append(sb, 3, "// ACK serialization");
                    Append(sb, 3, "byte[] ackBytes = null;");
                    Append(sb, 3, "int acksLength = 0;");
                    Append(sb, 3, "if (Header.AckList != null && Header.AckList.Length > 0) {");
                    Append(sb, 4, "Header.AppendedAcks = true;");
                    Append(sb, 4, "ackBytes = new byte[Header.AckList.Length * 4 + 1];");
                    Append(sb, 4, "Header.AcksToBytes(ackBytes, ref acksLength);");
                    Append(sb, 3, "}");
                    Append(sb, 3, "");

                    // Count fixed blocks into fixedLength
                    foreach (var block in packet.Blocks)
                    {
                        var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                        if (block.Count == 1)
                        {
                            Append(sb, 3, $"fixedLength += {sanitizedName}.Length;");
                        }
                        else if (block.Count > 0)
                        {
                            Append(sb, 3, $"for (int j = 0; j < {block.Count}; j++) {{ fixedLength += {sanitizedName}[j].Length; }}");
                        }
                    }

                    // Serialize fixed blocks into fixedBytes
                    Append(sb, 3, "byte[] fixedBytes = new byte[fixedLength];");
                    Append(sb, 3, "Header.ToBytes(fixedBytes, ref i);");
                    foreach (var block in packet.Blocks)
                    {
                        var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                        if (block.Count == 1)
                        {
                            Append(sb, 3, $"{sanitizedName}.ToBytes(fixedBytes, ref i);");
                        }
                        else if (block.Count > 0)
                        {
                            Append(sb, 3, $"for (int j = 0; j < {block.Count}; j++) {{ {sanitizedName}[j].ToBytes(fixedBytes, ref i); }}");
                        }
                    }

                    // Account for variable-count block count bytes (one byte per variable-count block)
                    var variableCountBlock = packet.Blocks.Count(b => b.Count == -1);
                    Append(sb, 3, $"fixedLength += {variableCountBlock};");
                    Append(sb, 3, "");

                    // Initialize starts for variable blocks
                    foreach (var block in packet.Blocks)
                    {
                        if (block.Count == -1)
                        {
                            var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                            Append(sb, 3, $"int {sanitizedName}Start = 0;");
                        }
                    }

                    Append(sb, 3, "do");
                    Append(sb, 3, "{");
                    Append(sb, 4, "int variableLength = 0;");

                    foreach (var block in packet.Blocks)
                    {
                        if (block.Count == -1)
                        {
                            var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                            Append(sb, 4, $"int {sanitizedName}Count = 0;");
                        }
                    }
                    Append(sb, 4, "");

                    foreach (var block in packet.Blocks)
                    {
                        if (block.Count == -1)
                        {
                            var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                            Append(sb, 4, $"i = {sanitizedName}Start;");
                            Append(sb, 4, $"while (fixedLength + variableLength + acksLength < Packet.MTU && i < {sanitizedName}.Length) {{");
                            Append(sb, 5, "int blockLength = " + sanitizedName + "[i].Length;");
                            Append(sb, 5, $"if (fixedLength + variableLength + blockLength + acksLength <= MTU || i == {sanitizedName}Start) {{");
                            Append(sb, 6, "variableLength += blockLength;");
                            Append(sb, 6, $"++{sanitizedName}Count;");
                            Append(sb, 5, "}");
                            Append(sb, 5, "else { break; }");
                            Append(sb, 5, "++i;");
                            Append(sb, 4, "}");
                            Append(sb, 4, "");
                        }
                    }

                    Append(sb, 4, "byte[] packet = new byte[fixedLength + variableLength + acksLength];");
                    Append(sb, 4, "int length = fixedBytes.Length;");
                    Append(sb, 4, "Buffer.BlockCopy(fixedBytes, 0, packet, 0, length);");
                    Append(sb, 4, "// Remove the appended ACKs flag from subsequent packets");
                    Append(sb, 4, "if (packets.Count > 0) { packet[0] = (byte)(packet[0] & ~0x10); }");
                    Append(sb, 4, "");

                    foreach (var block in packet.Blocks)
                    {
                        if (block.Count == -1)
                        {
                            var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                            Append(sb, 4, $"packet[length++] = (byte){sanitizedName}Count;");
                            Append(sb, 4, $"for (i = {sanitizedName}Start; i < {sanitizedName}Start + {sanitizedName}Count; i++) {{ {sanitizedName}[i].ToBytes(packet, ref length); }}");
                            Append(sb, 4, $"{sanitizedName}Start += {sanitizedName}Count;");
                            Append(sb, 4, "");
                        }
                    }

                    // ACK appending
                    Append(sb, 4, "if (acksLength > 0) {");
                    Append(sb, 5, "Buffer.BlockCopy(ackBytes, 0, packet, length, acksLength);");
                    Append(sb, 5, "acksLength = 0;");
                    Append(sb, 4, "}");
                    Append(sb, 4, "");

                    Append(sb, 4, "packets.Add(packet);");

                    Append(sb, 3, "}");
                    // build loop condition: any variableStart < length
                    {
                        var first = true;
                        Append(sb, 3, "while (");
                        foreach (var block in packet.Blocks)
                        {
                            if (block.Count == -1)
                            {
                                var sanitizedName = block.Name == "Header" ? "_" + block.Name : block.Name;
                                if (!first) Append(sb, 3, " ||");
                                Append(sb, 3, $"    {sanitizedName}Start < {sanitizedName}.Length");
                                first = false;
                            }
                        }
                        Append(sb, 3, ");");
                    }

                    Append(sb, 3, "");
                    Append(sb, 3, "return packets.ToArray();");
                    Append(sb, 2, "}");
                }
                else
                {
                    Append(sb, 2, "public override byte[][] ToBytesMultiple() => new byte[][] { ToBytes() };");
                }
            }

            Append(sb, 2, "public override bool UsesBufferPooling => false;");
            Append(sb, 2, "public override byte[] ToBytes(IByteBufferPool pool, ref int size) => ToBytes();");
            Append(sb, 2, "public override byte[][] ToBytesMultiple(IByteBufferPool pool, out int[] sizes)");
            Append(sb, 2, "{");
            Append(sb, 3, "var packets = ToBytesMultiple();");
            Append(sb, 3, "sizes = new int[packets.Length];");
            Append(sb, 3, "for (int j = 0; j < packets.Length; j++) sizes[j] = packets[j].Length;");
            Append(sb, 3, "return packets;");
            Append(sb, 2, "}");

            Append(sb, 1, "}");
            Append(sb, 0, "");
        }

        private static void EmitBlock(StringBuilder sb, MapBlock block)
        {
            Append(sb, 2, $"public sealed class {block.Name}Block : PacketBlock");
            Append(sb, 2, "{");

            // fields
            foreach (var f in block.Fields)
            {
                var decl = f.Type != FieldType.Variable ? MapType(f.Type) : "byte[]";
                Append(sb, 3, $"public {decl} {f.Name};");
            }
            Append(sb, 3, "");

            // Length
            Append(sb, 3, "public override int Length");
            Append(sb, 3, "{");
            Append(sb, 4, "get");
            Append(sb, 4, "{");
            int fixedLen = block.Fields.Sum(GetFieldFixedLength);
            Append(sb, 5, $"int length = {fixedLen};");
            foreach (var f in block.Fields.Where(x => x.Type == FieldType.Variable))
            {
                if (f.Count == 1)
                    Append(sb, 5, $"length += 1; if ({f.Name} != null) length += {f.Name}.Length;");
                else
                    Append(sb, 5, $"length += 2; if ({f.Name} != null) length += {f.Name}.Length;");
            }
            Append(sb, 5, "return length;");
            Append(sb, 4, "}");
            Append(sb, 3, "}");
            Append(sb, 3, "");

            // constructors
            Append(sb, 3, $"public {block.Name}Block() {{ }}");
            Append(sb, 3, $"public {block.Name}Block(byte[] bytes, ref int i) {{ FromBytes(bytes, ref i); }}");
            Append(sb, 3, "");

            // FromBytes
            Append(sb, 3, "public override void FromBytes(byte[] bytes, ref int i)");
            Append(sb, 3, "{");
            if (block.Fields.Any(f => f.Type == FieldType.Variable)) Append(sb, 4, "int length;");
            Append(sb, 4, "try");
            Append(sb, 4, "{");
            foreach (var f in block.Fields) WriteFieldFromBytes(sb, f);
            Append(sb, 4, "}");
            Append(sb, 4, "catch (Exception) { throw new MalformedDataException(); }");
            Append(sb, 3, "}");
            Append(sb, 3, "");

            // ToBytes
            Append(sb, 3, "public override void ToBytes(byte[] bytes, ref int i)");
            Append(sb, 3, "{");
            foreach (var f in block.Fields) WriteFieldToBytes(sb, f);
            Append(sb, 3, "}");
            Append(sb, 2, "}");
            Append(sb, 2, "");
        }

        private static int GetFieldFixedLength(MapField f) => f.Type switch
        {
            FieldType.BOOL or FieldType.U8 or FieldType.S8 => 1,
            FieldType.U16 or FieldType.S16 or FieldType.IPPORT => 2,
            FieldType.U32 or FieldType.S32 or FieldType.F32 or FieldType.IPADDR => 4,
            FieldType.U64 or FieldType.F64 => 8,
            FieldType.LLVector3 or FieldType.LLQuaternion => 12,
            FieldType.LLUUID or FieldType.LLVector4 => 16,
            FieldType.LLVector3d => 24,
            FieldType.Fixed => f.Count,
            FieldType.Variable => 0,
            _ => 0
        };

        private static void WriteFieldFromBytes(StringBuilder sb, MapField field)
        {
            var n = field.Name;
            switch (field.Type)
            {
                case FieldType.BOOL: Append(sb, 5, $"{n} = (bytes[i++] != 0);"); break;
                case FieldType.F32: Append(sb, 5, $"{n} = Utils.BytesToFloat(bytes, i); i += 4;"); break;
                case FieldType.F64: Append(sb, 5, $"{n} = Utils.BytesToDouble(bytes, i); i += 8;"); break;
                case FieldType.Fixed:
                    Append(sb, 5, $"{n} = new byte[{field.Count}];");
                    Append(sb, 5, $"Buffer.BlockCopy(bytes, i, {n}, 0, {field.Count}); i += {field.Count};");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    Append(sb, 5, $"{n} = (uint)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));");
                    break;
                case FieldType.IPPORT:
                    Append(sb, 5, $"{n} = (ushort)((bytes[i++] << 8) + bytes[i++]);");
                    break;
                case FieldType.U16:
                    Append(sb, 5, $"{n} = (ushort)(bytes[i++] + (bytes[i++] << 8));");
                    break;
                case FieldType.LLQuaternion:
                    Append(sb, 5, $"{n}.FromBytes(bytes, i, true); i += 12;"); break;
                case FieldType.LLUUID:
                    Append(sb, 5, $"{n}.FromBytes(bytes, i); i += 16;"); break;
                case FieldType.LLVector3:
                    Append(sb, 5, $"{n}.FromBytes(bytes, i); i += 12;"); break;
                case FieldType.LLVector3d:
                    Append(sb, 5, $"{n}.FromBytes(bytes, i); i += 24;"); break;
                case FieldType.LLVector4:
                    Append(sb, 5, $"{n}.FromBytes(bytes, i); i += 16;"); break;
                case FieldType.S16:
                    Append(sb, 5, $"{n} = (short)(bytes[i++] + (bytes[i++] << 8));"); break;
                case FieldType.S32:
                    Append(sb, 5, $"{n} = (int)(bytes[i++] + (bytes[i++] << 8) + (bytes[i++] << 16) + (bytes[i++] << 24));"); break;
                case FieldType.S8:
                    Append(sb, 5, $"{n} = (sbyte)bytes[i++];"); break;
                case FieldType.U64:
                    Append(sb, 5, $"{n} = (ulong)((ulong)bytes[i++] + ((ulong)bytes[i++] << 8) + ((ulong)bytes[i++] << 16) + ((ulong)bytes[i++] << 24) + ((ulong)bytes[i++] << 32) + ((ulong)bytes[i++] << 40) + ((ulong)bytes[i++] << 48) + ((ulong)bytes[i++] << 56));"); break;
                case FieldType.U8:
                    Append(sb, 5, $"{n} = (byte)bytes[i++];"); break;
                case FieldType.Variable:
                    if (field.Count == 1) Append(sb, 5, "length = bytes[i++];");
                    else Append(sb, 5, "length = (bytes[i++] + (bytes[i++] << 8));");
                    Append(sb, 5, $"{n} = new byte[length];");
                    Append(sb, 5, $"Buffer.BlockCopy(bytes, i, {n}, 0, length); i += length;");
                    break;
                default:
                    Append(sb, 5, $"// Unhandled FieldType {field.Type} for {n}");
                    break;
            }
        }

        private static void WriteFieldToBytes(StringBuilder sb, MapField field)
        {
            var n = field.Name;
            switch (field.Type)
            {
                case FieldType.BOOL:
                    Append(sb, 5, $"bytes[i++] = (byte)(({n}) ? 1 : 0);");
                    break;
                case FieldType.F32:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 4, \"{n}\"); Utils.FloatToBytes({n}, bytes, i); i += 4;");
                    break;
                case FieldType.F64:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 8, \"{n}\"); Utils.DoubleToBytes({n}, bytes, i); i += 8;");
                    break;
                case FieldType.Fixed:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, {field.Count}, \"{n}\");");
                    Append(sb, 5, $"if ({n} != null) {{ int _copy = Math.Min({n}.Length, {field.Count}); if (_copy > 0) Buffer.BlockCopy({n}, 0, bytes, i, _copy); for (int _z = _copy; _z < {field.Count}; _z++) bytes[i + _z] = 0; }} else {{ for (int _z = 0; _z < {field.Count}; _z++) bytes[i + _z] = 0; }}");
                    Append(sb, 5, $"i += {field.Count};");
                    break;
                case FieldType.IPPORT:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 2, \"{n}\"); bytes[i++] = (byte)(({n} >> 8) % 256); bytes[i++] = (byte)({n} % 256);");
                    break;
                case FieldType.U16:
                case FieldType.S16:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 2, \"{n}\"); bytes[i++] = (byte)({n} % 256); bytes[i++] = (byte)(({n} >> 8) % 256);");
                    break;
                case FieldType.LLQuaternion:
                case FieldType.LLVector3:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 12, \"{n}\"); {n}.ToBytes(bytes, i); i += 12;");
                    break;
                case FieldType.LLUUID:
                case FieldType.LLVector4:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 16, \"{n}\"); {n}.ToBytes(bytes, i); i += 16;");
                    break;
                case FieldType.LLVector3d:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 24, \"{n}\"); {n}.ToBytes(bytes, i); i += 24;");
                    break;
                case FieldType.U8:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 1, \"{n}\"); bytes[i++] = {n};");
                    break;
                case FieldType.S8:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 1, \"{n}\"); bytes[i++] = (byte){n};");
                    break;
                case FieldType.IPADDR:
                case FieldType.U32:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 4, \"{n}\"); Utils.UIntToBytes({n}, bytes, i); i += 4;");
                    break;
                case FieldType.S32:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 4, \"{n}\"); Utils.IntToBytes({n}, bytes, i); i += 4;");
                    break;
                case FieldType.U64:
                    Append(sb, 5, $"GeneratedEnsure.EnsureRemaining(bytes, i, 8, \"{n}\"); Utils.UInt64ToBytes({n}, bytes, i); i += 8;");
                    break;
                case FieldType.Variable:
                    Append(sb, 5, $"if ({n} != null) {{");
                    if (field.Count == 1)
                    {
                        // 1-byte length prefix + payload
                        Append(sb, 6, $"GeneratedEnsure.EnsureRemaining(bytes, i, 1 + {n}.Length, \"{n}\"); bytes[i++] = (byte){n}.Length;");
                        Append(sb, 6, $"Buffer.BlockCopy({n}, 0, bytes, i, {n}.Length); i += {n}.Length;");
                    }
                    else
                    {
                        // 2-byte length prefix + payload
                        Append(sb, 6, $"GeneratedEnsure.EnsureRemaining(bytes, i, 2 + {n}.Length, \"{n}\"); bytes[i++] = (byte)({n}.Length % 256); bytes[i++] = (byte)(({n}.Length >> 8) % 256);");
                        Append(sb, 6, $"Buffer.BlockCopy({n}, 0, bytes, i, {n}.Length); i += {n}.Length;");
                    }
                    Append(sb, 5, "} else {");
                    if (field.Count == 1) Append(sb, 6, $"GeneratedEnsure.EnsureRemaining(bytes, i, 1, \"{n}\"); bytes[i++] = 0;");
                    else Append(sb, 6, $"GeneratedEnsure.EnsureRemaining(bytes, i, 2, \"{n}\"); bytes[i++] = 0; bytes[i++] = 0;");
                    Append(sb, 5, "}");
                    break;
                default:
                    Append(sb, 5, $"                // Unhandled FieldType {field.Type} for {n}");
                    break;
            }
        }

        private static string MapType(FieldType ft) => ft switch
        {
            FieldType.BOOL => "bool",
            FieldType.F32 => "float",
            FieldType.F64 => "double",
            FieldType.IPPORT => "ushort",
            FieldType.U16 => "ushort",
            FieldType.IPADDR => "uint",
            FieldType.U32 => "uint",
            FieldType.LLQuaternion => "Quaternion",
            FieldType.LLUUID => "UUID",
            FieldType.LLVector3 => "Vector3",
            FieldType.LLVector3d => "Vector3d",
            FieldType.LLVector4 => "Vector4",
            FieldType.S16 => "short",
            FieldType.S32 => "int",
            FieldType.S8 => "sbyte",
            FieldType.U64 => "ulong",
            FieldType.U8 => "byte",
            FieldType.Fixed => "byte[]",
            _ => "byte[]"
        };

        private static string SafeName(string name)
        {
            return string.IsNullOrEmpty(name) ? "Unknown" :
                // ensure no spaces or invalid characters
                new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        }
    }
}