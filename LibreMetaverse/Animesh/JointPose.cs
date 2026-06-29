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

namespace LibreMetaverse.Animesh
{
    /// <summary>
    /// The animation-driven override for a single skeleton joint at a moment in time.
    /// Produced by <see cref="AnimationTrack.EvaluatePose"/> and consumed by
    /// <see cref="AnimeshSkinning"/>.
    /// </summary>
    public struct JointPose
    {
        /// <summary>Local rotation from the animation.  Valid only when <see cref="HasRotation"/> is true.</summary>
        public Quaternion Rotation;

        /// <summary>True when the animation provides a rotation key for this joint.</summary>
        public bool HasRotation;

        /// <summary>
        /// Local position offset from the animation.  In practice only the pelvis joint
        /// carries position keys in SL animations.  Valid only when <see cref="HasPosition"/> is true.
        /// </summary>
        public Vector3 Position;

        /// <summary>True when the animation provides a position key for this joint.</summary>
        public bool HasPosition;

        /// <summary>
        /// Per-joint priority from the source <see cref="binBVHJoint"/>.
        /// Higher values override lower ones during pose blending.
        /// </summary>
        public int Priority;

        /// <summary>
        /// Ease-in/ease-out weight in [0, 1].  Used to blend equal-priority joints smoothly
        /// when an animation starts or ends.
        /// </summary>
        public float EaseWeight;
    }
}
