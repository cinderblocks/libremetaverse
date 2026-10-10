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
using System.IO;
using System.Linq;
using System.Text;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// What the loaders and exporters say about what they left out. The tests look at the code, severity, subject and
    /// count of each diagnostic, never at its words, and every code has a file that makes it happen. A file that is
    /// fine must say nothing at all.
    /// </summary>
    [TestFixture]
    public class ModelDiagnosticsTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-diag-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private static IEnumerable<ModelDiagnosticCode> Codes(IEnumerable<ModelDiagnostic> diagnostics) => diagnostics.Select(d => d.Code);

        private string Write(string text, string name)
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        #region Files to load

        private const string Tetra = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\nvn 0 0 1\nf 1//1 3//1 2//1\nf 1//1 2//1 4//1\n";

        private const string ColladaTriangle = @"<?xml version=""1.0"" encoding=""utf-8""?>
<COLLADA xmlns=""http://www.collada.org/2005/11/COLLADASchema"" version=""1.4.1"">
  <asset><created>2026-01-01T00:00:00</created><modified>2026-01-01T00:00:00</modified><up_axis>Z_UP</up_axis></asset>
  <library_images><image id=""Image0""><init_from>IMAGE</init_from></image></library_images>
  <library_effects><effect id=""Effect0""><profile_COMMON><technique sid=""common""><lambert><diffuse><texture texture=""Image0"" texcoord=""Image0"" /></diffuse></lambert></technique></profile_COMMON></effect></library_effects>
  <library_materials><material id=""Mat0""><instance_effect url=""#Effect0"" /></material></library_materials>
  <library_geometries>
    <geometry id=""Mesh0""><mesh>
      <source id=""P""><float_array id=""P-a"" count=""9"">0 0 0 1 0 0 0 1 0</float_array></source>
      <source id=""N""><float_array id=""N-a"" count=""3"">0 0 1</float_array></source>
      <source id=""U""><float_array id=""U-a"" count=""6"">0 0 1 0 0 1</float_array></source>
      <vertices id=""V""><input semantic=""POSITION"" source=""#P"" /></vertices>
      <triangles count=""1"" material=""Sym"">
        <input semantic=""VERTEX"" offset=""0"" source=""#V"" /><input semantic=""NORMAL"" offset=""1"" source=""#N"" /><input semantic=""TEXCOORD"" offset=""2"" source=""#U"" />
        <p>0 0 0 1 0 1 2 0 2</p>
      </triangles>
    </mesh></geometry>
  </library_geometries>
  <library_visual_scenes><visual_scene id=""Scene"" name=""Scene"">
    <node id=""Prim0"" name=""Prim0""><matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
      <instance_geometry url=""#Mesh0""><bind_material><technique_common><instance_material symbol=""Sym"" target=""#Mat0"" /></technique_common></bind_material></instance_geometry>
    </node>
  </visual_scene></library_visual_scenes>
