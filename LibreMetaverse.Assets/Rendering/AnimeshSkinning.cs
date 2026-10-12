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
using LibreMetaverse.Animesh;
using LibreMetaverse.Rendering;

namespace LibreMetaverse
{
    /// <summary>
    /// CPU-side linear-blend skinning pipeline for Animesh objects.
    /// <para>
    /// Typical usage:
    /// <list type="number">
    ///   <item>Call <see cref="ComputeSkinningMatrices"/> once per frame to build the
    ///         per-joint deformation matrices from the animated pose.</item>
    ///   <item>Call <see cref="DeformVertices"/> for each <see cref="Face"/> to produce
    ///         the final vertex positions for rendering.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class AnimeshSkinning
    {
        private const float DegToRad = (float)(Math.PI / 180.0);

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Computes one skinning matrix per joint referenced by <paramref name="skinData"/>
        /// by walking the skeleton hierarchy and blending in the animated pose.
        /// </summary>
        /// <param name="pose">
        /// Blended joint pose from <see cref="AnimeshPlayer.EvaluatePose"/>.
        /// Joints absent from the dictionary are treated as bind-pose (identity local transform).
        /// </param>
        /// <param name="skeleton">The avatar skeleton defining joint hierarchy and bind transforms.</param>
        /// <param name="skinData">
        /// The mesh's skin section describing which joints influence this mesh and their inverse-bind matrices.
        /// </param>
        /// <returns>
        /// An array of <see cref="Matrix4"/> skinning matrices, one per entry in
        /// <see cref="MeshSkinData.JointNames"/>.  Pass this array to <see cref="DeformVertices"/>.
        /// </returns>
        public static Matrix4[] ComputeSkinningMatrices(
            Dictionary<string, JointPose> pose,
            LindenSkeleton skeleton,
            MeshSkinData skinData)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (skinData  == null) throw new ArgumentNullException(nameof(skinData));
            if (pose      == null) throw new ArgumentNullException(nameof(pose));

            var worldMatrices = new Dictionary<string, Matrix4>(StringComparer.Ordinal);

            // Walk the hierarchy depth-first starting from the root.
            if (skeleton.bone != null)
                ComputeWorldMatrices(skeleton.bone, Matrix4.Identity, pose, worldMatrices);

            // Build the per-joint skinning matrix array.
            int jointCount = skinData.JointNames.Length;
            var skinningMatrices = new Matrix4[jointCount];

            for (int i = 0; i < jointCount; i++)
            {
                string jointName = skinData.JointNames[i];

                if (!worldMatrices.TryGetValue(jointName, out Matrix4 worldMatrix))
                {
                    // Joint not found in skeleton — use identity so the mesh is undeformed.
                    skinningMatrices[i] = Matrix4.Identity;
                    continue;
                }

                // Extract the inverse-bind matrix stored in the mesh asset.
                // Layout: 16 floats per joint, row-major, row-vector convention.
                Matrix4 invBind = ExtractMatrix(skinData.InverseBindMatrices, i);

                // skinMat = invBind * worldMatrix
                // In row-vector convention (v * M): first invBind, then worldMatrix.
                skinningMatrices[i] = invBind * worldMatrix;
            }

            return skinningMatrices;
        }

        /// <summary>
        /// Applies linear-blend skinning to the vertices of <paramref name="face"/>.
        /// </summary>
        /// <param name="face">The mesh face with per-vertex <see cref="Face.Weights"/> data.</param>
        /// <param name="skinningMatrices">
        /// Output of <see cref="ComputeSkinningMatrices"/>, indexed by joint.
        /// </param>
        /// <param name="bindShapeMatrix">
        /// The <see cref="MeshSkinData.BindShapeMatrix"/> from the skin section.
        /// Transforms mesh-local vertices into skeleton reference space before skinning.
        /// </param>
        /// <param name="outPositions">
        /// Caller-provided span that receives the deformed vertex positions, one per vertex.
        /// Must be at least <c>face.Vertices.Count</c> in length.
        /// </param>
        /// <param name="outNormals">
        /// Optional span for deformed normals; null skips normal deformation.
        /// Must be at least <c>face.Vertices.Count</c> in length when not null.
        /// </param>
        public static void DeformVertices(
            Face face,
            Matrix4[] skinningMatrices,
            Matrix4 bindShapeMatrix,
            Span<Vector3> outPositions,
            Span<Vector3> outNormals = default)
        {
            if (outPositions.Length < face.Vertices.Count)
                throw new ArgumentException("outPositions is too short", nameof(outPositions));
            if (!outNormals.IsEmpty && outNormals.Length < face.Vertices.Count)
                throw new ArgumentException("outNormals is too short", nameof(outNormals));

            bool deformNormals = !outNormals.IsEmpty;
            var weights = face.Weights;

            for (int i = 0; i < face.Vertices.Count; i++)
            {
                var vertex = face.Vertices[i];

                if (weights == null || i >= weights.Count || skinningMatrices.Length == 0)
                {
                    // No skinning data — pass through unchanged.
                    outPositions[i] = vertex.Position;
                    if (deformNormals) outNormals[i] = vertex.Normal;
                    continue;
                }

                var w = weights[i];
                Vector3 p = Vector3.Transform(vertex.Position, bindShapeMatrix);

                // Blend contributions from up to 4 joints.
                Vector3 skinnedPos = BlendPosition(p, skinningMatrices, w.Joint0, w.Weight0)
                                   + BlendPosition(p, skinningMatrices, w.Joint1, w.Weight1)
                                   + BlendPosition(p, skinningMatrices, w.Joint2, w.Weight2)
                                   + BlendPosition(p, skinningMatrices, w.Joint3, w.Weight3);

                outPositions[i] = skinnedPos;

                if (deformNormals)
                {
                    Vector3 n = vertex.Normal;
                    Vector3 skinnedNorm = BlendNormal(n, skinningMatrices, w.Joint0, w.Weight0)
                                       + BlendNormal(n, skinningMatrices, w.Joint1, w.Weight1)
                                       + BlendNormal(n, skinningMatrices, w.Joint2, w.Weight2)
                                       + BlendNormal(n, skinningMatrices, w.Joint3, w.Weight3);
                    outNormals[i] = Vector3.Normalize(skinnedNorm);
                }
            }
        }

