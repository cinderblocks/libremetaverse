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
using System.Xml;
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;
using Path = System.IO.Path;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Writes decoded Second Life meshes and prims (<see cref="FacetedMesh"/>) out as a COLLADA 1.4.1
    /// <c>.dae</c>, with the texture images beside it.
    /// </summary>
    /// <remarks>
    /// The scene is Z-up, in meters, as Second Life is. Each prim is a node with its own full transform
    /// (<see cref="ColladaLoader"/> does not combine the transforms of nested nodes, and prims are not nested
    /// anyway), laid out the way <see cref="GltfExporter"/> lays out a linkset: the root prim's world position
    /// is dropped, so an export sits at the origin. Each prim's mesh is written as it is, in the unit cube the
    /// grid keeps it in, and the node's transform scales it to its size.
    /// Rigged meshes are exported as they stand in the pose their bind shape matrix gives them, because the
    /// skin is not written. A face's material has either its texture or its color, since COLLADA allows only
    /// one of them for the diffuse; its opacity is written either way. Texture repeats, offsets and rotation
    /// are not applied, and neither are glow, shininess or full bright. The meshes given are not changed.
    /// </remarks>
    public sealed class ColladaExporter
    {
        private const string Namespace = "http://www.collada.org/2005/11/COLLADASchema";

        private readonly List<FacetedMesh> _meshes = new List<FacetedMesh>();

        /// <summary>
        /// Supplies the encoded image for a face's texture, or null to leave that face untextured. Textures
        /// are JPEG2000 on the grid, so the caller decodes and re-encodes them as PNG or JPEG. Called at most
        /// once per texture. Without a provider no textures are exported.
        /// </summary>
        public Func<UUID, ExportImage?>? ImageProvider { get; set; }

        /// <summary>Written to the file's <c>authoring_tool</c></summary>
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

        /// <summary>Builds the files of the export: the model and an image for each texture</summary>
        /// <param name="fileName">The model's file name, without a directory, ending in <c>.dae</c></param>
        public IReadOnlyList<ExportedFile> Build(string fileName)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
                !fileName.EndsWith(".dae", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The file name must be a name ending in .dae, with no directory", nameof(fileName));
            }

            var materials = new ExportMaterials(ImageProvider);
            var geometries = new List<Geometry>();
            int number = 0;
            foreach (var part in ExportLayout.Parts(_meshes))
            {
                var geometry = BuildGeometry(part, "prim" + number, materials);
                if (geometry == null) continue;
                geometries.Add(geometry);
                number++;
            }

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                using (var xml = XmlWriter.Create(stream, settings))
                {
                    WriteDocument(xml, geometries, materials);
                }
                bytes = stream.ToArray();
            }

            var files = new List<ExportedFile> { new ExportedFile(fileName, bytes) };
            files.AddRange(materials.Images);
            return files;
        }

        /// <summary>
        /// Builds the export and writes it to a directory: the model at <paramref name="path"/> and the
        /// texture images beside it
        /// </summary>
        /// <exception cref="ArgumentException">The file name does not end in <c>.dae</c></exception>
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

        #region Geometry

        private sealed class Run
        {
            public ExportMaterial Material = null!;
            public bool Normals;
            public readonly List<int> Indices = new List<int>();
        }

        private sealed class Geometry
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public NumMatrix Transform;
            public readonly List<float> Positions = new List<float>();
            public readonly List<float> Normals = new List<float>();
            public readonly List<float> TexCoords = new List<float>();
            public readonly List<Run> Runs = new List<Run>();
        }

        private Geometry? BuildGeometry(ExportPart part, string id, ExportMaterials materials)
        {
            var geometry = new Geometry { Id = id, Name = part.Name, Transform = part.Transform };
            var runs = new Dictionary<string, Run>();
            int vertexBase = 0;

            var faces = part.Mesh.Faces;
            for (int f = 0; f < faces.Count; f++)
            {
                var face = faces[f];
                if (!ExportLayout.CanExport(face, part.Name, f)) continue;
                if (SkipTransparentFaces && face.TextureFace != null && face.TextureFace.RGBA.A < 0.01f) continue;

                bool allNormals = true;
                var unit = new Vector3[face.Vertices.Count];
                for (int v = 0; v < unit.Length; v++)
                {
                    if (!ExportLayout.TryUnit(face.Vertices[v].Normal, out unit[v])) allNormals = false;
                }

                for (int v = 0; v < unit.Length; v++)
                {
                    var vertex = face.Vertices[v];
                    geometry.Positions.Add(vertex.Position.X);
                    geometry.Positions.Add(vertex.Position.Y);
                    geometry.Positions.Add(vertex.Position.Z);

                    // A face with no normals still takes up room in the array, so the indices line up
                    geometry.Normals.Add(unit[v].X);
                    geometry.Normals.Add(unit[v].Y);
                    geometry.Normals.Add(unit[v].Z);

                    var uv = vertex.TexCoord;
                    geometry.TexCoords.Add(Utils.IsFinite(uv.X) ? uv.X : 0f);
                    geometry.TexCoords.Add(Utils.IsFinite(uv.Y) ? uv.Y : 0f);
                }

                // Faces that share a material share a <triangles> element, apart from those with no normals
                var material = materials.For(face);
                string key = material.Name + (allNormals ? "+n" : "");
                if (!runs.TryGetValue(key, out var run))
                {
                    run = new Run { Material = material, Normals = allNormals };
                    runs[key] = run;
                    geometry.Runs.Add(run);
                }
                int triangles = face.Indices.Count / 3;
                for (int i = 0; i < triangles * 3; i++) run.Indices.Add(vertexBase + face.Indices[i]);

                vertexBase += unit.Length;
            }

            if (geometry.Runs.Count == 0)
            {
                Logger.Warn($"Not exporting {part.Name}: it has no triangles");
                return null;
            }
            return geometry;
        }

        #endregion Geometry

        #region Document

        private void WriteDocument(XmlWriter xml, List<Geometry> geometries, ExportMaterials materials)
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            xml.WriteStartDocument();
            xml.WriteStartElement("COLLADA", Namespace);
            xml.WriteAttributeString("version", "1.4.1");

            xml.WriteStartElement("asset");
            xml.WriteStartElement("contributor");
            xml.WriteElementString("authoring_tool", ExportLayout.SingleLine(Generator));
            xml.WriteEndElement();
            xml.WriteElementString("created", now);
            xml.WriteElementString("modified", now);
            xml.WriteStartElement("unit");
            xml.WriteAttributeString("name", "meter");
            xml.WriteAttributeString("meter", "1");
            xml.WriteEndElement();
            xml.WriteElementString("up_axis", "Z_UP");
            xml.WriteEndElement(); // asset

            if (materials.Images.Count > 0)
            {
                xml.WriteStartElement("library_images");
                foreach (var image in materials.Images)
                {
                    xml.WriteStartElement("image");
                    xml.WriteAttributeString("id", ImageId(image.Name));
                    xml.WriteAttributeString("name", image.Name);
                    xml.WriteElementString("init_from", image.Name);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }

            xml.WriteStartElement("library_effects");
            foreach (var material in materials.Materials) WriteEffect(xml, material);
            xml.WriteEndElement();

            xml.WriteStartElement("library_materials");
            foreach (var material in materials.Materials)
            {
                xml.WriteStartElement("material");
                xml.WriteAttributeString("id", material.Name + "-material");
                xml.WriteAttributeString("name", material.Name);
                xml.WriteStartElement("instance_effect");
                xml.WriteAttributeString("url", "#" + material.Name + "-fx");
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("library_geometries");
            foreach (var geometry in geometries) WriteGeometry(xml, geometry);
            xml.WriteEndElement();

            xml.WriteStartElement("library_visual_scenes");
            xml.WriteStartElement("visual_scene");
            xml.WriteAttributeString("id", "Scene");
            xml.WriteAttributeString("name", "Scene");
            foreach (var geometry in geometries) WriteNode(xml, geometry);
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("scene");
            xml.WriteStartElement("instance_visual_scene");
            xml.WriteAttributeString("url", "#Scene");
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteEndElement(); // COLLADA
            xml.WriteEndDocument();
        }

        // The file name is "<asset ID>.png"; an ID has to start with a letter and has no dot
        private static string ImageId(string fileName) => "image-" + fileName.Replace('.', '-');

        private void WriteEffect(XmlWriter xml, ExportMaterial material)
        {
            string? image = material.TextureFile != null ? ImageId(material.TextureFile) : null;

            xml.WriteStartElement("effect");
            xml.WriteAttributeString("id", material.Name + "-fx");
            xml.WriteStartElement("profile_COMMON");

            if (image != null)
            {
                xml.WriteStartElement("newparam");
                xml.WriteAttributeString("sid", image + "-surface");
                xml.WriteStartElement("surface");
                xml.WriteAttributeString("type", "2D");
                xml.WriteElementString("init_from", image);
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteStartElement("newparam");
                xml.WriteAttributeString("sid", image + "-sampler");
                xml.WriteStartElement("sampler2D");
                xml.WriteElementString("source", image + "-surface");
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteStartElement("technique");
            xml.WriteAttributeString("sid", "common");
            xml.WriteStartElement("phong");
            xml.WriteStartElement("diffuse");
            if (image != null)
            {
                // ColladaLoader finds the image through texcoord, which is not what the attribute is for
                xml.WriteStartElement("texture");
                xml.WriteAttributeString("texture", image + "-sampler");
                xml.WriteAttributeString("texcoord", image);
                xml.WriteEndElement();
            }
            else
            {
                xml.WriteStartElement("color");
                xml.WriteAttributeString("sid", "diffuse");
                xml.WriteString(Numbers(material.Color.R, material.Color.G, material.Color.B, material.Color.A));
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); // diffuse
            xml.WriteStartElement("transparency");
            xml.WriteElementString("float", Number(material.Color.A));
            xml.WriteEndElement();
            xml.WriteEndElement(); // phong
            xml.WriteEndElement(); // technique

            xml.WriteEndElement(); // profile_COMMON
            xml.WriteEndElement(); // effect
        }

        private void WriteGeometry(XmlWriter xml, Geometry geometry)
        {
            string id = geometry.Id;
            xml.WriteStartElement("geometry");
            xml.WriteAttributeString("id", id + "-mesh");
            xml.WriteAttributeString("name", geometry.Name);
            xml.WriteStartElement("mesh");

            WriteSource(xml, id + "-positions", geometry.Positions, "X", "Y", "Z");
            WriteSource(xml, id + "-normals", geometry.Normals, "X", "Y", "Z");
            WriteSource(xml, id + "-map0", geometry.TexCoords, "S", "T");

            xml.WriteStartElement("vertices");
            xml.WriteAttributeString("id", id + "-vertices");
            WriteInput(xml, "POSITION", id + "-positions", null);
            xml.WriteEndElement();

            foreach (var run in geometry.Runs)
            {
                xml.WriteStartElement("triangles");
                xml.WriteAttributeString("material", run.Material.Name + "-material");
                xml.WriteAttributeString("count", (run.Indices.Count / 3).ToString(CultureInfo.InvariantCulture));
                WriteInput(xml, "VERTEX", id + "-vertices", 0);
                if (run.Normals) WriteInput(xml, "NORMAL", id + "-normals", 0);
                WriteInput(xml, "TEXCOORD", id + "-map0", 0);

                var p = new StringBuilder();
                for (int i = 0; i < run.Indices.Count; i++)
                {
                    if (i > 0) p.Append(' ');
                    p.Append(run.Indices[i].ToString(CultureInfo.InvariantCulture));
                }
                xml.WriteElementString("p", p.ToString());
                xml.WriteEndElement();
            }

            xml.WriteEndElement(); // mesh
            xml.WriteEndElement(); // geometry
        }

        private static void WriteInput(XmlWriter xml, string semantic, string source, int? offset)
        {
            xml.WriteStartElement("input");
            xml.WriteAttributeString("semantic", semantic);
            if (offset.HasValue) xml.WriteAttributeString("offset", offset.Value.ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("source", "#" + source);
            xml.WriteEndElement();
        }

        private static void WriteSource(XmlWriter xml, string id, List<float> values, params string[] parameters)
        {
            var text = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(Number(values[i]));
            }

            xml.WriteStartElement("source");
            xml.WriteAttributeString("id", id);
            xml.WriteStartElement("float_array");
            xml.WriteAttributeString("id", id + "-array");
            xml.WriteAttributeString("count", values.Count.ToString(CultureInfo.InvariantCulture));
            xml.WriteString(text.ToString());
            xml.WriteEndElement();

            xml.WriteStartElement("technique_common");
            xml.WriteStartElement("accessor");
            xml.WriteAttributeString("source", "#" + id + "-array");
            xml.WriteAttributeString("count", (values.Count / parameters.Length).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("stride", parameters.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var name in parameters)
            {
                xml.WriteStartElement("param");
                xml.WriteAttributeString("name", name);
                xml.WriteAttributeString("type", "float");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement(); // source
        }

        private static void WriteNode(XmlWriter xml, Geometry geometry)
        {
            xml.WriteStartElement("node");
            xml.WriteAttributeString("id", geometry.Id);
            xml.WriteAttributeString("name", geometry.Name);
            xml.WriteAttributeString("type", "NODE");

            // A COLLADA matrix is column vector and written row by row, which is the row vector matrix
            // written column by column
            var m = geometry.Transform;
            xml.WriteStartElement("matrix");
            xml.WriteAttributeString("sid", "transform");
            xml.WriteString(string.Join(" ", new[]
            {
                Number(m.M11), Number(m.M21), Number(m.M31), Number(m.M41),
                Number(m.M12), Number(m.M22), Number(m.M32), Number(m.M42),
                Number(m.M13), Number(m.M23), Number(m.M33), Number(m.M43),
                Number(m.M14), Number(m.M24), Number(m.M34), Number(m.M44)
            }));
            xml.WriteEndElement();

            xml.WriteStartElement("instance_geometry");
            xml.WriteAttributeString("url", "#" + geometry.Id + "-mesh");
            xml.WriteStartElement("bind_material");
            xml.WriteStartElement("technique_common");
            var bound = new HashSet<string>(StringComparer.Ordinal);
            foreach (var run in geometry.Runs)
            {
                // A material can have a run with normals and another without
                if (!bound.Add(run.Material.Name)) continue;
                xml.WriteStartElement("instance_material");
                xml.WriteAttributeString("symbol", run.Material.Name + "-material");
                xml.WriteAttributeString("target", "#" + run.Material.Name + "-material");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement(); // instance_geometry
            xml.WriteEndElement(); // node
        }

        // Adding zero turns -0 into 0, which reads better and means the same
        private static string Number(float value) => (value + 0f).ToString("R", CultureInfo.InvariantCulture);

        private static string Numbers(params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = Number(values[i]);
            return string.Join(" ", parts);
        }

        #endregion Document
    }
}
