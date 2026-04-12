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
using System.Linq;
using System.Xml;

namespace OpenMetaverse.Rendering
{
    /// <summary>
    /// Represents an attachment point as defined in avatar_lad.xml.
    /// </summary>
    public class AvatarAttachmentPoint
    {
        /// <summary>Unique numeric ID of this attachment point.</summary>
        public int Id { get; }
        /// <summary>Group index for this attachment point.</summary>
        public int Group { get; }
        /// <summary>Human-readable name, e.g. "Chest".</summary>
        public string Name { get; }
        /// <summary>Name of the skeleton joint this attachment point is anchored to.</summary>
        public string Joint { get; }
        /// <summary>Symbolic location constant, e.g. "ATTACH_CHEST".</summary>
        public string Location { get; }
        /// <summary>Offset position relative to the anchor joint in local space.</summary>
        public Vector3 Position { get; }
        /// <summary>Euler rotation (degrees) relative to the anchor joint.</summary>
        public Vector3 Rotation { get; }
        /// <summary>True when this attachment point is visible in first-person view.</summary>
        public bool VisibleInFirstPerson { get; }

        internal AvatarAttachmentPoint(int id, int group, string name, string joint,
            string location, Vector3 position, Vector3 rotation, bool visibleInFirstPerson)
        {
            Id = id;
            Group = group;
            Name = name;
            Joint = joint;
            Location = location;
            Position = position;
            Rotation = rotation;
            VisibleInFirstPerson = visibleInFirstPerson;
        }
    }

    /// <summary>
    /// Describes a single avatar mesh LOD entry as defined in avatar_lad.xml.
    /// </summary>
    public class AvatarMeshDefinition
    {
        /// <summary>Mesh type, e.g. "headMesh", "upperBodyMesh".</summary>
        public string Type { get; }
        /// <summary>Level-of-detail index (0 = highest quality).</summary>
        public int LodLevel { get; }
        /// <summary>Filename of the .llm mesh asset.</summary>
        public string FileName { get; }
        /// <summary>Minimum pixel width at which this LOD level is used.</summary>
        public int MinPixelWidth { get; }

        internal AvatarMeshDefinition(string type, int lodLevel, string fileName, int minPixelWidth)
        {
            Type = type;
            LodLevel = lodLevel;
            FileName = fileName;
            MinPixelWidth = minPixelWidth;
        }
    }

    /// <summary>
    /// Stores the morphed position and scale of a single skeleton bone
    /// after visual-parameter deformations have been applied.
    /// </summary>
    public struct BoneTransform
    {
        /// <summary>Morphed position in local bone space.</summary>
        public Vector3 Position;
        /// <summary>Morphed scale.</summary>
        public Vector3 Scale;
    }

    /// <summary>
    /// Loads and exposes the complete avatar definition from avatar_lad.xml,
    /// including the linked skeleton, attachment points, and mesh LOD descriptors.
    /// Provides <see cref="ComputeBoneTransforms"/> to apply visual-parameter
    /// deformations to the skeleton's default bone positions and scales.
    /// </summary>
    public class LindenAvatarDefinition
    {
        /// <summary>The parsed avatar skeleton.</summary>
        public LindenSkeleton Skeleton { get; }

        /// <summary>All attachment points defined in avatar_lad.xml.</summary>
        public IReadOnlyList<AvatarAttachmentPoint> AttachmentPoints { get; }

        /// <summary>All mesh LOD definitions from avatar_lad.xml.</summary>
        public IReadOnlyList<AvatarMeshDefinition> MeshDefinitions { get; }

        private LindenAvatarDefinition(LindenSkeleton skeleton,
            IReadOnlyList<AvatarAttachmentPoint> attachmentPoints,
            IReadOnlyList<AvatarMeshDefinition> meshDefinitions)
        {
            Skeleton = skeleton;
            AttachmentPoints = attachmentPoints;
            MeshDefinitions = meshDefinitions;
        }

        /// <summary>
        /// Loads the avatar definition from avatar_lad.xml and the associated avatar_skeleton.xml.
        /// </summary>
        /// <param name="ladFileName">
        /// Path to avatar_lad.xml. Defaults to the linden/character directory under
        /// <see cref="Settings.RESOURCE_DIR"/>.
        /// </param>
        /// <param name="skeletonFileName">
        /// Path to avatar_skeleton.xml. Defaults to the linden/character directory under
        /// <see cref="Settings.RESOURCE_DIR"/>.
        /// </param>
        public static LindenAvatarDefinition Load(string? ladFileName = null, string? skeletonFileName = null)
        {
            if (ladFileName == null)
                ladFileName = System.IO.Path.Combine(Settings.RESOURCE_DIR ?? string.Empty, "character", "avatar_lad.xml");

            var skeleton = LindenSkeleton.Load(skeletonFileName);

            var doc = new XmlDocument();
            doc.Load(ladFileName);

            var attachmentPoints = ParseAttachmentPoints(doc);
            var meshDefinitions = ParseMeshDefinitions(doc);
            return new LindenAvatarDefinition(skeleton, attachmentPoints, meshDefinitions);
        }

