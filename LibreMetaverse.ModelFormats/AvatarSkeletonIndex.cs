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
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// What the loaders and the exporter need to know about the default avatar skeleton: which names are
    /// joints (and what their canonical names are, since a mesh may use an alias), what hangs from what, and
    /// where the joints sit in the default pose.
    /// </summary>
    internal sealed class AvatarSkeletonIndex
    {
        private static readonly Lazy<AvatarSkeletonIndex?> DefaultIndex = new Lazy<AvatarSkeletonIndex?>(Build);

        private readonly Dictionary<string, string> _canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _parent = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The index of the default avatar skeleton, or null if it could not be loaded</summary>
        internal static AvatarSkeletonIndex? Default => DefaultIndex.Value;

        /// <summary>The name of the joint all the others hang from</summary>
        internal string RootName { get; private set; } = string.Empty;

        /// <summary>Where each joint (by canonical name and alias) is in the default pose, in avatar space (row vector)</summary>
        internal Dictionary<string, NumMatrix> DefaultWorld { get; private set; } = new Dictionary<string, NumMatrix>(StringComparer.Ordinal);

        /// <summary>The canonical name of a joint, collision volume or alias, if the skeleton has one by that name</summary>
        internal bool TryGetCanonical(string name, out string canonical)
        {
            if (name != null && _canonical.TryGetValue(name, out var found))
            {
                canonical = found;
                return true;
            }
            canonical = string.Empty;
            return false;
        }

        /// <summary>The canonical name of the joint a (canonically named) joint hangs from</summary>
        internal bool TryGetParent(string canonicalName, out string parent)
        {
            return _parent.TryGetValue(canonicalName, out parent!);
        }

        private static AvatarSkeletonIndex? Build()
        {
            try
            {
                var skeleton = LindenSkeleton.Load();
                var index = new AvatarSkeletonIndex
                {
                    RootName = skeleton.bone.name,
                    DefaultWorld = AvatarBoneMath.BuildBoneWorldMatrices(skeleton, new Dictionary<string, BoneTransform>())
                };
                index.Add(skeleton.bone, null);
                return index;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not load the avatar skeleton: {ex.Message}");
                return null;
            }
        }

        private void Add(Joint joint, string? parent)
        {
            if (string.IsNullOrEmpty(joint.name)) return;

            _canonical[joint.name] = joint.name;
            foreach (var alias in joint.GetAliasesList())
            {
                if (!_canonical.ContainsKey(alias)) _canonical[alias] = joint.name;
            }
            if (parent != null) _parent[joint.name] = parent;

            // Collision volumes are joints too, as far as fitted mesh is concerned
            foreach (var volume in joint.collision_volume ?? Array.Empty<CollisionVolume>())
            {
                if (volume == null || string.IsNullOrEmpty(volume.name)) continue;
                _canonical[volume.name] = volume.name;
                _parent[volume.name] = joint.name;
            }
            foreach (var child in joint.bone ?? Array.Empty<Joint>())
            {
                Add(child, joint.name);
            }
        }
    }
}
