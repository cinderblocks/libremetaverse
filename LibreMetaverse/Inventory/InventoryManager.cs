/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
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
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;
using System.Collections.Concurrent;
using LibreMetaverse.Threading;

namespace OpenMetaverse
{
    /// <summary>
    /// Tools for dealing with agents inventory
    /// </summary>
    [Serializable]
    public partial class InventoryManager : IDisposable
    {
        /// <summary>Used for converting shadow_id to asset_id</summary>
        public static readonly UUID MAGIC_ID = new UUID("3c115e51-04f4-523c-9fa6-98aff1034730");
        public static Task<List<InventoryBase>> NoResults = Task.FromResult<List<InventoryBase>>(null);
        /// <summary>Maximum items allowed to give</summary>
        public const int MAX_GIVE_ITEMS = 66; // viewer code says 66, but 42 in the notification
        protected struct InventorySearch
        {
            public UUID Folder;
            public UUID Owner;
            public string[] Path;
            public int Level;
        }

        [NonSerialized]
        private readonly GridClient Client;
        [NonSerialized]
        private Inventory _Store;
        [NonSerialized]
        private bool _disposed;
        [NonSerialized]
        private readonly CancellationTokenSource _callbackCleanupCts = new CancellationTokenSource();
        [NonSerialized]
        private readonly IReaderWriterLock _storeLock = new OptimisticReaderWriterLock();

        private long _CallbackPos, _SearchPos;
        private readonly ConcurrentDictionary<uint, ItemCreatedCallback> _ItemCreatedCallbacks = new ConcurrentDictionary<uint, ItemCreatedCallback>();
        private readonly ConcurrentDictionary<uint, ItemCopiedCallback> _ItemCopiedCallbacks = new ConcurrentDictionary<uint, ItemCopiedCallback>();
        private readonly ConcurrentDictionary<uint, InventoryType> _ItemInventoryTypeRequest = new ConcurrentDictionary<uint, InventoryType>();
        private readonly ConcurrentDictionary<uint, InventorySearch> _Searches = new ConcurrentDictionary<uint, InventorySearch>();

        /// <summary>Default timeout for waiting on a callback before cleaning it up (milliseconds)</summary>
        private const int CALLBACK_TIMEOUT_MS = 60000;

        #region Properties

        /// <summary>
        /// Get this agents Inventory data
        /// </summary>
        public Inventory Store => _Store;

        #endregion Properties

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">Reference to the GridClient object</param>
        public InventoryManager(GridClient client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.UpdateCreateInventoryItem, UpdateCreateInventoryItemHandler);
            Client.Network.RegisterCallback(PacketType.SaveAssetIntoInventory, SaveAssetIntoInventoryHandler);
            Client.Network.RegisterCallback(PacketType.BulkUpdateInventory, BulkUpdateInventoryHandler);
            Client.Network.RegisterEventCallback("BulkUpdateInventory", BulkUpdateInventoryCapHandler);
            Client.Network.RegisterCallback(PacketType.MoveInventoryItem, MoveInventoryItemHandler);
            Client.Network.RegisterCallback(PacketType.ReplyTaskInventory, ReplyTaskInventoryHandler);
            Client.Network.RegisterEventCallback("ScriptRunningReply", ScriptRunningReplyMessageHandler);

            // Deprecated and removed now in Second Life
            Client.Network.RegisterCallback(PacketType.InventoryDescendents, InventoryDescendentsHandler);
            Client.Network.RegisterCallback(PacketType.FetchInventoryReply, FetchInventoryReplyHandler);

            // Watch for inventory given to us through instant message            
            Client.Self.IM += Self_IM;

            // Register extra parameters with login and parse the inventory data that comes back
            Client.Network.RegisterLoginResponseCallback(
                Network_OnLoginResponse,
                new string[] {
                    "inventory-root", "inventory-skeleton", "inventory-lib-root",
                    "inventory-lib-owner", "inventory-skel-lib"});
        }

