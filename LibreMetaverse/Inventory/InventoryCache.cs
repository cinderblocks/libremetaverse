using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

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
    }
}
