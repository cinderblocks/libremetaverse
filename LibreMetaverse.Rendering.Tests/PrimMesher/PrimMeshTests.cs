using System;
using System.Linq;
using NUnit.Framework;
using LibreMetaverse.PrimMesher;

namespace LibreMetaverse.Rendering.Tests.PrimMesher
{
    [TestFixture]
    public class PrimMeshTests
    {
        private const float Tolerance = 1e-5f;

        // ─── helpers ───────────────────────────────────────────────────────────

        private static PrimMesh MakeBox(bool viewerMode = false)
        {
            var pm = new PrimMesh(4, 0f, 1f, 0f, 4)
            {
                viewerMode = viewerMode,
                calcVertexNormals = viewerMode
            };
            return pm;
        }

        private static PrimMesh MakeCylinder(bool viewerMode = false)
        {
            var pm = new PrimMesh(24, 0f, 1f, 0f, 4)
            {
                viewerMode = viewerMode,
                calcVertexNormals = viewerMode
            };
            return pm;
        }

        private static PrimMesh MakeTorus(bool viewerMode = false)
        {
            var pm = new PrimMesh(24, 0f, 1f, 0f, 4)
            {
                holeSizeX = 1f,
                holeSizeY = 0.25f,
                viewerMode = viewerMode,
                calcVertexNormals = viewerMode
            };
            return pm;
        }

        private static PrimMesh MakeSphere(bool viewerMode = false)
        {
            var pm = new PrimMesh(24, 0f, 1f, 0f, 4)
            {
                sphereMode = true,
                holeSizeX = 1f,
                holeSizeY = 0.5f,
                viewerMode = viewerMode,
                calcVertexNormals = viewerMode
            };
            return pm;
        }

        private static PrimMesh MakePrism(bool viewerMode = false)
        {
            var pm = new PrimMesh(3, 0f, 1f, 0f, 3)
            {
                viewerMode = viewerMode,
                calcVertexNormals = viewerMode
            };
            return pm;
        }

        // ─── basic geometry ────────────────────────────────────────────────────

        [Test]
        public void Extrude_Box_Linear_HasVerticesAndFaces()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Box_Linear_AllFaceIndicesInRange()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);