        /// <summary>
        /// Deterministically release resources and unregister callbacks.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    if (Client?.Network != null)
                    {
                        try { Client.Network.UnregisterCallback(PacketType.UpdateCreateInventoryItem, UpdateCreateInventoryItemHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister UpdateCreateInventoryItem callback", ex, Client); }
                        try { Client.Network.UnregisterCallback(PacketType.SaveAssetIntoInventory, SaveAssetIntoInventoryHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister SaveAssetIntoInventory callback", ex, Client); }
                        try { Client.Network.UnregisterCallback(PacketType.BulkUpdateInventory, BulkUpdateInventoryHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister BulkUpdateInventory callback", ex, Client); }
                        try { Client.Network.UnregisterEventCallback("BulkUpdateInventory", BulkUpdateInventoryCapHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister BulkUpdateInventory event callback", ex, Client); }
                        try { Client.Network.UnregisterCallback(PacketType.MoveInventoryItem, MoveInventoryItemHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister MoveInventoryItem callback", ex, Client); }
                        try { Client.Network.UnregisterCallback(PacketType.ReplyTaskInventory, ReplyTaskInventoryHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister ReplyTaskInventory callback", ex, Client); }
                        try { Client.Network.UnregisterEventCallback("ScriptRunningReply", ScriptRunningReplyMessageHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister ScriptRunningReply event callback", ex, Client); }

                        // Deprecated callbacks
                        try { Client.Network.UnregisterCallback(PacketType.InventoryDescendents, InventoryDescendentsHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister InventoryDescendents callback", ex, Client); }
                        try { Client.Network.UnregisterCallback(PacketType.FetchInventoryReply, FetchInventoryReplyHandler); } catch (Exception ex) { Logger.Debug("Failed to unregister FetchInventoryReply callback", ex, Client); }

                        try { Client.Network.UnregisterLoginResponseCallback(Network_OnLoginResponse); } catch (Exception ex) { Logger.Debug("Failed to unregister login response callback", ex, Client); }
                    }

                    try { if (Client?.Self != null) Client.Self.IM -= Self_IM; } catch (Exception ex) { Logger.Debug("Failed to detach Self_IM event handler", ex, Client); }

                    try { _ItemCreatedCallbacks.Clear(); } catch (Exception ex) { Logger.Debug("Failed to clear ItemCreatedCallbacks", ex, Client); }
                    try { _ItemCopiedCallbacks.Clear(); } catch (Exception ex) { Logger.Debug("Failed to clear ItemCopiedCallbacks", ex, Client); }
                    try { _ItemInventoryTypeRequest.Clear(); } catch (Exception ex) { Logger.Debug("Failed to clear ItemInventoryTypeRequest", ex, Client); }
                    try { _Searches.Clear(); } catch (Exception ex) { Logger.Debug("Failed to clear Searches", ex, Client); }

                    try
                    {
                        _callbackCleanupCts.Cancel();
                        _callbackCleanupCts.Dispose();
                    }
                    catch (Exception ex) { Logger.Debug("Failed to cancel/dispose callback cleanup CTS", ex, Client); }

                    try { _Store = null; } catch (Exception ex) { Logger.Debug("Failed to clear inventory store reference", ex, Client); }
                }
                catch (Exception ex)
                {
                    // Log the unexpected exception during Dispose to help debugging
                    Logger.Error($"Unhandled exception in InventoryManager.Dispose: {ex.Message}", ex, Client);
                }
            }

            _disposed = true;
        }

        ~InventoryManager()
        {
            Dispose(false);
        }

        #region Fetch

        /// <summary>
        /// Fetch an inventory item from the dataserver
        /// </summary>
        /// <param name="itemID">The items <see cref="UUID"/></param>
        /// <param name="ownerID">The item Owners <see cref="OpenMetaverse.UUID"/></param>
        /// <param name="timeout">time to wait for results represented by <see cref="TimeSpan"/></param>
        /// <returns>An <see cref="InventoryItem"/> object on success, or null if no item was found</returns>
        /// <remarks>Items will also be sent to the <see cref="InventoryManager.OnItemReceived"/> event</remarks>
        [Obsolete("Use FetchItemAsync or FetchItemHttpAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public InventoryItem FetchItem(UUID itemID, UUID ownerID, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(timeout);
                try
                {
                    return FetchItemAsync(itemID, ownerID, cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        public async Task<InventoryItem> FetchItemHttpAsync(UUID itemId, UUID ownerId, CancellationToken token = default)
        {
            InventoryItem item = null;
            await RequestFetchInventoryHttpAsync(itemId, ownerId, token, list =>
            {
                item = list.FirstOrDefault();
            });
            return item;
        }

        /// <summary>
        /// Request A single inventory item
        /// </summary>
        /// <param name="itemID">The items <see cref="OpenMetaverse.UUID"/></param>
        /// <param name="ownerID">The item Owners <see cref="OpenMetaverse.UUID"/></param>
        /// <param name="cancellationToken">Cancellation token to cancel the request</param>
        /// <see cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(UUID itemID, UUID ownerID, CancellationToken cancellationToken = default)
        {
            RequestFetchInventory(new Dictionary<UUID, UUID>(1) { { itemID, ownerID } }, cancellationToken);
        }

        /// <summary>
        /// Request inventory items
        /// </summary>
        /// <param name="items">Inventory items to request with owner</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request</param>
        /// <see cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(Dictionary<UUID, UUID> items, CancellationToken cancellationToken = default)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                RequestFetchInventoryHttp(items, cancellationToken);
                return;
            }

            var fetch = new FetchInventoryPacket
            {
                AgentData = new FetchInventoryPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = new FetchInventoryPacket.InventoryDataBlock[items.Count]
            };

            for (var i = 0; i < items.Count; ++i)
            {
                fetch.InventoryData[i] = new FetchInventoryPacket.InventoryDataBlock
                {
                    ItemID = items.ElementAt(i).Key,
                    OwnerID = items.ElementAt(i).Value
                };
            }

            Client.Network.SendPacket(fetch);
        }

        /// <summary>
        /// Request inventory items via Capabilities
        /// </summary>
        /// <param name="items">Inventory items to request with owners</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request</param>
        /// <see cref="OnItemReceived"/>
        private void RequestFetchInventoryHttp(Dictionary<UUID, UUID> items, CancellationToken cancellationToken = default)
        {
            // Fire-and-forget the async request. Use discard to explicitly start the task
            _ = RequestFetchInventoryHttpAsync(items, cancellationToken);
        }

        /// <summary>
        /// Request inventory items via HTTP capability
        /// </summary>
        /// <param name="itemID">The items <see cref="OpenMetaverse.UUID"/></param>
        /// <param name="ownerID">The item Owners <see cref="OpenMetaverse.UUID"/></param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <param name="callback">Action</param>
        private async Task RequestFetchInventoryHttpAsync(UUID itemID, UUID ownerID,
            CancellationToken cancellationToken, Action<List<InventoryItem>> callback = null)
        {
            await RequestFetchInventoryHttpAsync(new Dictionary<UUID, UUID>(1) { { itemID, ownerID } },
                cancellationToken, callback);
        }

        /// <summary>
        /// Request inventory items via HTTP capability
        /// </summary>
        /// <param name="items">Inventory items to request with owner</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request</param>
        /// <param name="callback">Action</param>
        public async Task RequestFetchInventoryHttpAsync(Dictionary<UUID, UUID> items,
            CancellationToken cancellationToken, Action<List<InventoryItem> > callback = null)
        {

            var cap = GetCapabilityURI("FetchInventory2");
            if (cap == null)
            {
                Logger.Warn($"Failed to obtain FetchInventory2 capability on {Client.Network.CurrentSim?.Name}", Client);
                return;
            }

            var payload = new OSDMap { ["agent_id"] = Client.Self.AgentID };

            var itemArray = new OSDArray(items.Count);
            foreach (var item in items.Select(kvp => new OSDMap(2)
                     {
                         ["item_id"] = kvp.Key,
                         ["owner_id"] = kvp.Value
                     }))
            {
                itemArray.Add(item);
            }

            payload["items"] = itemArray;

            try
            {
                var result = await PostCapAsync(cap, payload, cancellationToken).ConfigureAwait(false);
                if (result is OSDMap res && res.TryGetValue("items", out var itemsOsd) && itemsOsd is OSDArray itemsArray)
                {
                    var retrievedItems = new List<InventoryItem>(itemsArray.Count);
                    foreach (var it in itemsArray)
                    {
                        var item = InventoryItem.FromOSD(it);
                        // Update store under write lock to avoid races
                        if (_Store != null)
                        {
                            using (var writeLock = _storeLock.WriteLock())
                            {
                                _Store[item.UUID] = item;
                            }
                        }
                        else
                        {
                            Logger.Debug("Inventory store is not initialized, fetched item will not be cached locally", Client);
                        }
                        retrievedItems.Add(item);
                        OnItemReceived(new ItemReceivedEventArgs(item));
                    }

                    callback?.Invoke(retrievedItems);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed getting data from FetchInventory2 capability.", ex, Client);
            }
        }

        /// <summary>
        /// Retrieve contents of a folder
        /// </summary>
        /// <param name="folder">The <see cref="UUID"/> of the folder to search</param>
        /// <param name="owner">The <see cref="UUID"/> of the folders owner</param>
        /// <param name="fetchFolders">retrieve folders</param>
        /// <param name="fetchItems">retrieve items</param>
        /// <param name="order">sort order to return results in</param>
        /// <param name="timeout">time given to wait for results</param>
        /// <param name="followLinks">Resolve link items to the actual item</param>
        /// <returns>A list of inventory items matching search criteria within folder</returns>
        /// <see cref="RequestFolderContents(UUID,UUID,bool,bool,InventorySortOrder,CancellationToken)"/>
        /// <remarks>InventoryFolder.DescendentCount will only be accurate if both folders and items are
        /// requested</remarks>
        [Obsolete("Use FolderContentsAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public List<InventoryBase> FolderContents(UUID folder, UUID owner, bool fetchFolders, bool fetchItems,
            InventorySortOrder order, TimeSpan timeout, bool followLinks = false)
        {
            if (_Store == null)
            {
                var msg = "Inventory store not initialized, cannot get folder contents.";
                Logger.Warn(msg, Client);
                return new List<InventoryBase>();
            }

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(timeout);
                List<InventoryBase> inventory = null;
                try
                {
                    inventory = FolderContentsAsync(folder, owner, fetchFolders, fetchItems, order, cts.Token, followLinks).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    inventory = null;
                }
                catch
                {
                    inventory = null;
                }

                if (inventory == null)
                {
                    try
                    {
                        inventory = _Store != null ? _Store.GetContents(folder) : new List<InventoryBase>();
                    }
                    catch (InventoryException)
                    {
                        inventory = new List<InventoryBase>();
                    }
                }

                return inventory;
            }
        }

        /// <summary>
        /// Request the contents of an inventory folder using HTTP capabilities
        /// </summary>
        /// <param name="folderID">The folder to search</param>
        /// <param name="ownerID">The folder owners <see cref="UUID"/></param>
        /// <param name="fetchFolders">true to return <see cref="InventoryFolder"/>s contained in folder</param>
        /// <param name="fetchItems">true to return <see cref="InventoryItem"/>s contained in folder</param>
        /// <param name="order">the sort order to return items in</param>
        /// <param name="cancellationToken">CancellationToken for operation</param>
        /// <see cref="InventoryManager.FolderContents"/>
        public async Task<List<InventoryBase>> RequestFolderContents(UUID folderID, UUID ownerID, 
            bool fetchFolders, bool fetchItems, InventorySortOrder order, CancellationToken cancellationToken = default)
        {
            var cap = (ownerID == Client.Self.AgentID) ? "FetchInventoryDescendents2" : "FetchLibDescendents2";
            Uri url = GetCapabilityURI(cap);
            if (url == null)
            {
                OnFolderUpdated(new FolderUpdatedEventArgs(folderID, false));
                return await NoResults;
            }
            var folder = new InventoryFolder(folderID)
            {
                OwnerID = ownerID,
                UUID = folderID
            };
            return await RequestFolderContents(new List<InventoryFolder>(1) { folder }, 
                url, fetchFolders, fetchItems, order, cancellationToken);
        }

        /// <summary>
        /// Request the contents of an inventory folder using HTTP capabilities
        /// </summary>
        /// <param name="batch"><see cref="List" /> of folders to search</param>
        /// <param name="capabilityUri">HTTP capability <see cref="Uri"/> to POST</param>
        /// <param name="fetchFolders">true to return <see cref="InventoryFolder"/>s contained in folder</param>
        /// <param name="fetchItems">true to return <see cref="InventoryItem"/>s contained in folder</param>
        /// <param name="order">the sort order to return items in</param>
        /// <param name="cancellationToken">CancellationToken for operation</param>
        /// <see cref="InventoryManager.FolderContents"/>
        public async Task<List<InventoryBase>> RequestFolderContents(List<InventoryFolder> batch, Uri capabilityUri, 
            bool fetchFolders, bool fetchItems, InventorySortOrder order, CancellationToken cancellationToken = default)
        {
            List <InventoryBase> ret = null;
            try
            {
                var requestedFolders = new OSDArray(1);
                foreach (var requestedFolder in batch.Select(f => new OSDMap(1)
                         {
                             ["folder_id"] = f.UUID,
                             ["owner_id"] = f.OwnerID,
                             ["fetch_folders"] = fetchFolders,
                             ["fetch_items"] = fetchItems,
                             ["sort_order"] = (int)order
                         }))
                {
                    requestedFolders.Add(requestedFolder);
                }
                var payload = new OSDMap(1) { ["folders"] = requestedFolders };

                var result = await PostCapAsync(capabilityUri, payload, cancellationToken).ConfigureAwait(false);
                if (result is OSDMap resultMap && resultMap.TryGetValue("folders", out var foldersSd) && foldersSd is OSDArray fetchedFolders)
                {
                    ret = new List<InventoryBase>(fetchedFolders.Count);
                    foreach (var fetchedFolderNr in fetchedFolders)
                    {
                        var res = (OSDMap)fetchedFolderNr;
                        InventoryFolder fetchedFolder;

                        if (_Store.TryGetValue(res["folder_id"], out var invFolder) && invFolder is InventoryFolder folderCast)
                        {
                            fetchedFolder = folderCast;
                        }
                        else
                        {
                            fetchedFolder = new InventoryFolder(res["folder_id"]);
                            // Update store under write lock to avoid races
                            if (_Store != null)
                            {
                                using (var writeLock = _storeLock.WriteLock())
                                {
                                    _Store[fetchedFolder.UUID] = fetchedFolder;
                                }
                            }
                            else
                            {
                                Logger.Debug("Inventory store is not initialized, fetched folder will not be cached locally", Client);
                            }
                        }
                        fetchedFolder.DescendentCount = res["descendents"];
                        fetchedFolder.Version = res["version"];
                        fetchedFolder.OwnerID = res["owner_id"];
                        if (_Store != null && _Store.TryGetNodeFor(fetchedFolder.UUID, out var fetchedNode))
                        {
                            fetchedNode.NeedsUpdate = false;
                        }

                        // Do we have any descendants
                        if (fetchedFolder.DescendentCount > 0)
                        {
                            // Fetch descendent folders
                            if (res["categories"] is OSDArray folders)
                            {
                                foreach (var cat in folders)
                                {
                                    var descFolder = (OSDMap)cat;
                                    InventoryFolder folder;
                                    UUID folderID = descFolder.TryGetValue("category_id", out var category_id)
                                        ? category_id : descFolder["folder_id"];

                                    if (!(_Store != null 
                                          && _Store.TryGetValue(folderID, out var existing) 
                                          && existing is InventoryFolder existingFolder))
                                    {
                                        folder = new InventoryFolder(folderID)
                                        {
                                            ParentUUID = descFolder["parent_id"],
                                        };
                                        // Update store under write lock to avoid races
                                        if (_Store != null)
                                        {
                                            using (var writeLock = _storeLock.WriteLock())
                                            {
                                                _Store[folderID] = folder;
                                            }
                                        }
                                        else
                                        {
                                            Logger.Debug("Inventory store is not initialized, descendent folder will not be cached locally", Client);
                                        }
                                    }
                                    else
                                    {
                                        folder = existingFolder;
                                    }

                                    folder.OwnerID = descFolder["agent_id"];
                                    folder.Name = descFolder["name"];
                                    folder.Version = descFolder["version"];
                                    folder.PreferredType = (FolderType)descFolder["type_default"].AsInteger();
                                    ret.Add(folder);
                                }

                                // Fetch descendent items
                                if (res.TryGetValue("items", out var items))
                                {
                                    var arr = (OSDArray)items;
                                    foreach (var it in arr)
                                    {
                                        var item = InventoryItem.FromOSD(it);
                                        if (_Store != null)
                                        {
                                            using (var writeLock = _storeLock.WriteLock())
                                            {
                                                _Store[item.UUID] = item;
                                            }
                                        }
                                        else
                                        {
                                            Logger.Debug("Inventory store is not initialized, descendent item will not be cached locally", Client);
                                        }
                                        ret.Add(item);
                                    }
                                }
                            }
                        }
                        OnFolderUpdated(new FolderUpdatedEventArgs(res["folder_id"], true));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to fetch inventory descendants: {ex.Message}" + Environment.NewLine +
                           $"{ex.StackTrace}", Client);
                foreach (var f in batch)
                {
                    OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
                }
            }
            return ret;
        }

        #endregion Fetch

        #region Find

        /// <summary>
        /// Returns the UUID of the folder (category) that defaults to
        /// containing 'type'. The folder is not necessarily only for that
        /// type
        /// </summary>
        /// <remarks>This will return the root folder if one does not exist</remarks>
        /// <param name="type"></param>
        /// <returns>The UUID of the desired folder if found, the UUID of the RootFolder
        /// if not found, or UUID.Zero on failure</returns>
        public UUID FindFolderForType(AssetType type)
        {
            if (_Store == null)
            {
                Logger.Error("Inventory is null, FindFolderForType() lookup cannot continue", Client);
                return UUID.Zero;
            }

            if (_Store.RootFolder == null)
            {
                Logger.Error("Inventory RootFolder not initialized, FindFolderForType() lookup cannot continue", Client);
                return UUID.Zero;
            }

            // Folders go in the root
            if (type == AssetType.Folder)
                return _Store.RootFolder.UUID;

            // Loop through each top-level directory and check if PreferredType
            // matches the requested type
            var contents = _Store.GetContents(_Store.RootFolder.UUID);
            foreach (var inv in contents)
            {
                if (inv is InventoryFolder folder)
                {
                    if (folder.PreferredType == (FolderType)type)
                        return folder.UUID;
                }
            }

            // No match found, return Root Folder ID
            return _Store.RootFolder.UUID;
        }

        public UUID FindFolderForType(FolderType type)
        {
            if (_Store == null)
            {
                Logger.Error("Inventory is null, FindFolderForType() lookup cannot continue", Client);
                return UUID.Zero;
            }

            var contents = _Store.GetContents(_Store.RootFolder.UUID);
            foreach (var folder in contents.Select(inv => inv as InventoryFolder).Where(folder => folder?.PreferredType == type))
            {
                return folder.UUID;
            }

            // No match found, return Root Folder ID
            return _Store.RootFolder.UUID;
        }

        /// <summary>
        /// Find an object in inventory using a specific path to search
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="inventoryOwner">The object owners <see cref="UUID"/></param>
        /// <param name="path">A string path to search</param>
        /// <param name="timeout">time to wait for reply</param>
        /// <returns>Found items <see cref="UUID"/> or <see cref="UUID.Zero"/> if 
        /// timeout occurs or item is not found</returns>
        public UUID FindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(timeout);
                try
                {
                    return FindObjectByPathAsync(baseFolder, inventoryOwner, path, cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    return UUID.Zero;
                }
            }
        }

        /// <summary>
        /// Find inventory items by path
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="inventoryOwner">The object owners <see cref="UUID"/></param>
        /// <param name="path">A string path to search, folders/objects separated by a '/'</param>
        /// <remarks>Results are sent to the <see cref="InventoryManager.OnFindObjectByPath"/> event</remarks>
        public async Task RequestFindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Empty path is not supported");

            // Store this search using a generated id
            InventorySearch search;
            search.Folder = baseFolder;
            search.Owner = inventoryOwner;
            search.Path = path.Split('/');
            search.Level = 0;

            uint id;
            do
            {
                var v = Interlocked.Increment(ref _SearchPos);
                id = (uint)v;
            } while (id == 0);

            _Searches[id] = search;

            // Start the search
            await RequestFolderContents(baseFolder, inventoryOwner, true, true, InventorySortOrder.ByName, cancellationToken);
        }

        /// <summary>
        /// Search inventory Store object for an item or folder
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="path">An array which creates a path to search</param>
        /// <param name="level">Number of levels below baseFolder to conduct searches</param>
        /// <param name="firstOnly">if True, will stop searching after first match is found</param>
        /// <returns>A list of inventory items found</returns>
        public List<InventoryBase> LocalFind(UUID baseFolder, string[] path, int level, bool firstOnly)
        {
            var visited = new HashSet<UUID>();
            return LocalFindInternal(baseFolder, path, level, firstOnly, visited, 0);
        }

        private List<InventoryBase> LocalFindInternal(UUID baseFolder, string[] path, int level, bool firstOnly, HashSet<UUID> visited, int depth)
        {
            var objects = new List<InventoryBase>();

            // Defensive loop detection
            if (!visited.Add(baseFolder))
            {
                Logger.Warn($"Inventory loop detected during LocalFind: Folder {baseFolder} has already been visited. Circular parent reference detected at depth {depth}.", Client);
                return objects;
            }

            // Defensive depth limit
            const int maxDepth = 512;
            if (depth > maxDepth)
            {
                Logger.Warn($"Inventory LocalFind exceeded maximum depth of {maxDepth} at folder {baseFolder}. Possible circular reference or extremely deep hierarchy.", Client);
                return objects;
            }

            var contents = _Store.GetContents(baseFolder);

            foreach (var inv in contents.Where(inv => string.Compare(inv.Name, path[level], StringComparison.Ordinal) == 0))
            {
                if (level == path.Length - 1)
                {
                    objects.Add(inv);
                    if (firstOnly) return objects;
                }
                else if (inv is InventoryFolder)
                {
                    objects.AddRange(LocalFindInternal(inv.UUID, path, level + 1, firstOnly, visited, depth + 1));
                }
            }

            return objects;
        }

        #endregion Find

        #region Move/Rename

        /// <summary>
        /// Move an inventory item or folder to a new location
        /// </summary>
        /// <param name="item">The <see cref="T:InventoryBase"/> item or folder to move</param>
        /// <param name="newParent">The <see cref="T:InventoryFolder"/> to move item or folder to</param>
        public void Move(InventoryBase item, InventoryFolder newParent)
        {
            if (item is InventoryFolder)
                MoveFolder(item.UUID, newParent.UUID);
            else
                MoveItem(item.UUID, newParent.UUID);
        }

        /// <summary>
        /// Move an inventory item or folder to a new location and change its name
        /// </summary>
        /// <param name="item">The <see cref="T:InventoryBase"/> item or folder to move</param>
        /// <param name="newParent">The <see cref="T:InventoryFolder"/> to move item or folder to</param>
        /// <param name="newName">The name to change the item or folder to</param>
        [Obsolete("Method broken with AISv3. Use Move(item, parent) instead.")]
        public void Move(InventoryBase item, InventoryFolder newParent, string newName)
        {
            if (item is InventoryFolder)
                MoveFolder(item.UUID, newParent.UUID, newName);
            else
                MoveItem(item.UUID, newParent.UUID, newName);
        }

        /// <summary>
        /// Move and rename a folder
        /// </summary>
        /// <param name="folderID">The source folders <see cref="UUID"/></param>
        /// <param name="newParentID">The destination folders <see cref="UUID"/></param>
        /// <param name="newName">The name to change the folder to</param>
        [Obsolete("Method broken with AISv3. Use MoveFolder(folder, parent) and UpdateFolderProperties(folder, parent, name, type) instead")]
        public void MoveFolder(UUID folderID, UUID newParentID, string newName)
        {
            UpdateFolderProperties(folderID, newParentID, newName, FolderType.None);
        }

        /// <summary>
        /// Update folder properties
        /// </summary>
        /// <param name="folderID"><see cref="UUID"/> of the folder to update</param>
        /// <param name="parentID">Sets folder's parent to <see cref="UUID"/></param>
        /// <param name="name">Folder name</param>
        /// <param name="type">Folder type</param>
        public void UpdateFolderProperties(UUID folderID, UUID parentID, string name, FolderType type)
        {
            InventoryFolder inv = null;

            using (var upg = _storeLock.UpgradeableLock())
            {
                if (_Store != null 
                    && _Store.TryGetValue(folderID, out var storeItem) 
                    && storeItem is InventoryFolder item)
                {
                    // Retrieve node under read lock
                    inv = item;

                    // Upgrade to write lock and update the folder metadata
                    upg.Upgrade();
                    inv.Name = name;
                    inv.ParentUUID = parentID;
                    inv.PreferredType = type;
                    _Store.UpdateNodeFor(inv);
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                if (inv != null)
                {
                    _ = Client.AisClient.UpdateCategory(folderID, inv.GetOSD(), success =>
                    {
                        if (success)
                        {
                            // Ensure local store is updated (already updated above) but keep parity
                            using (var writeLock = _storeLock.WriteLock())
                            {
                                _Store.UpdateNodeFor(inv);
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
            else
            {
                var invFolder = new UpdateInventoryFolderPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    FolderData = new UpdateInventoryFolderPacket.FolderDataBlock[1]
                };
                invFolder.FolderData[0] = new UpdateInventoryFolderPacket.FolderDataBlock
                {
                    FolderID = folderID,
                    ParentID = parentID,
                    Name = Utils.StringToBytes(name),
                    Type = (sbyte)type
                };

                Client.Network.SendPacket(invFolder);
            }
        }

        /// <summary>
        /// Move a folder
        /// </summary>
        /// <param name="folderID">The source folders <see cref="UUID"/></param>
        /// <param name="newParentID">The destination folders <see cref="UUID"/></param>
        public void MoveFolder(UUID folderID, UUID newParentID, CancellationToken cancellationToken = default)
        {
            using (var writeLock = _storeLock.WriteLock())
            {
                if (_Store != null 
                    && _Store.TryGetValue(folderID, out var storeItem) 
                    && storeItem is InventoryFolder inv)
                {
                    inv.ParentUUID = newParentID;
                    _Store.UpdateNodeFor(inv);
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                // Fire-and-forget AIS move using Task-based API. Log failures and run onSuccess on success
                var moveTask = Client.AisClient.MoveCategoryAsync(folderID, newParentID, CancellationToken.None);
                ContinueWithLog(moveTask, $"MoveCategory {folderID} -> {newParentID}");
                return;
            }

            var move = new MoveInventoryFolderPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false //FIXME: ??
                },
                InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[1]
            };
            move.InventoryData[0] = new MoveInventoryFolderPacket.InventoryDataBlock
            {
                FolderID = folderID,
                ParentID = newParentID
            };

            Client.Network.SendPacket(move);
        }

        /// <summary>
        /// Move multiple folders, the keys in the Dictionary parameter,
        /// to a new parents, the value of that folder's key.
        /// </summary>
        /// <param name="foldersNewParents">A Dictionary containing the 
        /// <see cref="UUID"/> of the source as the key, and the 
        /// <see cref="UUID"/> of the destination as the value</param>
        public void MoveFolders(Dictionary<UUID, UUID> foldersNewParents, CancellationToken cancellationToken = default)
        {
            using (var writeLock = _storeLock.WriteLock())
            {
                foreach (var entry in foldersNewParents)
                {
                    if (_Store != null 
                        && _Store.TryGetValue(entry.Key, out var storeItem) 
                        && storeItem is InventoryFolder inv)
                    {
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                // Fire-and-forget AIS calls for each folder move. Run concurrently and log failures.
                var tasks = foldersNewParents.Select(kv => Client.AisClient.MoveCategoryAsync(kv.Key, kv.Value, CancellationToken.None)).ToArray();
                var whenAll = Task.WhenAll(tasks);
                ContinueWithWhenAllLog(whenAll, "MoveFolders", results =>
                {
                    var idx = 0;
                    foreach (var kv in foldersNewParents)
                    {
                        if (!results[idx])
                        {
                            Logger.Warn($"AIS MoveCategory failed for {kv.Key} -> {kv.Value}", Client);
                        }
                        idx++;
                    }
                });

                return;
            }

            //TODO: Test if this truly supports multiple-folder move
            var move = new MoveInventoryFolderPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false //FIXME: ??
                },
                InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[foldersNewParents.Count]
            };

            var index = 0;
            foreach (var folder in foldersNewParents)
            {
                var block = new MoveInventoryFolderPacket.InventoryDataBlock
                {
                    FolderID = folder.Key,
                    ParentID = folder.Value
                };
                move.InventoryData[index++] = block;
            }

            Client.Network.SendPacket(move);
        }

        /// <summary>
        /// Move an inventory item to a new folder (convenience overload)
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <see cref="UUID"/> of the destination folder</param>
        public void MoveItem(UUID itemID, UUID folderID, CancellationToken cancellationToken = default)
        {
            MoveItem(itemID, folderID, string.Empty, cancellationToken);
        }

        /// <summary>
        /// Move and optionally rename an inventory item
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <see cref="UUID"/> of the destination folder</param>
        /// <param name="newName">Optional new name for the item</param>
        public void MoveItem(UUID itemID, UUID folderID, string newName, CancellationToken cancellationToken = default)
        {
            // Update local store under write lock
            try
            {
                using (var writeLock = _storeLock?.WriteLock())
                {
                    if (_Store != null && _Store.TryGetValue(itemID, out var storeItem) && storeItem is InventoryItem inv)
                    {
                        if (!string.IsNullOrEmpty(newName)) inv.Name = newName;
                        inv.ParentUUID = folderID;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"MoveItem local update failed: {ex.Message}", Client);
            }

            // Prefer AISv3 when available
            if (Client?.AisClient?.IsAvailable == true)
            {
                var task = Client.AisClient.MoveItemAsync(itemID, folderID, CancellationToken.None);
                ContinueWithLog(task, $"MoveItem {itemID} -> {folderID}");
                return;
            }

            // Fallback to LLUDP packet
            var move = new MoveInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false
                },
                InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[1]
            };

            move.InventoryData[0] = new MoveInventoryItemPacket.InventoryDataBlock
            {
                ItemID = itemID,
                FolderID = folderID,
                NewName = Utils.StringToBytes(newName)
            };

            Client.Network.SendPacket(move);
        }

        /// <summary>
        /// Move multiple items, the keys in the Dictionary parameter,
        /// to a new folders, the value of that item's key.
        /// </summary>
        /// <param name="itemsNewFolders">A Dictionary containing the 
        /// <see cref="UUID"/> of the source as the key, and the 
        /// <see cref="UUID"/> of the destination as the value</param>
        public void MoveItems(Dictionary<UUID, UUID> itemsNewFolders, CancellationToken cancellationToken = default)
        {
            using (var writeLock = _storeLock.WriteLock())
            {
                foreach (var entry in itemsNewFolders)
                {
                    if (_Store != null && _Store.TryGetValue(entry.Key, out var storeItem) && storeItem is InventoryItem inv)
                    {
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                // Fire-and-forget for each item move using token-aware AIS calls
                var tasks = itemsNewFolders.Select(kv => Client.AisClient.MoveItemAsync(kv.Key, kv.Value, CancellationToken.None)).ToArray();
                var whenAll = Task.WhenAll(tasks);
                ContinueWithWhenAllLog(whenAll, "MoveItems", results =>
                {
                    var idx = 0;
                    foreach (var kv in itemsNewFolders)
                    {
                        if (!results[idx])
                        {
                            Logger.Warn($"AIS MoveItem failed for {kv.Key} -> {kv.Value}", Client);
                        }
                        idx++;
                    }
                });

                return;
            }

            var move = new MoveInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false //FIXME: ??
                },
                InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[itemsNewFolders.Count]
            };

            var index = 0;
            foreach (var item in itemsNewFolders)
            {
                var block = new MoveInventoryItemPacket.InventoryDataBlock
                {
                    ItemID = item.Key,
                    FolderID = item.Value
                };
                move.InventoryData[index++] = block;
            }

            Client.Network.SendPacket(move);
        }

        // MoveItem implementations are defined elsewhere in this partial class.

        #endregion Move

        #region Remove

        private void RemoveLocalUi(bool success, UUID itemId)
        {
            if (!success || _Store == null)
            {
                return;
            }

            // Collect all descendants and the root node to remove without recursion
            var toRemove = new List<InventoryBase>();

            using (var upg = _storeLock.UpgradeableLock())
            {
                if (!_Store.TryGetNodeFor(itemId, out var rootNode))
                {
                    return;
                }

                // Traverse descendants iteratively to avoid recursive write-lock reentrancy
                var stack = new Stack<UUID>();
                var visited = new HashSet<UUID>();
                int iterations = 0;
                const int maxIterations = 100000;
                
                stack.Push(itemId);

                while (stack.Count > 0)
                {
                    iterations++;
                    
                    // Defensive iteration limit
                    if (iterations > maxIterations)
                    {
                        Logger.Warn($"RemoveLocalUi exceeded maximum iterations ({maxIterations}). Possible circular reference in inventory hierarchy.", Client);
                        break;
                    }
                    
                    var id = stack.Pop();

                    // Defensive loop detection
                    if (!visited.Add(id))
                    {
                        Logger.Warn($"Inventory loop detected during RemoveLocalUi: Item {id} has already been visited. Circular parent reference detected.", Client);
                        continue;
                    }

                    // GetContents is a read operation
                    var children = _Store.GetContents(id);
                    foreach (var child in children)
                    {
                        toRemove.Add(child);

                        // If folder, traverse its children too
                        if (child is InventoryFolder)
                        {
                            stack.Push(child.UUID);
                        }
                    }
                }

                // Finally add the root node itself to the removal list
                toRemove.Add(rootNode.Data);
            }

            // Perform removals under write lock
            using (var writeLock = _storeLock.WriteLock())
            {
                foreach (var b in toRemove)
                {
                    try
                    {
                        _Store.RemoveNodeFor(b);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Failed removing inventory node {b}: {ex.Message}", ex, Client);
                    }
                }
            }
        }

        /// <summary>
        /// Remove descendants of a folder
        /// </summary>
        /// <param name="folder">The <see cref="UUID"/> of the folder</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        public void RemoveDescendants(UUID folder, CancellationToken cancellationToken = default)
        {
            // Preserve synchronous API by delegating to async-first implementation
            RemoveDescendantsAsync(folder, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task RemoveDescendantsAsync(UUID folder, CancellationToken cancellationToken = default)
        {
            if (Client.AisClient.IsAvailable)
            {
                try
                {
                    var success = await Client.AisClient.PurgeDescendentsAsync(folder, cancellationToken).ConfigureAwait(false);
                    RemoveLocalUi(success, folder);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn($"AIS PurgeDescendents exception for {folder}: {ex.Message}", Client);
                }
            }
            else
            {
                var purge = new PurgeInventoryDescendentsPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    InventoryData = { FolderID = folder }
                };
                Client.Network.SendPacket(purge);
                RemoveLocalUi(true, folder);
            }
        }

        public void RemoveItems(UUID[] items, CancellationToken cancellationToken = default)
        {
            // Preserve synchronous API by delegating to async-first implementation
            RemoveItemsAsync(items, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task RemoveItemsAsync(IEnumerable<UUID> items, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Client.AisClient.IsAvailable)
            {
                var tasks = items.Select(async n =>
                {
                    try
                    {
                        var success = await Client.AisClient.RemoveItemAsync(n, cancellationToken).ConfigureAwait(false);
                        if (success)
                        {
                            RemoveLocalUi(true, n);
                        }
                        else
                        {
                            Logger.Warn($"AIS RemoveItem failed for {n}", Client);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"AIS RemoveItem exception for {n}: {ex.Message}", Client);
                    }
                }).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            else
            {
#pragma warning disable CS0612 // Type or member is obsolete
                Remove(items.ToList(), new List<UUID>());
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        public void RemoveItem(UUID item, CancellationToken cancellationToken = default)
        {
            RemoveItemAsync(item, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task RemoveItemAsync(UUID item, CancellationToken cancellationToken = default)
        {
            if (Client.AisClient.IsAvailable)
            {
                try
                {
                    var success = await Client.AisClient.RemoveItemAsync(item, cancellationToken).ConfigureAwait(false);
                    if (success)
                    {
                        RemoveLocalUi(true, item);
                    }
                    else
                    {
                        Logger.Warn($"AIS RemoveItem failed for {item}", Client);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn($"AIS RemoveItem exception for {item}: {ex.Message}", Client);
                }
            }
            else
            {
                var items = new List<UUID>(1) { item };
#pragma warning disable CS0612 // Type or member is obsolete
                Remove(items, null);
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        public void RemoveFolder(UUID folder, CancellationToken cancellationToken = default)
        {
            RemoveFolderAsync(folder, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task RemoveFolderAsync(UUID folder, CancellationToken cancellationToken = default)
        {
            if (Client.AisClient.IsAvailable)
            {
                try
                {
                    var success = await Client.AisClient.RemoveCategoryAsync(folder, cancellationToken).ConfigureAwait(false);
                    if (success)
                    {
                        RemoveLocalUi(true, folder);
                    }
                    else
                    {
                        Logger.Warn($"AIS RemoveCategory failed for {folder}", Client);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn($"AIS RemoveCategory exception for {folder}: {ex.Message}", Client);
                }
            }
            else
            {
                var folders = new List<UUID>(1) { folder };
#pragma warning disable CS0612 // Type or member is obsolete
                Remove(null, folders);
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        /// <summary>
        /// Remove multiple items or folders from inventory. Note that this uses the LLUDP method
        /// which Second Life has deprecated and removed.
        /// </summary>
        /// <param name="items">A List containing the <see cref="UUID"/>s of items to remove</param>
        /// <param name="folders">A List containing the <see cref="UUID"/>s of the folders to remove</param>
        [Obsolete]
        public void Remove(List<UUID> items, List<UUID> folders)
        {
            if ((items == null || items.Count == 0) && (folders == null || folders.Count == 0))
                return;

            var rem = new RemoveInventoryObjectsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };

            if (items == null || items.Count == 0)
            {
                // To indicate that we want no items removed:
                rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[1];
                rem.ItemData[0] = new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = UUID.Zero };
            }
            else
            {
                using (var writeLock = _storeLock.WriteLock())
                {
                    rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[items.Count];
                    for (var i = 0; i < items.Count; i++)
                    {
                        rem.ItemData[i] = new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = items[i] };

                        // Update local copy
                        if (_Store != null && _Store.TryGetValue(items[i], out var storeItem))
                            _Store.RemoveNodeFor(storeItem);
                    }
                }
            }

            if (folders == null || folders.Count == 0)
            {
                // To indicate we want no folders removed:
                rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[1];
                rem.FolderData[0] = new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = UUID.Zero };
            }
            else
            {
                using (var writeLock = _storeLock.WriteLock())
                {
                    rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[folders.Count];
                    for (var i = 0; i < folders.Count; i++)
                    {
                        rem.FolderData[i] = new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = folders[i] };

                        // Update local copy
                        if (_Store != null && _Store.TryGetValue(folders[i], out var storeItem))
                            _Store.RemoveNodeFor(storeItem);
                    }
                }
            }
            Client.Network.SendPacket(rem);
        }

        /// <summary>
        /// Empty the Lost and Found folder
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        public void EmptyLostAndFound(CancellationToken cancellationToken = default)
        {
            // Preserve synchronous API by delegating to async-first implementation
            EmptyLostAndFoundAsync(cancellationToken).GetAwaiter().GetResult();
        }

        public async Task EmptyLostAndFoundAsync(CancellationToken cancellationToken = default)
        {
            // Try to locate the Lost and Found system folder in local store so we can update UI after successful server-side empty
            var folderKey = UUID.Zero;
            if (_Store?.RootFolder != null)
            {
                try
                {
                    var rootContents = _Store.GetContents(_Store.RootFolder);
                    foreach (var item in rootContents)
                    {
                        var folder = item as InventoryFolder;
                        if (folder?.PreferredType == FolderType.LostAndFound)
                        {
                            folderKey = folder.UUID;
                            break;
                        }
                    }
                }
                catch (Exception) { /* ignore errors when reading local store */ }
            }

            if (Client.AisClient.IsAvailable)
            {
                try
                {
                    // Use AIS async purge if available
                    var success = await Client.AisClient.PurgeDescendentsAsync(folderKey, cancellationToken).ConfigureAwait(false);
                    if (success && folderKey != UUID.Zero)
                    {
                        RemoveLocalUi(true, folderKey);
                    }
                    else if (!success)
                    {
                        Logger.Warn("AIS PurgeDescendents (LostAndFound) failed", Client);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn($"AIS PurgeDescendents (LostAndFound) exception: {ex.Message}", Client);
                }
            }
            else
            {
                EmptySystemFolder(FolderType.LostAndFound, cancellationToken);
            }
        }

        /// <summary>
        /// Empty the Trash folder
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        public void EmptyTrash(CancellationToken cancellationToken = default)
        {
            EmptyTrashAsync(cancellationToken).GetAwaiter().GetResult();
        }

        public async Task EmptyTrashAsync(CancellationToken cancellationToken = default)
        {
            // Try to locate the Trash system folder in local store so we can update UI after successful server-side empty
            var folderKey = UUID.Zero;
            if (_Store?.RootFolder != null)
            {
                try
                {
                    var rootContents = _Store.GetContents(_Store.RootFolder);
                    foreach (var item in rootContents)
                    {
                        var folder = item as InventoryFolder;
                        if (folder?.PreferredType == FolderType.Trash)
                        {
                            folderKey = folder.UUID;
                            break;
                        }
                    }
                }
                catch (Exception) { /* ignore errors when reading local store */ }
            }

            if (Client.AisClient.IsAvailable)
            {
                try
                {
                    var success = await Client.AisClient.EmptyTrash(cancellationToken).ConfigureAwait(false);
                    if (success && folderKey != UUID.Zero)
                    {
                        RemoveLocalUi(true, folderKey);
                    }
                    else if (!success)
                    {
                        Logger.Warn("AIS EmptyTrash failed", Client);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn($"AIS EmptyTrash exception: {ex.Message}", Client);
                }
            }
            else
            {
                EmptySystemFolder(FolderType.Trash, cancellationToken);
            }
        }

        private void EmptySystemFolder(FolderType folderType, CancellationToken cancellationToken = default)
        {
            if (_Store == null)
            {
                Logger.Warn("Inventory store not initialized, cannot empty system folder", Client);
                return;
            }

            var folderKey = UUID.Zero;

            var items = _Store.GetContents(_Store.RootFolder);
            foreach (var item in items)
            {
                var folder = item as InventoryFolder;
                if (folder?.PreferredType == folderType)
                {
                    folderKey = folder.UUID;
                    break;
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                if (folderKey != UUID.Zero)
                {
                    // Fire-and-forget AIS call
                    _ = Client.AisClient.PurgeDescendents(folderKey, RemoveLocalUi, cancellationToken);
                }
            }
            else
            {
                items = _Store.GetContents(folderKey);
                var remItems = new List<UUID>();
                var remFolders = new List<UUID>();
                foreach (var item in items)
                {
                    if (item is InventoryFolder)
                    {
                        remFolders.Add(item.UUID);
                    }
                    else
                    {
                        remItems.Add(item.UUID);
                    }
                }

#pragma warning disable CS0612 // Type or member is obsolete
                Remove(remItems, remFolders);
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }
        #endregion Remove

        #region Create

        /// <summary>
        /// Send a create item request
        /// </summary>
        /// <param name="parentFolder"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="assetTransactionID">Proper use is to upload the inventory's asset first, then provide the Asset's TransactionID here.</param>
        /// <param name="invType"></param>
        /// <param name="nextOwnerMask"></param>
        /// <param name="callback"></param>
        public void RequestCreateItem(UUID parentFolder, string name, string description, AssetType type, UUID assetTransactionID,
            InventoryType invType, PermissionMask nextOwnerMask, ItemCreatedCallback callback)
        {
            // Even though WearableType 0 is Shape, in this context it is treated as NOT_WEARABLE
            RequestCreateItem(parentFolder, name, description, type, assetTransactionID, invType, (WearableType)0, nextOwnerMask,
                callback);
        }

        /// <summary>
        /// Send a create item request
        /// </summary>
        /// <param name="parentFolder"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="assetTransactionID">Proper use is to upload the inventory's asset first, then provide the Asset's TransactionID here.</param>
        /// <param name="invType"></param>
        /// <param name="wearableType"></param>
        /// <param name="nextOwnerMask"></param>
        /// <param name="callback"></param>
        public void RequestCreateItem(UUID parentFolder, string name, string description, AssetType type, UUID assetTransactionID,
            InventoryType invType, WearableType wearableType, PermissionMask nextOwnerMask, ItemCreatedCallback callback)
        {
            var create = new CreateInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryBlock =
                {
                    CallbackID = RegisterItemCreatedCallback(callback),
                    FolderID = parentFolder,
                    TransactionID = assetTransactionID,
                    NextOwnerMask = (uint) nextOwnerMask,
                    Type = (sbyte) type,
                    InvType = (sbyte) invType,
                    WearableType = (byte) wearableType,
                    Name = Utils.StringToBytes(name),
                    Description = Utils.StringToBytes(description)
                }
            };

            Client.Network.SendPacket(create);
        }

        /// <summary>
        /// Creates a new inventory folder
        /// </summary>
        /// <param name="parentID">ID of the folder to put this folder in</param>
        /// <param name="name">Name of the folder to create</param>
        /// <returns>The UUID of the newly created folder</returns>
        public UUID CreateFolder(UUID parentID, string name)
        {
            return CreateFolder(parentID, name, FolderType.None);
        }

        /// <summary>
        /// Creates a new inventory folder
        /// </summary>
        /// <param name="parentID">ID of the folder to put this folder in</param>
        /// <param name="name">Name of the folder to create</param>
        /// <param name="preferredType">Sets this folder as the default folder
        /// for new assets of the specified type. Use <see cref="FolderType.None" />
        /// to create a normal folder, otherwise it will likely create a
        /// duplicate of an existing folder type</param>
        /// <returns>The UUID of the newly created folder</returns>
        /// <remarks>If you specify a preferred type of <see cref="AsseType.Folder" />
        /// it will create a new root folder which may likely cause all sorts
        /// of strange problems</remarks>
        public UUID CreateFolder(UUID parentID, string name, FolderType preferredType)
        {
            // If requesting a special preferred-type folder (e.g. Trash, LostAndFound, Current Outfit, etc.)
            // check the local store for an existing folder with the same preferred type and return it
            // instead of creating a duplicate.
            if (preferredType != FolderType.None)
            {
                try
                {
                    if (_Store?.RootFolder != null)
                    {
                        using (var upg = _storeLock.UpgradeableLock())
                        {
                            var rootContents = _Store.GetContents(_Store.RootFolder.UUID);
                            foreach (var item in rootContents)
                            {
                                if (item is InventoryFolder folder && folder.PreferredType == preferredType && folder.OwnerID == Client.Self.AgentID)
                                {
                                    // Found an existing system folder of the requested type, return its UUID
                                    return folder.UUID;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Error while checking for existing folder of type {preferredType}: {ex.Message}", ex, Client);
                }
            }

            var id = UUID.Random();

            // Assign a folder name if one is not already set
            if (string.IsNullOrEmpty(name))
            {
                name = preferredType.GetText() ?? "New Folder";
            }

            // Create the new folder locally
            var newFolder = new InventoryFolder(id)
            {
                Version = 1,
                DescendentCount = 0,
                ParentUUID = parentID,
                PreferredType = preferredType,
                Name = name,
                OwnerID = Client.Self.AgentID
            };

            // Update the local store if available
            try
            {
                if (_Store != null)
                {
                    using (_storeLock.WriteLock())
                    {
                        _Store[newFolder.UUID] = newFolder;
                    }
                }
                else
                {
                    Logger.Debug("Inventory store is not initialized, created folder will not be cached locally", Client);
                }
            }
            catch (InventoryException ie) { Logger.Warn(ie.Message, ie, Client); }

            // Create the CreateInventoryFolder packet and send it
            var create = new CreateInventoryFolderPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                FolderData =
                {
                    FolderID = id,
                    ParentID = parentID,
                    Type = (sbyte) preferredType,
                    Name = Utils.StringToBytes(name)
                }
            };

            Client.Network.SendPacket(create);

            return id;
        }

        /// <summary>
        /// Create an inventory item and upload asset data
        /// </summary>
        /// <param name="data">Asset data</param>
        /// <param name="name">Inventory item name</param>
        /// <param name="description">Inventory item description</param>
        /// <param name="assetType">Asset type</param>
        /// <param name="invType">Inventory type</param>
        /// <param name="folderID">Put newly created inventory in this folder</param>
        /// <param name="callback">Delegate that will receive feedback on success or failure</param>
        public void RequestCreateItemFromAsset(byte[] data, string name, string description, AssetType assetType,
            InventoryType invType, UUID folderID, ItemCreatedFromAssetCallback callback)
        {
            var permissions = new Permissions
            {
                EveryoneMask = PermissionMask.None,
                GroupMask = PermissionMask.None,
                NextOwnerMask = PermissionMask.All
            };

            try
            {
                // Forward to async-first implementation
                RequestCreateItemFromAssetAsync(data, name, description, assetType, invType, folderID, permissions, callback, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn($"RequestCreateItemFromAsset failed: {ex.Message}", ex, Client);
            }
        }

        /// <summary>
        /// Create an inventory item and upload asset data
        /// </summary>
        /// <param name="data">Asset data</param>
        /// <param name="name">Inventory item name</param>
        /// <param name="description">Inventory item description</param>
        /// <param name="assetType">Asset type</param>
        /// <param name="invType">Inventory type</param>
        /// <param name="folderID">Put newly created inventory in this folder</param>
        /// <param name="permissions">Permission of the newly created item 
        /// (EveryoneMask, GroupMask, and NextOwnerMask of Permissions struct are supported)</param>
        /// <param name="callback">Delegate that will receive feedback on success or failure</param>
        /// <param name="cancellationToken"></param>
        [Obsolete("Use RequestCreateItemFromAssetAsync")]
        public void RequestCreateItemFromAsset(byte[] data, string name, string description, AssetType assetType,
            InventoryType invType, UUID folderID, Permissions permissions, ItemCreatedFromAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("NewFileAgentInventory", false);
            if (cap == null)
            {
                throw new Exception("NewFileAgentInventory capability is not currently available");
            }

            var query = new OSDMap
            {
                {"folder_id", OSD.FromUUID(folderID)},
                {"asset_type", OSD.FromString(Utils.AssetTypeToString(assetType))},
                {"inventory_type", OSD.FromString(Utils.InventoryTypeToString(invType))},
                {"name", OSD.FromString(name)},
                {"description", OSD.FromString(description)},
                {"everyone_mask", OSD.FromInteger((int) permissions.EveryoneMask)},
                {"group_mask", OSD.FromInteger((int) permissions.GroupMask)},
                {"next_owner_mask", OSD.FromInteger((int) permissions.NextOwnerMask)},
                {"expected_upload_cost", OSD.FromInteger(Client.Settings.UPLOAD_COST)}
            };

            // Fire-and-forget using the async helper to preserve original non-blocking behavior
            _ = Task.Run(async () =>
            {
                try
                {
                    var res = await PostCapAsync(cap, query, cancellationToken).ConfigureAwait(false);
                    CreateItemFromAssetResponse(callback, data, query, res, null, cancellationToken);
                }
                catch (Exception ex)
                {
                    CreateItemFromAssetResponse(callback, data, query, null, ex, cancellationToken);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Creates inventory link to another inventory item or folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="bse">Inventory item or folder</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        /// <param name="cancellationToken"></param>
        public void CreateLink(UUID folderID, InventoryBase bse, ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            switch (bse)
            {
                case InventoryFolder folder:
                    CreateLink(folderID, folder, callback, cancellationToken);
                    break;
                case InventoryItem item:
                    CreateLink(folderID, item.UUID, item.Name, item.Description, item.InventoryType, UUID.Random(), callback, cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Creates inventory link to another inventory item
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="item">Original inventory item</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        /// <param name="cancellationToken"></param>
        public void CreateLink(UUID folderID, InventoryItem item, ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            CreateLink(folderID, item.UUID, item.Name, item.Description, item.InventoryType, UUID.Random(), callback, cancellationToken);
        }

        /// <summary>
        /// Creates inventory link to another inventory folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="folder">Original inventory folder</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        /// <param name="cancellationToken"></param>
        public void CreateLink(UUID folderID, InventoryFolder folder, ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            CreateLink(folderID, folder.UUID, folder.Name, "", InventoryType.Folder, UUID.Random(), callback, cancellationToken);
        }

        /// <summary>
        /// Creates inventory link to another inventory item or folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="itemID">Original item's UUID</param>
        /// <param name="name">Name</param>
        /// <param name="description">Description</param>
        /// <param name="invType">Inventory Type</param>
        /// <param name="transactionID">Transaction UUID</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        /// <param name="cancellationToken"></param>
        public void CreateLink(UUID folderID, UUID itemID, string name, string description, 
             InventoryType invType, UUID transactionID, ItemCreatedCallback callback, CancellationToken cancellationToken = default)
         {
            // preserve synchronous API by delegating to async-first implementation
            CreateLinkAsync(folderID, itemID, name, description, invType, transactionID, callback, cancellationToken).GetAwaiter().GetResult();
        }
        
        public async Task CreateLinkAsync(UUID folderID, UUID itemID, string name, string description,
            InventoryType invType, UUID transactionID, ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AssetType linkType = invType == InventoryType.Folder ? AssetType.LinkFolder : AssetType.Link;
            if (Client.AisClient.IsAvailable)
            {
                var links = new OSDArray();
                var link = new OSDMap
                {
                    ["linked_id"] = OSD.FromUUID(itemID),
                    ["type"] = OSD.FromInteger((sbyte)linkType),
                    ["inv_type"] = OSD.FromInteger((sbyte)invType),
                    ["name"] = OSD.FromString(name),
                    ["desc"] = OSD.FromString(description)
                };
                links.Add(link);

                var newInventory = new OSDMap { { "links", links } };

                var wrapped = WrapItemCreatedCallback(callback);
                try
                {
                    await Client.AisClient.CreateInventory(folderID, newInventory, true, wrapped, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn(ex.Message, Client);
                }
            }
            else
            {
                var create = new LinkInventoryItemPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    InventoryBlock = { CallbackID = RegisterItemCreatedCallback(callback) }
                };

                _ItemInventoryTypeRequest[create.InventoryBlock.CallbackID] = invType;
                create.InventoryBlock.FolderID = folderID;
                create.InventoryBlock.TransactionID = transactionID;
                create.InventoryBlock.OldItemID = itemID;
                create.InventoryBlock.Type = (sbyte)linkType;
                create.InventoryBlock.InvType = (sbyte)invType;
                create.InventoryBlock.Name = Utils.StringToBytes(name);
                create.InventoryBlock.Description = Utils.StringToBytes(description);

                Client.Network.SendPacket(create);
            }
        }

        #endregion Create

        #region Copy

        /// <summary>
        /// Send a copy item request
        /// </summary>
        /// <param name="item"></param>
        /// <param name="newParent"></param>
        /// <param name="newName"></param>
        /// <param name="callback"></param>
        public void RequestCopyItem(UUID item, UUID newParent, string newName, ItemCopiedCallback callback)
        {
            RequestCopyItem(item, newParent, newName, Client.Self.AgentID, callback);
        }

        /// <summary>
        /// Send a copy item request
        /// </summary>
        /// <param name="item"></param>
        /// <param name="newParent"></param>
        /// <param name="newName"></param>
        /// <param name="oldOwnerID"></param>
        /// <param name="callback"></param>
        public void RequestCopyItem(UUID item, UUID newParent, string newName, UUID oldOwnerID,
            ItemCopiedCallback callback)
        {
            var items = new List<UUID>(1) { item };
            var folders = new List<UUID>(1) { newParent };
            var names = new List<string>(1) { newName };

            RequestCopyItems(items, folders, names, oldOwnerID, callback);
        }

        /// <summary>
        /// Send a copy items request
        /// </summary>
        /// <param name="items"></param>
        /// <param name="targetFolders"></param>
        /// <param name="newNames"></param>
        /// <param name="oldOwnerID"></param>
        /// <param name="callback"></param>
        public void RequestCopyItems(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
            UUID oldOwnerID, ItemCopiedCallback callback)
        {
            // Forward to async-first implementation for consistency. Execute synchronously to preserve original API semantics.
            try
            {
                RequestCopyItemsAsync(items, targetFolders, newNames, oldOwnerID, callback).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn($"RequestCopyItems failed: {ex.Message}", ex, Client);
            }
        }

        /// <summary>
        /// Request a copy of an asset embedded within a notecard
        /// </summary>
        /// <param name="objectID">Usually UUID.Zero for copying an asset from a notecard</param>
        /// <param name="notecardID">UUID of the notecard to request an asset from</param>
        /// <param name="folderID">Put newly created inventory in this folder</param>
        /// <param name="itemID">UUID of the embedded asset</param>
        /// <param name="callback">callback to run when item is copied to inventory</param>
        /// <param name="cancellationToken"></param>
        public void RequestCopyItemFromNotecard(UUID objectID, UUID notecardID, UUID folderID, UUID itemID, ItemCopiedCallback callback, CancellationToken cancellationToken = default)
        {
            _ItemCopiedCallbacks[0] = callback; //Notecards always use callback ID 0

            var cap = GetCapabilityURI("CopyInventoryFromNotecard");
            if (cap != null)
            {
                var message = new CopyInventoryFromNotecardMessage
                {
                    CallbackID = 0,
                    FolderID = folderID,
                    ItemID = itemID,
                    NotecardID = notecardID,
                    ObjectID = objectID
                };

                _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, message.Serialize(), cancellationToken);
            }
            else
            {
                var copy = new CopyInventoryFromNotecardPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    NotecardData =
                    {
                        ObjectID = objectID,
                        NotecardItemID = notecardID
                    },
                    InventoryData = new CopyInventoryFromNotecardPacket.InventoryDataBlock[1]
                };


                copy.InventoryData[0] = new CopyInventoryFromNotecardPacket.InventoryDataBlock
                {
                    FolderID = folderID,
                    ItemID = itemID
                };

                Client.Network.SendPacket(copy);
            }
        }

        #endregion Copy

        #region Update

        /// <summary>
        /// Send an update item request
        /// </summary>
        /// <param name="item"></param>
        public void RequestUpdateItem(InventoryItem item)
        {
            var items = new List<InventoryItem>(1) { item };

            RequestUpdateItems(items, UUID.Random());
        }

        /// <summary>
        /// Send an update items request
        /// </summary>
        /// <param name="items"></param>
        public void RequestUpdateItems(List<InventoryItem> items)
        {
            RequestUpdateItems(items, UUID.Random());
        }

        /// <summary>
        /// Send an update items request
        /// </summary>
        /// <param name="items"></param>
        /// <param name="transactionID"></param>
        public void RequestUpdateItems(List<InventoryItem> items, UUID transactionID)
        {
            if (Client.AisClient.IsAvailable)
            {
                foreach (var item in items)
                {
                    var update = (OSDMap)item.GetOSD();
                    if (update.Remove("asset_id"))
                    {
                        if (item.TransactionID != UUID.Zero)
                        {
                            update["hash_id"] = item.TransactionID;
                        }
                    }
                    if (update.Remove("shadow_id"))
                    {
                        if (item.TransactionID != UUID.Zero)
                        {
                            update["hash_id"] = item.TransactionID;
                        }
                    }
                    // Fire-and-forget AIS update — wrap callback to merge into local store
                    Action<bool> aisCallback = success => { if (success) MergeUpdateIntoStore(update, item.UUID); };

                    _ = Client.AisClient.UpdateItem(item.UUID, update, aisCallback);
                }
            }
            else
            {
                var update = new UpdateInventoryItemPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        TransactionID = transactionID
                    },
                    InventoryData = new UpdateInventoryItemPacket.InventoryDataBlock[items.Count]
                };

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    var block = new UpdateInventoryItemPacket.InventoryDataBlock
                    {
                        BaseMask = (uint)item.Permissions.BaseMask,
                        CRC = ItemCRC(item),
                        CreationDate = (int)Utils.DateTimeToUnixTime(item.CreationDate),
                        CreatorID = item.CreatorID,
                        Description = Utils.StringToBytes(item.Description),
                        EveryoneMask = (uint)item.Permissions.EveryoneMask,
                        Flags = (uint)item.Flags,
                        FolderID = item.ParentUUID,
                        GroupID = item.GroupID,
                        GroupMask = (uint)item.Permissions.GroupMask,
                        GroupOwned = item.GroupOwned,
                        InvType = (sbyte)item.InventoryType,
                        ItemID = item.UUID,
                        Name = Utils.StringToBytes(item.Name),
                        NextOwnerMask = (uint)item.Permissions.NextOwnerMask,
                        OwnerID = item.OwnerID,
                        OwnerMask = (uint)item.Permissions.OwnerMask,
                        SalePrice = item.SalePrice,
                        SaleType = (byte)item.SaleType,
                        TransactionID = item.TransactionID,
                        Type = (sbyte)item.AssetType
                    };

                    update.InventoryData[i] = block;
                }

                Client.Network.SendPacket(update);
            }
        }

        /// <summary>
        /// Update an existing script in an agents Inventory
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        [Obsolete("Use RequestUpdateScriptAgentInventoryAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public void RequestUpdateScriptAgentInventory(byte[] data, UUID itemID, bool mono, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            // Forward to the async-first implementation and fire-and-forget to preserve original non-blocking behavior
            _ = RequestUpdateScriptAgentInventoryAsync(data, itemID, mono, callback, cancellationToken);
        }

        /// <summary>
        /// Send an upload notecard request
        /// </summary>
        /// <param name="data"></param>
        /// <param name="notecardID"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public void RequestUploadNotecardAsset(byte[] data, UUID notecardID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateNotecardAgentInventory", false);
            if (cap == null)
            {
                throw new Exception("Capability system not initialized to send asset");
            }

            var query = new OSDMap { { "item_id", OSD.FromUUID(notecardID) } };

            // Fire-and-forget the async-first implementation which will invoke the upload response handling
            _ = RequestUploadNotecardAssetAsync(data, notecardID, callback, cancellationToken);
        }

        /// <summary>
        /// Send an upload gesture request (synchronous wrapper preserved for compatibility)
        /// </summary>
        /// <param name="data">Gesture asset bytes</param>
        /// <param name="gestureID">UUID of the gesture item</param>
        /// <param name="callback">Callback invoked when upload completes</param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        [Obsolete("Use RequestUploadGestureAssetAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public void RequestUploadGestureAsset(byte[] data, UUID gestureID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateGestureAgentInventory", false);
            if (cap == null)
            {
                throw new Exception("UpdateGestureAgentInventory capability is not currently available");
            }

            var query = new OSDMap { { "item_id", OSD.FromUUID(gestureID) } };

            // Fire-and-forget the async-first implementation which will invoke the upload response handling
            _ = RequestUploadGestureAssetAsync(data, gestureID, callback, cancellationToken);
        }

        #endregion Update

        /// <summary>
        /// Wrapper for creating a new <see cref="InventoryItem"/> object
        /// </summary>
        /// <param name="type">The type of item from the <see cref="InventoryType"/> enum</param>
        /// <param name="id">The <see cref="UUID"/> of the newly created object</param>
        /// <returns><see cref="InventoryItem"/> with the type and id passed</returns>
        public static InventoryItem CreateInventoryItem(InventoryType type, UUID id)
        {
            switch (type)
            {
                case InventoryType.Texture: return new InventoryTexture(id);
                case InventoryType.Sound: return new InventorySound(id);
                case InventoryType.CallingCard: return new InventoryCallingCard(id);
                case InventoryType.Landmark: return new InventoryLandmark(id);
                case InventoryType.Object: return new InventoryObject(id);
                case InventoryType.Notecard: return new InventoryNotecard(id);
                case InventoryType.Category: return new InventoryCategory(id);
                case InventoryType.LSL: return new InventoryLSL(id);
                case InventoryType.Snapshot: return new InventorySnapshot(id);
                case InventoryType.Attachment: return new InventoryAttachment(id);
                case InventoryType.Wearable: return new InventoryWearable(id);
                case InventoryType.Animation: return new InventoryAnimation(id);
                case InventoryType.Gesture: return new InventoryGesture(id);
                case InventoryType.Settings: return new InventorySettings(id);
                case InventoryType.Material: return new InventoryMaterial(id);
                default: return new InventoryItem(type, id);
            }
        }

        /// <summary>
        /// Creates <see cref="InventoryItem"/> if item does not already exist in <see cref="Store"/>
        /// </summary>
        /// <param name="InvType">Item's <see cref="InventoryType"/></param>
        /// <param name="ItemID">Item's <see cref="UUID"/></param>
        /// <returns><see cref="InventoryItem"/> either prior stored or newly created</returns>
        /// <seealso cref="CreateInventoryItem"/>
        public InventoryItem CreateOrRetrieveInventoryItem(InventoryType InvType, UUID ItemID)
        {
            InventoryItem ret = null;

            if (_Store == null)
            {
                Logger.Warn("Inventory store not initialized, cannot create or retrieve inventory item", Client);
                return null;
            }

            if (_Store.TryGetValue(ItemID, out var storeItem) && storeItem is InventoryItem inventoryItem)
                ret = inventoryItem;

            return ret ?? (ret = CreateInventoryItem(InvType, ItemID));
        }

        /// <summary>
        /// Create a CRC from <see cref="InventoryItem"/>
        /// </summary>
        /// <param name="iitem">Source <see cref="InventoryItem"/></param>
        /// <returns>uint representing the source <see cref="InventoryItem"/> as a CRC</returns>
        public static uint ItemCRC(InventoryItem iitem)
        {
            uint CRC = 0;

            // IDs
            CRC += iitem.AssetUUID.CRC(); // AssetID
            CRC += iitem.ParentUUID.CRC(); // FolderID
            CRC += iitem.UUID.CRC(); // ItemID

            // Permission stuff
            CRC += iitem.CreatorID.CRC(); // CreatorID
            CRC += iitem.OwnerID.CRC(); // OwnerID
            CRC += iitem.GroupID.CRC(); // GroupID
            CRC += (uint)iitem.Permissions.OwnerMask; //owner_mask;      // Either owner_mask or next_owner_mask may need to be
            CRC += (uint)iitem.Permissions.NextOwnerMask; //next_owner_mask; // switched with base_mask -- 2 values go here and in my
            CRC += (uint)iitem.Permissions.EveryoneMask; //everyone_mask;   // study item, the three were identical.
            CRC += (uint)iitem.Permissions.GroupMask; //group_mask;

            // The rest of the CRC fields
            CRC += (uint)iitem.Flags; // Flags
            CRC += (uint)iitem.InventoryType; // InvType
            CRC += (uint)iitem.AssetType; // Type 
            CRC += (uint)Utils.DateTimeToUnixTime(iitem.CreationDate); // CreationDate
            CRC += (uint)iitem.SalePrice;    // SalePrice
            CRC += (uint)((uint)iitem.SaleType * 0x07073096); // SaleType

            return CRC;
        }

        /// <summary>
        /// Reverses a cheesy XORing with a fixed UUID to convert a shadow_id to an asset_id
        /// </summary>
        /// <param name="shadowID">Obfuscated shadow_id value</param>
        /// <returns>Deobfuscated asset_id value</returns>
        public static UUID DecryptShadowID(UUID shadowID)
        {
            return shadowID ^ MAGIC_ID;
        }

        /// <summary>
        /// Does a cheesy XORing with a fixed UUID to convert an asset_id to a shadow_id
        /// </summary>
        /// <param name="assetID">asset_id value to obfuscate</param>
        /// <returns>Obfuscated shadow_id value</returns>
        public static UUID EncryptAssetID(UUID assetID)
        {
            return assetID ^ MAGIC_ID;
        }

        private uint RegisterItemCreatedCallback(ItemCreatedCallback callback)
        {
            uint id;
            do
            {
                var v = Interlocked.Increment(ref _CallbackPos);
                id = (uint)v;
            } while (id == 0);

            if (!_ItemCreatedCallbacks.TryAdd(id, callback))
            {
                _ItemCreatedCallbacks[id] = callback;
                Logger.Warn("Overwriting an existing ItemCreatedCallback", Client);
            }

            // Schedule cleanup in case the server never responds. If the callback is
            // already removed by a successful response the TryRemove here will fail,
            // and we won't invoke the callback twice.
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(CALLBACK_TIMEOUT_MS, _callbackCleanupCts.Token).ConfigureAwait(false);
                    if (_ItemCreatedCallbacks.TryRemove(id, out var cb))
                    {
                        try
                        {
                            // Signal failure/timeout to the caller
                            cb(false, null);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message, ex, Client);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation expected during Dispose, swallow
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex.Message, ex, Client);
                }
            }, _callbackCleanupCts.Token);

            return id;
        }

        private uint RegisterItemsCopiedCallback(ItemCopiedCallback callback)
        {
            uint id;
            do
            {
                var v = Interlocked.Increment(ref _CallbackPos);
                id = (uint)v;
            } while (id == 0);

            if (!_ItemCopiedCallbacks.TryAdd(id, callback))
            {
                _ItemCopiedCallbacks[id] = callback;
                Logger.Warn("Overwriting an existing ItemsCopiedCallback", Client);
            }

            // Schedule cleanup for copied-item callbacks as well to avoid leaks
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(CALLBACK_TIMEOUT_MS, _callbackCleanupCts.Token).ConfigureAwait(false);
                    if (_ItemCopiedCallbacks.TryRemove(id, out var cb))
                    {
                        try
                        {
                            // Indicate failure by passing null
                            cb(null);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message, ex, Client);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation expected during Dispose, swallow
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex.Message, ex, Client);
                }
            }, _callbackCleanupCts.Token);

            return id;
        }

        private static bool ParseLine(string line, out string key, out string value)
        {
            // Clean up and convert tabs to spaces
            line = line.Trim();
            line = line.Replace('\t', ' ');

            // Shrink all whitespace down to single spaces
            while (line.IndexOf("  ", StringComparison.Ordinal) > 0)
                line = line.Replace("  ", " ");

            if (line.Length > 2)
            {
                var sep = line.IndexOf(' ');
                if (sep > 0)
                {
                    key = line.Substring(0, sep);
                    value = line.Substring(sep + 1);

                    return true;
                }
            }
            else if (line.Length == 1)
            {
                key = line;
                value = string.Empty;
                return true;
            }

            key = null;
            value = null;
            return false;
        }

        // Centralized capability lookup helper to reduce duplicated null checks
        private Uri GetCapabilityURI(string capName, bool logOnMissing = true)
        {
            var sim = Client?.Network?.CurrentSim;
            Uri uri = sim?.Caps?.CapabilityURI(capName);
            if (uri == null && logOnMissing)
            {
                var simName = sim?.Name ?? "unknown";
                Logger.Warn($"Failed to obtain {capName} capability on {simName}", Client);
            }
            return uri;
        }
    }
}

