/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    /// <summary>
    /// Class that handles the local asset cache
    /// </summary>
    public class AssetCache : IDisposable
    {
        // User can plug in a routine to compute the asset cache location
        public delegate string ComputeAssetCacheFilenameDelegate(string cacheDir, UUID assetID);

        public ComputeAssetCacheFilenameDelegate ComputeAssetCacheFilename = null;

        private readonly GridClient Client;
        private readonly ManualResetEventSlim cleanerEvent = new ManualResetEventSlim();
        private System.Timers.Timer cleanerTimer;
        private double pruneInterval = 1000 * 60 * 5;
        private bool autoPruneEnabled = true;

        private readonly EventHandler<LoginProgressEventArgs> loginHandler;
        private readonly EventHandler<DisconnectedEventArgs> disconnectedHandler;
        private bool _disposed = false;

        /// <summary>
        /// Auto-prune periodically if the cache grows too big.
        /// Default is enabled when caching is enabled.
        /// </summary>
        public bool AutoPruneEnabled
        {
            set
            {
                autoPruneEnabled = value;

                if (autoPruneEnabled)
                {
                    SetupTimer();
                }
                else
                {
                    DestroyTimer();
                }
            }
            get => autoPruneEnabled;
        }

        /// <summary>
        /// How long (in ms) between cache checks (default is 5 min.) 
        /// </summary>
        public double AutoPruneInterval
        {
            set
            {
                pruneInterval = value;
                SetupTimer();
            }
            get => pruneInterval;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to the GridClient object</param>
        public AssetCache(GridClient client)
        {
            Client = client;

            loginHandler = (sender, e) =>
            {
                if (e.Status == LoginStatus.Success)
                {
                    SetupTimer();
                }
            };

            disconnectedHandler = (sender, e) => { DestroyTimer(); };

            Client.Network.LoginProgress += loginHandler;
            Client.Network.Disconnected += disconnectedHandler;
        }

        /// <summary>
        /// Dispose pattern to unsubscribe event handlers and dispose timer
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                try
                {
                    DestroyTimer();

                    if (Client?.Network != null)
                    {
                        try { Client.Network.LoginProgress -= loginHandler; } catch { }
                        try { Client.Network.Disconnected -= disconnectedHandler; } catch { }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Disposes cleanup timer
        /// </summary>
        private void DestroyTimer()
        {
            if (cleanerTimer != null)
            {
                cleanerTimer.Dispose();
                cleanerTimer = null;
            }
        }

        /// <summary>
        /// Only create timer when needed
        /// </summary>
        private void SetupTimer()
        {
            if (Operational() && autoPruneEnabled && Client.Network.Connected)
            {
                if (cleanerTimer == null)
                {
                    cleanerTimer = new System.Timers.Timer(pruneInterval);
                    cleanerTimer.Elapsed += cleanerTimer_Elapsed;
                }
                cleanerTimer.Interval = pruneInterval;
                cleanerTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Return bytes read from the local asset cache, null if it does not exist
        /// </summary>
        /// <param name="assetID">UUID of the asset we want to get</param>
        /// <returns>Raw bytes of the asset, or null on failure</returns>
        public byte[] GetCachedAssetBytes(UUID assetID)
        {
            // Keep synchronous wrapper for compatibility
            return GetCachedAssetBytesAsync(assetID, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async variant that returns bytes read from the local asset cache, null if it does not exist
        /// </summary>
        public async Task<byte[]> GetCachedAssetBytesAsync(UUID assetID, CancellationToken cancellationToken = default)
        {
            if (!Operational())
            {
                return null;
            }

            try
            {
                var path = FileName(assetID);
                if (File.Exists(path))
                {
                    DebugLog($"Reading {path} from asset cache.");
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                    {
                        var length = (int)fs.Length;
                        var buffer = new byte[length];
                        var read = 0;
                        while (read < length)
                        {
                            var n = await fs.ReadAsync(buffer, read, length - read, cancellationToken).ConfigureAwait(false);
                            if (n == 0) break;
                            read += n;
                        }
                        return buffer;
                    }
                }

                var staticPath = StaticFileName(assetID);
                if (File.Exists(staticPath))
                {
                    DebugLog($"Reading {staticPath} from static asset cache.");
                    using (var fs = new FileStream(staticPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                    {
                        var length = (int)fs.Length;
                        var buffer = new byte[length];
                        var read = 0;
                        while (read < length)
                        {
                            var n = await fs.ReadAsync(buffer, read, length - read, cancellationToken).ConfigureAwait(false);
                            if (n == 0) break;
                            read += n;
                        }
                        return buffer;
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugLog("Failed reading asset from cache (" + ex.Message + ")");
                return null;
            }
        }

        /// <summary>
        /// Returns ImageDownload object of the
        /// image from the local image cache, null if it does not exist
        /// </summary>
        /// <param name="imageID">UUID of the image we want to get</param>
        /// <returns>ImageDownload object containing the image, or null on failure</returns>
        public ImageDownload GetCachedImage(UUID imageID)
        {
            if (!Operational())
                return null;

            byte[] imageData = GetCachedAssetBytes(imageID);
            if (imageData == null) { return null; }
            ImageDownload transfer = new ImageDownload
            {
                AssetType = AssetType.Texture,
                ID = imageID,
                Simulator = Client.Network.CurrentSim,
                Size = imageData.Length,
                Success = true,
                Transferred = imageData.Length,
                AssetData = imageData
            };
            return transfer;
        }

        /// <summary>
        /// Constructs a file name of the cached asset
        /// </summary>
        /// <param name="assetID">UUID of the asset</param>
        /// <returns>String with the file name of the cached asset</returns>
        private string FileName(UUID assetID)
        {
            if (ComputeAssetCacheFilename != null)
            {
                return ComputeAssetCacheFilename(Client.Settings.ASSET_CACHE_DIR, assetID);
            }
            return Client.Settings.ASSET_CACHE_DIR + Path.DirectorySeparatorChar + assetID;
        }

        /// <summary>
        /// Constructs a file name of the static cached asset
        /// </summary>
        /// <param name="assetID">UUID of the asset</param>
        /// <returns>String with the file name of the static cached asset</returns>
        private string StaticFileName(UUID assetID)
        {
            return Path.Combine(Settings.RESOURCE_DIR, "static_assets", assetID.ToString());
        }

        /// <summary>
        /// Saves an asset to the local cache
        /// </summary>
        /// <param name="assetID">UUID of the asset</param>
        /// <param name="assetData">Raw bytes the asset consists of</param>
        /// <returns>Whether the operation was successful</returns>
        public bool SaveAssetToCache(UUID assetID, byte[] assetData)
        {
            // Keep synchronous wrapper for compatibility
            return SaveAssetToCacheAsync(assetID, assetData, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async variant that saves an asset to the local cache
        /// </summary>
        public async Task<bool> SaveAssetToCacheAsync(UUID assetID, byte[] assetData, CancellationToken cancellationToken = default)
        {
            if (!Operational())
            {
                return false;
            }

            try
            {
                var path = FileName(assetID);
                DebugLog("Saving " + path + " to asset cache.");

                if (!Directory.Exists(Client.Settings.ASSET_CACHE_DIR))
                {
                    Directory.CreateDirectory(Client.Settings.ASSET_CACHE_DIR);
                }

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await fs.WriteAsync(assetData, 0, assetData.Length, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log("Failed saving asset to cache (" + ex.Message + ")", Helpers.LogLevel.Warning, Client);
                return false;
            }

            return true;
        }

        private void DebugLog(string message)
        {
            if (Client.Settings.LOG_DISKCACHE) Logger.DebugLog(message, Client);
        }

        /// <summary>
        /// Get the file name of the asset stored with given UUID
        /// </summary>
        /// <param name="assetID">UUID of the asset</param>
        /// <returns>Null if we don't have that UUID cached on disk, file name if found in the cache folder</returns>
        public string AssetFileName(UUID assetID)
        {
            if (!Operational())
            {
                return null;
            }

            string fileName = FileName(assetID);

            return File.Exists(fileName) ? fileName : null;
        }

        /// <summary>
        /// Checks if the asset exists in the local cache
        /// </summary>
        /// <param name="assetID">UUID of the asset</param>
        /// <returns>True is the asset is stored in the cache, otherwise false</returns>
        public bool HasAsset(UUID assetID)
        {
            return Operational() 
                && (File.Exists(FileName(assetID)) 
                    || File.Exists(StaticFileName(assetID)));
        }

        /// <summary>
        /// Wipes out entire cache
        /// </summary>
        public void Clear()
        {
            string cacheDir = Client.Settings.ASSET_CACHE_DIR;
            if (!Directory.Exists(cacheDir)) { return; }

            const string pattern = "????????-????-????-????-????????????";
            int num = 0;
            // Use EnumerateFiles to stream file entries instead of allocating an array
            foreach (var filePath in Directory.EnumerateFiles(cacheDir, pattern, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(filePath);
                    ++num;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to delete cache file {filePath}: {ex}", Helpers.LogLevel.Warning, Client);
                }
            }

            DebugLog($"Cleared out {num} files from the cache directory.");
        }

        /// <summary>
        /// Brings cache size to the 90% of the max size
        /// </summary>
        public async Task PruneAsync(CancellationToken cancellationToken = default)
        {
            string cacheDir = Client.Settings.ASSET_CACHE_DIR;
            if (!Directory.Exists(cacheDir))
            {
                cleanerEvent.Reset();
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    const string pattern = "????????-????-????-????-????????????";

                    // First, stream file paths to compute total size without allocating FileInfo[]
                    long size = 0;
                    var filePaths = Directory.EnumerateFiles(cacheDir, pattern, SearchOption.TopDirectoryOnly);
                    foreach (var p in filePaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(p);
                            size += fi.Length;
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"Failed accessing cache file {p}: {ex}", Helpers.LogLevel.Warning, Client);
                        }
                    }

                    if (size > Client.Settings.ASSET_CACHE_MAX_SIZE)
                    {
                        // Build a lightweight list of file metadata for sorting by LastAccessTime
                        var entries = new List<(string Path, long Length, DateTime LastAccess)>();
                        foreach (var p in Directory.EnumerateFiles(cacheDir, pattern, SearchOption.TopDirectoryOnly))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                var fi = new FileInfo(p);
                                entries.Add((p, fi.Length, fi.LastAccessTime));
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Failed accessing cache file {p}: {ex}", Helpers.LogLevel.Warning, Client);
                            }
                        }

                        // Sort by LastAccessTime ascending (oldest first)
                        entries.Sort((a, b) => a.LastAccess.CompareTo(b.LastAccess));

                        long targetSize = (long)(Client.Settings.ASSET_CACHE_MAX_SIZE * 0.9);
                        int num = 0;
                        foreach (var entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            ++num;
                            try
                            {
                                size -= entry.Length;
                                File.Delete(entry.Path);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Failed deleting cache file {entry.Path}: {ex}", Helpers.LogLevel.Warning, Client);
                            }
                            if (size < targetSize)
                            {
                                break;
                            }
                        }
                        DebugLog($"{num} files deleted from the cache, cache size now: {NiceFileSize(size)}");
                    }
                    else
                    {
                        DebugLog($"Cache size is {NiceFileSize(size)} file deletion not needed");
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cleanerEvent.Reset();
            }
        }

        /// <summary>
        /// Synchronous wrapper for PruneAsync for compatibility
        /// </summary>
        public void Prune()
        {
            PruneAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronously brings cache size to the 90% of the max size
        /// </summary>
        public void BeginPrune()
        {
            // Check if the background cache cleaning task is active first
            if (!cleanerEvent.IsSet)
            {
                cleanerEvent.Set();
                _ = Task.Run(() => PruneAsync());
            }
        }

        /// <summary>
        /// Adds up file sizes passes in a FileInfo array
        /// </summary>
        private static long GetFileSize(FileInfo[] files)
        {
            return files.Sum(file => file.Length);
        }

        /// <summary>
        /// Checks whether caching is enabled
        /// </summary>
        private bool Operational()
        {
            return Client.Settings.USE_ASSET_CACHE;
        }

        /// <summary>
        /// Periodically prune the cache
        /// </summary>
        private void cleanerTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            BeginPrune();
        }

        /// <summary>
        /// Nicely formats file sizes
        /// </summary>
        /// <param name="byteCount">Byte size we want to output</param>
        /// <returns>String with humanly readable file size</returns>
        private string NiceFileSize(long byteCount)
        {
            string size = "0 Bytes";
            if (byteCount >= 1073741824)
                size = $"{byteCount / 1073741824:##.##}" + " GB";
            else if (byteCount >= 1048576)
                size = $"{byteCount / 1048576:##.##}" + " MB";
            else if (byteCount >= 1024)
                size = $"{byteCount / 1024:##.##}" + " KB";
            else if (byteCount > 0 && byteCount < 1024)
                size = byteCount + " Bytes";

            return size;
        }

        /// <summary>
        /// Helper class for sorting files in the cache directory
        /// </summary>
        private class SortFilesByAccessTimeHelper : IComparer<FileInfo>
        {
            int IComparer<FileInfo>.Compare(FileInfo f1, FileInfo f2)
            {
                if (f2 != null && f1 != null && f1.LastAccessTime > f2.LastAccessTime)
                    return 1;
                if (f2 != null && f1 != null && f1.LastAccessTime < f2.LastAccessTime)
                    return -1;
                else
                    return 0;
            }
        }
    }
}
