/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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

using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    internal class InventoryCache
    {
        private static readonly string InventoryCacheMagic = "INVCACHE";
        private static readonly int InventoryCacheVersion = 1;

        /// <summary>
        /// Creates MessagePack serializer options for use in inventory cache serializing and deserializing
        /// </summary>
        /// <returns>MessagePack serializer options</returns>
        private static MessagePackSerializerOptions GetSerializerOptions()
        {
            var resolver = MessagePack.Resolvers.CompositeResolver.Create(
                new List<IMessagePackFormatter>()
                {
                    Formatters.UUIDFormatter.Instance,
                },
                new List<IFormatterResolver>
                {
                    StandardResolver.Instance,
                }
            );

            return MessagePackSerializerOptions
                .Standard
                .WithResolver(resolver);
        }

        /// <summary>
        /// Saves the current inventory structure to a cache file
        /// </summary>
        /// <param name="filename">Name of the cache file to save to</param>
        /// <param name="Items">Inventory store to write to disk</param>
        public static void SaveToDisk(string filename, ConcurrentDictionary<UUID, InventoryNode> Items)
        {
            try
            {
                using (var bw = new BinaryWriter(File.Open(filename, FileMode.Create)))
                {
                    var options = GetSerializerOptions();
                    var items = Items.Values.ToList();

                    Logger.Log($"Caching {items.Count} inventory items to {filename}", Helpers.LogLevel.Info);

                    bw.Write(Encoding.ASCII.GetBytes(InventoryCacheMagic));
                    bw.Write(InventoryCacheVersion);
                    MessagePackSerializer.Serialize(bw.BaseStream, items, options);
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error saving inventory cache to disk", Helpers.LogLevel.Error, e);
            }
        }

        /// <summary>
        /// Async variant of SaveToDisk using asynchronous streams and MessagePack async API
        /// Exceptions are propagated to the caller.
        /// </summary>
        public static async Task SaveToDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> Items, CancellationToken cancellationToken = default)
        {
            var options = GetSerializerOptions();
            var items = Items.Values.ToList();

            Logger.Log($"Caching {items.Count} inventory items to {filename}", Helpers.LogLevel.Info);

            // Use asynchronous FileStream
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                var magicBytes = Encoding.ASCII.GetBytes(InventoryCacheMagic);
                await fs.WriteAsync(magicBytes, 0, magicBytes.Length, cancellationToken).ConfigureAwait(false);

                var versionBytes = BitConverter.GetBytes(InventoryCacheVersion);
                await fs.WriteAsync(versionBytes, 0, versionBytes.Length, cancellationToken).ConfigureAwait(false);

                // MessagePack has an async serializer
                await MessagePackSerializer.SerializeAsync(fs, items, options, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Try-save variant that returns a (success, error) tuple instead of throwing.
        /// </summary>
        public static async Task<(bool Success, Exception Error)> TrySaveToDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> Items, CancellationToken cancellationToken = default)
        {
            try
            {
                await SaveToDiskAsync(filename, Items, cancellationToken).ConfigureAwait(false);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        /// <summary>
        /// Loads in inventory cache file into the inventory structure. Note only valid to call after login has been successful.
        /// </summary>
        /// <param name="filename">Name of the cache file to load</param>
        /// <param name="Items">Inventory store being populated from restore</param>
        /// <returns>The number of inventory items successfully reconstructed into the inventory node tree, or -1 on error</returns>
        public static int RestoreFromDisk(string filename, ConcurrentDictionary<UUID, InventoryNode> Items)
        {
            List<InventoryNode> cacheNodes;

            try
            {
                if (!File.Exists(filename))
                {
                    return -1;
                }

                using (var br = new BinaryReader(File.Open(filename, FileMode.Open)))
                {
                    var options = GetSerializerOptions();

                    if (br.BaseStream.Length < InventoryCacheMagic.Length + sizeof(int))
                    {
                        Logger.Log($"Invalid inventory cache file. Missing header.", Helpers.LogLevel.Warning);
                        return -1;
                    }

                    var magic = br.ReadBytes(InventoryCacheMagic.Length);
                    if (Encoding.ASCII.GetString(magic) != InventoryCacheMagic)
                    {
                        Logger.Log($"Invalid inventory cache file. Missing magic header.", Helpers.LogLevel.Warning);
                        return -1;
                    }

                    var version = br.ReadInt32();
                    if (version != InventoryCacheVersion)
                    {
                        Logger.Log($"Invalid inventory cache file. Expected version {InventoryCacheVersion}, got {version}.", Helpers.LogLevel.Warning);
                        return -1;
                    }

                    cacheNodes = MessagePackSerializer.Deserialize<List<InventoryNode>>(br.BaseStream, options);
                    if (cacheNodes == null)
                    {
                        Logger.Log($"Invalid inventory cache file. Failed to deserialize contents.", Helpers.LogLevel.Warning);
                        return -1;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error accessing inventory cache file", Helpers.LogLevel.Error, e);
                return -1;
            }

            Logger.Log($"Read {cacheNodes.Count} items from inventory cache file", Helpers.LogLevel.Info);

            var dirtyFolders = new HashSet<UUID>();

            // First pass: process InventoryFolders
            foreach (var cacheNode in cacheNodes)
            {
                if (!(cacheNode.Data is InventoryFolder cacheFolder))
                {
                    continue;
                }

                if (cacheNode.Data.ParentUUID == UUID.Zero)
                {
                    //We don't need the root nodes "My Inventory" etc as they will already exist for the correct
                    // user of this cache.
                    continue;
                }

                if (!Items.TryGetValue(cacheNode.Data.UUID, out var serverNode))
                {
                    // This is an orphaned folder that no longer exists on the server.
                    continue;
                }

                if (!(serverNode.Data is InventoryFolder serverFolder))
                {
                    Logger.Log($"Cached inventory node folder has a parent that is not an InventoryFolder", Helpers.LogLevel.Warning);
                    continue;
                }

                serverNode.NeedsUpdate = cacheFolder.Version != serverFolder.Version;

                if (serverNode.NeedsUpdate)
                {
                    Logger.DebugLog($"Inventory Cache/Server version mismatch on {cacheNode.Data.Name} {cacheFolder.Version} vs {serverFolder.Version}");
                    dirtyFolders.Add(cacheNode.Data.UUID);
                }
            }

            // Second pass: process InventoryItems
            var itemCount = 0;
            foreach (var cacheNode in cacheNodes)
            {
                if (!(cacheNode.Data is InventoryItem cacheItem))
                {
                    // Only process InventoryItems
                    continue;
                }

                if (!Items.TryGetValue(cacheNode.Data.ParentUUID, out var serverParentNode))
                {
                    // This item does not have a parent in our known inventory. The folder was probably deleted on the server
                    // and our cache is old
                    continue;
                }

                if (!(serverParentNode.Data is InventoryFolder serverParentFolder))
                {
                    Logger.Log($"Cached inventory node item {cacheItem.Name} has a parent {serverParentNode.Data.Name} that is not an InventoryFolder", Helpers.LogLevel.Warning);
                    continue;
                }

                if (dirtyFolders.Contains(serverParentFolder.UUID))
                {
                    // This item belongs to a folder that has been marked as dirty, so it too is dirty and must be skipped
                    continue;
                }

                if (Items.ContainsKey(cacheItem.UUID))
                {
                    // This item was already added to our Items store, likely added from previous server requests during this session
                    continue;
                }

                if (!Items.TryAdd(cacheItem.UUID, cacheNode))
                {
                    Logger.Log($"Failed to add cache item node {cacheItem.Name} with parent {serverParentFolder.Name}", Helpers.LogLevel.Info);
                    continue;
                }

                // Add this cached InventoryItem node to the parent
                cacheNode.Parent = serverParentNode;
                serverParentNode.Nodes.Add(cacheItem.UUID, cacheNode);
                itemCount++;
            }

            Logger.Log($"Reassembled {itemCount} items from inventory cache file", Helpers.LogLevel.Info);
            return itemCount;
        }

        /// <summary>
        /// Async variant of RestoreFromDisk using asynchronous streams and MessagePack async API
        /// Exceptions are propagated to the caller.
        /// </summary>
        public static async Task<int> RestoreFromDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> Items, CancellationToken cancellationToken = default)
        {
            List<InventoryNode> cacheNodes;

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("Inventory cache file not found", filename);
            }

            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                var options = GetSerializerOptions();

                if (fs.Length < InventoryCacheMagic.Length + sizeof(int))
                {
                    throw new InvalidDataException("Invalid inventory cache file. Missing header.");
                }

                var magicBytes = new byte[InventoryCacheMagic.Length];
                var read = await fs.ReadAsync(magicBytes, 0, magicBytes.Length, cancellationToken).ConfigureAwait(false);
                if (read != magicBytes.Length || Encoding.ASCII.GetString(magicBytes) != InventoryCacheMagic)
                {
                    throw new InvalidDataException("Invalid inventory cache file. Missing magic header.");
                }

                var versionBytes = new byte[sizeof(int)];
                read = await fs.ReadAsync(versionBytes, 0, versionBytes.Length, cancellationToken).ConfigureAwait(false);
                if (read != versionBytes.Length)
                {
                    throw new InvalidDataException("Invalid inventory cache file. Missing version.");
                }

                var version = BitConverter.ToInt32(versionBytes, 0);
                if (version != InventoryCacheVersion)
                {
                    throw new InvalidDataException($"Invalid inventory cache file. Expected version {InventoryCacheVersion}, got {version}.");
                }

                // Deserialize remaining stream using MessagePack async API
                cacheNodes = await MessagePackSerializer.DeserializeAsync<List<InventoryNode>>(fs, options, cancellationToken).ConfigureAwait(false);
                if (cacheNodes == null)
                {
                    throw new InvalidDataException("Invalid inventory cache file. Failed to deserialize contents.");
                }
            }

            Logger.Log($"Read {cacheNodes.Count} items from inventory cache file", Helpers.LogLevel.Info);

            var dirtyFolders = new HashSet<UUID>();

            // First pass: process InventoryFolders
            foreach (var cacheNode in cacheNodes)
            {
                if (!(cacheNode.Data is InventoryFolder cacheFolder))
                {
                    continue;
                }

                if (cacheNode.Data.ParentUUID == UUID.Zero)
                {
                    //We don't need the root nodes "My Inventory" etc as they will already exist for the correct
                    // user of this cache.
                    continue;
                }

                if (!Items.TryGetValue(cacheNode.Data.UUID, out var serverNode))
                {
                    // This is an orphaned folder that no longer exists on the server.
                    continue;
                }

                if (!(serverNode.Data is InventoryFolder serverFolder))
                {
                    Logger.Log($"Cached inventory node folder has a parent that is not an InventoryFolder", Helpers.LogLevel.Warning);
                    continue;
                }

                serverNode.NeedsUpdate = cacheFolder.Version != serverFolder.Version;

                if (serverNode.NeedsUpdate)
                {
                    Logger.DebugLog($"Inventory Cache/Server version mismatch on {cacheNode.Data.Name} {cacheFolder.Version} vs {serverFolder.Version}");
                    dirtyFolders.Add(cacheNode.Data.UUID);
                }
            }

            // Second pass: process InventoryItems
            var itemCount = 0;
            foreach (var cacheNode in cacheNodes)
            {
                if (!(cacheNode.Data is InventoryItem cacheItem))
                {
                    // Only process InventoryItems
                    continue;
                }

                if (!Items.TryGetValue(cacheNode.Data.ParentUUID, out var serverParentNode))
                {
                    // This item does not have a parent in our known inventory. The folder was probably deleted on the server
                    // and our cache is old
                    continue;
                }

                if (!(serverParentNode.Data is InventoryFolder serverParentFolder))
                {
                    Logger.Log($"Cached inventory node item {cacheItem.Name} has a parent {serverParentNode.Data.Name} that is not an InventoryFolder", Helpers.LogLevel.Warning);
                    continue;
                }

                if (dirtyFolders.Contains(serverParentFolder.UUID))
                {
                    // This item belongs to a folder that has been marked as dirty, so it too is dirty and must be skipped
                    continue;
                }

                if (Items.ContainsKey(cacheItem.UUID))
                {
                    // This item was already added to our Items store, likely added from previous server requests during this session
                    continue;
                }

                if (!Items.TryAdd(cacheItem.UUID, cacheNode))
                {
                    Logger.Log($"Failed to add cache item node {cacheItem.Name} with parent {serverParentFolder.Name}", Helpers.LogLevel.Info);
                    continue;
                }

                // Add this cached InventoryItem node to the parent
                cacheNode.Parent = serverParentNode;
                serverParentNode.Nodes.Add(cacheItem.UUID, cacheNode);
                itemCount++;
            }

            Logger.Log($"Reassembled {itemCount} items from inventory cache file", Helpers.LogLevel.Info);
            return itemCount;
        }

        /// <summary>
        /// Try-restore variant that returns a (success, count, error) tuple instead of throwing.
        /// </summary>
        public static async Task<(bool Success, int Count, Exception Error)> TryRestoreFromDiskAsync(string filename, ConcurrentDictionary<UUID, InventoryNode> Items, CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await RestoreFromDiskAsync(filename, Items, cancellationToken).ConfigureAwait(false);
                return (true, count, null);
            }
            catch (Exception ex)
            {
                return (false, -1, ex);
            }
        }
    }
}
