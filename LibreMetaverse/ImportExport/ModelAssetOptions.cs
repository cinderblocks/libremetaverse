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

namespace LibreMetaverse.ImportExport
{
    /// <summary>What to make of a model's mesh asset besides its full detail: lower levels of detail, and a physics shape</summary>
    public sealed class ModelAssetOptions
    {
        /// <summary>
        /// Make the medium, low and lowest levels of detail by simplifying the faces. Without them the grid draws the full
        /// detail at every distance, and the object costs more to have in a region.
        /// </summary>
        public bool GenerateLods { get; set; } = true;

        /// <summary>
        /// How many times fewer triangles each level of detail has than the one above it. The reference viewer uses 3, which
        /// gives a third, a ninth and a twenty-seventh of the triangles. Must be at least 1.
        /// </summary>
        public float LodReduction { get; set; } = 3f;

        /// <summary>
        /// Make a physics shape that is the convex hull of the mesh. Without it the shape is a fixed box, whatever the mesh
        /// looks like. A rigged mesh keeps the box, since it has no physics of its own.
        /// </summary>
        public bool GeneratePhysics { get; set; } = true;

        /// <summary>Neither: the asset is the full detail and a box physics shape, as <see cref="ModelPrim.CreateAsset(UUID)"/> makes it</summary>
        public static ModelAssetOptions None => new ModelAssetOptions { GenerateLods = false, GeneratePhysics = false };

        internal void Validate()
        {
            if (!(LodReduction >= 1f) || float.IsInfinity(LodReduction))
                throw new ArgumentOutOfRangeException(nameof(LodReduction), "The reduction must be at least 1");
        }
    }
}