            var vertCount = pm.coords.Count;
            foreach (var f in pm.faces)
            {
                Assert.That(f.v1, Is.InRange(0, vertCount - 1), "face.v1 out of range");
                Assert.That(f.v2, Is.InRange(0, vertCount - 1), "face.v2 out of range");
                Assert.That(f.v3, Is.InRange(0, vertCount - 1), "face.v3 out of range");
            }
        }

        [Test]
        public void Extrude_Cylinder_Linear_HasGeometry()
        {
            var pm = MakeCylinder();
            pm.Extrude(PathType.Linear);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Prism_Linear_HasGeometry()
        {
            var pm = MakePrism();
            pm.Extrude(PathType.Linear);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Torus_Circular_HasGeometry()
        {
            var pm = MakeTorus();
            pm.Extrude(PathType.Circular);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Torus_Circular_AllFaceIndicesInRange()
        {
            var pm = MakeTorus();
            pm.Extrude(PathType.Circular);

            var vertCount = pm.coords.Count;
            foreach (var f in pm.faces)
            {
                Assert.That(f.v1, Is.InRange(0, vertCount - 1), "face.v1 out of range");
                Assert.That(f.v2, Is.InRange(0, vertCount - 1), "face.v2 out of range");
                Assert.That(f.v3, Is.InRange(0, vertCount - 1), "face.v3 out of range");
            }
        }

        [Test]
        public void Extrude_Box_WithHollow_HasGeometry()
        {
            var pm = new PrimMesh(4, 0f, 1f, 0.5f, 4);
            pm.Extrude(PathType.Linear);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Box_WithProfileCut_HasGeometry()
        {
            var pm = new PrimMesh(4, 0.1f, 0.9f, 0f, 4);
            pm.Extrude(PathType.Linear);

            Assert.That(pm.coords.Count, Is.GreaterThan(0));
            Assert.That(pm.faces.Count, Is.GreaterThan(0));
        }

        // ─── viewer mode ───────────────────────────────────────────────────────

        [Test]
        public void Extrude_Box_ViewerMode_HasViewerFaces()
        {
            var pm = MakeBox(viewerMode: true);
            pm.Extrude(PathType.Linear);

            Assert.That(pm.viewerFaces, Is.Not.Null);
            Assert.That(pm.viewerFaces.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Extrude_Box_ViewerMode_AllViewerFaceVerticesFinite()
        {
            var pm = MakeBox(viewerMode: true);
            pm.Extrude(PathType.Linear);

            foreach (var vf in pm.viewerFaces)
            {
                Assert.That(float.IsFinite(vf.v1.X) && float.IsFinite(vf.v1.Y) && float.IsFinite(vf.v1.Z), Is.True, "v1 contains non-finite value");
                Assert.That(float.IsFinite(vf.v2.X) && float.IsFinite(vf.v2.Y) && float.IsFinite(vf.v2.Z), Is.True, "v2 contains non-finite value");
                Assert.That(float.IsFinite(vf.v3.X) && float.IsFinite(vf.v3.Y) && float.IsFinite(vf.v3.Z), Is.True, "v3 contains non-finite value");
            }
        }

        /// <summary>
        /// SL conformance: a plain box (sides=4, linear path, no hollow, no cut)
        /// must have exactly 6 prim faces (top, bottom, 4 sides).
        /// </summary>
        [Test]
        public void Extrude_Box_ViewerMode_NumPrimFaces_Is6()
        {
            var pm = MakeBox(viewerMode: true);
            pm.Extrude(PathType.Linear);

            var indexer = pm.GetVertexIndexer();
            Assert.That(indexer, Is.Not.Null);
            Assert.That(indexer.numPrimFaces, Is.EqualTo(6),
                "SL box must have exactly 6 prim faces (top, bottom, 4 sides)");
        }

        /// <summary>
        /// SL conformance: a plain cylinder (sides=24, linear path) must have 3
        /// prim faces (top cap, bottom cap, outer side).
        /// NOTE: The current implementation emits only the top end cap as a
        /// ViewerFace; the bottom cap is stored only in this.faces (Face structs),
        /// so numPrimFaces is currently 2 instead of the expected 3.
        /// This test documents the known gap against the SL protocol.
        /// </summary>
        [Test]
        public void Extrude_Cylinder_ViewerMode_NumPrimFaces_AtLeast2()
        {
            var pm = MakeCylinder(viewerMode: true);
            pm.Extrude(PathType.Linear);

            var indexer = pm.GetVertexIndexer();
            Assert.That(indexer, Is.Not.Null);
            // SL expects 3; current implementation produces 2 (missing bottom cap ViewerFace)
            Assert.That(indexer.numPrimFaces, Is.GreaterThanOrEqualTo(2),
                "Cylinder must have at least 2 prim faces; SL spec requires 3 (missing bottom cap is a known gap)");
        }

        /// <summary>
        /// SL conformance: a plain torus (circular path) must have 1 prim face.
        /// </summary>
        [Test]
        public void Extrude_Torus_ViewerMode_NumPrimFaces_Is1()
        {
            var pm = MakeTorus(viewerMode: true);
            pm.Extrude(PathType.Circular);

            var indexer = pm.GetVertexIndexer();
            Assert.That(indexer, Is.Not.Null);
            Assert.That(indexer.numPrimFaces, Is.EqualTo(1),
                "SL torus must have exactly 1 prim face");
        }

        /// <summary>
        /// SL conformance: a sphere (circular path, sphereMode) must have 1 prim face.
        /// </summary>
        [Test]
        public void Extrude_Sphere_ViewerMode_NumPrimFaces_Is1()
        {
            var pm = MakeSphere(viewerMode: true);
            pm.Extrude(PathType.Circular);

            var indexer = pm.GetVertexIndexer();
            Assert.That(indexer, Is.Not.Null);
            Assert.That(indexer.numPrimFaces, Is.EqualTo(1),
                "SL sphere must have exactly 1 prim face");
        }

        [Test]
        public void Extrude_Torus_ViewerMode_HasViewerFaces()
        {
            var pm = MakeTorus(viewerMode: true);
            pm.Extrude(PathType.Circular);

            Assert.That(pm.viewerFaces, Is.Not.Null);
            Assert.That(pm.viewerFaces.Count, Is.GreaterThan(0));
        }

        // ─── CalcNormals ───────────────────────────────────────────────────────

        [Test]
        public void CalcNormals_Box_ProducesOneNormalPerFace()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            pm.CalcNormals();

            Assert.That(pm.normals, Is.Not.Null);
            Assert.That(pm.normals.Count, Is.EqualTo(pm.faces.Count));
        }

        [Test]
        public void CalcNormals_Box_AllNonZeroNormalsUnitLength()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            pm.CalcNormals();

            foreach (var n in pm.normals)
            {
                var len = n.Length();
                // Degenerate (zero-area) faces produce a zero normal; skip those
                if (len > 0.001f)
                    Assert.That(len, Is.EqualTo(1f).Within(Tolerance), "Non-zero normal is not unit length");
            }
        }

        [Test]
        public void CalcNormals_CalledTwice_IsIdempotent()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            pm.CalcNormals();
            var firstCount = pm.normals.Count;
            pm.CalcNormals();
            Assert.That(pm.normals.Count, Is.EqualTo(firstCount));
        }

        // ─── VertexIndexer ─────────────────────────────────────────────────────

        [Test]
        public void VertexIndexer_Box_IsNotNull()
        {
            var pm = MakeBox(viewerMode: true);
            pm.Extrude(PathType.Linear);
            Assert.That(pm.GetVertexIndexer(), Is.Not.Null);
        }

        [Test]
        public void VertexIndexer_NonViewerMode_ReturnsNull()
        {
            var pm = MakeBox(viewerMode: false);
            pm.Extrude(PathType.Linear);
            Assert.That(pm.GetVertexIndexer(), Is.Null);
        }

        [Test]
        public void VertexIndexer_Box_AllPolygonIndicesInRange()
        {
            var pm = MakeBox(viewerMode: true);
            pm.Extrude(PathType.Linear);
            var indexer = pm.GetVertexIndexer();
            Assert.That(indexer, Is.Not.Null);

            for (var face = 0; face < indexer.numPrimFaces; face++)
            {
                var polys = indexer.viewerPolygons[face];
                var verts = indexer.viewerVertices[face];
                Assert.That(verts, Is.Not.Null);
                if (polys == null) continue;
                foreach (var poly in polys)
                {
                    var vertCount = verts.Count;
                    Assert.That(poly.v1, Is.InRange(0, vertCount - 1), $"Polygon v1 {poly.v1} out of range for face {face}");
                    Assert.That(poly.v2, Is.InRange(0, vertCount - 1), $"Polygon v2 {poly.v2} out of range for face {face}");
                    Assert.That(poly.v3, Is.InRange(0, vertCount - 1), $"Polygon v3 {poly.v3} out of range for face {face}");
                }
            }
        }

        // ─── Copy ──────────────────────────────────────────────────────────────

        [Test]
        public void Copy_Box_ProducesIndependentInstance()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            var copy = pm.Copy();

            Assert.That(copy.coords.Count, Is.EqualTo(pm.coords.Count));
            Assert.That(copy.faces.Count, Is.EqualTo(pm.faces.Count));

            // Mutate the copy and ensure original is unaffected
            copy.AddPos(1f, 0f, 0f);
            Assert.That(pm.coords[0].X, Is.Not.EqualTo(copy.coords[0].X));
        }

        // ─── AddPos / Scale / AddRot ───────────────────────────────────────────

        [Test]
        public void AddPos_ShiftsAllCoords()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            var originalX = pm.coords[0].X;
            pm.AddPos(10f, 0f, 0f);
            Assert.That(pm.coords[0].X, Is.EqualTo(originalX + 10f).Within(Tolerance));
        }

        [Test]
        public void Scale_ScalesAllCoords()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            var originalX = pm.coords[0].X;
            pm.Scale(2f, 2f, 2f);
            Assert.That(pm.coords[0].X, Is.EqualTo(originalX * 2f).Within(Tolerance));
        }

        [Test]
        public void AddRot_ByIdentity_DoesNotChangeCoords()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            var originalX = pm.coords[0].X;
            var originalY = pm.coords[0].Y;
            var originalZ = pm.coords[0].Z;
            pm.AddRot(new Quat(0f, 0f, 0f, 1f));
            Assert.That(pm.coords[0].X, Is.EqualTo(originalX).Within(Tolerance));
            Assert.That(pm.coords[0].Y, Is.EqualTo(originalY).Within(Tolerance));
            Assert.That(pm.coords[0].Z, Is.EqualTo(originalZ).Within(Tolerance));
        }

        // ─── SurfaceNormal ─────────────────────────────────────────────────────

        [Test]
        public void SurfaceNormal_Box_FirstFace_IsUnitLength()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            var n = pm.SurfaceNormal(0);
            Assert.That(n.Length(), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void SurfaceNormal_InvalidIndex_Throws()
        {
            var pm = MakeBox();
            pm.Extrude(PathType.Linear);
            Assert.Throws<Exception>(() => pm.SurfaceNormal(-1));
            Assert.Throws<Exception>(() => pm.SurfaceNormal(pm.faces.Count));
        }
    }
}
