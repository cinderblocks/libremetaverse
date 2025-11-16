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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;
using System.Collections.Concurrent;

namespace OpenMetaverse
{
    /// <summary>
    /// Tools for dealing with agents inventory
    /// </summary>
    [Serializable]
    public partial class InventoryManager
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

        #region String Arrays

        /// <summary>Partial mapping of FolderTypes to folder names</summary>
        private static readonly string[] _NewFolderNames = new string[]
        {
            "Textures",         //  0
            "Sounds",           //  1
            "Calling Cards",    //  2
            "Landmarks",        //  3
            string.Empty,       //  4
            "Clothing",         //  5
            "Objects",          //  6
            "Notecards",        //  7
            "My Inventory",     //  8
            string.Empty,       //  9
            "Scripts",          // 10
            string.Empty,       // 11
            string.Empty,       // 12
            "Body Parts",       // 13
            "Trash",            // 14
            "Photo Album",      // 15
            "Lost And Found",   // 16
            string.Empty,       // 17
            string.Empty,       // 18
            string.Empty,       // 19
            "Animations",       // 20
            "Gestures",         // 21
            string.Empty,       // 22
            "Favorites",        // 23
            string.Empty,       // 24
            string.Empty,       // 25
            "New Folder",       // 26
            "New Folder",       // 27
            "New Folder",       // 28
            "New Folder",       // 29
            "New Folder",       // 30
            "New Folder",       // 31
            "New Folder",       // 32
            "New Folder",       // 33
            "New Folder",       // 34
            "New Folder",       // 35
            "New Folder",       // 36
            "New Folder",       // 37
            "New Folder",       // 38
            "New Folder",       // 39
            "New Folder",       // 40
            "New Folder",       // 41
            "New Folder",       // 42
            "New Folder",       // 43
            "New Folder",       // 44
            "New Folder",       // 45
            "Current Outfit",   // 46
            "New Outfit",       // 47
            "My Outfits",       // 48
            "Meshes",           // 49
            "Received Items",   // 50
            "Merchant Outbox",  // 51
            "Basic Root",       // 52
            "Marketplace Listings",   // 53
            "New Stock",      // 54
            "Marketplace Version", // 55
            "Settings",         // 56
            "Material",         // 57
            "Animation Overrides", //58
            "New Folder",       // 59
            "RLV",              // 60
        };

        #endregion String Arrays

        [NonSerialized]
        private readonly GridClient Client;
        [NonSerialized]
        private Inventory _Store;
        

        private readonly ReaderWriterLockSlim _storeLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

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
        /// <param name="itemID">The items <see cref="OpenMetaverse.UUID"/></param>f
        /// <param name="ownerID">The item Owners <see cref="OpenMetaverse.UUID"/></param>
        /// <see cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(UUID itemID, UUID ownerID)
        {
            RequestFetchInventory(new Dictionary<UUID, UUID>(1) { { itemID, ownerID } });
        }

