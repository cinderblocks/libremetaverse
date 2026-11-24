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

using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.Caching
{
    /// <summary>
    /// What <see cref="AssetManager.Cache"/> needs from an asset cache. <see cref="AssetCache"/> is
    /// the default, disk-backed implementation that ships in core; assign a different implementation
    /// to <see cref="AssetManager.Cache"/> to change where downloaded assets are kept.
    /// </summary>
    public interface IAssetCacheProvider
    {
        /// <summary>Auto-prune periodically if the cache grows too big</summary>
        bool AutoPruneEnabled { get; set; }

        /// <summary>How long (in ms) between cache checks</summary>
        double AutoPruneInterval { get; set; }

        /// <summary>User-supplied routine to compute the asset cache location, or null for the default</summary>
        AssetCache.ComputeAssetCacheFilenameDelegate? ComputeAssetCacheFilename { get; set; }

        /// <summary>Checks if the asset exists in the local cache</summary>
        bool HasAsset(UUID assetID);

        /// <summary>The file name of the asset stored with the given UUID, or null if not cached</summary>
        string? AssetFileName(UUID assetID);

        /// <summary>Bytes read from the local asset cache, or null if it does not exist</summary>
        byte[]? GetCachedAssetBytes(UUID assetID);

        /// <summary>Async variant of <see cref="GetCachedAssetBytes"/></summary>
        Task<byte[]?> GetCachedAssetBytesAsync(UUID assetID, CancellationToken cancellationToken = default);

        /// <summary>Tries to read cached bytes, returning false on any failure (locked file, IO error, missing file)</summary>
        bool TryGetCachedAssetBytes(UUID assetID, out byte[]? data);

        /// <summary>Async variant of <see cref="TryGetCachedAssetBytes"/></summary>
        Task<(bool Success, byte[]? Data)> TryGetCachedAssetBytesAsync(UUID assetID, CancellationToken cancellationToken = default);

        /// <summary>The image from the local image cache, or null if it does not exist</summary>
        ImageDownload? GetCachedImage(UUID imageID);

        /// <summary>Saves an asset to the local cache</summary>
        bool SaveAssetToCache(UUID assetID, byte[] assetData);

        /// <summary>Async variant of <see cref="SaveAssetToCache"/></summary>
        Task<bool> SaveAssetToCacheAsync(UUID assetID, byte[] assetData, CancellationToken cancellationToken = default);

        /// <summary>Wipes out the entire cache</summary>
        void Clear();

        /// <summary>Brings the cache size down to the configured maximum, synchronously</summary>
        Task PruneAsync(CancellationToken cancellationToken = default);

        /// <summary>Starts pruning the cache in the background if it is not already running</summary>
        void BeginPrune();
    }
}
