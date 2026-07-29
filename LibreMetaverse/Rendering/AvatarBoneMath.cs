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
    using Vector3    = System.Numerics.Vector3;
    using Quaternion = System.Numerics.Quaternion;
    using Matrix4x4  = System.Numerics.Matrix4x4;

    /// <summary>
    /// Computes avatar skeleton bone world matrices from VP-shaped local transforms,
    /// matching the real SL viewer's <c>LLXform::update</c> model (verified against
    /// <c>indra/llmath/xform.cpp</c> and <c>indra/llcharacter/lljoint.cpp</c>), NOT naive
    /// hierarchical matrix multiplication (<c>world = local * parentWorld</c>).
    /// <para>
    /// The difference matters once any joint carries non-unit VP scale: SL never
    /// compounds scale across generations — each joint's final matrix uses ONLY its own
    /// scale — while a parent's scale affects only where children are positioned
    /// (<c>LLJoint</c> sets <c>mScaleChildOffset=true</c> unconditionally in
    /// <c>LLJoint::init()</c>). A naive matrix-chain implementation entangles every
    /// ancestor's scale AND rotation into each descendant's effective transform; that
    /// entanglement is harmless when rotation deltas are near-identity (T-pose) but
    /// produces severe shear/collapse once real rotation is applied (A-pose, live
    /// animation) — exactly the symptom that motivated this model (fitted mesh on an
    /// elongated shape: correct in T-pose, collapsed to spikes in A-pose).
    /// </para>
    /// <para>
    /// SL's model (the <c>mScaleChildOffset</c> path, the only one <c>LLJoint</c> uses):
    /// <code>
    /// worldRotation = localRotation * parentWorldRotation      (pure rotation, scale-free)
    /// worldPosition = Rotate(localPosition * parentOwnScale, parentWorldRotation) + parentWorldPosition
    /// worldMatrix   = Scale(ownScale) * Rotate(worldRotation) * Translate(worldPosition)
    /// </code>
    /// — using this joint's OWN scale only, never an ancestor's.
    /// </para>
    /// </summary>
    public static class AvatarBoneMath
    {
        /// <summary>
        /// Returns a copy of <paramref name="m"/> with the scale removed from the upper-left 3×3,
        /// leaving only rotation and translation. Used for attachment placement so VP bone scale
        /// (which adjusts skeleton proportions) does not stretch rigid attachment geometry.
        /// </summary>
        public static Matrix4x4 StripScale(Matrix4x4 m)
        {
            var r0 = new Vector3(m.M11, m.M12, m.M13);
            if (r0.LengthSquared() > 1e-10f) r0 = Vector3.Normalize(r0);
            var r1 = new Vector3(m.M21, m.M22, m.M23);
            if (r1.LengthSquared() > 1e-10f) r1 = Vector3.Normalize(r1);
            var r2 = new Vector3(m.M31, m.M32, m.M33);
            if (r2.LengthSquared() > 1e-10f) r2 = Vector3.Normalize(r2);
            return new Matrix4x4(
                r0.X, r0.Y, r0.Z, 0f,
                r1.X, r1.Y, r1.Z, 0f,
                r2.X, r2.Y, r2.Z, 0f,
                m.M41, m.M42, m.M43, m.M44);
        }

        /// <summary>
        /// Computes how far above the avatar's true ground/feet position the SL network
        /// position (<c>Avatar.Position.Z</c> / <c>AgentManager.SimPosition.Z</c>) sits.
        /// <para>
        /// SL's avatar position is <b>not</b> feet height — it's offset upward by roughly the
        /// pelvis-to-head-top distance. The skeleton's own leg chain (mHip → mKnee → mAnkle →
        /// mFoot) already returns to ~0 when the root (mPelvis) is placed at the network
        /// position, so without this correction the whole avatar renders floating by that
        /// same amount. Matches classic Radegast's <c>RenderAvatar.UpdateSize</c> /
        /// <c>AdjustedPosition</c> (Radegast/GUI/Rendering/RenderAvatar.cs), which computes
        /// this per-avatar from the same VP-morphed bone positions and subtracts it before
        /// placing the OpenGL avatar transform.
        /// </para>
        /// Returns the classic-viewer fallback of 1.0m (its default Height=2.0/PelvisToFoot=1.0)
        /// when any required bone is missing.
        /// </summary>
        public static float ComputeGroundAdjustment(Dictionary<string, BoneTransform>? boneTransforms)
        {
            const float FallbackAdjustment = 1.0f;
            const float Sqrt2 = 1.4142135623730950488016887242097f;

            if (boneTransforms == null ||
                !boneTransforms.TryGetValue("mPelvis",   out var pelvis) ||
                !boneTransforms.TryGetValue("mSkull",    out var skull) ||
                !boneTransforms.TryGetValue("mNeck",     out var neck) ||
                !boneTransforms.TryGetValue("mChest",    out var chest) ||
                !boneTransforms.TryGetValue("mHead",     out var head) ||
                !boneTransforms.TryGetValue("mTorso",    out var torso) ||
                !boneTransforms.TryGetValue("mHipLeft",  out var hip) ||
                !boneTransforms.TryGetValue("mKneeLeft", out var knee) ||
                !boneTransforms.TryGetValue("mAnkleLeft",out var ankle) ||
                !boneTransforms.TryGetValue("mFootLeft", out var foot))
                return FallbackAdjustment;

            float pelvisToFoot = hip.Position.Z   * pelvis.Scale.Z
                                - knee.Position.Z  * hip.Scale.Z
                                - ankle.Position.Z * knee.Scale.Z
                                - foot.Position.Z  * ankle.Scale.Z;

            float height = pelvisToFoot
                         + Sqrt2 * (skull.Position.Z * head.Scale.Z)
                         + head.Position.Z  * neck.Scale.Z
                         + neck.Position.Z  * chest.Scale.Z
                         + chest.Position.Z * torso.Scale.Z
                         + torso.Position.Z * pelvis.Scale.Z;

            float adjustment = height - pelvisToFoot;
            bool isFinite = !float.IsNaN(adjustment) && !float.IsInfinity(adjustment);
            return isFinite && adjustment > 0f ? adjustment : FallbackAdjustment;
        }

        /// <summary>
        /// Builds the default (bind-pose) bone world matrices from the skeleton's own T-pose
        /// joint positions/scales, with no VP deformation applied — <paramref name="boneTransforms"/>
        /// is typically an empty dictionary for this call.
        /// </summary>
        public static Dictionary<string, Matrix4x4> BuildBoneWorldMatrices(
            LindenSkeleton                    skeleton,
            Dictionary<string, BoneTransform> boneTransforms)
        {
            var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            BuildBoneMatricesRecursive(skeleton.bone, Quaternion.Identity, Vector3.Zero, Vector3.One,
                boneTransforms, result);
            return result;
        }

        // Caches the split alias arrays for each Joint instance so aliases.Split is never
        // called more than once per distinct Joint object across the lifetime of the process.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Joint, string[]>
            s_aliasCache = new();

        // net481 (this project's oldest target) has no Dictionary<,>.TryAdd.
        private static void TryAddOnce(Dictionary<string, Matrix4x4> dict, string key, Matrix4x4 value)
        {
            if (!dict.ContainsKey(key)) dict[key] = value;
        }

        private static void BuildBoneMatricesRecursive(
            Joint                             joint,
            Quaternion                        parentWorldRot,
            Vector3                           parentWorldPos,
            Vector3                           parentOwnScale,
            Dictionary<string, BoneTransform> boneTransforms,
            Dictionary<string, Matrix4x4>     result,
            IReadOnlyDictionary<string, Quaternion>? rotDeltas = null,
            bool                              applyVpScale = true)
        {
            var (localPos, localRot, ownScale) =
                GetLocalPRS(joint, boneTransforms, rotDeltas, applyVpScale);

            // Quaternion multiplication order matters here: System.Numerics.Quaternion's
            // operator* follows the standard Hamilton-product convention (q1*q2 applied to
            // a vector means "apply q2 first, then q1"), which is the REVERSE of this
            // codebase's row-vector Matrix4x4 convention (M1*M2 means "apply M1 first, then
            // M2"). To get "apply localRot first, then parentWorldRot" — matching the
            // existing local*parentWorld matrix convention — the correct combination is
            // parentWorldRot * localRot, not localRot * parentWorldRot. Verified by
            // AvatarBoneMathTests (unit-scale output must exactly match the old naive
            // matrix-chain implementation).
            var worldRot = parentWorldRot * localRot;
            var worldPos = Vector3.Transform(localPos * parentOwnScale, parentWorldRot) + parentWorldPos;
            var world    = Matrix4x4.CreateScale(ownScale)
                         * Matrix4x4.CreateFromQuaternion(worldRot)
                         * Matrix4x4.CreateTranslation(worldPos);

            if (!string.IsNullOrEmpty(joint.name))
                result[joint.name] = world;

            // Use cached split — Joint objects are loaded once at startup so this
            // eliminates the per-frame string[] allocation that was the #1 hotspot.
            if (!string.IsNullOrEmpty(joint.aliases))
            {
                var aliases = s_aliasCache.GetValue(joint,
                    j => j.aliases.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (var alias in aliases)
                    if (!string.IsNullOrEmpty(alias))
                        TryAddOnce(result, alias, world);
            }

            foreach (var cv in joint.collision_volume ?? [])
            {
                if (cv == null) continue;
                // Collision volumes are fitted-mesh bones (LLViewerJointCollisionVolume
                // extends LLJoint in SL, so the same mScaleChildOffset model applies with
                // the owning joint as "parent"). Built through the same corrected model so
                // VP position/scale deformations drive attachments exactly as they drive
                // the live avatar skeleton, without the scale-compounding bug.
                var (cvPos, cvRot, cvScale) = GetCvLocalPRS(cv, boneTransforms);
                var cvWorldRot = worldRot * cvRot;
                var cvWorldPos = Vector3.Transform(cvPos * ownScale, worldRot) + worldPos;
                var cvWorld    = Matrix4x4.CreateScale(cvScale)
                               * Matrix4x4.CreateFromQuaternion(cvWorldRot)
                               * Matrix4x4.CreateTranslation(cvWorldPos);
                if (!string.IsNullOrEmpty(cv.name))
                    TryAddOnce(result, cv.name, cvWorld);
            }

            foreach (var child in joint.bone ?? [])
            {
                if (child == null) continue;
                BuildBoneMatricesRecursive(child, worldRot, worldPos, ownScale,
                    boneTransforms, result, rotDeltas, applyVpScale);
            }
        }

        /// <summary>Local (parent-relative) position, rotation, and own-scale for a joint.</summary>
        private static (Vector3 Pos, Quaternion Rot, Vector3 Scale) GetLocalPRS(
            JointBase                         joint,
            Dictionary<string, BoneTransform> boneTransforms,
            IReadOnlyDictionary<string, Quaternion>? rotDeltas,
            bool                              applyVpScale)
        {
            if (string.IsNullOrEmpty(joint.name))
                return (Vector3.Zero, Quaternion.Identity, Vector3.One);

            Vector3 pos, scale;
            if (boneTransforms.TryGetValue(joint.name, out var bt))
            {
                pos   = new Vector3(bt.Position.X, bt.Position.Y, bt.Position.Z);
                // When applyVpScale is false, use the XML default scale instead of the
                // VP-computed one (currently always true for both body and attachment
                // paths — see ComputeAttachmentBoneWorldMatrices's doc comment for why the
                // attachment path no longer disables this).
                scale = applyVpScale
                    ? new Vector3(bt.Scale.X, bt.Scale.Y, bt.Scale.Z)
                    : (joint.scale?.Length >= 3
                        ? new Vector3(joint.scale[0], joint.scale[1], joint.scale[2])
                        : Vector3.One);
            }
            else
            {
                pos   = joint.pos?.Length   >= 3
                    ? new Vector3(joint.pos[0],   joint.pos[1],   joint.pos[2])
                    : Vector3.Zero;
                scale = joint.scale?.Length >= 3
                    ? new Vector3(joint.scale[0], joint.scale[1], joint.scale[2])
                    : Vector3.One;
            }

            float rx = joint.rot?.Length > 0 ? joint.rot[0] * ((float)Math.PI / 180f) : 0f;
            float ry = joint.rot?.Length > 1 ? joint.rot[1] * ((float)Math.PI / 180f) : 0f;
            float rz = joint.rot?.Length > 2 ? joint.rot[2] * ((float)Math.PI / 180f) : 0f;
            var rot = Quaternion.CreateFromYawPitchRoll(ry, rx, rz);

            // Apply animation delta on top of the T-pose rotation (mirrors deformbone in RenderAvatar).
            if (rotDeltas != null && rotDeltas.TryGetValue(joint.name, out var delta))
                rot = rot * delta;

            return (pos, rot, scale);
        }

        /// <summary>Local (parent-relative) position, rotation, and own-scale for a collision volume.</summary>
        private static (Vector3 Pos, Quaternion Rot, Vector3 Scale) GetCvLocalPRS(
            CollisionVolume                   volume,
            Dictionary<string, BoneTransform> boneTransforms)
        {
            Vector3 pos, scale;
            if (!string.IsNullOrEmpty(volume.name) && boneTransforms.TryGetValue(volume.name, out var bt))
            {
                pos   = new Vector3(bt.Position.X, bt.Position.Y, bt.Position.Z);
                scale = new Vector3(bt.Scale.X,    bt.Scale.Y,    bt.Scale.Z);
            }
            else
            {
                pos = volume.pos?.Length >= 3
                    ? new Vector3(volume.pos[0], volume.pos[1], volume.pos[2])
                    : Vector3.Zero;
                scale = volume.scale?.Length >= 3
                    ? new Vector3(volume.scale[0], volume.scale[1], volume.scale[2])
                    : Vector3.One;
            }

            float rx = volume.rot?.Length > 0 ? volume.rot[0] * ((float)Math.PI / 180f) : 0f;
            float ry = volume.rot?.Length > 1 ? volume.rot[1] * ((float)Math.PI / 180f) : 0f;
            float rz = volume.rot?.Length > 2 ? volume.rot[2] * ((float)Math.PI / 180f) : 0f;
            var rot = Quaternion.CreateFromYawPitchRoll(ry, rx, rz);

            return (pos, rot, scale);
        }

        /// <summary>
        /// Compute animated bone world matrices by rebuilding the skeleton with the
        /// given per-joint rotation deltas applied on top of the T-pose rotations.
        /// Equivalent to calling <see cref="BuildBoneWorldMatrices"/> but with live
        /// animation values threaded through.
        /// </summary>
        public static Dictionary<string, Matrix4x4> ComputeAnimatedBoneWorldMatrices(
            LindenAvatarDefinition                   avatarDef,
            Dictionary<string, BoneTransform>        boneTransforms,
            IReadOnlyDictionary<string, Quaternion>  rotDeltas)
        {
            var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            BuildBoneMatricesRecursive(avatarDef.Skeleton.bone, Quaternion.Identity, Vector3.Zero, Vector3.One,
                boneTransforms, result, rotDeltas);
            return result;
        }

        /// <summary>
        /// Reuse-overload: clears and refills <paramref name="result"/> in-place so the
        /// caller can keep a pre-allocated dictionary alive across frames.
        /// </summary>
        public static void ComputeAnimatedBoneWorldMatrices(
            LindenAvatarDefinition                   avatarDef,
            Dictionary<string, BoneTransform>        boneTransforms,
            IReadOnlyDictionary<string, Quaternion>  rotDeltas,
            Dictionary<string, Matrix4x4>            result)
        {
            result.Clear();
            BuildBoneMatricesRecursive(avatarDef.Skeleton.bone, Quaternion.Identity, Vector3.Zero, Vector3.One,
                boneTransforms, result, rotDeltas);
        }

        /// <summary>
        /// Compute bone world matrices for rigged attachment LBS, applying VP position
        /// AND scale deltas — the same skeleton the classic body skins against
        /// (<see cref="ComputeAnimatedBoneWorldMatrices(LindenAvatarDefinition,Dictionary{string,BoneTransform},IReadOnlyDictionary{string,Quaternion})"/>
        /// omits <c>applyVpScale</c>, defaulting to <c>true</c>). Verified against the real SL
        /// viewer source: <c>LLPolySkeletalDistortion::apply</c> writes shape-driven joint
        /// scale directly onto the shared skeleton via <c>setScale()</c>, and rigged
        /// attachments skin against that same skeleton — so fitted mesh inherits shape-driven
        /// scale in SL, not just position.
        /// </summary>
        public static Dictionary<string, Matrix4x4> ComputeAttachmentBoneWorldMatrices(
            LindenAvatarDefinition                   avatarDef,
            Dictionary<string, BoneTransform>        boneTransforms,
            IReadOnlyDictionary<string, Quaternion>  rotDeltas)
        {
            var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            BuildBoneMatricesRecursive(avatarDef.Skeleton.bone, Quaternion.Identity, Vector3.Zero, Vector3.One,
                boneTransforms, result, rotDeltas);
            return result;
        }

        /// <summary>
        /// Reuse-overload: clears and refills <paramref name="result"/> in-place.
        /// </summary>
        public static void ComputeAttachmentBoneWorldMatrices(
            LindenAvatarDefinition                   avatarDef,
            Dictionary<string, BoneTransform>        boneTransforms,
            IReadOnlyDictionary<string, Quaternion>  rotDeltas,
            Dictionary<string, Matrix4x4>            result)
        {
            result.Clear();
            BuildBoneMatricesRecursive(avatarDef.Skeleton.bone, Quaternion.Identity, Vector3.Zero, Vector3.One,
                boneTransforms, result, rotDeltas);
        }
    }
}
