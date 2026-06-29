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

using System.Collections.Generic;

namespace LibreMetaverse.Animesh
{
    /// <summary>
    /// Manages the set of animations currently playing on a single Animesh object and
    /// produces a blended <see cref="JointPose"/> dictionary each frame.
    /// </summary>
    public sealed class AnimeshPlayer
    {
        /// <summary>Full UUID of the in-world object this player drives.</summary>
        public UUID ObjectID { get; }

        // Active tracks keyed by animation UUID for O(1) add/remove.
        private readonly Dictionary<UUID, AnimationTrack> _tracks = new Dictionary<UUID, AnimationTrack>();
        private readonly object _lock = new object();

        internal AnimeshPlayer(UUID objectID)
        {
            ObjectID = objectID;
        }

        // ── Track management ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the track for <paramref name="animationID"/>, creating it if needed.
        /// Called by <see cref="AnimeshManager"/> when the server signals a new animation.
        /// </summary>
        internal AnimationTrack GetOrAddTrack(UUID animationID)
        {
            lock (_lock)
            {
                if (!_tracks.TryGetValue(animationID, out var track))
                {
                    track = new AnimationTrack(animationID);
                    _tracks[animationID] = track;
                }
                return track;
            }
        }

        /// <summary>
        /// Removes any tracks whose animation IDs are not in <paramref name="activeIDs"/>.
        /// Called when the server sends a complete replacement animation state.
        /// </summary>
        internal void RetainOnly(ISet<UUID> activeIDs)
        {
            lock (_lock)
            {
                var toRemove = new List<UUID>();
                foreach (var id in _tracks.Keys)
                    if (!activeIDs.Contains(id))
                        toRemove.Add(id);
                foreach (var id in toRemove)
                    _tracks.Remove(id);
            }
        }

        /// <summary>
        /// Returns the number of active tracks (including those waiting for asset data).
        /// </summary>
        public int TrackCount
        {
            get { lock (_lock) { return _tracks.Count; } }
        }

        // ── Playback ──────────────────────────────────────────────────────────

        /// <summary>
        /// Advance all animation clocks by <paramref name="dt"/> seconds.
        /// Call once per frame from your render/simulation loop.
        /// Finished non-looping animations remain in the track list; they will be removed
        /// on the next <see cref="RetainOnly"/> call from the server.
        /// </summary>
        public void Update(float dt)
        {
            lock (_lock)
            {
                foreach (var track in _tracks.Values)
                    track.Advance(dt);
            }
        }

        /// <summary>
        /// Evaluates the blended pose for all active, loaded tracks at their current times.
        /// </summary>
        /// <returns>
        /// A dictionary mapping joint names to their blended <see cref="JointPose"/>.
        /// Joints not driven by any active animation are absent from the dictionary;
        /// <see cref="AnimeshSkinning"/> treats absent joints as being in bind pose.
        /// </returns>
        public Dictionary<string, JointPose> EvaluatePose()
        {
            var pose = new Dictionary<string, JointPose>();
            lock (_lock)
            {
                foreach (var track in _tracks.Values)
                    track.EvaluatePose(pose);
            }
            return pose;
        }
    }
}
