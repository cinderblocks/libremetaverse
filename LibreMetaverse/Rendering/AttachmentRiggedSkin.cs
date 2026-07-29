/*
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

namespace LibreMetaverse.Rendering
{
    using Vector3 = System.Numerics.Vector3;

    /// <summary>
    /// Rigged / fitted mesh skinning data for a single attachment face.
    /// When non-null, the caller must render the face in avatar-local space
    /// (its render-face transform is already Identity and its vertices are in mesh
    /// bind-space) and drive animation through the supplied joint names + inverse bind
    /// matrices + per-vertex weights.
    /// </summary>
    public sealed class AttachmentRiggedSkin
    {
        public string[]                     JointNames      = [];
        public System.Numerics.Matrix4x4[]  InvBindMatrices = [];
        /// <summary>
        /// Interleaved per-vertex joint indices: <c>Joints[vi * 4 + k]</c> is influence k of vertex vi.
        /// </summary>
        public int[]       Joints  = [];
        /// <summary>
        /// Interleaved per-vertex weights: <c>Weights[vi * 4 + k]</c> is weight k of vertex vi.
        /// </summary>
        public float[]     Weights = [];
        /// <summary>Mesh asset UUID (<c>prim.Sculpt.SculptTexture</c>) — the tiebreak key used by
        /// SL's own attachment-override conflict resolution (highest UUID wins per joint).</summary>
        public UUID        MeshId  = UUID.Zero;
        /// <summary>
        /// Joint-position-override candidates this mesh's own skin block offers, raw and
        /// unfiltered against the avatar skeleton (see
        /// <see cref="RiggedSkinMath.ExtractJointPositionOverrides"/>). Empty when the mesh
        /// carries no (valid) AltInverseBindMatrices data.
        /// </summary>
        public (string JointName, Vector3 Position)[] JointPositionOverrides = [];
        /// <summary>Mirrors <see cref="MeshSkinData.LockScaleIfJointPosition"/> for this mesh.</summary>
        public bool        LockScaleIfJointPosition;
    }

    /// <summary>
    /// Pure data/math helpers for turning a mesh asset's raw <see cref="MeshSkinData"/> into
    /// the per-vertex inputs a rigged-mesh LBS skinning loop needs. No rendering-backend
    /// dependency (no OpenGL, no UI framework) — usable by any renderer.
    /// </summary>
    public static class RiggedSkinMath
    {
        /// <summary>Converts a 16-element row-major float array to a Matrix4x4.</summary>
        public static System.Numerics.Matrix4x4 FloatsToMatrix(float[] f)
        {
            if (f == null || f.Length < 16) return System.Numerics.Matrix4x4.Identity;
            return new System.Numerics.Matrix4x4(
                f[ 0], f[ 1], f[ 2], f[ 3],
                f[ 4], f[ 5], f[ 6], f[ 7],
                f[ 8], f[ 9], f[10], f[11],
                f[12], f[13], f[14], f[15]);
        }

        /// <summary>
        /// Builds the per-joint inverse bind matrix array used for rigged skinning.
        /// The SL mesh format supplies one 4×4 (16 floats, row-major, row-vector) per joint.
        /// Always uses the regular <see cref="MeshSkinData.InverseBindMatrices"/>, never
        /// <see cref="MeshSkinData.AltInverseBindMatrices"/>.
        /// </summary>
        /// <remarks>
        /// A previous version of this method preferred AltInverseBindMatrices whenever present,
        /// reasoning (by analogy, not verified) that SL had a "use_alt_ibm" branch for meshes with
        /// joint-position overrides. Verified against the actual SL viewer source instead of
        /// guessing (indra/newview/llvoavatar.cpp, indra/llprimitive/llmodel.h/.cpp,
        /// indra/llprimitive/lldaeloader.cpp — searched via GitHub code search): mAlternateBindMatrix
        /// is referenced in exactly one runtime consumer, LLVOAvatar::addAttachmentOverridesForObject,
        /// which reads ONLY its translation component to seed a joint-position override applied to
        /// the *shared avatar skeleton* (see ExtractJointPositionOverrides below). It is never used
        /// to skin the mesh's own vertices — every mesh, override-carrying or not, always skins
        /// against the regular inverse bind matrix in real SL. AltInverseBindMatrices is a
        /// position-only side channel, not a usable substitute inverse-bind matrix: composing it as
        /// one (as this method previously did) feeds a matrix whose rotation/scale components were
        /// never meant to be inverted-and-composed that way into the LBS formula, which plausibly
        /// explains "fanned spike" artifacts on override-carrying content (a symptom independently
        /// observed and initially unexplained before this was found).
        /// </remarks>
        public static System.Numerics.Matrix4x4[] BuildInvBindMatrices(MeshSkinData skin)
        {
            int n = skin.JointNames.Length;
            var result = new System.Numerics.Matrix4x4[n];

            var raw  = skin.InverseBindMatrices;
            int have = raw.Length / 16;

            for (int i = 0; i < n; i++)
            {
                if (i < have)
                {
                    int b = i * 16;
                    result[i] = new System.Numerics.Matrix4x4(
                        raw[b    ], raw[b + 1], raw[b + 2], raw[b + 3],
                        raw[b + 4], raw[b + 5], raw[b + 6], raw[b + 7],
                        raw[b + 8], raw[b + 9], raw[b +10], raw[b +11],
                        raw[b +12], raw[b +13], raw[b +14], raw[b +15]);
                }
                else
                {
                    result[i] = System.Numerics.Matrix4x4.Identity;
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts joint-position-override candidates from a mesh's own skin block, mirroring
        /// <c>LLVOAvatar::addAttachmentOverridesForObject</c> (indra/newview/llvoavatar.cpp) as
        /// verified against the SL viewer source: real SL applies these to the *shared* avatar
        /// skeleton (not just to the overriding mesh's own skinning, which is all
        /// <see cref="BuildInvBindMatrices"/> above does) so that the classic body and every other
        /// worn attachment agree on where an overridden joint actually is.
        ///
        /// Only extracts raw (JointName, Position) candidates — no threshold-filtering against the
        /// avatar's default skeleton and no cross-attachment conflict resolution here; both need
        /// data (the skeleton, and every other worn attachment) this method doesn't have. That
        /// happens once, avatar-wide, in the caller (a full-avatar mesh build pass).
        /// </summary>
        public static (string JointName, Vector3 Position)[] ExtractJointPositionOverrides(MeshSkinData skin)
        {
            int n = skin.JointNames.Length;
            if (n < 1) return [];

            // SL: bindCnt != jointCnt -> "invalid mesh... joint overrides will be ignored" —
            // the mesh's *entire* override set is dropped, not applied partially.
            int altCount = skin.AltInverseBindMatrices.Length / 16;
            if (altCount != n) return [];

            var result = new List<(string, Vector3)>(n);
            for (int i = 0; i < n; i++)
            {
                var jointName = skin.JointNames[i];
                if (string.IsNullOrEmpty(jointName)) continue;

                // Translation only, taken directly from the raw parsed matrix — SL does not
                // invert this despite the "inverse bind matrix" name: addAttachmentOverridesForObject
                // reads pSkinData->mAlternateBindMatrix[i].getTranslation() with no Invert() call.
                int b = i * 16;
                var pos = new Vector3(
                    skin.AltInverseBindMatrices[b + 12],
                    skin.AltInverseBindMatrices[b + 13],
                    skin.AltInverseBindMatrices[b + 14]);
                result.Add((jointName, pos));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Clamps/zeroes out-of-range influences and renormalizes the 4 weights to sum to 1.
        /// If all 4 are invalid, force-binds to joint index 0 with full weight.
        /// </summary>
        public static void NormalizeSkinWeights(
            int jointCount,
            ref int j0, ref float w0,
            ref int j1, ref float w1,
            ref int j2, ref float w2,
            ref int j3, ref float w3)
        {
            if ((uint)j0 >= (uint)jointCount) w0 = 0f;
            if ((uint)j1 >= (uint)jointCount) w1 = 0f;
            if ((uint)j2 >= (uint)jointCount) w2 = 0f;
            if ((uint)j3 >= (uint)jointCount) w3 = 0f;

            w0 = Clamp01(w0);
            w1 = Clamp01(w1);
            w2 = Clamp01(w2);
            w3 = Clamp01(w3);

            float sum = w0 + w1 + w2 + w3;
            if (sum > 1e-6f)
            {
                float inv = 1f / sum;
                w0 *= inv; w1 *= inv; w2 *= inv; w3 *= inv;
                return;
            }

            j0 = 0; j1 = 0; j2 = 0; j3 = 0;
            w0 = jointCount > 0 ? 1f : 0f;
            w1 = 0f; w2 = 0f; w3 = 0f;
        }

        // net481 (this project's oldest target) has no Math.Clamp(float,float,float).
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
