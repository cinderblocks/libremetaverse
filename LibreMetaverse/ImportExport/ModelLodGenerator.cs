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
using System.Threading;

namespace LibreMetaverse.ImportExport
{
    /// <summary>
    /// Makes the lower levels of detail of a prim the way the reference viewer's uploader does: each has a
    /// fixed fraction of the triangles of the one above it, every face is reduced by the same ratio, and none
    /// loses its last triangle, so the faces stay the same ones in the same order at every level.
    /// </summary>
    internal static class ModelLodGenerator
    {
        /// <returns>The medium, low and lowest levels of detail, in that order</returns>
        internal static List<ModelFace>[] Generate(ModelPrim prim, float reduction, CancellationToken cancellationToken)
        {
            if (!(reduction >= 1f)) reduction = 1f;

            var highTriangles = new List<int>(prim.Faces.Count);
            foreach (var face in prim.Faces) highTriangles.Add(face.Indices.Count / 3);

            var result = new List<ModelFace>[3];
            var above = prim.Faces;
            double divisor = 1;
            for (int level = 0; level < 3; level++)
            {
                divisor *= reduction;
                var faces = new List<ModelFace>(above.Count);
                for (int i = 0; i < above.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Each level is made from the one above it, so it can only have less of everything
                    int target = System.Math.Max(1, (int)System.Math.Floor(highTriangles[i] / divisor));
                    var simplified = ModelSimplifier.Simplify(above[i], target, prim.Scale, cancellationToken);
                    if (simplified.Indices.Count > above[i].Indices.Count || simplified.Vertices.Count > above[i].Vertices.Count)
                        simplified = above[i];
                    faces.Add(simplified);
                }
                result[level] = faces;
                above = faces;
            }
            return result;
        }
    }
}