</COLLADA>";

        private static byte[] Bytes(IEnumerable<float> values) => values.SelectMany(BitConverter.GetBytes).ToArray();

        private static int Accessor(GltfDocument doc, byte[] data, GltfComponentType component, GltfAccessorType type, int count)
        {
            if (doc.Buffers.Count == 0) doc.Buffers.Add(new GltfBuffer { Data = Array.Empty<byte>() });
            var buffer = doc.Buffers[0];
            var old = buffer.Data ?? Array.Empty<byte>();
            int offset = (old.Length + 3) & ~3;
            var grown = new byte[offset + data.Length];
            Buffer.BlockCopy(old, 0, grown, 0, old.Length);
            Buffer.BlockCopy(data, 0, grown, offset, data.Length);
            buffer.Data = grown;
            buffer.ByteLength = grown.Length;
            doc.BufferViews.Add(new GltfBufferView { Buffer = 0, ByteOffset = offset, ByteLength = data.Length });
            doc.Accessors.Add(new GltfAccessor { BufferView = doc.BufferViews.Count - 1, ComponentType = component, Type = type, Count = count });
            return doc.Accessors.Count - 1;
        }

        /// <summary>A glTF with one mesh of the given primitive modes, and the scene's roots</summary>
        private string Glb(GltfPrimitiveMode[] modes, int[]? roots = null, string? requires = null, string name = "model.glb")
        {
            var doc = new GltfDocument();
            var mesh = new GltfMesh { Name = "Thing" };
            foreach (var mode in modes)
            {
                var primitive = new GltfPrimitive { Mode = mode };
                primitive.Attributes[GltfPrimitive.ATTR_POSITION] = Accessor(doc, Bytes(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }),
                    GltfComponentType.Float, GltfAccessorType.Vec3, 3);
                mesh.Primitives.Add(primitive);
            }
            doc.Meshes.Add(mesh);
            doc.Nodes.Add(new GltfNode { Name = "Node", Mesh = 0 });
            var scene = new GltfScene();
            scene.Nodes.AddRange(roots ?? new[] { 0 });
            doc.Scenes.Add(scene);
            doc.DefaultScene = 0;
            if (requires != null) doc.ExtensionsRequired.Add(requires);

            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, doc.ToGlb());
            return path;
        }

        #endregion Files to load

        #region Files that are fine

        [Test]
        public void ObjLoader_FineFile_SaysNothing()
        {
            var result = new ObjLoader().LoadWithDiagnostics(Write(Tetra, "tetra.obj"), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.HasErrors, Is.False);
        }

        [Test]
        public void GltfLoader_FineFile_SaysNothing()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Triangles }), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void ColladaLoader_FineFile_SaysNothing()
        {
            var result = new ColladaLoader().LoadWithDiagnostics(Write(ColladaTriangle.Replace("IMAGE", "tex.tga"), "fine.dae"), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        #endregion Files that are fine

        #region Files that are refused

        [TestCase("obj")]
        [TestCase("glb")]
        [TestCase("dae")]
        public void Loader_MissingFile_IsUnreadable(string kind)
        {
            var path = Path.Combine(_tempDir, "missing." + kind);

            var result = Load(kind, path);

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.HasErrors, Is.True);
            var error = result.Diagnostics.Single();
            Assert.That(error.Code, Is.EqualTo(ModelDiagnosticCode.FileUnreadable));
            Assert.That(error.Severity, Is.EqualTo(ModelDiagnosticSeverity.Error));
        }

        [TestCase("obj", "f 1 2 3\n")]
        [TestCase("glb", "this is not a glTF file")]
        [TestCase("dae", "this is not an XML document")]
        public void Loader_DamagedFile_IsMalformed(string kind, string text)
        {
            var result = Load(kind, Write(text, "damaged." + kind));

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.FileMalformed));
        }

        [Test]
        public void GltfLoader_RequiredExtensionItCannotRead_IsUnsupported()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Triangles }, requires: "KHR_draco_mesh_compression"), false);

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.FileUnsupported));
        }

        [Test]
        public void Loaders_FileOverTheSizeLimit_IsALimitError()
        {
            var obj = new ObjLoader { MaxFileSize = 10 }.LoadWithDiagnostics(Write(Tetra, "big.obj"), false);
            var glb = new GltfLoader { MaxFileSize = 10 }.LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Triangles }), false);

            Assert.That(obj.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.LimitExceeded));
            Assert.That(glb.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.LimitExceeded));
        }

        private ModelLoadResult Load(string kind, string path)
        {
            switch (kind)
            {
                case "obj": return new ObjLoader().LoadWithDiagnostics(path, false);
                case "glb": return new GltfLoader().LoadWithDiagnostics(path, false);
                default: return new ColladaLoader().LoadWithDiagnostics(path, false);
            }
        }

        #endregion Files that are refused

        #region What was left out

        [Test]
        public void ObjLoader_NoFaces_HasNoGeometry()
        {
            var result = new ObjLoader().LoadWithDiagnostics(Write("v 0 0 0\nv 1 0 0\n", "empty.obj"), false);

            Assert.That(result.Prims, Is.Empty);
            Assert.That(result.HasErrors, Is.False, "an empty model is not a damaged one");
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.NoGeometry));
        }

        [Test]
        public void ObjLoader_FacesWithTooFewCorners_AreCounted()
        {
            var result = new ObjLoader().LoadWithDiagnostics(Write(Tetra + "f 1 2\nf 2 3\n", "thin.obj"), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var skipped = result.Diagnostics.Single();
            Assert.That(skipped.Code, Is.EqualTo(ModelDiagnosticCode.FaceSkipped));
            Assert.That(skipped.Count, Is.EqualTo(2), "one entry with a count, not one for each");
        }

        [Test]
        public void ObjLoader_MoreMaterialsThanFaces_SaysSo()
        {
            var text = new StringBuilder("v 0 0 0\nv 1 0 0\nv 0 1 0\n");
            for (int i = 0; i < 9; i++) text.Append($"usemtl m{i}\nf 1 2 3\n");

            var result = new ObjLoader().LoadWithDiagnostics(Write(text.ToString(), "nine.obj"), false);

            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TooManyFaces));
            Assert.That(warning.Count, Is.EqualTo(9));
        }

        [Test]
        public void ObjLoader_MaterialLibraryThatIsNotThere_IsMissing()
        {
            var result = new ObjLoader().LoadWithDiagnostics(Write("mtllib nope.mtl\n" + Tetra, "lib.obj"), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.MaterialLibraryNotLoaded));
            Assert.That(warning.Subject, Is.EqualTo("nope.mtl"));
        }

        [Test]
        public void ObjLoader_TextureThatIsNotThere_CostsOnlyTheTexture()
        {
            Write("newmtl m\nmap_Kd missing.tga\n", "m.mtl");

            var result = new ObjLoader().LoadWithDiagnostics(Write("mtllib m.mtl\nusemtl m\n" + Tetra, "tex.obj"), loadImages: true);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.HasErrors, Is.False);
            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TextureNotLoaded));
            Assert.That(warning.Subject, Is.EqualTo("missing.tga"));
        }

        [Test]
        public void ObjLoader_TextureThatCannotBeDecoded_CostsOnlyTheTexture()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "bad.tga"), new byte[] { 1, 2, 3 });
            Write("newmtl m\nmap_Kd bad.tga\n", "m.mtl");

            var result = new ObjLoader().LoadWithDiagnostics(Write("mtllib m.mtl\nusemtl m\n" + Tetra, "bad.obj"), loadImages: true);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TextureNotLoaded));
            Assert.That(warning.Subject, Is.EqualTo("bad.tga"));
        }

        [Test]
        public void ObjLoader_TextureOutsideTheModelsDirectory_PointsAtTheSetting()
        {
            Write("newmtl m\nmap_Kd ../elsewhere.tga\n", "m.mtl");

            var result = new ObjLoader().LoadWithDiagnostics(Write("mtllib m.mtl\nusemtl m\n" + Tetra, "out.obj"), loadImages: true);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureOutsideModelDirectory));
            Assert.That(new ObjLoader { RestrictTexturesToModelDirectory = false }.LoadWithDiagnostics(
                Path.Combine(_tempDir, "out.obj"), true).Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureNotLoaded),
                "with the setting off it is only a texture that is not there");
        }

        [Test]
        public void GltfLoader_PrimitiveThatIsNotATriangleList_IsSkipped()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Triangles, GltfPrimitiveMode.Lines }), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var skipped = result.Diagnostics.Single();
            Assert.That(skipped.Code, Is.EqualTo(ModelDiagnosticCode.PrimitiveSkipped));
            Assert.That(skipped.Subject, Is.EqualTo("Thing"));
        }

        [Test]
        public void GltfLoader_MeshWithOnlyLines_HasNoGeometry()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Lines }), false);

            Assert.That(result.Prims, Is.Empty);
            Assert.That(Codes(result.Diagnostics), Is.EquivalentTo(new[] { ModelDiagnosticCode.PrimitiveSkipped, ModelDiagnosticCode.NoGeometry }));
        }

        [Test]
        public void GltfLoader_NodeListedTwice_IsSkippedTheSecondTime()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(new[] { GltfPrimitiveMode.Triangles }, roots: new[] { 0, 0 }), false);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.NodeSkipped));
        }

        [Test]
        public void GltfLoader_MoreThanEightPrimitives_SaysSo()
        {
            var result = new GltfLoader().LoadWithDiagnostics(Glb(Enumerable.Repeat(GltfPrimitiveMode.Triangles, 9).ToArray()), false);

            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TooManyFaces));
            Assert.That(warning.Count, Is.EqualTo(9));
        }

        [Test]
        public void ColladaLoader_TextureOutsideTheModelsDirectory_PointsAtTheSetting()
        {
            var path = Write(ColladaTriangle.Replace("IMAGE", "../elsewhere.tga"), "out.dae");

            var result = new ColladaLoader().LoadWithDiagnostics(path, loadImages: true);

            Assert.That(result.Prims, Has.Count.EqualTo(1));
            var warning = result.Diagnostics.Single();
            Assert.That(warning.Code, Is.EqualTo(ModelDiagnosticCode.TextureOutsideModelDirectory));
            Assert.That(warning.Subject, Is.EqualTo("../elsewhere.tga"));
        }

        #endregion What was left out

        #region Reuse and the older method

        [Test]
        public void Loader_UsedAgain_DoesNotCarryTheLastLoadsDiagnostics()
        {
            var loader = new ObjLoader();
            var bad = loader.LoadWithDiagnostics(Path.Combine(_tempDir, "nope.obj"), false);
            var good = loader.LoadWithDiagnostics(Write(Tetra, "tetra.obj"), false);
            var thin1 = loader.LoadWithDiagnostics(Write(Tetra + "f 1 2\n", "thin.obj"), false);
            var thin2 = loader.LoadWithDiagnostics(Path.Combine(_tempDir, "thin.obj"), false);

            Assert.That(bad.HasErrors, Is.True);
            Assert.That(good.Diagnostics, Is.Empty);
            Assert.That(thin1.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(thin2.Diagnostics, Has.Count.EqualTo(1), "not two");
        }

        [Test]
        public void Load_GivesTheSamePrimsAsLoadWithDiagnostics()
        {
            var path = Write(Tetra + "f 1 2\n", "same.obj");
            var loader = new ObjLoader();

            var legacy = loader.Load(path, false);
            var result = loader.LoadWithDiagnostics(path, false);

            Assert.That(legacy.Select(p => p.ID), Is.EqualTo(result.Prims.Select(p => p.ID)));
            Assert.That(legacy[0].Faces[0].Vertices.Count, Is.EqualTo(result.Prims[0].Faces[0].Vertices.Count));
            Assert.That(new ObjLoader().Load(Path.Combine(_tempDir, "nope.obj"), false), Is.Empty);
            Assert.That(loader, Is.InstanceOf<IDiagnosticModelLoader>());
            Assert.That(new GltfLoader(), Is.InstanceOf<IDiagnosticModelLoader>());
            Assert.That(new ColladaLoader(), Is.InstanceOf<IDiagnosticModelLoader>());
        }

        #endregion Reuse and the older method

        #region Exporters

        private static Face TetraFace()
        {
            var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) };
            var face = new Face
            {
                Vertices = new List<Vertex>(),
                Indices = new List<ushort> { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 },
                TextureFace = new Primitive.TextureEntryFace(null)
            };
            foreach (var p in positions) face.Vertices.Add(new Vertex { Position = p, Normal = Vector3.UnitZ, TexCoord = new Vector2(p.X, p.Y) });
            return face;
        }

        private static FacetedMesh Prim(uint id, uint parent, Vector3 scale, params Face[] faces)
        {
            var mesh = new FacetedMesh
            {
                Prim = new Primitive { LocalID = id, ParentID = parent, Rotation = Quaternion.Identity, Scale = scale }
            };
            mesh.Faces.AddRange(faces.Length > 0 ? faces : new[] { TetraFace() });
            return mesh;
        }

        private static FacetedMesh Rigged(float[]? inverseBind = null)
        {
            var mesh = Prim(7, 0, Vector3.One);
            var face = mesh.Faces[0];
            face.Weights = Enumerable.Range(0, 4).Select(_ => new VertexWeight { Joint0 = 0, Weight0 = 1f }).ToList();
            mesh.Faces[0] = face;
            mesh.SkinData = new MeshSkinData
            {
                JointNames = new[] { "mPelvis" },
                InverseBindMatrices = inverseBind ?? new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }
            };
            return mesh;
        }

        // Each exporter builds the file and reports what it left out
        private static IReadOnlyList<ModelDiagnostic> Export(string kind, Func<UUID, ExportImage?>? provider, params FacetedMesh[] meshes)
        {
            switch (kind)
            {
                case "obj":
                {
                    var e = new ObjExporter { ImageProvider = provider };
                    foreach (var m in meshes) e.Add(m);
                    e.Build("a.obj");
                    return e.Diagnostics;
                }
                case "dae":
                {
                    var e = new ColladaExporter { ImageProvider = provider };
                    foreach (var m in meshes) e.Add(m);
                    e.Build("a.dae");
                    return e.Diagnostics;
                }
                default:
                {
                    var e = new GltfExporter { ImageProvider = provider };
                    foreach (var m in meshes) e.Add(m);
                    e.Build();
                    return e.Diagnostics;
                }
            }
        }

        [TestCase("obj")]
        [TestCase("dae")]
        [TestCase("glb")]
        public void Exporter_FineLinkset_SaysNothing(string kind)
        {
            var diagnostics = Export(kind, null, Prim(1, 0, Vector3.One), Prim(2, 1, new Vector3(2, 2, 2)));

            Assert.That(diagnostics, Is.Empty);
        }

        [TestCase("obj")]
        [TestCase("dae")]
        [TestCase("glb")]
        public void Exporter_ChildWhoseParentWasNotAdded_IsExportedOnItsOwn(string kind)
        {
            var diagnostics = Export(kind, null, Prim(2, 99, Vector3.One));

            Assert.That(diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.LinksetBroken));
        }

        [TestCase("obj")]
        [TestCase("dae")]
        public void Exporter_RiggedMesh_SaysItsSkinIsGone(string kind)
        {
            var diagnostics = Export(kind, null, Rigged());

            Assert.That(diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.RigFlattened));
        }

        [TestCase("obj")]
        [TestCase("dae")]
        [TestCase("glb")]
        public void Exporter_FaceWithATriangleOutsideItsVertices_IsSkipped(string kind)
        {
            var bad = TetraFace();
            bad.Indices[0] = 99;

            var diagnostics = Export(kind, null, Prim(1, 0, Vector3.One, bad, TetraFace()));

            var skipped = diagnostics.Single();
            Assert.That(skipped.Code, Is.EqualTo(ModelDiagnosticCode.FaceSkipped));
            Assert.That(skipped.Severity, Is.EqualTo(ModelDiagnosticSeverity.Warning));
        }

        [TestCase("obj")]
        [TestCase("dae")]
        [TestCase("glb")]
        public void Exporter_TextureTheProviderCannotGive_CostsOnlyTheTexture(string kind)
        {
            var id = UUID.Random();
            var face = TetraFace();
            face.TextureFace = new Primitive.TextureEntryFace(null) { TextureID = id };

            var thrown = Export(kind, _ => throw new InvalidOperationException("no"), Prim(1, 0, Vector3.One, face));
            var wrongKind = Export(kind, _ => new ExportImage(new byte[] { 1 }, "image/gif"), Prim(1, 0, Vector3.One, face));

            // glTF takes any mime type the provider names, and only complains when it throws
            Assert.That(thrown.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureNotExported));
            Assert.That(thrown.Single().Subject, Is.EqualTo(id.ToString()));
            if (kind != "glb") Assert.That(wrongKind.Single().Code, Is.EqualTo(ModelDiagnosticCode.TextureNotExported));
        }

        [Test]
        public void ColladaExporter_MeshWithNoFaces_HasNoGeometry()
        {
            var empty = Prim(1, 0, Vector3.One);
            empty.Faces.Clear();

            Assert.That(Export("dae", null, empty).Single().Code, Is.EqualTo(ModelDiagnosticCode.NoGeometry));
            Assert.That(Export("glb", null, empty).Single().Code, Is.EqualTo(ModelDiagnosticCode.NoGeometry));
        }

        [Test]
        public void ObjExporter_PrimWithNoSize_IsSkipped()
        {
            var diagnostics = Export("obj", null, Prim(1, 0, new Vector3(0, 1, 1)));

            Assert.That(diagnostics.Single().Code, Is.EqualTo(ModelDiagnosticCode.PrimitiveSkipped));
        }

        [Test]
        public void GltfExporter_RiggedMeshWithAMatrixThatCannotBeInverted_SaysSo()
        {
            var diagnostics = Export("glb", null, Rigged(new float[16]));

            Assert.That(Codes(diagnostics), Does.Contain(ModelDiagnosticCode.RigApproximated));
        }

        [Test]
        public void Exporter_BuiltAgain_StartsWithNoDiagnostics()
        {
            var exporter = new ObjExporter();
            exporter.Add(Prim(2, 99, Vector3.One));

            exporter.Build("a.obj");
            Assert.That(exporter.Diagnostics, Has.Count.EqualTo(1));
            exporter.Build("a.obj");
            Assert.That(exporter.Diagnostics, Has.Count.EqualTo(1), "not two");
        }

        #endregion Exporters
    }
}