        /// <summary>
        /// Request inventory items
        /// </summary>
        /// <param name="items">Inventory items to request with owner</param>
        /// <see cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(Dictionary<UUID, UUID> items)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                RequestFetchInventoryHttp(items);
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
        /// <see cref="OnItemReceived"/>
        private void RequestFetchInventoryHttp(Dictionary<UUID, UUID> items)
        {
            RequestFetchInventoryHttpAsync(items, CancellationToken.None).ConfigureAwait(false);
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
        /// <param name="cancellationToken">Cancellation token for operation</param>
        /// <param name="callback">Action</param>
        public async Task RequestFetchInventoryHttpAsync(Dictionary<UUID, UUID> items,
            CancellationToken cancellationToken, Action<List<InventoryItem> > callback = null)
        {

            var cap = GetCapabilityURI("FetchInventory2");
            if (cap == null)
            {
                Logger.Log($"Failed to obtain FetchInventory2 capability on {Client.Network.CurrentSim?.Name}",
                    Helpers.LogLevel.Warning, Client);
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
                        _Store[item.UUID] = item;
                        retrievedItems.Add(item);
                        OnItemReceived(new ItemReceivedEventArgs(item));
                    }

                    callback?.Invoke(retrievedItems);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed getting data from FetchInventory2 capability.", Helpers.LogLevel.Error, Client, ex);
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
                Logger.Log(msg, Helpers.LogLevel.Warning, Client);
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
                    inventory = _Store.GetContents(folder);
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

                await Client.HttpCapsClient.PostRequestAsync(capabilityUri, OSDFormat.Xml, payload, 
                    cancellationToken, (response, data, error) => 
                {
                    try
                    {
                        if (error != null)
                        {
                            throw error;
                        }

                        var result = OSDParser.Deserialize(data);
                        var resultMap = ((OSDMap)result);
                        if (resultMap.TryGetValue("folders", out var foldersSd))
                        {
                            var fetchedFolders = (OSDArray)foldersSd;
                            ret = new List<InventoryBase>(fetchedFolders.Count);
                            foreach (var fetchedFolderNr in fetchedFolders)
                            {
                                var res = (OSDMap)fetchedFolderNr;
                                InventoryFolder fetchedFolder;

                                if (_Store.Contains(res["folder_id"])
                                    && _Store[res["folder_id"]] is InventoryFolder invFolder)
                                {
                                    fetchedFolder = invFolder;
                                }
                                else
                                {
                                    fetchedFolder = new InventoryFolder(res["folder_id"]);
                                    _Store[res["folder_id"]] = fetchedFolder;
                                }
                                fetchedFolder.DescendentCount = res["descendents"];
                                fetchedFolder.Version = res["version"];
                                fetchedFolder.OwnerID = res["owner_id"];
                                _Store.GetNodeFor(fetchedFolder.UUID).NeedsUpdate = false;

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
                                            if (!_Store.Contains(folderID))
                                            {
                                                folder = new InventoryFolder(folderID)
                                                {
                                                    ParentUUID = descFolder["parent_id"],
                                                };
                                                _Store[folderID] = folder;
                                            }
                                            else
                                            {
                                                folder = (InventoryFolder)_Store[folderID];
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
                                                _Store[item.UUID] = item;
                                                ret.Add(item);
                                            }
                                        }
                                    }
                                }
                                OnFolderUpdated(new FolderUpdatedEventArgs(res["folder_id"], true));
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Logger.Log($"Failed to fetch inventory descendants: {exc.Message}" + Environment.NewLine +
                                   $"{exc.StackTrace}",
                                   Helpers.LogLevel.Warning, Client);
                        foreach (var f in batch)
                        {
                            OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to fetch inventory descendants: {ex.Message}" + Environment.NewLine +
                           $"{ex.StackTrace}",
                           Helpers.LogLevel.Warning, Client);
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
                Logger.Log("Inventory is null, FindFolderForType() lookup cannot continue",
                    Helpers.LogLevel.Error, Client);
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
                Logger.Log("Inventory is null, FindFolderForType() lookup cannot continue",
                    Helpers.LogLevel.Error, Client);
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
        public async Task RequestFindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path)
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
            await RequestFolderContents(baseFolder, inventoryOwner, true, true, InventorySortOrder.ByName);
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
            var objects = new List<InventoryBase>();
            //List<InventoryFolder> folders = new List<InventoryFolder>();
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
                    objects.AddRange(LocalFind(inv.UUID, path, level + 1, firstOnly));
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
        /// <param name="newparentID">The destination folders <see cref="UUID"/></param>
        /// <param name="newName">The name to change the folder to</param>
        [Obsolete("Method broken with AISv3. Use MoveFolder(folder, parent) and UpdateFolderProperties(folder, parent, name, type) instead")]
        public void MoveFolder(UUID folderID, UUID newparentID, string newName)
        {
            UpdateFolderProperties(folderID, newparentID, newName, FolderType.None);
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

            _storeLock.EnterUpgradeableReadLock();
            try
            {
                if (_Store != null && _Store.Contains(folderID))
                {
                    // Retrieve node under read lock
                    inv = (InventoryFolder)Store[folderID];

                    // Update the folder metadata under write lock
                    _storeLock.EnterWriteLock();
                    try
                    {
                        inv.Name = name;
                        inv.ParentUUID = parentID;
                        inv.PreferredType = type;
                        _Store.UpdateNodeFor(inv);
                    }
                    finally
                    {
                        _storeLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _storeLock.ExitUpgradeableReadLock();
            }

            if (Client.AisClient.IsAvailable)
            {
                if (inv != null)
                {
                    Client.AisClient.UpdateCategory(folderID, inv.GetOSD(), success =>
                    {
                        if (success)
                        {
                            // Ensure local store is updated (already updated above) but keep parity
                            _storeLock.EnterWriteLock();
                            try
                            {
                                _Store.UpdateNodeFor(inv);
                            }
                            finally
                            {
                                _storeLock.ExitWriteLock();
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
        public void MoveFolder(UUID folderID, UUID newParentID)
        {
            _storeLock.EnterWriteLock();
            try
            {
                if (_Store != null && _Store.Contains(folderID))
                {
                    var inv = Store[folderID];
                    inv.ParentUUID = newParentID;
                    _Store.UpdateNodeFor(inv);
                }
            }
            finally
            {
                _storeLock.ExitWriteLock();
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
        public void MoveFolders(Dictionary<UUID, UUID> foldersNewParents)
        {
            // Update local store under a write lock
            _storeLock.EnterWriteLock();
            try
            {
                foreach (var entry in foldersNewParents)
                {
                    if (_Store != null && _Store.Contains(entry.Key))
                    {
                        var inv = _Store[entry.Key];
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }
            finally
            {
                _storeLock.ExitWriteLock();
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
        /// Move an inventory item to a new folder
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <see cref="UUID"/> of the destination folder</param>
        public void MoveItem(UUID itemID, UUID folderID)
        {
            MoveItem(itemID, folderID, string.Empty);
        }

        /// <summary>
        /// Move and rename an inventory item
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <see cref="UUID"/> of the destination folder</param>
        /// <param name="newName">The name to change the folder to</param>
        public void MoveItem(UUID itemID, UUID folderID, string newName)
        {
            _storeLock.EnterWriteLock();
            try
            {
                if (_Store.Contains(itemID))
                {
                    var inv = _Store[itemID];
                    if (!string.IsNullOrEmpty(newName))
                    {
                        inv.Name = newName;
                    }
                    inv.ParentUUID = folderID;
                    _Store.UpdateNodeFor(inv);
                }
            }
            finally
            {
                _storeLock.ExitWriteLock();
            }

            var move = new MoveInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false //FIXME: ??
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
        /// Move multiple inventory items to new locations
        /// </summary>
        /// <param name="itemsNewParents">A Dictionary containing the 
        /// <see cref="UUID"/> of the source item as the key, and the 
        /// <see cref="UUID"/> of the destination folder as the value</param>
        public void MoveItems(Dictionary<UUID, UUID> itemsNewParents)
        {
            _storeLock.EnterWriteLock();
            try
            {
                foreach (var entry in itemsNewParents)
                {
                    if (_Store != null && _Store.Contains(entry.Key))
                    {
                        var inv = _Store[entry.Key];
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }
            finally
            {
                _storeLock.ExitWriteLock();
            }

            var move = new MoveInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    Stamp = false //FIXME: ??
                },
                InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[itemsNewParents.Count]
            };

            var index = 0;
            foreach (var entry in itemsNewParents)
            {
                var block = new MoveInventoryItemPacket.InventoryDataBlock
                {
                    ItemID = entry.Key,
                    FolderID = entry.Value,
                    NewName = Utils.EmptyBytes
                };
                move.InventoryData[index++] = block;
            }

            Client.Network.SendPacket(move);
        }

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

            _storeLock.EnterReadLock();
            try
            {
                if (!_Store.TryGetNodeFor(itemId, out var rootNode))
                {
                    return;
                }

                // Traverse descendants iteratively to avoid recursive write-lock reentrancy
                var stack = new Stack<UUID>();
                stack.Push(itemId);

                while (stack.Count > 0)
                {
                    var id = stack.Pop();

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

                // Finally remove the root node itself
                toRemove.Add(rootNode.Data);
            }
            finally
            {
                _storeLock.ExitReadLock();
            }

            // Perform removals under write lock
            _storeLock.EnterWriteLock();
            try
            {
                foreach (var b in toRemove)
                {
                    try { _Store.RemoveNodeFor(b); } catch { /* swallow individual remove failures */ }
                }
            }
            finally
            {
                _storeLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Remove descendants of a folder
        /// </summary>
        /// <param name="folder">The <see cref="UUID"/> of the folder</param>
        public void RemoveDescendants(UUID folder)
        {
            if (Client.AisClient.IsAvailable)
            {
                Client.AisClient.PurgeDescendents(folder, RemoveLocalUi).ConfigureAwait(false);
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

        /// <summary>
        /// Remove a single item from inventory
        /// </summary>
        /// <param name="item">The <see cref="UUID"/> of the inventory item to remove</param>
        public void RemoveItem(UUID item)
        {
            if (Client.AisClient.IsAvailable)
            {
                Client.AisClient.RemoveItem(item, RemoveLocalUi).ConfigureAwait(false);
            }
            else
            {
                var items = new List<UUID>(1) { item };
#pragma warning disable CS0612 // Type or member is obsolete
                Remove(items, null);
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        public async Task RemoveItemsAsync(IEnumerable<UUID> items, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Client.AisClient.IsAvailable)
            {
                var tasks = items
                    .Select(n => Client.AisClient.RemoveItem(n, RemoveLocalUi, cancellationToken))
                    .ToList();

                await Task.WhenAll(tasks);
            }
            else
            {
#pragma warning disable CS0612 // Type or member is obsolete
                Remove(items.ToList(), new List<UUID>());
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        /// <summary>
        /// Remove a folder from inventory
        /// </summary>
        /// <param name="folder">The <see cref="UUID"/> of the folder to remove</param>
        public void RemoveFolder(UUID folder)
        {
            if (Client.AisClient.IsAvailable)
            {
                Client.AisClient.RemoveCategory(folder, RemoveLocalUi).ConfigureAwait(false);
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
                _storeLock.EnterWriteLock();
                try
                {
                    rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[items.Count];
                    for (var i = 0; i < items.Count; i++)
                    {
                        rem.ItemData[i] = new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = items[i] };

                        // Update local copy
                        if (_Store != null && _Store.Contains(items[i]))
                            _Store.RemoveNodeFor(Store[items[i]]);
                    }
                }
                finally
                {
                    _storeLock.ExitWriteLock();
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
                _storeLock.EnterWriteLock();
                try
                {
                    rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[folders.Count];
                    for (var i = 0; i < folders.Count; i++)
                    {
                        rem.FolderData[i] = new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = folders[i] };

                        // Update local copy
                        if (_Store != null && _Store.Contains(folders[i]))
                            _Store.RemoveNodeFor(Store[folders[i]]);
                    }
                }
                finally
                {
                    _storeLock.ExitWriteLock();
                }
            }
            Client.Network.SendPacket(rem);
        }

        /// <summary>
        /// Empty the Lost and Found folder
        /// </summary>
        public void EmptyLostAndFound()
        {
            EmptySystemFolder(FolderType.LostAndFound);
        }

        /// <summary>
        /// Empty the Trash folder
        /// </summary>
        public void EmptyTrash()
        {
            EmptySystemFolder(FolderType.Trash);
        }

        private void EmptySystemFolder(FolderType folderType)
        {
            if (_Store == null)
            {
                Logger.Log("Inventory store not initialized, cannot empty system folder",
                    Helpers.LogLevel.Warning, Client);
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
                    Client.AisClient.PurgeDescendents(folderKey, RemoveLocalUi).ConfigureAwait(false);
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
            var id = UUID.Random();

            // Assign a folder name if one is not already set
            if (string.IsNullOrEmpty(name))
            {
                if (preferredType >= FolderType.Texture && preferredType <= FolderType.MarkplaceStock)
                {
                    name = _NewFolderNames[(int)preferredType];
                }
                else
                {
                    name = "New Folder";
                }
                if (name?.Length == 0)
                {
                    name = "New Folder";
                }
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
                    _Store[newFolder.UUID] = newFolder;
                }
                else
                {
                    Logger.Log("Inventory store is not initialized, created folder will not be cached locally",
                        Helpers.LogLevel.Debug, Client);
                }
            }
            catch (InventoryException ie) { Logger.Log(ie.Message, Helpers.LogLevel.Warning, Client, ie); }

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
                Logger.Log($"RequestCreateItemFromAsset failed: {ex.Message}", Helpers.LogLevel.Warning, Client, ex);
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

            // Make the request
            var req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, query, cancellationToken,
                (response, responseData, error) =>
                {
                    if (responseData == null) { throw error; }

                    CreateItemFromAssetResponse(callback, data, query,
                        OSDParser.Deserialize(responseData), error, cancellationToken);
                });
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
            if (bse is InventoryFolder folder)
            {
                CreateLink(folderID, folder, callback, cancellationToken);
            }
            else if (bse is InventoryItem item)
            {
                CreateLink(folderID, item.UUID, item.Name, item.Description, item.InventoryType, UUID.Random(), callback, cancellationToken);
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
                Client.AisClient.CreateInventory(folderID, newInventory, true, callback, cancellationToken)
                    .ConfigureAwait(false);
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
                Logger.Log($"RequestCopyItems failed: {ex.Message}", Helpers.LogLevel.Warning, Client, ex);
            }
        }

        /// <summary>
        /// Request a copy of an asset embedded within a notecard
        /// </summary>
        /// <param name="objectID">Usually UUID.Zero for copying an asset from a notecard</param>
        /// <param name="notecardID">UUID of the notecard to request an asset from</param>
        /// <param name="folderID">Target folder for asset to go to in your inventory</param>
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

                var req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, message.Serialize(), cancellationToken, null);
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
                    if (update.ContainsKey("asset_id"))
                    {
                        update.Remove("asset_id");
                        if (item.TransactionID != UUID.Zero)
                        {
                            update["hash_id"] = item.TransactionID;
                        }
                    }
                    if (update.ContainsKey("shadow_id"))
                    {
                        update.Remove("shadow_id");
                        if (item.TransactionID != UUID.Zero)
                        {
                            update["hash_id"] = item.TransactionID;
                        }
                    }
                    Client.AisClient.UpdateItem(item.UUID, update, null).ConfigureAwait(false);
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

            // Make the request
            var req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, query, cancellationToken,
                (response, responseData, error) =>
                {
                    if (responseData == null) { throw error; }

                    UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data),
                        notecardID, OSDParser.Deserialize(responseData), error, cancellationToken);
                });
        }

        /// <summary>
        /// Save changes to notecard embedded in object contents
        /// </summary>
        /// <param name="data">Encoded notecard asset data</param>
        /// <param name="notecardID">Notecard UUID</param>
        /// <param name="taskID">Object's UUID</param>
        /// <param name="callback">Called upon finish of the upload with status information</param>
        /// <param name="cancellationToken"></param>
        public void RequestUpdateNotecardTask(byte[] data, UUID notecardID, UUID taskID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateNotecardTaskInventory", false);
            if (cap == null)
            {
                throw new Exception("UpdateNotecardTaskInventory capability is not currently available");
            }

            var query = new OSDMap
            {
                {"item_id", OSD.FromUUID(notecardID)},
                { "task_id", OSD.FromUUID(taskID)}
            };

            // Make the request
            var req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, query, cancellationToken,
                (response, responseData, error) =>
                {
                    if (responseData == null) { throw error; }

                    UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data),
                        notecardID, OSDParser.Deserialize(responseData), error, cancellationToken);
                });
        }

        /// <summary>
        /// Upload new gesture asset for an inventory gesture item
        /// </summary>
        /// <param name="data">Encoded gesture asset</param>
        /// <param name="gestureID">Gesture inventory UUID</param>
        /// <param name="callback">Method to call upon completion of the upload</param>
        /// <param name="cancellationToken"></param>
        public void RequestUploadGestureAsset(byte[] data, UUID gestureID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateGestureAgentInventory", false);
            if (cap == null)
            {
                throw new Exception("UpdateGestureAgentInventory capability is not currently available");
            }

            var query = new OSDMap { { "item_id", OSD.FromUUID(gestureID) } };

            // Make the request
            var req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, query, cancellationToken,
                (response, responseData, error) =>
                {
                    if (responseData == null) { throw error; }

                    UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data),
                        gestureID, OSDParser.Deserialize(responseData), error, cancellationToken);
                });
        }

        /// <summary>
        /// Update an existing script in an agents Inventory
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public void RequestUpdateScriptAgentInventory(byte[] data, UUID itemID, bool mono, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateScriptAgent");
            if (cap != null)
            {
                var request = new UpdateScriptAgentRequestMessage
                {
                    ItemID = itemID,
                    Target = mono ? "mono" : "lsl2"
                };

                _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, request.Serialize(), cancellationToken,
                    (response, responseData, error) =>
                    {
                        UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data),
                            itemID, OSDParser.Deserialize(responseData), error, cancellationToken);
                    });
            }
            else
            {
                throw new Exception("UpdateScriptAgent capability is not currently available");
            }
        }

        /// <summary>
        /// Update an existing script in a task Inventory
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="taskID">UUID of the prim containing the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="running">if true, sets the script to running</param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        public void RequestUpdateScriptTask(byte[] data, UUID itemID, UUID taskID, bool mono, bool running, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateScriptTask");
            if (cap != null)
            {
                var msg = new UpdateScriptTaskUpdateMessage
                {
                    ItemID = itemID,
                    TaskID = taskID,
                    ScriptRunning = running,
                    Target = mono ? "mono" : "lsl2"
                };

                _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, msg.Serialize(), cancellationToken,
                    (response, responseData, error) =>
                    {
                        UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data),
                            itemID, OSDParser.Deserialize(responseData), error, cancellationToken);
                    });
            }
            else
            {
                throw new Exception("UpdateScriptTask capability is not currently available");
            }
        }
        #endregion Update

        #region Rez/Give

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
            InventoryItem item)
        {
            return RequestRezFromInventory(simulator, rotation, position, item, Client.Self.ActiveGroup,
                UUID.Random(), true);
        }

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        /// <param name="groupOwner">UUID of group to own the object</param>
        public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
            InventoryItem item, UUID groupOwner)
        {
            return RequestRezFromInventory(simulator, rotation, position, item, groupOwner, UUID.Random(), true);
        }

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        /// <param name="groupOwner">UUID of group to own the object</param>        
        /// <param name="queryID">User defined queryID to correlate replies</param>
        /// <param name="rezSelected">If set to true, the CreateSelected flag
        /// will be set on the rezzed object</param>        
        public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
            InventoryItem item, UUID groupOwner, UUID queryID, bool rezSelected)
        {
            return RequestRezFromInventory(simulator, UUID.Zero, rotation, position, item, groupOwner, queryID,
                                           rezSelected);
        }

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="taskID">TaskID object when rezzed</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        /// <param name="groupOwner">UUID of group to own the object</param>        
        /// <param name="queryID">User defined queryID to correlate replies</param>
        /// <param name="rezSelected">If set to true, the CreateSelected flag
        /// will be set on the rezzed object</param>        
        public UUID RequestRezFromInventory(Simulator simulator, UUID taskID, Quaternion rotation, Vector3 position,
            InventoryItem item, UUID groupOwner, UUID queryID, bool rezSelected)
        {
            var add = new RezObjectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupOwner
                },
                RezData =
                {
                    FromTaskID = taskID,
                    BypassRaycast = 1,
                    RayStart = position,
                    RayEnd = position,
                    RayTargetID = UUID.Zero,
                    RayEndIsIntersection = false,
                    RezSelected = rezSelected,
                    RemoveItem = false,
                    ItemFlags = (uint) item.Flags,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask
                },
                InventoryData =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = queryID,
                    Type = (sbyte) item.InventoryType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate)
                }
            };

            Client.Network.SendPacket(add, simulator);

            // Remove from store if the item is no copy
            if (Store.Contains(item.UUID) && Store[item.UUID] is InventoryItem invItem)
            {
                if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                {
                    Store.RemoveNodeFor(invItem);
                }
            }

            return queryID;
        }

