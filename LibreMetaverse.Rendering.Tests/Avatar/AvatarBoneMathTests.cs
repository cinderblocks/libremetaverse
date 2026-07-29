using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace LibreMetaverse.Rendering.Tests.Avatar
{
    // Namespace-body-scoped aliases (NOT file/compilation-unit scope): this namespace nests
    // under LibreMetaverse, which has its own Vector3/Quaternion (SL-protocol structs) — a
    // file-scope "using Vector3 = System.Numerics.Vector3;" here would silently lose to
    // LibreMetaverse.Vector3 via enclosing-namespace member lookup. See the identical note in
    // AvatarBoneMath.cs, discovered and verified via reflection while moving this code.
    using Vector3    = System.Numerics.Vector3;
    using Quaternion = System.Numerics.Quaternion;
    using Matrix4x4  = System.Numerics.Matrix4x4;

    /// <summary>
    /// Regression coverage for <see cref="AvatarBoneMath.BuildBoneWorldMatrices"/>'s bone-matrix
    /// composition. Verifies it matches the documented SL <c>LLXform::update</c> model
    /// (<c>indra/llmath/xform.cpp</c>): each joint's own scale never compounds into its
    /// descendants' scale, while a parent's scale DOES offset where children are positioned.
    /// A naive <c>world = local * parentWorld</c> matrix-chain implementation gets this wrong —
    /// see AvatarBoneMath's class doc comment for the fitted-mesh symptom that motivated the fix
    /// (correct at T-pose, collapsed to spikes once real rotation was applied at non-unit scale).
    /// </summary>
    [TestFixture]
    public class AvatarBoneMathTests
    {
        private const float Tolerance = 1e-4f;

        private static LindenSkeleton BuildRootChildSkeleton(float[] rootRot, float[] childRot)
        {
            var child = new Joint { name = "Child", rot = childRot };
            var root = new Joint { name = "Root", rot = rootRot, bone = new[] { child } };
            return new LindenSkeleton { bone = root };
        }

        [Test]
        public void BuildBoneWorldMatrices_UnitScale_MatchesNaiveMatrixChain()
        {
            // At unit scale, the SL model and a naive local*parentWorld matrix chain must
            // agree exactly (scale-compounding only diverges once scale is non-unit) — this
            // is the "T-pose still looks right" invariant that made the original scale bug
            // invisible until real (A-pose) rotation was applied on a non-default shape.
            var skeleton = BuildRootChildSkeleton(rootRot: new[] { 0f, 0f, 30f }, childRot: new[] { 0f, 15f, 0f });
            var boneTransforms = new Dictionary<string, BoneTransform>(StringComparer.Ordinal)
            {
                ["Root"]  = new BoneTransform { Position = new Vector3(0f, 0f, 1f).ToLm(), Scale = Vector3.One.ToLm() },
                ["Child"] = new BoneTransform { Position = new Vector3(0f, 1f, 0f).ToLm(), Scale = Vector3.One.ToLm() },
            };

            var result = AvatarBoneMath.BuildBoneWorldMatrices(skeleton, boneTransforms);

            // Naive chain: world = local * parentWorld, using the SAME per-joint local matrix
            // (Scale * Rotate * Translate) AvatarBoneMath itself builds — this is the exact
            // "old" formula being regression-tested against, reduced to unit scale where the
            // two formulations are mathematically required to coincide.
            var rootLocal = LocalMatrix(new Vector3(0f, 0f, 1f), Vector3.One, rootRot: new[] { 0f, 0f, 30f });
            var rootNaive = rootLocal; // parentWorld = Identity
            var childLocal = LocalMatrix(new Vector3(0f, 1f, 0f), Vector3.One, rootRot: new[] { 0f, 15f, 0f });
            var childNaive = childLocal * rootNaive;

            AssertMatrixApprox(rootNaive, result["Root"]);
            AssertMatrixApprox(childNaive, result["Child"]);
        }

        [Test]
        public void BuildBoneWorldMatrices_AnisotropicParentScale_OffsetsChildPositionButNotChildScale()
        {
            // Root scaled 2x on Y only (anisotropic — the exact shape of a VP "Leg Length"/
            // "Height" style skeletal distortion), rotated 90 degrees. Child extends 1 unit
            // along local Y (mirrors an arm/leg bone's own extension axis).
            var skeleton = BuildRootChildSkeleton(rootRot: new[] { 0f, 0f, 90f }, childRot: new[] { 0f, 0f, 0f });
            var rootScale  = new Vector3(1f, 2f, 1f);
            var childScale = new Vector3(1f, 1f, 1f);
            var boneTransforms = new Dictionary<string, BoneTransform>(StringComparer.Ordinal)
            {
                ["Root"]  = new BoneTransform { Position = new Vector3(0f, 0f, 1f).ToLm(), Scale = rootScale.ToLm() },
                ["Child"] = new BoneTransform { Position = new Vector3(0f, 1f, 0f).ToLm(), Scale = childScale.ToLm() },
            };

            var result = AvatarBoneMath.BuildBoneWorldMatrices(skeleton, boneTransforms);

            // Independently derived expected value, straight from the documented SL formula —
            // does not call any AvatarBoneMath internals:
            //   worldRotation = localRotation * parentWorldRotation
            //   worldPosition = Rotate(localPosition * parentOwnScale, parentWorldRotation) + parentWorldPosition
            //   worldMatrix   = Scale(ownScale) * Rotate(worldRotation) * Translate(worldPosition)
            var rootRot = Quaternion.CreateFromYawPitchRoll(0f, 0f, MathF.PI / 2f); // matches rot=[0,0,90]
            var rootWorldPos = new Vector3(0f, 0f, 1f); // parent is Identity/Zero/One
            var childWorldPos = Vector3.Transform(new Vector3(0f, 1f, 0f) * rootScale, rootRot) + rootWorldPos;
            var childWorldMatrix = Matrix4x4.CreateScale(childScale)
                                 * Matrix4x4.CreateFromQuaternion(rootRot) // child local rot is Identity
                                 * Matrix4x4.CreateTranslation(childWorldPos);

            AssertMatrixApprox(childWorldMatrix, result["Child"]);

            // The invariant this whole rewrite exists to guarantee: a child's OWN scale is
            // never multiplied by an ancestor's scale. If Root's 2x Y-scale had compounded
            // into Child (the naive-matrix-chain bug), Child's own row lengths would show a
            // (1,2,1)-ish scale here instead of (1,1,1).
            var childRow0Len = new Vector3(result["Child"].M11, result["Child"].M12, result["Child"].M13).Length();
            var childRow1Len = new Vector3(result["Child"].M21, result["Child"].M22, result["Child"].M23).Length();
            var childRow2Len = new Vector3(result["Child"].M31, result["Child"].M32, result["Child"].M33).Length();
            Assert.That(childRow0Len, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(childRow1Len, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(childRow2Len, Is.EqualTo(1f).Within(Tolerance));
        }

        // Builds a single joint's own local matrix (Scale * Rotate * Translate) using the
        // library's own documented composition order, for use as a NAIVE (non-recursive,
        // no scale-hierarchy-fix) reference chain in the unit-scale regression test above.
        private static Matrix4x4 LocalMatrix(Vector3 pos, Vector3 scale, float[] rootRot)
        {
            var rot = Quaternion.CreateFromYawPitchRoll(
                rootRot.Length > 1 ? rootRot[1] * (MathF.PI / 180f) : 0f,
                rootRot.Length > 0 ? rootRot[0] * (MathF.PI / 180f) : 0f,
                rootRot.Length > 2 ? rootRot[2] * (MathF.PI / 180f) : 0f);
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
        }

        private static void AssertMatrixApprox(Matrix4x4 expected, Matrix4x4 actual)
        {
            Assert.That(actual.M11, Is.EqualTo(expected.M11).Within(Tolerance));
            Assert.That(actual.M12, Is.EqualTo(expected.M12).Within(Tolerance));
            Assert.That(actual.M13, Is.EqualTo(expected.M13).Within(Tolerance));
            Assert.That(actual.M21, Is.EqualTo(expected.M21).Within(Tolerance));
            Assert.That(actual.M22, Is.EqualTo(expected.M22).Within(Tolerance));
            Assert.That(actual.M23, Is.EqualTo(expected.M23).Within(Tolerance));
            Assert.That(actual.M31, Is.EqualTo(expected.M31).Within(Tolerance));
            Assert.That(actual.M32, Is.EqualTo(expected.M32).Within(Tolerance));
            Assert.That(actual.M33, Is.EqualTo(expected.M33).Within(Tolerance));
            Assert.That(actual.M41, Is.EqualTo(expected.M41).Within(Tolerance));
            Assert.That(actual.M42, Is.EqualTo(expected.M42).Within(Tolerance));
            Assert.That(actual.M43, Is.EqualTo(expected.M43).Within(Tolerance));
        }
    }

    /// <summary>Converts a System.Numerics.Vector3 test value into the LibreMetaverse.Vector3
    /// that <see cref="BoneTransform"/> actually stores (BoneTransform lives in a namespace
    /// nested under LibreMetaverse, so its Vector3 fields are the SL-protocol type, not
    /// System.Numerics — see the namespace-scoping note in AvatarBoneMath.cs).</summary>
    internal static class Vector3TestExtensions
    {
        public static LibreMetaverse.Vector3 ToLm(this Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
