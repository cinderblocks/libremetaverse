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
using System.IO;
using LibreMetaverse.Rendering;
using NumMatrix = System.Numerics.Matrix4x4;

namespace LibreMetaverse.ImportExport
{
    /// <summary>Which joints of a file's skin are joints of the avatar, and what they are called there</summary>
    internal sealed class JointSelection
    {
        /// <summary>Avatar joint names, each once, in the order the weights index them</summary>
        public readonly List<string> Names = new List<string>();

        /// <summary>For each joint of the file's skin, its index in <see cref="Names"/>, or -1 if the avatar has no such joint</summary>
        public int[] Map = Array.Empty<int>();

        /// <summary>The joint of the file's skin that each entry of <see cref="Names"/> was taken from</summary>
        public readonly List<int> FirstOf = new List<int>();

        /// <summary>The names of the joints the avatar does not have</summary>
        public readonly List<string> Dropped = new List<string>();
    }

    /// <summary>The parts of reading a skin that do not depend on the file format</summary>
    internal static class RigImport
    {
        /// <summary>
        /// Matches a skin's joints against the avatar skeleton: aliases become the joint's own name, joints the
        /// avatar does not have are left out, and two joints that are the same avatar joint share the first.
        /// With no skeleton to check against every name is taken as it is.
        /// </summary>
        internal static JointSelection SelectJoints(IReadOnlyList<string> names, AvatarSkeletonIndex? avatar)
        {
            var result = new JointSelection { Map = new int[names.Count] };
            for (int j = 0; j < names.Count; j++)
            {
                string name = names[j] ?? string.Empty;
                string canonical = name;
                if (avatar != null && !avatar.TryGetCanonical(name, out canonical))
                {
                    result.Map[j] = -1;
                    result.Dropped.Add(name);
                    continue;
                }

                int existing = result.Names.IndexOf(canonical);
                if (existing >= 0)
                {
                    result.Map[j] = existing;
                    continue;
                }

                result.Map[j] = result.Names.Count;
                result.Names.Add(canonical);
                result.FirstOf.Add(j);
            }
            return result;
        }

        /// <summary>
        /// Reduces what pulls on a vertex to what Second Life keeps: the four strongest influences, adding up to one.
        /// An influence can be listed more than once (it counts for the sum), and one that is not positive is left out.
        /// </summary>
        /// <returns>False if nothing is left, in which case <paramref name="weight"/> follows the first joint</returns>
        internal static bool Reduce(List<KeyValuePair<int, float>> influences, out VertexWeight weight)
        {
            var merged = new List<KeyValuePair<int, float>>(influences.Count);
            foreach (var influence in influences)
            {
                if (!(influence.Value > 0f)) continue;
                int existing = merged.FindIndex(e => e.Key == influence.Key);
                if (existing >= 0)
                    merged[existing] = new KeyValuePair<int, float>(influence.Key, merged[existing].Value + influence.Value);
                else
                    merged.Add(influence);
            }

            merged.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (merged.Count > 4) merged.RemoveRange(4, merged.Count - 4);

            float total = 0f;
            foreach (var influence in merged) total += influence.Value;

            weight = new VertexWeight();
            if (merged.Count == 0 || !(total > 0f) || float.IsInfinity(total))
            {
                weight.Weight0 = 1f;
                return false;
            }

            weight.Joint0 = merged[0].Key; weight.Weight0 = merged[0].Value / total;
            if (merged.Count > 1) { weight.Joint1 = merged[1].Key; weight.Weight1 = merged[1].Value / total; }
            if (merged.Count > 2) { weight.Joint2 = merged[2].Key; weight.Weight2 = merged[2].Value / total; }
            if (merged.Count > 3) { weight.Joint3 = merged[3].Key; weight.Weight3 = merged[3].Value / total; }
            return true;
        }

        /// <summary>Row-major floats for each matrix, as the skin block of a mesh asset has them</summary>
        /// <exception cref="InvalidDataException">A value is not finite</exception>
        internal static float[] Flatten(IEnumerable<NumMatrix> matrices, string source)
        {
            var result = new List<float>();
            foreach (var m in matrices)
            {
                var values = new[]
                {
                    m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
                };
                foreach (var value in values)
                {
                    if (!Utils.IsFinite(value))
                        throw new InvalidDataException(source + " gives an inverse bind matrix that is not finite");
                }
                result.AddRange(values);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Like <see cref="OnAvatarAxes"/>, but a joint keeps the scale it was bound with. Fitted mesh is bound to
        /// collision volumes at the shape the wearer had, and that shape is the volumes' scale, so replacing it
        /// with the default would fit the mesh to the wrong avatar. Only the rotation is the avatar's.
        /// </summary>
        internal static NumMatrix OnAvatarRotation(string joint, NumMatrix bindWorld, out bool ok)
        {
            var defaults = AvatarSkeletonIndex.Default?.DefaultWorld;
            var rotation = System.Numerics.Quaternion.Identity;
            if (defaults != null && defaults.TryGetValue(joint, out var atRest) &&
                !NumMatrix.Decompose(atRest, out _, out rotation, out _))
            {
                ok = false;
                return NumMatrix.Identity;
            }

            if (!NumMatrix.Decompose(bindWorld, out var scale, out _, out _))
            {
                ok = false;
                return NumMatrix.Identity;
            }

            var world = NumMatrix.CreateScale(scale) * NumMatrix.CreateFromQuaternion(rotation);
            world.M41 = bindWorld.M41;
            world.M42 = bindWorld.M42;
            world.M43 = bindWorld.M43;
            ok = NumMatrix.Invert(world, out var inverse);
            return inverse;
        }

        /// <summary>
        /// The same joints rebuilt with the avatar's own axes: each keeps the position it has in the bind pose
        /// (<paramref name="bindWorld"/> is where its inverse bind matrix puts it), and has the rotation and scale
        /// the avatar's joint has. Bones have none of either; collision volumes, which fitted mesh is weighted
        /// to, have both.
        /// </summary>
        internal static NumMatrix OnAvatarAxes(string joint, NumMatrix bindWorld, out bool ok)
        {
            var avatarWorld = NumMatrix.CreateTranslation(bindWorld.M41, bindWorld.M42, bindWorld.M43);
            var defaults = AvatarSkeletonIndex.Default?.DefaultWorld;
            if (defaults != null && defaults.TryGetValue(joint, out var atRest))
            {
                avatarWorld = atRest;
                avatarWorld.M41 = bindWorld.M41;
                avatarWorld.M42 = bindWorld.M42;
                avatarWorld.M43 = bindWorld.M43;
            }
            ok = NumMatrix.Invert(avatarWorld, out var inverse);
            return inverse;
        }
    }
}
