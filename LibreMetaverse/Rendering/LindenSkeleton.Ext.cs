/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025-2026, Sjofn LLC.
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
using System.Xml;
using System.Xml.Serialization;

namespace OpenMetaverse.Rendering
{
    /// <summary>
    /// Indicates whether a skeleton joint is part of the original base avatar skeleton
    /// or the extended set added for fitted mesh, creature features, etc.
    /// </summary>
    public enum JointSupportCategory
    {
        /// <summary>Original Linden Lab base avatar skeleton bone.</summary>
        Base,
        /// <summary>Extended bone added for fitted mesh, tails, wings, and other creature features.</summary>
        Extended
    }

    /// <summary>
    /// Extension helpers for <see cref="JointBase"/>.
    /// </summary>
    public partial class JointBase
    {
        /// <summary>
        /// Returns the joint's support category derived from the <c>support</c> XML attribute.
        /// Bones without the attribute default to <see cref="JointSupportCategory.Base"/>.
        /// </summary>
        public JointSupportCategory SupportCategory =>
            string.Equals(support, "extended", StringComparison.OrdinalIgnoreCase)
                ? JointSupportCategory.Extended
                : JointSupportCategory.Base;
    }

    /// <summary>
    /// Extension helpers for <see cref="Joint"/>.
    /// </summary>
    public partial class Joint
    {
        /// <summary>
        /// Splits the space-separated <c>aliases</c> XML attribute into individual alias names.
        /// Returns an empty array when no aliases are defined.
        /// </summary>
        public string[] GetAliasesList()
        {
            if (string.IsNullOrWhiteSpace(aliases))
                return Array.Empty<string>();
            return aliases.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }

    /// <summary>
    /// load the 'avatar_skeleton.xml'
    /// </summary>
    /// <remarks>
    /// Partial class which extends the auto-generated 'LindenSkeleton.Xsd.cs'.eton.xsd
    /// </remarks>
    public partial class LindenSkeleton
   {
        /// <summary>
        /// Load a skeleton from a given file.
        /// </summary>
        /// <remarks>
        /// We use xml schema validation on top of the xml de-serializer, since the schema has
        /// some stricter checks than the de-serializer provides. E.g. the vector attributes
        /// are guaranteed to hold only 3 float values. This reduces the need for error checking
        /// while working with the loaded skeleton.
        /// </remarks>
        /// <returns>A valid recursive skeleton</returns>
        public static LindenSkeleton Load()
        {
            return Load(null);
        }

        /// <summary>
        /// Load a skeleton from a given file.
        /// </summary>
        /// <remarks>
        /// We use xml schema validation on top of the xml de-serializer, since the schema has
        /// some stricter checks than the de-serializer provides. E.g. the vector attributes
        /// are guaranteed to hold only 3 float values. This reduces the need for error checking
        /// while working with the loaded skeleton.
        /// </remarks>
        /// <param name="fileName">The path to the skeleton definition file</param>
        /// <returns>A valid recursive skeleton</returns>
        public static LindenSkeleton Load(string? fileName)
        {
            if (fileName == null)
                fileName = System.IO.Path.Combine(Settings.RESOURCE_DIR ?? string.Empty, "character", "avatar_skeleton.xml");

            LindenSkeleton result;

            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                CheckCharacters = false,
                IgnoreComments = true,
                IgnoreProcessingInstructions = false,
                DtdProcessing = DtdProcessing.Ignore
            };
            using (FileStream skeletonData = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(skeletonData, readerSettings))
            {
                XmlSerializer ser = new XmlSerializer(typeof(LindenSkeleton));
                result = (LindenSkeleton?)ser.Deserialize(reader) ?? new LindenSkeleton();
            }
            return result;
        }

