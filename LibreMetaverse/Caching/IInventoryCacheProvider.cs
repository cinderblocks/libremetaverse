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

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.Caching
{
    /// <summary>
    /// What <see cref="Inventory.CacheProvider"/> needs to save and restore an inventory structure to disk.
    /// The caller picks the file name and when to call these; nothing in LibreMetaverse calls them on its own.
    /// </summary>
    public interface IInventoryCacheProvider
    {
        /// <summary>Saves the current inventory structure to a cache file</summary>
        void SaveToDisk(string filename, ConcurrentDictionary<UUID, InventoryNode> items);

        /// <summary>Async variant of <see cref="SaveToDisk"/>; exceptions propagate to the caller</summary>
        Task SaveToDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> items, CancellationToken cancellationToken = default);

        /// <summary>Loads a cache file into the inventory structure; returns the number of items restored, or -1 on error</summary>
        int RestoreFromDisk(string filename, ConcurrentDictionary<UUID, InventoryNode> items);

        /// <summary>Async variant of <see cref="RestoreFromDisk"/>; exceptions propagate to the caller</summary>
        Task<int> RestoreFromDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> items, CancellationToken cancellationToken = default);
    }
}
