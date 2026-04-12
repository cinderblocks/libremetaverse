/*
 * Copyright (c) 2024-2026, Sjofn LLC
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

using System.Collections.Generic;

namespace OpenMetaverse.Assets.Gltf
{
    /// <summary>A single draw call's geometry and material assignment.</summary>
    public sealed class GltfPrimitive
    {
        public const string ATTR_POSITION   = "POSITION";
        public const string ATTR_NORMAL     = "NORMAL";
        public const string ATTR_TANGENT    = "TANGENT";
        public const string ATTR_TEXCOORD_0 = "TEXCOORD_0";
        public const string ATTR_TEXCOORD_1 = "TEXCOORD_1";
        public const string ATTR_COLOR_0    = "COLOR_0";
        public const string ATTR_JOINTS_0   = "JOINTS_0";
        public const string ATTR_WEIGHTS_0  = "WEIGHTS_0";

        /// <summary>Maps attribute semantic names (e.g. "POSITION") to accessor indices.</summary>
        public Dictionary<string, int> Attributes { get; } = new Dictionary<string, int>();
        /// <summary>Accessor index for the index buffer. -1 for non-indexed geometry.</summary>
        public int Indices { get; set; } = -1;
        /// <summary>Index into <see cref="GltfDocument.Materials"/>. -1 means use default material.</summary>
        public int Material { get; set; } = -1;
        public GltfPrimitiveMode Mode { get; set; } = GltfPrimitiveMode.Triangles;
    }

    /// <summary>A named mesh composed of one or more primitives.</summary>
    public sealed class GltfMesh
    {
        public string? Name { get; set; }
        public List<GltfPrimitive> Primitives { get; } = new List<GltfPrimitive>();
        /// <summary>Morph target weights, if any.</summary>
        public double[]? Weights { get; set; }
    }

    /// <summary>A node in the scene hierarchy. Holds a transform and optional mesh or skin reference.</summary>
    public sealed class GltfNode
    {
        public string? Name { get; set; }
        /// <summary>Index into <see cref="GltfDocument.Meshes"/>. -1 if none.</summary>
        public int Mesh { get; set; } = -1;
        /// <summary>Index into <see cref="GltfDocument.Skins"/>. -1 if none.</summary>
        public int Skin { get; set; } = -1;
        /// <summary>Indices of child nodes.</summary>
        public List<int> Children { get; } = new List<int>();

        public Vector3 Translation { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;

        /// <summary>
        /// Column-major 4x4 matrix transform. When non-null it was explicitly specified in JSON
        /// and takes precedence over the TRS properties.
        /// </summary>
        public Matrix4? Matrix { get; set; }

        /// <summary>Morph target weights, if any.</summary>
        public double[]? Weights { get; set; }
    }

    /// <summary>A named collection of root nodes that forms one renderable scene.</summary>
    public sealed class GltfScene
    {
        public string? Name { get; set; }
        public List<int> Nodes { get; } = new List<int>();
    }

    /// <summary>Skinning data: maps joints to a skeleton and provides inverse-bind matrices.</summary>
    public sealed class GltfSkin
    {
        public string? Name { get; set; }
        /// <summary>Accessor index for MAT4 FLOAT inverse bind matrices. -1 means identity for all joints.</summary>
        public int InverseBindMatrices { get; set; } = -1;
        /// <summary>Index of the root skeleton node. -1 if unspecified.</summary>
        public int Skeleton { get; set; } = -1;
        /// <summary>Ordered list of node indices that act as joints.</summary>
        public List<int> Joints { get; } = new List<int>();
    }
}