        /// <summary>
        /// Build and "expanded" list of joints
        /// </summary>
        /// <remarks>
        /// The algorithm is based on this description:
        /// 
        /// >An "expanded" list of joints, not just a
        /// >linear array of the joints as defined in the skeleton file.
        /// >In particular, any joint that has more than one child will
        /// >be repeated in the list for each of its children.
        ///
        /// The modern avatar_skeleton.xml contains intermediate spine bones
        /// (mSpine1–mSpine4) that are NOT referenced by the LLM skin-joint list.
        /// The original algorithm used the actual skeleton parent name as a
        /// placeholder, which caused these intermediate bones to appear in the
        /// expanded list and shift all downstream joint indices.
        /// The fixed algorithm tracks the "effective parent" — the nearest ancestor
        /// that IS in the filter — and uses that as the placeholder instead.
        /// </remarks>
        /// <param name="jointsFilter">The list should only take these joint names in consideration</param>
        /// <returns>An "expanded" joints list as a flat list of bone names</returns>
        public List<string> BuildExpandedJointList(IEnumerable<string> jointsFilter)
        {
            List<string> expandedJointList = new List<string>();

            if (bone.bone == null) return expandedJointList;
            var filter = jointsFilter as string[] ?? jointsFilter.ToArray();
            foreach (Joint child in bone.bone)
                ExpandJoint(child, bone.name, expandedJointList, filter);

            return expandedJointList;
        }

        /// <summary>
        /// Enumerates every <see cref="Joint"/> in the skeleton tree in depth-first order.
        /// Each joint is returned exactly once; <see cref="CollisionVolume"/> entries are excluded.
        /// </summary>
        public IEnumerable<Joint> GetAllJoints() => EnumerateJointsDepthFirst(bone);

        private static IEnumerable<Joint> EnumerateJointsDepthFirst(Joint joint)
        {
            yield return joint;
            if (joint.bone == null) yield break;
            foreach (var child in joint.bone)
                foreach (var descendant in EnumerateJointsDepthFirst(child))
                    yield return descendant;
        }

        /// <summary>
        /// Builds a flat dictionary that maps every joint's canonical name <em>and</em> each of
        /// its aliases to the corresponding <see cref="Joint"/> instance.
        /// </summary>
        /// <returns>
        /// A dictionary with ordinal string comparison, keyed by joint name or alias.
        /// </returns>
        public Dictionary<string, Joint> BuildJointDictionary()
        {
            var dict = new Dictionary<string, Joint>(StringComparer.Ordinal);
            foreach (var joint in GetAllJoints())
            {
                if (!string.IsNullOrEmpty(joint.name))
                    dict[joint.name] = joint;
                foreach (var alias in joint.GetAliasesList())
                    if (!string.IsNullOrEmpty(alias))
                        dict[alias] = joint;
            }
            return dict;
        }

        /// <summary>
        /// Finds a joint by its canonical name or any of its aliases.
        /// </summary>
        /// <param name="nameOrAlias">The joint name or alias to search for.</param>
        /// <returns>The matching <see cref="Joint"/>, or <c>null</c> if not found.</returns>
        public Joint? GetBone(string nameOrAlias)
        {
            if (string.IsNullOrEmpty(nameOrAlias)) return null;
            return BuildJointDictionary().TryGetValue(nameOrAlias, out var joint) ? joint : null;
        }

        /// <summary>
        /// Expand one joint, tracking the nearest filter-ancestor as the effective parent.
        /// </summary>
        /// <param name="currentJoint">The joint we are supposed to expand</param>
        /// <param name="effectiveParentName">
        /// Name of the nearest ancestor that is present in <paramref name="jointsFilter"/>,
        /// or the skeleton root name when no such ancestor exists yet.
        /// </param>
        /// <param name="expandedJointList">Joint list that we will extend upon</param>
        /// <param name="jointsFilter">The expanded list should only contain these joints</param>
        private static void ExpandJoint(Joint currentJoint, string effectiveParentName, List<string> expandedJointList, string[] jointsFilter)
        {
            string nextEffectiveParent = effectiveParentName;

            // does the mesh reference this joint?
            if (jointsFilter.Contains(currentJoint.name))
            {
                if (expandedJointList.Count > 0 &&
                    effectiveParentName == expandedJointList[expandedJointList.Count - 1])
                    expandedJointList.Add(currentJoint.name);
                else
                {
                    expandedJointList.Add(effectiveParentName);
                    expandedJointList.Add(currentJoint.name);
                }
                // this joint becomes the effective parent for its children
                nextEffectiveParent = currentJoint.name;
            }

            // recurse the joint hierarchy
            if (currentJoint.bone == null) return;
            foreach (Joint child in currentJoint.bone)
                ExpandJoint(child, nextEffectiveParent, expandedJointList, jointsFilter);
        }
    }
}
