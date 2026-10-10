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
using System.Globalization;
using System.IO;
using System.Text;
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;
using NumVector3 = System.Numerics.Vector3;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Writes decoded Second Life meshes and prims (<see cref="FacetedMesh"/>) out as a Wavefront
    /// <c>.obj</c> with a <c>.mtl</c> beside it.
    /// </summary>
    /// <remarks>
    /// OBJ has no transforms, so every prim is put in its place in the file itself, in the same linkset layout
    /// <see cref="GltfExporter"/> uses (the root prim's world position is dropped, so an export sits at the
    /// origin). The file is Y-up, which is what <see cref="ObjLoader"/> expects by default and what most
    /// programs assume. Each prim is an <c>o</c> object and each material a <c>usemtl</c> run.
    /// Rigged meshes are exported as they stand in the pose their bind shape matrix gives them, because OBJ
    /// cannot hold a skin. Only each face's color, opacity and texture are exported, and the texture image is
    /// written beside the model under its asset ID. Texture repeats, offsets and rotation are not applied, and
    /// neither are glow, shininess or full bright. The meshes given are not changed.
    /// </remarks>
    public sealed class ObjExporter
    {
        private readonly List<FacetedMesh> _meshes = new List<FacetedMesh>();

        /// <summary>
        /// Supplies the encoded image for a face's texture, or null to leave that face untextured. Textures
        /// are JPEG2000 on the grid, so the caller decodes and re-encodes them as PNG or JPEG. Called at most
        /// once per texture. Without a provider no textures are exported.
        /// </summary>
        public Func<UUID, ExportImage?>? ImageProvider { get; set; }

        /// <summary>Written to a comment at the top of the file</summary>
        public string Generator { get; set; } = "LibreMetaverse.ModelFormats";

        /// <summary>Faces with an alpha below 1/100 are left out when this is set</summary>
        public bool SkipTransparentFaces { get; set; }

        /// <summary>Number of meshes added so far</summary>
        public int Count => _meshes.Count;

        /// <summary>
        /// Adds a mesh or prim. A mesh whose prim has a parent is placed in the linkset of the root prim with
        /// that local ID if that was added too, and otherwise it is exported on its own, with a warning.
        /// </summary>
        public void Add(FacetedMesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            _meshes.Add(mesh);
        }

        /// <summary>
        /// Builds the files of the export: the model, its <c>.mtl</c> and an image for each texture
        /// </summary>
        /// <param name="fileName">The model's file name, without a directory, ending in <c>.obj</c></param>
        public IReadOnlyList<ExportedFile> Build(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
                !fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The file name must be a name ending in .obj, with no directory", nameof(fileName));
            }

            string mtlName = Path.ChangeExtension(fileName, ".mtl");
            var materials = new ExportMaterials(ImageProvider);
            var obj = new StringBuilder();
            obj.Append("# ").Append(ExportLayout.SingleLine(Generator)).Append('\n');
            obj.Append("mtllib ").Append(ExportLayout.SingleLine(mtlName)).Append('\n');

            int written = 0; // vertices written so far, which the indices of the next face count from
            foreach (var part in ExportLayout.Parts(_meshes))
            {
                WritePart(obj, part, materials, ref written);
            }

            var mtl = new StringBuilder();
            mtl.Append("# ").Append(ExportLayout.SingleLine(Generator)).Append('\n');
            foreach (var material in materials.Materials)
            {
                mtl.Append("newmtl ").Append(material.Name).Append('\n');
                mtl.Append("Kd ").Append(Number(material.Color.R)).Append(' ').Append(Number(material.Color.G))
                   .Append(' ').Append(Number(material.Color.B)).Append('\n');
                mtl.Append("d ").Append(Number(material.Color.A)).Append('\n');
                if (material.TextureFile != null) mtl.Append("map_Kd ").Append(material.TextureFile).Append('\n');
                mtl.Append('\n');
            }

            var files = new List<ExportedFile>
            {
                new ExportedFile(fileName, new UTF8Encoding(false).GetBytes(obj.ToString())),
                new ExportedFile(mtlName, new UTF8Encoding(false).GetBytes(mtl.ToString()))
            };
            files.AddRange(materials.Images);
            return files;
        }

        /// <summary>
        /// Builds the export and writes it to a directory: the model at <paramref name="path"/>, its
        /// <c>.mtl</c> and the texture images beside it
        /// </summary>
        /// <exception cref="ArgumentException">The file name does not end in <c>.obj</c></exception>
        public void Save(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            var files = Build(Path.GetFileName(path));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
            Directory.CreateDirectory(directory);
            foreach (var file in files)
            {
                File.WriteAllBytes(Path.Combine(directory, file.Name), file.Data);
            }
        }

        private void WritePart(StringBuilder obj, ExportPart part, ExportMaterials materials, ref int written)
        {
            // Out to the file's space, which is Y-up. The matrix is not turned into the one that normals use
            // until it has everything in it: a normal goes through the inverse transpose.
            var toFile = part.Transform * ToNumerics(ModelAxes.ZUpToYUp);
            if (!NumMatrix.Invert(toFile, out var inverse))
            {
                Logger.Warn($"Not exporting {part.Name}: its size is zero along an axis");
                return;
            }
            var normalMatrix = NumMatrix.Transpose(inverse);

            // A mirrored mesh turns its triangles inside out, so they are written the other way round
            bool mirrored = toFile.GetDeterminant() < 0f;

            bool header = false;
            var faces = part.Mesh.Faces;
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                if (!ExportLayout.CanExport(face, part.Name, f)) continue;
                if (SkipTransparentFaces && face.TextureFace != null && face.TextureFace.RGBA.A < 0.01f) continue;

                if (!header)
                {
                    obj.Append("o ").Append(part.Name).Append('\n');
                    header = true;
                }

                int count = face.Vertices.Count;
                var normals = new Vector3[count];
                bool allNormals = true;
                for (int v = 0; v < count; v++)
                {
                    var vertex = face.Vertices[v];
                    var p = NumVector3.Transform(new NumVector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), toFile);
                    obj.Append("v ").Append(Number(p.X)).Append(' ').Append(Number(p.Y)).Append(' ').Append(Number(p.Z)).Append('\n');

                    var uv = vertex.TexCoord;
                    obj.Append("vt ").Append(Number(Utils.IsFinite(uv.X) ? uv.X : 0f)).Append(' ')
                       .Append(Number(Utils.IsFinite(uv.Y) ? uv.Y : 0f)).Append('\n');

                    if (ExportLayout.TryUnit(vertex.Normal, out var unit))
                    {
                        var n = NumVector3.Normalize(NumVector3.TransformNormal(new NumVector3(unit.X, unit.Y, unit.Z), normalMatrix));
                        normals[v] = new Vector3(n.X, n.Y, n.Z);
                    }
                    else
                    {
                        allNormals = false;
                    }
                }
                if (allNormals)
                {
                    foreach (var n in normals)
                        obj.Append("vn ").Append(Number(n.X)).Append(' ').Append(Number(n.Y)).Append(' ').Append(Number(n.Z)).Append('\n');
                }

                obj.Append("usemtl ").Append(materials.For(face).Name).Append('\n');
                int triangles = face.Indices.Count / 3;
                for (int t = 0; t < triangles; t++)
                {
                    obj.Append('f');
                    for (int c = 0; c < 3; c++)
                    {
                        // Counted from the start of the file, and one-based
                        int corner = mirrored ? (c == 0 ? 0 : 3 - c) : c;
                        int index = written + face.Indices[t * 3 + corner] + 1;
                        obj.Append(' ').Append(index).Append('/').Append(index);
                        if (allNormals) obj.Append('/').Append(index);
                    }
                    obj.Append('\n');
                }
                written += count;
            }
        }

        private static NumMatrix ToNumerics(Matrix4 m)
        {
            return new NumMatrix(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }

        // Adding zero turns -0 into 0, which reads better and means the same
        private static string Number(float value) => (value + 0f).ToString("R", CultureInfo.InvariantCulture);
    }
}
