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

using System;
using System.IO;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OSDInspector
{
    /// <summary>
    /// A utility for inspecting, converting, and validating OSD (OpenMetaverse Structured Data) files.
    /// Demonstrates OSD serialization formats and Primitive OSD serialization.
    /// </summary>
    internal class OSDInspector
    {
        static int Main(string[] args)
        {
            Console.WriteLine("OSDInspector - OpenMetaverse Structured Data Tool");
            Console.WriteLine();

            if (args.Length == 0)
            {
                ShowUsage();
                return 1;
            }

            try
            {
                var command = args[0].ToLower();

                switch (command)
                {
                    case "inspect":
                    case "i":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please specify input file");
                            return 1;
                        }
                        return InspectFile(args[1]);

                    case "convert":
                    case "c":
                        if (args.Length < 4)
                        {
                            Console.WriteLine("Error: Usage: convert <input> <format> <output>");
                            Console.WriteLine("Formats: json, xml, binary, notation");
                            return 1;
                        }
                        return ConvertFile(args[1], args[2], args[3]);

                    case "validate":
                    case "v":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please specify input file");
                            return 1;
                        }
                        return ValidateFile(args[1]);

                    case "prim-to-osd":
                        return PrimToOSD();

                    case "osd-to-prim":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please specify input file");
                            return 1;
                        }
                        return OSDToPrim(args[1]);

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        ShowUsage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"  {ex.InnerException.Message}");
                return 1;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: OSDInspector <command> [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  inspect <file>                    - Display OSD structure and contents");
            Console.WriteLine("  convert <in> <format> <out>       - Convert between OSD formats");
            Console.WriteLine("  validate <file>                   - Validate OSD file");
            Console.WriteLine("  prim-to-osd                       - Create sample Primitive and show OSD");
            Console.WriteLine("  osd-to-prim <file>                - Parse OSD file as Primitive");
            Console.WriteLine();
            Console.WriteLine("Formats: json, xml, binary, notation");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  OSDInspector inspect data.json");
            Console.WriteLine("  OSDInspector convert data.json xml data.xml");
            Console.WriteLine("  OSDInspector validate prim.json");
            Console.WriteLine("  OSDInspector prim-to-osd > cube.json");
        }

        static int InspectFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"File not found: {filename}");
                return 1;
            }

            var content = File.ReadAllText(filename);
            var osd = ParseOSD(content, filename);

            if (osd == null)
            {
                Console.WriteLine("Failed to parse file");
                return 1;
            }

            Console.WriteLine($"File: {filename}");
            Console.WriteLine($"Type: {osd.Type}");
            Console.WriteLine($"Size: {new FileInfo(filename).Length} bytes");
            Console.WriteLine();
            Console.WriteLine("Structure:");
            Console.WriteLine(new string('-', 60));
            DisplayOSD(osd, 0);
            Console.WriteLine(new string('-', 60));

            return 0;
        }

        static int ConvertFile(string input, string format, string output)
        {
            if (!File.Exists(input))
            {
                Console.WriteLine($"Input file not found: {input}");
                return 1;
            }

            var content = File.ReadAllText(input);
            var osd = ParseOSD(content, input);

            if (osd == null)
            {
                Console.WriteLine("Failed to parse input file");
                return 1;
            }

            string outputContent;
            switch (format.ToLower())
            {
                case "json":
                    outputContent = OSDParser.SerializeJsonString(osd, true);
                    break;
                case "xml":
                    outputContent = OSDParser.SerializeLLSDXmlString(osd);
                    break;
                case "notation":
                    outputContent = OSDParser.SerializeLLSDNotationString(osd);
                    break;
                case "binary":
                    var bytes = OSDParser.SerializeLLSDBinary(osd);
                    File.WriteAllBytes(output, bytes);
                    Console.WriteLine($"Converted to binary: {output} ({bytes.Length} bytes)");
                    return 0;
                default:
                    Console.WriteLine($"Unknown format: {format}");
                    return 1;
            }

            File.WriteAllText(output, outputContent);
            Console.WriteLine($"Converted {input} to {format}: {output}");
            return 0;
        }

        static int ValidateFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"File not found: {filename}");
                return 1;
            }

            try
            {
                var content = File.ReadAllText(filename);
                var osd = ParseOSD(content, filename);

                if (osd == null)
                {
                    Console.WriteLine("? Invalid: Failed to parse");
                    return 1;
                }

                Console.WriteLine("? Valid OSD file");
                Console.WriteLine($"  Type: {osd.Type}");
                
                if (osd is OSDMap map)
                    Console.WriteLine($"  Keys: {map.Count}");
                else if (osd is OSDArray array)
                    Console.WriteLine($"  Elements: {array.Count}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Invalid: {ex.Message}");
                return 1;
            }
        }

        static int PrimToOSD()
        {
            var prim = new Primitive
            {
                ID = UUID.Random(),
                LocalID = 12345,
                Position = new Vector3(128, 128, 25),
                Rotation = Quaternion.Identity,
                Scale = new Vector3(1, 1, 1),
                PrimData = new Primitive.ConstructionData
                {
                    PCode = PCode.Prim,
                    Material = Material.Wood,
                    PathCurve = PathCurve.Line,
                    ProfileCurve = ProfileCurve.Square,
                    PathScaleX = 1.0f,
                    PathScaleY = 1.0f
                }
            };

            prim.Properties = new Primitive.ObjectProperties
            {
                Name = "Example Cube",
                Description = "A simple cube created with LibreMetaverse"
            };

            var osd = prim.GetOSD();
            var json = OSDParser.SerializeJsonString(osd, true);
            
            Console.WriteLine(json);
            return 0;
        }

        static int OSDToPrim(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"File not found: {filename}");
                return 1;
            }

            var content = File.ReadAllText(filename);
            var osd = ParseOSD(content, filename);

            if (osd == null)
            {
                Console.WriteLine("Failed to parse file");
                return 1;
            }

            try
            {
                var prim = Primitive.FromOSD(osd);
                
                Console.WriteLine("Successfully parsed Primitive:");
                Console.WriteLine($"  ID: {prim.ID}");
                Console.WriteLine($"  Name: {prim.Properties?.Name ?? "Unknown"}");
                Console.WriteLine($"  Type: {prim.Type}");
                Console.WriteLine($"  Position: {prim.Position}");
                Console.WriteLine($"  Scale: {prim.Scale}");
                
                if (prim.PrimData != null)
                {
                    Console.WriteLine($"  Material: {prim.PrimData.Material}");
                    Console.WriteLine($"  PCode: {prim.PrimData.PCode}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse as Primitive: {ex.Message}");
                return 1;
            }
        }

        static OSD? ParseOSD(string content, string filename)
        {
            var ext = Path.GetExtension(filename).ToLower();

            try
            {
                // Try to detect format
                if (ext == ".json" || content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
                {
                    return OSDParser.DeserializeJson(content);
                }
                else if (ext == ".xml" || content.TrimStart().StartsWith("<"))
                {
                    return OSDParser.DeserializeLLSDXml(content);
                }
                else
                {
                    // Try binary
                    var bytes = File.ReadAllBytes(filename);
                    return OSDParser.DeserializeLLSDBinary(bytes);
                }
            }
            catch
            {
                // If all else fails, try notation
                try
                {
                    return OSDParser.DeserializeLLSDNotation(content);
                }
                catch
                {
                    return null;
                }
            }
        }

        static void DisplayOSD(OSD osd, int indent)
        {
            var prefix = new string(' ', indent * 2);

            switch (osd.Type)
            {
                case OSDType.Map:
                    var map = (OSDMap)osd;
                    Console.WriteLine($"{prefix}Map ({map.Count} keys)");
                    foreach (var kvp in map)
                    {
                        Console.Write($"{prefix}  {kvp.Key}: ");
                        if (kvp.Value.Type == OSDType.Map || kvp.Value.Type == OSDType.Array)
                        {
                            Console.WriteLine();
                            DisplayOSD(kvp.Value, indent + 2);
                        }
                        else
                        {
                            DisplayOSD(kvp.Value, 0);
                        }
                    }
                    break;

                case OSDType.Array:
                    var array = (OSDArray)osd;
                    Console.WriteLine($"{prefix}Array ({array.Count} elements)");
                    for (int i = 0; i < array.Count; i++)
                    {
                        Console.Write($"{prefix}  [{i}]: ");
                        if (array[i].Type == OSDType.Map || array[i].Type == OSDType.Array)
                        {
                            Console.WriteLine();
                            DisplayOSD(array[i], indent + 2);
                        }
                        else
                        {
                            DisplayOSD(array[i], 0);
                        }
                    }
                    break;

                case OSDType.String:
                    Console.WriteLine($"String: \"{osd.AsString()}\"");
                    break;
                case OSDType.Integer:
                    Console.WriteLine($"Integer: {osd.AsInteger()}");
                    break;
                case OSDType.Real:
                    Console.WriteLine($"Real: {osd.AsReal()}");
                    break;
                case OSDType.Boolean:
                    Console.WriteLine($"Boolean: {osd.AsBoolean()}");
                    break;
                case OSDType.UUID:
                    Console.WriteLine($"UUID: {osd.AsUUID()}");
                    break;
                case OSDType.Date:
                    Console.WriteLine($"Date: {osd.AsDate()}");
                    break;
                case OSDType.URI:
                    Console.WriteLine($"URI: {osd.AsUri()}");
                    break;
                case OSDType.Binary:
                    var binary = osd.AsBinary();
                    Console.WriteLine($"Binary: {binary.Length} bytes");
                    break;
                default:
                    Console.WriteLine($"Unknown ({osd.Type})");
                    break;
            }
        }
    }
}
