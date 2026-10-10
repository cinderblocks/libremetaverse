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
    /// <summary>
    /// The unit cube a mesh is fitted into for upload. The viewer scales a model's positions into
    /// -0.5 .. 0.5 and the prim's scale puts the size back, and since that scale is not uniform the
    /// normals have to be stored scaled the other way to come out right.
    /// </summary>
    internal static class UnitCubeFit
    {
        // The viewer's F_APPROXIMATELY_ZERO. An axis thinner than this is treated as flat
        private const float FlatAxis = 0.00001f;

        /// <summary>
        /// Puts a normal through the fit, as the viewer's <c>normalizeVolumeFaces</c> does: it is
        /// multiplied by the model's size along each axis and renormalized. When the prim is drawn its
        /// scale is divided back out (normals follow the inverse transpose), leaving the normal the
        /// file had. A flat axis counts as size 1 so the normal of a plane is not scaled away.
        /// </summary>
        /// <param name="normal">The normal, in the mesh's own space</param>
        /// <param name="size">The extent of the mesh along each axis</param>
        internal static Vector3 FitNormal(Vector3 normal, Vector3 size)
        {
            var scaled = new Vector3(
                normal.X * Extent(size.X),
                normal.Y * Extent(size.Y),
                normal.Z * Extent(size.Z));

            float length = scaled.Length();
            return length > 0f && !float.IsNaN(length) && !float.IsInfinity(length) ? scaled / length : Vector3.Zero;
        }

        private static float Extent(float size) => Math.Abs(size) < FlatAxis ? 1f : size;
    }
}
