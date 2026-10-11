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
using System.Text;
using LibreMetaverse.Imaging;
using LibreMetaverse.ImportExport;
using NUnit.Framework;
using Path = System.IO.Path;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the .obj/.mtl -&gt; List&lt;ModelPrim&gt; path. Models are written as text to a temp
    /// directory and loaded through the real file path. Each hostile-input test has a control that
    /// loads, so a refusal can only be down to the one thing the test changed.
    /// </summary>
    [TestFixture]
    public class ObjLoaderTests
    {
        // The tetrahedron the glTF tests use: one distinct normal and UV per corner
        private const string TetraGeometry =
            "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\n" +
            "vt 0 0\nvt 1 0\nvt 0 1\nvt 1 1\n" +
            "vn 0 0 1\nvn 1 0 0\nvn 0 1 0\nvn 0 0 -1\n";

        private const string TetraFaces =
            "f 1/1/1 3/3/3 2/2/2\nf 1/1/1 2/2/2 4/4/4\nf 2/2/2 3/3/3 4/4/4\nf 3/3/3 1/1/1 4/4/4\n";

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "lm-obj-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private string Write(string text, string name = "model.obj")
        {
            var path = Path.Combine(_tempDir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        private List<ModelPrim> Load(string text, bool loadImages = false)
        {
            return new ObjLoader().Load(Write(text), loadImages);
        }

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

        private static string Quad(string faceLine = "f 1 2 3 4") =>
            "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n" + faceLine + "\n";

        private static int TriangleCount(ModelFace face) => face.Indices.Count / 3;

        #region Loading

        [Test]
        public void Load_Tetrahedron_GivesOnePrimWithFourTriangles()
        {
            var prims = Load(TetraGeometry + TetraFaces);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(prims[0].Faces, Has.Count.EqualTo(1));
                Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(4));
                Assert.That(prims[0].Faces[0].Vertices, Has.Count.EqualTo(4));
                Assert.That(prims[0].Asset, Is.Not.Empty);
                Assert.That(prims[0].ID, Is.EqualTo("model"));
            });
        }

        [Test]
        public void Load_IsAnIModelLoader()
        {
            IModelLoader loader = new ObjLoader();
            Assert.That(loader.Load(Write(TetraGeometry + TetraFaces), false), Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_MissingFile_ReturnsEmpty()
        {
            Assert.That(new ObjLoader().Load(Path.Combine(_tempDir, "nope.obj"), false), Is.Empty);
        }

        [TestCase("")]
        [TestCase("# only a comment\n")]
        [TestCase("v 0 0 0\nv 1 0 0\nv 0 1 0\n")]
        [TestCase("this is not an obj file at all\nneither is this\n")]
        public void Load_FileWithoutTriangles_ReturnsEmpty(string text)
        {
            Assert.That(Load(text), Is.Empty);
        }

        [Test]
        public void Load_BinaryGarbage_ReturnsEmpty()
        {
            var bytes = new byte[4096];
            new Random(7).NextBytes(bytes);
            var path = Path.Combine(_tempDir, "garbage.obj");
            File.WriteAllBytes(path, bytes);

            Assert.That(new ObjLoader().Load(path, false), Is.Empty);
        }

        [Test]
        public void Load_TextFormattingVariations_AreAllAccepted()
        {
            // CRLF, bare CR, tabs, runs of spaces, comments, a UTF-8 BOM, vertex colors and a w component
            var text = "﻿# a comment\r\nv\t0   0 0 1\r\nv 1 0 0 1 0.5 0.5 0.5\rv 1 1 0 # trailing comment\n" +
                       "v 0 1 0\n\n   \nf 1 2 3 \\\n 4\n";
            var path = Path.Combine(_tempDir, "variations.obj");
            File.WriteAllText(path, text, new UTF8Encoding(true));

            var prims = new ObjLoader().Load(path, false);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(2));
        }

        [Test]
        public void Load_ExponentNumbers_AreAccepted()
        {
            var prims = Load("v 0 0 0\nv 1E0 0 0\nv 0 1e0 0\nf 1 2 3\n");
            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_WithADecimalCommaCulture_StillReadsDecimalPoints()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var prims = Load("v 0 0 0\nv 0.5 0 0\nv 0 2.5 0\nf 1 2 3\n");

                Assert.That(prims, Has.Count.EqualTo(1));
                // Y-up input: the 2.5 along Y ends up along Z
                Assert.That(prims[0].Scale.Z, Is.EqualTo(2.5f).Within(1e-5f));
                Assert.That(prims[0].Scale.X, Is.EqualTo(0.5f).Within(1e-5f));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        #endregion Loading

        #region Faces

        [Test]
        public void Load_Quad_GivesTwoTriangles()
        {
            var prims = Load(Quad());
            Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(2));
        }

        [Test]
        public void Load_Pentagon_IsFannedIntoThreeTriangles()
        {
            var prims = Load("v 0 0 0\nv 2 0 0\nv 3 1 0\nv 1 2 0\nv -1 1 0\nf 1 2 3 4 5\n");

            var face = prims[0].Faces[0];
            Assert.Multiple(() =>
            {
                Assert.That(TriangleCount(face), Is.EqualTo(3));
                // every triangle in the fan starts at the first corner, so the first vertex is shared
                Assert.That(face.Indices[0], Is.EqualTo(face.Indices[3]));
                Assert.That(face.Indices[0], Is.EqualTo(face.Indices[6]));
            });
        }

        [Test]
        public void Load_FaceWithTwoCorners_IsIgnoredButTheRestLoads()
        {
            var prims = Load(Quad("f 1 2\nf 1 2 3"));

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(1));
        }

        [Test]
        public void Load_NegativeIndices_GiveTheSamePrimAsPositiveOnes()
        {
            var positive = Load(TetraGeometry + TetraFaces)[0];

            // Written after the 4 positions, 4 texture coordinates and 4 normals, so -4 is the first of each.
            // The negative forms are relative to what has been read at that point in the file.
            var negative = Load(TetraGeometry +
                "f -4/-4/-4 -2/-2/-2 -3/-3/-3\nf -4/-4/-4 -3/-3/-3 -1/-1/-1\n" +
                "f -3/-3/-3 -2/-2/-2 -1/-1/-1\nf -2/-2/-2 -4/-4/-4 -1/-1/-1\n")[0];

            // The asset embeds a timestamp, so compare what it is built from
            Assert.That(negative.Faces[0].Indices, Is.EqualTo(positive.Faces[0].Indices));
            Assert.That(negative.Faces[0].Vertices, Is.EqualTo(positive.Faces[0].Vertices));
        }

        [Test]
        public void Load_NegativeIndices_CountBackFromWhatHasBeenReadSoFar()
        {
            // Two quads. The second quad's -4..-1 must mean its own four positions, not the file's last four
            var text = "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf -4 -3 -2 -1\n" +
                       "v 0 0 5\nv 1 0 5\nv 1 1 5\nv 0 1 5\nf -4 -3 -2 -1\n";

            var prims = Load(text);

            // Y-up input, so the Z offset of 5 ends up along -Y. The prim spans both quads either way.
            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Vertices, Has.Count.EqualTo(8));
        }

        [Test]
        public void Load_CornerFormats_AreAllAccepted()
        {
            var geometry = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\n";
            foreach (var corners in new[] { "1 2 3", "1/1 2/2 3/3", "1//1 2//1 3//1", "1/1/1 2/2/1 3/3/1" })
            {
                var prims = Load(geometry + "f " + corners + "\n");
                Assert.That(prims, Has.Count.EqualTo(1), corners);
            }
        }

        [Test]
        public void Load_PointsLinesAndCurves_AreIgnored()
        {
            var text = Quad("f 1 2 3\np 1\nl 1 2 3\ncurv 0 1 1 2\ns off\nvp 0.5\nunknownstatement 1 2 3");
            var prims = Load(text);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(1));
        }

        #endregion Faces

        #region Conventions

        [Test]
        public void Load_Tetrahedron_MatchesColladaLoader()
        {
            // The same mesh in both formats must give the same prim: same axis conversion, same fit into the
            // unit cube, same vertices and normals, and the same UVs -- OBJ and Collada both put V=0 at the bottom
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
            var daePath = Path.Combine(_tempDir, "tetra.dae");
            File.WriteAllText(daePath, dae);

            var expected = new ColladaLoader().Load(daePath, loadImages: false);
            var actual = Load(TetraGeometry + TetraFaces);

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
                Assert.That(a.BoundMin.Z, Is.EqualTo(e.BoundMin.Z).Within(tol));
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
        public void Load_YUpModel_HasItsHeightAlongZ()
        {
            // 1 wide (X), 2 tall (Y), 3 deep (Z)
            var prims = Load("v 0 0 0\nv 1 0 0\nv 1 2 0\nv 0 2 3\nf 1 2 3 4\n");

            var prim = prims[0];
            Assert.Multiple(() =>
            {
                Assert.That(prim.Scale.X, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(prim.Scale.Z, Is.EqualTo(2f).Within(1e-5f), "Y became Z");
                Assert.That(prim.Scale.Y, Is.EqualTo(3f).Within(1e-5f), "Z became Y");
                Assert.That(prim.Position.Z, Is.EqualTo(1f).Within(1e-5f), "centred on the middle of the height");
            });
        }

        [Test]
        public void Load_ZUpModel_IsLeftAlone()
        {
            var prims = new ObjLoader { YUp = false }.Load(
                Write("v 0 0 0\nv 1 0 0\nv 1 2 0\nv 0 2 3\nf 1 2 3 4\n"), false);

            Assert.Multiple(() =>
            {
                Assert.That(prims[0].Scale.X, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(prims[0].Scale.Y, Is.EqualTo(2f).Within(1e-5f));
                Assert.That(prims[0].Scale.Z, Is.EqualTo(3f).Within(1e-5f));
            });
        }

        [Test]
        public void Load_RotatingAxes_DoesNotFlipWinding()
        {
            // The triangle's geometric normal is +Y, and so is its stored normal. Both are rotated, but
            // one as a position and one as a direction, and if the rotation also mirrored the winding
            // the surface would end up facing the wrong way.
            var text = "v 0 0 0\nv 1 0 0\nv 0 0 -1\nvn 0 1 0\nf 1//1 2//1 3//1\n";

            var vertices = Load(text)[0].Faces[0].Vertices;

            // positions come back fitted into the unit cube, so rebuild the geometric normal from them
            var p0 = vertices[0].Position;
            var p1 = vertices[1].Position;
            var p2 = vertices[2].Position;
            var geometric = Vector3.Cross(p1 - p0, p2 - p0);
            Assert.That(Vector3.Dot(geometric, vertices[0].Normal), Is.GreaterThan(0f));
        }

        [Test]
        public void Load_Normals_AreRotatedAndNormalized()
        {
            var text = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 4 0\nf 1//1 2//1 3//1\n";

            var normal = Load(text)[0].Faces[0].Vertices[0].Normal;

            Assert.Multiple(() =>
            {
                Assert.That(normal.X, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(normal.Y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(normal.Z, Is.EqualTo(1f).Within(1e-5f), "+Y became +Z and has length one");
            });
        }

        [Test]
        public void Load_UnevenSize_StoresNormalsScaledByTheSizeSoTheyDrawTrue()
        {
            // The mesh is 10 x 2 x 1. The prim is drawn at that scale, and a normal follows the inverse
            // transpose of it, so the stored normal has to be multiplied by the size to come out unchanged.
            var text = "v 0 0 0\nv 10 0 0\nv 0 2 0\nv 0 0 1\nvn 1 1 1\nf 1//1 2//1 3//1\nf 1//1 3//1 4//1\n";

            var prim = new ObjLoader { YUp = false }.Load(Write(text), false)[0];

            var expected = Vector3.Normalize(new Vector3(10f, 2f, 1f));
            var stored = prim.Faces[0].Vertices[0].Normal;
            var drawn = Vector3.Normalize(new Vector3(stored.X / prim.Scale.X, stored.Y / prim.Scale.Y, stored.Z / prim.Scale.Z));
            var original = Vector3.Normalize(new Vector3(1f, 1f, 1f));
            Assert.Multiple(() =>
            {
                Assert.That(prim.Scale.X, Is.EqualTo(10f).Within(1e-5f));
                Assert.That(stored.Length(), Is.EqualTo(1f).Within(1e-5f));
                Assert.That(stored.X, Is.EqualTo(expected.X).Within(1e-5f));
                Assert.That(stored.Y, Is.EqualTo(expected.Y).Within(1e-5f));
                Assert.That(stored.Z, Is.EqualTo(expected.Z).Within(1e-5f));
                Assert.That(drawn.X, Is.EqualTo(original.X).Within(1e-5f));
                Assert.That(drawn.Y, Is.EqualTo(original.Y).Within(1e-5f));
                Assert.That(drawn.Z, Is.EqualTo(original.Z).Within(1e-5f));
            });
        }

        [Test]
        public void Load_FlatMesh_KeepsTheNormalOfThePlane()
        {
            // No thickness along Z: a size of zero must not scale the plane's own normal away
            var text = "v 0 0 0\nv 10 0 0\nv 0 2 0\nvn 0 0 1\nf 1//1 2//1 3//1\n";

            var stored = new ObjLoader { YUp = false }.Load(Write(text), false)[0].Faces[0].Vertices[0].Normal;

            Assert.Multiple(() =>
            {
                Assert.That(stored.X, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(stored.Y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(stored.Z, Is.EqualTo(1f).Within(1e-5f));
            });
        }

        [Test]
        public void Load_MissingNormals_AreShadedFlat()
        {
            // Z-up, counter-clockwise seen from +Z, so the flat normal is +Z
            var prims = new ObjLoader { YUp = false }.Load(Write("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"), false);

            foreach (var v in prims[0].Faces[0].Vertices)
            {
                Assert.That(v.Normal.Z, Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void Load_TexCoords_KeepTheirV()
        {
            var text = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0.25 0.75\nvt 1 0\nvt 0 1\nf 1/1 2/2 3/3\n";

            var uv = Load(text)[0].Faces[0].Vertices[0].TexCoord;

            Assert.Multiple(() =>
            {
                Assert.That(uv.X, Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(uv.Y, Is.EqualTo(0.75f).Within(1e-6f));
            });
        }

        [Test]
        public void Load_TexCoordWithOnlyU_HasZeroV()
        {
            var text = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0.5\nf 1/1 2/1 3/1\n";
            Assert.That(Load(text)[0].Faces[0].Vertices[0].TexCoord.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Load_FlatMesh_HasAZeroScaleAxisNotAnException()
        {
            var prims = new ObjLoader { YUp = false }.Load(Write(Quad()), false);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Scale.Z, Is.EqualTo(0f));
        }

        #endregion Conventions

        #region Objects

        private const string TwoObjects =
            "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 10 10 10\nv 11 10 10\nv 10 11 10\n" +
            "o First\nf 1 2 3\n" +
            "o Second\nf 4 5 6\n";

        [Test]
        public void Load_Objects_BecomeSeparatePrims()
        {
            var prims = Load(TwoObjects);

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(prims[0].ID, Is.EqualTo("First"));
                Assert.That(prims[1].ID, Is.EqualTo("Second"));
            });
        }

        [Test]
        public void Load_Objects_AreFittedToTheVerticesTheyUse()
        {
            // Vertices are numbered for the whole file; the second object's bounds must not include the first's
            var prims = Load(TwoObjects);

            Assert.Multiple(() =>
            {
                Assert.That(prims[0].Scale.X, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(prims[1].Scale.X, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(prims[1].BoundMin.X, Is.EqualTo(10f).Within(1e-5f));
                Assert.That(prims[1].BoundMax.X, Is.EqualTo(11f).Within(1e-5f));
            });
        }

        [Test]
        public void Load_Objects_CanReuseVerticesOfEarlierOnes()
        {
            var text = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\no A\nf 1 2 3\no B\nf 1 3 4\n";

            var prims = Load(text);

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.That(prims[1].Faces[0].Vertices, Has.Count.EqualTo(3));
        }

        [Test]
        public void Load_GroupsInAFileWithoutObjects_BecomeSeparatePrims()
        {
            var prims = Load(TwoObjects.Replace("o First", "g First").Replace("o Second", "g Second"));

            Assert.That(prims, Has.Count.EqualTo(2));
        }

        [Test]
        public void Load_GroupsInsideObjects_StayPartOfTheirObject()
        {
            var text = TwoObjects.Replace("f 1 2 3", "g left\nf 1 2 3\ng right\nf 1 3 2");

            var prims = Load(text);

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.That(TriangleCount(prims[0].Faces[0]), Is.EqualTo(2));
        }

        [Test]
        public void Load_UnnamedParts_FallBackToTheFileName()
        {
            var prims = Load("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\no\nf 1 3 2\n");

            Assert.That(prims, Has.Count.EqualTo(2));
            Assert.That(prims[0].ID, Is.Not.EqualTo(prims[1].ID));
            Assert.That(prims[0].ID, Does.StartWith("model"));
        }

        #endregion Objects

        #region Materials

        private const string RedAndBlue =
            "newmtl Red\nKd 1 0 0\nnewmtl Blue\nKd 0 0 1\nd 0.5\n";

        [Test]
        public void Load_Materials_BecomeFacesInOrderOfFirstUse()
        {
            File.WriteAllText(Path.Combine(_tempDir, "colors.mtl"), RedAndBlue);
            var text = "mtllib colors.mtl\n" + Quad("usemtl Blue\nf 1 2 3\nusemtl Red\nf 1 3 4\nusemtl Blue\nf 1 2 4");

            var prims = Load(text);

            var faces = prims[0].Faces;
            Assert.Multiple(() =>
            {
                Assert.That(faces, Has.Count.EqualTo(2), "repeated uses of a material share a face");
                Assert.That(faces[0].MaterialID, Is.EqualTo("Blue"));
                Assert.That(TriangleCount(faces[0]), Is.EqualTo(2));
                Assert.That(faces[0].Material.DiffuseColor, Is.EqualTo(new Color4(0f, 0f, 1f, 0.5f)));
                Assert.That(faces[1].MaterialID, Is.EqualTo("Red"));
                Assert.That(faces[1].Material.DiffuseColor, Is.EqualTo(new Color4(1f, 0f, 0f, 1f)));
            });
        }

        [Test]
        public void Load_TransparencyKeyword_IsTheInverseOfD()
        {
            File.WriteAllText(Path.Combine(_tempDir, "t.mtl"), "newmtl Glass\nKd 1 1 1\nTr 0.75\n");

            var prims = Load("mtllib t.mtl\n" + Quad("usemtl Glass\nf 1 2 3"));

            Assert.That(prims[0].Faces[0].Material.DiffuseColor.A, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void Load_ColorsOutsideZeroToOne_AreClamped()
        {
            File.WriteAllText(Path.Combine(_tempDir, "c.mtl"), "newmtl Hot\nKd 2 -1 0.5\n");

            var color = Load("mtllib c.mtl\n" + Quad("usemtl Hot\nf 1 2 3"))[0].Faces[0].Material.DiffuseColor;

            Assert.That(color, Is.EqualTo(new Color4(1f, 0f, 0.5f, 1f)));
        }

        [Test]
        public void Load_FacesBeforeAnyUsemtl_GetTheDefaultMaterial()
        {
            var prims = Load(Quad("f 1 2 3"));

            Assert.That(prims[0].Faces[0].Material.DiffuseColor, Is.EqualTo(Color4.White));
        }

        [Test]
        public void Load_MaterialNobodyDefines_IsWhiteAndLoadsAnyway()
        {
            var prims = Load(Quad("usemtl Ghost\nf 1 2 3"));

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].MaterialID, Is.EqualTo("Ghost"));
            Assert.That(prims[0].Faces[0].Material.DiffuseColor, Is.EqualTo(Color4.White));
        }

        [Test]
        public void Load_MissingMaterialLibrary_CostsTheMaterialsNotTheMesh()
        {
            var prims = Load("mtllib gone.mtl\n" + Quad("usemtl Red\nf 1 2 3"));

            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_SeveralLibrariesOnOneLine_AreAllRead()
        {
            File.WriteAllText(Path.Combine(_tempDir, "a.mtl"), "newmtl A\nKd 1 0 0\n");
            File.WriteAllText(Path.Combine(_tempDir, "b.mtl"), "newmtl B\nKd 0 1 0\n");

            var prims = Load("mtllib a.mtl b.mtl\n" + Quad("usemtl A\nf 1 2 3\nusemtl B\nf 1 3 4"));

            Assert.Multiple(() =>
            {
                Assert.That(prims[0].Faces[0].Material.DiffuseColor.R, Is.EqualTo(1f));
                Assert.That(prims[0].Faces[1].Material.DiffuseColor.G, Is.EqualTo(1f));
            });
        }

        [Test]
        public void Load_LibraryNameWithASpace_IsOneFile()
        {
            File.WriteAllText(Path.Combine(_tempDir, "my colors.mtl"), RedAndBlue);

            var prims = Load("mtllib my colors.mtl\n" + Quad("usemtl Red\nf 1 2 3"));

            Assert.That(prims[0].Faces[0].Material.DiffuseColor.R, Is.EqualTo(1f));
        }

        [Test]
        public void Load_MaterialNameWithASpace_IsMatched()
        {
            File.WriteAllText(Path.Combine(_tempDir, "s.mtl"), "newmtl Dark Red\nKd 0.5 0 0\n");

            var prims = Load("mtllib s.mtl\n" + Quad("usemtl Dark Red\nf 1 2 3"));

            Assert.That(prims[0].Faces[0].Material.DiffuseColor.R, Is.EqualTo(0.5f));
        }

        [Test]
        public void Load_MalformedMaterialLine_IsSkipped()
        {
            File.WriteAllText(Path.Combine(_tempDir, "m.mtl"), "newmtl M\nKd banana 0 0\nKd 0 1 0\n");

            var prims = Load("mtllib m.mtl\n" + Quad("usemtl M\nf 1 2 3"));

            Assert.That(prims[0].Faces[0].Material.DiffuseColor.G, Is.EqualTo(1f));
        }

        [Test]
        public void Load_MaterialLibraryOutsideTheModelDirectory_IsNotRead()
        {
            var outside = Path.Combine(Path.GetTempPath(), "lm-obj-outside-" + Guid.NewGuid() + ".mtl");
            File.WriteAllText(outside, "newmtl Red\nKd 1 0 0\n");
            try
            {
                var text = Quad("usemtl Red\nf 1 2 3");
                var relative = "../" + Path.GetFileName(outside);

                var restricted = Load("mtllib " + relative + "\n" + text);
                var open = new ObjLoader { RestrictTexturesToModelDirectory = false }
                    .Load(Write("mtllib " + relative + "\n" + text), false);

                Assert.Multiple(() =>
                {
                    Assert.That(restricted, Has.Count.EqualTo(1));
                    Assert.That(restricted[0].Faces[0].Material.DiffuseColor, Is.EqualTo(Color4.White), "not read");
                    Assert.That(open[0].Faces[0].Material.DiffuseColor.G, Is.EqualTo(0f), "the control does read it");
                    Assert.That(open[0].Faces[0].Material.DiffuseColor.R, Is.EqualTo(1f));
                });
            }
            finally
            {
                File.Delete(outside);
            }
        }

        [Test]
        public void Load_MaterialLibraryNamedByAUri_IsNotRead()
        {
            var prims = Load("mtllib file:///etc/passwd\nmtllib http://example.com/x.mtl\n" + Quad("f 1 2 3"));

            Assert.That(prims, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_OversizedMaterialLibrary_CostsTheMaterialsNotTheMesh()
        {
            File.WriteAllText(Path.Combine(_tempDir, "r.mtl"), RedAndBlue);
            var text = "mtllib r.mtl\n" + Quad("usemtl Red\nf 1 2 3");

            var small = new ObjLoader { MaxFileSize = 4096 }.Load(Write(text), false);
            File.WriteAllText(Path.Combine(_tempDir, "r.mtl"), RedAndBlue + new string('#', 8192));
            var big = new ObjLoader { MaxFileSize = 4096 }.Load(Write(text), false);

            Assert.Multiple(() =>
            {
                Assert.That(small[0].Faces[0].Material.DiffuseColor.R, Is.EqualTo(1f), "control reads it");
                Assert.That(big, Has.Count.EqualTo(1));
                Assert.That(big[0].Faces[0].Material.DiffuseColor, Is.EqualTo(Color4.White), "too big to read");
            });
        }

        #endregion Materials

        #region Textures

        private string TexturedModel(string mapLine)
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "tex.tga"), TgaBytes());
            File.WriteAllText(Path.Combine(_tempDir, "t.mtl"), "newmtl Textured\nKd 1 1 1\n" + mapLine + "\n");
            return "mtllib t.mtl\n" + Quad("usemtl Textured\nf 1 2 3");
        }

        [Test]
        public void Load_DiffuseTexture_IsDecodedAndEncoded()
        {
            var prims = Load(TexturedModel("map_Kd tex.tga"), loadImages: true);

            var material = prims[0].Faces[0].Material;
            Assert.Multiple(() =>
            {
                Assert.That(material.Texture, Is.EqualTo("tex.tga"));
                Assert.That(material.TextureData, Is.Not.Empty);
                Assert.That(material.Width, Is.EqualTo(64));
                Assert.That(material.Height, Is.EqualTo(32));
            });
        }

        [Test]
        public void Load_DiffuseTexture_IsNotDecodedWhenImagesAreNotWanted()
        {
            var material = Load(TexturedModel("map_Kd tex.tga"), loadImages: false)[0].Faces[0].Material;

            Assert.That(material.Texture, Is.EqualTo("tex.tga"));
            Assert.That(material.TextureData, Is.Empty);
        }

        [TestCase("map_Kd -s 1 1 1 tex.tga")]
        [TestCase("map_Kd -o 0.5 0.5 -s 2 2 tex.tga")]
        [TestCase("map_Kd -mm 0 1 -clamp on -blendu off tex.tga")]
        [TestCase("map_Kd -bm 1.5 -imfchan r tex.tga")]
        [TestCase("MAP_KD tex.tga")]
        public void Load_TextureOptions_AreSkippedToFindTheFileName(string mapLine)
        {
            var material = Load(TexturedModel(mapLine), loadImages: true)[0].Faces[0].Material;

            Assert.That(material.Texture, Is.EqualTo("tex.tga"));
            Assert.That(material.TextureData, Is.Not.Empty);
        }

        [Test]
        public void Load_TextureInASubdirectoryWithBackslashes_IsFound()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "maps"));
            File.WriteAllBytes(Path.Combine(_tempDir, "maps", "wood.tga"), TgaBytes());
            File.WriteAllText(Path.Combine(_tempDir, "t.mtl"), "newmtl Wood\nmap_Kd maps\\wood.tga\n");

            var material = Load("mtllib t.mtl\n" + Quad("usemtl Wood\nf 1 2 3"), loadImages: true)[0].Faces[0].Material;

            Assert.That(material.Texture, Is.EqualTo("maps/wood.tga"));
            Assert.That(material.TextureData, Is.Not.Empty);
        }

        [Test]
        public void Load_TextureOutsideTheModelDirectory_IsNotRead()
        {
            var outside = Path.Combine(Path.GetTempPath(), "lm-obj-outside-" + Guid.NewGuid() + ".tga");
            File.WriteAllBytes(outside, TgaBytes());
            try
            {
                var text = TexturedModel("map_Kd ../" + Path.GetFileName(outside));

                var restricted = Load(text, loadImages: true);
                var open = new ObjLoader { RestrictTexturesToModelDirectory = false }.Load(Write(text), true);

                Assert.Multiple(() =>
                {
                    Assert.That(restricted, Has.Count.EqualTo(1));
                    Assert.That(restricted[0].Faces[0].Material.TextureData, Is.Empty);
                    Assert.That(open[0].Faces[0].Material.TextureData, Is.Not.Empty, "the control does read it");
                });
            }
            finally
            {
                File.Delete(outside);
            }
        }

        [TestCase("map_Kd http://example.com/tex.tga")]
        [TestCase("map_Kd file:///tmp/tex.tga")]
        [TestCase("map_Kd missing.tga")]
        public void Load_TextureThatCannotBeLoaded_CostsTheTextureNotTheMesh(string mapLine)
        {
            var prims = Load(TexturedModel(mapLine), loadImages: true);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Material.TextureData, Is.Empty);
        }

        [Test]
        public void Load_PngWithoutACodec_CostsTheTextureNotTheMesh()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "tex.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            var prims = Load(TexturedModel("map_Kd tex.png"), loadImages: true);

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Material.TextureData, Is.Empty);
        }

        [Test]
        public void Load_MaterialsSharingATexture_ShareTheEncodedData()
        {
            File.WriteAllBytes(Path.Combine(_tempDir, "tex.tga"), TgaBytes());
            File.WriteAllText(Path.Combine(_tempDir, "t.mtl"),
                "newmtl One\nmap_Kd tex.tga\nnewmtl Two\nmap_Kd tex.tga\n");

            var prims = Load("mtllib t.mtl\n" + Quad("usemtl One\nf 1 2 3\nusemtl Two\nf 1 3 4"), loadImages: true);

            var faces = prims[0].Faces;
            Assert.Multiple(() =>
            {
                Assert.That(faces[0].Material.TextureData, Is.Not.Empty);
                Assert.That(faces[1].Material.TextureData, Is.SameAs(faces[0].Material.TextureData));
                Assert.That(faces[1].Material.Width, Is.EqualTo(64));
            });
        }

        #endregion Textures

        #region Hostile input

        private void AssertRefusedBecauseOf(string hostile, string control, ObjLoader? loader = null)
        {
            loader ??= new ObjLoader();
            Assert.That(loader.Load(Write(control, "control.obj"), false), Has.Count.EqualTo(1),
                "the control must load, or this test proves nothing");
            Assert.That(loader.Load(Write(hostile, "hostile.obj"), false), Is.Empty);
        }

        private const string Triangle = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\n";

        [TestCase("f 0 1 2")]
        [TestCase("f 1 2 4")]
        [TestCase("f 1 2 -4")]
        [TestCase("f 1 2 x")]
        [TestCase("f 1 2 3.5")]
        [TestCase("f 1 2 99999999999")]
        [TestCase("f 1 2 -2147483648")]
        public void Load_BadPositionIndex_IsRefused(string face)
        {
            AssertRefusedBecauseOf(Triangle + face + "\n", Triangle + "f 1 2 3\n");
        }

        [TestCase("f 1/2 2/2 3/4")]
        [TestCase("f 1/0 2/1 3/1")]
        [TestCase("f 1/-4 2/1 3/1")]
        [TestCase("f 1//2 2//1 3//1")]
        [TestCase("f 1//0 2//1 3//1")]
        [TestCase("f 1/1/1/1 2/2/1 3/3/1")]
        public void Load_BadTexCoordOrNormalIndex_IsRefused(string face)
        {
            AssertRefusedBecauseOf(Triangle + face + "\n", Triangle + "f 1/1/1 2/2/1 3/3/1\n");
        }

        [Test]
        public void Load_NormalIndexWhenThereAreNoNormals_IsRefused()
        {
            AssertRefusedBecauseOf("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1//1 2//1 3//1\n", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        }

        [TestCase("v NaN 0 0")]
        [TestCase("v Infinity 0 0")]
        [TestCase("v -Infinity 0 0")]
        [TestCase("v 1e999 0 0")]
        [TestCase("v banana 0 0")]
        [TestCase("v 0 0")]
        [TestCase("vt 0 nan")]
        [TestCase("vn 0 0")]
        [TestCase("vn 0 0 inf")]
        public void Load_BadNumbers_AreRefused(string line)
        {
            AssertRefusedBecauseOf(Triangle + line + "\nf 1 2 3\n", Triangle + "f 1 2 3\n");
        }

        [Test]
        public void Load_FileLargerThanTheLimit_IsRefused()
        {
            var text = Triangle + "f 1 2 3\n";
            var loader = new ObjLoader { MaxFileSize = text.Length + 10 };

            AssertRefusedBecauseOf(text + new string('#', 100), text, loader);
        }

        [Test]
        public void Load_MoreVerticesThanTheLimit_IsRefused()
        {
            var loader = new ObjLoader { MaxVertices = 20 };
            var sb = new StringBuilder("v 0 0 0\nv 1 0 0\nv 0 1 0\n");
            for (int i = 0; i < 20; i++) sb.Append("v 2 2 2\n");

            AssertRefusedBecauseOf(sb + "f 1 2 3\n", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n", loader);
        }

        [Test]
        public void Load_MoreTriangleCornersThanTheLimit_IsRefused()
        {
            // One face line with 3000 corners is 2998 triangles, 8994 corners, far over the cap
            var loader = new ObjLoader { MaxVertices = 1000 };
            var hostile = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf " + string.Join(" ", new string[3000].Select((_, i) => (i % 3 + 1).ToString())) + "\n";

            AssertRefusedBecauseOf(hostile, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n", loader);
        }

        [Test]
        public void Load_LineLongerThanTheLimit_IsRefused()
        {
            var control = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n";
            var hostile = control + "# " + new string('x', (1 << 20) + 10) + "\n";

            AssertRefusedBecauseOf(hostile, control);
        }

        [Test]
        public void Load_EndlessLineContinuation_IsRefused()
        {
            var control = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n";
            var hostile = new StringBuilder(control + "# ");
            // each physical line is short, but they are joined into one statement
            for (int i = 0; i < 20000; i++) hostile.Append(new string('y', 80)).Append("\\\n");

            AssertRefusedBecauseOf(hostile.ToString(), control);
        }

        [Test]
        public void Load_ManyMaterials_AreCapped()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 10_050; i++) sb.Append("newmtl m").Append(i).Append("\nKd 1 0 0\n");
            File.WriteAllText(Path.Combine(_tempDir, "many.mtl"), sb.ToString());

            var prims = Load("mtllib many.mtl\n" + Quad("usemtl m5\nf 1 2 3\nusemtl m10040\nf 1 3 4"));

            // The whole library is dropped, not just the excess, so neither material has its color
            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].Material.DiffuseColor, Is.EqualTo(Color4.White));
        }

        [Test]
        public void Load_FaceWithMoreVerticesThanAFaceCanIndex_IsSkipped()
        {
            // Triangles that share no corners: 22000 of them make 66000 distinct vertices in one material
            var sb = new StringBuilder();
            for (int t = 0; t < 22000; t++)
            {
                float x = t * 2f;
                sb.Append($"v {x} 0 0\nv {x + 1} 0 0\nv {x} 1 0\n");
            }
            for (int t = 0; t < 22000; t++) sb.Append($"f {t * 3 + 1} {t * 3 + 2} {t * 3 + 3}\n");

            Assert.That(Load(sb.ToString()), Is.Empty);
        }

        [Test]
        public void Load_OneOversizedMaterial_DoesNotTakeTheOthersWithIt()
        {
            var sb = new StringBuilder();
            for (int t = 0; t < 22000; t++)
            {
                float x = t * 2f;
                sb.Append($"v {x} 0 0\nv {x + 1} 0 0\nv {x} 1 0\n");
            }
            sb.Append("usemtl Huge\n");
            for (int t = 0; t < 22000; t++) sb.Append($"f {t * 3 + 1} {t * 3 + 2} {t * 3 + 3}\n");
            sb.Append("usemtl Small\nf 1 2 3\n");

            var prims = Load(sb.ToString());

            Assert.That(prims, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces, Has.Count.EqualTo(1));
            Assert.That(prims[0].Faces[0].MaterialID, Is.EqualTo("Small"));
        }

        [Test]
        public void Load_MoreThanEightMaterials_StillLoads()
        {
            var sb = new StringBuilder("v 0 0 0\nv 1 0 0\nv 0 1 0\n");
            for (int i = 0; i < 9; i++) sb.Append("usemtl m").Append(i).Append("\nf 1 2 3\n");

            var prims = Load(sb.ToString());

            Assert.That(prims[0].Faces, Has.Count.EqualTo(9));
        }

        #endregion Hostile input
    }
}