        /// <summary>
        /// Computes morphed bone transforms by applying all visual-parameter skeletal
        /// deformations to the skeleton's default bone positions and scales.
        /// </summary>
        /// <remarks>
        /// Deformation formula (matching LLPolySkeletalDistortion::apply in the SL viewer):
        /// <c>finalScale = defaultScale + scaleDeformation * paramValue</c>
        /// </remarks>
        /// <param name="paramValues">
        /// Current visual-parameter values keyed by param ID.
        /// Parameters absent from this dictionary use their default value from <see cref="VisualParams.Params"/>.
        /// </param>
        /// <returns>
        /// Dictionary mapping bone name to its morphed <see cref="BoneTransform"/>.
        /// </returns>
        public Dictionary<string, BoneTransform> ComputeBoneTransforms(IReadOnlyDictionary<int, float> paramValues)
        {
            var result = new Dictionary<string, BoneTransform>(StringComparer.Ordinal);

            foreach (var joint in Skeleton.GetAllJoints())
            {
                if (string.IsNullOrEmpty(joint.name)) continue;
                var pos = joint.pos != null && joint.pos.Length >= 3
                    ? new Vector3(joint.pos[0], joint.pos[1], joint.pos[2])
                    : Vector3.Zero;
                var scale = joint.scale != null && joint.scale.Length >= 3
                    ? new Vector3(joint.scale[0], joint.scale[1], joint.scale[2])
                    : Vector3.One;
                result[joint.name] = new BoneTransform { Position = pos, Scale = scale };
            }

            foreach (var kv in VisualParams.Params)
            {
                var vp = kv.Value;
                if (vp.SkeletalDistortions == null) continue;

                if (!paramValues.TryGetValue(vp.ParamID, out var paramVal))
                    paramVal = vp.DefaultValue;

                foreach (var boneInfo in vp.SkeletalDistortions)
                {
                    if (!result.TryGetValue(boneInfo.BoneName, out var bt)) continue;
                    bt.Scale = bt.Scale + boneInfo.ScaleDeformation * paramVal;
                    if (boneInfo.HasPositionDeformation)
                        bt.Position = bt.Position + boneInfo.PositionDeformation * paramVal;
                    result[boneInfo.BoneName] = bt;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the attachment point with the given numeric ID, or <c>null</c> if not found.
        /// </summary>
        public AvatarAttachmentPoint? GetAttachmentPoint(int id)
        {
            return AttachmentPoints.FirstOrDefault(a => a.Id == id);
        }

        /// <summary>
        /// Returns the attachment point with the given name (case-insensitive), or <c>null</c> if not found.
        /// </summary>
        public AvatarAttachmentPoint? GetAttachmentPoint(string name)
        {
            return AttachmentPoints.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<AvatarAttachmentPoint> ParseAttachmentPoints(XmlDocument doc)
        {
            var list = new List<AvatarAttachmentPoint>();
            foreach (XmlNode node in doc.GetElementsByTagName("attachment_point"))
            {
                if (node.Attributes == null) continue;
                if (!TryParseInt(node.Attributes["id"]?.Value, out var id)) continue;
                TryParseInt(node.Attributes["group"]?.Value, out var group);
                var name = node.Attributes["name"]?.Value ?? string.Empty;
                var joint = node.Attributes["joint"]?.Value ?? string.Empty;
                var location = node.Attributes["location"]?.Value ?? string.Empty;
                var position = ParseVector3Attr(node.Attributes["position"]?.Value);
                var rotation = ParseVector3Attr(node.Attributes["rotation"]?.Value);
                var visibleInFirstPerson = string.Equals(
                    node.Attributes["visible_in_first_person"]?.Value, "true",
                    StringComparison.OrdinalIgnoreCase);
                list.Add(new AvatarAttachmentPoint(id, group, name, joint, location, position, rotation, visibleInFirstPerson));
            }
            return list;
        }

        private static IReadOnlyList<AvatarMeshDefinition> ParseMeshDefinitions(XmlDocument doc)
        {
            var list = new List<AvatarMeshDefinition>();
            foreach (XmlNode node in doc.GetElementsByTagName("mesh"))
            {
                if (node.Attributes == null) continue;
                var type = node.Attributes["type"]?.Value;
                if (type == null) continue;
                if (!TryParseInt(node.Attributes["lod"]?.Value, out var lod)) continue;
                var fileName = node.Attributes["file_name"]?.Value ?? string.Empty;
                TryParseInt(node.Attributes["min_pixel_width"]?.Value, out var minPixelWidth);
                list.Add(new AvatarMeshDefinition(type, lod, fileName, minPixelWidth));
            }
            return list;
        }

        private static Vector3 ParseVector3Attr(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Vector3.Zero;
            var parts = value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var x = parts.Length > 0 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var fx) ? fx : 0f;
            var y = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fy) ? fy : 0f;
            var z = parts.Length > 2 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fz) ? fz : 0f;
            return new Vector3(x, y, z);
        }

        private static bool TryParseInt(string? value, out int result)
        {
            result = 0;
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}
