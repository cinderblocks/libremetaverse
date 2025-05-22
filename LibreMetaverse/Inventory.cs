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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if NET7_0_OR_GREATER
using MemoryPack;
#else
using System.Runtime.Serialization.Formatters.Binary;
#endif


namespace OpenMetaverse
{
    /// <inheritdoc />
    /// <summary>
    /// Exception class to identify inventory exceptions
    /// </summary>
    [Serializable]
    public class InventoryException : Exception
    {
        public InventoryException() { }
        public InventoryException(string message) : base(message) { }
        public InventoryException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Responsible for maintaining inventory structure. Inventory constructs nodes
    /// and manages node children as is necessary to maintain a coherent hierarchy.
    /// Other classes should not manipulate or create InventoryNodes explicitly. When
    /// A node's parent changes (when a folder is moved, for example) simply pass
    /// Inventory the updated InventoryFolder, and it will make the appropriate changes
    /// to its internal representation.
    /// </summary>
    public class Inventory
    {
        #region EventHandlers
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<InventoryObjectUpdatedEventArgs> m_InventoryObjectUpdated;

        ///<summary>Raises the InventoryObjectUpdated Event</summary>
        /// <param name="e">A InventoryObjectUpdatedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnInventoryObjectUpdated(InventoryObjectUpdatedEventArgs e)
        {
            var handler = m_InventoryObjectUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InventoryObjectUpdatedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<InventoryObjectUpdatedEventArgs> InventoryObjectUpdated
        {
            add { lock (m_InventoryObjectUpdatedLock) { m_InventoryObjectUpdated += value; } }
            remove { lock (m_InventoryObjectUpdatedLock) { m_InventoryObjectUpdated -= value; } }
        }
       
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<InventoryObjectRemovedEventArgs> m_InventoryObjectRemoved;

        ///<summary>Raises the InventoryObjectRemoved Event</summary>
        /// <param name="e">A InventoryObjectRemovedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnInventoryObjectRemoved(InventoryObjectRemovedEventArgs e)
        {
            var handler = m_InventoryObjectRemoved;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InventoryObjectRemovedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<InventoryObjectRemovedEventArgs> InventoryObjectRemoved
        {
            add { lock (m_InventoryObjectRemovedLock) { m_InventoryObjectRemoved += value; } }
            remove { lock (m_InventoryObjectRemovedLock) { m_InventoryObjectRemoved -= value; } }
        }
        
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<InventoryObjectAddedEventArgs> m_InventoryObjectAdded;

        ///<summary>Raises the InventoryObjectAdded Event</summary>
        /// <param name="e">A InventoryObjectAddedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnInventoryObjectAdded(InventoryObjectAddedEventArgs e)
        {
            var handler = m_InventoryObjectAdded;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InventoryObjectAddedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<InventoryObjectAddedEventArgs> InventoryObjectAdded
        {
            add { lock (m_InventoryObjectAddedLock) { m_InventoryObjectAdded += value; } }
            remove { lock (m_InventoryObjectAddedLock) { m_InventoryObjectAdded -= value; } }
        }
        #endregion EventHandlers

        #region Properties

        /// <summary>
        /// The root folder of this avatars inventory
        /// </summary>
        public InventoryFolder RootFolder
        {
            get => RootNode.Data as InventoryFolder;
            set 
            {
                UpdateNodeFor(value);
                RootNode = Items[value.UUID];
            }
        }

        /// <summary>
        /// The default shared library folder
        /// </summary>
        public InventoryFolder LibraryFolder
        {
            get => LibraryRootNode.Data as InventoryFolder;
            set
            {
                UpdateNodeFor(value);
                LibraryRootNode = Items[value.UUID];
            }
        }

        /// <summary>
        /// The root node of the avatars inventory
        /// </summary>
        public InventoryNode RootNode { get; private set; }

        /// <summary>
        /// The root node of the default shared library
        /// </summary>
        public InventoryNode LibraryRootNode { get; private set; }

        /// <summary>
        /// Returns owner of Inventory
        /// </summary>
        public UUID Owner { get; }

        /// <summary>
        /// Returns number of stored entries
        /// </summary>
        public int Count => Items.Count;

        #endregion Properties

        private readonly GridClient Client;
        //private InventoryManager Manager;
        private ConcurrentDictionary<UUID, InventoryNode> Items;

        public Inventory(GridClient client)
            : this(client, client.Self.AgentID) { }

        public Inventory(GridClient client, UUID owner)
        {
            Client = client;
            Owner = owner;
            if (owner == UUID.Zero)
                Logger.Log("Inventory owned by nobody!", Helpers.LogLevel.Warning, Client);
            Items = new ConcurrentDictionary<UUID, InventoryNode>();
        }

        public List<InventoryBase> GetContents(InventoryFolder folder)
        {
            return GetContents(folder.UUID);
        }

        /// <summary>
        /// Returns the contents of the specified folder
        /// </summary>
        /// <param name="folder">A folder's UUID</param>
        /// <returns>The contents of the folder corresponding to <paramref name="folder"/></returns>
        /// <exception cref="InventoryException">When <paramref name="folder"/> does not exist in the inventory</exception>
        public List<InventoryBase> GetContents(UUID folder)
        {
            if (!Items.TryGetValue(folder, out var folderNode))
                throw new InventoryException("Unknown folder: " + folder);
            lock (folderNode.Nodes.SyncRoot)
            {
                var contents = new List<InventoryBase>(folderNode.Nodes.Count);
                contents.AddRange(folderNode.Nodes.Values.Select(node => node.Data));
                return contents;
            }
        }

        /// <summary>
        /// Updates the state of the InventoryNode and inventory data structure that
        /// is responsible for the InventoryObject. If the item was previously not added to inventory,
        /// it adds the item, and updates structure accordingly. If it was, it updates the 
        /// InventoryNode, changing the parent node if <see cref="item.parentUUID"/> does 
        /// not match <see cref="node.Parent.Data.UUID" />.
        /// 
        /// You can not set the inventory root folder using this method
        /// </summary>
        /// <param name="item">The InventoryObject to store</param>
        public void UpdateNodeFor(InventoryBase item)
        {
            lock (Items)
            {
                InventoryNode itemParent = null;
                if (item.ParentUUID != UUID.Zero && !Items.TryGetValue(item.ParentUUID, out itemParent))
                {
                    // OK, we have no data on the parent, let's create a fake one.
                    var fakeParent = new InventoryFolder(item.ParentUUID)
                    {
                        DescendentCount = 1 // Dear god, please forgive me.
                    };
                    itemParent = new InventoryNode(fakeParent);
                    Items[item.ParentUUID] = itemParent;
                    // Unfortunately, this breaks the nice unified tree
                    // while we're waiting for the parent's data to come in.
                    // As soon as we get the parent, the tree repairs itself.
                    //Logger.DebugLog("Attempting to update inventory child of " +
                    //    item.ParentUUID.ToString() + " when we have no local reference to that folder", Client);
                }

                if (Items.TryGetValue(item.UUID, out var itemNode)) // We're updating.
                {
                    var oldParent = itemNode.Parent;
                    // Handle parent change
                    if (oldParent == null || itemParent == null || itemParent.Data.UUID != oldParent.Data.UUID)
                    {
                        if (oldParent != null)
                        {
                            lock (oldParent.Nodes.SyncRoot)
                                oldParent.Nodes.Remove(item.UUID);
                        }
                        if (itemParent != null)
                        {
                            lock (itemParent.Nodes.SyncRoot)
                                itemParent.Nodes[item.UUID] = itemNode;
                        }
                    }

                    itemNode.Parent = itemParent;

                    if (m_InventoryObjectUpdated != null)
                    {
                        OnInventoryObjectUpdated(new InventoryObjectUpdatedEventArgs(itemNode.Data, item));
                    }

                    itemNode.Data = item;
                }
                else // We're adding.
                {
                    itemNode = new InventoryNode(item, itemParent);
                    bool added = Items.TryAdd(item.UUID, itemNode);
                    if (added && m_InventoryObjectAdded != null)
                    {
                        OnInventoryObjectAdded(new InventoryObjectAddedEventArgs(item));
                    }
                }
            }
        }

        public InventoryNode GetNodeFor(UUID uuid)
        {
            return Items[uuid];
        }

        /// <summary>
        /// Removes the InventoryObject and all related node data from Inventory.
        /// </summary>
        /// <param name="item">The InventoryObject to remove.</param>
        public void RemoveNodeFor(InventoryBase item)
        {
            lock (Items)
            {
                if (Items.TryGetValue(item.UUID, out var node))
                {
                    if (node.Parent != null)
                    {
                        lock (node.Parent.Nodes.SyncRoot)
                            node.Parent.Nodes.Remove(item.UUID);
                    }

                    bool removed = Items.TryRemove(item.UUID, out node);
                    if (removed && m_InventoryObjectRemoved != null)
                    {
                        OnInventoryObjectRemoved(new InventoryObjectRemovedEventArgs(item));
                    }                    
                }

                // In case there's a new parent:
                if (Items.TryGetValue(item.ParentUUID, out var newParent))
                {
                    lock (newParent.Nodes.SyncRoot)
                        newParent.Nodes.Remove(item.UUID);
                }
            }
        }

        /// <summary>
        /// Check that Inventory contains the InventoryObject specified by <paramref name="uuid"/>.
        /// </summary>
        /// <param name="uuid">The UUID to check.</param>
        /// <returns>true if inventory contains uuid, false otherwise</returns>
        public bool Contains(UUID uuid)
        {
            return Items.ContainsKey(uuid);
        }

        /// <summary>
        /// Check that Inventory contains the InventoryObject specified by <paramref name="obj"/>.
        /// </summary>
        /// <param name="obj">Object to check for</param>
        /// <returns>true if inventory contains object, false otherwise</returns>
        public bool Contains(InventoryBase obj)
        {
            return Contains(obj.UUID);
        }

        /// <summary>
        /// Clear all entries from Inventory <see cref="Items"/> store.
        /// Useful for regenerating contents.
        /// </summary>
        public void Clear()
        {
            Items.Clear();
        }

        /// <summary>
        /// Saves the current inventory structure to a cache file
        /// </summary>
        /// <param name="filename">Name of the cache file to save to</param>
        public void SaveToDisk(string filename)
        {
	        try
	        {
                using (Stream stream = File.Open(filename, FileMode.Create))
                {
#if !NET7_0_OR_GREATER
                    var bformatter = new BinaryFormatter();
#endif
                    lock (Items)
                    {
                        Logger.Log($"Caching {Items.Count} inventory items to {filename}", Helpers.LogLevel.Info);
                        foreach (var kvp in Items)
                        {
#if NET7_0_OR_GREATER
                            MemoryPackSerializer.SerializeAsync(stream, kvp.Value);
#else
                            bformatter.Serialize(stream, kvp.Value);
#endif
                        }
                    }
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
        /// <returns>The number of inventory items successfully reconstructed into the inventory node tree</returns>
        public int RestoreFromDisk(string filename)
        {
            var cacheNodes = new List<InventoryNode>();

            try
            {
                if (!File.Exists(filename))
                {
                    return -1;
                }

                using (var stream = File.Open(filename, FileMode.Open))
                {
#if !NET7_0_OR_GREATER
                    var bformatter = new BinaryFormatter();
#endif
                    while (stream.Position < stream.Length)
                    {
#if NET7_0_OR_GREATER                  
                        var node = MemoryPackSerializer.DeserializeAsync<InventoryNode>(stream);
                        cacheNodes.Add(node.Result);
#else
                        var node = (InventoryNode)bformatter.Deserialize(stream);
                        cacheNodes.Add(node);
#endif
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
                if(!(cacheNode.Data is InventoryFolder cacheFolder))
                {
                    continue;
                }

                if (cacheNode.ParentID == UUID.Zero)
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

                var serverFolder = (InventoryFolder)serverNode.Data;
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

                if (!Items.TryGetValue(cacheNode.ParentID, out var serverParentNode))
                {
                    // This item does not have a parent in our known inventory. The folder was probably deleted on the server
                    // and our cache is old
                    continue;
                }

                if(dirtyFolders.Contains(serverParentNode.Data.UUID))
                {
                    // This item belongs to a folder that has been marked as dirty, so it too is dirty and must be skipped
                    continue;
                }

                if(Items.ContainsKey(cacheItem.UUID))
                {
                    // This item was already added to our Items store, likely added from previous server requests during this session
                    continue;
                }

                if (!Items.TryAdd(cacheItem.UUID, cacheNode))
                {
                    Logger.Log($"Failed to add cache item node {cacheItem.Name} with parent {serverParentNode.Data.Name}", Helpers.LogLevel.Info);
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

        #region Operators

        /// <summary>
        /// By using the bracket operator on this class, the program can get the 
        /// InventoryObject designated by the specified uuid. If the value for the corresponding
        /// UUID is null, the call is equivalent to a call to <see cref="RemoveNodeFor(InventoryBase)" />.
        /// If the value is non-null, it is equivalent to a call to <see cref="UpdateNodeFor(InventoryBase)" />,
        /// the uuid parameter is ignored.
        /// </summary>
        /// <param name="uuid">The UUID of the InventoryObject to get or set, ignored if set to non-null value.</param>
        /// <returns>The InventoryObject corresponding to <see cref="UUID"/>.</returns>
        public InventoryBase this[UUID uuid]
        {
            get
            {
                var node = Items[uuid];
                return node.Data;
            }
            set
            {
                if (value != null)
                {
                    // Log a warning if there is a UUID mismatch, this will cause problems
                    if (value.UUID != uuid)
                    {
                        Logger.Log($"Inventory[uuid]: uuid {uuid} is not equal to value.UUID {value.UUID}",
                            Helpers.LogLevel.Warning, Client);
                    }
                    UpdateNodeFor(value);
                }
                else
                {
                    if (Items.TryGetValue(uuid, out var node))
                    {
                        RemoveNodeFor(node.Data);
                    }
                }
            }
        }

        #endregion Operators
        
    }
    #region EventArgs classes
    
    public class InventoryObjectUpdatedEventArgs : EventArgs
    {
        public InventoryBase OldObject { get; }
        public InventoryBase NewObject { get; }

        public InventoryObjectUpdatedEventArgs(InventoryBase oldObject, InventoryBase newObject)
        {
            this.OldObject = oldObject;
            this.NewObject = newObject;
        }
    }

    public class InventoryObjectRemovedEventArgs : EventArgs
    {
        public InventoryBase Obj { get; }

        public InventoryObjectRemovedEventArgs(InventoryBase obj)
        {
            this.Obj = obj;
        }
    }

    public class InventoryObjectAddedEventArgs : EventArgs
    {
        public InventoryBase Obj { get; }

        public InventoryObjectAddedEventArgs(InventoryBase obj)
        {
            this.Obj = obj;
        }
    }
    #endregion EventArgs
}