        /// <summary>
        /// DeRez an object from the simulator to the agents Objects folder in the agents Inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
        public void RequestDeRezToInventory(uint objectLocalID)
        {
            RequestDeRezToInventory(objectLocalID, DeRezDestination.AgentInventoryTake,
                Client.Inventory.FindFolderForType(AssetType.Object), UUID.Random());
        }

        /// <summary>
        /// DeRez an object from the simulator and return to inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <param name="destType">The type of destination from the <see cref="DeRezDestination"/> enum</param>
        /// <param name="destFolder">The destination inventory folders <see cref="UUID"/> -or-
        /// if DeRezzing object to a tasks Inventory, the Tasks <see cref="UUID"/></param>
        /// <param name="transactionID">The transaction ID for this request which
        /// can be used to correlate this request with other packets</param>
        /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
        public void RequestDeRezToInventory(uint objectLocalID, DeRezDestination destType, UUID destFolder, UUID transactionID)
        {
            var take = new DeRezObjectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                AgentBlock = new DeRezObjectPacket.AgentBlockBlock
                {
                    GroupID = UUID.Zero,
                    Destination = (byte)destType,
                    DestinationID = destFolder,
                    PacketCount = 1,
                    PacketNumber = 1,
                    TransactionID = transactionID
                },
                ObjectData = new DeRezObjectPacket.ObjectDataBlock[1]
            };


