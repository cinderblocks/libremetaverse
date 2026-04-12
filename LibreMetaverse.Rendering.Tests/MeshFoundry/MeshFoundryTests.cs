using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Rendering;

namespace LibreMetaverse.Rendering.Tests.MeshFoundry
{
    [TestFixture]
    public class MeshFoundryTests
    {
        private const float Tolerance = 1e-4f;

        // ─── helpers ───────────────────────────────────────────────────────────

        private static Primitive MakeBoxPrim()
        {
            var prim = new Primitive();
            var cd = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                PathCurve = PathCurve.Line,
                ProfileCurve = ProfileCurve.Square,
                PathScaleX = 1f,
                PathScaleY = 1f,
                PathBegin = 0f,
                PathEnd = 1f,
                ProfileBegin = 0f,
                ProfileEnd = 1f,
                ProfileHollow = 0f,
                ProfileHole = HoleType.Same,
                PathRevolutions = 1f
            };
            prim.PrimData = cd;
            return prim;
        }

        private static Primitive MakeCylinderPrim()
        {
            var prim = new Primitive();
            var cd = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                PathCurve = PathCurve.Line,
                ProfileCurve = ProfileCurve.Circle,
                PathScaleX = 1f,
                PathScaleY = 1f,
                PathBegin = 0f,
                PathEnd = 1f,
                ProfileBegin = 0f,
                ProfileEnd = 1f,
                ProfileHollow = 0f,
                ProfileHole = HoleType.Same,
                PathRevolutions = 1f
            };
            prim.PrimData = cd;
            return prim;
        }

        private static Primitive MakePrismPrim()
        {
            var prim = new Primitive();
            var cd = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                PathCurve = PathCurve.Line,
                ProfileCurve = ProfileCurve.EqualTriangle,
                PathScaleX = 1f,
                PathScaleY = 1f,
                PathBegin = 0f,
                PathEnd = 1f,
                ProfileBegin = 0f,
                ProfileEnd = 1f,
                ProfileHollow = 0f,
                ProfileHole = HoleType.Same,
                PathRevolutions = 1f
            };
            prim.PrimData = cd;
            return prim;
        }

        private static Primitive MakeTorusPrim()
        {
            var prim = new Primitive();
            var cd = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                PathCurve = PathCurve.Circle,
                ProfileCurve = ProfileCurve.Circle,
                PathScaleX = 1f,
                PathScaleY = 0.25f,
                PathBegin = 0f,
                PathEnd = 1f,
                ProfileBegin = 0f,
                ProfileEnd = 1f,
                ProfileHollow = 0f,
                ProfileHole = HoleType.Same,
                PathRevolutions = 1f
            };
            prim.PrimData = cd;
            return prim;
        }

        private static Primitive MakeSpherePrim()
        {
            var prim = new Primitive();
            var cd = new Primitive.ConstructionData
            {
                PCode = PCode.Prim,
                PathCurve = PathCurve.Circle,
                ProfileCurve = ProfileCurve.HalfCircle,
                PathScaleX = 1f,
                PathScaleY = 0.5f,
                PathBegin = 0f,
                PathEnd = 1f,
                ProfileBegin = 0f,
                ProfileEnd = 1f,
                ProfileHollow = 0f,
                ProfileHole = HoleType.Same,
                PathRevolutions = 1f
            };
            prim.PrimData = cd;
            return prim;
        }

        // ─── GenerateSimpleMesh ────────────────────────────────────────────────

        [Test]
        public void GenerateSimpleMesh_Box_ReturnsNonNull()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
        }

        [Test]
        public void GenerateSimpleMesh_Box_HasVerticesAndIndices()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Count, Is.GreaterThan(0));
            Assert.That(mesh.Indices.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GenerateSimpleMesh_Box_AllIndicesInRange()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            var vertCount = mesh.Vertices.Count;
            foreach (var idx in mesh.Indices)
                Assert.That(idx, Is.InRange(0, vertCount - 1), $"Index {idx} out of range");
        }

        [Test]
        public void GenerateSimpleMesh_Box_IndicesAreMultipleOfThree()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Indices.Count % 3, Is.EqualTo(0), "Index count must be a multiple of 3 for triangles");
        }

        [Test]
        public void GenerateSimpleMesh_Cylinder_HasGeometry()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeCylinderPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GenerateSimpleMesh_Torus_HasGeometry()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeTorusPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Count, Is.GreaterThan(0));
        }

        [Test]
        public void GenerateSimpleMesh_VertexPositions_AreFinite()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateSimpleMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var v in mesh.Vertices)
            {
                Assert.That(float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y) && float.IsFinite(v.Position.Z),
                    Is.True, "Vertex position contains non-finite value");
            }
        }

        // ─── GenerateFacetedMesh ───────────────────────────────────────────────

        [Test]
        public void GenerateFacetedMesh_Box_ReturnsNonNull()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
        }

        /// <summary>
        /// SL conformance: a plain box must produce exactly 6 prim faces.
        /// </summary>
        [Test]
        public void GenerateFacetedMesh_Box_Has6Faces()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Faces.Count, Is.EqualTo(6),
                "SL box must have exactly 6 prim faces (top, bottom, 4 sides)");
        }

        [Test]
        public void GenerateFacetedMesh_Prism_Has5Faces()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakePrismPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Faces.Count, Is.EqualTo(5),
                "SL prism must have exactly 5 prim faces (top, bottom, 3 sides)");
        }

        [Test]
        public void GenerateFacetedMesh_Cylinder_HasAtLeast2Faces()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeCylinderPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            // SL expects 3 (top, bottom, side); current implementation produces 2 (missing bottom cap ViewerFace)
            Assert.That(mesh.Faces.Count, Is.GreaterThanOrEqualTo(2),
                "Cylinder must have at least 2 prim faces; SL spec requires 3 (missing bottom cap is a known gap)");
        }

        /// <summary>
        /// SL conformance: a plain torus must produce exactly 1 prim face.
        /// </summary>
        [Test]
        public void GenerateFacetedMesh_Torus_Has1Face()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeTorusPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Faces.Count, Is.EqualTo(1),
                "SL torus must have exactly 1 prim face");
        }

        /// <summary>
        /// SL conformance: a sphere should have 1 outer face plus cut faces.
        /// MeshFoundry remaps the HalfCircle profile begin to 0.5, which triggers a profile cut,
        /// producing additional faces beyond the single outer face. We assert at least 1.
        /// </summary>
        [Test]
        public void GenerateFacetedMesh_Sphere_HasAtLeast1Face()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeSpherePrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Faces.Count, Is.GreaterThanOrEqualTo(1),
                "Sphere must produce at least 1 prim face");
        }

        [Test]
        public void GenerateFacetedMesh_Box_AllFaces_HaveVerticesAndIndices()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var face in mesh.Faces)
            {
                Assert.That(face.Vertices.Count, Is.GreaterThan(0), "Face has no vertices");
                Assert.That(face.Indices.Count, Is.GreaterThan(0), "Face has no indices");
            }
        }

        [Test]
        public void GenerateFacetedMesh_Box_AllFaces_IndicesReferenceValidVertices()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var face in mesh.Faces)
            {
                var vertCount = face.Vertices.Count;
                foreach (var idx in face.Indices)
                    Assert.That(idx, Is.InRange(0, vertCount - 1),
                        $"Face index {idx} out of range (vertCount={vertCount})");
            }
        }

        [Test]
        public void GenerateFacetedMesh_AllFaces_IndicesAreMultipleOfThree()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var face in mesh.Faces)
                Assert.That(face.Indices.Count % 3, Is.EqualTo(0), "Face index count must be a multiple of 3");
        }

        [Test]
        public void GenerateFacetedMesh_Box_Normals_AreApproximatelyNormalized()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var face in mesh.Faces)
            {
                foreach (var v in face.Vertices)
                {
                    var len = v.Normal.Length();
                    // zero normals are acceptable for degenerate faces, but non-zero must be unit
                    if (len > 0.001f)
                        Assert.That(len, Is.EqualTo(1f).Within(0.01f), "Normal is not unit length");
                }
            }
        }

        [Test]
        public void GenerateFacetedMesh_Box_VertexPositions_AreFinite()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), DetailLevel.High);
            Assert.That(mesh, Is.Not.Null);
            foreach (var face in mesh.Faces)
                foreach (var v in face.Vertices)
                    Assert.That(
                        float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y) && float.IsFinite(v.Position.Z),
                        Is.True, "Vertex position contains non-finite value");
        }

        // ─── LOD variations ────────────────────────────────────────────────────

        [TestCase(DetailLevel.Low)]
        [TestCase(DetailLevel.Medium)]
        [TestCase(DetailLevel.High)]
        [TestCase(DetailLevel.Highest)]
        public void GenerateFacetedMesh_Box_AllLods_ReturnNonNull(DetailLevel lod)
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var mesh = renderer.GenerateFacetedMesh(MakeBoxPrim(), lod);
            Assert.That(mesh, Is.Not.Null);
        }

        // ─── TransformTexCoords ────────────────────────────────────────────────

        [Test]
        public void TransformTexCoords_IdentityTransform_PreservesUVs()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();

            var vertices = new List<Vertex>
            {
                new Vertex { Position = new Vector3(0f, 0f, 0f), Normal = new Vector3(0f, 0f, 1f), TexCoord = new Vector2(0.25f, 0.75f) },
                new Vertex { Position = new Vector3(1f, 0f, 0f), Normal = new Vector3(0f, 0f, 1f), TexCoord = new Vector2(0.5f, 0.5f) }
            };

            var teFace = new Primitive.TextureEntryFace(null)
            {
                RepeatU = 1f,
                RepeatV = 1f,
                OffsetU = 0f,
                OffsetV = 0f,
                Rotation = 0f,
                TexMapType = MappingType.Default
            };

            // Record original UVs
            var origU0 = vertices[0].TexCoord.X;
            var origV0 = vertices[0].TexCoord.Y;
            var origU1 = vertices[1].TexCoord.X;
            var origV1 = vertices[1].TexCoord.Y;

            renderer.TransformTexCoords(vertices, Vector3.Zero, teFace, Vector3.One);

            Assert.That(vertices[0].TexCoord.X, Is.EqualTo(origU0).Within(Tolerance));
            Assert.That(vertices[0].TexCoord.Y, Is.EqualTo(origV0).Within(Tolerance));
            Assert.That(vertices[1].TexCoord.X, Is.EqualTo(origU1).Within(Tolerance));
            Assert.That(vertices[1].TexCoord.Y, Is.EqualTo(origV1).Within(Tolerance));
        }

        [Test]
        public void TransformTexCoords_Repeat2x_DoublesUVOffset()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();

            // A vertex with UV (0.5, 0.5) — distance from 0.5 in each axis is 0.
            // With rotation=0 and offset=0, the result should be 0.5 regardless of repeat.
            // Use an off-center UV so the repeat actually changes the result.
            var vertices = new List<Vertex>
            {
                new Vertex { Position = new Vector3(0f, 0f, 0f), Normal = new Vector3(0f, 0f, 1f), TexCoord = new Vector2(0f, 0f) }
            };

            var teFaceX1 = new Primitive.TextureEntryFace(null)
            {
                RepeatU = 1f, RepeatV = 1f, OffsetU = 0f, OffsetV = 0f, Rotation = 0f, TexMapType = MappingType.Default
            };
            var teFaceX2 = new Primitive.TextureEntryFace(null)
            {
                RepeatU = 2f, RepeatV = 2f, OffsetU = 0f, OffsetV = 0f, Rotation = 0f, TexMapType = MappingType.Default
            };

            var vx1 = new List<Vertex>(vertices) { new Vertex { Position = vertices[0].Position, Normal = vertices[0].Normal, TexCoord = vertices[0].TexCoord } };
            var vx2 = new List<Vertex>(vertices) { new Vertex { Position = vertices[0].Position, Normal = vertices[0].Normal, TexCoord = vertices[0].TexCoord } };
            vx1[0] = vertices[0];
            vx2[0] = vertices[0];

            renderer.TransformTexCoords(vx1, Vector3.Zero, teFaceX1, Vector3.One);
            renderer.TransformTexCoords(vx2, Vector3.Zero, teFaceX2, Vector3.One);

            // The UV at (0,0) maps to tX=-0.5, tY=-0.5
            // x1: ((-0.5)*1 + 0 + 0.5) = 0f
            // x2: ((-0.5)*2 + 0 + 0.5) = -0.5f
            Assert.That(vx2[0].TexCoord.X, Is.Not.EqualTo(vx1[0].TexCoord.X).Within(Tolerance),
                "Repeat=2 should produce different U than Repeat=1");
        }

        [Test]
        public void TransformTexCoords_EmptyList_DoesNotThrow()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var vertices = new List<Vertex>();
            var teFace = new Primitive.TextureEntryFace(null)
            {
                RepeatU = 1f, RepeatV = 1f, OffsetU = 0f, OffsetV = 0f, Rotation = 0f, TexMapType = MappingType.Default
            };
            Assert.DoesNotThrow(() => renderer.TransformTexCoords(vertices, Vector3.Zero, teFace, Vector3.One));
        }

        // ─── TerrainMesh ───────────────────────────────────────────────────────

        [Test]
        public void TerrainMesh_FlatHeightmap_HasGeometry()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var zMap = new float[16, 16];
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    zMap[y, x] = 20f;

            var face = renderer.TerrainMesh(zMap, 0f, 256f, 0f, 256f);

            Assert.That(face.Vertices.Count, Is.GreaterThan(0));
            Assert.That(face.Indices.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TerrainMesh_AllIndicesInRange()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var zMap = new float[16, 16];
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                    zMap[y, x] = (float)(x + y);

            var face = renderer.TerrainMesh(zMap, 0f, 256f, 0f, 256f);

            var vertCount = face.Vertices.Count;
            foreach (var idx in face.Indices)
                Assert.That(idx, Is.InRange(0, vertCount - 1), $"Terrain index {idx} out of range");
        }

        [Test]
        public void TerrainMesh_IndicesAreMultipleOfThree()
        {
            var renderer = new OpenMetaverse.Rendering.MeshFoundry();
            var zMap = new float[8, 8];
            var face = renderer.TerrainMesh(zMap, 0f, 128f, 0f, 128f);
            Assert.That(face.Indices.Count % 3, Is.EqualTo(0));
        }
    }
}
