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
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for FacetedMesh -&gt; OBJ and COLLADA. A linkset with uneven sizes, rotations that do not
    /// commute and a slanted normal is exported and read back by each format's loader, and what comes out is
    /// compared with world positions and normals worked out here from the prims, so the exporters and
    /// loaders cannot agree on a mistake. The COLLADA node matrix is also checked by hand.
    /// </summary>
    [TestFixture]
    public class ModelExporterTests
    {
        public enum Format { Obj, Collada, Glb }

        private static readonly Vector3[] Positions =
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)
        };
        private static readonly Vector3[] Normals =
        {
            new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 1)
        };
        private static readonly Vector2[] Uvs =
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0.25f, 0.75f)
        };
        private static readonly ushort[] Indices = { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 };

        private string _tempDir = string.Empty;
        private CultureInfo _culture = CultureInfo.CurrentCulture;

        [SetUp]
        public void SetUp()
        {
            _culture = CultureInfo.CurrentCulture;
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-export2-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _culture;
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        #region Builders

        private static Face TetraFace(Primitive.TextureEntryFace? texture = null, bool normals = true)
        {
            var face = new Face
            {
                Vertices = new List<Vertex>(),
                Indices = new List<ushort>(Indices),
                TextureFace = texture ?? new Primitive.TextureEntryFace(null)
            };
            for (int i = 0; i < Positions.Length; i++)
            {
                face.Vertices.Add(new Vertex
                {
                    Position = Positions[i],
                    Normal = normals ? Normals[i] : Vector3.Zero,
                    TexCoord = Uvs[i]
                });
            }
            return face;
        }

        private static FacetedMesh Prim(uint localId, uint parentId, Vector3 position, Quaternion rotation, Vector3 scale,
            string? name = null, params Face[] faces)
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
            if (name != null) mesh.Prim.Properties = new Primitive.ObjectProperties { Name = name };
            mesh.Faces.AddRange(faces.Length > 0 ? faces : new[] { TetraFace() });
            return mesh;
        }

        private static readonly Quaternion RootRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(Math.PI / 2));
        private static readonly Quaternion ChildRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(Math.PI / 2));
        private static readonly Vector3 RootScale = new Vector3(2, 3, 4);
        private static readonly Vector3 ChildScale = new Vector3(1, 2, 3);
        private static readonly Vector3 ChildPosition = new Vector3(5, 0, 0);

        /// <summary>A root with a child. The two rotations do not commute, and neither scale is even.</summary>
        private static FacetedMesh[] Linkset()
        {
            return new[]
            {
                Prim(1, 0, new Vector3(100, 200, 30), RootRotation, RootScale),
                Prim(2, 1, ChildPosition, ChildRotation, ChildScale)
            };
        }

        /// <summary>The world position and the normal as drawn for each exported vertex of <see cref="Linkset"/></summary>
        private static List<(Vector3 Position, Vector3 Normal)> ExpectedLinkset()
        {
            var result = new List<(Vector3, Vector3)>();
            for (int i = 0; i < Positions.Length; i++)
            {
                // The root: sized, then turned by its own rotation. Its world position is dropped.
                result.Add(((Positions[i] * RootScale) * RootRotation,
                    Vector3.Normalize((Normals[i] / RootScale) * RootRotation)));
                // The child: sized, turned and moved within the root, and then the root's rotation applies
                result.Add((((Positions[i] * ChildScale) * ChildRotation + ChildPosition) * RootRotation,
                    Vector3.Normalize(((Normals[i] / ChildScale) * ChildRotation) * RootRotation)));
            }
            return result;
        }

        private string Export(Format format, params FacetedMesh[] meshes)
        {
            switch (format)
            {
                case Format.Obj:
                {
                    var exporter = new ObjExporter();
                    foreach (var m in meshes) exporter.Add(m);
                    var path = Path.Combine(_tempDir, "out.obj");
                    exporter.Save(path);
                    return path;
                }
                case Format.Collada:
                {
                    var exporter = new ColladaExporter();
                    foreach (var m in meshes) exporter.Add(m);
                    var path = Path.Combine(_tempDir, "out.dae");
                    exporter.Save(path);
                    return path;
                }
                default:
                {
                    var exporter = new GltfExporter();
                    foreach (var m in meshes) exporter.Add(m);
                    var path = Path.Combine(_tempDir, "out.glb");
                    exporter.Save(path);
                    return path;
                }
            }
        }

        private static List<ModelPrim> Load(Format format, string path)
        {
            switch (format)
            {
                case Format.Obj: return new ObjLoader().Load(path, false);
                case Format.Collada: return new ColladaLoader().Load(path, false);
                default: return new GltfLoader().Load(path, false);
            }
        }

        /// <summary>Every vertex of every loaded prim in world space, with its normal as the viewer draws it</summary>
        private static List<(Vector3 Position, Vector3 Normal)> World(IEnumerable<ModelPrim> prims)
        {
            var result = new List<(Vector3, Vector3)>();
            foreach (var prim in prims)
            {
                foreach (var face in prim.Faces)
                {
                    foreach (var v in face.Vertices)
                    {
                        // The stored vertex is in the unit cube. The prim's size undoes that for positions, and for
                        // normals it is divided out (they follow the inverse transpose).
                        var position = prim.Position + (v.Position * prim.Scale) * prim.Rotation;
                        var normal = Vector3.Normalize(((v.Normal / prim.Scale)) * prim.Rotation);
                        result.Add((position, normal));
                    }
                }
            }
            return result;
        }

        private static void AssertSame(List<(Vector3 Position, Vector3 Normal)> expected, List<(Vector3 Position, Vector3 Normal)> actual)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            var left = new List<(Vector3 Position, Vector3 Normal)>(actual);
            foreach (var e in expected)
            {
                int found = left.FindIndex(a => (a.Position - e.Position).Length() < 2e-3f && (a.Normal - e.Normal).Length() < 2e-3f);
                Assert.That(found, Is.GreaterThanOrEqualTo(0),
                    $"nothing at {e.Position} with normal {e.Normal}; left: " + string.Join(" ", left.Select(l => $"[{l.Position} / {l.Normal}]")));
                left.RemoveAt(found);
            }
        }

        #endregion Builders

        #region Placement

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        [TestCase(Format.Glb)]
        public void Linkset_ComesBackWhereThePrimsPutIt(Format format)
        {
            var path = Export(format, Linkset());

            AssertSame(ExpectedLinkset(), World(Load(format, path)));
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void SeveralFaces_EachKeepTheirOwnVertices(Format format)
        {
            // The second face sits 10 along X and shares nothing with the first, in a material of its own and
            // then in the first face's again, so indices have to count on from the vertices before them
            var shifted = TetraFace(new Primitive.TextureEntryFace(null) { RGBA = new Color4(1, 0, 0, 1) });
            for (int i = 0; i < shifted.Vertices.Count; i++)
            {
                var v = shifted.Vertices[i];
                v.Position += new Vector3(10, 0, 0);
                shifted.Vertices[i] = v;
            }
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, null, TetraFace(), shifted, TetraFace());

            var prims = Load(format, Export(format, mesh));

            var expected = new List<(Vector3, Vector3)>();
            for (int i = 0; i < Positions.Length; i++)
            {
                expected.Add((Positions[i], Vector3.Normalize(Normals[i])));
                expected.Add((Positions[i] + new Vector3(10, 0, 0), Vector3.Normalize(Normals[i])));
            }
            // the third face repeats the first, and loaders merge a material's identical faces into one
            var world = World(prims);
            var distinct = new List<(Vector3 Position, Vector3 Normal)>();
            foreach (var w in world)
                if (!distinct.Any(d => (d.Position - w.Position).Length() < 1e-3f && (d.Normal - w.Normal).Length() < 1e-3f)) distinct.Add(w);
            AssertSame(expected, distinct);
        }

        [Test]
        public void Collada_NodeMatrix_IsTheScaledAndTurnedPrim()
        {
            // Z quarter turn: x -> y, y -> -x. Scaled (2, 3, 4) first, so the columns are the turned,
            // scaled axes: (0, 2, 0), (-3, 0, 0) and (0, 0, 4). COLLADA writes the matrix a row at a time.
            var path = Export(Format.Collada, Prim(1, 0, new Vector3(9, 9, 9), RootRotation, RootScale));

            var matrix = ReadMatrices(path).Single();

            double[] expected = { 0, -3, 0, 0, 2, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 1 };
            Assert.That(matrix, Has.Length.EqualTo(16));
            for (int i = 0; i < 16; i++) Assert.That(matrix[i], Is.EqualTo(expected[i]).Within(1e-5), $"element {i}");
        }

        [Test]
        public void Collada_ChildNodeMatrix_PutsTheChildInTheRootsFrame()
        {
            // The child's origin is (5, 0, 0) in the root's frame, which the root's quarter turn takes to (0, 5, 0)
            var path = Export(Format.Collada, Linkset());

            var matrices = ReadMatrices(path);

            Assert.That(matrices, Has.Count.EqualTo(2));
            var child = matrices[1];
            Assert.That(new[] { child[3], child[7], child[11] }, Is.EqualTo(new[] { 0.0, 5.0, 0.0 }).Within(1e-5));
        }

        private static List<double[]> ReadMatrices(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("c", "http://www.collada.org/2005/11/COLLADASchema");
            return doc.SelectNodes("//c:node/c:matrix", ns)!.Cast<XmlNode>()
                .Select(n => n.InnerText.Split(' ').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray())
                .ToList();
        }

        [Test]
        public void Obj_IsYUp()
        {
            // A prim of size 1 with a vertex at +Z: the model's top. In a Y-up file that is +Y.
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One);

            var text = File.ReadAllText(Export(Format.Obj, mesh));

            Assert.That(VertexLines(text), Does.Contain("0 1 0"), "(0, 0, 1) becomes (0, 1, 0)");
            Assert.That(VertexLines(text), Does.Contain("0 0 -1").Or.Contain("0 -0 0").Or.Contain("0 0 0"));
            Assert.That(VertexLines(text), Does.Contain("1 0 0"));
        }

        private static List<string> VertexLines(string text) =>
            text.Split('\n').Where(l => l.StartsWith("v ")).Select(l => l.Substring(2).Trim()).ToList();

        [Test]
        public void Obj_VertexNormalsAreInverseTransposed()
        {
            // Size (1, 1, 4) with a slanted normal (1, 0, 1): drawn, it must lean towards +X, not the diagonal
            var face = TetraFace();
            face.Vertices[0] = new Vertex { Position = Positions[0], Normal = new Vector3(1, 0, 1), TexCoord = Uvs[0] };
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, new Vector3(1, 1, 4), null, face);

            var lines = File.ReadAllLines(Export(Format.Obj, mesh));
            var first = lines.First(l => l.StartsWith("vn ")).Substring(3).Split(' ')
                .Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

            // n / size = (1, 0, 0.25), normalized, then into Y-up (x, z, -y)
            var expected = Vector3.Normalize(new Vector3(1, 0, 0.25f));
            Assert.That(first[0], Is.EqualTo(expected.X).Within(1e-5f));
            Assert.That(first[1], Is.EqualTo(expected.Z).Within(1e-5f));
            Assert.That(first[2], Is.EqualTo(-expected.Y).Within(1e-5f));
        }

        [Test]
        public void Obj_MirroredPrim_KeepsItsTrianglesFacingOut()
        {
            // A triangle facing +Z, mirrored along X: its winding has to flip to still face the way its normal does
            var face = new Face
            {
                Vertices = new List<Vertex>
                {
                    new Vertex { Position = new Vector3(0, 0, 0), Normal = Vector3.UnitZ },
                    new Vertex { Position = new Vector3(1, 0, 0), Normal = Vector3.UnitZ },
                    new Vertex { Position = new Vector3(0, 1, 0), Normal = Vector3.UnitZ }
                },
                Indices = new List<ushort> { 0, 1, 2 },
                TextureFace = new Primitive.TextureEntryFace(null)
            };
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, new Vector3(-1, 1, 1), null, face);

            var lines = File.ReadAllLines(Export(Format.Obj, mesh));
            var p = lines.Where(l => l.StartsWith("v ")).Select(l => Floats(l.Substring(2))).ToArray();
            var n = Floats(lines.First(l => l.StartsWith("vn ")).Substring(3));
            var corners = lines.First(l => l.StartsWith("f ")).Substring(2).Split(' ')
                .Select(c => int.Parse(c.Split('/')[0]) - 1).ToArray();

            var a = p[corners[0]]; var b = p[corners[1]]; var c2 = p[corners[2]];
            var cross = Vector3.Cross(b - a, c2 - a);
            Assert.That(Vector3.Dot(cross, n), Is.GreaterThan(0f), "the triangle faces the way its normal does");
        }

        private static Vector3 Floats(string text)
        {
            var parts = text.Trim().Split(' ').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(parts[0], parts[1], parts[2]);
        }

        #endregion Placement

        #region Safety

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void Numbers_DoNotDependOnTheCurrentCulture(Format format)
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, new Vector3(1.5f, 1, 1));

            var path = Export(format, mesh);
            var text = File.ReadAllText(path);

            Assert.That(text, Does.Not.Match(@"\d,\d"), "no decimal commas");
            Assert.That(text, Does.Contain("0.25"));
            // and it still reads back
            var prims = Load(format, path);
            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Scale.X, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void NameWithLineBreaks_CannotAddLinesToTheFile(Format format)
        {
            var evil = "wall\nv 9 9 9\r\nmtllib other.mtl\u2028usemtl x\u0001";
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, evil);

            var path = Export(format, mesh);

            if (format == Format.Obj)
            {
                var lines = File.ReadAllLines(path);
                Assert.That(lines.Count(l => l.StartsWith("v ")), Is.EqualTo(4), "only the mesh's own vertices");
                Assert.That(lines.Count(l => l.StartsWith("mtllib ")), Is.EqualTo(1));
                Assert.That(lines.Count(l => l.StartsWith("usemtl ")), Is.EqualTo(1));
            }
            else
            {
                var doc = new XmlDocument();
                Assert.DoesNotThrow(() => doc.Load(path), "the document is still well formed");
            }
            Assert.That(Load(format, path), Has.Count.EqualTo(1));
        }

        [Test]
        public void FileNamesWithDirectoriesOrTheWrongExtension_AreRefused()
        {
            var obj = new ObjExporter();
            var dae = new ColladaExporter();

            Assert.Throws<ArgumentException>(() => obj.Build("sub/model.obj"));
            Assert.Throws<ArgumentException>(() => obj.Build("model.dae"));
            Assert.Throws<ArgumentException>(() => dae.Build("model.obj"));
            Assert.Throws<ArgumentException>(() => dae.Save(Path.Combine(_tempDir, "model.txt")));
            Assert.Throws<ArgumentNullException>(() => obj.Add(null!));
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void Export_DoesNotChangeTheMeshes(Format format)
        {
            var meshes = Linkset();
            var before = meshes.Select(m => m.Faces[0].Vertices.Select(v => (v.Position, v.Normal, v.TexCoord)).ToList()).ToList();

            Export(format, meshes);

            for (int i = 0; i < meshes.Length; i++)
                Assert.That(meshes[i].Faces[0].Vertices.Select(v => (v.Position, v.Normal, v.TexCoord)), Is.EqualTo(before[i]));
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void BrokenFaces_AreLeftOutAndTheRestExported(Format format)
        {
            var badIndex = TetraFace();
            badIndex.Indices[0] = 99;
            var nan = TetraFace();
            nan.Vertices[1] = new Vertex { Position = new Vector3(float.NaN, 0, 0), Normal = Vector3.UnitZ };
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, null, badIndex, nan, TetraFace());

            var prims = Load(format, Export(format, mesh));

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces, Has.Count.EqualTo(1));
        }

        #endregion Safety

        #region Materials and textures

        private static Primitive.TextureEntryFace Texture(UUID id, Color4 color)
        {
            return new Primitive.TextureEntryFace(null) { TextureID = id, RGBA = color };
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void Textures_AreWrittenBesideTheModelUnderTheirAssetIds(Format format)
        {
            var png = UUID.Random();
            var jpg = UUID.Random();
            var missing = UUID.Random();
            var calls = new List<UUID>();
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, "evil name\n.png",
                TetraFace(Texture(png, Color4.White)), TetraFace(Texture(png, Color4.White)),
                TetraFace(Texture(jpg, Color4.White)), TetraFace(Texture(missing, Color4.White)));

            Func<UUID, ExportImage?> provider = id =>
            {
                calls.Add(id);
                if (id == png) return new ExportImage(new byte[] { 1, 2, 3 }, "image/png");
                if (id == jpg) return new ExportImage(new byte[] { 4, 5 }, "image/jpeg");
                return null;
            };
            var dir = Path.Combine(_tempDir, "nested", "deeper");
            string modelPath;
            if (format == Format.Obj)
            {
                modelPath = Path.Combine(dir, "house.obj");
                var exporter = new ObjExporter { ImageProvider = provider };
                exporter.Add(mesh);
                exporter.Save(modelPath);
            }
            else
            {
                modelPath = Path.Combine(dir, "house.dae");
                var exporter = new ColladaExporter { ImageProvider = provider };
                exporter.Add(mesh);
                exporter.Save(modelPath);
            }

            Assert.That(File.ReadAllBytes(Path.Combine(dir, png + ".png")), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.ReadAllBytes(Path.Combine(dir, jpg + ".jpg")), Is.EqualTo(new byte[] { 4, 5 }));
            Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Has.None.Contain("evil"), "named by asset ID only");
            Assert.That(calls.Count(c => c == png), Is.EqualTo(1), "each texture is asked for once");

            var loaded = Load(format, modelPath).Single();
            var textures = loaded.Faces.Select(f => f.Material.Texture).ToList();
            Assert.That(textures, Is.EquivalentTo(new[] { png + ".png", jpg + ".jpg", null }.Select(t => t ?? string.Empty)).Or
                .EquivalentTo(new string?[] { png + ".png", jpg + ".jpg", null }));
            foreach (var texture in textures.Where(t => !string.IsNullOrEmpty(t)))
            {
                Assert.That(File.Exists(Path.Combine(dir, texture!)), Is.True);
                Assert.That(Path.GetFileName(texture), Is.EqualTo(texture), "a plain file name, so a loader that stays in the model's directory can read it");
            }
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void ColorsAndOpacity_ComeBack(Format format)
        {
            var red = new Color4(1f, 0.25f, 0f, 0.5f);
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, null,
                TetraFace(Texture(UUID.Zero, red)), TetraFace(Texture(UUID.Zero, Color4.White)), TetraFace(Texture(UUID.Zero, red)));

            var prim = Load(format, Export(format, mesh)).Single();

            Assert.That(prim.Faces, Has.Count.EqualTo(2), "faces of one color share a material");
            var colors = prim.Faces.Select(f => f.Material.DiffuseColor).ToList();
            Assert.That(colors.Any(c => Math.Abs(c.R - 1f) < 1e-5f && Math.Abs(c.G - 0.25f) < 1e-5f && Math.Abs(c.A - 0.5f) < 1e-5f), Is.True);
            Assert.That(colors.Any(c => Math.Abs(c.R - 1f) < 1e-5f && Math.Abs(c.G - 1f) < 1e-5f && Math.Abs(c.A - 1f) < 1e-5f), Is.True);
        }

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void SkipTransparentFaces_LeavesOutInvisibleFaces(Format format)
        {
            var clear = Texture(UUID.Zero, new Color4(1f, 1f, 1f, 0f));
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, null, TetraFace(), TetraFace(clear));

            string Run(bool skip)
            {
                if (format == Format.Obj)
                {
                    var e = new ObjExporter { SkipTransparentFaces = skip };
                    e.Add(mesh);
                    return Path.Combine(_tempDir, skip ? "skip.obj" : "keep.obj").Also(p => e.Save(p));
                }
                var d = new ColladaExporter { SkipTransparentFaces = skip };
                d.Add(mesh);
                return Path.Combine(_tempDir, skip ? "skip.dae" : "keep.dae").Also(p => d.Save(p));
            }

            Assert.That(Load(format, Run(false)).Single().Faces, Has.Count.EqualTo(2));
            Assert.That(Load(format, Run(true)).Single().Faces, Has.Count.EqualTo(1));
        }

        [Test]
        public void FacesWithoutNormals_AreWrittenWithoutThem()
        {
            var mesh = Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, null, TetraFace(normals: false));

            var obj = File.ReadAllLines(Export(Format.Obj, mesh));
            Assert.That(obj.Count(l => l.StartsWith("vn ")), Is.EqualTo(0));
            Assert.That(obj.First(l => l.StartsWith("f ")).Split(' ').Skip(1), Has.All.Matches(@"^\d+/\d+$"));

            var dae = File.ReadAllText(Export(Format.Collada, mesh));
            var triangles = Regex.Match(dae, "<triangles.*?</triangles>", RegexOptions.Singleline).Value;
            Assert.That(triangles, Does.Not.Contain("NORMAL"));
            Assert.That(Load(Format.Collada, Path.Combine(_tempDir, "out.dae")), Has.Count.EqualTo(1));
        }

        [Test]
        public void Collada_IdsAreUniqueAndMaterialSymbolsAreTheDocumentWide()
        {
            var a = Texture(UUID.Zero, new Color4(1, 0, 0, 1));
            var b = Texture(UUID.Zero, new Color4(0, 1, 0, 1));
            var meshes = new[]
            {
                Prim(1, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, "same name", TetraFace(a), TetraFace(b)),
                Prim(2, 0, Vector3.Zero, Quaternion.Identity, Vector3.One, "same name", TetraFace(b), TetraFace(a))
            };

            var doc = new XmlDocument();
            doc.Load(Export(Format.Collada, meshes));

            var ids = doc.SelectNodes("//@id")!.Cast<XmlNode>().Select(n => n.Value).ToList();
            Assert.That(ids, Is.Unique);
            Assert.That(ids.Where(i => i != null), Has.All.Matches(@"^[A-Za-z_][A-Za-z0-9_.\-]*$"), "valid XML names");
            Assert.That(Load(Format.Collada, Path.Combine(_tempDir, "out.dae")).Sum(p => p.Faces.Count), Is.EqualTo(4));
        }

        #endregion Materials and textures

        #region Rigged

        [TestCase(Format.Obj)]
        [TestCase(Format.Collada)]
        public void RiggedMesh_IsExportedInItsBindPoseWithoutTheSkin(Format format)
        {
            // Scaled (2, 1, 1) and lifted by 3: a normal has to follow the inverse transpose
            var mesh = Prim(7, 0, new Vector3(50, 50, 50), Quaternion.Identity, new Vector3(9, 9, 9));
            mesh.SkinData = new MeshSkinData
            {
                JointNames = new[] { "mPelvis" },
                InverseBindMatrices = new float[]
                {
                    1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1
                },
                BindShapeMatrix = new float[]
                {
                    2, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 3, 1
                }
            };

            var path = Export(format, mesh);
            var prims = Load(format, path);

            // The prim's own position and size play no part: a rigged mesh is placed by its bind shape
            var expected = new List<(Vector3, Vector3)>();
            for (int i = 0; i < Positions.Length; i++)
            {
                expected.Add((new Vector3(Positions[i].X * 2, Positions[i].Y, Positions[i].Z + 3),
                    Vector3.Normalize(new Vector3(Normals[i].X / 2, Normals[i].Y, Normals[i].Z))));
            }
            AssertSame(expected, World(prims));
        }

        #endregion Rigged
    }

    internal static class StringExtensions
    {
        public static string Also(this string value, Action<string> action)
        {
            action(value);
            return value;
        }
    }
}
