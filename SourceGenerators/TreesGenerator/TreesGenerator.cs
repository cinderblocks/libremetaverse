/*
 * Copyright (c) 2025-2026, Sjofn LLC.
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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TreesGenerator
{
    [Generator]
    public class TreesGenerator : IIncrementalGenerator
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly DiagnosticDescriptor MissingTreesXml = new(
            "TRG001",
            "Missing trees.xml",
            "AdditionalFile 'trees.xml' not found. Generator will not produce TreeDefinitions.g.cs.",
            "TreesGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor MissingGrassXml = new(
            "TRG002",
            "Missing grass.xml",
            "AdditionalFile 'grass.xml' not found. Generator will not produce GrassDefinitions.g.cs.",
            "TreesGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GenerationFailed = new(
            "TRG999",
            "Generation failed",
            "Foliage definition generation failed: {0}",
            "TreesGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var treesProvider = context.AdditionalTextsProvider
                .Where(at => Path.GetFileName(at.Path).Equals("trees.xml", StringComparison.OrdinalIgnoreCase))
                .Select((at, ct) => at.GetText(ct)?.ToString() ?? string.Empty)
                .Collect();

            var grassProvider = context.AdditionalTextsProvider
                .Where(at => Path.GetFileName(at.Path).Equals("grass.xml", StringComparison.OrdinalIgnoreCase))
                .Select((at, ct) => at.GetText(ct)?.ToString() ?? string.Empty)
                .Collect();

            context.RegisterSourceOutput(treesProvider, (spc, xmls) =>
            {
                var xmlText = xmls.Length > 0 ? xmls[0] : null;
                if (string.IsNullOrWhiteSpace(xmlText))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MissingTreesXml, Location.None));
                    return;
                }
                try
                {
                    spc.AddSource("TreeDefinitions.g.cs", SourceText.From(GenerateTrees(xmlText!), Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, ex.Message));
                }
            });

            context.RegisterSourceOutput(grassProvider, (spc, xmls) =>
            {
                var xmlText = xmls.Length > 0 ? xmls[0] : null;
                if (string.IsNullOrWhiteSpace(xmlText))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MissingGrassXml, Location.None));
                    return;
                }
                try
                {
                    spc.AddSource("GrassDefinitions.g.cs", SourceText.From(GenerateGrass(xmlText!), Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, ex.Message));
                }
            });
        }

        private static string GenerateTrees(string xmlText)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlText);
            var root = doc.DocumentElement!;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("namespace LibreMetaverse");
            sb.AppendLine("{");

            // Tree enum — species names and IDs from trees.xml
            sb.AppendLine("    /// <summary>Linden tree foliage species.</summary>");
            sb.AppendLine("    public enum Tree : byte");
            sb.AppendLine("    {");
            foreach (XmlNode enumNode in root.ChildNodes)
            {
                if (enumNode is not XmlElement enumEl || enumEl.Name != "tree") continue;
                var displayName = enumEl.GetAttribute("name");
                var memberName = displayName.Replace(" ", "");
                var speciesId = enumEl.GetAttribute("species_id");
                sb.AppendLine($"        /// <summary>{displayName}</summary>");
                sb.AppendLine($"        {memberName} = {speciesId},");
            }
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>Parameters for a single Linden tree species from trees.xml.</summary>");
            sb.AppendLine("    public readonly struct TreeDefinition");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Name { get; init; }");
            sb.AppendLine("        public int SpeciesId { get; init; }");
            sb.AppendLine("        public UUID TextureId { get; init; }");
            sb.AppendLine("        public float Droop { get; init; }");
            sb.AppendLine("        public float Twist { get; init; }");
            sb.AppendLine("        public float Branches { get; init; }");
            sb.AppendLine("        public int Depth { get; init; }");
            sb.AppendLine("        public float ScaleStep { get; init; }");
            sb.AppendLine("        public float TrunkDepth { get; init; }");
            sb.AppendLine("        public float BranchLength { get; init; }");
            sb.AppendLine("        public float TrunkLength { get; init; }");
            sb.AppendLine("        public float LeafScale { get; init; }");
            sb.AppendLine("        public float BillboardScale { get; init; }");
            sb.AppendLine("        public float BillboardRatio { get; init; }");
            sb.AppendLine("        public float TrunkAspect { get; init; }");
            sb.AppendLine("        public float BranchAspect { get; init; }");
            sb.AppendLine("        public float LeafRotate { get; init; }");
            sb.AppendLine("        public float NoiseMag { get; init; }");
            sb.AppendLine("        public float NoiseScale { get; init; }");
            sb.AppendLine("        public float Taper { get; init; }");
            sb.AppendLine("        public int RepeatZ { get; init; }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>All Linden tree species definitions, indexed by <see cref=\"Tree\"/> species id.</summary>");
            sb.AppendLine("    public static class TreeDefinitions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly TreeDefinition[] All = new TreeDefinition[]");
            sb.AppendLine("        {");

            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is not XmlElement el || el.Name != "tree") continue;

                var name = Escape(el.GetAttribute("name"));
                var speciesId = el.GetAttribute("species_id");
                var textureId = el.GetAttribute("texture_id");
                var droop = ParseFloat(el.GetAttribute("droop"));
                var twist = ParseFloat(el.GetAttribute("twist"));
                var branches = ParseFloat(el.GetAttribute("branches"));
                var depth = ParseInt(el.GetAttribute("depth"));
                var scaleStep = ParseFloat(el.GetAttribute("scale_step"));
                var trunkDepth = ParseFloat(el.GetAttribute("trunk_depth"));
                var branchLength = ParseFloat(el.GetAttribute("branch_length"));
                var trunkLength = ParseFloat(el.GetAttribute("trunk_length"));
                var leafScale = ParseFloat(el.GetAttribute("leaf_scale"));
                var billboardScale = ParseFloat(el.GetAttribute("billboard_scale"));
                var billboardRatio = ParseFloat(el.GetAttribute("billboard_ratio"));
                var trunkAspect = ParseFloat(el.GetAttribute("trunk_aspect"));
                var branchAspect = ParseFloat(el.GetAttribute("branch_aspect"));
                var leafRotate = ParseFloat(el.GetAttribute("leaf_rotate"));
                var noiseMag = ParseFloat(el.GetAttribute("noise_mag"));
                var noiseScale = ParseFloat(el.GetAttribute("noise_scale"));
                var taper = ParseFloat(el.GetAttribute("taper"));
                var repeatZ = ParseInt(el.GetAttribute("repeat_z"));

                sb.AppendLine($"            new TreeDefinition {{ Name = \"{name}\", SpeciesId = {speciesId}, TextureId = new UUID(\"{textureId}\"),");
                sb.AppendLine($"                Droop = {droop}, Twist = {twist}, Branches = {branches}, Depth = {depth}, ScaleStep = {scaleStep},");
                sb.AppendLine($"                TrunkDepth = {trunkDepth}, BranchLength = {branchLength}, TrunkLength = {trunkLength},");
                sb.AppendLine($"                LeafScale = {leafScale}, BillboardScale = {billboardScale}, BillboardRatio = {billboardRatio},");
                sb.AppendLine($"                TrunkAspect = {trunkAspect}, BranchAspect = {branchAspect}, LeafRotate = {leafRotate},");
                sb.AppendLine($"                NoiseMag = {noiseMag}, NoiseScale = {noiseScale}, Taper = {taper}, RepeatZ = {repeatZ} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Returns the definition for the given tree species.</summary>");
            sb.AppendLine("        public static ref readonly TreeDefinition Get(Tree species) => ref All[(byte)species];");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateGrass(string xmlText)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlText);
            var root = doc.DocumentElement!;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("namespace LibreMetaverse");
            sb.AppendLine("{");

            // Grass enum — species names and IDs from grass.xml
            sb.AppendLine("    /// <summary>Linden grass foliage species.</summary>");
            sb.AppendLine("    public enum Grass : byte");
            sb.AppendLine("    {");
            foreach (XmlNode enumNode in root.ChildNodes)
            {
                if (enumNode is not XmlElement enumEl || enumEl.Name != "grass") continue;
                var displayName = enumEl.GetAttribute("name");
                var memberName = ToPascalCase(displayName);
                var speciesId = enumEl.GetAttribute("species_id");
                sb.AppendLine($"        /// <summary>{Escape(displayName)}</summary>");
                sb.AppendLine($"        {memberName} = {speciesId},");
            }
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>Parameters for a single Linden grass species from grass.xml.</summary>");
            sb.AppendLine("    public readonly struct GrassDefinition");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Name { get; init; }");
            sb.AppendLine("        public int SpeciesId { get; init; }");
            sb.AppendLine("        public UUID TextureId { get; init; }");
            sb.AppendLine("        public float BladeSizeX { get; init; }");
            sb.AppendLine("        public float BladeSizeY { get; init; }");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>All Linden grass species definitions, indexed by <see cref=\"Grass\"/> species id.</summary>");
            sb.AppendLine("    public static class GrassDefinitions");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly GrassDefinition[] All = new GrassDefinition[]");
            sb.AppendLine("        {");

            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is not XmlElement el || el.Name != "grass") continue;

                var name = Escape(el.GetAttribute("name"));
                var speciesId = el.GetAttribute("species_id");
                var textureId = el.GetAttribute("texture_id");
                var bladeSizeX = ParseFloat(el.GetAttribute("blade_size_x"));
                var bladeSizeY = ParseFloat(el.GetAttribute("blade_size_y"));

                sb.AppendLine($"            new GrassDefinition {{ Name = \"{name}\", SpeciesId = {speciesId}, TextureId = new UUID(\"{textureId}\"), BladeSizeX = {bladeSizeX}, BladeSizeY = {bladeSizeY} }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Returns the definition for the given grass species.</summary>");
            sb.AppendLine("        public static ref readonly GrassDefinition Get(Grass species) => ref All[(byte)species];");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // Converts "Grass 0", "undergrowth_1" etc. to valid PascalCase C# identifiers.
        private static string ToPascalCase(string s)
        {
            var parts = s.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    sb.Append(part, 1, part.Length - 1);
            }
            return sb.ToString();
        }

        private static string FormatFloat(float f)
        {
            if (f == 0f) return "0f";
            return f.ToString("G9", Inv) + "f";
        }

        private static string ParseFloat(string s)
        {
            if (float.TryParse(s, NumberStyles.Float, Inv, out var f))
                return FormatFloat(f);
            return "0f";
        }

        private static string ParseInt(string s)
        {
            if (int.TryParse(s, NumberStyles.Integer, Inv, out var i))
                return i.ToString(Inv);
            if (float.TryParse(s, NumberStyles.Float, Inv, out var f))
                return ((int)f).ToString(Inv);
            return "0";
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
