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
using LibreMetaverse.Assets;
using LibreMetaverse.Assets.Gltf;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;
using NUnit.Framework;
using Path = System.IO.Path;
using NumMatrix = System.Numerics.Matrix4x4;
using NumQuaternion = System.Numerics.Quaternion;
using NumVector3 = System.Numerics.Vector3;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for FacetedMesh -&gt; glTF. Placement is checked by walking the exported node tree by hand
    /// from the JSON, with plain System.Numerics maths, so it does not depend on the matrix layout or axis
    /// conventions that the exporter and <see cref="GltfLoader"/> share.
    /// </summary>
    internal static class ExporterExtensions
    {
        public static GltfExporter Also(this GltfExporter exporter, Action<GltfExporter> action)
        {
            action(exporter);
            return exporter;
        }
    }

    [TestFixture]
    public class GltfExporterTests
    {
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
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0.25f, 0.75f)
        };
        private static readonly ushort[] TetraIndices = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-export-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Builders

        private static Face TetraFace(Primitive.TextureEntryFace? texture = null, bool normals = true)
        {
            var face = new Face
            {
                Vertices = new List<Vertex>(),
                Indices = new List<ushort>(TetraIndices),
                TextureFace = texture ?? new Primitive.TextureEntryFace(null)
            };
            for (int i = 0; i < TetraPositions.Length; i++)
            {
                face.Vertices.Add(new Vertex
                {
                    Position = TetraPositions[i],
                    Normal = normals ? TetraNormals[i] : Vector3.Zero,
                    TexCoord = TetraUvs[i]
                });
            }
            return face;
        }

        private static FacetedMesh Prim(uint localId, uint parentId, Vector3 position, Quaternion rotation, Vector3 scale,
            params Face[] faces)
        {
            var mesh = new FacetedMesh
            {
                Prim = new Primitive
                {
                    LocalID = localId,
                    ParentID = parentId,
                    Position = position,
                    Rotation = rotation,
                    Scale = scale
                }
            };
            mesh.Faces.AddRange(faces.Length > 0 ? faces : new[] { TetraFace() });
            return mesh;
        }

        private static FacetedMesh SimplePrim(params Face[] faces) =>
            Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, faces);

        private static GltfDocument Export(params FacetedMesh[] meshes)
        {
            var exporter = new GltfExporter();
            foreach (var mesh in meshes) exporter.Add(mesh);
            return exporter.Build();
        }

        #endregion Builders

        #region Hand-walked scene

        // 4x4 column-major matrices, as plain doubles
        private static double[] Identity() => new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        private static double[] Multiply(double[] a, double[] b)
        {
            var r = new double[16];
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 4; row++)
                    for (int k = 0; k < 4; k++)
                        r[col * 4 + row] += a[k * 4 + row] * b[col * 4 + k];
            return r;
        }

        private static double[] LocalMatrix(OSDMap node)
        {
            if (node.TryGetValue("matrix", out var m))
            {
                var array = (OSDArray)m;
                return array.Select(v => v.AsReal()).ToArray();
            }

            double[] t = Identity(), r = Identity(), s = Identity();
            if (node.TryGetValue("translation", out var tr))
            {
                var a = (OSDArray)tr;
                t[12] = a[0].AsReal(); t[13] = a[1].AsReal(); t[14] = a[2].AsReal();
            }
            if (node.TryGetValue("scale", out var sc))
            {
                var a = (OSDArray)sc;
                s[0] = a[0].AsReal(); s[5] = a[1].AsReal(); s[10] = a[2].AsReal();
            }
            if (node.TryGetValue("rotation", out var ro))
            {
                var a = (OSDArray)ro;
                double x = a[0].AsReal(), y = a[1].AsReal(), z = a[2].AsReal(), w = a[3].AsReal();
                r = new double[]
                {
                    1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0,
                    2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0,
                    2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0,
                    0, 0, 0, 1
                };
            }
            return Multiply(t, Multiply(r, s));
        }

        /// <summary>The world matrix of a node, read from the serialized JSON and multiplied up its parents</summary>
        private static double[] WorldMatrix(OSDMap root, int node)
        {
            var nodes = (OSDArray)root["nodes"];
            var parent = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (((OSDMap)nodes[i]).TryGetValue("children", out var children))
                {
                    foreach (var c in (OSDArray)children) parent[c.AsInteger()] = i;
                }
            }

            var world = LocalMatrix((OSDMap)nodes[node]);
            while (parent.TryGetValue(node, out var p))
            {
                node = p;
                world = Multiply(LocalMatrix((OSDMap)nodes[node]), world);
            }
            return world;
        }

        private static NumVector3 Apply(double[] m, Vector3 v)
        {
            return new NumVector3(
                (float)(m[0] * v.X + m[4] * v.Y + m[8] * v.Z + m[12]),
                (float)(m[1] * v.X + m[5] * v.Y + m[9] * v.Z + m[13]),
                (float)(m[2] * v.X + m[6] * v.Y + m[10] * v.Z + m[14]));
        }

        private static OSDMap Json(GltfDocument doc) => (OSDMap)OSDParser.DeserializeJson(doc.ToJson());

        /// <summary>glTF-space world positions of every vertex of every mesh node, found by node name</summary>
        private static List<NumVector3> WorldPositions(GltfDocument doc, string nodeName)
        {
            var root = Json(doc);
            var nodes = (OSDArray)root["nodes"];
            int index = Enumerable.Range(0, nodes.Count).First(i => ((OSDMap)nodes[i])["name"].AsString() == nodeName);
            var world = WorldMatrix(root, index);

            var result = new List<NumVector3>();
            foreach (var primitive in doc.Meshes[doc.Nodes[index].Mesh].Primitives)
            {
                foreach (var p in doc.GetPositions(primitive)) result.Add(Apply(world, p));
            }
            return result;
        }

        private static NumVector3 Sl(Vector3 v) => new NumVector3(v.X, v.Y, v.Z);

        private static NumQuaternion Sl(Quaternion q) => new NumQuaternion(q.X, q.Y, q.Z, q.W);

        /// <summary>Second Life Z-up to glTF Y-up, written out: (x, y, z) -> (x, z, -y)</summary>
        private static NumVector3 ToYUp(NumVector3 v) => new NumVector3(v.X, v.Z, -v.Y);

        private static void AssertClose(NumVector3 actual, NumVector3 expected, string what)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-4f), what + " X");
                Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-4f), what + " Y");
                Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-4f), what + " Z");
            });
        }

        #endregion Hand-walked scene

        #region Scene

        [Test]
        public void Build_TopNode_IsTheLiteralZUpToYUpMatrix()
        {
            var root = Json(Export(SimplePrim()));

            // Column-major, one column per Second Life axis: X stays X, Y becomes -Z, Z becomes +Y
            var matrix = ((OSDArray)((OSDMap)((OSDArray)root["nodes"])[0])["matrix"]).Select(v => v.AsReal()).ToArray();

            Assert.That(matrix, Is.EqualTo(new double[] { 1, 0, 0, 0, 0, 0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1 }).Within(1e-9));
        }

        [Test]
        public void Build_SceneHasOneRoot_TheTopNode()
        {
            var doc = Export(SimplePrim());

            Assert.Multiple(() =>
            {
                Assert.That(doc.Scenes, Has.Count.EqualTo(1));
                Assert.That(doc.Scenes[0].Nodes, Is.EqualTo(new[] { 0 }));
                Assert.That(doc.DefaultScene, Is.EqualTo(0));
                Assert.That(doc.Version, Is.EqualTo("2.0"));
            });
        }

        [Test]
        public void Build_RootPrim_IsScaledThenStoodUp()
        {
            var doc = Export(Prim(1, 0, new Vector3(100, 200, 300), Quaternion.Identity, new Vector3(2, 3, 4)));

            var actual = WorldPositions(doc, "prim1");

            Assert.That(actual, Has.Count.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                var sl = new NumVector3(TetraPositions[i].X * 2, TetraPositions[i].Y * 3, TetraPositions[i].Z * 4);
                AssertClose(actual[i], ToYUp(sl), "vertex " + i);
            }
        }

        [Test]
        public void Build_Linkset_PlacesChildrenTheWayTheGridDoes()
        {
            var rootRotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), 90f * (float)Math.PI / 180f);
            var childRotation = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 35f * (float)Math.PI / 180f);
            var rootScale = new Vector3(2, 3, 4);
            var childScale = new Vector3(1, 2, 1);
            var childPosition = new Vector3(1, 0.5f, -2);

            var root = Prim(10, 0, new Vector3(100, 200, 300), rootRotation, rootScale);
            root.Prim.Properties = new Primitive.ObjectProperties { Name = "Root" };
            var child = Prim(11, 10, childPosition, childRotation, childScale);
            child.Prim.Properties = new Primitive.ObjectProperties { Name = "Child" };
            var doc = Export(root, child);

            var rootActual = WorldPositions(doc, "Root");
            var childActual = WorldPositions(doc, "Child");

            // The root's world position is dropped. The root's rotation applies to everything, its scale only
            // to its own mesh: a child is placed in the root's frame at its own size.
            var rr = Sl(rootRotation);
            var cr = Sl(childRotation);
            for (int i = 0; i < 4; i++)
            {
                var p = Sl(TetraPositions[i]);
                var rootExpected = NumVector3.Transform(p * Sl(rootScale), rr);
                var childExpected = NumVector3.Transform(Sl(childPosition) + NumVector3.Transform(p * Sl(childScale), cr), rr);
                AssertClose(rootActual[i], ToYUp(rootExpected), "root vertex " + i);
                AssertClose(childActual[i], ToYUp(childExpected), "child vertex " + i);
            }
        }

        [Test]
        public void Build_ChildWhoseParentWasNotAdded_IsExportedOnItsOwn()
        {
            var orphan = Prim(11, 99, new Vector3(5, 5, 5), Quaternion.Identity, Vector3.One);

            var doc = Export(orphan);

            Assert.That(WorldPositions(doc, "prim11"), Has.Count.EqualTo(4));
        }

        [Test]
        public void Build_ChildAddedBeforeItsRoot_StillGoesInTheLinkset()
        {
            var root = Prim(10, 0, Vector3.Zero, Quaternion.Identity, Vector3.One);
            var child = Prim(11, 10, new Vector3(3, 0, 0), Quaternion.Identity, Vector3.One);

            var doc = Export(child, root);

            var childActual = WorldPositions(doc, "prim11");
            AssertClose(childActual[0], ToYUp(new NumVector3(3, 0, 0)), "child origin");
        }

        [Test]
        public void Build_NothingAdded_GivesAValidEmptyScene()
        {
            var doc = new GltfExporter().Build();

            Assert.That(doc.Meshes, Is.Empty);
            Assert.That(GltfDocument.Load(doc.ToGlb()).Nodes, Has.Count.EqualTo(1));
        }

        #endregion Scene

        #region Geometry

        [Test]
        public void Build_Face_KeepsItsVerticesNormalsIndicesAndFlipsV()
        {
            var doc = Export(SimplePrim());
            var primitive = doc.Meshes[0].Primitives[0];

            var positions = doc.GetPositions(primitive);
            var normals = doc.GetNormals(primitive);
            var uvs = doc.GetTexCoords(primitive);
            var indices = doc.GetIndices(primitive);

            Assert.Multiple(() =>
            {
                Assert.That(indices, Is.EqualTo(TetraIndices.Select(i => (uint)i).ToArray()));
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(positions[i].X, Is.EqualTo(TetraPositions[i].X).Within(1e-6f));
                    Assert.That(positions[i].Y, Is.EqualTo(TetraPositions[i].Y).Within(1e-6f));
                    Assert.That(positions[i].Z, Is.EqualTo(TetraPositions[i].Z).Within(1e-6f));
                    Assert.That(normals[i].X, Is.EqualTo(TetraNormals[i].X).Within(1e-6f));
                    Assert.That(normals[i].Y, Is.EqualTo(TetraNormals[i].Y).Within(1e-6f));
                    Assert.That(normals[i].Z, Is.EqualTo(TetraNormals[i].Z).Within(1e-6f));
                    Assert.That(uvs[i].X, Is.EqualTo(TetraUvs[i].X).Within(1e-6f));
                    Assert.That(uvs[i].Y, Is.EqualTo(1f - TetraUvs[i].Y).Within(1e-6f), "glTF's V runs the other way");
                }
            });
        }

        [Test]
        public void Build_PositionAccessor_HasTheMinAndMaxTheSpecRequires()
        {
            var accessor = Export(SimplePrim()).Accessors[Export(SimplePrim()).Meshes[0].Primitives[0].Attributes[GltfPrimitive.ATTR_POSITION]];

            Assert.Multiple(() =>
            {
                Assert.That(accessor.Min, Is.EqualTo(new double[] { 0, 0, 0 }));
                Assert.That(accessor.Max, Is.EqualTo(new double[] { 1, 1, 1 }));
            });
        }

        [Test]
        public void Build_QuantizedNormals_AreWrittenAsUnitVectors()
        {
            var face = TetraFace();
            for (int i = 0; i < face.Vertices.Count; i++)
            {
                var v = face.Vertices[i];
                v.Normal = v.Normal * 0.9987f;
                face.Vertices[i] = v;
            }

            var doc = Export(SimplePrim(face));

            foreach (var n in doc.GetNormals(doc.Meshes[0].Primitives[0]))
            {
                Assert.That(Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z), Is.EqualTo(1.0).Within(1e-6));
            }
        }

        [Test]
        public void Build_FaceWithoutNormals_LeavesOutTheNormalAttribute()
        {
            var doc = Export(SimplePrim(TetraFace(normals: false)));

            Assert.That(doc.Meshes[0].Primitives[0].Attributes.ContainsKey(GltfPrimitive.ATTR_NORMAL), Is.False);
        }

        [Test]
        public void Build_FaceWithNoTriangles_IsSkipped_AndAMeshWithNoneIsLeftOut()
        {
            var empty = new Face { Vertices = new List<Vertex>(), Indices = new List<ushort>() };

            var withOne = Export(SimplePrim(empty, TetraFace()));
            var withNone = Export(SimplePrim(empty));

            Assert.Multiple(() =>
            {
                Assert.That(withOne.Meshes[0].Primitives, Has.Count.EqualTo(1));
                Assert.That(withNone.Meshes, Is.Empty);
                Assert.That(withNone.Nodes.Count(n => n.Mesh >= 0), Is.EqualTo(0));
            });
        }

        [Test]
        public void Build_FaceWithAnIndexPastItsVertices_IsSkipped()
        {
            var bad = TetraFace();
            bad.Indices[0] = 9;

            Assert.That(Export(SimplePrim(bad)).Meshes, Is.Empty);
        }

        [Test]
        public void Build_FaceWithANonFinitePosition_IsSkipped()
        {
            var bad = TetraFace();
            var v = bad.Vertices[2];
            v.Position = new Vector3(float.NaN, 0, 0);
            bad.Vertices[2] = v;

            Assert.That(Export(SimplePrim(bad, TetraFace())).Meshes[0].Primitives, Has.Count.EqualTo(1));
        }

        [Test]
        public void Build_EveryFace_BecomesAPrimitiveOfOneMesh()
        {
            var doc = Export(SimplePrim(TetraFace(), TetraFace(), TetraFace()));

            Assert.That(doc.Meshes, Has.Count.EqualTo(1));
            Assert.That(doc.Meshes[0].Primitives, Has.Count.EqualTo(3));
        }

        [Test]
        public void Build_Buffer_IsAlignedAndLongEnoughForEveryView()
        {
            var doc = Export(SimplePrim());

            Assert.That(doc.Buffers, Has.Count.EqualTo(1));
            foreach (var view in doc.BufferViews)
            {
                Assert.That(view.ByteOffset % 4, Is.EqualTo(0));
                Assert.That(view.ByteOffset + view.ByteLength, Is.LessThanOrEqualTo(doc.Buffers[0].ByteLength));
            }
        }

        #endregion Geometry

        #region Materials

        private static Primitive.TextureEntryFace Face(Color4 color, UUID texture)
        {
            return new Primitive.TextureEntryFace(null) { RGBA = color, TextureID = texture };
        }

        [Test]
        public void Build_FaceColor_BecomesTheBaseColorOfANonMetallicMaterial()
        {
            var doc = Export(SimplePrim(TetraFace(Face(new Color4(1f, 0.5f, 0.25f, 1f), UUID.Zero))));

            var material = doc.Materials[doc.Meshes[0].Primitives[0].Material];
            Assert.Multiple(() =>
            {
                Assert.That(material.BaseColorFactor.R, Is.EqualTo(1f).Within(0.01f));
                Assert.That(material.BaseColorFactor.G, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(material.BaseColorFactor.B, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(material.MetallicFactor, Is.EqualTo(0f));
                Assert.That(material.RoughnessFactor, Is.EqualTo(1f));
                Assert.That(material.AlphaMode, Is.EqualTo(GltfAlphaMode.Opaque));
            });
        }

        [Test]
        public void Build_TranslucentFace_IsBlended()
        {
            var doc = Export(SimplePrim(TetraFace(Face(new Color4(1f, 1f, 1f, 0.5f), UUID.Zero))));

            var material = doc.Materials[doc.Meshes[0].Primitives[0].Material];
            Assert.That(material.AlphaMode, Is.EqualTo(GltfAlphaMode.Blend));
        }

        [Test]
        public void Build_FacesAlike_ShareAMaterial()
        {
            var red = Face(new Color4(1f, 0f, 0f, 1f), UUID.Zero);
            var blue = Face(new Color4(0f, 0f, 1f, 1f), UUID.Zero);

            var doc = Export(SimplePrim(TetraFace(red), TetraFace(blue), TetraFace(red)));

            Assert.Multiple(() =>
            {
                Assert.That(doc.Materials, Has.Count.EqualTo(2));
                var p = doc.Meshes[0].Primitives;
                Assert.That(p[0].Material, Is.EqualTo(p[2].Material));
                Assert.That(p[0].Material, Is.Not.EqualTo(p[1].Material));
            });
        }

        private static byte[] FakePng(byte seed) =>
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, seed, 1, 2, 3, 4, 5 };

        [Test]
        public void Build_Texture_IsEmbeddedOncePerTextureAndAskedForOnce()
        {
            var textureA = UUID.Random();
            var textureB = UUID.Random();
            var asked = new List<UUID>();
            var exporter = new GltfExporter
            {
                ImageProvider = id =>
                {
                    asked.Add(id);
                    return new ExportImage(FakePng(id == textureA ? (byte)7 : (byte)9), "image/png");
                }
            };
            exporter.Add(SimplePrim(
                TetraFace(Face(Color4.White, textureA)),
                TetraFace(Face(new Color4(1f, 0f, 0f, 1f), textureA)),
                TetraFace(Face(Color4.White, textureB))));

            var doc = exporter.Build();

            Assert.Multiple(() =>
            {
                Assert.That(asked, Is.EquivalentTo(new[] { textureA, textureB }), "one request per texture");
                Assert.That(doc.Images, Has.Count.EqualTo(2));
                Assert.That(doc.Materials, Has.Count.EqualTo(3), "same texture in a different color is a different material");

                var image = doc.Images[doc.Textures[doc.Materials[0].BaseColorTexture!.Index].Source];
                var view = doc.BufferViews[image.BufferView];
                var embedded = doc.Buffers[0].Data!.Skip(view.ByteOffset).Take(view.ByteLength).ToArray();
                Assert.That(image.MimeType, Is.EqualTo("image/png"));
                Assert.That(embedded, Is.EqualTo(FakePng(7)));
            });
        }

        [Test]
        public void Build_TextureTheProviderDoesNotHave_LeavesTheFaceUntextured()
        {
            var exporter = new GltfExporter { ImageProvider = _ => null };
            exporter.Add(SimplePrim(TetraFace(Face(Color4.White, UUID.Random()))));

            var doc = exporter.Build();

            Assert.That(doc.Images, Is.Empty);
            Assert.That(doc.Materials[0].BaseColorTexture, Is.Null);
        }

        [Test]
        public void Build_ProviderThatThrows_CostsTheTextureNotTheExport()
        {
            var exporter = new GltfExporter { ImageProvider = _ => throw new InvalidOperationException("no codec") };
            exporter.Add(SimplePrim(TetraFace(Face(Color4.White, UUID.Random()))));

            var doc = exporter.Build();

            Assert.That(doc.Meshes, Has.Count.EqualTo(1));
            Assert.That(doc.Images, Is.Empty);
        }

        [Test]
        public void Build_NoProvider_ExportsNoTextures()
        {
            var doc = Export(SimplePrim(TetraFace(Face(Color4.White, UUID.Random()))));

            Assert.That(doc.Images, Is.Empty);
        }

        [Test]
        public void Build_ZeroTexture_IsNeverAskedFor()
        {
            bool asked = false;
            var exporter = new GltfExporter { ImageProvider = _ => { asked = true; return null; } };
            exporter.Add(SimplePrim(TetraFace(Face(Color4.White, UUID.Zero))));

            exporter.Build();

            Assert.That(asked, Is.False);
        }

        #endregion Materials


        #region Rig

        private static float Rad(float degrees) => degrees * (float)Math.PI / 180f;

        // Where the two joints rest in avatar space (row vectors). Asymmetric on purpose: rotations about
        // different axes plus translations, so a transposed matrix or swapped joint cannot cancel out.
        private static readonly NumMatrix RestTorso = NumMatrix.CreateRotationZ(Rad(20)) * NumMatrix.CreateTranslation(0.5f, 0.2f, 1.1f);
        private static readonly NumMatrix RestChest = NumMatrix.CreateRotationX(Rad(-35)) * NumMatrix.CreateTranslation(-0.3f, 0.1f, 1.7f);
        private static readonly NumMatrix BindShape = NumMatrix.CreateScale(1.1f, 0.9f, 1.0f) * NumMatrix.CreateRotationZ(Rad(10))
            * NumMatrix.CreateTranslation(0.05f, -0.02f, 0.3f);

        private static float[] Floats(NumMatrix m) => new[]
        {
            m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
        };

        private static NumMatrix Inverse(NumMatrix m)
        {
            Assert.That(NumMatrix.Invert(m, out var inverse), Is.True);
            return inverse;
        }

        private static readonly VertexWeight[] TetraWeights =
        {
            new VertexWeight { Joint0 = 0, Weight0 = 1f },
            new VertexWeight { Joint0 = 1, Weight0 = 1f },
            new VertexWeight { Joint0 = 0, Weight0 = 0.5f, Joint1 = 1, Weight1 = 0.5f },
            new VertexWeight { Joint0 = 0, Weight0 = 0.25f, Joint1 = 1, Weight1 = 0.75f }
        };

        private static FacetedMesh RiggedMesh(uint localId = 1, string[]? joints = null, Face? face = null, NumMatrix? torsoRest = null)
        {
            joints ??= new[] { "mTorso", "mChest" };
            var mesh = SimplePrim(face ?? TetraFace());
            mesh.Prim.LocalID = localId;
            mesh.Prim.Scale = new Vector3(5, 5, 5); // must not be applied to a rigged mesh
            mesh.SkinData = new MeshSkinData
            {
                JointNames = joints,
                BindShapeMatrix = Floats(BindShape),
                InverseBindMatrices = Floats(Inverse(torsoRest ?? RestTorso)).Concat(Floats(Inverse(RestChest))).Take(joints.Length * 16)
                    .Concat(Enumerable.Repeat(0f, Math.Max(0, joints.Length * 16 - 32))).ToArray()
            };
            if (mesh.Faces[0].Weights == null)
            {
                var f = mesh.Faces[0];
                f.Weights = new List<VertexWeight>(TetraWeights);
                mesh.Faces[0] = f;
            }
            return mesh;
        }

        private static int NodeIndex(OSDMap root, string name)
        {
            var nodes = (OSDArray)root["nodes"];
            return Enumerable.Range(0, nodes.Count).First(i => ((OSDMap)nodes[i]).TryGetValue("name", out var n) && n.AsString() == name);
        }

        private static double[] ReadMat4(GltfDocument doc, int accessorIndex, int matrix)
        {
            var accessor = doc.Accessors[accessorIndex];
            var view = doc.BufferViews[accessor.BufferView];
            var data = doc.Buffers[0].Data!;
            var result = new double[16];
            for (int i = 0; i < 16; i++)
            {
                result[i] = BitConverter.ToSingle(data, view.ByteOffset + accessor.ByteOffset + (matrix * 16 + i) * 4);
            }
            return result;
        }

        /// <summary>glTF's skinning formula, worked by hand from the serialized tree: sum of weight * jointWorld * inverseBind * v</summary>
        private static NumVector3 GltfSkinned(OSDMap root, GltfDocument doc, int vertex, int meshIndex = 0, int skinIndex = 0)
        {
            var primitive = doc.Meshes[meshIndex].Primitives[0];
            var skin = doc.Skins[skinIndex];
            var joints = doc.GetJoints(primitive)[vertex];
            var weights = doc.GetWeights(primitive)[vertex];
            var position = doc.GetPositions(primitive)[vertex];

            var sum = NumVector3.Zero;
            var influences = new[] { (joints.j0, weights.X), (joints.j1, weights.Y), (joints.j2, weights.Z), (joints.j3, weights.W) };
            foreach (var (joint, weight) in influences)
            {
                if (weight == 0f) continue;
                var world = WorldMatrix(root, skin.Joints[joint]);
                var skinMatrix = Multiply(world, ReadMat4(doc, skin.InverseBindMatrices, joint));
                sum += Apply(skinMatrix, position) * weight;
            }
            return sum;
        }

        /// <summary>Second Life's skinning formula at a pose, worked from first principles with row vectors:
        /// v * bindShape * sum(weight * inverseBind * jointWorld)</summary>
        private static NumVector3 SlSkinned(Vector3 vertex, VertexWeight weight, NumMatrix[] pose, NumMatrix[]? bound = null)
        {
            // The inverse bind matrices are the inverses of where the joints were when the mesh was bound
            bound ??= new[] { RestTorso, RestChest };
            var inverseBind = new[] { Inverse(bound[0]), Inverse(bound[1]) };
            var influences = new[] { (weight.Joint0, weight.Weight0), (weight.Joint1, weight.Weight1) };

            var sum = new NumMatrix();
            foreach (var (joint, w) in influences)
            {
                if (w == 0f) continue;
                sum += (inverseBind[joint] * pose[joint]) * w;
            }
            var bindShape = NumVector3.Transform(Sl(vertex), BindShape);
            return ToYUp(NumVector3.Transform(bindShape, sum));
        }

        private static NumMatrix GetNodeMatrix(OSDMap root, string name)
        {
            var node = (OSDMap)((OSDArray)root["nodes"])[NodeIndex(root, name)];
            var f = ((OSDArray)node["matrix"]).Select(v => (float)v.AsReal()).ToArray();
            // Column major column vector floats are the row major floats of the row vector matrix
            return new NumMatrix(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]);
        }

        private static void SetNodeMatrix(OSDMap root, string name, NumMatrix rowVectorLocal)
        {
            var node = (OSDMap)((OSDArray)root["nodes"])[NodeIndex(root, name)];
            node.Remove("translation"); node.Remove("rotation"); node.Remove("scale");
            // A row vector matrix's row major floats are a column vector matrix's column major floats
            var array = new OSDArray();
            foreach (var f in Floats(rowVectorLocal)) array.Add(OSD.FromReal(f));
            node["matrix"] = array;
        }

        [Test]
        public void Build_RiggedMesh_AtRest_StaysWhereItWasBound()
        {
            var doc = Export(RiggedMesh());
            var root = Json(doc);

            for (int v = 0; v < 4; v++)
            {
                var expected = SlSkinned(TetraPositions[v], TetraWeights[v], new[] { RestTorso, RestChest });
                AssertClose(GltfSkinned(root, doc, v), expected, "vertex " + v);
            }
        }

        [Test]
        public void Build_RiggedMesh_PosedByMovingAJoint_DeformsTheWaySecondLifeDoes()
        {
            var doc = Export(RiggedMesh());
            var root = Json(doc);

            // Turn and shift the chest in its own frame, as an animator would. Everything above it stays at
            // rest, so its pose in avatar space is the delta applied first and then where it rests.
            var delta = NumMatrix.CreateRotationY(Rad(25)) * NumMatrix.CreateTranslation(0.2f, -0.1f, 0.5f);
            var posedChest = delta * RestChest;
            SetNodeMatrix(root, "mChest", delta * GetNodeMatrix(root, "mChest"));

            for (int v = 0; v < 4; v++)
            {
                var expected = SlSkinned(TetraPositions[v], TetraWeights[v], new[] { RestTorso, posedChest });
                AssertClose(GltfSkinned(root, doc, v), expected, "vertex " + v);
            }

            // Vertex 0 is bound to the torso alone, so it cannot have moved; vertex 1 to the chest alone
            var rest = SlSkinned(TetraPositions[1], TetraWeights[1], new[] { RestTorso, RestChest });
            Assert.That((GltfSkinned(root, doc, 1) - rest).Length(), Is.GreaterThan(0.05f), "the pose must actually move something");
        }

        [Test]
        public void Build_RiggedMesh_IsSkinnedAtTheTopAndIgnoresPrimScale()
        {
            var doc = Export(RiggedMesh());

            var node = doc.Nodes.First(n => n.Skin >= 0);
            Assert.Multiple(() =>
            {
                Assert.That(doc.Nodes[0].Children, Does.Contain(doc.Nodes.IndexOf(node)), "directly under the top node");
                Assert.That(node.Scale, Is.EqualTo(Vector3.One));
                Assert.That(doc.Skins[0].Joints.Select(j => doc.Nodes[j].Name), Is.EqualTo(new[] { "mTorso", "mChest" }), "in the mesh's own order");
                Assert.That(doc.Skins[0].InverseBindMatrices, Is.GreaterThanOrEqualTo(0));
                Assert.That(doc.Accessors[doc.Skins[0].InverseBindMatrices].Type, Is.EqualTo(GltfAccessorType.Mat4));
                Assert.That(doc.Accessors[doc.Skins[0].InverseBindMatrices].Count, Is.EqualTo(2));
            });
        }

        [Test]
        public void Build_RiggedMesh_JointsFollowTheAvatarSkeleton()
        {
            var doc = Export(RiggedMesh());
            int Index(string name) => doc.Nodes.FindIndex(n => n.Name == name);
            int ParentOf(int node) => doc.Nodes.FindIndex(n => n.Children.Contains(node));

            // The ancestors of mChest, bottom to top. The real skeleton has spine bones between these.
            var chain = new List<string>();
            for (int n = ParentOf(Index("mChest")); n >= 0; n = ParentOf(n)) chain.Add(doc.Nodes[n].Name!);

            Assert.Multiple(() =>
            {
                Assert.That(chain, Does.Contain("mTorso"));
                Assert.That(chain.IndexOf("mTorso"), Is.LessThan(chain.IndexOf("mPelvis")), "the torso is below the pelvis");
                Assert.That(chain.Last(), Is.EqualTo("SecondLife"), "all the way up to the top node");
                Assert.That(chain[chain.Count - 2], Is.EqualTo("mPelvis"), "the skeleton root is directly under it");
                Assert.That(doc.Nodes[doc.Skins[0].Skeleton].Name, Is.EqualTo("mPelvis"));
                Assert.That(doc.Skins[0].Joints, Does.Not.Contain(Index("mPelvis")), "ancestors are nodes, not joints of the skin");
            });
        }

        [Test]
        public void Build_RiggedMesh_WeightsSumToOneAndJointsAreInRange()
        {
            var doc = Export(RiggedMesh());
            var primitive = doc.Meshes[0].Primitives[0];

            var joints = doc.GetJoints(primitive);
            var weights = doc.GetWeights(primitive);
            for (int v = 0; v < 4; v++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(weights[v].X + weights[v].Y + weights[v].Z + weights[v].W, Is.EqualTo(1f).Within(1e-5f));
                    Assert.That(new[] { joints[v].j0, joints[v].j1, joints[v].j2, joints[v].j3 }.Max(), Is.LessThan(2));
                });
            }
            Assert.That(doc.Accessors[primitive.Attributes[GltfPrimitive.ATTR_JOINTS_0]].ComponentType, Is.EqualTo(GltfComponentType.UnsignedShort));
        }

        [Test]
        public void Build_RiggedMesh_WithClampedWeights_AreRenormalized()
        {
            // The decoder clamps a weight to 0.001..0.999, so a vertex can come out summing to over 1
            var face = TetraFace();
            face.Weights = new List<VertexWeight>
            {
                new VertexWeight { Joint0 = 0, Weight0 = 0.999f, Joint1 = 1, Weight1 = 0.999f },
                new VertexWeight { Joint0 = 1, Weight0 = 0.001f },
                new VertexWeight { Joint0 = 0, Weight0 = 0.3f, Joint1 = 1, Weight1 = 0.3f },
                new VertexWeight { Joint0 = 0, Weight0 = 0.2f }
            };

            var doc = Export(RiggedMesh(face: face));

            foreach (var w in doc.GetWeights(doc.Meshes[0].Primitives[0]))
            {
                Assert.That(w.X + w.Y + w.Z + w.W, Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void Build_RiggedFace_WithNoOrTooFewWeights_FollowsTheFirstJoint()
        {
            var face = TetraFace();
            face.Weights = new List<VertexWeight> { TetraWeights[1] }; // one vertex's worth for four vertices

            var doc = Export(RiggedMesh(face: face));

            var primitive = doc.Meshes[0].Primitives[0];
            var joints = doc.GetJoints(primitive);
            var weights = doc.GetWeights(primitive);
            Assert.Multiple(() =>
            {
                Assert.That(joints[0].j0, Is.EqualTo(1));
                for (int v = 1; v < 4; v++)
                {
                    Assert.That(joints[v].j0, Is.EqualTo(0));
                    Assert.That(weights[v].X, Is.EqualTo(1f));
                }
            });

            var none = TetraFace();
            var f = none;
            f.Weights = null;
            var meshNone = RiggedMesh(face: f);
            var mf = meshNone.Faces[0];
            mf.Weights = null;
            meshNone.Faces[0] = mf;
            var docNone = Export(meshNone);
            Assert.That(docNone.GetWeights(docNone.Meshes[0].Primitives[0]).All(w => w.X == 1f), Is.True);
        }

        [Test]
        public void Build_TwoRiggedMeshes_ShareTheirJointNodes()
        {
            var doc = Export(RiggedMesh(1), RiggedMesh(2));

            Assert.Multiple(() =>
            {
                Assert.That(doc.Skins, Has.Count.EqualTo(2));
                Assert.That(doc.Nodes.Count(n => n.Name == "mTorso"), Is.EqualTo(1));
                Assert.That(doc.Skins[0].Joints, Is.EqualTo(doc.Skins[1].Joints));
            });
        }

        [Test]
        public void Build_JointNameAnAvatarSkeletonDoesNotKnow_StillRestsWhereItsBindMatrixSays()
        {
            var doc = Export(RiggedMesh(joints: new[] { "mTorso", "customBone" }));
            var root = Json(doc);

            // customBone is a top level node: its world is its local, and its inverse bind matrix is RestChest's
            var expected = Apply(WorldMatrix(root, NodeIndex(root, "customBone")), Vector3.Zero);
            var restOrigin = NumVector3.Transform(NumVector3.Zero, RestChest);
            AssertClose(expected, ToYUp(restOrigin), "customBone origin");
            Assert.That(doc.Nodes[0].Children, Does.Contain(doc.Nodes.FindIndex(n => n.Name == "customBone")));
            Assert.That(doc.Skins[0].Skeleton, Is.EqualTo(-1), "mPelvis is not an ancestor of customBone, so it cannot be the skeleton root");
        }

        [Test]
        public void Build_TwoRiggedMeshes_ThatDisagreeAboutWhereAJointRests_BothStayInShapeAtRest()
        {
            // The second mesh was bound with its torso 0.3 further along X. If the two shared a torso node, one
            // of them would be pulled that far out of shape at rest.
            var shifted = RestTorso * NumMatrix.CreateTranslation(0.3f, 0f, 0f);
            var doc = Export(RiggedMesh(1), RiggedMesh(2, torsoRest: shifted));
            var root = Json(doc);

            Assert.That(doc.Skins, Has.Count.EqualTo(2));
            Assert.That(doc.Skins[0].Joints[0], Is.Not.EqualTo(doc.Skins[1].Joints[0]), "they could not share the torso");
            for (int v = 0; v < 4; v++)
            {
                AssertClose(GltfSkinned(root, doc, v, meshIndex: 0, skinIndex: 0),
                    SlSkinned(TetraPositions[v], TetraWeights[v], new[] { RestTorso, RestChest }), "first mesh, vertex " + v);
                AssertClose(GltfSkinned(root, doc, v, meshIndex: 1, skinIndex: 1),
                    SlSkinned(TetraPositions[v], TetraWeights[v], new[] { shifted, RestChest }, new[] { shifted, RestChest }),
                    "second mesh, vertex " + v);
            }
        }

        [Test]
        public void Build_TwoRiggedMeshes_ThatAgreeWithinRounding_StillShareJoints()
        {
            var nudged = RestTorso * NumMatrix.CreateTranslation(0.00001f, 0f, 0f);

            var doc = Export(RiggedMesh(1), RiggedMesh(2, torsoRest: nudged));

            Assert.That(doc.Skins[0].Joints, Is.EqualTo(doc.Skins[1].Joints));
        }

        [Test]
        public void Build_InverseBindMatrices_AreNotVertexData()
        {
            var doc = Export(RiggedMesh());

            var view = doc.BufferViews[doc.Accessors[doc.Skins[0].InverseBindMatrices].BufferView];
            Assert.That(view.Target, Is.EqualTo(-1));
        }

        [Test]
        public void Build_RiggedAndPlainPrimsTogether_EachGoWhereTheyBelong()
        {
            var plain = Prim(7, 0, Vector3.Zero, Quaternion.Identity, new Vector3(2, 2, 2));
            var doc = Export(plain, RiggedMesh(8));

            Assert.Multiple(() =>
            {
                Assert.That(doc.Skins, Has.Count.EqualTo(1));
                Assert.That(doc.Meshes, Has.Count.EqualTo(2));
                Assert.That(doc.Nodes.Count(n => n.Mesh >= 0 && n.Skin < 0), Is.EqualTo(1));
                Assert.That(WorldPositions(doc, "prim7"), Has.Count.EqualTo(4));
            });
        }

        [Test]
        public void Build_RiggedMeshThatSurvivesAGlbRoundTrip_StillSkinsTheSame()
        {
            var bytes = new GltfExporter { }.Also(e => e.Add(RiggedMesh())).ToGlb();
            var doc = GltfDocument.Load(bytes);

            var root = (OSDMap)OSDParser.DeserializeJson(doc.ToJson());
            for (int v = 0; v < 4; v++)
            {
                var expected = SlSkinned(TetraPositions[v], TetraWeights[v], new[] { RestTorso, RestChest });
                AssertClose(GltfSkinned(root, doc, v), expected, "vertex " + v);
            }
        }

        #endregion Rig

        #region Files

        [TestCase("model.glb")]
        [TestCase("model.gltf")]
        [TestCase("MODEL.GLB")]
        public void Save_WritesAFileTheDocumentLoaderReadsBack(string name)
        {
            var exporter = new GltfExporter { ImageProvider = _ => new ExportImage(FakePng(1), "image/png") };
            exporter.Add(SimplePrim(TetraFace(Face(Color4.White, UUID.Random()))));
            var path = Path.Combine(_tempDir, name);

            exporter.Save(path);

            var data = File.ReadAllBytes(path);
            var doc = name.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
                ? GltfDocument.Load(data)
                : GltfDocument.LoadGltf(System.Text.Encoding.UTF8.GetString(data));
            Assert.Multiple(() =>
            {
                Assert.That(doc.Meshes, Has.Count.EqualTo(1));
                Assert.That(doc.Meshes[0].Primitives, Has.Count.EqualTo(1));
                Assert.That(doc.GetPositions(doc.Meshes[0].Primitives[0]), Has.Length.EqualTo(4));
                Assert.That(doc.Images, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Save_AnyOtherExtension_Throws()
        {
            Assert.Throws<ArgumentException>(() => new GltfExporter().Save(Path.Combine(_tempDir, "model.dae")));
        }

        [Test]
        public void Build_ExportThenGltfLoader_GivesBackTheSameShapeInTheSameSpace()
        {
            // A real mesh asset: encode it, decode it the way a viewer does, export it, import it again.
            // Placement is already pinned by hand above; this is the whole path end to end.
            var original = new ModelPrim { Scale = new Vector3(2, 3, 4), Rotation = Quaternion.Identity };
            var modelFace = new ModelFace { MaterialID = "m" };
            for (int t = 0; t < TetraIndices.Length; t++)
            {
                var p = TetraPositions[TetraIndices[t]] - new Vector3(0.5f, 0.5f, 0.5f);
                modelFace.AddVertex(new Vertex
                {
                    Position = p,
                    Normal = TetraNormals[TetraIndices[t]],
                    TexCoord = TetraUvs[TetraIndices[t]]
                });
            }
            original.Faces.Add(modelFace);
            original.CreateAsset(UUID.Zero);

            var prim = new Primitive { LocalID = 1, Scale = original.Scale, Rotation = Quaternion.Identity };
            Assert.That(FacetedMesh.TryDecodeFromAsset(prim, new AssetMesh(UUID.Random(), original.Asset),
                DetailLevel.Highest, out var decoded), Is.True);

            var path = Path.Combine(_tempDir, "roundtrip.glb");
            var exporter = new GltfExporter();
            exporter.Add(decoded!);
            exporter.Save(path);
            var reloaded = new GltfLoader().Load(path, loadImages: false);

            Assert.That(reloaded, Has.Count.EqualTo(1));
            var rot = Matrix4.CreateFromQuaternion(reloaded[0].Rotation);
            var actual = reloaded[0].Faces[0].Vertices.Select(v =>
                Vector3.Transform(v.Position * reloaded[0].Scale, rot) + reloaded[0].Position).ToList();
            var expected = modelFace.Vertices.Select(v => v.Position * original.Scale).ToList();

            // 16 bit positions across a unit cube, scaled up by up to 4
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(actual[i].X, Is.EqualTo(expected[i].X).Within(1e-3f), $"vertex {i} X");
                    Assert.That(actual[i].Y, Is.EqualTo(expected[i].Y).Within(1e-3f), $"vertex {i} Y");
                    Assert.That(actual[i].Z, Is.EqualTo(expected[i].Z).Within(1e-3f), $"vertex {i} Z");
                });
            }
        }

        #endregion Files
    }
}
