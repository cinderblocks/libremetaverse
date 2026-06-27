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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AttentionsGenerator
{
    [Generator]
    public class AttentionsGenerator : IIncrementalGenerator
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Ordered list of attention entries matching attentions.xml names and the LookAtType enum layout.
        // Index values correspond directly to (byte)LookAtType: None=0, these=1-9, Clear=10.
        private static readonly (int Index, string XmlName, string CSharpName, string Summary, bool Obsolete)[] AttentionEntries =
        {
            (1, "idle",         "Idle",         "Tracks the mouse pointer movement.", false),
            (2, "auto_listen",  "AutoListen",   "Tracks nearby chat.", false),
            (3, "freelook",     "FreeLook",     "Tracks target objects and mouse movement in third-person mode.", false),
            (4, "respond",      "Respond",      "Tracks the beginning of typing.", false),
            (5, "hover",        "Hover",        "Tracks objects the mouse lingers over when hover tooltips are enabled.", false),
            (6, "conversation", "Conversation", "Tracks avatars and other objects clicked on.", true),
            (7, "select",       "Select",       "Tracks objects grabbed and being moved.", false),
            (8, "focus",        "Focus",        "Freezes during avatar customization and when focused on an object or point.", false),
            (9, "mouselook",    "Mouselook",    "Tracks the center of view in mouselook mode.", false),
        };

        // Total size of entry array: LookAtType.None(0) through LookAtType.Clear(10)
        private const int EntryCount = 11;

        // Maps XML attention name → array index for use during XML parsing
        private static readonly Dictionary<string, int> AttentionIndex;

        static AttentionsGenerator()
        {
            AttentionIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (index, xmlName, _, _, _) in AttentionEntries)
                AttentionIndex[xmlName] = index;
        }

        private static readonly DiagnosticDescriptor MissingXml = new(
            "ATG001",
            "Missing attentions XML",
            "AdditionalFile '{0}' not found. Corresponding LindenAttentions set will be empty.",
            "AttentionsGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GenerationFailed = new(
            "ATG999",
            "Generation failed",
            "Attentions generation failed: {0}",
            "AttentionsGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var xmlProvider = context.AdditionalTextsProvider
                .Where(at =>
                {
                    var fn = Path.GetFileName(at.Path);
                    return fn.Equals("attentions.xml",  StringComparison.OrdinalIgnoreCase) ||
                           fn.Equals("attentionsN.xml", StringComparison.OrdinalIgnoreCase);
                })
                .Select((at, ct) => (Name: Path.GetFileName(at.Path), Text: at.GetText(ct)?.ToString() ?? string.Empty))
                .Collect();

            context.RegisterSourceOutput(xmlProvider, (spc, files) =>
            {
                string? defaultText = null;
                string? updatedText = null;

                foreach (var (name, text) in files)
                {
                    if (name.Equals("attentions.xml",  StringComparison.OrdinalIgnoreCase))
                        defaultText = string.IsNullOrWhiteSpace(text) ? null : text;
                    else if (name.Equals("attentionsN.xml", StringComparison.OrdinalIgnoreCase))
                        updatedText = string.IsNullOrWhiteSpace(text) ? null : text;
                }

                if (defaultText == null)
                    spc.ReportDiagnostic(Diagnostic.Create(MissingXml, Location.None, "attentions.xml"));
                if (updatedText == null)
                    spc.ReportDiagnostic(Diagnostic.Create(MissingXml, Location.None, "attentionsN.xml"));

                try
                {
                    var generated = Generate(defaultText, updatedText);
                    spc.AddSource("LindenAttentions.g.cs", SourceText.From(generated, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, ex.Message));
                }
            });
        }

        private static string Generate(string? defaultText, string? updatedText)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("namespace LibreMetaverse");
            sb.AppendLine("{");

            // LookAtType enum
            sb.AppendLine("    /// <summary>Type of LookAt viewer effect.</summary>");
            sb.AppendLine("    public enum LookAtType : byte");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>No look-at target.</summary>");
            sb.AppendLine("        None,");
            foreach (var (_, _, csharpName, summary, obsolete) in AttentionEntries)
            {
                sb.AppendLine($"        /// <summary>{summary}</summary>");
                if (obsolete)
                    sb.AppendLine("        [System.Obsolete]");
                sb.AppendLine($"        {csharpName},");
            }
            sb.AppendLine("        /// <summary>Clears the look-at target.</summary>");
            sb.AppendLine("        Clear");
            sb.AppendLine("    }");
            sb.AppendLine();

            // AttentionData struct
            sb.AppendLine("    /// <summary>Priority and timeout for a single avatar attention (look-at) event.</summary>");
            sb.AppendLine("    public readonly struct AttentionData");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Priority of this attention relative to others; higher wins.</summary>");
            sb.AppendLine("        public float Priority { get; init; }");
            sb.AppendLine("        /// <summary>Duration in seconds before reverting to idle. Negative means no timeout.</summary>");
            sb.AppendLine("        public float Timeout { get; init; }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // AttentionSet struct
            sb.AppendLine("    /// <summary>Attention parameters for one avatar gender, indexed by <see cref=\"LookAtType\"/>.</summary>");
            sb.AppendLine("    public readonly struct AttentionSet");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Name { get; init; }");
            sb.AppendLine("        /// <summary>Entries indexed by <c>(byte)LookAtType</c>. None and Clear slots are zero-initialized.</summary>");
            sb.AppendLine("        public AttentionData[] Entries { get; init; }");
            sb.AppendLine("        /// <summary>Returns the attention data for the given look-at type.</summary>");
            sb.AppendLine("        public ref readonly AttentionData Get(LookAtType type) => ref Entries[(byte)type];");
            sb.AppendLine("    }");
            sb.AppendLine();

            // LindenAttentions static class
            sb.AppendLine("    /// <summary>Avatar attention (look-at) parameters from attentions.xml and attentionsN.xml.</summary>");
            sb.AppendLine("    public static class LindenAttentions");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Original attention parameters (attentions.xml).</summary>");
            EmitSet(sb, "Default", defaultText);
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Updated attention parameters with refined conversation/select/focus tuning (attentionsN.xml).</summary>");
            EmitSet(sb, "Updated", updatedText);
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Default masculine (attentions.xml) attention parameters.</summary>");
            sb.AppendLine("        public static ref readonly AttentionSet DefaultMasculine => ref Default[0];");
            sb.AppendLine("        /// <summary>Default feminine (attentions.xml) attention parameters.</summary>");
            sb.AppendLine("        public static ref readonly AttentionSet DefaultFeminine => ref Default[1];");
            sb.AppendLine("        /// <summary>Updated masculine (attentionsN.xml) attention parameters.</summary>");
            sb.AppendLine("        public static ref readonly AttentionSet UpdatedMasculine => ref Updated[0];");
            sb.AppendLine("        /// <summary>Updated feminine (attentionsN.xml) attention parameters.</summary>");
            sb.AppendLine("        public static ref readonly AttentionSet UpdatedFeminine => ref Updated[1];");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void EmitSet(StringBuilder sb, string fieldName, string? xmlText)
        {
            sb.AppendLine($"        public static readonly AttentionSet[] {fieldName} = new AttentionSet[]");
            sb.AppendLine("        {");

            if (xmlText != null)
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlText);
                var root = doc.DocumentElement!;

                foreach (XmlNode genderNode in root.ChildNodes)
                {
                    if (genderNode is not XmlElement genderEl || genderEl.Name != "gender") continue;

                    var genderName = Escape(genderEl.GetAttribute("name"));

                    var entries = new float[EntryCount, 2]; // [index, 0=priority 1=timeout]

                    foreach (XmlNode paramNode in genderEl.ChildNodes)
                    {
                        if (paramNode is not XmlElement paramEl || paramEl.Name != "param") continue;

                        var attentionName = paramEl.GetAttribute("attention");
                        if (!AttentionIndex.TryGetValue(attentionName, out var idx)) continue;

                        float.TryParse(paramEl.GetAttribute("priority"), NumberStyles.Float, Inv, out var priority);
                        float.TryParse(paramEl.GetAttribute("timeout"),  NumberStyles.Float, Inv, out var timeout);
                        entries[idx, 0] = priority;
                        entries[idx, 1] = timeout;
                    }

                    sb.AppendLine($"            new AttentionSet");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Name = \"{genderName}\",");
                    sb.AppendLine($"                Entries = new AttentionData[{EntryCount}]");
                    sb.AppendLine("                {");

                    for (int i = 0; i < EntryCount; i++)
                    {
                        var comment = LookAtComment(i);
                        sb.AppendLine($"                    new AttentionData {{ Priority = {FormatFloat(entries[i, 0])}, Timeout = {FormatFloat(entries[i, 1])} }}, // {comment}");
                    }

                    sb.AppendLine("                }");
                    sb.AppendLine("            },");
                }
            }

            sb.AppendLine("        };");
        }

        private static string LookAtComment(int index) => index switch
        {
            0  => "None",
            1  => "Idle",
            2  => "AutoListen",
            3  => "FreeLook",
            4  => "Respond",
            5  => "Hover",
            6  => "Conversation",
            7  => "Select",
            8  => "Focus",
            9  => "Mouselook",
            10 => "Clear",
            _  => index.ToString(Inv)
        };

        private static string FormatFloat(float f)
        {
            if (f == 0f) return "0f";
            return f.ToString("G9", Inv) + "f";
        }

        private static string Escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