        // ── Forward kinematics ────────────────────────────────────────────────

        private static void ComputeWorldMatrices(
            Joint joint,
            Matrix4 parentWorld,
            Dictionary<string, JointPose> pose,
            Dictionary<string, Matrix4> worldMatrices)
        {
            Matrix4 local = BuildLocalTransform(joint, pose);
            Matrix4 world = local * parentWorld;

            worldMatrices[joint.name] = world;

            // Also register under every alias so mesh skin data can find the joint
            // regardless of which name the mesh author used.
            foreach (var alias in joint.GetAliasesList())
                worldMatrices[alias] = world;

            if (joint.bone == null) return;
            foreach (var child in joint.bone)
                ComputeWorldMatrices(child, world, pose, worldMatrices);
        }

        /// <summary>
        /// Builds the local transform matrix for <paramref name="joint"/> by combining
        /// the animation override (if present) with the joint's skeleton bind pose.
        /// </summary>
        private static Matrix4 BuildLocalTransform(Joint joint, Dictionary<string, JointPose> pose)
        {
            // Default translation is the joint's bind-pose offset from its parent.
            Vector3 translation = ParsePos(joint.pos);
            // Default rotation is the joint's bind-pose rotation from its parent.
            Quaternion rotation = ParseRot(joint.rot);

            // Apply animation overrides when available.
            if (pose.TryGetValue(joint.name, out var jp))
            {
                if (jp.HasPosition) translation = jp.Position;
                if (jp.HasRotation) rotation    = jp.Rotation;
            }
            else
            {
                // Try aliases.
                foreach (var alias in joint.GetAliasesList())
                {
                    if (pose.TryGetValue(alias, out var aliasJp))
                    {
                        if (aliasJp.HasPosition) translation = aliasJp.Position;
                        if (aliasJp.HasRotation) rotation    = aliasJp.Rotation;
                        break;
                    }
                }
            }

            // T * R: first rotate, then translate.  In row-vector convention both are
            // left-multiplied so: localTransform = rotMat * transMat.
            Matrix4 rotMat   = Matrix4.CreateFromQuaternion(rotation);
            Matrix4 transMat = Matrix4.CreateTranslation(translation);
            return rotMat * transMat;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Vector3 ParsePos(float[]? pos)
        {
            if (pos == null || pos.Length < 3) return Vector3.Zero;
            return new Vector3(pos[0], pos[1], pos[2]);
        }

        private static Quaternion ParseRot(float[]? rot)
        {
            if (rot == null || rot.Length < 3) return Quaternion.Identity;
            // avatar_skeleton.xml stores Euler angles in degrees (roll, pitch, yaw).
            return Quaternion.CreateFromEulers(
                rot[0] * DegToRad,
                rot[1] * DegToRad,
                rot[2] * DegToRad);
        }

        private static Matrix4 ExtractMatrix(float[] data, int jointIndex)
        {
            int o = jointIndex * 16;
            if (o + 16 > data.Length) return Matrix4.Identity;
            return new Matrix4(
                data[o+ 0], data[o+ 1], data[o+ 2], data[o+ 3],
                data[o+ 4], data[o+ 5], data[o+ 6], data[o+ 7],
                data[o+ 8], data[o+ 9], data[o+10], data[o+11],
                data[o+12], data[o+13], data[o+14], data[o+15]);
        }

        private static Vector3 BlendPosition(Vector3 p, Matrix4[] matrices, int joint, float weight)
        {
            if (weight <= 0f || joint < 0 || joint >= matrices.Length) return Vector3.Zero;
            return Vector3.Transform(p, matrices[joint]) * weight;
        }

        private static Vector3 BlendNormal(Vector3 n, Matrix4[] matrices, int joint, float weight)
        {
            if (weight <= 0f || joint < 0 || joint >= matrices.Length) return Vector3.Zero;
            return Vector3.TransformNormal(n, matrices[joint]) * weight;
        }
    }
}
