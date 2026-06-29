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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibreMetaverse.Animesh
{
    /// <summary>
    /// GridClient subsystem that tracks which animations are playing on Animesh objects
    /// and manages the per-object <see cref="AnimeshPlayer"/> instances.
    /// <para>
    /// The manager listens to <see cref="ObjectManager.ObjectAnimation"/> events, requests
    /// animation assets from the asset server, and feeds parsed data into the relevant
    /// <see cref="AnimationTrack"/>.  The host application drives playback by calling
    /// <see cref="Update"/> once per frame and then calling
    /// <see cref="AnimeshPlayer.EvaluatePose"/> on individual players.
    /// </para>
    /// </summary>
    public sealed class AnimeshManager
    {
        private readonly GridClient _client;

        // One player per in-world object UUID.
        private readonly ConcurrentDictionary<UUID, AnimeshPlayer> _players
            = new ConcurrentDictionary<UUID, AnimeshPlayer>();

        internal AnimeshManager(GridClient client)
        {
            _client = client;
            _client.Objects.ObjectAnimation += OnObjectAnimation;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="AnimeshPlayer"/> for <paramref name="objectID"/>,
        /// or null if the object has never received an ObjectAnimation event.
        /// </summary>
        public AnimeshPlayer? GetPlayer(UUID objectID)
            => _players.TryGetValue(objectID, out var p) ? p : null;

        /// <summary>
        /// Returns a snapshot of all currently tracked players.
        /// </summary>
        public IEnumerable<AnimeshPlayer> AllPlayers => _players.Values;

        /// <summary>
        /// Advance all players by <paramref name="dt"/> seconds.
        /// Call once per frame, typically from a fixed-rate simulation or render loop.
        /// </summary>
        public void Update(float dt)
        {
            foreach (var player in _players.Values)
                player.Update(dt);
        }

        /// <summary>
        /// Remove the player for an object that has left the scene.
        /// </summary>
        public void RemovePlayer(UUID objectID) => _players.TryRemove(objectID, out _);

        // ── ObjectAnimation handler ───────────────────────────────────────────

        private void OnObjectAnimation(object? sender, ObjectAnimationEventArgs e)
        {
            var player = _players.GetOrAdd(e.ObjectID, id => new AnimeshPlayer(id));

            // Build the new active set so we can prune stopped animations.
            var activeIDs = new HashSet<UUID>(e.Animations.Count);
            foreach (var anim in e.Animations)
                activeIDs.Add(anim.AnimationID);

            player.RetainOnly(activeIDs);

            foreach (var anim in e.Animations)
            {
                var track = player.GetOrAddTrack(anim.AnimationID);

                if (track.Data != null) continue;

                // Request the animation asset; decode it when it arrives.
                UUID animID = anim.AnimationID;
                _ = FetchAndApplyAsync(player, animID);
            }
        }
        // ── Asset fetching ────────────────────────────────────────────────────

        private async Task FetchAndApplyAsync(AnimeshPlayer player, UUID animID)
        {
            try
            {
                var asset = await _client.Assets.RequestAssetAsync(animID, AssetType.Animation, false)
                    .ConfigureAwait(false);

                if (asset?.AssetData == null) return;

                var decoded = new BinBVHAnimationReader(asset.AssetData);

                // The player may have already discarded this track if the server stopped
                // the animation before the download finished.
                var track = player.GetOrAddTrack(animID);
                track.Data = decoded;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[AnimeshManager] Failed to fetch animation {animID}: {ex.Message}");
            }
        }
    }
}
