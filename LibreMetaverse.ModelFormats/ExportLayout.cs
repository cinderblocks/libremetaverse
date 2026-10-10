/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.Text;
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;
using NumQuaternion = System.Numerics.Quaternion;
using NumVector3 = System.Numerics.Vector3;

namespace LibreMetaverse.ImportExport
{
    /// <summary>One mesh of an export, and where in the exported scene it goes</summary>
    internal sealed class ExportPart
    {
        public FacetedMesh Mesh = null!;

        /// <summary>A name for the mesh that is safe to write on one line of a text file</summary>
        public string Name = string.Empty;

        /// <summary>
        /// Takes a vertex of the mesh to its place in the scene, which is Z-up with the linkset's root prim
        /// at the origin. A row vector matrix, like the rest of the library.
        /// </summary>
        public NumMatrix Transform = NumMatrix.Identity;

        public bool Rigged;
    }

    /// <summary>
    /// How the exporters lay out what they were given: which prims make up a linkset, where each one sits
    /// in it, and what to call it. <see cref="GltfExporter"/> uses the grouping, and the formats that have
    /// no skins use all of it.
    /// </summary>
    internal static class ExportLayout
    {
        internal static bool IsRigged(FacetedMesh mesh)
        {
            return mesh.SkinData != null && mesh.SkinData.JointNames != null && mesh.SkinData.JointNames.Length > 0;
        }

        internal static string NameOf(FacetedMesh mesh)
        {
            var name = mesh.Prim.Properties?.Name;
            return !string.IsNullOrEmpty(name) ? name! : "prim" + mesh.Prim.LocalID;
        }

        /// <summary>
        /// A name that stays on one line. Prim names come from other residents, and a line break in one would
        /// let it write lines of its own into a text format, or break an XML document with a character it
        /// cannot hold.
        /// </summary>
        internal static string SingleLine(string name)
        {
            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
                {
                    sb.Append(c).Append(name[++i]);
                    continue;
                }

                // Controls, line and paragraph separators, lone surrogates and the two characters XML forbids
                bool bad = c < ' ' || (c >= '\u007f' && c <= '\u009f') || c == '\u2028' || c == '\u2029' ||
                           char.IsSurrogate(c) || c == '\uFFFE' || c == '\uFFFF';
                sb.Append(bad ? ' ' : c);
            }
            var result = sb.ToString().Trim();
            return result.Length > 0 ? result : "prim";
        }

        /// <summary>
        /// Sorts the meshes into linksets. A mesh whose prim has a parent is placed with the root prim that has
        /// that local ID if that was added too, and otherwise it is exported on its own, with a warning. Rigged
        /// meshes are left out: they are placed by their skin, not by their prim.
        /// </summary>
        internal static void Group(IEnumerable<FacetedMesh> meshes, out List<FacetedMesh> roots,
            out Dictionary<uint, List<FacetedMesh>> children)
        {
            roots = new List<FacetedMesh>();
            children = new Dictionary<uint, List<FacetedMesh>>();
            var known = new HashSet<uint>();

            foreach (var mesh in meshes)
            {
                if (!IsRigged(mesh) && mesh.Prim.ParentID == 0) known.Add(mesh.Prim.LocalID);
            }
            foreach (var mesh in meshes)
            {
                if (IsRigged(mesh)) continue;

                uint parent = mesh.Prim.ParentID;
                if (parent == 0)
                {
                    roots.Add(mesh);
                }
                else if (known.Contains(parent))
                {
                    if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<FacetedMesh>();
                    list.Add(mesh);
                }
                else
                {
                    Logger.Warn($"Exporting prim {mesh.Prim.LocalID} on its own: its parent {parent} was not added, or is a rigged mesh");
                    roots.Add(mesh);
                }
            }
        }

        /// <summary>
        /// Puts every mesh in its place, for the formats that cannot hold a skin. Linksets are laid out the
        /// way <see cref="GltfExporter"/> lays them out: the root prim's rotation applies to the whole
        /// linkset, the root's mesh is scaled by its size, and each child sits at its own position, rotation
        /// and size within that. A rigged mesh is put in the pose its bind shape matrix gives it, with its
        /// skin dropped.
        /// </summary>
        internal static List<ExportPart> Parts(IEnumerable<FacetedMesh> meshes)
        {
            var all = new List<FacetedMesh>(meshes);
            Group(all, out var roots, out var children);
            var parts = new List<ExportPart>();

            foreach (var root in roots)
            {
                var group = NumMatrix.CreateFromQuaternion(Unit(root.Prim.Rotation));
                parts.Add(Part(root, Scale(root.Prim.Scale) * group, rigged: false));

                if (!children.TryGetValue(root.Prim.LocalID, out var kids)) continue;
                foreach (var child in kids)
                {
                    var local = Scale(child.Prim.Scale) * NumMatrix.CreateFromQuaternion(Unit(child.Prim.Rotation)) *
                                NumMatrix.CreateTranslation(ToNumerics(child.Prim.Position));
                    parts.Add(Part(child, local * group, rigged: false));
                }
            }

            foreach (var mesh in all)
            {
                if (!IsRigged(mesh)) continue;

                Logger.Warn($"{NameOf(mesh)} is a rigged mesh, and this format cannot hold a skin: " +
                            "it is exported as it stands in the pose its bind shape gives it");
                parts.Add(Part(mesh, RiggedSkinMath.FloatsToMatrix(mesh.SkinData!.BindShapeMatrix), rigged: true));
            }

            return parts;
        }

        private static ExportPart Part(FacetedMesh mesh, NumMatrix transform, bool rigged)
        {
            return new ExportPart { Mesh = mesh, Name = SingleLine(NameOf(mesh)), Transform = transform, Rigged = rigged };
        }

        private static NumMatrix Scale(Vector3 scale) => NumMatrix.CreateScale(ToNumerics(scale));

        private static NumVector3 ToNumerics(Vector3 v) => new NumVector3(v.X, v.Y, v.Z);

        private static NumQuaternion Unit(Quaternion q)
        {
            var n = new NumQuaternion(q.X, q.Y, q.Z, q.W);
            float length = n.Length();
            return length > 1e-6f && !float.IsInfinity(length) ? NumQuaternion.Normalize(n) : NumQuaternion.Identity;
        }

        /// <summary>
        /// Checks that a face can be written: it has vertices and triangles, the triangles only use vertices
        /// it has, and no position is infinite or not a number.
        /// </summary>
        internal static bool CanExport(Face face, string owner, int faceIndex)
        {
            if (face.Vertices == null || face.Indices == null) return false;
            int vertexCount = face.Vertices.Count;
            if (vertexCount == 0 || face.Indices.Count < 3) return false;

            foreach (var index in face.Indices)
            {
                if (index >= vertexCount)
                {
                    Logger.Warn($"Not exporting face {faceIndex} of {owner}: a triangle refers to a missing vertex");
                    return false;
                }
            }
            foreach (var vertex in face.Vertices)
            {
                if (!vertex.Position.IsFinite())
                {
                    Logger.Warn($"Not exporting face {faceIndex} of {owner}: a vertex position is not a number");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Decoded normals are quantized, so they are not quite unit length, and a mesh asset with no normal block
        /// decodes to all zeroes. Returns false when there is no normal to write.
        /// </summary>
        internal static bool TryUnit(Vector3 normal, out Vector3 unit)
        {
            float length = normal.Length();
            if (!(length > 1e-6f) || float.IsInfinity(length))
            {
                unit = Vector3.Zero;
                return false;
            }
            unit = normal / length;
            return true;
        }
    }
}
