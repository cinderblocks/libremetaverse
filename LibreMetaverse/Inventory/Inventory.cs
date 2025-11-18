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

        /// <summary>Collection of all InventoryNodes</summary>
        private readonly ConcurrentDictionary<UUID, InventoryNode> Items;
        /// <summary>Index of direct children by parent UUID to avoid full Items scans</summary>
        private readonly ConcurrentDictionary<UUID, ConcurrentDictionary<UUID, InventoryNode>> ChildrenIndex;
        /// <summary>Index of links by the linked asset UUID for O(1) FindAllLinks</summary>
        private readonly ConcurrentDictionary<UUID, ConcurrentDictionary<UUID, InventoryNode>> LinksByAssetId;

        public Inventory(GridClient client)
            : this(client, client.Self.AgentID) { }

        public Inventory(GridClient client, UUID owner)
        {
            Client = client;
            Owner = owner;
            if (owner == UUID.Zero)
                Logger.Log("Inventory owned by nobody!", Helpers.LogLevel.Warning, Client);
            Items = new ConcurrentDictionary<UUID, InventoryNode>();
            ChildrenIndex = new ConcurrentDictionary<UUID, ConcurrentDictionary<UUID, InventoryNode>>();
            LinksByAssetId = new ConcurrentDictionary<UUID, ConcurrentDictionary<UUID, InventoryNode>>();
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

            if (LinksByAssetId.TryGetValue(assertId, out var dict))
            {
                return dict.Values.ToList();
            }
            return new List<InventoryNode>();
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
                foreach (var node in folderNode.Nodes.Values)
                {
                    contents.Add(node.Data);
                }
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

            // Resolve or create parent node
            InventoryNode itemParent = null;
            if (item.ParentUUID != UUID.Zero)
            {
                if (!Items.TryGetValue(item.ParentUUID, out itemParent))
                {
                    var fakeParent = new InventoryFolder(item.ParentUUID);
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
            }


            if (Items.TryGetValue(item.UUID, out var itemNode)) // We're updating.
            {
                // Update link index: remove old mapping if necessary, add new mapping after update
                try { RemoveNodeFromAllLinks(item.UUID); } catch { }

                var newItem = item as InventoryItem;
                if (newItem != null && newItem.AssetType == AssetType.Link)
                {
                    try { AddToLinksIndex(newItem.ActualUUID, itemNode); } catch { }
                }

                var oldParent = itemNode.Parent;

                int oldCount = (itemNode.Data is InventoryItem) ? 1 : (itemNode.Data is InventoryFolder oldFolder ? oldFolder.DescendentCount : 0);
                int newCount = (item is InventoryItem) ? 1 : (item is InventoryFolder newFolder ? newFolder.DescendentCount : 0);
                int delta = newCount - oldCount;

                // Handle parent change
                if (oldParent == null || itemParent == null || itemParent.Data.UUID != oldParent.Data.UUID)
                {
                    if (oldParent != null)
                    {
                        lock (oldParent.Nodes.SyncRoot)
                        {
                            oldParent.Nodes.Remove(item.UUID);
                        }

                        // Remove from children index of old parent
                        try
                        {
                            RemoveFromChildrenIndex(oldParent.Data.UUID, item.UUID);
                        }
                        catch { }

                        var ancDec = oldParent;
                        while (ancDec?.Data is InventoryFolder folder)
                        {
                            // Atomically decrement descendant count and clamp to zero
                            AtomicallyAdjustDescendentCount(folder, -oldCount);
                            ancDec = ancDec.Parent;
                        }
                    }

                    if (itemParent != null)
                    {
                        lock (itemParent.Nodes.SyncRoot)
                        {
                            itemParent.Nodes[item.UUID] = itemNode;
                        }

                        // Add to children index of new parent
                        try
                        {
                            AddToChildrenIndex(itemParent.Data.UUID, itemNode);
                        }
                        catch { }

                        var ancInc = itemParent;
                        while (ancInc?.Data is InventoryFolder folder)
                        {
                            // Atomically increment descendant count
                            AtomicallyAdjustDescendentCount(folder, newCount);
                            ancInc = ancInc.Parent;
                        }
                    }
                }
                else
                {
                    if (delta != 0)
                    {
                        var anc = oldParent;
                        while (anc?.Data is InventoryFolder folder)
                        {
                            AtomicallyAdjustDescendentCount(folder, delta);
                            anc = anc.Parent;
                        }
                    }
                }

                itemNode.Parent = itemParent;

                // Update data and prepare event
                if (m_InventoryObjectUpdated != null)
                {
                    itemUpdatedEventArgs = new InventoryObjectUpdatedEventArgs(itemNode.Data, item);
                }

                itemNode.Data = item;

                // Add to link index for new item if it's a link
                if (newItem != null && newItem.AssetType == AssetType.Link)
                {
                    try { AddToLinksIndex(newItem.ActualUUID, itemNode); } catch { }
                }
            }
            else // We're adding.
            {
                itemNode = new InventoryNode(item, itemParent);
                bool added = Items.TryAdd(item.UUID, itemNode);
                if (added)
                {
                    int addedCount = 0;

                    if (item is InventoryFolder addedFolder)
                    {
                        // initialize descendant count based on existing children (items already referencing this folder)
                        int existingChildrenCount = 0;
                        // sum item counts of existing child nodes whose ParentUUID == this folder
                        // sum item counts of existing direct child nodes using the children index
                        if (ChildrenIndex.TryGetValue(item.UUID, out var directChildren))
                        {
                            foreach (var kvp in directChildren)
                            {
                                var n = kvp.Value;
                                if (n != null && n.Data.UUID != item.UUID)
                                {
                                    existingChildrenCount += GetItemCountInSubtree(n);
                                }
                            }
                        }
                        addedFolder.DescendentCount = existingChildrenCount;
                        addedCount = existingChildrenCount;
                    }
                    else
                    {
                        // item is an InventoryItem
                        addedCount = 1;
                    }

                    // Increment ancestor counts by addedCount
                    if (itemParent != null && addedCount != 0)
                    {
                        var p = itemParent;
                        while (p?.Data is InventoryFolder folder)
                        {
                            AtomicallyAdjustDescendentCount(folder, addedCount);
                            p = p.Parent;
                        }
                    }

                    // Maintain children index for the new node
                    try
                    {
                        if (itemParent != null)
                        {
                            AddToChildrenIndex(itemParent.Data.UUID, itemNode);
                        }
                    }
                    catch { }

                    // Maintain links index for the new node if it's a link
                    if (item is InventoryItem newIt && newIt.AssetType == AssetType.Link)
                    {
                        try { AddToLinksIndex(newIt.ActualUUID, itemNode); } catch { }
                    }

                    if (m_InventoryObjectAdded != null)
                    {
                        itemAddedEventArgs = new InventoryObjectAddedEventArgs(item);
                    }
                }
            }

            if (itemUpdatedEventArgs != null)
                OnInventoryObjectUpdated(itemUpdatedEventArgs);
            if (itemAddedEventArgs != null)
                OnInventoryObjectAdded(itemAddedEventArgs);
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

            if (!Items.TryGetValue(item.UUID, out var node))
            {
                return;
            }

            var toRemove = new List<InventoryNode>();
            int removedItemCount = CollectSubtreeAndCount(node, toRemove);

            // Remove from parents and Items dictionary
            foreach (var n in toRemove)
            {
                if (n.Parent != null)
                {
                    lock (n.Parent.Nodes.SyncRoot)
                    {
                        n.Parent.Nodes.Remove(n.Data.UUID);
                    }
                }
                Items.TryRemove(n.Data.UUID, out _);
            }

            if (m_InventoryObjectRemoved != null)
            {
                itemRemovedEventArgs = new InventoryObjectRemovedEventArgs(item);
            }

            // In case there's a new parent (moved elsewhere), ensure it's cleaned up
            if (Items.TryGetValue(item.ParentUUID, out var newParent))
            {
                lock (newParent.Nodes.SyncRoot)
                {
                    newParent.Nodes.Remove(item.UUID);
                }
                try { RemoveFromChildrenIndex(newParent.Data.UUID, item.UUID); } catch { }
            }

            var ancestor = node.Parent;
            while (ancestor?.Data is InventoryFolder)
            {
                var folder = (InventoryFolder)ancestor.Data;
                AtomicallyAdjustDescendentCount(folder, -removedItemCount);
                ancestor = ancestor.Parent;
            }

            if (itemRemovedEventArgs != null)
                OnInventoryObjectRemoved(itemRemovedEventArgs);
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

        private void CollectSubtree(InventoryNode node, List<InventoryNode> list)
        {
            list.Add(node);
            lock (node.Nodes.SyncRoot)
            {
                foreach (var child in node.Nodes.Values)
                {
                    CollectSubtree(child, list);
                }
            }
        }

        private int CountSubtree(InventoryNode node)
        {
            var count = 1; // count this node
            lock (node.Nodes.SyncRoot)
            {
                foreach (var child in node.Nodes.Values)
                {
                    count += CountSubtree(child);
                }
            }
            return count;
        }

        // Count only InventoryItem instances in the subtree rooted at node (includes root if item)
        private int CountItemDescendants(InventoryNode node)
        {
            if (node == null) return 0;
            int count = (node.Data is InventoryItem) ? 1 : 0;
            lock (node.Nodes.SyncRoot)
            {
                foreach (var child in node.Nodes.Values)
                {
                    count += CountItemDescendants(child);
                }
            }
            return count;
        }

        // Iterative subtree item counter used by incremental updates
        private int GetItemCountInSubtree(InventoryNode node)
        {
            if (node == null) return 0;
            int count = 0;
            var stack = new Stack<InventoryNode>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n.Data is InventoryItem) count++;
                lock (n.Nodes.SyncRoot)
                {
                    foreach (var child in n.Nodes.Values)
                    {
                        stack.Push(child);
                    }
                }
            }
            return count;
        }

        // Collect subtree into list and return number of InventoryItem nodes collected
        private int CollectSubtreeAndCount(InventoryNode node, List<InventoryNode> list)
        {
            int count = 0;
            var stack = new Stack<InventoryNode>();
            stack.Push(node);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                list.Add(n);
                // Remove from children index as we're collecting for deletion
                try { RemoveFromChildrenIndex(n.Parent?.Data.UUID ?? UUID.Zero, n.Data.UUID); } catch { }
                // Remove from links index if this node is a link
                try
                {
                    if (n.Data is InventoryItem li && li.AssetType == AssetType.Link)
                    {
                        RemoveFromLinksIndex(li.ActualUUID, n.Data.UUID);
                    }
                }
                catch { }
                try { RemoveNodeFromAllLinks(n.Data.UUID); } catch { }
                if (n.Data is InventoryItem) count++;
                lock (n.Nodes.SyncRoot)
                {
                    foreach (var child in n.Nodes.Values)
                    {
                        stack.Push(child);
                    }
                }
            }
            return count;
        }

        // Add a child mapping to the children index
        private void AddToChildrenIndex(UUID parentUuid, InventoryNode child)
        {
            if (parentUuid == UUID.Zero || child == null) return;
            var dict = ChildrenIndex.GetOrAdd(parentUuid, _ => new ConcurrentDictionary<UUID, InventoryNode>());
            dict[child.Data.UUID] = child;
        }

        // Remove a child mapping from the children index
        private void RemoveFromChildrenIndex(UUID parentUuid, UUID childUuid)
        {
            if (parentUuid == UUID.Zero) return;
            if (ChildrenIndex.TryGetValue(parentUuid, out var dict))
            {
                dict.TryRemove(childUuid, out _);
                if (dict.IsEmpty)
                {
                    ChildrenIndex.TryRemove(parentUuid, out _);
                }
            }
        }
        
        // Add a mapping to the links index: assetId -> node
        private void AddToLinksIndex(UUID assetId, InventoryNode node)
        {
            if (assetId == UUID.Zero || node == null) return;
            var dict = LinksByAssetId.GetOrAdd(assetId, _ => new ConcurrentDictionary<UUID, InventoryNode>());
            dict[node.Data.UUID] = node;
        }

        // Remove a mapping from the links index
        private void RemoveFromLinksIndex(UUID assetId, UUID nodeUuid)
        {
            if (assetId == UUID.Zero) return;
            if (LinksByAssetId.TryGetValue(assetId, out var dict))
            {
                // Try direct remove by key first
                dict.TryRemove(nodeUuid, out _);

                try
                {
                    foreach (var kvp in dict.Keys)
                    {
                        if (dict.TryGetValue(kvp, out var existing) && existing?.Data?.UUID == nodeUuid)
                        {
                            dict.TryRemove(kvp, out _);
                        }
                    }
                }
                catch { }

                if (dict.IsEmpty)
                {
                    LinksByAssetId.TryRemove(assetId, out _);
                }
            }
        }

        // Remove a node UUID from all link mappings across all asset keys
        private void RemoveNodeFromAllLinks(UUID nodeUuid)
        {
            if (nodeUuid == UUID.Zero) return;
            foreach (var kv in LinksByAssetId)
            {
                var dict = kv.Value;
                try
                {
                    dict.TryRemove(nodeUuid, out _);
                }
                catch { }
                if (dict.IsEmpty)
                {
                    LinksByAssetId.TryRemove(kv.Key, out _);
                }
            }
        }

        // Atomically adjust a folder's DescendentCount by delta and clamp to >= 0
        private static void AtomicallyAdjustDescendentCount(InventoryFolder folder, int delta)
        {
            if (folder == null || delta == 0) return;

            int initial, newVal;
            do
            {
                initial = folder.DescendentCount;
                newVal = initial + delta;
                if (newVal < 0) newVal = 0;
            }
            while (Interlocked.CompareExchange(ref folder.DescendentCount, newVal, initial) != initial);
        }
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
