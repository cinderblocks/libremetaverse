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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    /// <summary>
    /// Responsible for maintaining an avatar's inventory structure.
    /// Inventory constructs nodes and manages node children to maintain a coherent hierarchy.
    /// Other classes should not manipulate or create <see cref="InventoryNode"/> instances directly.
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
        /// The root folder of this avatar's inventory.
        /// Setting this will create or update the underlying node.
        /// </summary>
        public InventoryFolder RootFolder
        {
            get => RootNode?.Data as InventoryFolder;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                UpdateNodeFor(value);
                if (!Items.TryGetValue(value.UUID, out var rootNode))
                    throw new InventoryException($"Failed to set RootFolder; unknown node: {value.UUID}");
                RootNode = rootNode;
            }
        }

        /// <summary>
        /// The default shared library folder.
        /// Setting this will create or update the underlying node.
        /// </summary>
        public InventoryFolder LibraryFolder
        {
            get => LibraryRootNode?.Data as InventoryFolder;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                UpdateNodeFor(value);
                if (!Items.TryGetValue(value.UUID, out var libNode))
                    throw new InventoryException($"Failed to set LibraryFolder; unknown node: {value.UUID}");
                LibraryRootNode = libNode;
            }
        }

        /// <summary>
        /// The root node of the avatar's inventory.
        /// </summary>
        public InventoryNode RootNode { get; private set; }

        /// <summary>
        /// The root node of the default shared library.
        /// </summary>
        public InventoryNode LibraryRootNode { get; private set; }

        /// <summary>
        /// Returns the owner of the inventory.
        /// </summary>
        public UUID Owner { get; }

        /// <summary>
        /// Returns number of stored entries.
        /// </summary>
        public int Count => Items.Count;

        #endregion Properties

        private readonly GridClient Client;

        private ConcurrentDictionary<UUID, InventoryNode> Items;
        private readonly object itemsLock = new object();

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

        /// <summary>
        /// Returns all links that link to the specified <paramref name="assertId"/>.
        /// </summary>
        /// <param name="assertId">An inventory item's asset UUID.</param>
        /// <returns>List of link nodes that reference <paramref name="assertId"/>.</returns>
        public List<InventoryNode> FindAllLinks(UUID assertId)
        {
            // If we have no root, there are no links to find
            if (RootNode == null) return new List<InventoryNode>();

            // Snapshot the values to avoid enumerating a changing collection
            var snapshot = Items.Values.ToList();
            var links = snapshot.Where(node => IsLinkOf(node, assertId)).ToList();
            return links;
        }

        private static bool IsLinkOf(InventoryNode node, UUID assertId)
        {
            if (node.Data is InventoryItem item && item.AssetType == AssetType.Link)
            {
                return item.ActualUUID == assertId;
            }

            return false;
        }

        /// <summary>
        /// Returns the contents of the given folder.
        /// </summary>
        /// <param name="folder">The folder to list.</param>
        /// <returns>A list of <see cref="InventoryBase"/> entries contained in <paramref name="folder"/>.</returns>
        public List<InventoryBase> GetContents(InventoryFolder folder)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            return GetContents(folder.UUID);
        }

        /// <summary>
        /// Returns the contents of the specified folder UUID.
        /// </summary>
        /// <param name="folder">A folder's UUID.</param>
        /// <returns>The contents of the folder corresponding to <paramref name="folder"/>.</returns>
        /// <exception cref="InventoryException">When <paramref name="folder"/> does not exist in the inventory.</exception>
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
        /// Updates or inserts the specified inventory item or folder.
        /// </summary>
        /// <param name="item">The inventory object to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
        public void UpdateNodeFor(InventoryBase item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            InventoryObjectUpdatedEventArgs itemUpdatedEventArgs = null;
            InventoryObjectAddedEventArgs itemAddedEventArgs = null;

            lock (itemsLock)
            {
                InventoryNode itemParent = null;
                if (item.ParentUUID != UUID.Zero && !Items.TryGetValue(item.ParentUUID, out itemParent))
                {
                    // OK, we have no data on the parent, let's create a fake one.
                    var fakeParent = new InventoryFolder(item.ParentUUID)
                    {
                        DescendentCount = 1 // Dear god, please forgive me.
                    };
                    var fakeItemParent = new InventoryNode(fakeParent);
                    if (Items.TryAdd(item.ParentUUID, fakeItemParent))
                    {
                        itemParent = fakeItemParent;
                    }
                    else
                    {
                        Items.TryGetValue(item.ParentUUID, out itemParent);
                    }
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
                        itemUpdatedEventArgs = new InventoryObjectUpdatedEventArgs(itemNode.Data, item);
                    }

                    itemNode.Data = item;
                }
                else // We're adding.
                {
                    itemNode = new InventoryNode(item, itemParent);
                    bool added = Items.TryAdd(item.UUID, itemNode);
                    if (added && m_InventoryObjectAdded != null)
                    {
                        itemAddedEventArgs = new InventoryObjectAddedEventArgs(item);
                    }
                }
            }

            if(itemUpdatedEventArgs != null)
            {
                OnInventoryObjectUpdated(itemUpdatedEventArgs);
            }
            if(itemAddedEventArgs != null)
            {
                OnInventoryObjectAdded(itemAddedEventArgs);
            }
        }

        /// <summary>
        /// Returns the <see cref="InventoryNode"/> for the specified UUID, throwing when not found.
        /// </summary>
        /// <param name="uuid">The UUID of the node.</param>
        /// <returns>The corresponding <see cref="InventoryNode"/>.</returns>
        /// <exception cref="InventoryException">Thrown when the node does not exist.</exception>
        public InventoryNode GetNodeFor(UUID uuid)
        {
            if (!Items.TryGetValue(uuid, out var node))
                throw new InventoryException($"Unknown inventory node: {uuid}");
            return node;
        }

        /// <summary>
        /// Returns the node for the specified UUID or null if not found.
        /// This is a non-throwing convenience alternative to <see cref="GetNodeFor"/>.
        /// </summary>
        /// <param name="uuid">Node UUID</param>
        /// <returns>InventoryNode or null</returns>
        public InventoryNode GetNodeOrDefault(UUID uuid)
        {
            Items.TryGetValue(uuid, out var node);
            return node;
        }

        public bool TryGetNodeFor(UUID uuid, out InventoryNode node)
        {
            return Items.TryGetValue(uuid, out node);
        }

        /// <summary>
        /// Removes the InventoryObject and all related node data from Inventory.
        /// </summary>
        /// <param name="item">The InventoryObject to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
        public void RemoveNodeFor(InventoryBase item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            InventoryObjectRemovedEventArgs itemRemovedEventArgs = null;

            lock (itemsLock)
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
                        itemRemovedEventArgs = new InventoryObjectRemovedEventArgs(item);
                    }
                }

                // In case there's a new parent:
                if (Items.TryGetValue(item.ParentUUID, out var newParent))
                {
                    lock (newParent.Nodes.SyncRoot)
                        newParent.Nodes.Remove(item.UUID);
                }
            }

            if(itemRemovedEventArgs != null)
            {
                OnInventoryObjectRemoved(itemRemovedEventArgs);
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
        /// Attempts to retrieve an <see cref="InventoryBase"/> item associated with the specified UUID.
        /// </summary>
        /// <param name="uuid">The unique identifier of the item to retrieve.</param>
        /// <param name="item">When this method returns <c>true</c>, contains the <see cref="InventoryBase"/> item if found; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if an item with the specified UUID was found; otherwise, <c>false</c>.</returns>
        public bool TryGetValue(UUID uuid, out InventoryBase item)
        {
            item = null;

            if(TryGetNodeFor(uuid, out var node))
            {
                item = node.Data;
            }

            return item != null;
        }

        /// <summary>
        /// Non-throwing convenience getter that returns the <see cref="InventoryBase"/> for the UUID or null if not found.
        /// </summary>
        public InventoryBase GetValueOrDefault(UUID uuid)
        {
            return TryGetNodeFor(uuid, out var node) ? node.Data : null;
        }

        /// <summary>
        /// Attempts to retrieve an item of type <typeparamref name="T"/> associated with the specified UUID.
        /// </summary>
        public bool TryGetValue<T>(UUID uuid, out T item)
        {
            if (TryGetNodeFor(uuid, out var node) && node.Data is T requestedItem)
            {
                item = requestedItem;
                return true;
            }

            item = default;
            return false;
        }

        /// <summary>
        /// Non-throwing convenience getter that returns the item of type <typeparamref name="T"/> or default if not found or not compatible.
        /// </summary>
        public T GetValueOrDefault<T>(UUID uuid)
        {
            if (TryGetNodeFor(uuid, out var node) && node.Data is T requestedItem)
                return requestedItem;
            return default;
        }

        /// <summary>
        /// Check that Inventory contains the InventoryObject specified by <paramref name="obj"/>.
        /// </summary>
        /// <param name="obj">Object to check for</param>
        /// <returns>true if inventory contains object, false otherwise</returns>
        public bool Contains(InventoryBase obj)
        {
            return obj != null && Contains(obj.UUID);
        }

        /// <summary>
        /// Clear all entries from Inventory <see cref="Items"/> store.
        /// </summary>
        public void Clear()
        {
            Items.Clear();
        }


        /// <summary>
        /// Saves the current inventory structure to a cache file.
        /// </summary>
        /// <param name="filename">Name of the cache file to save to</param>
        public void SaveToDisk(string filename)
        {
            InventoryCache.SaveToDisk(filename, Items);
        }

        /// <summary>
        /// Asynchronous save to disk. Exceptions from the underlying implementation will propagate to the caller.
        /// </summary>
        /// <param name="filename">Cache filename</param>
        /// <param name="cancellationToken">Cancellation token (best-effort)</param>
        public Task SaveToDiskAsync(string filename, CancellationToken cancellationToken = default)
        {
            return filename == null 
                ? throw new ArgumentNullException(nameof(filename)) 
                : InventoryCache.SaveToDiskAsync(filename, Items, cancellationToken);
        }

        /// <summary>
        /// Restores inventory from a cache file. Returns the number of items restored or -1 on error.
        /// </summary>
        /// <param name="filename">Name of the cache file to load</param>
        public int RestoreFromDisk(string filename)
        {
            return InventoryCache.RestoreFromDisk(filename, Items);
        }

        /// <summary>
        /// Asynchronous restore from disk. Exceptions from the underlying implementation will propagate to the caller.
        /// </summary>
        public Task<int> RestoreFromDiskAsync(string filename, CancellationToken cancellationToken = default)
        {
            return filename == null 
                ? throw new ArgumentNullException(nameof(filename)) 
                : InventoryCache.RestoreFromDiskAsync(filename, Items, cancellationToken);
        }

        #region Operators

        /// <summary>
        /// Get or set an inventory entry by UUID. Setting to null removes the item.
        /// </summary>
        /// <param name="uuid">The UUID of the InventoryObject to get or set.</param>
        /// <returns>The InventoryObject corresponding to <see cref="UUID"/>.</returns>
        public InventoryBase this[UUID uuid]
        {
            get => !Items.TryGetValue(uuid, out var node) 
                ? throw new InventoryException($"Unknown inventory item: {uuid}") 
                : node.Data;
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
            OldObject = oldObject;
            NewObject = newObject;
        }
    }

    public class InventoryObjectRemovedEventArgs : EventArgs
    {
        public InventoryBase Obj { get; }

        public InventoryObjectRemovedEventArgs(InventoryBase obj)
        {
            Obj = obj;
        }
    }

    public class InventoryObjectAddedEventArgs : EventArgs
    {
        public InventoryBase Obj { get; }

        public InventoryObjectAddedEventArgs(InventoryBase obj)
        {
            Obj = obj;
        }
    }
    #endregion EventArgs
}
