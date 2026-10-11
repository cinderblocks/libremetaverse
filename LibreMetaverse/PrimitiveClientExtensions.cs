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
using System.Linq;

namespace LibreMetaverse
{
    /// <summary>What a <see cref="Primitive"/> can be asked once there is a grid client to ask it of</summary>
    public static class PrimitiveClientExtensions
    {
        /// <summary>The prims in the same region that are linked to this one as its children</summary>
        /// <param name="primitive">The root prim</param>
        /// <param name="client">The client that knows the region the prim is in</param>
        public static IEnumerable<Primitive> GetChildren(this Primitive primitive, GridClient client)
        {
            if (primitive == null) throw new ArgumentNullException(nameof(primitive));
            if (client == null) throw new ArgumentNullException(nameof(client));

            var sim = client.Network.FindSimulator(primitive.RegionHandle);
            return sim != null
                ? sim.ObjectsPrimitives.Where(p => p.Value.ParentID == primitive.LocalID)
                    .Select(p => p.Value)
                : Array.Empty<Primitive>();
        }
    }
}
