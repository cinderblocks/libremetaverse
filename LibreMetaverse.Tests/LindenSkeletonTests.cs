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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenMetaverse.Rendering;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Unit tests for <see cref="LindenSkeleton.BuildExpandedJointList"/>.
    /// These exercise the effective-parent tracking fix: intermediate bones that are
    /// NOT present in the skin-joints filter must not appear as placeholder entries in
    /// the expanded list, which would shift all downstream joint indices.
    /// </summary>
    [TestFixture]
    [Category("LindenSkeleton")]
    public class LindenSkeletonTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static Joint MakeJoint(string name, params Joint[] children)
        {
            return new Joint { name = name, bone = children };
        }

        private static LindenSkeleton MakeSkeleton(string rootName, params Joint[] rootChildren)
        {
            return new LindenSkeleton { bone = MakeJoint(rootName, rootChildren) };
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void BuildExpandedJointList_EmptyFilter_ReturnsEmptyList()
        {
            var skeleton = MakeSkeleton("mPelvis",
                MakeJoint("mTorso",
                    MakeJoint("mChest")));

            var result = skeleton.BuildExpandedJointList(new string[0]);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildExpandedJointList_NoBoneChildren_ReturnsEmptyList()
        {
            var skeleton = new LindenSkeleton { bone = new Joint { name = "mPelvis", bone = null } };

            var result = skeleton.BuildExpandedJointList(new[] { "mPelvis" });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildExpandedJointList_LinearChain_AllInFilter_ProducesCorrectSequence()
        {
            // root -> A -> B -> C, all three in filter
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("B",
                        MakeJoint("C"))));

            var result = skeleton.BuildExpandedJointList(new[] { "A", "B", "C" });

            // root is not in the filter so no bridge entry is emitted; the filter-present bones chain directly
            Assert.That(result, Is.EqualTo(new List<string> { "A", "B", "C" }));
        }

        [Test]
        public void BuildExpandedJointList_IntermediateBoneNotInFilter_IsSkippedAsPlaceholder()
        {
            // root -> A -> B(not in filter) -> C
            // Before the fix, "B" appeared as C's placeholder, corrupting all subsequent
            // joint indices in the skin-weight lookup table.
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("B",
                        MakeJoint("C"))));

            var result = skeleton.BuildExpandedJointList(new[] { "A", "C" });

            Assert.That(result, Does.Not.Contain("B"),
                "Intermediate bone absent from filter must not appear as a placeholder");
            Assert.That(result, Is.EqualTo(new List<string> { "A", "C" }));
        }

        [Test]
        public void BuildExpandedJointList_MultipleConsecutiveIntermediates_AllSkipped()
        {
            // root -> A -> inter1 -> inter2 -> B
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("inter1",
                        MakeJoint("inter2",
                            MakeJoint("B")))));

            var result = skeleton.BuildExpandedJointList(new[] { "A", "B" });

            Assert.That(result, Does.Not.Contain("inter1"));
            Assert.That(result, Does.Not.Contain("inter2"));
            Assert.That(result, Is.EqualTo(new List<string> { "A", "B" }));
        }

        [Test]
        public void BuildExpandedJointList_BranchingHierarchy_BranchPointRepeatedPerChild()
        {
            // root -> A -> [B -> C, D]
            // A has two children; its name must be re-emitted before D's subtree.
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("B",
                        MakeJoint("C")),
                    MakeJoint("D")));

            var result = skeleton.BuildExpandedJointList(new[] { "A", "B", "C", "D" });

            // First child branch:  A, B, C
            // Second child branch: A (repeated placeholder), D
            Assert.That(result, Is.EqualTo(new List<string> { "A", "B", "C", "A", "D" }));
        }

        [Test]
        public void BuildExpandedJointList_BranchingWithIntermediates_EffectiveParentUsedForAllBranches()
        {
            // root -> A -> [B(not in filter) -> C, D]
            // The effective parent for both C and D is A (B is absent from filter).
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("B",
                        MakeJoint("C")),
                    MakeJoint("D")));

            var result = skeleton.BuildExpandedJointList(new[] { "A", "C", "D" });

            Assert.That(result, Does.Not.Contain("B"),
                "Intermediate bone absent from filter must not appear in the expanded list");
            Assert.That(result, Is.EqualTo(new List<string> { "A", "C", "A", "D" }));
        }

        [Test]
        public void BuildExpandedJointList_OnlyLeafInFilter_ReachableViaRootPlaceholder()
        {
            // root -> inter1 -> inter2 -> leaf (only leaf is in filter)
            // Root and intermediates are not in the filter, so no bridge entries are emitted.
            var skeleton = MakeSkeleton("root",
                MakeJoint("inter1",
                    MakeJoint("inter2",
                        MakeJoint("leaf"))));

            var result = skeleton.BuildExpandedJointList(new[] { "leaf" });

            Assert.That(result, Is.EqualTo(new List<string> { "leaf" }));
        }

        [Test]
        public void BuildExpandedJointList_SingleFilteredDirectChild_ProducesTwoEntries()
        {
            // root -> A (A is the only child and the only filter entry)
            var skeleton = MakeSkeleton("root",
                MakeJoint("A"));

            var result = skeleton.BuildExpandedJointList(new[] { "A" });

            Assert.That(result, Is.EqualTo(new List<string> { "A" }));
        }

        [Test]
        public void BuildExpandedJointList_SpineBonesSkipped_DownstreamIndicesNotShifted()
        {
            // Mirrors the real-world avatar_skeleton.xml case that motivated the fix:
            // mPelvis -> mTorso -> mSpine1(intermediate) -> mChest -> mNeck -> mHead
            // Only mTorso, mChest, and mHead are referenced by a hypothetical mesh.
            var skeleton = MakeSkeleton("mPelvis",
                MakeJoint("mTorso",
                    MakeJoint("mSpine1",
                        MakeJoint("mChest",
                            MakeJoint("mNeck",
                                MakeJoint("mHead"))))));

            var result = skeleton.BuildExpandedJointList(new[] { "mTorso", "mChest", "mHead" });

            Assert.That(result, Does.Not.Contain("mSpine1"),
                "mSpine1 is not in the filter and must not appear as a placeholder");
            Assert.That(result, Does.Not.Contain("mNeck"),
                "mNeck is not in the filter and must not appear as a placeholder");
            Assert.That(result, Is.EqualTo(
                new List<string> { "mTorso", "mChest", "mHead" }));
        }

        // ── JointBase.SupportCategory ──────────────────────────────────────────

        [Test]
        public void JointBase_SupportCategory_ReturnsBase_WhenAttributeIsBase()
        {
            var joint = new Joint { name = "mTorso", support = "base" };
            Assert.That(joint.SupportCategory, Is.EqualTo(JointSupportCategory.Base));
        }

        [Test]
        public void JointBase_SupportCategory_ReturnsExtended_WhenAttributeIsExtended()
        {
            var joint = new Joint { name = "mTail1", support = "extended" };
            Assert.That(joint.SupportCategory, Is.EqualTo(JointSupportCategory.Extended));
        }

        [Test]
        public void JointBase_SupportCategory_DefaultsToBase_WhenAttributeIsMissing()
        {
            var joint = new Joint { name = "mTest" };
            Assert.That(joint.SupportCategory, Is.EqualTo(JointSupportCategory.Base));
        }

        // ── Joint.GetAliasesList ───────────────────────────────────────────────

        [Test]
        public void Joint_GetAliasesList_ReturnsSplitAliases_WhenAttributeIsSet()
        {
            var joint = new Joint { name = "mPelvis", aliases = "hip avatar_mPelvis" };
            Assert.That(joint.GetAliasesList(), Is.EqualTo(new[] { "hip", "avatar_mPelvis" }));
        }

        [Test]
        public void Joint_GetAliasesList_ReturnsEmpty_WhenAttributeIsEmpty()
        {
            var joint = new Joint { name = "mHead" };
            Assert.That(joint.GetAliasesList(), Is.Empty);
        }

        // ── LindenSkeleton.GetAllJoints ────────────────────────────────────────

        [Test]
        public void GetAllJoints_ReturnsAllJointsDepthFirst()
        {
            var skeleton = MakeSkeleton("root",
                MakeJoint("A",
                    MakeJoint("B")),
                MakeJoint("C"));

            var names = skeleton.GetAllJoints().Select(j => j.name).ToList();

            Assert.That(names, Is.EqualTo(new[] { "root", "A", "B", "C" }));
        }

        // ── LindenSkeleton.BuildJointDictionary ────────────────────────────────

        [Test]
        public void BuildJointDictionary_ContainsCanonicalNamesAndAliases()
        {
            var child = new Joint { name = "mPelvis", aliases = "hip avatar_mPelvis" };
            var root = new Joint { name = "root", bone = new[] { child } };
            var skeleton = new LindenSkeleton { bone = root };

            var dict = skeleton.BuildJointDictionary();

            Assert.That(dict.ContainsKey("root"), Is.True);
            Assert.That(dict.ContainsKey("mPelvis"), Is.True);
            Assert.That(dict.ContainsKey("hip"), Is.True);
            Assert.That(dict.ContainsKey("avatar_mPelvis"), Is.True);
            Assert.That(dict["hip"], Is.SameAs(dict["mPelvis"]));
        }

        // ── LindenSkeleton.GetBone ─────────────────────────────────────────────

        [Test]
        public void GetBone_ReturnsBone_ByCanonicalName()
        {
            var skeleton = MakeSkeleton("root", MakeJoint("mTorso"));
            var result = skeleton.GetBone("mTorso");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.name, Is.EqualTo("mTorso"));
        }

        [Test]
        public void GetBone_ReturnsBone_ByAlias()
        {
            var child = new Joint { name = "mPelvis", aliases = "hip" };
            var root = new Joint { name = "root", bone = new[] { child } };
            var skeleton = new LindenSkeleton { bone = root };

            var result = skeleton.GetBone("hip");

            Assert.That(result, Is.Not.Null);
            Assert.That(result.name, Is.EqualTo("mPelvis"));
        }

        [Test]
        public void GetBone_ReturnsNull_ForUnknownName()
        {
            var skeleton = MakeSkeleton("root", MakeJoint("mTorso"));
            Assert.That(skeleton.GetBone("nonexistent"), Is.Null);
        }
    }
}
