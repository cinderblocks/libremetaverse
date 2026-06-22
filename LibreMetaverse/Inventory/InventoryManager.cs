/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2026, Sjofn LLC.
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
using LibreMetaverse.StructuredData;
using LibreMetaverse.Packets;
using System.Collections.Concurrent;
using LibreMetaverse.Threading;

namespace LibreMetaverse
{
    /// <summary>
    /// Tools for dealing with agents inventory
    /// </summary>
    [Serializable]
    public partial class InventoryManager : IDisposable
    {
        /// <summary>Used for converting shadow_id to asset_id</summary>
        public static readonly UUID MAGIC_ID = new UUID("3c115e51-04f4-523c-9fa6-98aff1034730");
        public static Task<List<InventoryBase>> NoResults => Task.FromResult(new List<InventoryBase>());
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
        private Inventory? _Store;
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
        public Inventory? Store => _Store;

        #endregion Properties

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">Reference to the GridClient object</param>
        public InventoryManager(GridClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));

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

            Client.AisClient.AISMetaReceived += OnAISMetaReceived;
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
                    try { if (Client?.AisClient != null) Client.AisClient.AISMetaReceived -= OnAISMetaReceived; } catch (Exception ex) { Logger.Debug("Failed to detach AISMetaReceived handler", ex, Client); }

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

        public async Task<InventoryItem?> FetchItemHttpAsync(UUID itemId, UUID ownerId, CancellationToken token = default)
        {
            InventoryItem? item = null;
            await RequestFetchInventoryHttpAsync(itemId, ownerId, token, list =>
            {
                item = list.FirstOrDefault();
            }).ConfigureAwait(false);
            return item;
        }

        /// <summary>
        /// Request A single inventory item
        /// </summary>
        /// <param name="itemID">The items <see cref="LibreMetaverse.UUID"/></param>
        /// <param name="ownerID">The item Owners <see cref="LibreMetaverse.UUID"/></param>
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
        /// <param name="itemID">The items <see cref="LibreMetaverse.UUID"/></param>
        /// <param name="ownerID">The item Owners <see cref="LibreMetaverse.UUID"/></param>
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <param name="callback">Action</param>
        private async Task RequestFetchInventoryHttpAsync(UUID itemID, UUID ownerID,
            CancellationToken cancellationToken, Action<List<InventoryItem>>? callback = null)
        {
            await RequestFetchInventoryHttpAsync(new Dictionary<UUID, UUID>(1) { { itemID, ownerID } },
                cancellationToken, callback).ConfigureAwait(false);
        }

