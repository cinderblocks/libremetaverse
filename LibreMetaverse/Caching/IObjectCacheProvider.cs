/*
 * Copyright (c) 2026, Sjofn LLC
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
using LibreMetaverse.Packets;

namespace LibreMetaverse.Caching
{
    /// <summary>
    /// Supplies per-region object-update caching, the way the reference viewer's object cache lets a region skip
    /// resending every static object on every visit. <see cref="ObjectManager.CacheProvider"/> is null by default,
    /// which reproduces today's behavior exactly: the handshake always claims the cache is empty, and nothing is
    /// stored or replayed. Assign an implementation (for example <c>ObjectDiskCache</c> from the
    /// LibreMetaverse.Caching package) before connecting to enable it.
    /// </summary>
    public interface IObjectCacheProvider
    {
        /// <summary>
        /// True if this provider already has updates stored for this region and cache ID (the region's
        /// <see cref="Simulator.ID"/>, which changes when the region's own object cache is invalidated). Checked
        /// when building the handshake reply: true leaves out the "cache empty" flag, so the region sends
        /// <c>ObjectUpdateCached</c> probes instead of full updates for everything.
        /// </summary>
        bool HasEntries(ulong regionHandle, UUID cacheId);

        /// <summary>Store a compressed object update just received from this region, for possible replay on a
        /// later visit. Implementations typically drop avatars and anything beyond their own capacity.</summary>
        void Store(ulong regionHandle, UUID cacheId, ObjectUpdateCompressedPacket.ObjectDataBlock block);

        /// <summary>
        /// Match <c>ObjectUpdateCached</c> probes from this region against stored updates. A probe whose CRC
        /// matches is added to <paramref name="hits"/>, to be replayed as if the region had sent it; the local IDs
        /// of the rest -- unmatched or changed -- are returned so the caller can request them normally.
        /// </summary>
        List<uint> Lookup(ulong regionHandle, UUID cacheId, IEnumerable<(uint LocalID, uint Crc)> probes,
            List<ObjectUpdateCompressedPacket.ObjectDataBlock> hits);
    }
}
