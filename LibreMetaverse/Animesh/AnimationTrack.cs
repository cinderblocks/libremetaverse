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

namespace LibreMetaverse.Animesh
{
    /// <summary>
    /// Manages playback state for a single <see cref="BinBVHAnimationReader"/> animation
    /// and evaluates per-joint poses via keyframe interpolation.
    /// </summary>
    public sealed class AnimationTrack
    {
        /// <summary>UUID of the animation asset.</summary>
        public UUID AnimationID { get; }

        /// <summary>
        /// Parsed animation data.  Null until the asset has been downloaded and decoded.
        /// Joints are evaluated only when this is non-null.
        /// </summary>
        public BinBVHAnimationReader? Data { get; internal set; }

        /// <summary>Current playback position in seconds.</summary>
        public float CurrentTime { get; private set; }

        /// <summary>True once a non-looping animation has played past its <see cref="BinBVHAnimationReader.OutPoint"/>.</summary>
        public bool IsFinished { get; private set; }

        internal AnimationTrack(UUID id)
        {
            AnimationID = id;
        }

        /// <summary>
        /// Advance the playback clock by <paramref name="dt"/> seconds.
        /// Loops when <see cref="BinBVHAnimationReader.Loop"/> is true;
        /// clamps and marks finished otherwise.
        /// </summary>
        public void Advance(float dt)
        {
            if (Data == null || IsFinished) return;
            CurrentTime += dt;

            if (Data.Loop)
            {
                float span = Data.OutPoint - Data.InPoint;
                if (span > 0f && CurrentTime > Data.OutPoint)
                    CurrentTime = Data.InPoint + (CurrentTime - Data.InPoint) % span;
            }
            else if (CurrentTime >= Data.OutPoint)
            {
                CurrentTime = Data.OutPoint;
                IsFinished = true;
            }
        }

        /// <summary>
        /// Current blend weight [0, 1] factoring in the ease-in and ease-out curves
        /// specified by the animation metadata.
        /// </summary>
        public float EaseWeight
        {
            get
            {
                if (Data == null) return 0f;
                float t = CurrentTime;
                if (Data.EaseInTime > 0f && t < Data.EaseInTime)
                    return t / Data.EaseInTime;
                float easeOutStart = Data.OutPoint - Data.EaseOutTime;
                if (Data.EaseOutTime > 0f && t > easeOutStart)
                    return Math.Max(0f, (Data.OutPoint - t) / Data.EaseOutTime);
                return 1f;
            }
        }

        /// <summary>
        /// Evaluate the animation at <see cref="CurrentTime"/> and merge each joint's
        /// interpolated pose into <paramref name="pose"/>.
        /// <para>
        /// Per-joint priority controls which track "wins" when multiple tracks drive the
        /// same joint: a higher <see cref="JointPose.Priority"/> value takes precedence.
        /// Equal-priority joints from different tracks are blended by
        /// <see cref="JointPose.EaseWeight"/>.
        /// </para>
        /// </summary>
        /// <param name="pose">Dictionary keyed by joint name, updated in place.</param>
        public void EvaluatePose(Dictionary<string, JointPose> pose)
        {
            if (Data == null) return;
            float t = CurrentTime;
            float ease = EaseWeight;

            foreach (var joint in Data.joints)
            {
                Quaternion? rot = joint.rotationkeys.Length > 0
                    ? InterpolateRotation(joint.rotationkeys, t)
                    : (Quaternion?)null;

                Vector3? pos = joint.positionkeys.Length > 0
                    ? InterpolatePosition(joint.positionkeys, t)
                    : (Vector3?)null;

                if (rot == null && pos == null) continue;

                if (pose.TryGetValue(joint.Name, out var existing))
                {
                    if (joint.Priority < existing.Priority) continue;

                    if (joint.Priority == existing.Priority)
                    {
                        // Equal priority: blend by ease weight.
                        float total = existing.EaseWeight + ease;
                        float myWeight = total > 0f ? ease / total : 0.5f;

                        if (rot.HasValue && existing.HasRotation)
                            rot = Quaternion.Slerp(existing.Rotation, rot.Value, myWeight);
                        if (pos.HasValue && existing.HasPosition)
                            pos = Vector3.Lerp(existing.Position, pos.Value, myWeight);
                    }
                }

                pose[joint.Name] = new JointPose
                {
                    Rotation    = rot ?? Quaternion.Identity,
                    HasRotation = rot.HasValue,
                    Position    = pos ?? Vector3.Zero,
                    HasPosition = pos.HasValue,
                    Priority    = joint.Priority,
                    EaseWeight  = ease,
                };
            }
        }

        // ── Keyframe interpolation ─────────────────────────────────────────────

        private static Quaternion InterpolateRotation(binBVHJointKey[] keys, float t)
        {
            if (keys.Length == 1)
                return DecodeRotation(keys[0].key_element);

            int hi = BracketTime(keys, t);
            if (hi == 0)               return DecodeRotation(keys[0].key_element);
            if (hi >= keys.Length)     return DecodeRotation(keys[keys.Length - 1].key_element);

            float t0 = keys[hi - 1].time, t1 = keys[hi].time;
            float frac = (t1 > t0) ? (t - t0) / (t1 - t0) : 0f;
            return Quaternion.Slerp(
                DecodeRotation(keys[hi - 1].key_element),
                DecodeRotation(keys[hi].key_element),
                frac);
        }

        private static Vector3 InterpolatePosition(binBVHJointKey[] keys, float t)
        {
            if (keys.Length == 1) return keys[0].key_element;

            int hi = BracketTime(keys, t);
            if (hi == 0)               return keys[0].key_element;
            if (hi >= keys.Length)     return keys[keys.Length - 1].key_element;

            float t0 = keys[hi - 1].time, t1 = keys[hi].time;
            float frac = (t1 > t0) ? (t - t0) / (t1 - t0) : 0f;
            return Vector3.Lerp(keys[hi - 1].key_element, keys[hi].key_element, frac);
        }

        // Returns the index of the first key with time >= t (or keys.Length if all are earlier).
        private static int BracketTime(binBVHJointKey[] keys, float t)
        {
            int lo = 0, hi = keys.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (keys[mid].time < t) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        // ── Quaternion decoding ────────────────────────────────────────────────

        /// <summary>
        /// Decodes the compressed quaternion stored in BVH rotation keyframes.
        /// The binary format packs x, y, z into the range [-1, 1]; w is recovered as
        /// sqrt(1 – |xyz|²), which is always ≥ 0 in SL's sign convention.
        /// </summary>
        internal static Quaternion DecodeRotation(Vector3 v)
        {
            float wSq = 1f - v.X * v.X - v.Y * v.Y - v.Z * v.Z;
            float w = wSq > 0f ? (float)Math.Sqrt(wSq) : 0f;
            // Normalize to guard against quantization error.
            var q = new Quaternion(v.X, v.Y, v.Z, w);
            return Quaternion.Normalize(q);
        }
    }
}