        /// <summary>
        /// Request inventory items via HTTP capability
        /// </summary>
        /// <param name="items">Inventory items to request with owner</param>
        /// <param name="cancellationToken">Cancellation token to cancel the request</param>
        /// <param name="callback">Action</param>
        private async Task RequestFetchInventoryHttpAsync(Dictionary<UUID, UUID> items,
            CancellationToken cancellationToken, Action<List<InventoryItem>>? callback = null)
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
        /// Request the contents of an inventory folder using HTTP capabilities
        /// </summary>
        /// <param name="folderID">The folder to search</param>
        /// <param name="ownerID">The folder owners <see cref="UUID"/></param>
        /// <param name="fetchFolders">true to return <see cref="InventoryFolder"/>s contained in folder</param>
        /// <param name="fetchItems">true to return <see cref="InventoryItem"/>s contained in folder</param>
        /// <param name="order">the sort order to return items in</param>
        /// <param name="cancellationToken">CancellationToken for operation</param>
        /// <see cref="InventoryManager.FolderContents"/>
        public async Task<List<InventoryBase>> RequestFolderContentsAsync(UUID folderID, UUID ownerID,
            bool fetchFolders, bool fetchItems, InventorySortOrder order, CancellationToken cancellationToken = default)
        {
            var cap = (ownerID == Client.Self.AgentID) ? "FetchInventoryDescendents2" : "FetchLibDescendents2";
            Uri? url = GetCapabilityURI(cap);
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
            return await RequestFolderContentsAsync(new List<InventoryFolder>(1) { folder },
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
        public async Task<List<InventoryBase>> RequestFolderContentsAsync(List<InventoryFolder> batch, Uri capabilityUri,
            bool fetchFolders, bool fetchItems, InventorySortOrder order, CancellationToken cancellationToken = default)
        {
            List <InventoryBase> ret = new List<InventoryBase>();
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

                        var store = _Store;
                        if (store != null && store.TryGetValue(res["folder_id"], out var invFolder) && invFolder is InventoryFolder folderCast)
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
                            fetchedNode!.NeedsUpdate = false;
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

        #region AIS meta application

        /// <summary>
        /// Applies AIS3 response side-effect metadata to the local inventory store.
        /// Removes broken links and collateral deletions; updates folder version numbers.
        /// Subscribed to <see cref="LibreMetaverse.InventoryAISClient.AISMetaReceived"/>.
        /// </summary>
        private void OnAISMetaReceived(LibreMetaverse.AISResponseMeta meta)
        {
            var store = _Store;
            if (store == null || !meta.HasAnyData) return;

            using var writeLock = _storeLock.WriteLock();

            foreach (var id in meta.BrokenLinksRemoved)
            {
                if (store.TryGetValue(id, out var obj) && obj != null)
                    store.RemoveNodeFor(obj);
            }
            foreach (var id in meta.ItemsRemoved)
            {
                if (store.TryGetValue(id, out var obj) && obj != null)
                    store.RemoveNodeFor(obj);
            }
            foreach (var id in meta.CategoriesRemoved)
            {
                if (store.TryGetValue(id, out var obj) && obj != null)
                    store.RemoveNodeFor(obj);
            }
            foreach (var kv in meta.CategoryVersionUpdates)
            {
                if (store.TryGetValue<InventoryFolder>(kv.Key, out var folder))
                    folder!.Version = kv.Value;
            }
        }

        #endregion AIS meta application

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

        /// <summary>
        /// Find the UUID of the default folder for a given folder type
        /// </summary>
        /// <remarks>Returns the root folder UUID if no matching folder is found</remarks>
        /// <param name="type">The <see cref="FolderType"/> to search for</param>
        /// <returns>The UUID of the matching folder, or the root folder UUID if not found</returns>
        public UUID FindFolderForType(FolderType type)
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

            var contents = _Store.GetContents(_Store.RootFolder.UUID);
            foreach (var folder in contents.OfType<InventoryFolder>().Where(f => f.PreferredType == type))
            {
                return folder.UUID;
            }

            // No match found, return Root Folder ID
            return _Store.RootFolder.UUID;
        }

        /// <summary>
        /// Find inventory items by path
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="inventoryOwner">The object owners <see cref="UUID"/></param>
        /// <param name="path">A string path to search, folders/objects separated by a '/'</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <remarks>Results are sent to the <see cref="InventoryManager.OnFindObjectByPath"/> event</remarks>
        public async Task RequestFindObjectByPathAsync(UUID baseFolder, UUID inventoryOwner, string path, CancellationToken cancellationToken = default)
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
            await RequestFolderContentsAsync(baseFolder, inventoryOwner, true, true, InventorySortOrder.ByName, cancellationToken);
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

            if (_Store == null) return objects;

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
        /// Update folder properties
        /// </summary>
        /// <param name="folderID"><see cref="UUID"/> of the folder to update</param>
        /// <param name="parentID">Sets folder's parent to <see cref="UUID"/></param>
        /// <param name="name">Folder name</param>
        /// <param name="type">Folder type</param>
        public void UpdateFolderProperties(UUID folderID, UUID parentID, string name, FolderType type)
        {
            InventoryFolder? inv = null;

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
                    if (_Store != null)
                        _Store.UpdateNodeFor(inv);
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                if (inv != null)
                {
                    _ = Client.AisClient.UpdateCategoryAsync(folderID, inv.GetOSD());
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
        /// <param name="cancellationToken">Cancellation token for the operation</param>
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
                _ = Client.AisClient.MoveCategoryAsync(folderID, newParentID, cancellationToken);
                return;
            }

            var move = new MoveInventoryFolderPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false
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
        /// <param name="cancellationToken">Cancellation token for the operation</param>
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
                var tasks = foldersNewParents.Select(kv => Client.AisClient.MoveCategoryAsync(kv.Key, kv.Value, cancellationToken)).ToArray();
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
        /// <param name="cancellationToken">Cancellation token for the operation</param>
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
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        public void MoveItem(UUID itemID, UUID folderID, string newName, CancellationToken cancellationToken = default)
        {
            // Update local store under write lock
            try
            {
                using (_storeLock.WriteLock())
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

            // AIS3 supports move-only; fall through to UDP when a rename is also requested
            // (renaming on move is deprecated per the [Obsolete] on Move(item, parent, name))
            if (string.IsNullOrEmpty(newName) && Client.AisClient.IsAvailable)
            {
                _ = Client.AisClient.MoveItemAsync(itemID, folderID, cancellationToken);
                return;
            }

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
        /// <param name="cancellationToken">Cancellation token for the operation</param>
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
                var tasks = itemsNewFolders.Select(kv => Client.AisClient.MoveItemAsync(kv.Key, kv.Value, cancellationToken)).ToArray();
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
                toRemove.Add(rootNode.Data!);
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
                var itemList = items.ToList();
                var rem = new RemoveInventoryObjectsPacket
                {
                    AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID }
                };
                rem.ItemData = itemList.Select(id => new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = id }).ToArray();
                rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[0];
                Client.Network.SendPacket(rem);
                foreach (var id in itemList)
                    RemoveLocalUi(true, id);
            }
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
                var rem = new RemoveInventoryObjectsPacket
                {
                    AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID }
                };
                rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[] { new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = item } };
                rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[0];
                Client.Network.SendPacket(rem);
                RemoveLocalUi(true, item);
            }
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
                var rem = new RemoveInventoryObjectsPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    }
                };
                rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[1];
                rem.ItemData[0] = new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = UUID.Zero };
                using (var writeLock = _storeLock.WriteLock())
                {
                    rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[1];
                    rem.FolderData[0] = new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = folder };
                    if (_Store != null && _Store.TryGetValue(folder, out var storeItem))
                        _Store.RemoveNodeFor(storeItem!);
                }
                Client.Network.SendPacket(rem);
            }
        }

        /// <summary>
        /// Empty the Lost and Found folder
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
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
                if (folderKey == UUID.Zero)
                {
                    Logger.Warn("LostAndFound folder not found in local store; cannot empty via AIS", Client);
                    return;
                }

                try
                {
                    var success = await Client.AisClient.PurgeDescendentsAsync(folderKey, cancellationToken).ConfigureAwait(false);
                    if (success)
                    {
                        RemoveLocalUi(true, folderKey);
                    }
                    else
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
                    var success = await Client.AisClient.EmptyTrashAsync(cancellationToken).ConfigureAwait(false);
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

            var items = _Store.RootFolder != null ? _Store.GetContents(_Store.RootFolder) : new List<InventoryBase>();
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
                    _ = Client.AisClient.PurgeDescendentsAsync(folderKey, cancellationToken)
                        .ContinueWith(t => RemoveLocalUi(t.Status == TaskStatus.RanToCompletion && t.Result, folderKey), TaskScheduler.Default);
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

                var rem = new RemoveInventoryObjectsPacket
                {
                    AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID }
                };
                rem.ItemData = remItems.Select(id => new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = id }).ToArray();
                rem.FolderData = remFolders.Select(id => new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = id }).ToArray();
                Client.Network.SendPacket(rem);
                foreach (var id in remItems) RemoveLocalUi(true, id);
                foreach (var id in remFolders) RemoveLocalUi(true, id);
            }
        }
        #endregion Remove

        #region Create

        /// <summary>
        /// Creates a new inventory item and returns it when confirmed by the server.
        /// Uses the UDP protocol path; for capabilities-based uploads use <see cref="CreateItemFromAssetAsync"/>.
        /// </summary>
        public Task<InventoryItem?> CreateItemAsync(UUID parentFolder, string name, string description, AssetType type,
            UUID assetTransactionID, InventoryType invType, PermissionMask nextOwnerMask,
            CancellationToken cancellationToken = default)
        {
            return CreateItemAsync(parentFolder, name, description, type, assetTransactionID, invType,
                (WearableType)0, nextOwnerMask, cancellationToken);
        }

        /// <summary>
        /// Creates a new inventory item and returns it when confirmed by the server.
        /// Uses the UDP protocol path; for capabilities-based uploads use <see cref="CreateItemFromAssetAsync"/>.
        /// </summary>
        public Task<InventoryItem?> CreateItemAsync(UUID parentFolder, string name, string description, AssetType type,
            UUID assetTransactionID, InventoryType invType, WearableType wearableType, PermissionMask nextOwnerMask,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<InventoryItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
            ItemCreatedCallback callback = (success, item) =>
            {
                if (success) tcs.TrySetResult(item);
                else tcs.TrySetResult(null);
            };
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
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
                        NextOwnerMask = (uint)nextOwnerMask,
                        Type = (sbyte)type,
                        InvType = (sbyte)invType,
                        WearableType = (byte)wearableType,
                        Name = Utils.StringToBytes(name),
                        Description = Utils.StringToBytes(description)
                    }
                };
                Client.Network.SendPacket(create);
                return tcs.Task;
            }
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

        public async Task<InventoryItem?> CreateLinkAsync(UUID folderID, UUID itemID, string name, string description,
            InventoryType invType, UUID transactionID, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<InventoryItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            ItemCreatedCallback innerCallback = (success, item) =>
            {
                if (success) tcs.TrySetResult(item);
                else tcs.TrySetResult(null);
            };

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
                var wrapped = WrapItemCreatedCallback(innerCallback);
                try
                {
                    var (aisSuccess, aisCreated) = await Client.AisClient.CreateInventoryAsync(folderID, newInventory, true, cancellationToken).ConfigureAwait(false);
                    wrapped?.Invoke(aisSuccess, aisCreated);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn(ex.Message, Client);
                    tcs.TrySetResult(null);
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
                    InventoryBlock = { CallbackID = RegisterItemCreatedCallback(WrapItemCreatedCallback(innerCallback)) }
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

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Creates multiple inventory links in a single AIS3 request when available,
        /// falling back to sequential UDP link creation for non-AIS3 connections.
        /// </summary>
        /// <param name="folderID">Destination folder UUID</param>
        /// <param name="linksToCreate">Items paired with their link description strings</param>
        /// <param name="callback">Optional callback invoked with overall success/failure</param>
        /// <param name="cancellationToken"></param>
        public async Task CreateLinksAsync(
            UUID folderID,
            IEnumerable<(InventoryBase item, string description)> linksToCreate,
            Action<bool>? callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linkList = linksToCreate.ToList();
            if (linkList.Count == 0) { callback?.Invoke(true); return; }

            if (Client.AisClient.IsAvailable)
            {
                var links = new OSDArray();
                foreach (var (item, description) in linkList)
                {
                    var isFolder = item is InventoryFolder;
                    links.Add(new OSDMap
                    {
                        ["linked_id"] = OSD.FromUUID(item.UUID),
                        ["type"] = OSD.FromInteger((sbyte)(isFolder ? AssetType.LinkFolder : AssetType.Link)),
                        ["inv_type"] = OSD.FromInteger((sbyte)(isFolder ? InventoryType.Folder : ((InventoryItem)item).InventoryType)),
                        ["name"] = OSD.FromString(item.Name),
                        ["desc"] = OSD.FromString(description)
                    });
                }

                try
                {
                    var createdLinks = await Client.AisClient.CreateInventoryLinksAsync(
                        folderID, new OSDMap { { "links", links } }, cancellationToken).ConfigureAwait(false);

                    if (_Store != null && createdLinks.Count > 0)
                    {
                        using (var writeLock = _storeLock.WriteLock())
                        {
                            foreach (var link in createdLinks)
                                _Store[link.UUID] = link;
                        }
                        foreach (var link in createdLinks)
                        {
                            try { OnItemReceived(new ItemReceivedEventArgs(link)); }
                            catch (Exception ex) { Logger.Debug($"OnItemReceived handler threw: {ex.Message}", ex, Client); }
                        }
                    }
                    callback?.Invoke(true);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn(ex.Message, Client);
                    callback?.Invoke(false);
                }
            }
            else
            {
                var allSucceeded = true;
                foreach (var (item, description) in linkList)
                {
                    switch (item)
                    {
                        case InventoryItem invItem:
                            if (await CreateLinkAsync(folderID, invItem.UUID, invItem.Name, description,
                                invItem.InventoryType, UUID.Random(), cancellationToken).ConfigureAwait(false) == null)
                                allSucceeded = false;
                            break;
                        case InventoryFolder folder:
                            if (await CreateLinkAsync(folderID, folder.UUID, folder.Name, "",
                                InventoryType.Folder, UUID.Random(), cancellationToken).ConfigureAwait(false) == null)
                                allSucceeded = false;
                            break;
                    }
                }
                callback?.Invoke(allSucceeded);
            }
        }

        #endregion Create

        #region Copy

        /// <summary>
        /// Copies a single inventory item to a new folder and returns the copy.
        /// </summary>
        public Task<InventoryBase?> CopyItemAsync(UUID item, UUID newParent, string newName,
            CancellationToken cancellationToken = default)
        {
            return CopyItemAsync(item, newParent, newName, Client.Self.AgentID, cancellationToken);
        }

        /// <summary>
        /// Copies a single inventory item to a new folder and returns the copy.
        /// </summary>
        public async Task<InventoryBase?> CopyItemAsync(UUID item, UUID newParent, string newName, UUID oldOwnerID,
            CancellationToken cancellationToken = default)
        {
            var result = await RequestCopyItemsWithResultAsync(
                new List<UUID>(1) { item },
                new List<UUID>(1) { newParent },
                new List<string>(1) { newName },
                oldOwnerID, cancellationToken).ConfigureAwait(false);
            return result.CopiedItems?.Count > 0 ? result.CopiedItems[0] : null;
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
                    // Fire-and-forget AIS update � wrap callback to merge into local store
                    _ = Client.AisClient.UpdateItemAsync(item.UUID, update)
                        .ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion && t.Result) MergeUpdateIntoStore(update, item.UUID); }, TaskScheduler.Default);
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
            InventoryItem? ret = null;

            if (_Store == null)
            {
                Logger.Warn("Inventory store not initialized, cannot create or retrieve inventory item", Client);
                // Create a new item instance to return so callers do not get null
                return CreateInventoryItem(InvType, ItemID);
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

        private static bool ParseLine(string line, out string? key, out string? value)
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
        private Uri? GetCapabilityURI(string capName, bool logOnMissing = true)
        {
            var sim = Client?.Network?.CurrentSim;
            Uri? uri = sim?.Caps?.CapabilityURI(capName);
            if (uri == null && logOnMissing)
            {
                var simName = sim?.Name ?? "unknown";
                Logger.Warn($"Failed to obtain {capName} capability on {simName}", Client);
            }
            return uri;
        }

        /// <summary>
        /// Returns the correct <c>expected_upload_cost</c> in L$ for the given asset type,
        /// using account-level benefit costs where available and falling back to
        /// <see cref="Settings.UploadCost"/> (the texture cost) for types not separately priced.
        /// </summary>
        private int GetUploadCostForAssetType(AssetType assetType)
        {
            var b = Client.Self.Benefits;
            return assetType switch
            {
                AssetType.Animation => b.AnimationUploadCost > 0 ? b.AnimationUploadCost : Client.Settings.UploadCost,
                AssetType.Sound     => b.SoundUploadCost     > 0 ? b.SoundUploadCost     : Client.Settings.UploadCost,
                AssetType.Object    => b.MeshUploadCost      > 0 ? b.MeshUploadCost      : Client.Settings.UploadCost,
                _                   => Client.Settings.UploadCost
            };
        }
    }
}

