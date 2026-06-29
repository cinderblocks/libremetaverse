using System;
using System.Collections.Generic;
using NUnit.Framework;
using LibreMetaverse.Animesh;
using LibreMetaverse.Rendering;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Animesh")]
    public class AnimeshSkinningTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static float[] IdentityMatrix() => new float[]
        {
            1,0,0,0,
            0,1,0,0,
            0,0,1,0,
            0,0,0,1,
        };

        private static Joint MakeJoint(string name, float[] pos = null, float[] rot = null)
        {
            return new Joint { name = name, pos = pos ?? new float[] { 0, 0, 0 }, rot = rot ?? new float[] { 0, 0, 0 } };
        }

        private static LindenSkeleton MakeSingleJointSkeleton(string jointName)
        {
            return new LindenSkeleton { bone = MakeJoint(jointName) };
        }

        private static MeshSkinData MakeSkinData(string jointName)
        {
            return new MeshSkinData
            {
                JointNames = new[] { jointName },
                InverseBindMatrices = IdentityMatrix(),
                BindShapeMatrix = IdentityMatrix(),
            };
        }

        // ── Argument validation ────────────────────────────────────────────────

        [Test]
        public void ComputeSkinningMatrices_NullPose_ThrowsArgumentNullException()
        {
            var skeleton = MakeSingleJointSkeleton("mRoot");
            var skinData = MakeSkinData("mRoot");
            Assert.Throws<ArgumentNullException>(
                () => AnimeshSkinning.ComputeSkinningMatrices(null, skeleton, skinData));
        }

        [Test]
        public void ComputeSkinningMatrices_NullSkeleton_ThrowsArgumentNullException()
        {
            var pose = new Dictionary<string, JointPose>();
            var skinData = MakeSkinData("mRoot");
            Assert.Throws<ArgumentNullException>(
                () => AnimeshSkinning.ComputeSkinningMatrices(pose, null, skinData));
        }

        [Test]
        public void ComputeSkinningMatrices_NullSkinData_ThrowsArgumentNullException()
        {
            var pose = new Dictionary<string, JointPose>();
            var skeleton = MakeSingleJointSkeleton("mRoot");
            Assert.Throws<ArgumentNullException>(
                () => AnimeshSkinning.ComputeSkinningMatrices(pose, skeleton, null));
        }

        // ── Empty / trivial cases ──────────────────────────────────────────────

        [Test]
        public void ComputeSkinningMatrices_EmptySkinData_ReturnsEmptyArray()
        {
            var pose = new Dictionary<string, JointPose>();
            var skeleton = MakeSingleJointSkeleton("mRoot");
            var skinData = new MeshSkinData { JointNames = Array.Empty<string>(), InverseBindMatrices = Array.Empty<float>() };

            var result = AnimeshSkinning.ComputeSkinningMatrices(pose, skeleton, skinData);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ComputeSkinningMatrices_JointNotInSkeleton_ProducesIdentity()
        {
            var pose = new Dictionary<string, JointPose>();
            var skeleton = MakeSingleJointSkeleton("mRoot");
            var skinData = MakeSkinData("nonexistent");

            var result = AnimeshSkinning.ComputeSkinningMatrices(pose, skeleton, skinData);

            Assert.That(result.Length, Is.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(Matrix4.Identity));
        }

        // ── Identity skinning ─────────────────────────────────────────────────

        [Test]
        public void ComputeSkinningMatrices_IdentitySkeletonAndBindPose_ProducesIdentityMatrix()
        {
            // Joint at origin with zero rotation, invBind = identity → skinMat = identity * identity = identity.
            var pose = new Dictionary<string, JointPose>();
            var skeleton = MakeSingleJointSkeleton("mRoot");
            var skinData = MakeSkinData("mRoot");

            var result = AnimeshSkinning.ComputeSkinningMatrices(pose, skeleton, skinData);

            Assert.That(result.Length, Is.EqualTo(1));
            AssertMatrixIdentity(result[0], tolerance: 1e-5f);
        }

        [Test]
        public void DeformVertices_NullWeights_PassesPositionsThrough()
        {
            var face = new Face
            {
                Vertices = new System.Collections.Generic.List<Vertex>
                {
                    new Vertex { Position = new Vector3(1, 2, 3) },
                    new Vertex { Position = new Vector3(4, 5, 6) },
                },
                Weights = null,
            };

            var outPos = new Vector3[2];
            AnimeshSkinning.DeformVertices(face, Array.Empty<Matrix4>(), Matrix4.Identity,
                outPos.AsSpan());

            Assert.That(outPos[0], Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(outPos[1], Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void DeformVertices_ShortOutputSpan_ThrowsArgumentException()
        {
            var face = new Face
            {
                Vertices = new System.Collections.Generic.List<Vertex>
                {
                    new Vertex { Position = Vector3.Zero },
                    new Vertex { Position = Vector3.Zero },
                },
                Weights = null,
            };

            var shortOutput = new Vector3[1]; // too short for 2 vertices
            Assert.Throws<ArgumentException>(
                () => AnimeshSkinning.DeformVertices(face, Array.Empty<Matrix4>(), Matrix4.Identity,
                    shortOutput.AsSpan()));
        }

        [Test]
        public void DeformVertices_IdentitySkinning_FullWeight_PreservesPositions()
        {
            // Single vertex, weight=1 on joint0, identity skinning matrix → position unchanged.
            var expectedPos = new Vector3(3f, 7f, -2f);

            var face = new Face
            {
                Vertices = new System.Collections.Generic.List<Vertex>
                {
                    new Vertex { Position = expectedPos },
                },
                Weights = new System.Collections.Generic.List<VertexWeight>
                {
                    new VertexWeight { Joint0 = 0, Weight0 = 1f },
                },
            };

            var skinMatrices = new[] { Matrix4.Identity };
            var outPos = new Vector3[1];
            AnimeshSkinning.DeformVertices(face, skinMatrices, Matrix4.Identity, outPos.AsSpan());

            Assert.That(outPos[0].X, Is.EqualTo(expectedPos.X).Within(1e-5f));
            Assert.That(outPos[0].Y, Is.EqualTo(expectedPos.Y).Within(1e-5f));
            Assert.That(outPos[0].Z, Is.EqualTo(expectedPos.Z).Within(1e-5f));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AssertMatrixIdentity(Matrix4 m, float tolerance)
        {
            var id = Matrix4.Identity;
            Assert.That(m.M11, Is.EqualTo(id.M11).Within(tolerance), "M11");
            Assert.That(m.M12, Is.EqualTo(id.M12).Within(tolerance), "M12");
            Assert.That(m.M13, Is.EqualTo(id.M13).Within(tolerance), "M13");
            Assert.That(m.M14, Is.EqualTo(id.M14).Within(tolerance), "M14");
            Assert.That(m.M21, Is.EqualTo(id.M21).Within(tolerance), "M21");
            Assert.That(m.M22, Is.EqualTo(id.M22).Within(tolerance), "M22");
            Assert.That(m.M23, Is.EqualTo(id.M23).Within(tolerance), "M23");
            Assert.That(m.M24, Is.EqualTo(id.M24).Within(tolerance), "M24");
            Assert.That(m.M31, Is.EqualTo(id.M31).Within(tolerance), "M31");
            Assert.That(m.M32, Is.EqualTo(id.M32).Within(tolerance), "M32");
            Assert.That(m.M33, Is.EqualTo(id.M33).Within(tolerance), "M33");
            Assert.That(m.M34, Is.EqualTo(id.M34).Within(tolerance), "M34");
            Assert.That(m.M41, Is.EqualTo(id.M41).Within(tolerance), "M41");
            Assert.That(m.M42, Is.EqualTo(id.M42).Within(tolerance), "M42");
            Assert.That(m.M43, Is.EqualTo(id.M43).Within(tolerance), "M43");
            Assert.That(m.M44, Is.EqualTo(id.M44).Within(tolerance), "M44");
        }
    }
}
