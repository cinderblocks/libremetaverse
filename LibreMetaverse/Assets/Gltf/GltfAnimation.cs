/*
 * Copyright (c) 2024-2026, Sjofn LLC
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

namespace OpenMetaverse.Assets.Gltf
{
    /// <summary>
    /// Pairs a time-input accessor with a value-output accessor and an interpolation method.
    /// Used by <see cref="GltfAnimationChannel"/> to drive a single property over time.
    /// </summary>
    public sealed class GltfAnimationSampler
    {
        /// <summary>Accessor index for the SCALAR FLOAT keyframe timestamps (input).</summary>
        public int Input { get; set; } = -1;
        /// <summary>Accessor index for the keyframe values (output). Element type depends on the channel target path.</summary>
        public int Output { get; set; } = -1;
        public GltfAnimationInterpolation Interpolation { get; set; } = GltfAnimationInterpolation.Linear;
    }

    /// <summary>Identifies which node property an animation channel drives.</summary>
    public sealed class GltfAnimationChannelTarget
    {
        /// <summary>Index of the target node. -1 if unspecified.</summary>
        public int Node { get; set; } = -1;
        public GltfAnimationTargetPath Path { get; set; }
    }

    /// <summary>Binds an animation sampler to a node property target.</summary>
    public sealed class GltfAnimationChannel
    {
        /// <summary>Index into the owning <see cref="GltfAnimation.Samplers"/> list.</summary>
        public int Sampler { get; set; } = -1;
        public GltfAnimationChannelTarget Target { get; } = new GltfAnimationChannelTarget();
    }

    /// <summary>A named animation clip with one or more channels and their samplers.</summary>
    public sealed class GltfAnimation
    {
        public string? Name { get; set; }
        public List<GltfAnimationSampler> Samplers { get; } = new List<GltfAnimationSampler>();
        public List<GltfAnimationChannel> Channels { get; } = new List<GltfAnimationChannel>();
    }
}