            take.ObjectData[0] = new DeRezObjectPacket.ObjectDataBlock { ObjectLocalID = objectLocalID };

            Client.Network.SendPacket(take);
        }

        /// <summary>
        /// Empty a folder by removing all of its contents (including sub-folders)
        /// </summary>
        /// <param name="folderID">The folder to empty</param>
        /// <remarks>This does not remove the folder itself, only its contents</remarks>
        public void EmptyFolder(UUID folderID)
        {
            if (Client.AisClient.IsAvailable)
            {
                Client.AisClient.PurgeDescendents(folderID, RemoveLocalUi).ConfigureAwait(false);
            }
            else
            {
                var items = _Store.GetContents(folderID);
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

        /// <summary>
        /// Rez an item from inventory to its previous simulator location
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="item"></param>
        /// <param name="queryID"></param>
        /// <returns></returns>
        public UUID RequestRestoreRezFromInventory(Simulator simulator, InventoryItem item, UUID queryID)
        {
            var add = new RezRestoreToWorldPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = queryID,
                    Type = (sbyte) item.InventoryType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate)
                }
            };



            Client.Network.SendPacket(add, simulator);

            return queryID;
        }

        /// <summary>
        /// Give an inventory item to another avatar
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the item to give</param>
        /// <param name="itemName">The name of the item</param>
        /// <param name="assetType">The type of the item from the <see cref="AssetType"/> enum</param>
        /// <param name="recipient">The <see cref="UUID"/> of the recipient</param>
        /// <param name="doEffect">true to generate a beam-effect during transfer</param>
        public void GiveItem(UUID itemID, string itemName, AssetType assetType, UUID recipient,
            bool doEffect)
        {
            var bucket = new byte[17];
            bucket[0] = (byte)assetType;
            Buffer.BlockCopy(itemID.GetBytes(), 0, bucket, 1, 16);

            Client.Self.InstantMessage(
                    Client.Self.Name,
                    recipient,
                    itemName,
                    UUID.Random(),
                    InstantMessageDialog.InventoryOffered,
                    InstantMessageOnline.Online,
                    Client.Self.SimPosition,
                    Client.Network.CurrentSim.ID,
                    bucket);

            if (doEffect)
            {
                Client.Self.BeamEffect(Client.Self.AgentID, recipient, Vector3d.Zero,
                    Client.Settings.DEFAULT_EFFECT_COLOR, 1f, UUID.Random());
            }

            // Remove from store if the item is no copy
            if (Store.Contains(itemID) && Store[itemID] is InventoryItem item)
            {
                if ((item.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                {
                    Store.RemoveNodeFor(item);
                }
            }
        }

        /// <summary>
        /// Recurse inventory category and return folders and items. Does NOT contain parent folder being searched
        /// </summary>
        /// <param name="folderID">Inventory category to recursively search</param>
        /// <param name="owner">Owner of folder</param>
        /// <param name="cats">reference to list of categories</param>
        /// <param name="items">reference to list of items</param>
        private void GetInventoryRecursive(UUID folderID, UUID owner,
            ref List<InventoryFolder> cats, ref List<InventoryItem> items)
        {
            // Use the async implementation with a reasonable timeout to preserve original behavior
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    GetInventoryRecursiveAsync(folderID, owner, cats, items, cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // preserve previous behavior: if timeout occurs, just return what we have
                }
            }
        }

        /// <summary>
        /// Give an inventory Folder with contents to another avatar
        /// </summary>
        /// <param name="folderID">The <see cref="UUID"/> of the Folder to give</param>
        /// <param name="folderName">The name of the folder</param>
        /// <param name="recipient">The <see cref="UUID"/> of the recipient</param>
        /// <param name="doEffect">true to generate a beam-effect during transfer</param>
        public void GiveFolder(UUID folderID, string folderName, UUID recipient, bool doEffect)
        {
            try
            {
                GiveFolderAsync(folderID, folderName, recipient, doEffect, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
            }
        }

        #endregion Rez/Give

        #region Task

        /// <summary>
        /// Copy or move an <see cref="InventoryItem"/> from agent inventory to a task (primitive) inventory
        /// </summary>
        /// <param name="objectLocalID">The target object</param>
        /// <param name="item">The item to copy or move from inventory</param>
        /// <returns>Returns transaction id</returns>
        /// <remarks>For items with copy permissions a copy of the item is placed in the tasks inventory,
        /// for no-copy items the object is moved to the tasks inventory</remarks>
        public UUID UpdateTaskInventory(uint objectLocalID, InventoryItem item)
        {
            var transactionID = UUID.Random();

            var update = new UpdateTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                UpdateData =
                {
                    Key = 0,
                    LocalID = objectLocalID
                },
                InventoryData =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = transactionID,
                    Type = (sbyte) item.AssetType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate),
                    CRC = ItemCRC(item)
                }
            };


            Client.Network.SendPacket(update);

            return transactionID;
        }

        /// <summary>
        /// Retrieve a listing of the items contained in a task (Primitive)
        /// </summary>
        /// <param name="objectID">The tasks <see cref="UUID"/></param>
        /// <param name="objectLocalID">The tasks simulator local ID</param>
        /// <param name="timeout">time to wait for reply from simulator</param>
        /// <returns>A list containing the inventory items inside the task or null
        /// if a timeout occurs</returns>
        /// <remarks>This request blocks until the response from the simulator arrives 
        /// before timeout is exceeded</remarks>
        [Obsolete("Use GetTaskInventoryAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public List<InventoryBase> GetTaskInventory(UUID objectID, uint objectLocalID, TimeSpan timeout)
        {
            string filename = null;
            var taskReplyEvent = new AutoResetEvent(false);

            void Callback(object sender, TaskInventoryReplyEventArgs e)
            {
                if (e.ItemID == objectID)
                {
                    filename = e.AssetFilename;
                    taskReplyEvent.Set();
                }
            }

            TaskInventoryReply += Callback;

            RequestTaskInventory(objectLocalID);

            taskReplyEvent.WaitOne(timeout, false);

            TaskInventoryReply -= Callback;

            if (!string.IsNullOrEmpty(filename))
            {
                byte[] assetData = null;
                ulong xferID = 0;
                var taskDownloadEvent = new AutoResetEvent(false);

                void XferCallback(object sender, XferReceivedEventArgs e)
                {
                    if (e.Xfer.XferID == xferID)
                    {
                        assetData = e.Xfer.AssetData;
                        taskDownloadEvent.Set();
                    }
                }

                Client.Assets.XferReceived += XferCallback;

                // Start the actual asset xfer
                xferID = Client.Assets.RequestAssetXfer(filename, true, false, UUID.Zero, AssetType.Unknown, true);

                taskDownloadEvent.WaitOne(timeout, false);

                Client.Assets.XferReceived -= XferCallback;

                var taskList = Utils.BytesToString(assetData);
                return ParseTaskInventory(taskList);
            }
            else
            {
                Logger.DebugLog("Task is empty for " + objectLocalID, Client);
                return new List<InventoryBase>(0);
            }
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory from the 
        /// current simulator
        /// </summary>
        /// <param name="objectLocalID">The LocalID of the object</param>
        /// <see cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID)
        {
            RequestTaskInventory(objectLocalID, Client.Network.CurrentSim);
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <param name="simulator">A reference to the simulator object that contains the object</param>
        /// <see cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID, Simulator simulator)
        {
            var request = new RequestTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = { LocalID = objectLocalID }
            };

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Move an item from a tasks (Primitive) inventory to the specified folder in the avatars inventory
        /// </summary>
        /// <param name="objectLocalID">LocalID of the object in the simulator</param>
        /// <param name="taskItemID">UUID of the task item to move</param>
        /// <param name="inventoryFolderID">The ID of the destination folder in this agents inventory</param>
        /// <param name="simulator">Simulator Object</param>
        /// <remarks>Raises the <see cref="OnTaskItemReceived"/> event</remarks>
        public void MoveTaskInventory(uint objectLocalID, UUID taskItemID, UUID inventoryFolderID, Simulator simulator)
        {
            var request = new MoveTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    FolderID = inventoryFolderID
                },
                InventoryData =
                {
                    ItemID = taskItemID,
                    LocalID = objectLocalID
                }
            };

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Remove an item from an objects (Prim) Inventory
        /// </summary>
        /// <param name="objectLocalID">LocalID of the object in the simulator</param>
        /// <param name="taskItemID">UUID of the task item to remove</param>
        /// <param name="simulator">Simulator Object</param>
        /// <remarks>You can confirm the removal by comparing the tasks inventory serial before and after the 
        /// request with the <see cref="RequestTaskInventory"/> request combined with
        /// the <see cref="TaskInventoryReply"/> event</remarks>
        public void RemoveTaskInventory(uint objectLocalID, UUID taskItemID, Simulator simulator)
        {
            var remove = new RemoveTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData =
                {
                    ItemID = taskItemID,
                    LocalID = objectLocalID
                }
            };

            Client.Network.SendPacket(remove, simulator);
        }

        /// <summary>
        /// Copy an InventoryScript item from the Agents Inventory into a primitives task inventory
        /// </summary>
        /// <param name="objectLocalID">An unsigned integer representing a primitive being simulated</param>
        /// <param name="item">An <see cref="InventoryItem"/> which represents a script object from the agents inventory</param>
        /// <param name="enableScript">true to set the scripts running state to enabled</param>
        /// <returns>A Unique Transaction ID</returns>
        /// <example>
        /// The following example shows the basic steps necessary to copy a script from the agents inventory into a tasks inventory
        /// and assumes the script exists in the agents inventory.
        /// <code>
        ///    uint primID = 95899503; // Fake prim ID
        ///    UUID scriptID = UUID.Parse("92a7fe8a-e949-dd39-a8d8-1681d8673232"); // Fake Script UUID in Inventory
        ///
        ///    Client.Inventory.FolderContents(Client.Inventory.FindFolderForType(AssetType.LSLText), Client.Self.AgentID, 
        ///        false, true, InventorySortOrder.ByName, 10000);
        ///
        ///    Client.Inventory.RezScript(primID, (InventoryItem)Client.Inventory.Store[scriptID]);
        /// </code>
        /// </example>
        public UUID CopyScriptToTask(uint objectLocalID, InventoryItem item, bool enableScript)
        {
            var transactionID = UUID.Random();

            var ScriptPacket = new RezScriptPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                UpdateBlock =
                {
                    ObjectLocalID = objectLocalID,
                    Enabled = enableScript
                },
                InventoryBlock =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = transactionID,
                    Type = (sbyte) item.AssetType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate),
                    CRC = ItemCRC(item)
                }
            };

            Client.Network.SendPacket(ScriptPacket);

            return transactionID;
        }


        /// <summary>
        /// Request the running status of a script contained in a task (primitive) inventory
        /// </summary>
        /// <param name="objectID">The ID of the primitive containing the script</param>
        /// <param name="scriptID">The ID of the script</param>
        /// <remarks>The <see cref="ScriptRunningReply"/> event can be used to obtain the results of the 
        /// request</remarks>
        /// <see cref="ScriptRunningReply"/>
        public void RequestGetScriptRunning(UUID objectID, UUID scriptID)
        {
            var request = new GetScriptRunningPacket
            {
                Script =
                {
                    ObjectID = objectID,
                    ItemID = scriptID
                }
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Send a request to set the running state of a script contained in a task (primitive) inventory
        /// </summary>
        /// <param name="objectID">The ID of the primitive containing the script</param>
        /// <param name="scriptID">The ID of the script</param>
        /// <param name="running">true to set the script running, false to stop a running script</param>
        /// <remarks>To verify the change you can use the <see cref="RequestGetScriptRunning"/> method combined
        /// with the <see cref="ScriptRunningReply"/> event</remarks>
        public void RequestSetScriptRunning(UUID objectID, UUID scriptID, bool running)
        {
            var request = new SetScriptRunningPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Script =
                {
                    Running = running,
                    ItemID = scriptID,
                    ObjectID = objectID
                }
            };

            Client.Network.SendPacket(request);
        }

        #endregion Task

        #region Helper Functions

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
                Logger.Log("Inventory store not initialized, cannot create or retrieve inventory item",
                    Helpers.LogLevel.Warning, Client);
                return null;
            }

            if (_Store.Contains(ItemID))
                ret = _Store[ItemID] as InventoryItem;

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

            // CRC += another 4 words which always seem to be zero -- unclear if this is a UUID or what
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

        /// <summary>
        /// Parse the results of a RequestTaskInventory() response
        /// </summary>
        /// <param name="taskData">A string which contains the data from the task reply</param>
        /// <returns>A List containing the items contained within the tasks inventory</returns>
        public static List<InventoryBase> ParseTaskInventory(string taskData)
        {
            var items = new List<InventoryBase>();
            var lineNum = 0;
            var lines = taskData.Replace("\r\n", "\n").Split('\n');

            while (lineNum < lines.Length)
            {
                string key, value;
                if (ParseLine(lines[lineNum++], out key, out value))
                {
                    if (key == "inv_object")
                    {
                        #region inv_object

                        // In practice this appears to only be used for folders
                        var itemID = UUID.Zero;
                        var parentID = UUID.Zero;
                        var name = string.Empty;
                        var assetType = AssetType.Unknown;

                        while (lineNum < lines.Length)
                        {
                            if (ParseLine(lines[lineNum++], out key, out value))
                            {
                                if (key == "{")
                                {
                                    continue;
                                }
                                else if (key == "}")
                                {
                                    break;
                                }
                                else if (key == "obj_id")
                                {
                                    UUID.TryParse(value, out itemID);
                                }
                                else if (key == "parent_id")
                                {
                                    UUID.TryParse(value, out parentID);
                                }
                                else if (key == "type")
                                {
                                    assetType = Utils.StringToAssetType(value);
                                }
                                else if (key == "name")
                                {
                                    name = value.Substring(0, value.IndexOf('|'));
                                }
                            }
                        }

                        if (assetType == AssetType.Folder)
                        {
                            var folder = new InventoryFolder(itemID)
                            {
                                Name = name,
                                ParentUUID = parentID
                            };

                            items.Add(folder);
                        }
                        else
                        {
                            var item = new InventoryItem(itemID)
                            {
                                Name = name,
                                ParentUUID = parentID,
                                AssetType = assetType
                            };

                            items.Add(item);
                        }

                        #endregion inv_object
                    }
                    else if (key == "inv_item")
                    {
                        #region inv_item

                        // Any inventory item that links to an assetID, has permissions, etc
                        var itemID = UUID.Zero;
                        var assetID = UUID.Zero;
                        var parentID = UUID.Zero;
                        var creatorID = UUID.Zero;
                        var ownerID = UUID.Zero;
                        var lastOwnerID = UUID.Zero;
                        var groupID = UUID.Zero;
                        var groupOwned = false;
                        var name = string.Empty;
                        var desc = string.Empty;
                        var assetType = AssetType.Unknown;
                        var inventoryType = InventoryType.Unknown;
                        var creationDate = Utils.Epoch;
                        uint flags = 0;
                        var perms = Permissions.NoPermissions;
                        var saleType = SaleType.Not;
                        var salePrice = 0;

                        while (lineNum < lines.Length)
                        {
                            if (ParseLine(lines[lineNum++], out key, out value))
                            {
                                if (key == "{")
                                {
                                    continue;
                                }
                                else if (key == "}")
                                {
                                    break;
                                }
                                else if (key == "item_id")
                                {
                                    UUID.TryParse(value, out itemID);
                                }
                                else if (key == "parent_id")
                                {
                                    UUID.TryParse(value, out parentID);
                                }
                                else if (key == "permissions")
                                {
                                    #region permissions

                                    while (lineNum < lines.Length)
                                    {
                                        if (ParseLine(lines[lineNum++], out key, out value))
                                        {
                                            if (key == "{")
                                            {
                                                continue;
                                            }
                                            else if (key == "}")
                                            {
                                                break;
                                            }
                                            else if (key == "creator_mask")
                                            {
                                                // Deprecated
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.BaseMask = (PermissionMask)val;
                                            }
                                            else if (key == "base_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.BaseMask = (PermissionMask)val;
                                            }
                                            else if (key == "owner_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.OwnerMask = (PermissionMask)val;
                                            }
                                            else if (key == "group_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.GroupMask = (PermissionMask)val;
                                            }
                                            else if (key == "everyone_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.EveryoneMask = (PermissionMask)val;
                                            }
                                            else if (key == "next_owner_mask")
                                            {
                                                uint val;
                                                if (Utils.TryParseHex(value, out val))
                                                    perms.NextOwnerMask = (PermissionMask)val;
                                            }
                                            else if (key == "creator_id")
                                            {
                                                UUID.TryParse(value, out creatorID);
                                            }
                                            else if (key == "owner_id")
                                            {
                                                UUID.TryParse(value, out ownerID);
                                            }
                                            else if (key == "last_owner_id")
                                            {
                                                UUID.TryParse(value, out lastOwnerID);
                                            }
                                            else if (key == "group_id")
                                            {
                                                UUID.TryParse(value, out groupID);
                                            }
                                            else if (key == "group_owned")
                                            {
                                                uint val;
                                                if (uint.TryParse(value, out val))
                                                    groupOwned = (val != 0);
                                            }
                                        }
                                    }

                                    #endregion permissions
                                }
                                else if (key == "sale_info")
                                {
                                    #region sale_info

                                    while (lineNum < lines.Length)
                                    {
                                        if (ParseLine(lines[lineNum++], out key, out value))
                                        {
                                            if (key == "{")
                                            {
                                                continue;
                                            }
                                            else if (key == "}")
                                            {
                                                break;
                                            }
                                            else if (key == "sale_type")
                                            {
                                                saleType = Utils.StringToSaleType(value);
                                            }
                                            else if (key == "sale_price")
                                            {
                                                int.TryParse(value, out salePrice);
                                            }
                                        }
                                    }

                                    #endregion sale_info
                                }
                                else if (key == "shadow_id")
                                {
                                    UUID shadowID;
                                    if (UUID.TryParse(value, out shadowID))
                                        assetID = DecryptShadowID(shadowID);
                                }
                                else if (key == "asset_id")
                                {
                                    UUID.TryParse(value, out assetID);
                                }
                                else if (key == "type")
                                {
                                    assetType = Utils.StringToAssetType(value);
                                }
                                else if (key == "inv_type")
                                {
                                    inventoryType = Utils.StringToInventoryType(value);
                                }
                                else if (key == "flags")
                                {
                                    uint.TryParse(value, out flags);
                                }
                                else if (key == "name")
                                {
                                    name = value.Substring(0, value.IndexOf('|'));
                                }
                                else if (key == "desc")
                                {
                                    desc = value.Substring(0, value.IndexOf('|'));
                                }
                                else if (key == "creation_date")
                                {
                                    uint timestamp;
                                    if (uint.TryParse(value, out timestamp))
                                        creationDate = Utils.UnixTimeToDateTime(timestamp);
                                    else
                                        Logger.Log($"Failed to parse creation_date: {value}", Helpers.LogLevel.Warning);
                                }
                            }
                        }

                        var item = CreateInventoryItem(inventoryType, itemID);
                        item.AssetUUID = assetID;
                        item.AssetType = assetType;
                        item.CreationDate = creationDate;
                        item.CreatorID = creatorID;
                        item.Description = desc;
                        item.Flags = flags;
                        item.GroupID = groupID;
                        item.GroupOwned = groupOwned;
                        item.Name = name;
                        item.OwnerID = ownerID;
                        item.LastOwnerID = lastOwnerID;
                        item.ParentUUID = parentID;
                        item.Permissions = perms;
                        item.SalePrice = salePrice;
                        item.SaleType = saleType;

                        items.Add(item);

                        #endregion inv_item
                    }
                    else
                    {
                        Logger.Log($"Unrecognized token {key} in: " + Environment.NewLine + taskData,
                            Helpers.LogLevel.Error);
                    }
                }
            }

            return items;
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
                Logger.Log("Overwriting an existing ItemCreatedCallback", Helpers.LogLevel.Warning, Client);
            }

            // Schedule cleanup in case the server never responds. If the callback is
            // already removed by a successful response the TryRemove here will fail
            // and we won't invoke the callback twice.
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(CALLBACK_TIMEOUT_MS).ConfigureAwait(false);
                    if (_ItemCreatedCallbacks.TryRemove(id, out var cb))
                    {
                        try
                        {
                            // Signal failure/timeout to the caller
                            cb(false, null);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Debug, Client, ex);
                }
            }).ConfigureAwait(false);

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
                Logger.Log("Overwriting an existing ItemsCopiedCallback", Helpers.LogLevel.Warning, Client);
            }

            // Schedule cleanup for copied-item callbacks as well to avoid leaks
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(CALLBACK_TIMEOUT_MS).ConfigureAwait(false);
                    if (_ItemCopiedCallbacks.TryRemove(id, out var cb))
                    {
                        try
                        {
                            // Indicate failure by passing null
                            cb(null);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Debug, Client, ex);
                }
            }).ConfigureAwait(false);

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

        #endregion Helper Functions

        #region Internal Callbacks

        // Centralized capability lookup helper to reduce duplicated null checks
        private Uri GetCapabilityURI(string capName, bool logOnMissing = true)
        {
            var sim = Client?.Network?.CurrentSim;
            Uri uri = sim?.Caps?.CapabilityURI(capName);
            if (uri == null && logOnMissing)
            {
                var simName = sim?.Name ?? "unknown";
                Logger.Log($"Failed to obtain {capName} capability on {simName}", Helpers.LogLevel.Warning, Client);
            }
            return uri;
        }

        // POST JSON/XML OSD payload to a capability URI and return deserialized OSD result.
        private async Task<OSD> PostCapAsync(Uri uri, OSD payload, CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var tcs = new TaskCompletionSource<OSD>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Kick off the HTTP POST and wire up the callback to the TaskCompletionSource
            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, payload, cancellationToken,
                (response, responseData, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(error);
                        return;
                    }

                    try
                    {
                        var osd = OSDParser.Deserialize(responseData);
                        tcs.TrySetResult(osd);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            return await tcs.Task.ConfigureAwait(false);
        }

        // Convenience overload that looks up the capability by name first
        private async Task<OSD> PostCapAsync(string capName, OSD payload, CancellationToken cancellationToken = default)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) throw new InvalidOperationException($"Capability {capName} is not available");
            return await PostCapAsync(uri, payload, cancellationToken).ConfigureAwait(false);
        }

        // POST raw bytes to a capability URI (e.g., uploader endpoints) and return deserialized OSD result
        private async Task<OSD> PostBytesAsync(Uri uri, string contentType, byte[] data, CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var tcs = new TaskCompletionSource<OSD>(TaskCreationOptions.RunContinuationsAsynchronously);

            await Client.HttpCapsClient.PostRequestAsync(uri, contentType, data, cancellationToken,
                (response, responseData, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(error);
                        return;
                    }

                    try
                    {
                        var osd = OSDParser.Deserialize(responseData);
                        tcs.TrySetResult(osd);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            return await tcs.Task.ConfigureAwait(false);
        }

        // Convenience overload for consistency
        private async Task<OSD> PostBytesAsync(string capName, string contentType, byte[] data, CancellationToken cancellationToken = default)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) throw new InvalidOperationException($"Capability {capName} is not available");
            return await PostBytesAsync(uri, contentType, data, cancellationToken).ConfigureAwait(false);
        }

        void Self_IM(object sender, InstantMessageEventArgs e)
        {
            // TODO: MainAvatar.InstantMessageDialog.GroupNotice can also be an inventory offer, should we
            // handle it here?

            if (m_InventoryObjectOffered != null &&
                (e.IM.Dialog == InstantMessageDialog.InventoryOffered
                || e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered))
            {
                var type = AssetType.Unknown;
                var objectID = UUID.Zero;
                var fromTask = false;

                if (e.IM.Dialog == InstantMessageDialog.InventoryOffered)
                {
                    if (e.IM.BinaryBucket.Length == 17)
                    {
                        type = (AssetType)e.IM.BinaryBucket[0];
                        objectID = new UUID(e.IM.BinaryBucket, 1);
                        fromTask = false;
                    }
                    else
                    {
                        Logger.Log("Malformed inventory offer from agent", Helpers.LogLevel.Warning, Client);
                        return;
                    }
                }
                else if (e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered)
                {
                    if (e.IM.BinaryBucket.Length == 1)
                    {
                        type = (AssetType)e.IM.BinaryBucket[0];
                        fromTask = true;
                    }
                    else
                    {
                        Logger.Log("Malformed inventory offer from object", Helpers.LogLevel.Warning, Client);
                        return;
                    }
                }

                // Find the folder where this is going to go
                var destinationFolderID = FindFolderForType(type);

                // Fire the callback
                try
                {
                    var imp = new ImprovedInstantMessagePacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        MessageBlock =
                        {
                            FromGroup = false,
                            ToAgentID = e.IM.FromAgentID,
                            Offline = 0,
                            ID = e.IM.IMSessionID,
                            Timestamp = 0,
                            FromAgentName = Utils.StringToBytes(Client.Self.Name),
                            Message = Utils.EmptyBytes,
                            ParentEstateID = 0,
                            RegionID = UUID.Zero,
                            Position = Client.Self.SimPosition
                        }
                    };

                    var args = new InventoryObjectOfferedEventArgs(e.IM, type, objectID, fromTask, destinationFolderID);

                    OnInventoryObjectOffered(args);

                    if (args.Accept)
                    {
                        // Accept the inventory offer
                        switch (e.IM.Dialog)
                        {
                            case InstantMessageDialog.InventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryAccepted;
                                break;
                            case InstantMessageDialog.TaskInventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryAccepted;
                                break;
                            case InstantMessageDialog.GroupNotice:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryAccepted;
                                break;
                        }
                        imp.MessageBlock.BinaryBucket = args.FolderID.GetBytes();
                        RequestFetchInventory(objectID, e.IM.ToAgentID);
                    }
                    else
                    {
                        // Decline the inventory offer
                        switch (e.IM.Dialog)
                        {
                            case InstantMessageDialog.InventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryDeclined;
                                break;
                            case InstantMessageDialog.TaskInventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryDeclined;
                                break;
                            case InstantMessageDialog.GroupNotice:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryDeclined;
                                break;
                        }

                        imp.MessageBlock.BinaryBucket = Utils.EmptyBytes;
                    }

                    Client.Network.SendPacket(imp, e.Simulator ?? Client.Network.CurrentSim);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }
        }

        private void CreateItemFromAssetResponse(ItemCreatedFromAssetCallback callback, byte[] itemData, OSDMap request, 
            OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            if (result == null)
            {
                try
                {
                    callback(false, error?.Message ?? "Unknown error", UUID.Zero, UUID.Zero);
                }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                return;
            }

            if (result.Type == OSDType.Unknown)
            {
                try
                {
                    callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }

            var contents = (OSDMap)result;

            var status = contents["state"].AsString().ToLower();

            if (status == "upload")
            {
                var uploadURL = contents["uploader"].AsString();

                Logger.DebugLog($"CreateItemFromAsset: uploading to {uploadURL}");

                // This makes the assumption that all uploads go to CurrentSim, to avoid
                // the problem of HttpRequestState not knowing anything about simulators
                var uploadUri = new Uri(uploadURL);

                // Fire-and-forget async upload using centralized helper
                Task.Run(async () =>
                {
                    try
                    {
                        var res = await PostBytesAsync(uploadUri, "application/octet-stream", itemData, cancellationToken).ConfigureAwait(false);
                        CreateItemFromAssetResponse(callback, itemData, request, res, null, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        CreateItemFromAssetResponse(callback, itemData, request, null, ex, cancellationToken);
                    }
                }, cancellationToken);
            }
            else if (status == "complete")
            {
                Logger.DebugLog("CreateItemFromAsset: completed");

                if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
                {
                    // Request full update on the item in order to update the local store
                    RequestFetchInventory(contents["new_inventory_item"].AsUUID(), Client.Self.AgentID);

                    try { callback(true, string.Empty, contents["new_inventory_item"].AsUUID(), contents["new_asset"].AsUUID()); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
                else
                {
                    try
                    {
                        callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
            }
            else
            {
                // Failure
                try { callback(false, status, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }
        }

        private void UploadInventoryAssetResponse(KeyValuePair<InventoryUploadedAssetCallback, byte[]> kvp,
            UUID itemId, OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            var callback = kvp.Key;
            var itemData = (byte[])kvp.Value;

            if (error == null && result is OSDMap contents)
            {
                var status = contents["state"].AsString();

                if (status == "upload")
                {
                    var uploadURL = contents["uploader"].AsUri();

                    if (uploadURL != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                var res = await PostBytesAsync(uploadURL, "application/octet-stream", itemData, cancellationToken).ConfigureAwait(false);
                                UploadInventoryAssetResponse(kvp, itemId, res, null, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                UploadInventoryAssetResponse(kvp, itemId, null, ex, cancellationToken);
                            }
                        }, cancellationToken);
                    }
                    else
                    {
                        try { callback(false, "Missing uploader URL", UUID.Zero, UUID.Zero); }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                }
                else if (status == "complete" && callback != null)
                {
                    if (contents.ContainsKey("new_asset"))
                    {
                        // Request full item update so we keep store in sync
                        RequestFetchInventory(itemId, Client.Self.AgentID);

                        try { callback(true, string.Empty, itemId, contents["new_asset"].AsUUID()); }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                    else
                    {
                        try
                        {
                            callback(false, "Failed to parse asset UUID",
                            UUID.Zero, UUID.Zero);
                        }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                }
                else if (callback != null)
                {
                    try { callback(false, status, UUID.Zero, UUID.Zero); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
            }
            else
            {
                var message = "Unrecognized or empty response";

                if (error != null)
                {
                    if (error is WebException webEx && webEx.Response is HttpWebResponse http)
                        message = http.StatusDescription ?? webEx.Message;
                    else
                        message = error.Message;
                }

                try { callback(false, message, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }
        }

        private void UpdateScriptAgentInventoryResponse(KeyValuePair<ScriptUpdatedCallback, byte[]> kvpCb,
            UUID itemId, OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            var callback = kvpCb.Key;
            var itemData = kvpCb.Value;

            if (result == null)
            {
                try
                {
                    callback(false, error?.Message ?? "Unknown error", false,
                    null, UUID.Zero, UUID.Zero);
                }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                return;
            }

            var contents = (OSDMap)result;

            var status = contents["state"].AsString();
            if (status == "upload")
            {
                var uploadURL = contents["uploader"].AsString();

                // This makes the assumption that all uploads go to CurrentSim, to avoid
                // the problem of HttpRequestState not knowing anything about simulators
                var uploadUri = new Uri(uploadURL);

                Task.Run(async () =>
                {
                    try
                    {
                        var res = await PostBytesAsync(uploadUri, "application/octet-stream", itemData, cancellationToken).ConfigureAwait(false);
                        UpdateScriptAgentInventoryResponse(kvpCb, itemId, res, null, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        UpdateScriptAgentInventoryResponse(kvpCb, itemId, null, ex, cancellationToken);
                    }
                }, cancellationToken);
            }
            else if (status == "complete" && callback != null)
            {
                if (contents.ContainsKey("new_asset"))
                {
                    // Request full item update so we keep store in sync
                    RequestFetchInventory(itemId, Client.Self.AgentID);

                    try
                    {
                        List<string> compileErrors = null;

                        if (contents.TryGetValue("errors", out var content))
                        {
                            var errors = (OSDArray)content;
                            compileErrors = new List<string>(errors.Count);
                            compileErrors.AddRange(errors.Select(t => t.AsString()));
                        }

                        callback(true,
                            status,
                            contents["compiled"].AsBoolean(),
                            compileErrors,
                            itemId,
                            contents["new_asset"].AsUUID());
                    }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
                else
                {
                    try
                    {
                        callback(false, "Failed to parse asset UUID",
                        false, null, UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
            }
            else if (callback != null)
            {
                try
                {
                    callback(false, status, false,
                    null, UUID.Zero, UUID.Zero);
                }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }
        }

        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason, LoginResponseData replyData)
        {
            if (!loginSuccess) { return; }
            if (replyData.InventorySkeleton == null || replyData.LibrarySkeleton == null) { return; }

            // Initialize the store here to link it with the owner
            _Store = new Inventory(Client, Client.Self.AgentID);
            Logger.DebugLog($"Setting InventoryRoot to {replyData.InventoryRoot}", Client);
            var rootFolder = new InventoryFolder(replyData.InventoryRoot)
            {
                Name = string.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.RootFolder = rootFolder;

            foreach (var folder in replyData.InventorySkeleton)
                _Store.UpdateNodeFor(folder);

            var libraryRootFolder = new InventoryFolder(replyData.LibraryRoot)
            {
                Name = string.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.LibraryFolder = libraryRootFolder;

            foreach (var folder in replyData.LibrarySkeleton)
                _Store.UpdateNodeFor(folder);
        }


        #endregion Internal Handlers
    }
}
