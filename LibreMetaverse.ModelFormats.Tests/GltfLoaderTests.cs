/*
 * Copyright (c) 2026, Sjofn LLC
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
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.Imaging;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the .gltf/.glb -&gt; List&lt;ModelPrim&gt; path. Models are built in memory with
    /// <see cref="GltfDocument"/>, written to a temp directory, and loaded through the real file path.
    /// </summary>
    [TestFixture]
    public class GltfLoaderTests
    {
        private const float DegToRad = 0.017453292519943295769236907684886f;

        // A tetrahedron, one distinct normal and UV per corner, wound so the triangles share vertices
        private static readonly Vector3[] TetraPositions =
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)
        };
        private static readonly Vector3[] TetraNormals =
        {
            new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1)
        };
        private static readonly Vector2[] TetraUvs =
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1)
        };
        private static readonly ushort[] TetraIndices = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-gltf-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Builders

        private static byte[] Bytes(IEnumerable<float> values)
        {
            var bytes = new List<byte>();
            foreach (var v in values) bytes.AddRange(BitConverter.GetBytes(v));
            return bytes.ToArray();
        }

        private static byte[] Bytes(IEnumerable<ushort> values)
        {
            var bytes = new List<byte>();
            foreach (var v in values) bytes.AddRange(BitConverter.GetBytes(v));
            return bytes.ToArray();
        }

        private static IEnumerable<float> Floats(IEnumerable<Vector3> vectors)
        {
            foreach (var v in vectors)
            {
                yield return v.X; yield return v.Y; yield return v.Z;
            }
        }

        private static IEnumerable<float> Floats(IEnumerable<Vector2> vectors)
        {
            foreach (var v in vectors)
            {
                yield return v.X; yield return v.Y;
            }
        }

        /// <summary>Appends data to the document's first buffer and returns a buffer view over it</summary>
        private static int AddView(GltfDocument doc, byte[] data)
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
            return doc.BufferViews.Count - 1;
        }

        private static int AddAccessor(GltfDocument doc, byte[] data, GltfComponentType component,
            GltfAccessorType type, int count)
        {
            doc.Accessors.Add(new GltfAccessor
            {
                BufferView = AddView(doc, data),
                ComponentType = component,
                Type = type,
                Count = count
            });
            return doc.Accessors.Count - 1;
        }

        private static GltfPrimitive AddTetra(GltfDocument doc, Vector3 offset = default, bool normals = true, bool uvs = true)
        {
            var positions = new Vector3[TetraPositions.Length];
            for (int i = 0; i < positions.Length; i++) positions[i] = TetraPositions[i] + offset;

            var primitive = new GltfPrimitive();
            primitive.Attributes[GltfPrimitive.ATTR_POSITION] =
                AddAccessor(doc, Bytes(Floats(positions)), GltfComponentType.Float, GltfAccessorType.Vec3, positions.Length);
            if (normals)
            {
                primitive.Attributes[GltfPrimitive.ATTR_NORMAL] =
                    AddAccessor(doc, Bytes(Floats(TetraNormals)), GltfComponentType.Float, GltfAccessorType.Vec3, TetraNormals.Length);
            }
            if (uvs)
            {
                // glTF's UV origin is the top left, so these are the Collada UVs with V flipped
                var flipped = new Vector2[TetraUvs.Length];
                for (int i = 0; i < flipped.Length; i++) flipped[i] = new Vector2(TetraUvs[i].X, 1f - TetraUvs[i].Y);
                primitive.Attributes[GltfPrimitive.ATTR_TEXCOORD_0] =
                    AddAccessor(doc, Bytes(Floats(flipped)), GltfComponentType.Float, GltfAccessorType.Vec2, flipped.Length);
            }
            primitive.Indices = AddAccessor(doc, Bytes(TetraIndices), GltfComponentType.UnsignedShort,
                GltfAccessorType.Scalar, TetraIndices.Length);
            return primitive;
        }

        private static GltfNode AddMeshNode(GltfDocument doc, GltfPrimitive primitive, string? name = "Prim0")
        {
            var mesh = new GltfMesh { Name = "Mesh" };
            mesh.Primitives.Add(primitive);
            doc.Meshes.Add(mesh);
            var node = new GltfNode { Name = name, Mesh = doc.Meshes.Count - 1 };
            doc.Nodes.Add(node);
            return node;
        }

        private static void SetRoots(GltfDocument doc, params int[] nodes)
        {
            var scene = new GltfScene();
            scene.Nodes.AddRange(nodes);
            doc.Scenes.Add(scene);
            doc.DefaultScene = 0;
        }

        private GltfDocument SingleTetra(out GltfNode node, Vector3 offset = default)
        {
            var doc = new GltfDocument();
            node = AddMeshNode(doc, AddTetra(doc, offset));
            SetRoots(doc, 0);
            return doc;
        }

        private string WriteGlb(GltfDocument doc, string name = "model.glb")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllBytes(path, doc.ToGlb());
            return path;
        }

        private string WriteGltf(GltfDocument doc, string name = "model.gltf")
        {
            var path = Path.Combine(_tempDir, name);
            File.WriteAllText(path, doc.ToJson());
            return path;
        }

        /// <summary>
        /// Where the vertices of a prim end up in the world: the unit cube mesh is scaled, rotated and
        /// moved by the prim's own transform.
        /// </summary>
        private static List<Vector3> WorldVertices(ModelPrim prim)
        {
            var result = new List<Vector3>();
            var rot = Matrix4.CreateFromQuaternion(prim.Rotation);
            foreach (var face in prim.Faces)
            {
                foreach (var v in face.Vertices)
                {
                    result.Add(Vector3.Transform(v.Position * prim.Scale, rot) + prim.Position);
                }
            }
            return result;
        }

        private static void AssertVectorsEqual(List<Vector3> actual, List<Vector3> expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].X, Is.EqualTo(expected[i].X).Within(1e-4f), $"vertex {i} X");
                Assert.That(actual[i].Y, Is.EqualTo(expected[i].Y).Within(1e-4f), $"vertex {i} Y");
                Assert.That(actual[i].Z, Is.EqualTo(expected[i].Z).Within(1e-4f), $"vertex {i} Z");
            }
        }

        #endregion Builders

        #region Loading

        [Test]
        public void Load_Glb_ProducesPrimWithOneFace()
        {
            var doc = SingleTetra(out _);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(prims[0].ID, Is.EqualTo("Prim0"));
                Assert.That(prims[0].Faces, Has.Count.EqualTo(1));
                Assert.That(prims[0].Faces[0].Vertices, Has.Count.EqualTo(4));
                Assert.That(prims[0].Faces[0].Indices, Has.Count.EqualTo(12));
                Assert.That(prims[0].Asset, Is.Not.Empty);
            });
        }

        [Test]
        public void Load_GltfWithDataUriBuffer_ProducesPrim()
        {
            var doc = SingleTetra(out _);

            var prims = new GltfLoader().Load(WriteGltf(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Vertices, Has.Count.EqualTo(4));
        }

        [Test]
        public void Load_GltfWithExternalBuffer_ProducesPrim()
        {
            var doc = SingleTetra(out _);
            File.WriteAllBytes(Path.Combine(_tempDir, "geo.bin"), doc.Buffers[0].Data!);
            doc.Buffers[0].Uri = "geo.bin";

            var prims = new GltfLoader().Load(WriteGltf(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_ExternalBufferOutsideModelDirectory_IsRefused_UnlessAllowed()
        {
            var doc = SingleTetra(out _);
            var modelDir = Path.Combine(_tempDir, "model");
            Directory.CreateDirectory(modelDir);
            File.WriteAllBytes(Path.Combine(_tempDir, "geo.bin"), doc.Buffers[0].Data!);
            doc.Buffers[0].Uri = "../geo.bin";
            var path = Path.Combine(modelDir, "model.gltf");
            File.WriteAllText(path, doc.ToJson());

            var restricted = new GltfLoader().Load(path, loadImages: false);
            var allowed = new GltfLoader { RestrictTexturesToModelDirectory = false }.Load(path, loadImages: false);

            Assert.That(restricted, Is.Empty);
            Assert.That(allowed, Has.Count.EqualTo(1));
        }

        [TestCase("http://example.invalid/geo.bin")]
        [TestCase("file:///etc/passwd")]
        public void Load_BufferWithUriScheme_IsRefused(string uri)
        {
            var doc = SingleTetra(out _);
            doc.Buffers[0].Uri = uri;

            var prims = new GltfLoader { RestrictTexturesToModelDirectory = false }
                .Load(WriteGltf(doc), loadImages: false);

            Assert.That(prims, Is.Empty);
        }

        [Test]
        public void Load_PercentEncodedBufferName_IsDecoded()
        {
            var doc = SingleTetra(out _);
            File.WriteAllBytes(Path.Combine(_tempDir, "my geo.bin"), doc.Buffers[0].Data!);
            doc.Buffers[0].Uri = "my%20geo.bin";

            var prims = new GltfLoader().Load(WriteGltf(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_NotAModel_ReturnsEmptyList()
        {
            var path = Path.Combine(_tempDir, "garbage.gltf");
            File.WriteAllText(path, "this is not json");

            Assert.That(new GltfLoader().Load(path, loadImages: false), Is.Empty);
            Assert.That(new GltfLoader().Load(Path.Combine(_tempDir, "missing.glb"), loadImages: false), Is.Empty);
        }

        #endregion Loading

        #region Conventions

        [Test]
        public void Load_YUpModel_StandsUpAlongZ()
        {
            // The tetrahedron spans Y 0..1 and Z 0..1 in glTF. Y-up becomes Z-up with (x, y, z) -> (x, -z, y),
            // so it must end up spanning Z 0..1 (its height) and Y -1..0. Asymmetric, so a flipped axis shows.
            var prim = new GltfLoader().Load(WriteGlb(SingleTetra(out _)), loadImages: false)[0];

            Assert.Multiple(() =>
            {
                Assert.That(prim.BoundMin.Y, Is.EqualTo(-1f).Within(1e-5f));
                Assert.That(prim.BoundMax.Y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(prim.BoundMin.Z, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(prim.BoundMax.Z, Is.EqualTo(1f).Within(1e-5f));
            });
        }

        [Test]
        public void Load_Tetrahedron_MatchesColladaLoader()
        {
            // The same mesh in both formats must give the same prim: same fit into the unit cube, same
            // vertices, normals and (once V is flipped back) UVs. The Collada file is Z-up and holds the
            // tetrahedron already turned from glTF's Y-up with (x, y, z) -> (x, -z, y), so the oracle does
            // not depend on how either loader builds its rotation.
            var dae = @"<?xml version=""1.0"" encoding=""utf-8""?>
<COLLADA xmlns=""http://www.collada.org/2005/11/COLLADASchema"" version=""1.4.1"">
  <asset><created>2026-01-01T00:00:00</created><modified>2026-01-01T00:00:00</modified><up_axis>Z_UP</up_axis></asset>
  <library_geometries>
    <geometry id=""Mesh0"">
      <mesh>
        <source id=""pos""><float_array id=""pos-array"" count=""12"">0 0 0 1 0 0 0 0 1 0 -1 0</float_array></source>
        <source id=""nor""><float_array id=""nor-array"" count=""12"">0 -1 0 1 0 0 0 0 1 0 1 0</float_array></source>
        <source id=""uv""><float_array id=""uv-array"" count=""8"">0 0 1 0 0 1 1 1</float_array></source>
        <vertices id=""verts""><input semantic=""POSITION"" source=""#pos"" /></vertices>
        <triangles count=""4"">
          <input semantic=""VERTEX"" offset=""0"" source=""#verts"" />
          <input semantic=""NORMAL"" offset=""1"" source=""#nor"" />
          <input semantic=""TEXCOORD"" offset=""2"" source=""#uv"" />
          <p>0 0 0 2 2 2 1 1 1  0 0 0 1 1 1 3 3 3  1 1 1 2 2 2 3 3 3  2 2 2 0 0 0 3 3 3</p>
        </triangles>
      </mesh>
    </geometry>
  </library_geometries>
  <library_visual_scenes>
    <visual_scene id=""Scene"" name=""Scene"">
      <node id=""Prim0"" name=""Prim0"">
        <matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
        <instance_geometry url=""#Mesh0"" />
      </node>
    </visual_scene>
  </library_visual_scenes>
</COLLADA>";
            var daePath = Path.Combine(_tempDir, "model.dae");
            File.WriteAllText(daePath, dae);

            var expected = new ColladaLoader().Load(daePath, loadImages: false);
            var actual = new GltfLoader().Load(WriteGlb(SingleTetra(out _)), loadImages: false);

            Assert.That(expected, Has.Count.EqualTo(1), "the Collada reference must load");
            Assert.That(actual, Has.Count.EqualTo(1));

            var e = expected[0];
            var a = actual[0];
            const float tol = 1e-5f;
            Assert.Multiple(() =>
            {
                Assert.That(a.Position.X, Is.EqualTo(e.Position.X).Within(tol));
                Assert.That(a.Position.Y, Is.EqualTo(e.Position.Y).Within(tol));
                Assert.That(a.Position.Z, Is.EqualTo(e.Position.Z).Within(tol));
                Assert.That(a.Scale.X, Is.EqualTo(e.Scale.X).Within(tol));
                Assert.That(a.Scale.Y, Is.EqualTo(e.Scale.Y).Within(tol));
                Assert.That(a.Scale.Z, Is.EqualTo(e.Scale.Z).Within(tol));
                Assert.That(a.BoundMin.X, Is.EqualTo(e.BoundMin.X).Within(tol));
                Assert.That(a.BoundMin.Y, Is.EqualTo(e.BoundMin.Y).Within(tol));
                Assert.That(a.BoundMin.Z, Is.EqualTo(e.BoundMin.Z).Within(tol));
                Assert.That(a.BoundMax.X, Is.EqualTo(e.BoundMax.X).Within(tol));
                Assert.That(a.BoundMax.Y, Is.EqualTo(e.BoundMax.Y).Within(tol));
                Assert.That(a.BoundMax.Z, Is.EqualTo(e.BoundMax.Z).Within(tol));
                Assert.That(Quaternion.Dot(a.Rotation, e.Rotation), Is.EqualTo(1f).Within(1e-4f).Or.EqualTo(-1f).Within(1e-4f));
                Assert.That(a.Faces, Has.Count.EqualTo(e.Faces.Count));
            });

            var av = a.Faces[0].Vertices;
            var ev = e.Faces[0].Vertices;
            Assert.That(av, Has.Count.EqualTo(ev.Count));
            Assert.That(a.Faces[0].Indices, Is.EqualTo(e.Faces[0].Indices));
            for (int i = 0; i < ev.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(av[i].Position.X, Is.EqualTo(ev[i].Position.X).Within(tol), $"vertex {i} position X");
                    Assert.That(av[i].Position.Y, Is.EqualTo(ev[i].Position.Y).Within(tol), $"vertex {i} position Y");
                    Assert.That(av[i].Position.Z, Is.EqualTo(ev[i].Position.Z).Within(tol), $"vertex {i} position Z");
                    Assert.That(av[i].Normal.X, Is.EqualTo(ev[i].Normal.X).Within(tol), $"vertex {i} normal X");
                    Assert.That(av[i].Normal.Y, Is.EqualTo(ev[i].Normal.Y).Within(tol), $"vertex {i} normal Y");
                    Assert.That(av[i].Normal.Z, Is.EqualTo(ev[i].Normal.Z).Within(tol), $"vertex {i} normal Z");
                    Assert.That(av[i].TexCoord.X, Is.EqualTo(ev[i].TexCoord.X).Within(tol), $"vertex {i} U");
                    Assert.That(av[i].TexCoord.Y, Is.EqualTo(ev[i].TexCoord.Y).Within(tol), $"vertex {i} V");
                });
            }
        }

        [Test]
        public void Load_NodeTranslation_PlacesMeshLikeMovedVertices()
        {
            // Moving a node must land the mesh in the same place as moving its vertices, whichever
            // axis conversion is used
            var offset = new Vector3(1.5f, -2f, 4f);

            var moved = SingleTetra(out var node);
            node.Translation = offset;
            var viaNode = new GltfLoader().Load(WriteGlb(moved, "a.glb"), loadImages: false);
            var viaVertices = new GltfLoader().Load(WriteGlb(SingleTetra(out _, offset), "b.glb"), loadImages: false);

            Assert.That(viaNode, Has.Count.EqualTo(1));
            Assert.That(viaVertices, Has.Count.EqualTo(1));
            AssertVectorsEqual(WorldVertices(viaNode[0]), WorldVertices(viaVertices[0]));
        }

        [Test]
        public void Load_NodeMatrix_MatchesEquivalentTrs()
        {
            var rotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 40f * DegToRad);
            var translation = new Vector3(3, 1, -2);

            var trs = SingleTetra(out var trsNode);
            trsNode.Rotation = rotation;
            trsNode.Translation = translation;
            trsNode.Scale = new Vector3(2, 2, 2);

            // The same transform as the column-major matrix a file would hold (translation in M14..M34)
            var rowVector = Matrix4.CreateScale(new Vector3(2, 2, 2)) * Matrix4.CreateFromQuaternion(rotation)
                * Matrix4.CreateTranslation(translation);
            var matrix = SingleTetra(out var matrixNode);
            matrixNode.Matrix = Matrix4.Transpose(rowVector);

            var a = new GltfLoader().Load(WriteGlb(trs, "trs.glb"), loadImages: false);
            var b = new GltfLoader().Load(WriteGlb(matrix, "matrix.glb"), loadImages: false);

            Assert.That(a, Has.Count.EqualTo(1));
            Assert.That(b, Has.Count.EqualTo(1));
            AssertVectorsEqual(WorldVertices(b[0]), WorldVertices(a[0]));
        }

        [Test]
        public void Load_TranslatedChildOfRotatedParent_AppliesParentTransform()
        {
            var doc = new GltfDocument();
            var parent = new GltfNode
            {
                Name = "Parent",
                Translation = new Vector3(1, 2, 3),
                Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), 90f * DegToRad),
                Scale = new Vector3(2, 2, 2)
            };
            doc.Nodes.Add(parent);
            var child = AddMeshNode(doc, AddTetra(doc), "Child");
            child.Translation = new Vector3(1, 0, 0);
            child.Rotation = Quaternion.CreateFromAxisAngle(new Vector3(1, 0, 0), 45f * DegToRad);
            parent.Children.Add(1);
            SetRoots(doc, 0);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            // Work out where the tetrahedron should end up, in glTF's space and then Second Life's
            var childMatrix = Matrix4.CreateScale(child.Scale) * Matrix4.CreateFromQuaternion(child.Rotation)
                * Matrix4.CreateTranslation(child.Translation);
            var parentMatrix = Matrix4.CreateScale(parent.Scale) * Matrix4.CreateFromQuaternion(parent.Rotation)
                * Matrix4.CreateTranslation(parent.Translation);

            var expected = new List<Vector3>();
            foreach (var index in TetraIndices)
            {
                var world = Vector3.Transform(Vector3.Transform(TetraPositions[index], childMatrix), parentMatrix);
                expected.Add(new Vector3(world.X, -world.Z, world.Y)); // Y-up to Z-up
            }

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].ID, Is.EqualTo("Child"));

            // WorldVertices lists each distinct vertex once; expand it through the face indices
            var distinct = WorldVertices(prims[0]);
            var actual = new List<Vector3>();
            foreach (var index in prims[0].Faces[0].Indices) actual.Add(distinct[(int)index]);
            AssertVectorsEqual(actual, expected);
        }

        [Test]
        public void Load_SceneWithoutDefault_UsesFirstScene_AndNoScenesUsesParentlessNodes()
        {
            var doc = SingleTetra(out _);
            doc.DefaultScene = -1;
            Assert.That(new GltfLoader().Load(WriteGlb(doc, "first.glb"), loadImages: false), Has.Count.EqualTo(1));

            doc.Scenes.Clear();
            Assert.That(new GltfLoader().Load(WriteGlb(doc, "none.glb"), loadImages: false), Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_MeshInstancedByTwoNodes_SharesTheMesh()
        {
            var doc = SingleTetra(out _);
            doc.Nodes.Add(new GltfNode { Name = "Copy", Mesh = 0, Translation = new Vector3(5, 0, 0) });
            doc.Scenes[0].Nodes.Add(1);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.That(prims[1].Asset, Is.SameAs(prims[0].Asset));
            Assert.That(prims[1].Position.X, Is.Not.EqualTo(prims[0].Position.X));
        }

        [Test]
        public void Load_NodeCycle_DoesNotLoopForever()
        {
            var doc = SingleTetra(out var node);
            node.Children.Add(0);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_VeryDeepNodeChain_DoesNotOverflowTheStack()
        {
            var doc = new GltfDocument();
            for (int i = 0; i < 50000; i++)
            {
                var n = new GltfNode { Name = "n" + i };
                if (i > 0) doc.Nodes[i - 1].Children.Add(i);
                doc.Nodes.Add(n);
            }
            var leaf = doc.Nodes[doc.Nodes.Count - 1];
            var mesh = new GltfMesh();
            mesh.Primitives.Add(AddTetra(doc));
            doc.Meshes.Add(mesh);
            leaf.Mesh = 0;
            SetRoots(doc, 0);

            Assert.DoesNotThrow(() => new GltfLoader().Load(WriteGlb(doc), loadImages: false));
        }

        [Test]
        public void Load_MeshWithoutNormals_GetsFlatNormals()
        {
            var doc = new GltfDocument();
            AddMeshNode(doc, AddTetra(doc, normals: false));
            SetRoots(doc, 0);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
            foreach (var v in prims[0].Faces[0].Vertices)
            {
                Assert.That(v.Normal.Length(), Is.EqualTo(1f).Within(1e-4f));
            }
            // flat shading cannot share a vertex between triangles that face different ways
            Assert.That(prims[0].Faces[0].Vertices.Count, Is.EqualTo(12));
        }

        [Test]
        public void Load_UnevenSize_StoresNormalsScaledByTheSizeSoTheyDrawTrue()
        {
            // Y-up positions 10 x 2 x 1 become a Z-up mesh of 10 x 1 x 2. The prim is drawn at that size
            // and normals follow the inverse transpose of it, so the stored ones are multiplied by the size.
            var positions = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(0, 2, 0), new Vector3(0, 0, 1) };
            var normals = new Vector3[4];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.Normalize(new Vector3(1f, 1f, 1f));

            var doc = new GltfDocument();
            var primitive = new GltfPrimitive();
            primitive.Attributes[GltfPrimitive.ATTR_POSITION] =
                AddAccessor(doc, Bytes(Floats(positions)), GltfComponentType.Float, GltfAccessorType.Vec3, 4);
            primitive.Attributes[GltfPrimitive.ATTR_NORMAL] =
                AddAccessor(doc, Bytes(Floats(normals)), GltfComponentType.Float, GltfAccessorType.Vec3, 4);
            primitive.Indices = AddAccessor(doc, Bytes(new ushort[] { 0, 1, 2, 0, 2, 3 }), GltfComponentType.UnsignedShort,
                GltfAccessorType.Scalar, 6);
            AddMeshNode(doc, primitive);
            SetRoots(doc, 0);

            var prim = new GltfLoader().Load(WriteGlb(doc), loadImages: false)[0];

            // glTF (1, 1, 1) is (1, -1, 1) in Z-up
            var expected = Vector3.Normalize(new Vector3(10f, -1f, 2f));
            var stored = prim.Faces[0].Vertices[0].Normal;
            var drawn = Vector3.Normalize(new Vector3(stored.X / prim.Scale.X, stored.Y / prim.Scale.Y, stored.Z / prim.Scale.Z));
            var original = Vector3.Normalize(new Vector3(1f, -1f, 1f));
            Assert.Multiple(() =>
            {
                Assert.That(prim.Scale.X, Is.EqualTo(10f).Within(1e-4f));
                Assert.That(prim.Scale.Y, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(prim.Scale.Z, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(stored.X, Is.EqualTo(expected.X).Within(1e-4f));
                Assert.That(stored.Y, Is.EqualTo(expected.Y).Within(1e-4f));
                Assert.That(stored.Z, Is.EqualTo(expected.Z).Within(1e-4f));
                Assert.That(drawn.X, Is.EqualTo(original.X).Within(1e-4f));
                Assert.That(drawn.Y, Is.EqualTo(original.Y).Within(1e-4f));
                Assert.That(drawn.Z, Is.EqualTo(original.Z).Within(1e-4f));
            });
        }

        [Test]
        public void Load_NonTrianglePrimitive_IsSkipped()
        {
            var doc = new GltfDocument();
            var primitive = AddTetra(doc);
            primitive.Mode = GltfPrimitiveMode.Lines;
            AddMeshNode(doc, primitive);
            SetRoots(doc, 0);

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_TwoPrimitivesWithTwoMaterials_MakeTwoFacesInOneUnitCube()
        {
            var doc = new GltfDocument();
            doc.Materials.Add(new GltfDocumentMaterial { Name = "Red", BaseColorFactor = new Color4(1f, 0f, 0f, 1f) });
            doc.Materials.Add(new GltfDocumentMaterial { Name = "Blue", BaseColorFactor = new Color4(0f, 0f, 1f, 0.5f) });

            var first = AddTetra(doc);
            first.Material = 0;
            var second = AddTetra(doc, new Vector3(4, 0, 0));
            second.Material = 1;
            var mesh = new GltfMesh();
            mesh.Primitives.Add(first);
            mesh.Primitives.Add(second);
            doc.Meshes.Add(mesh);
            doc.Nodes.Add(new GltfNode { Name = "Two", Mesh = 0 });
            SetRoots(doc, 0);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            Assert.That(prims, Has.Count.EqualTo(1));
            var faces = prims[0].Faces;
            Assert.That(faces, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(faces[0].Material.ID, Is.EqualTo("Red"));
                Assert.That(faces[0].Material.DiffuseColor.R, Is.EqualTo(1f));
                Assert.That(faces[1].Material.ID, Is.EqualTo("Blue"));
                Assert.That(faces[1].Material.DiffuseColor.B, Is.EqualTo(1f));
                Assert.That(faces[1].Material.DiffuseColor.A, Is.EqualTo(0.5f).Within(1e-5f));
            });

            // Both primitives are fitted into one cube, so the pair spans all of it
            foreach (var face in faces)
            {
                foreach (var v in face.Vertices)
                {
                    Assert.That(v.Position.X, Is.InRange(-0.5001f, 0.5001f));
                    Assert.That(v.Position.Y, Is.InRange(-0.5001f, 0.5001f));
                    Assert.That(v.Position.Z, Is.InRange(-0.5001f, 0.5001f));
                }
            }
            Assert.That(prims[0].Scale.X, Is.EqualTo(5f).Within(1e-4f), "tetrahedra at 0 and 4, each 1 wide");
        }

        [Test]
        public void Load_PrimitiveWithMoreVerticesThanAFaceCanIndex_IsSkipped()
        {
            // 65538 vertices, none shared: ModelPrim.CreateAsset writes face indices as 16 bit
            const int triangles = 21846;
            var positions = new List<float>();
            for (int t = 0; t < triangles; t++)
            {
                float x = t * 2f; // keep neighbours apart so no corner is shared
                positions.AddRange(new[] { x, 0f, 0f, x + 0.5f, 1f, 0f, x + 1f, 0f, 0f });
            }

            var doc = new GltfDocument();
            var primitive = new GltfPrimitive();
            primitive.Attributes[GltfPrimitive.ATTR_POSITION] =
                AddAccessor(doc, Bytes(positions), GltfComponentType.Float, GltfAccessorType.Vec3, triangles * 3);
            AddMeshNode(doc, primitive);
            SetRoots(doc, 0);

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        #endregion Conventions

        #region Hostile input

        [Test]
        public void Load_AccessorCountPastTheBuffer_ReturnsEmptyList()
        {
            var doc = SingleTetra(out _);
            doc.Accessors[0].Count = 1000;

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_HugeAccessorCount_IsRefusedWithoutAllocating()
        {
            var doc = SingleTetra(out _);
            doc.Accessors[0].Count = int.MaxValue;

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_NegativeOffsetsAndCounts_AreRefused()
        {
            var doc = SingleTetra(out _);
            doc.Accessors[0].ByteOffset = -12;
            Assert.That(new GltfLoader().Load(WriteGlb(doc, "offset.glb"), loadImages: false), Is.Empty);

            doc = SingleTetra(out _);
            doc.Accessors[0].Count = -3;
            Assert.That(new GltfLoader().Load(WriteGlb(doc, "count.glb"), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_IndexPastTheVertices_ReturnsEmptyList()
        {
            var doc = SingleTetra(out _);
            var indices = doc.Buffers[0].Data!;
            // the index accessor is the last one, so its data sits at the end of the buffer
            var view = doc.BufferViews[doc.Accessors[doc.Accessors.Count - 1].BufferView];
            indices[view.ByteOffset] = 200;

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_PositionThatIsNotAFloat_IsRefused()
        {
            var doc = SingleTetra(out _);
            doc.Accessors[0].ComponentType = GltfComponentType.Short;

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_NaNPosition_IsRefused()
        {
            var doc = SingleTetra(out _);
            var view = doc.BufferViews[doc.Accessors[0].BufferView];
            Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, doc.Buffers[0].Data!, view.ByteOffset, 4);

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_RequiredCompressionExtension_IsRefused()
        {
            var doc = SingleTetra(out _);
            doc.ExtensionsRequired.Add("KHR_draco_mesh_compression");

            Assert.That(new GltfLoader().Load(WriteGlb(doc), loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_GlbChunkLongerThanTheFile_IsRefusedWithoutAllocating()
        {
            var glb = SingleTetra(out _).ToGlb();
            // First chunk header sits at byte 12; claim the chunk is about 2 GB long
            Buffer.BlockCopy(BitConverter.GetBytes(0x7FFFFFF0u), 0, glb, 12, 4);
            var path = Path.Combine(_tempDir, "evil.glb");
            File.WriteAllBytes(path, glb);

            Assert.That(new GltfLoader().Load(path, loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_FileOverTheSizeLimit_IsRefused()
        {
            var path = WriteGlb(SingleTetra(out _));

            Assert.That(new GltfLoader { MaxFileSize = 64 }.Load(path, loadImages: false), Is.Empty);
        }

        [Test]
        public void Load_MoreVerticesThanTheLimit_IsRefused()
        {
            var path = WriteGlb(SingleTetra(out _));

            Assert.That(new GltfLoader { MaxVertices = 8 }.Load(path, loadImages: false), Is.Empty);
        }

        #endregion Hostile input

        #region Textures

        private static byte[] TgaBytes()
        {
            var image = new ManagedImage(64, 32, ManagedImage.ImageChannels.Color);
            for (var i = 0; i < image.Red.Length; i++)
            {
                image.Red[i] = (byte)(i * 7);
                image.Green[i] = (byte)(i * 11);
                image.Blue[i] = (byte)(i * 13);
            }
            return Targa.Encode(image);
        }

        private GltfDocument TexturedTetra(Action<GltfDocument, GltfImage> describeImage)
        {
            var doc = new GltfDocument();
            var primitive = AddTetra(doc);
            primitive.Material = 0;
            var image = new GltfImage();
            doc.Images.Add(image);
            doc.Textures.Add(new GltfTexture { Source = 0 });
            doc.Materials.Add(new GltfDocumentMaterial
            {
                Name = "Textured",
                BaseColorTexture = new GltfTextureRef { Index = 0 }
            });
            describeImage(doc, image);
            AddMeshNode(doc, primitive);
            SetRoots(doc, 0);
            return doc;
        }

        [Test]
        public void Load_EmbeddedTgaTexture_IsDecodedAndEncoded()
        {
            var doc = TexturedTetra((d, image) =>
            {
                image.BufferView = AddView(d, TgaBytes());
                image.MimeType = "image/x-tga";
            });

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: true);

            Assert.That(prims, Has.Count.EqualTo(1));
            var material = prims[0].Faces[0].Material;
            Assert.Multiple(() =>
            {
                Assert.That(material.Texture, Is.EqualTo("gltf-image-0"));
                Assert.That(material.TextureData, Is.Not.Empty);
                Assert.That(material.Width, Is.EqualTo(64));
                Assert.That(material.Height, Is.EqualTo(32));
            });
        }

        [Test]
        public void Load_ExternalTexture_IsLoadedFromTheModelDirectory()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "tex0.tga"), TgaBytes());
            var doc = TexturedTetra((d, image) => image.Uri = "tex0.tga");

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: true);

            var material = prims[0].Faces[0].Material;
            Assert.That(material.Texture, Is.EqualTo("tex0.tga"));
            Assert.That(material.TextureData, Is.Not.Empty);
        }

        [Test]
        public void Load_ExternalTextureOutsideModelDirectory_IsNotLoaded_UnlessAllowed()
        {
            var modelDir = Path.Combine(_tempDir, "model");
            Directory.CreateDirectory(modelDir);
            File.WriteAllBytes(Path.Combine(_tempDir, "tex0.tga"), TgaBytes());
            var doc = TexturedTetra((d, image) => image.Uri = "../tex0.tga");
            var path = Path.Combine(modelDir, "model.glb");
            File.WriteAllBytes(path, doc.ToGlb());

            var restricted = new GltfLoader().Load(path, loadImages: true);
            var allowed = new GltfLoader { RestrictTexturesToModelDirectory = false }.Load(path, loadImages: true);

            Assert.That(restricted[0].Faces[0].Material.TextureData, Is.Null.Or.Empty);
            Assert.That(allowed[0].Faces[0].Material.TextureData, Is.Not.Empty);
        }

        [Test]
        public void Load_TextureWithoutLoadImages_LeavesTheDataEmpty()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "tex0.tga"), TgaBytes());
            var doc = TexturedTetra((d, image) => image.Uri = "tex0.tga");

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: false);

            var material = prims[0].Faces[0].Material;
            Assert.That(material.Texture, Is.EqualTo("tex0.tga"));
            Assert.That(material.TextureData, Is.Empty);
        }

        [TestCase("http://example.invalid/tex.png", true)]
        [TestCase("http://example.invalid/tex.png", false)]
        [TestCase("file:///etc/passwd", true)]
        [TestCase("file:///etc/passwd", false)]
        [TestCase("tex%00.tga", true)]
        [TestCase("tex%00.tga", false)]
        public void Load_TextureUriThatIsNotALocalFile_CostsTheTextureNotTheMesh(string uri, bool loadImages)
        {
            var doc = TexturedTetra((d, image) => image.Uri = uri);

            var prims = new GltfLoader { RestrictTexturesToModelDirectory = false }
                .Load(WriteGlb(doc), loadImages);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Material.Texture, Is.Empty);
            Assert.That(prims[0].Faces[0].Material.TextureData, Is.Empty);
        }

        [Test]
        public void Load_PngWithoutACodec_LeavesTheTextureEmptyButKeepsTheMesh()
        {
            var doc = TexturedTetra((d, image) =>
            {
                image.BufferView = AddView(d, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
                image.MimeType = "image/png";
            });

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: true);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Material.TextureData, Is.Empty);
        }

        [Test]
        public void Load_TwoMaterialsSharingAnImage_ShareTheTextureName()
        {
            var doc = TexturedTetra((d, image) => image.BufferView = AddView(d, TgaBytes()));
            doc.Images[0].MimeType = "image/x-tga";
            doc.Materials.Add(new GltfDocumentMaterial { Name = "Second", BaseColorTexture = new GltfTextureRef { Index = 0 } });
            var second = AddTetra(doc, new Vector3(3, 0, 0));
            second.Material = 1;
            doc.Meshes[0].Primitives.Add(second);

            var prims = new GltfLoader().Load(WriteGlb(doc), loadImages: true);

            var faces = prims[0].Faces;
            Assert.That(faces, Has.Count.EqualTo(2));
            Assert.That(faces[1].Material.Texture, Is.EqualTo(faces[0].Material.Texture));
            Assert.That(faces[1].Material.TextureData, Is.Not.Empty);
        }

        #endregion Textures
    }
}
