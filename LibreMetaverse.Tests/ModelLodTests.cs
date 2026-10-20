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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.ImportExport;
using LibreMetaverse.Rendering;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The levels of detail and the physics shape made for an upload. Everything that comes out is checked against what went in
    /// and against shapes whose answer is known: a sphere stays closed and keeps its volume, a long thin box keeps its length, a flat
    /// sheet keeps its corners, and the hull of a mesh holds the mesh.
    /// </summary>
    [TestFixture]
    public class ModelLodTests
    {
        #region Builders

        private static ModelFace Face(ModelMaterial? material = null)
        {
            return new ModelFace { MaterialID = "m", Material = material ?? new ModelMaterial { ID = "m" } };
        }

        /// <summary>A sphere of radius 0.5, with a seam down one side (the vertices there are twice, with different texture coordinates)</summary>
        private static ModelFace Sphere(int rings, int sectors, ModelMaterial? material = null)
        {
            var face = Face(material);
            // Rounded, so the two sides of the seam and the points at the poles are the same position exactly and not nearly
            float Round(double v) => (float)(Math.Round(v, 6) + 0.0);
            Vertex At(int i, int j)
            {
                double theta = Math.PI * i / rings, phi = 2 * Math.PI * (j % sectors) / sectors;
                var d = new Vector3(Round(Math.Sin(theta) * Math.Cos(phi)), Round(Math.Sin(theta) * Math.Sin(phi)), Round(Math.Cos(theta)));
                return new Vertex { Position = d * 0.5f, Normal = d, TexCoord = new Vector2((float)j / sectors, (float)i / rings) };
            }

            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sectors; j++)
                {
                    var a = At(i, j); var b = At(i, j + 1); var c = At(i + 1, j); var d = At(i + 1, j + 1);
                    // the poles have a single point, so one of each pair of triangles is a line there
                    if (i != 0) Triangle(face, a, c, b);
                    if (i != rings - 1) Triangle(face, b, c, d);
                }
            }
            return face;
        }

        // A triangle facing away from the middle of the shape, whichever way it was given (or the way its normals face)
        private static void Triangle(ModelFace face, Vertex a, Vertex b, Vertex c, bool centreOf = false)
        {
            var cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            var centroid = (a.Position + b.Position + c.Position) / 3f;
            float facing = centreOf ? Vector3.Dot(cross, a.Normal + b.Normal + c.Normal) : Vector3.Dot(cross, centroid);
            if (facing < 0) { var swap = b; b = c; c = swap; }
            face.AddVertex(a); face.AddVertex(b); face.AddVertex(c);
        }

        /// <summary>A torus (a ring with a hole through it), which a careless simplifier closes up or pinches</summary>
        private static ModelFace Torus(int rings, int sectors)
        {
            var face = Face();
            float Round(double v) => (float)(Math.Round(v, 6) + 0.0);
            Vertex At(int i, int j)
            {
                double u = 2 * Math.PI * (i % rings) / rings, v = 2 * Math.PI * (j % sectors) / sectors;
                double ring = 0.35 + 0.15 * Math.Cos(v);
                var position = new Vector3(Round(ring * Math.Cos(u)), Round(ring * Math.Sin(u)), Round(0.15 * Math.Sin(v)));
                var centre = new Vector3(Round(0.35 * Math.Cos(u)), Round(0.35 * Math.Sin(u)), 0f);
                return new Vertex { Position = position, Normal = Vector3.Normalize(position - centre), TexCoord = new Vector2((float)i / rings, (float)j / sectors) };
            }
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sectors; j++)
                {
                    var a = At(i, j); var b = At(i + 1, j); var c = At(i, j + 1); var d = At(i + 1, j + 1);
                    // outward is away from the ring's own middle
                    Triangle(face, a, b, c, centreOf: true); Triangle(face, b, d, c, centreOf: true);
                }
            }
            return face;
        }

        /// <summary>A sheet with a tall bump in it and a hollow beside it, so there are folds to be made</summary>
        private static ModelFace Bumpy(int n)
        {
            var face = Face();
            Vertex At(int x, int y)
            {
                float fx = (float)x / n - 0.5f, fy = (float)y / n - 0.5f;
                float z = 0.4f * (float)Math.Exp(-40 * ((fx + 0.2) * (fx + 0.2) + fy * fy)) - 0.3f * (float)Math.Exp(-40 * ((fx - 0.2) * (fx - 0.2) + (fy - 0.1) * (fy - 0.1)));
                return new Vertex { Position = new Vector3(fx, fy, z), Normal = Vector3.UnitZ, TexCoord = new Vector2((float)x / n, (float)y / n) };
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    face.AddVertex(At(x, y)); face.AddVertex(At(x + 1, y)); face.AddVertex(At(x, y + 1));
                    face.AddVertex(At(x + 1, y)); face.AddVertex(At(x + 1, y + 1)); face.AddVertex(At(x, y + 1));
                }
            return face;
        }

        /// <summary>A tube open at both ends, so it has two edges to keep</summary>
        private static ModelFace Tube(int rings, int sectors)
        {
            var face = Face();
            float Round(double v) => (float)(Math.Round(v, 6) + 0.0);
            Vertex At(int i, int j)
            {
                double phi = 2 * Math.PI * (j % sectors) / sectors;
                var d = new Vector3(Round(Math.Cos(phi)), Round(Math.Sin(phi)), 0f);
                return new Vertex { Position = new Vector3(d.X * 0.5f, d.Y * 0.5f, (float)i / rings - 0.5f), Normal = d, TexCoord = new Vector2((float)j / sectors, (float)i / rings) };
            }
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < sectors; j++)
                {
                    var a = At(i, j); var b = At(i, j + 1); var c = At(i + 1, j); var d = At(i + 1, j + 1);
                    Triangle(face, a, c, b); Triangle(face, b, c, d);
                }
            }
            return face;
        }

        /// <summary>A flat sheet in the unit square, n by n quads</summary>
        private static ModelFace Sheet(int n)
        {
            var face = Face();
            Vertex At(int x, int y) => new Vertex
            {
                Position = new Vector3((float)x / n - 0.5f, (float)y / n - 0.5f, 0f),
                Normal = Vector3.UnitZ,
                TexCoord = new Vector2((float)x / n, (float)y / n)
            };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    face.AddVertex(At(x, y)); face.AddVertex(At(x + 1, y)); face.AddVertex(At(x, y + 1));
                    face.AddVertex(At(x + 1, y)); face.AddVertex(At(x + 1, y + 1)); face.AddVertex(At(x, y + 1));
                }
            }
            return face;
        }

        /// <summary>A box with n by n quads on each side, one face, hard edges</summary>
        private static ModelFace Box(int n)
        {
            var face = Face();
            // each side: an origin, and the two directions that span it, facing outward
            var sides = new[]
            {
                (new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)),
                (new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0)),
                (new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0)),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1)),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, -1, 0)),
                (new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0))
            };
            foreach (var (origin, u, v, normal) in sides)
            {
                Vertex At(int i, int j) => new Vertex { Position = origin + u * ((float)i / n) + v * ((float)j / n), Normal = normal, TexCoord = new Vector2((float)i / n, (float)j / n) };
                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Triangle(face, At(i, j), At(i + 1, j), At(i, j + 1));
                        Triangle(face, At(i + 1, j), At(i + 1, j + 1), At(i, j + 1));
                    }
                }
            }
            return face;
        }

        private static ModelPrim Prim(Vector3 scale, params ModelFace[] faces)
        {
            var prim = new ModelPrim { ID = "prim", Rotation = Quaternion.Identity, Scale = scale };
            prim.Faces.AddRange(faces);
            return prim;
        }

        private static int Triangles(ModelFace face) => face.Indices.Count / 3;

        // Edges (by position) and how many triangles use each, to tell a closed surface from one with holes or folds
        private static Dictionary<(Vector3, Vector3), int> EdgeUse(ModelFace face)
        {
            var use = new Dictionary<(Vector3, Vector3), int>();
            for (int t = 0; t < Triangles(face); t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    var a = face.Vertices[(int)face.Indices[t * 3 + e]].Position;
                    var b = face.Vertices[(int)face.Indices[t * 3 + (e + 1) % 3]].Position;
                    var key = a.X < b.X || (a.X == b.X && (a.Y < b.Y || (a.Y == b.Y && a.Z <= b.Z))) ? (a, b) : (b, a);
                    use.TryGetValue(key, out int count);
                    use[key] = count + 1;
                }
            }
            return use;
        }

        // Counts of what a surface is made of, by position: corners, edges and triangles
        private static (int V, int E, int F) Counts(ModelFace face)
        {
            var edges = EdgeUse(face);
            return (face.Vertices.Select(v => v.Position).Distinct().Count(), edges.Count, Triangles(face));
        }

        // What any simplified surface must still be: no edge shared by more than two triangles, every pair of triangles
        // that share an edge going round it in opposite directions (so no triangle is turned over), no triangle with a
        // repeated corner or no area, and no triangle twice
        private static void AssertSane(ModelFace face, string what)
        {
            var directed = new Dictionary<(Vector3, Vector3), int>();
            var seen = new HashSet<(Vector3, Vector3, Vector3)>();
            for (int t = 0; t < Triangles(face); t++)
            {
                var p = new[]
                {
                    face.Vertices[(int)face.Indices[t * 3]].Position, face.Vertices[(int)face.Indices[t * 3 + 1]].Position,
                    face.Vertices[(int)face.Indices[t * 3 + 2]].Position
                };
                Assert.That(p[0] != p[1] && p[1] != p[2] && p[0] != p[2], Is.True, $"{what}: triangle {t} has a repeated corner");
                Assert.That(Vector3.Cross(p[1] - p[0], p[2] - p[0]).Length(), Is.GreaterThan(0f), $"{what}: triangle {t} has no area");
                var sorted = p.OrderBy(v => v.X).ThenBy(v => v.Y).ThenBy(v => v.Z).ToArray();
                Assert.That(seen.Add((sorted[0], sorted[1], sorted[2])), Is.True, $"{what}: triangle {t} is there twice");
                for (int e = 0; e < 3; e++)
                {
                    var key = (p[e], p[(e + 1) % 3]);
                    directed.TryGetValue(key, out int count);
                    directed[key] = count + 1;
                }
            }
            foreach (var edge in directed)
            {
                Assert.That(edge.Value, Is.EqualTo(1), $"{what}: an edge is walked the same way twice");
            }
            foreach (var edge in EdgeUse(face))
            {
                Assert.That(edge.Value, Is.LessThanOrEqualTo(2), $"{what}: an edge has more than two triangles");
            }
        }

        // The volume a closed surface holds, from the signed volumes of the tetrahedra its triangles make with the origin
        private static double Volume(ModelFace face)
        {
            double volume = 0;
            for (int t = 0; t < Triangles(face); t++)
            {
                var a = face.Vertices[(int)face.Indices[t * 3]].Position;
                var b = face.Vertices[(int)face.Indices[t * 3 + 1]].Position;
                var c = face.Vertices[(int)face.Indices[t * 3 + 2]].Position;
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }
            return volume;
        }

        private static List<ModelFace>[] Levels(ModelPrim prim) => ModelLodGenerator.Generate(prim, 3f, CancellationToken.None);

        #endregion Builders

        #region Each level of detail

        [Test]
        public void Levels_FallToAThirdAndANinthAndATwentySeventh_OfTheTriangles()
        {
            var prim = Prim(Vector3.One, Sphere(24, 48));
            int high = Triangles(prim.Faces[0]);

            var levels = Levels(prim);

            TestContext.Out.WriteLine($"triangles: {high} -> {Triangles(levels[0][0])}, {Triangles(levels[1][0])}, {Triangles(levels[2][0])}");
            Assert.Multiple(() =>
            {
                Assert.That(Triangles(levels[0][0]), Is.LessThanOrEqualTo(high / 3));
                Assert.That(Triangles(levels[1][0]), Is.LessThanOrEqualTo(high / 9));
                Assert.That(Triangles(levels[2][0]), Is.LessThanOrEqualTo(high / 27));
                Assert.That(Triangles(levels[2][0]), Is.GreaterThanOrEqualTo(1));
            });
        }

        [Test]
        public void Levels_NeverGrow_AndAreMadeOfTheFacesVertices()
        {
            var prim = Prim(Vector3.One, Sphere(16, 32), Box(4));
            var levels = Levels(prim);

            for (int i = 0; i < prim.Faces.Count; i++)
            {
                var above = prim.Faces[i];
                foreach (var level in levels)
                {
                    var face = level[i];
                    Assert.That(Triangles(face), Is.LessThanOrEqualTo(Triangles(above)));
                    Assert.That(face.Vertices.Count, Is.LessThanOrEqualTo(above.Vertices.Count));
                    Assert.That(face.Vertices.All(v => above.Vertices.Contains(v)), Is.True, "only vertices of the level above, with their own texture coordinates and normals");
                    above = face;
                }
            }
        }

        [Test]
        public void Levels_KeepTheFacesTheirOrderAndTheirMaterials()
        {
            var red = new ModelMaterial { ID = "red" };
            var blue = new ModelMaterial { ID = "blue" };
            var prim = Prim(Vector3.One, Sphere(8, 16, red), Sheet(6), Sphere(8, 16, blue));
            prim.Faces[1].Material = blue;

            foreach (var level in Levels(prim))
            {
                Assert.That(level.Select(f => f.Material), Is.EqualTo(prim.Faces.Select(f => f.Material)));
                Assert.That(level.Select(f => f.MaterialID), Is.EqualTo(prim.Faces.Select(f => f.MaterialID)));
            }
        }

        [Test]
        public void Levels_FaceOfOneTriangle_KeepsIt()
        {
            var one = Face();
            one.AddVertex(new Vertex { Position = new Vector3(0, 0, 0), Normal = Vector3.UnitZ });
            one.AddVertex(new Vertex { Position = new Vector3(0.5f, 0, 0), Normal = Vector3.UnitZ });
            one.AddVertex(new Vertex { Position = new Vector3(0, 0.5f, 0), Normal = Vector3.UnitZ });
            var prim = Prim(Vector3.One, one, Sphere(8, 16));

            foreach (var level in Levels(prim))
            {
                Assert.That(Triangles(level[0]), Is.EqualTo(1));
                Assert.That(Triangles(level[1]), Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public void Levels_FaceOfTwoTriangles_KeepsAtLeastOne()
        {
            var prim = Prim(Vector3.One, Sheet(1));
            Assert.That(Triangles(prim.Faces[0]), Is.EqualTo(2));

            foreach (var level in Levels(prim))
            {
                Assert.That(Triangles(level[0]), Is.InRange(1, 2));
                Assert.That(ModelUploadChecks.Check(new[] { Prim(Vector3.One, level[0]) }), Is.Empty);
            }
        }

        [Test]
        public void Levels_PassTheUploadChecks()
        {
            var prim = Prim(Vector3.One, Sphere(16, 32), Box(4), Sheet(8));
            foreach (var level in Levels(prim))
            {
                var made = Prim(Vector3.One, level.ToArray());
                Assert.That(ModelUploadChecks.Check(new[] { made }), Is.Empty);
            }
        }

        [Test]
        public void Levels_AreTheSameEveryTime()
        {
            var prim = Prim(Vector3.One, Sphere(16, 32));

            var first = Levels(prim);
            var second = Levels(prim);

            for (int level = 0; level < 3; level++)
            {
                Assert.That(second[level][0].Indices, Is.EqualTo(first[level][0].Indices));
                Assert.That(second[level][0].Vertices, Is.EqualTo(first[level][0].Vertices));
            }
        }

        [Test]
        public void Levels_OfARiggedFace_KeepTheWeightsOfEachVertexThatIsLeft()
        {
            var sphere = Sphere(12, 24);
            var weighted = Face();
            var weightOf = new Dictionary<Vertex, VertexWeight>();
            for (int t = 0; t < Triangles(sphere); t++)
            {
                for (int c = 0; c < 3; c++)
                {
                    var v = sphere.Vertices[(int)sphere.Indices[t * 3 + c]];
                    // the joint the vertex follows depends on where it is
                    var w = new VertexWeight { Joint0 = v.Position.Z > 0 ? 0 : 1, Weight0 = 0.75f, Joint1 = v.Position.Z > 0 ? 1 : 0, Weight1 = 0.25f };
                    weightOf[v] = w;
                    weighted.AddVertex(v, w);
                }
            }
            var prim = Prim(Vector3.One, weighted);

            foreach (var level in Levels(prim))
            {
                var face = level[0];
                Assert.That(face.Weights, Is.Not.Null);
                Assert.That(face.Weights!.Count, Is.EqualTo(face.Vertices.Count), "parallel");
                for (int v = 0; v < face.Vertices.Count; v++)
                {
                    var expected = weightOf[face.Vertices[v]];
                    Assert.That((face.Weights[v].Joint0, face.Weights[v].Weight0), Is.EqualTo((expected.Joint0, expected.Weight0)));
                }
            }
        }

        #endregion Each level of detail

        #region Shapes with known answers

        [Test]
        public void Sphere_StaysClosedFacesOutAndKeepsMostOfItsVolume()
        {
            var sphere = Sphere(24, 48);
            double original = Volume(sphere);
            var levels = Levels(Prim(Vector3.One, sphere));

            // A closed surface: every edge is shared by exactly two triangles
            var kept = new[] { 0.95, 0.90, 0.75 };
            for (int level = 0; level < 3; level++)
            {
                var face = levels[level][0];
                Assert.That(EdgeUse(face).Values, Has.All.EqualTo(2), $"level {level} has no holes or folds");
                for (int t = 0; t < Triangles(face); t++)
                {
                    var a = face.Vertices[(int)face.Indices[t * 3]].Position;
                    var b = face.Vertices[(int)face.Indices[t * 3 + 1]].Position;
                    var c = face.Vertices[(int)face.Indices[t * 3 + 2]].Position;
                    Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f), Is.GreaterThan(0f), $"level {level} triangle {t} faces outward");
                }
                double volume = Volume(face);
                TestContext.Out.WriteLine($"level {level}: {Triangles(face)} triangles, {volume / original:P1} of the volume");
                Assert.That(volume / original, Is.GreaterThan(kept[level]), $"level {level} keeps its shape");
                Assert.That(volume, Is.LessThan(original * 1.001), "and does not swell");
            }
        }

        [Test]
        public void EverySurface_StaysASurfaceOfTheSameKind()
        {
            // The Euler characteristic (corners - edges + triangles) is 2 for a sphere and 0 for a torus and a tube, and does not
            // change unless the surface is torn, closed up or pinched
            var shapes = new (string Name, ModelFace Face, int Euler)[]
            {
                ("sphere", Sphere(24, 48), 2), ("torus", Torus(40, 24), 0), ("tube", Tube(20, 24), 0), ("box", Box(6), 2), ("sheet", Sheet(24), 1),
                // and small ones, which run out of room to collapse long before they reach the target
                ("small sphere", Sphere(5, 8), 2), ("small torus", Torus(8, 6), 0), ("thin torus", Torus(6, 4), 0), ("small tube", Tube(3, 6), 0),
                ("small sheet", Sheet(3), 1), ("bumpy sheet", Bumpy(14), 1), ("small bumpy sheet", Bumpy(5), 1), ("small box", Box(1), 2)
            };
            foreach (var (name, face, euler) in shapes)
            {
                var (v0, e0, f0) = Counts(face);
                Assert.That(v0 - e0 + f0, Is.EqualTo(euler), name + " as made");
                AssertSane(face, name + " as made");

                var levels = Levels(Prim(Vector3.One, face));
                for (int level = 0; level < 3; level++)
                {
                    var (v, e, f) = Counts(levels[level][0]);
                    TestContext.Out.WriteLine($"{name} level {level}: {f} triangles of {f0}");
                    Assert.That(v - e + f, Is.EqualTo(euler), $"{name} level {level} is the same kind of surface");
                    AssertSane(levels[level][0], $"{name} level {level}");
                }
            }
        }

        [Test]
        public void OpenSurfaces_KeepTheirEdgesWhereTheyWere()
        {
            foreach (var face in new[] { Tube(20, 24), Sheet(24) })
            {
                var edge = EdgeUse(face).Where(e => e.Value == 1).SelectMany(e => new[] { e.Key.Item1, e.Key.Item2 }).ToHashSet();
                Assert.That(edge, Is.Not.Empty);

                foreach (var level in Levels(Prim(Vector3.One, face)))
                {
                    var kept = EdgeUse(level[0]).Where(e => e.Value == 1).SelectMany(e => new[] { e.Key.Item1, e.Key.Item2 }).ToList();
                    Assert.That(kept, Is.Not.Empty);
                    Assert.That(kept.All(p => edge.Contains(p)), Is.True, "the edge of the surface is made of points that were on its edge");
                }
            }
        }

        [Test]
        public void LongThinBox_KeepsItsLength_AtEveryLevel()
        {
            // 10 by 0.1 by 0.1 meters. In the unit cube the box is a cube, and judged there its long edges would
            // seem as important as its short ones.
            var prim = Prim(new Vector3(10f, 0.1f, 0.1f), Box(8));

            foreach (var level in Levels(prim))
            {
                var xs = level[0].Vertices.Select(v => v.Position.X).ToList();
                Assert.That(xs.Max() - xs.Min(), Is.GreaterThan(0.99f), "still the whole length");
                Assert.That(level[0].Vertices.Select(v => v.Position.Y).Max() - level[0].Vertices.Select(v => v.Position.Y).Min(), Is.GreaterThan(0.99f));
            }
        }

        [Test]
        public void FlatSheet_KeepsItsCorners_AndItsEdges()
        {
            var levels = Levels(Prim(Vector3.One, Sheet(32)));

            foreach (var level in levels)
            {
                var positions = level[0].Vertices.Select(v => v.Position).ToList();
                foreach (var corner in new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) })
                    Assert.That(positions, Does.Contain(corner));
                Assert.That(positions.All(p => p.Z == 0f && Math.Abs(p.X) <= 0.5f && Math.Abs(p.Y) <= 0.5f), Is.True);
            }
            Assert.That(Triangles(levels[2][0]), Is.LessThan(Triangles(levels[0][0])));
        }

        [Test]
        public void UnevenlyScaledBox_IsJudgedAtItsRealSize()
        {
            // Stretched only along x. The same mesh, scaled evenly, must come out differently from it.
            var stretched = Levels(Prim(new Vector3(10f, 1f, 1f), Sphere(16, 32)));
            var even = Levels(Prim(Vector3.One, Sphere(16, 32)));

            Assert.That(stretched[1][0].Vertices.Select(v => v.Position).ToList(), Is.Not.EqualTo(even[1][0].Vertices.Select(v => v.Position).ToList()));
        }

        #endregion Shapes with known answers

        #region The asset

        private static OSDMap Header(byte[] asset)
        {
            var mesh = new AssetMesh(UUID.Random(), asset);
            Assert.That(mesh.Decode(), Is.True);
            return (OSDMap)mesh.MeshData["asset_header"];
        }

        private static bool Decodes(byte[] asset, DetailLevel level, out FacetedMesh? mesh)
        {
            return FacetedMesh.TryDecodeFromAsset(new Primitive { LocalID = 1, Scale = Vector3.One }, new AssetMesh(UUID.Random(), asset), level, out mesh);
        }

        [Test]
        public void CreateAsset_AsItWas_HasOnlyTheHighLevel()
        {
            var prim = Prim(Vector3.One, Sphere(8, 16));
            prim.CreateAsset(UUID.Zero);

            var header = Header(prim.Asset);

            Assert.That(header.Keys, Is.EquivalentTo(new[] { "version", "creator", "date", "high_lod", "physics_convex" }));
            Assert.That(Decodes(prim.Asset, DetailLevel.Highest, out _), Is.True);
            Assert.That(Decodes(prim.Asset, DetailLevel.Low, out _), Is.False, "nothing to draw far away");
        }

        [Test]
        public void CreateAsset_WithNoOptions_IsTheSameAssetAsItWas()
        {
            var prim = Prim(Vector3.One, Sphere(8, 16));
            prim.CreateAsset(UUID.Zero);
            var before = new AssetMesh(UUID.Random(), prim.Asset);
            before.Decode();

            prim.CreateAsset(UUID.Zero, ModelAssetOptions.None);
            var after = new AssetMesh(UUID.Random(), prim.Asset);
            after.Decode();

            Assert.That(Header(prim.Asset).Keys, Is.EquivalentTo(new[] { "version", "creator", "date", "high_lod", "physics_convex" }));
            Assert.That(after.MeshData["high_lod"].ToString(), Is.EqualTo(before.MeshData["high_lod"].ToString()));
            Assert.That(((OSDMap)after.MeshData["physics_convex"])["BoundingVerts"].AsBinary(),
                Is.EqualTo(((OSDMap)before.MeshData["physics_convex"])["BoundingVerts"].AsBinary()));
        }

        [Test]
        public void CreateAsset_WithOptions_HasEveryLevelAndEachDecodesToItsOwnFaces()
        {
            var prim = Prim(Vector3.One, Sphere(24, 48), Box(4));
            var expected = Levels(prim);
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions());

            Assert.That(Header(prim.Asset).Keys, Is.SupersetOf(new[] { "lowest_lod", "low_lod", "medium_lod", "high_lod", "physics_convex" }));
            var levels = new[] { (DetailLevel.Highest, -1), (DetailLevel.High, 0), (DetailLevel.Medium, 1), (DetailLevel.Low, 2) };
            foreach (var (detail, index) in levels)
            {
                Assert.That(Decodes(prim.Asset, detail, out var mesh), Is.True, detail.ToString());
                Assert.That(mesh!.Faces, Has.Count.EqualTo(2));
                for (int f = 0; f < 2; f++)
                {
                    int triangles = index < 0 ? Triangles(prim.Faces[f]) : Triangles(expected[index][f]);
                    Assert.That(mesh.Faces[f].Indices.Count / 3, Is.EqualTo(triangles), $"{detail} face {f}");
                }
            }
        }

        [Test]
        public void CreateAsset_OfARiggedPrim_HasWeightsAtEveryLevel_AndTheStubPhysics()
        {
            var sphere = Sphere(12, 24);
            var weighted = Face();
            for (int t = 0; t < Triangles(sphere); t++)
                for (int c = 0; c < 3; c++)
                    weighted.AddVertex(sphere.Vertices[(int)sphere.Indices[t * 3 + c]], new VertexWeight { Joint0 = 0, Weight0 = 1f });
            var prim = Prim(Vector3.One, weighted);
            prim.Skin = new ModelSkin { InverseBindMatrices = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 } };
            prim.Skin.JointNames.Add("mPelvis");

            prim.CreateAsset(UUID.Zero, new ModelAssetOptions());

            foreach (var detail in new[] { DetailLevel.Highest, DetailLevel.High, DetailLevel.Medium, DetailLevel.Low })
            {
                Assert.That(Decodes(prim.Asset, detail, out var mesh), Is.True, detail.ToString());
                Assert.That(mesh!.Faces[0].Weights, Has.Count.EqualTo(mesh.Faces[0].Vertices.Count));
            }
            var stub = Prim(Vector3.One, Sphere(12, 24));
            stub.CreateAsset(UUID.Zero);
            Assert.That(Hull(prim.Asset), Is.EqualTo(Hull(stub.Asset)), "the stub, not a hull of the mesh");
        }

        [Test]
        public void CreateAsset_Cancelled_Stops()
        {
            var prim = Prim(Vector3.One, Sphere(24, 48));
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();

            Assert.Throws<OperationCanceledException>(() => prim.CreateAsset(UUID.Zero, new ModelAssetOptions(), cancel.Token));
        }

        [Test]
        public void Options_ReductionBelowOne_IsRefused_AndOneMakesLevelsThatAreTheSame()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModelAssetOptions { LodReduction = 0.5f }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModelAssetOptions { LodReduction = float.NaN }.Validate());

            var prim = Prim(Vector3.One, Sphere(8, 16));
            foreach (var level in ModelLodGenerator.Generate(prim, 1f, CancellationToken.None))
                Assert.That(Triangles(level[0]), Is.EqualTo(Triangles(prim.Faces[0])));
        }

        #endregion The asset

        #region Physics

        private static List<Vector3> Hull(byte[] asset)
        {
            var mesh = new AssetMesh(UUID.Random(), asset);
            Assert.That(mesh.Decode(), Is.True);
            var block = (OSDMap)mesh.MeshData["physics_convex"];
            var min = block["Min"].AsVector3();
            var max = block["Max"].AsVector3();
            var bytes = block["BoundingVerts"].AsBinary();
            var points = new List<Vector3>();
            for (int i = 0; i + 5 < bytes.Length; i += 6)
            {
                float X(int o) => (bytes[i + o] | (bytes[i + o + 1] << 8)) / 65535f;
                points.Add(new Vector3(min.X + X(0) * (max.X - min.X), min.Y + X(2) * (max.Y - min.Y), min.Z + X(4) * (max.Z - min.Z)));
            }
            return points;
        }

        private static float Support(IEnumerable<Vector3> points, Vector3 direction) => points.Max(p => Vector3.Dot(p, direction));

        private static IEnumerable<Vector3> RandomDirections(int count)
        {
            var random = new Random(7);
            for (int i = 0; i < count; i++)
            {
                var d = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
                if (d.Length() > 0.01f) yield return Vector3.Normalize(d);
            }
        }

        [Test]
        public void Hull_OfACubeWithPointsInside_IsTheCubesCorners()
        {
            var points = new List<double[]>();
            foreach (var x in new[] { 0.0, 1.0 }) foreach (var y in new[] { 0.0, 1.0 }) foreach (var z in new[] { 0.0, 1.0 }) points.Add(new[] { x, y, z });
            points.Add(new[] { 0.5, 0.5, 0.5 }); points.Add(new[] { 0.2, 0.7, 0.4 }); points.Add(new[] { 0.5, 0.5, 1.0 }); // inside, and on a face

            var hull = ModelPhysics.Hull(points.ToArray());

            Assert.That(hull, Is.Not.Null);
            Assert.That(hull!.Count, Is.EqualTo(8));
            Assert.That(hull.All(i => i < 8), Is.True, "the corners");
        }

        [TestCase("flat")]
        [TestCase("line")]
        [TestCase("point")]
        public void Hull_OfPointsThatAreNotSolid_IsNone(string kind)
        {
            var points = new List<double[]>();
            for (int i = 0; i < 10; i++)
            {
                double a = i, b = i * i % 7;
                points.Add(kind == "flat" ? new[] { a, b, 0.0 } : kind == "line" ? new[] { a, 2 * a, 3 * a } : new[] { 1.0, 1.0, 1.0 });
            }

            Assert.That(ModelPhysics.Hull(points.ToArray()), Is.Null);
        }

        [Test]
        public void Physics_OfASphere_HoldsTheMeshAndHasNoMorePointsThanAreAllowed()
        {
            var prim = Prim(new Vector3(2, 3, 4), Sphere(40, 80));
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions { GenerateLods = false });

            // At the prim's real size, which is where a shape is judged
            var size = prim.Scale;
            var hull = Hull(prim.Asset).Select(p => p * size).ToList();
            var mesh = prim.Faces[0].Vertices.Select(v => v.Position * size).ToList();

            Assert.That(hull.Count, Is.InRange(4, ModelPhysics.MaxHullPoints));
            foreach (var d in RandomDirections(500))
            {
                // The hull reaches nearly as far as the mesh does in every direction (3%)
                Assert.That(Support(hull, d), Is.GreaterThan(Support(mesh, d) * 0.97f), $"direction {d}");
                Assert.That(Support(hull, d), Is.LessThan(Support(mesh, d) * 1.001f), "and no further");
            }
        }

        [Test]
        public void Physics_OfAVeryStretchedMesh_IsJudgedAtItsRealSize()
        {
            // 20 by 1 by 1: spread over the unit cube the sampling would crowd the ends and leave the sides bare
            var prim = Prim(new Vector3(20f, 1f, 1f), Sphere(40, 80));
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions { GenerateLods = false });

            var size = prim.Scale;
            var hull = Hull(prim.Asset).Select(p => p * size).ToList();
            var mesh = prim.Faces[0].Vertices.Select(v => v.Position * size).ToList();

            foreach (var d in RandomDirections(500))
                Assert.That(Support(hull, d), Is.GreaterThan(Support(mesh, d) * 0.97f), $"direction {d}");
        }

        [Test]
        public void Physics_OfABox_IsExactlyTheBox()
        {
            var prim = Prim(Vector3.One, Box(6));
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions { GenerateLods = false });

            var hull = Hull(prim.Asset);

            Assert.That(hull, Has.Count.EqualTo(8));
            foreach (var d in RandomDirections(200))
                Assert.That(Support(hull, d), Is.EqualTo(Support(prim.Faces[0].Vertices.Select(v => v.Position), d)).Within(0.001f));
        }

        [Test]
        public void Physics_OfAFlatSheet_IsTheStub()
        {
            var prim = Prim(Vector3.One, Sheet(4));
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions());
            var stub = Prim(Vector3.One, Sheet(4));
            stub.CreateAsset(UUID.Zero);

            Assert.That(Hull(prim.Asset), Is.EqualTo(Hull(stub.Asset)));
        }

        [Test]
        public void Physics_TurnedOff_IsTheStub()
        {
            var prim = Prim(Vector3.One, Sphere(10, 20));
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions { GeneratePhysics = false });
            var stub = Prim(Vector3.One, Sphere(10, 20));
            stub.CreateAsset(UUID.Zero);

            Assert.That(Hull(prim.Asset), Is.EqualTo(Hull(stub.Asset)));
        }

        #endregion Physics

        #region Upload

        private const string CapUrl = "http://test.invalid/new-file-agent-inventory";
        private const string UploaderUrl = "http://test.invalid/uploader/abc123";

        private async Task<OSDMap> Upload(ModelPrim prim, ModelAssetOptions? options)
        {
            using var client = new FakeGridClient();
            client.AddCapability("NewFileAgentInventory", new Uri(CapUrl));
            client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\",\"upload_price\":10}}", "application/json");
            client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_inventory_item\":\"{UUID.Random()}\",\"new_asset\":\"{UUID.Random()}\"}}", "application/json");

            var result = await client.Inventory.UploadModelAsync(new[] { prim }, "m", "d", UUID.Random(), Permissions.NoPermissions, options: options);
            Assert.That(result.Success, Is.True);
            return (OSDMap)OSDParser.Deserialize(client.CapturedRequestBodies[0]);
        }

        [Test]
        public async Task UploadModelAsync_ByDefault_SendsEveryLevelAndAPhysicsHull()
        {
            var body = await Upload(Prim(Vector3.One, Sphere(24, 48)), null);

            var resources = (OSDMap)body["asset_resources"];
            var asset = ((OSDArray)resources["mesh_list"])[0].AsBinary();
            Assert.That(Header(asset).Keys, Is.SupersetOf(new[] { "lowest_lod", "low_lod", "medium_lod", "high_lod", "physics_convex" }));
            Assert.That(Hull(asset).Count, Is.GreaterThan(11), "the hull of a sphere, not the stub");
            var instance = (OSDMap)((OSDArray)resources["instance_list"])[0];
            Assert.That(instance["physics_shape_type"].AsInteger(), Is.EqualTo((int)PhysicsShapeType.ConvexHull));
        }

        [Test]
        public async Task UploadModelAsync_WithTheOptionsOff_SendsWhatItAlwaysDid()
        {
            var body = await Upload(Prim(Vector3.One, Sphere(24, 48)), ModelAssetOptions.None);

            var asset = ((OSDArray)((OSDMap)body["asset_resources"])["mesh_list"])[0].AsBinary();
            Assert.That(Header(asset).Keys, Is.EquivalentTo(new[] { "version", "creator", "date", "high_lod", "physics_convex" }));
            var stub = Prim(Vector3.One, Sphere(24, 48));
            stub.CreateAsset(UUID.Zero);
            Assert.That(Hull(asset), Is.EqualTo(Hull(stub.Asset)));
        }

        [Test]
        public async Task UploadModelAsync_ReductionBelowOne_Throws()
        {
            using var client = new FakeGridClient();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Inventory.UploadModelAsync(
                new[] { Prim(Vector3.One, Sphere(8, 16)) }, "m", "d", UUID.Random(), Permissions.NoPermissions,
                options: new ModelAssetOptions { LodReduction = 0.1f }));
        }

        #endregion Upload

        #region Size and speed

        [Test]
        public void BigFace_IsDoneInSeconds()
        {
            // 255 by 256 vertices: nearly the most a face holds, and about 130 thousand triangles
            var face = Face();
            const int w = 255, h = 256;
            var grid = new Vertex[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = (float)x / (w - 1) - 0.5f, fy = (float)y / (h - 1) - 0.5f;
                    grid[y * w + x] = new Vertex { Position = new Vector3(fx, fy, 0.05f * (float)Math.Sin(fx * 12) * (float)Math.Cos(fy * 9)), Normal = Vector3.UnitZ, TexCoord = new Vector2(fx + 0.5f, fy + 0.5f) };
                }
            for (int y = 0; y + 1 < h; y++)
                for (int x = 0; x + 1 < w; x++)
                {
                    face.AddVertex(grid[y * w + x]); face.AddVertex(grid[y * w + x + 1]); face.AddVertex(grid[(y + 1) * w + x]);
                    face.AddVertex(grid[y * w + x + 1]); face.AddVertex(grid[(y + 1) * w + x + 1]); face.AddVertex(grid[(y + 1) * w + x]);
                }
            var prim = Prim(new Vector3(4, 4, 1), face);
            Assert.That(face.Vertices.Count, Is.LessThanOrEqualTo(ModelPrim.MaxVerticesPerFace));

            var watch = Stopwatch.StartNew();
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions());
            watch.Stop();

            TestContext.Out.WriteLine($"{Triangles(face)} triangles, three levels and a hull: {watch.Elapsed.TotalSeconds:F1} s");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(15), "seconds, not minutes (a poor hash of the edges once made it half a minute)");
            Assert.That(Decodes(prim.Asset, DetailLevel.Low, out var lowest), Is.True);
            Assert.That(lowest!.Faces[0].Indices.Count / 3, Is.LessThanOrEqualTo(Triangles(face) / 27));
        }

        [Test]
        public void EightFaces_AreDoneInSeconds()
        {
            var prim = Prim(Vector3.One, Enumerable.Range(0, 8).Select(i => Sphere(60, 120)).ToArray());

            var watch = Stopwatch.StartNew();
            prim.CreateAsset(UUID.Zero, new ModelAssetOptions());
            watch.Stop();

            TestContext.Out.WriteLine($"8 faces of {Triangles(prim.Faces[0])} triangles: {watch.Elapsed.TotalSeconds:F1} s");
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(30));
        }

        #endregion Size and speed
    }
}
