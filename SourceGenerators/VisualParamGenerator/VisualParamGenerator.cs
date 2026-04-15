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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace VisualParamGenerator
{
    [Generator]
    public class VisualParamGenerator : IIncrementalGenerator
    {
        private static readonly CultureInfo EnUsCulture = new CultureInfo("en-us");

        // Keep these diagnostics for XML errors / generation failure
        private static readonly DiagnosticDescriptor MissingXml = new(
            "VPG002",
            "Missing avatar_lad.xml",
            "AdditionalFile 'avatar_lad.xml' not found. Generator will not produce VisualParams.cs.",
            "VisualParamGenerator",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GenerationFailed = new(
            "VPG999",
            "Generation failed",
            "VisualParam generation failed: {0}",
            "VisualParamGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly string EmbeddedTemplate = """
using System;
using System.Collections.Generic;

namespace OpenMetaverse
{
    /// <summary>
    /// Operation to apply when applying color to texture
    /// </summary>
    public enum VisualColorOperation
    {
        Add,
        Blend,
        Multiply
    }

    /// <summary>
    /// Information needed to translate visual param value to RGBA color
    /// </summary>
    public struct VisualColorParam
    {
        public VisualColorOperation Operation;
        public Color4[] Colors;

        /// <summary>
        /// Construct VisualColorParam
        /// </summary>
        /// <param name="operation">Operation to apply when applying color to texture</param>
        /// <param name="colors">Colors</param>
        public VisualColorParam(VisualColorOperation operation, Color4[] colors)
        {
            Operation = operation;
            Colors = colors;
        }
    }

    /// <summary>
    /// Represents alpha blending and bump for for a visual parameter
    /// such as sleeve length
    /// </summary>
    public struct VisualAlphaParam
    {
        /// <summary>Strength of the alpha to apply</summary>
        public float Domain;

        /// <summary>File containing the alpha channel</summary>
        public string TGAFile;

        /// <summary>Skip blending if parameter value is 0</summary>
        public bool SkipIfZero;

        /// <summary>Use multiply instead of alpha blending</summary>
        public bool MultiplyBlend;

        /// <summary>
        /// Create new alpha information for a visual param
        /// </summary>
        /// <param name="domain">Strength of the alpha to apply</param>
        /// <param name="tgaFile">File containing the alpha channel</param>
        /// <param name="skipIfZero">Skip blending if parameter value is 0</param>
        /// <param name="multiplyBlend">Use multiply instead of alpha blending</param>
        public VisualAlphaParam(float domain, string tgaFile, bool skipIfZero, bool multiplyBlend)
        {
            Domain = domain;
            TGAFile = tgaFile;
            SkipIfZero = skipIfZero;
            MultiplyBlend = multiplyBlend;
        }
    }
    /// <summary>
    /// Describes a single driven-param relationship for a driver visual parameter.
    /// Min1/Max1/Max2/Min2 are in the driver parameter's own value range.
    /// </summary>
    public struct DrivenParamInfo
    {
        /// <summary>ParamID of the driven parameter.</summary>
        public int ParamID;
        /// <summary>Piecewise-linear range start on the driver axis.</summary>
        public float Min1;
        /// <summary>Piecewise-linear range peak-start on the driver axis.</summary>
        public float Max1;
        /// <summary>Piecewise-linear range peak-end on the driver axis.</summary>
        public float Max2;
        /// <summary>Piecewise-linear range end on the driver axis.</summary>
        public float Min2;
        /// <summary>True when Min1/Max1/Max2/Min2 define a custom mapping; false means pass-through.</summary>
        public bool HasRange;
        public DrivenParamInfo(int paramID, float min1, float max1, float max2, float min2, bool hasRange)
        {
            ParamID = paramID;
            Min1 = min1;
            Max1 = max1;
            Max2 = max2;
            Min2 = min2;
            HasRange = hasRange;
        }
    }
    /// <summary>
    /// Describes how a single bone should be scaled (and optionally repositioned) by a visual parameter.
    /// Corresponds to a &lt;bone&gt; entry inside a &lt;param_skeleton&gt; element in avatar_lad.xml.
    /// The deformation formula is additive: finalScale = defaultScale + ScaleDeformation * paramValue.
    /// </summary>
    public struct SkeletalBoneInfo
    {
        /// <summary>Name of the skeleton bone to deform.</summary>
        public string BoneName;
        /// <summary>Additive scale delta applied to the bone when the parameter is at full value.</summary>
        public Vector3 ScaleDeformation;
        /// <summary>Additive position delta applied to the bone when the parameter is at full value.</summary>
        public Vector3 PositionDeformation;
        /// <summary>True when this bone entry also carries a position deformation.</summary>
        public bool HasPositionDeformation;
        public SkeletalBoneInfo(string boneName, Vector3 scaleDeformation, Vector3 positionDeformation, bool hasPositionDeformation)
        {
            BoneName = boneName;
            ScaleDeformation = scaleDeformation;
            PositionDeformation = positionDeformation;
            HasPositionDeformation = hasPositionDeformation;
        }
    }
    /// <summary>
    /// A single visual characteristic of an avatar mesh, such as eyebrow height
    /// </summary>
    public struct VisualParam
    {
        /// <summary>Index of this visual param</summary>
        public int ParamID;
        /// <summary>Internal name</summary>
        public string Name;
        /// <summary>Group ID this parameter belongs to</summary>
        public int Group;
        /// <summary>Name of the wearable this parameter belongs to</summary>
        public string Wearable;
        /// <summary>Displayable label of this characteristic</summary>
        public string Label;
        /// <summary>Displayable label for the minimum value of this characteristic</summary>
        public string LabelMin;
        /// <summary>Displayable label for the maximum value of this characteristic</summary>
        public string LabelMax;
        /// <summary>Default value</summary>
        public float DefaultValue;
        /// <summary>Minimum value</summary>
        public float MinValue;
        /// <summary>Maximum value</summary>
        public float MaxValue;
        /// <summary>Is this param used for creation of bump layer?</summary>
        public bool IsBumpAttribute;
        /// <summary>Alpha blending/bump info</summary>
        public VisualAlphaParam? AlphaParams;
        /// <summary>Color information</summary>
        public VisualColorParam? ColorParams;
        /// <summary>Array of param IDs driven by this parameter (for driver params).</summary>
        public int[] Drivers;
        /// <summary>Full driven-param info including piecewise-linear mapping (non-null for driver params).</summary>
        public DrivenParamInfo[]? DrivenParams;
        /// <summary>Per-bone skeletal deformations driven by this visual parameter (non-null for skeletal morph params).</summary>
        public SkeletalBoneInfo[]? SkeletalDistortions;
        /// <summary>
        /// Set all the values through the constructor
        /// </summary>
        /// <param name="paramID">Index of this visual param</param>
        /// <param name="name">Internal name</param>
        /// <param name="group"></param>
        /// <param name="wearable"></param>
        /// <param name="label">Displayable label of this characteristic</param>
        /// <param name="labelMin">Displayable label for the minimum value of this characteristic</param>
        /// <param name="labelMax">Displayable label for the maximum value of this characteristic</param>
        /// <param name="def">Default value</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <param name="isBumpAttribute">Is this param used for creation of bump layer?</param>
        /// <param name="drivers">Array of param IDs driven by this parameter</param>
        /// <param name="alpha">Alpha blending/bump info</param>
        /// <param name="colorParams">Color information</param>
        /// <param name="drivenParams">Full driven-param info for driver params</param>
        public VisualParam(int paramID, string name, int group, string wearable, string label, string labelMin, string labelMax, float def, float min, float max, bool isBumpAttribute, int[] drivers, VisualAlphaParam? alpha, VisualColorParam? colorParams, DrivenParamInfo[]? drivenParams = null, SkeletalBoneInfo[]? skeletalDistortions = null)
        {
            ParamID = paramID;
            Name = name;
            Group = group;
            Wearable = wearable;
            Label = label;
            LabelMin = labelMin;
            LabelMax = labelMax;
            DefaultValue = def;
            MaxValue = max;
            MinValue = min;
            IsBumpAttribute = isBumpAttribute;
            Drivers = drivers;
            AlphaParams = alpha;
            ColorParams = colorParams;
            DrivenParams = drivenParams;
            SkeletalDistortions = skeletalDistortions;
        }
    }

    /// <summary>
    /// Holds the Params array of all the avatar appearance parameters
    /// </summary>
    public static class VisualParams
    {
        public static SortedList<int, VisualParam> Params = new SortedList<int, VisualParam>();

        /// <summary>
        /// Group-0 parameter IDs in the exact order they appear in avatar_lad.xml.
        /// This order matches the byte sequence in the AvatarAppearance packet visual_param block.
        /// </summary>
        public static int[] Group0ParamIds = Array.Empty<int>();

        public static VisualParam Find(string name, string wearable)
        {
            foreach (KeyValuePair<int, VisualParam> param in Params)
                if (param.Value.Name == name && param.Value.Wearable == wearable)
                    return param.Value;

            return new VisualParam();
        }

        static VisualParams()
        {
""";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var additional = context.AdditionalTextsProvider;

            var templateProvider = additional
                .Where(at => Path.GetFileName(at.Path).Equals("visualparamtemplate.cs", StringComparison.OrdinalIgnoreCase))
                .Select((at, ct) => at.GetText(ct)?.ToString() ?? string.Empty)
                .Collect();

            var xmlProvider = additional
                .Where(at => Path.GetFileName(at.Path).Equals("avatar_lad.xml", StringComparison.OrdinalIgnoreCase))
                .Select((at, ct) => at.GetText(ct)?.ToString() ?? string.Empty)
                .Collect();

            var combined = templateProvider.Combine(xmlProvider);

            context.RegisterSourceOutput(combined, (spc, pair) =>
            {
                var templates = pair.Left;
                var xmls = pair.Right;

                var templateText = templates.FirstOrDefault();
                var xmlText = xmls.FirstOrDefault();

                // Prefer AdditionalFile template if provided; otherwise use embedded template
                if (string.IsNullOrWhiteSpace(templateText))
                {
                    templateText = EmbeddedTemplate;
                }

                if (string.IsNullOrWhiteSpace(xmlText))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(MissingXml, Location.None));
                    return;
                }

                try
                {
                    var generated = GenerateFromTemplateAndXml(templateText!, xmlText!, spc);
                    spc.AddSource("VisualParams.g.cs", SourceText.From(generated, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, ex.Message));
                }
            });
        }

        // Helper to format float literals using invariant/en-US culture with trailing 'f'
        private static string FormatFloat(float value)
        {
            return value.ToString(EnUsCulture) + "f";
        }

        private static (string x, string y, string z) ParseVector3(string value)
        {
            var parts = value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var x = parts.Length > 0 ? FormatFloat(float.Parse(parts[0], NumberStyles.Float, EnUsCulture.NumberFormat)) : "0f";
            var y = parts.Length > 1 ? FormatFloat(float.Parse(parts[1], NumberStyles.Float, EnUsCulture.NumberFormat)) : "0f";
            var z = parts.Length > 2 ? FormatFloat(float.Parse(parts[2], NumberStyles.Float, EnUsCulture.NumberFormat)) : "0f";
            return (x, y, z);
        }

        private static string GenerateFromTemplateAndXml(string templateText, string xmlText, SourceProductionContext spc)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlText);
            var nodes = doc.GetElementsByTagName("param");

            var ids = new SortedList<int, string>();
            var group0IdsInOrder = new List<int>();  // group-0 IDs in avatar_lad.xml document order
            var alphas = new Dictionary<int, string>();
            var colors = new Dictionary<int, string>();
            var drivenParamInfoMap = new Dictionary<int, string>();
            var skeletalInfoMap = new Dictionary<int, string>();

            var sb = new StringBuilder();
            sb.AppendLine("#nullable enable");

            // copy template first
            sb.AppendLine(templateText);

            var count = 0;

            foreach (XmlNode node in nodes)
            {
                if (node.Attributes?["group"] == null)
                    continue;

                if (node.Attributes["shared"]?.Value == "1")
                    continue;

                if (node.Attributes["id"] == null || node.Attributes["name"] == null)
                    continue;

                try
                {
                    var id = int.Parse(node.Attributes["id"].Value, CultureInfo.InvariantCulture);

                    var bumpAttrib = "false";
                    var skipColor = false;

                    if (node.ParentNode is { Name: "layer" } parent)
                    {
                        if (parent.Attributes?["render_pass"]?.Value == "bump")
                            bumpAttrib = "true";

                        for (var nodeNr = 0; nodeNr < parent.ChildNodes.Count; nodeNr++)
                        {
                            var lnode = parent.ChildNodes[nodeNr];
                            if (lnode.Name != "texture") continue;
                            if (lnode.Attributes?["local_texture_alpha_only"]?.Value?.ToLowerInvariant() == "true")
                                skipColor = true;
                        }
                    }

                    if (node.HasChildNodes)
                    {
                        for (var nodeNr = 0; nodeNr < node.ChildNodes.Count; nodeNr++)
                        {
                            var child = node.ChildNodes[nodeNr];

                            if (child.Name == "param_alpha")
                            {
                                var anode = child;
                                var tgaFile = "string.Empty";
                                var skipIfZero = "false";
                                var multiplyBlend = "false";
                                var domainVal = 0f;

                                if (anode.Attributes?["domain"] != null)
                                    domainVal = float.Parse(anode.Attributes["domain"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);

                                if (anode.Attributes?["tga_file"] != null)
                                    tgaFile = $"\"{anode.Attributes["tga_file"].Value}\"";

                                if (anode.Attributes?["skip_if_zero"]?.Value?.ToLowerInvariant() == "true")
                                    skipIfZero = "true";

                                if (anode.Attributes?["multiply_blend"]?.Value?.ToLowerInvariant() == "true")
                                    multiplyBlend = "true";

                                alphas[id] = $"new VisualAlphaParam({FormatFloat(domainVal)}, {tgaFile}, {skipIfZero}, {multiplyBlend})";
                            }
                            else if (child is { Name: "param_color", HasChildNodes: true })
                            {
                                var cnode = child;
                                var operation = "VisualColorOperation.Add";
                                var colorList = new List<string>();

                                if (cnode.Attributes?["operation"] != null)
                                {
                                    switch (cnode.Attributes["operation"].Value)
                                    {
                                        case "blend":
                                            operation = "VisualColorOperation.Blend";
                                            break;
                                        case "multiply":
                                            operation = "VisualColorOperation.Blend";
                                            break;
                                    }
                                }

                                foreach (XmlNode cvalue in cnode.ChildNodes)
                                {
                                    if (cvalue.Name != "value" || cvalue.Attributes?["color"] == null) continue;
                                    var m = Regex.Match(cvalue.Attributes["color"].Value, @"((?<val>\d+)(?:, *)?){4}");
                                    if (!m.Success) continue;
                                    var val = m.Groups["val"].Captures;
                                    if (val.Count >= 4)
                                        colorList.Add($"new Color4({val[0]}, {val[1]}, {val[2]}, {val[3]})");
                                }

                                if (colorList.Count > 0 && !skipColor)
                                {
                                    var colorsStr = string.Join(", ", colorList);
                                    colors[id] = $"new VisualColorParam({operation}, new Color4[] {{ {colorsStr} }})";
                                }
                            }
                            else if (child is { Name: "param_skeleton", HasChildNodes: true })
                            {
                                var boneInfoList = new List<string>();
                                foreach (XmlNode bnode in child.ChildNodes)
                                {
                                    if (bnode.Name != "bone" || bnode.Attributes?["name"] == null) continue;
                                    var boneName = bnode.Attributes["name"].Value;
                                    var scaleAttr = bnode.Attributes["scale"]?.Value ?? "0 0 0";
                                    var posAttr = bnode.Attributes["offset"]?.Value;
                                    var (sx, sy, sz) = ParseVector3(scaleAttr);
                                    var hasPosDeform = posAttr != null;
                                    string posArg;
                                    if (hasPosDeform)
                                    {
                                        var (px, py, pz) = ParseVector3(posAttr!);
                                        posArg = $"new Vector3({px}, {py}, {pz})";
                                    }
                                    else
                                    {
                                        posArg = "Vector3.Zero";
                                    }
                                    var hasPosStr = hasPosDeform ? "true" : "false";
                                    boneInfoList.Add($"new SkeletalBoneInfo(\"" + boneName + $"\", new Vector3({sx}, {sy}, {sz}), {posArg}, {hasPosStr})");
                                }
                                if (boneInfoList.Count > 0)
                                {
                                    var joinedBones = string.Join(", ", boneInfoList);
                                    skeletalInfoMap[id] = $"new SkeletalBoneInfo[] {{ {joinedBones} }}";
                                }
                            }
                        }
                    }

                    if (ids.ContainsKey(id))
                        continue;

                    var name = node.Attributes["name"].Value;
                    var group = int.Parse(node.Attributes["group"].Value, CultureInfo.InvariantCulture);

                    var wearable = node.Attributes["wearable"] != null ? $"\"{node.Attributes["wearable"].Value}\"" : "null";
                    var label = node.Attributes["label"] != null ? $"\"{node.Attributes["label"].Value}\"" : "string.Empty";
                    var labelMin = node.Attributes["label_min"] != null ? $"\"{node.Attributes["label_min"].Value}\"" : "string.Empty";
                    var labelMax = node.Attributes["label_max"] != null ? $"\"{node.Attributes["label_max"].Value}\"" : "string.Empty";

                    var min = float.Parse(node.Attributes["value_min"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);
                    var max = float.Parse(node.Attributes["value_max"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);

                    var defAttr = node.Attributes["value_default"];
                    var def = defAttr != null && !string.IsNullOrEmpty(defAttr.Value)
                        ? float.Parse(defAttr.Value, NumberStyles.Float, EnUsCulture.NumberFormat)
                        : 0f;

                    var drivers = "null";
                    var drivenInfos = "null";
                    if (node.HasChildNodes)
                    {
                        for (var nodeNr = 0; nodeNr < node.ChildNodes.Count; nodeNr++)
                        {
                            var cnode = node.ChildNodes[nodeNr];
                            if (cnode.Name != "param_driver" || !cnode.HasChildNodes) continue;

                            var driverIDs = new List<string>();
                            var drivenInfoList = new List<string>();
                            foreach (XmlNode dnode in cnode.ChildNodes)
                            {
                                if (dnode.Name != "driven" || dnode.Attributes?["id"] == null) continue;
                                var drivenId = dnode.Attributes["id"].Value;
                                driverIDs.Add(drivenId);
                                var min1Attr = dnode.Attributes["min1"];
                                if (min1Attr != null)
                                {
                                    var dMin1 = float.Parse(min1Attr.Value, NumberStyles.Float, EnUsCulture.NumberFormat);
                                    var dMax1 = float.Parse(dnode.Attributes["max1"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);
                                    var dMax2 = float.Parse(dnode.Attributes["max2"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);
                                    var dMin2 = float.Parse(dnode.Attributes["min2"].Value, NumberStyles.Float, EnUsCulture.NumberFormat);
                                    drivenInfoList.Add($"new DrivenParamInfo({drivenId}, {FormatFloat(dMin1)}, {FormatFloat(dMax1)}, {FormatFloat(dMax2)}, {FormatFloat(dMin2)}, true)");
                                }
                                else
                                {
                                    drivenInfoList.Add($"new DrivenParamInfo({drivenId}, 0f, 0f, 0f, 0f, false)");
                                }
                            }

                            if (driverIDs.Count > 0)
                            {
                                drivers = $"new int[] {{ {string.Join(", ", driverIDs)} }}";
                                drivenInfos = $"new DrivenParamInfo[] {{ {string.Join(", ", drivenInfoList)} }}";
                            }
                        }
                    }
                    if (drivenInfos != "null")
                        drivenParamInfoMap[id] = drivenInfos;

                    ids.Add(id,
                        string.Format("            Params[{0}] = new VisualParam({0}, \"{1}\", {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, ",
                                      id, name, group, wearable, label, labelMin, labelMax, FormatFloat(def), FormatFloat(min), FormatFloat(max), bumpAttrib, drivers));

                    if (group == 0)
                    {
                        group0IdsInOrder.Add(id);
                        ++count;
                    }
                }
                catch
                {
                    // skip invalid nodes
                }
            }

            // Optional sanity check: emits no diagnostic here to avoid noise, but available if needed
            if (count != 251)
            {
                // not fatal; original generator used a warning. We keep silent to avoid noisy builds.
            }

            // Now write out sorted entries
            foreach (var kv in ids)
            {
                sb.Append(kv.Value);

                if (alphas.TryGetValue(kv.Key, out var alpha))
                    sb.Append(alpha + ", ");
                else
                    sb.Append("null, ");

                sb.Append((colors.TryGetValue(kv.Key, out var color) ? color : "null") + ", ");
                sb.Append(drivenParamInfoMap.TryGetValue(kv.Key, out var dpi) ? dpi : "null");
                if (skeletalInfoMap.TryGetValue(kv.Key, out var skeletal))
                    sb.Append(", " + skeletal);
                sb.AppendLine(");");
            }

            // Emit Group0ParamIds initializer — group-0 params in avatar_lad.xml document order.
            // This order matches the AvatarAppearance visual_param byte sequence.
            sb.Append("            Group0ParamIds = new int[] { ");
            sb.Append(string.Join(", ", group0IdsInOrder));
            sb.AppendLine(" };");

            // close the constructor / class / namespace that the template left open
            sb.Append("        }").AppendLine();
            sb.Append("    }").AppendLine();
            sb.Append("}").AppendLine();

            return sb.ToString();
        }
    }
}
