/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using System.Runtime.Serialization;
using System.Threading.Tasks;
using OpenMetaverse.Http;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    #region Enums

    [Flags]
    public enum InventorySortOrder : int
    {
        /// <summary>Sort by name</summary>
        ByName = 0,
        /// <summary>Sort by date</summary>
        ByDate = 1,
        /// <summary>Sort folders by name, regardless of whether items are
        /// sorted by name or date</summary>
        FoldersByName = 2,
        /// <summary>Place system folders at the top</summary>
        SystemFoldersToTop = 4
    }

    /// <summary>
    /// Possible destinations for DeRezObject request
    /// </summary>
    public enum DeRezDestination : byte
    {
        /// <summary></summary>
        AgentInventorySave = 0,
        /// <summary>Copy from in-world to agent inventory</summary>
        AgentInventoryCopy = 1,
        /// <summary>Derez to TaskInventory</summary>
        TaskInventory = 2,
        /// <summary></summary>
        Attachment = 3,
        /// <summary>Take Object</summary>
        AgentInventoryTake = 4,
        /// <summary>God force to inventory</summary>
        ForceToGodInventory = 5,
        /// <summary>Delete Object</summary>
        TrashFolder = 6,
        /// <summary>Put an avatar attachment into agent inventory</summary>
        AttachmentToInventory = 7,
        /// <summary></summary>
        AttachmentExists = 8,
        /// <summary>Return an object back to the owner's inventory</summary>
        ReturnToOwner = 9,
        /// <summary>Return a deeded object back to the last owner's inventory</summary>
        ReturnToLastOwner = 10
    }

    /// <summary>
    /// Upper half of the Flags field for inventory items
    /// </summary>
    [Flags]
    public enum InventoryItemFlags : uint
    {
        None = 0,
        /// <summary>Indicates that the NextOwner permission will be set to the
        /// most restrictive set of permissions found in the object set
        /// (including linkset items and object inventory items) on next rez</summary>
        ObjectSlamPerm = 0x100,
        /// <summary>Indicates that the object sale information has been
        /// changed</summary>
        ObjectSlamSale = 0x1000,
        /// <summary>If set, and a slam bit is set, indicates BaseMask will be overwritten on Rez</summary>
        ObjectOverwriteBase = 0x010000,
        /// <summary>If set, and a slam bit is set, indicates OwnerMask will be overwritten on Rez</summary>
        ObjectOverwriteOwner = 0x020000,
        /// <summary>If set, and a slam bit is set, indicates GroupMask will be overwritten on Rez</summary>
        ObjectOverwriteGroup = 0x040000,
        /// <summary>If set, and a slam bit is set, indicates EveryoneMask will be overwritten on Rez</summary>
        ObjectOverwriteEveryone = 0x080000,
        /// <summary>If set, and a slam bit is set, indicates NextOwnerMask will be overwritten on Rez</summary>
        ObjectOverwriteNextOwner = 0x100000,
        /// <summary>Indicates whether this object is composed of multiple
        /// items or not</summary>
        ObjectHasMultipleItems = 0x200000,
        /// <summary>Indicates that the asset is only referenced by this
        /// inventory item. If this item is deleted or updated to reference a
        /// new assetID, the asset can be deleted</summary>
        SharedSingleReference = 0x40000000,
    }

    #endregion Enums

    /// <summary>
    /// Tools for dealing with agents inventory
    /// </summary>
    [Serializable()]
    public class InventoryManager
    {
        /// <summary>Used for converting shadow_id to asset_id</summary>
        public static readonly UUID MAGIC_ID = new UUID("3c115e51-04f4-523c-9fa6-98aff1034730");

        /// <summary>Maximum items allowed to give</summary>
        public static readonly int MAX_GIVE_ITEMS = 66; // viewer code says 66, but 42 in the notification

        protected struct InventorySearch
        {
            public UUID Folder;
            public UUID Owner;
            public string[] Path;
            public int Level;
        }

        #region Delegates

        /// <summary>
        /// Callback for inventory item creation finishing
        /// </summary>
        /// <param name="success">Whether the request to create an inventory
        /// item succeeded or not</param>
        /// <param name="item">Inventory item being created. If success is
        /// false this will be null</param>
        public delegate void ItemCreatedCallback(bool success, InventoryItem item);

        /// <summary>
        /// Callback for an inventory item being create from an uploaded asset
        /// </summary>
        /// <param name="success">true if inventory item creation was successful</param>
        /// <param name="status"></param>
        /// <param name="itemID"></param>
        /// <param name="assetID"></param>
        public delegate void ItemCreatedFromAssetCallback(bool success, string status, UUID itemID, UUID assetID);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public delegate void ItemCopiedCallback(InventoryBase item);

        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<ItemReceivedEventArgs> m_ItemReceived;

        ///<summary>Raises the ItemReceived Event</summary>
        /// <param name="e">A ItemReceivedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnItemReceived(ItemReceivedEventArgs e)
        {
            EventHandler<ItemReceivedEventArgs> handler = m_ItemReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ItemReceivedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<ItemReceivedEventArgs> ItemReceived
        {
            add { lock (m_ItemReceivedLock) { m_ItemReceived += value; } }
            remove { lock (m_ItemReceivedLock) { m_ItemReceived -= value; } }
        }


        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<FolderUpdatedEventArgs> m_FolderUpdated;

        ///<summary>Raises the FolderUpdated Event</summary>
        /// <param name="e">A FolderUpdatedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnFolderUpdated(FolderUpdatedEventArgs e)
        {
            EventHandler<FolderUpdatedEventArgs> handler = m_FolderUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FolderUpdatedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<FolderUpdatedEventArgs> FolderUpdated
        {
            add { lock (m_FolderUpdatedLock) { m_FolderUpdated += value; } }
            remove { lock (m_FolderUpdatedLock) { m_FolderUpdated -= value; } }
        }


        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<InventoryObjectOfferedEventArgs> m_InventoryObjectOffered;

        ///<summary>Raises the InventoryObjectOffered Event</summary>
        /// <param name="e">A InventoryObjectOfferedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnInventoryObjectOffered(InventoryObjectOfferedEventArgs e)
        {
            EventHandler<InventoryObjectOfferedEventArgs> handler = m_InventoryObjectOffered;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InventoryObjectOfferedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// an inventory object sent by another avatar or primitive</summary>
        public event EventHandler<InventoryObjectOfferedEventArgs> InventoryObjectOffered
        {
            add { lock (m_InventoryObjectOfferedLock) { m_InventoryObjectOffered += value; } }
            remove { lock (m_InventoryObjectOfferedLock) { m_InventoryObjectOffered -= value; } }
        }

        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<TaskItemReceivedEventArgs> m_TaskItemReceived;

        ///<summary>Raises the TaskItemReceived Event</summary>
        /// <param name="e">A TaskItemReceivedEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnTaskItemReceived(TaskItemReceivedEventArgs e)
        {
            EventHandler<TaskItemReceivedEventArgs> handler = m_TaskItemReceived;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TaskItemReceivedLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<TaskItemReceivedEventArgs> TaskItemReceived
        {
            add { lock (m_TaskItemReceivedLock) { m_TaskItemReceived += value; } }
            remove { lock (m_TaskItemReceivedLock) { m_TaskItemReceived -= value; } }
        }


        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<FindObjectByPathReplyEventArgs> m_FindObjectByPathReply;

        ///<summary>Raises the FindObjectByPath Event</summary>
        /// <param name="e">A FindObjectByPathEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnFindObjectByPathReply(FindObjectByPathReplyEventArgs e)
        {
            EventHandler<FindObjectByPathReplyEventArgs> handler = m_FindObjectByPathReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FindObjectByPathReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<FindObjectByPathReplyEventArgs> FindObjectByPathReply
        {
            add { lock (m_FindObjectByPathReplyLock) { m_FindObjectByPathReply += value; } }
            remove { lock (m_FindObjectByPathReplyLock) { m_FindObjectByPathReply -= value; } }
        }


        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<TaskInventoryReplyEventArgs> m_TaskInventoryReply;

        ///<summary>Raises the TaskInventoryReply Event</summary>
        /// <param name="e">A TaskInventoryReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnTaskInventoryReply(TaskInventoryReplyEventArgs e)
        {
            EventHandler<TaskInventoryReplyEventArgs> handler = m_TaskInventoryReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TaskInventoryReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<TaskInventoryReplyEventArgs> TaskInventoryReply
        {
            add { lock (m_TaskInventoryReplyLock) { m_TaskInventoryReply += value; } }
            remove { lock (m_TaskInventoryReplyLock) { m_TaskInventoryReply -= value; } }
        }

        /// <summary>
        /// Reply received when uploading an inventory asset
        /// </summary>
        /// <param name="success">Has upload been successful</param>
        /// <param name="status">Error message if upload failed</param>
        /// <param name="itemID">Inventory asset UUID</param>
        /// <param name="assetID">New asset UUID</param>
        public delegate void InventoryUploadedAssetCallback(bool success, string status, UUID itemID, UUID assetID);


        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<SaveAssetToInventoryEventArgs> m_SaveAssetToInventory;

        ///<summary>Raises the SaveAssetToInventory Event</summary>
        /// <param name="e">A SaveAssetToInventoryEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSaveAssetToInventory(SaveAssetToInventoryEventArgs e)
        {
            EventHandler<SaveAssetToInventoryEventArgs> handler = m_SaveAssetToInventory;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SaveAssetToInventoryLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<SaveAssetToInventoryEventArgs> SaveAssetToInventory
        {
            add { lock (m_SaveAssetToInventoryLock) { m_SaveAssetToInventory += value; } }
            remove { lock (m_SaveAssetToInventoryLock) { m_SaveAssetToInventory -= value; } }
        }

        /// <summary>
        /// Delegate that is invoked when script upload is completed
        /// </summary>
        /// <param name="uploadSuccess">Has upload succeded (note, there still might be compile errors)</param>
        /// <param name="uploadStatus">Upload status message</param>
        /// <param name="compileSuccess">Is compilation successful</param>
        /// <param name="compileMessages">If compilation failed, list of error messages, null on compilation success</param>
        /// <param name="itemID">Script inventory UUID</param>
        /// <param name="assetID">Script's new asset UUID</param>
        public delegate void ScriptUpdatedCallback(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string> compileMessages, UUID itemID, UUID assetID);

        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<ScriptRunningReplyEventArgs> m_ScriptRunningReply;

        ///<summary>Raises the ScriptRunningReply Event</summary>
        /// <param name="e">A ScriptRunningReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnScriptRunningReply(ScriptRunningReplyEventArgs e)
        {
            EventHandler<ScriptRunningReplyEventArgs> handler = m_ScriptRunningReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptRunningReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<ScriptRunningReplyEventArgs> ScriptRunningReply
        {
            add { lock (m_ScriptRunningReplyLock) { m_ScriptRunningReply += value; } }
            remove { lock (m_ScriptRunningReplyLock) { m_ScriptRunningReply -= value; } }
        }
        #endregion Delegates

        #region String Arrays

        /// <summary>Partial mapping of FolderTypes to folder names</summary>
        private static readonly string[] _NewFolderNames = new string[]
        {
            "Textures",         //  0
            "Sounds",           //  1
            "Calling Cards",    //  2
            "Landmarks",        //  3
            String.Empty,       //  4
            "Clothing",         //  5
            "Objects",          //  6
            "Notecards",        //  7
            "My Inventory",     //  8
            String.Empty,       //  9
            "Scripts",          // 10
            String.Empty,       // 11
            String.Empty,       // 12
            "Body Parts",       // 13
            "Trash",            // 14
            "Photo Album",      // 15
            "Lost And Found",   // 16
            String.Empty,       // 17
            String.Empty,       // 18
            String.Empty,       // 19
            "Animations",       // 20
            "Gestures",         // 21
            String.Empty,       // 22
            "Favorites",        // 23
            String.Empty,       // 24
            String.Empty,       // 25
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
        };


        #endregion String Arrays

        [NonSerialized]
        private GridClient Client;
        [NonSerialized]
        private Inventory _Store;
        //private Random _RandNumbers = new Random();
        private object _CallbacksLock = new object();
        private uint _CallbackPos;
        private Dictionary<uint, ItemCreatedCallback> _ItemCreatedCallbacks = new Dictionary<uint, ItemCreatedCallback>();
        private Dictionary<uint, ItemCopiedCallback> _ItemCopiedCallbacks = new Dictionary<uint, ItemCopiedCallback>();
        private Dictionary<uint, InventoryType> _ItemInventoryTypeRequest = new Dictionary<uint, InventoryType>();
        private List<InventorySearch> _Searches = new List<InventorySearch>();

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
            Client.Network.RegisterEventCallback("BulkUpdateInventory", new Caps.EventQueueCallback(BulkUpdateInventoryCapHandler));
            Client.Network.RegisterCallback(PacketType.MoveInventoryItem, MoveInventoryItemHandler);
            Client.Network.RegisterCallback(PacketType.ReplyTaskInventory, ReplyTaskInventoryHandler);
            Client.Network.RegisterEventCallback("ScriptRunningReply", new Caps.EventQueueCallback(ScriptRunningReplyMessageHandler));

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
        /// <param name="itemID">The items <seealso cref="UUID"/></param>
        /// <param name="ownerID">The item Owners <seealso cref="OpenMetaverse.UUID"/></param>
        /// <param name="timeoutMS">a integer representing the number of milliseconds to wait for results</param>
        /// <returns>An <seealso cref="InventoryItem"/> object on success, or null if no item was found</returns>
        /// <remarks>Items will also be sent to the <seealso cref="InventoryManager.OnItemReceived"/> event</remarks>
        public InventoryItem FetchItem(UUID itemID, UUID ownerID, int timeoutMS)
        {
            AutoResetEvent fetchEvent = new AutoResetEvent(false);
            InventoryItem fetchedItem = null;

            EventHandler<ItemReceivedEventArgs> callback =
                delegate(object sender, ItemReceivedEventArgs e)
                {
                    if (e.Item.UUID == itemID)
                    {
                        fetchedItem = e.Item;
                        fetchEvent.Set();
                    }
                };

            ItemReceived += callback;
            RequestFetchInventory(itemID, ownerID);

            fetchEvent.WaitOne(timeoutMS, false);
            ItemReceived -= callback;

            return fetchedItem;
        }

        /// <summary>
        /// Request A single inventory item
        /// </summary>
        /// <param name="itemID">The items <seealso cref="OpenMetaverse.UUID"/></param>
        /// <param name="ownerID">The item Owners <seealso cref="OpenMetaverse.UUID"/></param>
        /// <seealso cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(UUID itemID, UUID ownerID)
        {
            RequestFetchInventory(new List<UUID>(1) { itemID }, new List<UUID>(1) { ownerID });
        }

        /// <summary>
        /// Request inventory items
        /// </summary>
        /// <param name="itemIDs">Inventory items to request</param>
        /// <param name="ownerIDs">Owners of the inventory items</param>
        /// <seealso cref="InventoryManager.OnItemReceived"/>
        public void RequestFetchInventory(List<UUID> itemIDs, List<UUID> ownerIDs)
        {
            if (itemIDs.Count != ownerIDs.Count)
                throw new ArgumentException("itemIDs and ownerIDs must contain the same number of entries");

            if (Client.Settings.HTTP_INVENTORY &&
                Client.Network.CurrentSim.Caps != null &&
                Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventory2") != null)
            {
                RequestFetchInventoryCap(itemIDs, ownerIDs);
                return;
            }


            FetchInventoryPacket fetch = new FetchInventoryPacket
            {
                AgentData = new FetchInventoryPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = new FetchInventoryPacket.InventoryDataBlock[itemIDs.Count]
            };

            for (int i = 0; i < itemIDs.Count; i++)
            {
                fetch.InventoryData[i] = new FetchInventoryPacket.InventoryDataBlock
                {
                    ItemID = itemIDs[i],
                    OwnerID = ownerIDs[i]
                };
            }

            Client.Network.SendPacket(fetch);
        }

        /// <summary>
        /// Request inventory items via Capabilities
        /// </summary>
        /// <param name="itemIDs">Inventory items to request</param>
        /// <param name="ownerIDs">Owners of the inventory items</param>
        /// <seealso cref="InventoryManager.OnItemReceived"/>
        private void RequestFetchInventoryCap(IReadOnlyList<UUID> itemIDs, IReadOnlyList<UUID> ownerIDs)
        {
            if (itemIDs.Count != ownerIDs.Count)
                throw new ArgumentException("itemIDs and ownerIDs must contain the same number of entries");

            if (Client.Settings.HTTP_INVENTORY &&
                Client.Network.CurrentSim.Caps != null &&
                Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventory2") != null)
            {
                CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("FetchInventory2");
                
                request.OnComplete += (client, result, error) =>
                {
                    if (error != null) return;

                    try
                    {
                        OSDMap res = (OSDMap)result;
                        OSDArray itemsOSD = (OSDArray)res["items"];

                        foreach (var it in itemsOSD)
                        {
                            InventoryItem item = InventoryItem.FromOSD(it);
                            _Store[item.UUID] = item;
                            OnItemReceived(new ItemReceivedEventArgs(item));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Failed getting data from FetchInventory2 capability.", Helpers.LogLevel.Error, Client, ex);
                    }
                };

                OSDMap OSDRequest = new OSDMap {["agent_id"] = Client.Self.AgentID};

                OSDArray items = new OSDArray(itemIDs.Count);
                for (int i = 0; i < itemIDs.Count; i++)
                {
                    OSDMap item = new OSDMap(2)
                    {
                        ["item_id"] = itemIDs[i],
                        ["owner_id"] = ownerIDs[i]
                    };
                    items.Add(item);
                }

                OSDRequest["items"] = items;

                request.BeginGetResponse(OSDRequest, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
        }

        /// <summary>
        /// Get contents of a folder
        /// </summary>
        /// <param name="folder">The <seealso cref="UUID"/> of the folder to search</param>
        /// <param name="owner">The <seealso cref="UUID"/> of the folders owner</param>
        /// <param name="folders">true to retrieve folders</param>
        /// <param name="items">true to retrieve items</param>
        /// <param name="order">sort order to return results in</param>
        /// <param name="timeoutMS">a integer representing the number of milliseconds to wait for results</param>
        /// <returns>A list of inventory items matching search criteria within folder</returns>
        /// <seealso cref="InventoryManager.RequestFolderContents"/>
        /// <remarks>InventoryFolder.DescendentCount will only be accurate if both folders and items are
        /// requested</remarks>
        public List<InventoryBase> FolderContents(UUID folder, UUID owner, bool folders, bool items,
            InventorySortOrder order, int timeoutMS)
        {
            List<InventoryBase> objects = null;
            AutoResetEvent fetchEvent = new AutoResetEvent(false);

            void FolderUpdatedCB(object sender, FolderUpdatedEventArgs e)
            {
                if (e.FolderID == folder && _Store[folder] is InventoryFolder)
                {
                    // InventoryDescendentsHandler only stores DescendentCount if both folders and items are fetched.
                    if (_Store.GetContents(folder).Count >= ((InventoryFolder) _Store[folder]).DescendentCount)
                    {
                        fetchEvent.Set();
                    }
                }
                else
                {
                    fetchEvent.Set();
                }
            }

            FolderUpdated += FolderUpdatedCB;

            RequestFolderContents(folder, owner, folders, items, order);
            if (fetchEvent.WaitOne(timeoutMS, false))
                objects = _Store.GetContents(folder);

            FolderUpdated -= FolderUpdatedCB;

            return objects;
        }

        /// <summary>
        /// Request the contents of an inventory folder
        /// </summary>
        /// <param name="folder">The folder to search</param>
        /// <param name="owner">The folder owners <seealso cref="UUID"/></param>
        /// <param name="folders">true to return <seealso cref="InventoryFolder"/>s contained in folder</param>
        /// <param name="items">true to return <seealso cref="InventoryItem"/>s contained in folder</param>
        /// <param name="order">the sort order to return items in</param>
        /// <seealso cref="InventoryManager.FolderContents"/>
        public void RequestFolderContents(UUID folder, UUID owner, bool folders, bool items,
            InventorySortOrder order)
        {
            string cap = owner == Client.Self.AgentID ? "FetchInventoryDescendents2" : "FetchLibDescendents2";

            if (Client.Settings.HTTP_INVENTORY &&
                Client.Network.CurrentSim.Caps != null &&
                Client.Network.CurrentSim.Caps.CapabilityURI(cap) != null)
            {
                RequestFolderContentsCap(folder, owner, folders, items, order);
            }
            else // Legacy UDP - REMOVED FROM SECOND LIFE
            {
                var fetch = new FetchInventoryDescendentsPacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        InventoryData =
                        {
                            FetchFolders = folders,
                            FetchItems = items,
                            FolderID = folder,
                            OwnerID = owner,
                            SortOrder = (int) order
                        }
                    };
                Client.Network.SendPacket(fetch);
            }
        }

        /// <summary>
        /// Request the contents of an inventory folder using HTTP capabilities
        /// </summary>
        /// <param name="folderID">The folder to search</param>
        /// <param name="ownerID">The folder owners <seealso cref="UUID"/></param>
        /// <param name="fetchFolders">true to return <seealso cref="InventoryFolder"/>s contained in folder</param>
        /// <param name="fetchItems">true to return <seealso cref="InventoryItem"/>s contained in folder</param>
        /// <param name="order">the sort order to return items in</param>
        /// <seealso cref="InventoryManager.FolderContents"/>
        public void RequestFolderContentsCap(UUID folderID, UUID ownerID, bool fetchFolders, bool fetchItems,
            InventorySortOrder order)
        {
            Uri url = null;
            string cap = ownerID == Client.Self.AgentID ? "FetchInventoryDescendents2" : "FetchLibDescendents2";
            if (Client.Network.CurrentSim.Caps == null ||
                (url = Client.Network.CurrentSim.Caps.CapabilityURI(cap)) == null)
            {
                Logger.Log(cap + " capability not available in the current sim", Helpers.LogLevel.Warning, Client);
                OnFolderUpdated(new FolderUpdatedEventArgs(folderID, false));
            }
            else
            {
                InventoryFolder folder = new InventoryFolder(folderID)
                {
                    OwnerID = ownerID,
                    UUID = folderID
                };
                RequestFolderContentsCap(new List<InventoryFolder>() {folder}, url, fetchFolders, fetchItems, order);
            }
        }

        public void RequestFolderContentsCap(List<InventoryFolder> batch, Uri url, bool fetchFolders, bool fetchItems, InventorySortOrder order)
        {

            try
            {
                CapsClient request = new CapsClient(url, "ReqFolderContents");
                request.OnComplete += (client, result, error) =>
                {
                    try
                    {
                        if (error != null)
                        {
                            throw error;
                        }

                        OSDMap resultMap = ((OSDMap)result);
                        if (resultMap.ContainsKey("folders"))
                        {
                            OSDArray fetchedFolders = (OSDArray)resultMap["folders"];
                            foreach (var fetchedFolderNr in fetchedFolders)
                            {
                                OSDMap res = (OSDMap)fetchedFolderNr;
                                InventoryFolder fetchedFolder;

                                if (_Store.Contains(res["folder_id"])
                                    && _Store[res["folder_id"]] is InventoryFolder)
                                {
                                    fetchedFolder = (InventoryFolder)_Store[res["folder_id"]];
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
                                        foreach (OSD cat in folders)
                                        {
                                            OSDMap descFolder = (OSDMap)cat;
                                            InventoryFolder folder;
                                            UUID folderID = descFolder.ContainsKey("category_id") ? descFolder["category_id"] : descFolder["folder_id"];
                                            if (!_Store.Contains(folderID))
                                            {
                                                folder = new InventoryFolder(folderID)
                                                {
                                                    ParentUUID = descFolder["parent_id"]
                                                };
                                                _Store[folderID] = folder;
                                            }
                                            else
                                            {
                                                folder = (InventoryFolder)_Store[folderID];
                                            }

                                            folder.OwnerID = descFolder["agent_id"];
                                            folder.ParentUUID = descFolder["parent_id"];
                                            folder.Name = descFolder["name"];
                                            folder.Version = descFolder["version"];
                                            folder.PreferredType = (FolderType)(int)descFolder["type_default"];
                                        }

                                        // Fetch descendent items
                                        OSDArray items = (OSDArray)res["items"];
                                        foreach (OSD it in items)
                                        {
                                            InventoryItem item = InventoryItem.FromOSD(it);
                                            _Store[item.UUID] = item;
                                        }
                                    }
                                }

                                OnFolderUpdated(new FolderUpdatedEventArgs(res["folder_id"], true));
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Logger.Log($"Failed to fetch inventory descendants: {exc.Message}\n" +
                                   $"{exc.StackTrace.ToString()}", 
                                   Helpers.LogLevel.Warning, Client);
                        foreach (var f in batch)
                        {
                            OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
                        }
                    }

                };

                // Construct request
                OSDArray requestedFolders = new OSDArray(1);
                foreach (var f in batch)
                {
                    OSDMap requestedFolder = new OSDMap(1)
                    {
                        ["folder_id"] = f.UUID,
                        ["owner_id"] = f.OwnerID,
                        ["fetch_folders"] = fetchFolders,
                        ["fetch_items"] = fetchItems,
                        ["sort_order"] = (int) order
                    };

                    requestedFolders.Add(requestedFolder);
                }
                OSDMap req = new OSDMap(1) {["folders"] = requestedFolders};

                request.BeginGetResponse(req, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to fetch inventory descendants: {ex.Message}\n" +
                           $"{ex.StackTrace}",
                           Helpers.LogLevel.Warning, Client);
                foreach (var f in batch)
                {
                    OnFolderUpdated(new FolderUpdatedEventArgs(f.UUID, false));
                }
                return;
            }
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
            List<InventoryBase> contents = _Store.GetContents(_Store.RootFolder.UUID);
            foreach (InventoryBase inv in contents)
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

            List<InventoryBase> contents = _Store.GetContents(_Store.RootFolder.UUID);
            foreach (InventoryBase inv in contents)
            {
                InventoryFolder folder = inv as InventoryFolder;

                if (folder?.PreferredType == type)
                    return folder.UUID;
            }

            // No match found, return Root Folder ID
            return _Store.RootFolder.UUID;
        }

        /// <summary>
        /// Find an object in inventory using a specific path to search
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="inventoryOwner">The object owners <seealso cref="UUID"/></param>
        /// <param name="path">A string path to search</param>
        /// <param name="timeoutMS">milliseconds to wait for a reply</param>
        /// <returns>Found items <seealso cref="UUID"/> or <seealso cref="UUID.Zero"/> if 
        /// timeout occurs or item is not found</returns>
        public UUID FindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path, int timeoutMS)
        {
            AutoResetEvent findEvent = new AutoResetEvent(false);
            UUID foundItem = UUID.Zero;

            void Callback(object sender, FindObjectByPathReplyEventArgs e)
            {
                if (e.Path == path)
                {
                    foundItem = e.InventoryObjectID;
                    findEvent.Set();
                }
            }

            FindObjectByPathReply += Callback;

            RequestFindObjectByPath(baseFolder, inventoryOwner, path);
            findEvent.WaitOne(timeoutMS, false);

            FindObjectByPathReply -= Callback;

            return foundItem;
        }

        /// <summary>
        /// Find inventory items by path
        /// </summary>
        /// <param name="baseFolder">The folder to begin the search in</param>
        /// <param name="inventoryOwner">The object owners <seealso cref="UUID"/></param>
        /// <param name="path">A string path to search, folders/objects separated by a '/'</param>
        /// <remarks>Results are sent to the <seealso cref="InventoryManager.OnFindObjectByPath"/> event</remarks>
        public void RequestFindObjectByPath(UUID baseFolder, UUID inventoryOwner, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Empty path is not supported");

            // Store this search
            InventorySearch search;
            search.Folder = baseFolder;
            search.Owner = inventoryOwner;
            search.Path = path.Split('/');
            search.Level = 0;
            lock (_Searches) _Searches.Add(search);

            // Start the search
            RequestFolderContents(baseFolder, inventoryOwner, true, true, InventorySortOrder.ByName);
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
            List<InventoryBase> objects = new List<InventoryBase>();
            //List<InventoryFolder> folders = new List<InventoryFolder>();
            List<InventoryBase> contents = _Store.GetContents(baseFolder);

            foreach (InventoryBase inv in contents)
            {
                if (string.Compare(inv.Name, path[level], StringComparison.Ordinal) == 0)
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
            }

            return objects;
        }

        #endregion Find

        #region Move/Rename

        /// <summary>
        /// Move an inventory item or folder to a new location
        /// </summary>
        /// <param name="item">The <seealso cref="T:InventoryBase"/> item or folder to move</param>
        /// <param name="newParent">The <seealso cref="T:InventoryFolder"/> to move item or folder to</param>
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
        /// <param name="item">The <seealso cref="T:InventoryBase"/> item or folder to move</param>
        /// <param name="newParent">The <seealso cref="T:InventoryFolder"/> to move item or folder to</param>
        /// <param name="newName">The name to change the item or folder to</param>
        [Obsolete("Method broken with AIS3. Use Move(item, parent) instead.")]
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
        /// <param name="folderID">The source folders <seealso cref="UUID"/></param>
        /// <param name="newparentID">The destination folders <seealso cref="UUID"/></param>
        /// <param name="newName">The name to change the folder to</param>
        [Obsolete("Method broken with AIS3. Use MoveFolder(folder, parent) and UpdateFolderProperties(folder, parent, name, type) instead")]
        public void MoveFolder(UUID folderID, UUID newparentID, string newName)
        {
            UpdateFolderProperties(folderID, newparentID, newName, FolderType.None);
        }

        /// <summary>
        /// Update folder properties
        /// </summary>
        /// <param name="folderID"><seealso cref="UUID"/> of the folder to update</param>
        /// <param name="parentID">Sets folder's parent to <seealso cref="UUID"/></param>
        /// <param name="name">Folder name</param>
        /// <param name="type">Folder type</param>
        public void UpdateFolderProperties(UUID folderID, UUID parentID, string name, FolderType type)
        {
            InventoryFolder inv = null;
            lock (_Store)
            {
                if (_Store.Contains(folderID))
                {
                    inv = (InventoryFolder)Store[folderID];
                    inv.Name = name;
                    inv.ParentUUID = parentID;
                    inv.PreferredType = type;
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                if (inv != null)
                {
                    Client.AisClient.UpdateCategory(folderID, inv.GetOSD(), (success) =>
                        {
                            if (success)
                            {
                                lock (_Store)
                                {
                                    _Store.UpdateNodeFor(inv);
                                }
                            }
                        }
                        ).ConfigureAwait(false);
                }
            }
            else
            {
                lock (_Store)
                {
                    _Store.UpdateNodeFor(inv);
                }

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
                    Type = (sbyte) type
                };

                Client.Network.SendPacket(invFolder);
            }
        }

        /// <summary>
        /// Move a folder
        /// </summary>
        /// <param name="folderID">The source folders <seealso cref="UUID"/></param>
        /// <param name="newParentID">The destination folders <seealso cref="UUID"/></param>
        public void MoveFolder(UUID folderID, UUID newParentID)
        {
            lock (Store)
            {
                if (_Store.Contains(folderID))
                {
                    InventoryBase inv = Store[folderID];
                    inv.ParentUUID = newParentID;
                    _Store.UpdateNodeFor(inv);
                }
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
        /// <seealso cref="UUID"/> of the source as the key, and the 
        /// <seealso cref="UUID"/> of the destination as the value</param>
        public void MoveFolders(Dictionary<UUID, UUID> foldersNewParents)
        {
            // FIXME: Use two List<UUID> to stay consistent

            lock (Store)
            {
                foreach (KeyValuePair<UUID, UUID> entry in foldersNewParents)
                {
                    if (_Store.Contains(entry.Key))
                    {
                        InventoryBase inv = _Store[entry.Key];
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
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


            int index = 0;
            foreach (KeyValuePair<UUID, UUID> folder in foldersNewParents)
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
        /// <param name="itemID">The <seealso cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <seealso cref="UUID"/> of the destination folder</param>
        public void MoveItem(UUID itemID, UUID folderID)
        {
            MoveItem(itemID, folderID, String.Empty);
        }

        /// <summary>
        /// Move and rename an inventory item
        /// </summary>
        /// <param name="itemID">The <seealso cref="UUID"/> of the source item to move</param>
        /// <param name="folderID">The <seealso cref="UUID"/> of the destination folder</param>
        /// <param name="newName">The name to change the folder to</param>
        public void MoveItem(UUID itemID, UUID folderID, string newName)
        {
            lock (_Store)
            {
                if (_Store.Contains(itemID))
                {
                    InventoryBase inv = _Store[itemID];
                    if (!string.IsNullOrEmpty(newName))
                    {
                        inv.Name = newName;
                    }
                    inv.ParentUUID = folderID;
                    _Store.UpdateNodeFor(inv);
                }
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
        /// <seealso cref="UUID"/> of the source item as the key, and the 
        /// <seealso cref="UUID"/> of the destination folder as the value</param>
        public void MoveItems(Dictionary<UUID, UUID> itemsNewParents)
        {
            lock (_Store)
            {
                foreach (KeyValuePair<UUID, UUID> entry in itemsNewParents)
                {
                    if (_Store.Contains(entry.Key))
                    {
                        InventoryBase inv = _Store[entry.Key];
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
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
            
            int index = 0;
            foreach (KeyValuePair<UUID, UUID> entry in itemsNewParents)
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

        private void RemoveLocalUi(bool success, UUID folder)
        {
            if (success)
            {
                lock (_Store)
                {
                    if (!_Store.Contains(folder)) return;
                    foreach (InventoryBase obj in _Store.GetContents(folder))
                    {
                        _Store.RemoveNodeFor(obj);
                    }
                }
            }
        }

        /// <summary>
        /// Remove descendants of a folder
        /// </summary>
        /// <param name="folder">The <seealso cref="UUID"/> of the folder</param>
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
                    InventoryData = {FolderID = folder}
                };
                Client.Network.SendPacket(purge);
                RemoveLocalUi(true, folder);
            }
        }

        /// <summary>
        /// Remove a single item from inventory
        /// </summary>
        /// <param name="item">The <seealso cref="UUID"/> of the inventory item to remove</param>
        public void RemoveItem(UUID item)
        {
            List<UUID> items = new List<UUID>(1) {item};
            Remove(items, null);
        }

        /// <summary>
        /// Remove a folder from inventory
        /// </summary>
        /// <param name="folder">The <seealso cref="UUID"/> of the folder to remove</param>
        public void RemoveFolder(UUID folder)
        {
            List<UUID> folders = new List<UUID>(1) {folder};
            Remove(null, folders);
        }

        /// <summary>
        /// Remove multiple items or folders from inventory
        /// </summary>
        /// <param name="items">A List containing the <seealso cref="UUID"/>s of items to remove</param>
        /// <param name="folders">A List containing the <seealso cref="UUID"/>s of the folders to remove</param>
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
                rem.ItemData[0] = new RemoveInventoryObjectsPacket.ItemDataBlock {ItemID = UUID.Zero};
            }
            else
            {
                lock (_Store)
                {
                    rem.ItemData = new RemoveInventoryObjectsPacket.ItemDataBlock[items.Count];
                    for (int i = 0; i < items.Count; i++)
                    {
                        rem.ItemData[i] = new RemoveInventoryObjectsPacket.ItemDataBlock {ItemID = items[i]};

                        // Update local copy
                        if (_Store.Contains(items[i]))
                            _Store.RemoveNodeFor(Store[items[i]]);
                    }
                }
            }

            if (folders == null || folders.Count == 0)
            {
                // To indicate we want no folders removed:
                rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[1];
                rem.FolderData[0] = new RemoveInventoryObjectsPacket.FolderDataBlock {FolderID = UUID.Zero};
            }
            else
            {
                lock (_Store)
                {
                    rem.FolderData = new RemoveInventoryObjectsPacket.FolderDataBlock[folders.Count];
                    for (int i = 0; i < folders.Count; i++)
                    {
                        rem.FolderData[i] = new RemoveInventoryObjectsPacket.FolderDataBlock {FolderID = folders[i]};

                        // Update local copy
                        if (_Store.Contains(folders[i]))
                            _Store.RemoveNodeFor(Store[folders[i]]);
                    }
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
            UUID folderKey = UUID.Zero;

            var items = _Store.GetContents(_Store.RootFolder);
            foreach (var item in items)
            {
                InventoryFolder folder = item as InventoryFolder;
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
                List<UUID> remItems = new List<UUID>();
                List<UUID> remFolders = new List<UUID>();
                foreach (InventoryBase item in items)
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

                Remove(remItems, remFolders);
            }
        }
        #endregion Remove

        #region Create

        /// <summary>
        /// 
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
        /// 
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
        /// for new assets of the specified type. Use <code>FolderType.None</code>
        /// to create a normal folder, otherwise it will likely create a
        /// duplicate of an existing folder type</param>
        /// <returns>The UUID of the newly created folder</returns>
        /// <remarks>If you specify a preferred type of <code>AsseType.Folder</code>
        /// it will create a new root folder which may likely cause all sorts
        /// of strange problems</remarks>
        public UUID CreateFolder(UUID parentID, string name, FolderType preferredType)
        {
            UUID id = UUID.Random();

            // Assign a folder name if one is not already set
            if (String.IsNullOrEmpty(name))
            {
                if (preferredType >= FolderType.Texture && preferredType <= FolderType.MarkplaceStock)
                {
                    name = _NewFolderNames[(int)preferredType];
                }
                else
                {
                    name = "New Folder";
                }
                if (name == string.Empty)
                {
                    name = "New Folder";
                }
            }

            // Create the new folder locally
            InventoryFolder newFolder = new InventoryFolder(id)
            {
                Version = 1,
                DescendentCount = 0,
                ParentUUID = parentID,
                PreferredType = preferredType,
                Name = name,
                OwnerID = Client.Self.AgentID
            };

            // Update the local store
            try { _Store[newFolder.UUID] = newFolder; }
            catch (InventoryException ie) { Logger.Log(ie.Message, Helpers.LogLevel.Warning, Client, ie); }

            // Create the create folder packet and send it
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
            Permissions permissions = new Permissions
            {
                EveryoneMask = PermissionMask.None,
                GroupMask = PermissionMask.None,
                NextOwnerMask = PermissionMask.All
            };

            RequestCreateItemFromAsset(data, name, description, assetType, invType, folderID, permissions, callback);
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
        public void RequestCreateItemFromAsset(byte[] data, string name, string description, AssetType assetType,
            InventoryType invType, UUID folderID, Permissions permissions, ItemCreatedFromAssetCallback callback)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("NewFileAgentInventory capability is not currently available");

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("NewFileAgentInventory");

            if (request != null)
            {
                OSDMap query = new OSDMap
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
                request.OnComplete += CreateItemFromAssetResponse;
                request.UserData = new object[] { callback, data, Client.Settings.CAPS_TIMEOUT, query };

                request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("NewFileAgentInventory capability is not currently available");
            }
        }

        /// <summary>
        /// Creates inventory link to another inventory item or folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="bse">Inventory item or folder</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        public void CreateLink(UUID folderID, InventoryBase bse, ItemCreatedCallback callback)
        {
            if (bse is InventoryFolder folder)
            {
                CreateLink(folderID, folder, callback);
            }
            else if (bse is InventoryItem item)
            {
                CreateLink(folderID, item.UUID, item.Name, item.Description, AssetType.Link, item.InventoryType, UUID.Random(), callback);
            }
        }

        /// <summary>
        /// Creates inventory link to another inventory item
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="item">Original inventory item</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        public void CreateLink(UUID folderID, InventoryItem item, ItemCreatedCallback callback)
        {
            CreateLink(folderID, item.UUID, item.Name, item.Description, AssetType.Link, 
                item.InventoryType, UUID.Random(), callback);
        }

        /// <summary>
        /// Creates inventory link to another inventory folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="folder">Original inventory folder</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        public void CreateLink(UUID folderID, InventoryFolder folder, ItemCreatedCallback callback)
        {
            CreateLink(folderID, folder.UUID, folder.Name, "",
                AssetType.LinkFolder, InventoryType.Folder, UUID.Random(), callback);
        }

        /// <summary>
        /// Creates inventory link to another inventory item or folder
        /// </summary>
        /// <param name="folderID">Put newly created link in folder with this UUID</param>
        /// <param name="itemID">Original item's UUID</param>
        /// <param name="name">Name</param>
        /// <param name="description">Description</param>
        /// <param name="assetType">Asset Type</param>
        /// <param name="invType">Inventory Type</param>
        /// <param name="transactionID">Transaction UUID</param>
        /// <param name="callback">Method to call upon creation of the link</param>
        public void CreateLink(UUID folderID, UUID itemID, string name, string description, 
            AssetType assetType, InventoryType invType, UUID transactionID, ItemCreatedCallback callback)
        {
            if (Client.AisClient.IsAvailable)
            {
                OSDArray links = new OSDArray();
                OSDMap link = new OSDMap
                {
                    ["linked_id"] = OSD.FromUUID(itemID),
                    ["type"] = OSD.FromInteger((int)assetType),
                    ["inv_type"] = OSD.FromInteger((int)invType),
                    ["name"] = OSD.FromString(name),
                    ["desc"] = OSD.FromString(description)
                };
                links.Add(link);

                OSDMap newInventory = new OSDMap {{"links", links}};
                Client.AisClient.CreateInventory(folderID, newInventory, true, callback)
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
                    InventoryBlock = {CallbackID = RegisterItemCreatedCallback(callback)}
                };

                lock (_ItemInventoryTypeRequest)
                {
                    _ItemInventoryTypeRequest[create.InventoryBlock.CallbackID] = invType;
                }
                create.InventoryBlock.FolderID = folderID;
                create.InventoryBlock.TransactionID = transactionID;
                create.InventoryBlock.OldItemID = itemID;
                create.InventoryBlock.Type = (sbyte) assetType;
                create.InventoryBlock.InvType = (sbyte) invType;
                create.InventoryBlock.Name = Utils.StringToBytes(name);
                create.InventoryBlock.Description = Utils.StringToBytes(description);

                Client.Network.SendPacket(create);
            }
        }

        #endregion Create

        #region Copy

        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="newParent"></param>
        /// <param name="newName"></param>
        /// <param name="oldOwnerID"></param>
        /// <param name="callback"></param>
        public void RequestCopyItem(UUID item, UUID newParent, string newName, UUID oldOwnerID,
            ItemCopiedCallback callback)
        {
            List<UUID> items = new List<UUID>(1) {item};
            List<UUID> folders = new List<UUID>(1) {newParent};
            List<string> names = new List<string>(1) {newName};

            RequestCopyItems(items, folders, names, oldOwnerID, callback);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        /// <param name="targetFolders"></param>
        /// <param name="newNames"></param>
        /// <param name="oldOwnerID"></param>
        /// <param name="callback"></param>
        public void RequestCopyItems(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
            UUID oldOwnerID, ItemCopiedCallback callback)
        {
            if (items.Count != targetFolders.Count || (newNames != null && items.Count != newNames.Count))
                throw new ArgumentException("All list arguments must have an equal number of entries");

            uint callbackID = RegisterItemsCopiedCallback(callback);

            var copy = new CopyInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = new CopyInventoryItemPacket.InventoryDataBlock[items.Count]
            };

            for (int i = 0; i < items.Count; ++i)
            {
                copy.InventoryData[i] = new CopyInventoryItemPacket.InventoryDataBlock
                {
                    CallbackID = callbackID,
                    NewFolderID = targetFolders[i],
                    OldAgentID = oldOwnerID,
                    OldItemID = items[i],
                    NewName = !String.IsNullOrEmpty(newNames?[i])
                        ? Utils.StringToBytes(newNames[i])
                        : Utils.EmptyBytes
                };

            }

            Client.Network.SendPacket(copy);
        }

        /// <summary>
        /// Request a copy of an asset embedded within a notecard
        /// </summary>
        /// <param name="objectID">Usually UUID.Zero for copying an asset from a notecard</param>
        /// <param name="notecardID">UUID of the notecard to request an asset from</param>
        /// <param name="folderID">Target folder for asset to go to in your inventory</param>
        /// <param name="itemID">UUID of the embedded asset</param>
        /// <param name="callback">callback to run when item is copied to inventory</param>
        public void RequestCopyItemFromNotecard(UUID objectID, UUID notecardID, UUID folderID, UUID itemID, ItemCopiedCallback callback)
        {
            _ItemCopiedCallbacks[0] = callback; //Notecards always use callback ID 0

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("CopyInventoryFromNotecard");

            if (request != null)
            {
                var message = new CopyInventoryFromNotecardMessage
                {
                    CallbackID = 0,
                    FolderID = folderID,
                    ItemID = itemID,
                    NotecardID = notecardID,
                    ObjectID = objectID
                };

                request.BeginGetResponse(message.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
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
        /// 
        /// </summary>
        /// <param name="item"></param>
        public void RequestUpdateItem(InventoryItem item)
        {
            List<InventoryItem> items = new List<InventoryItem>(1) {item};

            RequestUpdateItems(items, UUID.Random());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        public void RequestUpdateItems(List<InventoryItem> items)
        {
            RequestUpdateItems(items, UUID.Random());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="items"></param>
        /// <param name="transactionID"></param>
        public void RequestUpdateItems(List<InventoryItem> items, UUID transactionID)
        {
            if (Client.AisClient.IsAvailable)
            {
                foreach (var item in items)
                {
                    OSDMap update = (OSDMap)item.GetOSD();
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

                for (int i = 0; i < items.Count; i++)
                {
                    InventoryItem item = items[i];

                    var block = new UpdateInventoryItemPacket.InventoryDataBlock
                    {
                        BaseMask = (uint) item.Permissions.BaseMask,
                        CRC = ItemCRC(item),
                        CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate),
                        CreatorID = item.CreatorID,
                        Description = Utils.StringToBytes(item.Description),
                        EveryoneMask = (uint) item.Permissions.EveryoneMask,
                        Flags = (uint) item.Flags,
                        FolderID = item.ParentUUID,
                        GroupID = item.GroupID,
                        GroupMask = (uint) item.Permissions.GroupMask,
                        GroupOwned = item.GroupOwned,
                        InvType = (sbyte) item.InventoryType,
                        ItemID = item.UUID,
                        Name = Utils.StringToBytes(item.Name),
                        NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                        OwnerID = item.OwnerID,
                        OwnerMask = (uint) item.Permissions.OwnerMask,
                        SalePrice = item.SalePrice,
                        SaleType = (byte) item.SaleType,
                        TransactionID = item.TransactionID,
                        Type = (sbyte) item.AssetType
                    };

                    update.InventoryData[i] = block;
                }

                Client.Network.SendPacket(update);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="notecardID"></param>
        /// <param name="callback"></param>
        public void RequestUploadNotecardAsset(byte[] data, UUID notecardID, InventoryUploadedAssetCallback callback)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("UpdateNotecardAgentInventory capability is not currently available");

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateNotecardAgentInventory");

            if (request != null)
            {
                OSDMap query = new OSDMap {{"item_id", OSD.FromUUID(notecardID)}};

                // Make the request
                request.OnComplete += UploadInventoryAssetResponse;
                request.UserData = new object[] { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID };
                request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("UpdateNotecardAgentInventory capability is not currently available");
            }
        }

        /// <summary>
        /// Save changes to notecard embedded in object contents
        /// </summary>
        /// <param name="data">Encoded notecard asset data</param>
        /// <param name="notecardID">Notecard UUID</param>
        /// <param name="taskID">Object's UUID</param>
        /// <param name="callback">Called upon finish of the upload with status information</param>
        public void RequestUpdateNotecardTask(byte[] data, UUID notecardID, UUID taskID, InventoryUploadedAssetCallback callback)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("UpdateNotecardTaskInventory capability is not currently available");

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateNotecardTaskInventory");

            if (request != null)
            {
                OSDMap query = new OSDMap
                {
                    {"item_id", OSD.FromUUID(notecardID)},
                    { "task_id", OSD.FromUUID(taskID)}
                };

                // Make the request
                request.OnComplete += UploadInventoryAssetResponse;
                request.UserData = new object[] { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID };
                request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("UpdateNotecardTaskInventory capability is not currently available");
            }
        }

        /// <summary>
        /// Upload new gesture asset for an inventory gesture item
        /// </summary>
        /// <param name="data">Encoded gesture asset</param>
        /// <param name="gestureID">Gesture inventory UUID</param>
        /// <param name="callback">Callback whick will be called when upload is complete</param>
        public void RequestUploadGestureAsset(byte[] data, UUID gestureID, InventoryUploadedAssetCallback callback)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("UpdateGestureAgentInventory capability is not currently available");

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateGestureAgentInventory");

            if (request != null)
            {
                OSDMap query = new OSDMap {{"item_id", OSD.FromUUID(gestureID)}};

                // Make the request
                request.OnComplete += UploadInventoryAssetResponse;
                request.UserData = new object[] { new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), gestureID };
                request.BeginGetResponse(query, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("UpdateGestureAgentInventory capability is not currently available");
            }
        }

        /// <summary>
        /// Update an existing script in an agents Inventory
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="callback"></param>
        public void RequestUpdateScriptAgentInventory(byte[] data, UUID itemID, bool mono, ScriptUpdatedCallback callback)
        {
            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateScriptAgent");

            if (request != null)
            {
                var msg = new UpdateScriptAgentRequestMessage
                {
                    ItemID = itemID,
                    Target = mono ? "mono" : "lsl2"
                };

                request.OnComplete += new CapsClient.CompleteCallback(UpdateScriptAgentInventoryResponse);
                request.UserData = new object[2] { new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID };
                request.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("UpdateScriptAgent capability is not currently available");
            }
        }

        /// <summary>
        /// Update an existing script in an task Inventory
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="taskID">UUID of the prim containting the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="running">if true, sets the script to running</param>
        /// <param name="callback"></param>
        public void RequestUpdateScriptTask(byte[] data, UUID itemID, UUID taskID, bool mono, bool running, ScriptUpdatedCallback callback)
        {
            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateScriptTask");

            if (request != null)
            {
                var msg = new UpdateScriptTaskUpdateMessage
                {
                    ItemID = itemID,
                    TaskID = taskID,
                    ScriptRunning = running,
                    Target = mono ? "mono" : "lsl2"
                };

                request.OnComplete += new CapsClient.CompleteCallback(UpdateScriptAgentInventoryResponse);
                request.UserData = new object[2] { new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID };
                request.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
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
            if (Store.Items.ContainsKey(item.UUID) && Store[item.UUID] is InventoryItem)
            {
                InventoryItem invItem = (InventoryItem)Store[item.UUID];
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
        /// <param name="destType">The type of destination from the <seealso cref="DeRezDestination"/> enum</param>
        /// <param name="destFolder">The destination inventory folders <seealso cref="UUID"/> -or- 
        /// if DeRezzing object to a tasks Inventory, the Tasks <seealso cref="UUID"/></param>
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
                    Destination = (byte) destType,
                    DestinationID = destFolder,
                    PacketCount = 1,
                    PacketNumber = 1,
                    TransactionID = transactionID
                },
                ObjectData = new DeRezObjectPacket.ObjectDataBlock[1]
            };


            take.ObjectData[0] = new DeRezObjectPacket.ObjectDataBlock {ObjectLocalID = objectLocalID};

            Client.Network.SendPacket(take);
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
        /// <param name="itemID">The <seealso cref="UUID"/> of the item to give</param>
        /// <param name="itemName">The name of the item</param>
        /// <param name="assetType">The type of the item from the <seealso cref="AssetType"/> enum</param>
        /// <param name="recipient">The <seealso cref="UUID"/> of the recipient</param>
        /// <param name="doEffect">true to generate a beameffect during transfer</param>
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
            if (Store.Items.ContainsKey(itemID) && Store[itemID] is InventoryItem)
            {
                InventoryItem invItem = (InventoryItem)Store[itemID];
                if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                {
                    Store.RemoveNodeFor(invItem);
                }
            }
        }

        /// <summary>
        /// Recurse inventory category and return folders and items. Does NOT contain parent folder being searched
        /// </summary>
        /// <param name="folder">Inventory category to recursively search</param>
        /// <param name="owner">Owner of folder</param>
        /// <param name="cats">reference to list of categories</param>
        /// <param name="items">reference to list of items</param>
        private void GetInventoryRecursive(UUID folderID, UUID owner, 
            ref List<InventoryFolder> cats, ref List<InventoryItem> items)
        {

            List<InventoryBase> contents = Client.Inventory.FolderContents(
                folderID, owner, true, true, InventorySortOrder.ByDate, 1000 * 15);

            foreach (var entry in contents)
            {
                switch (entry)
                {
                    case InventoryFolder folder:
                        cats.Add(folder);
                        GetInventoryRecursive(folder.UUID, owner, ref cats, ref items);
                        break;
                    case InventoryItem _:
                        items.Add(Client.Inventory.FetchItem(entry.UUID, owner, 1000 * 10));
                        break;
                    default: // shouldn't happen
                        Logger.Log("Retrieved inventory contents of invalid type", Helpers.LogLevel.Error);
                        break;
                }
            }
        }

        /// <summary>
        /// Give an inventory Folder with contents to another avatar
        /// </summary>
        /// <param name="folderID">The <seealso cref="UUID"/> of the Folder to give</param>
        /// <param name="folderName">The name of the folder</param>
        /// <param name="recipient">The <seealso cref="UUID"/> of the recipient</param>
        /// <param name="doEffect">true to generate a beameffect during transfer</param>
        public void GiveFolder(UUID folderID, string folderName, UUID recipient, bool doEffect)
        {
            var folders = new List<InventoryFolder>();
            var items = new List<InventoryItem>();
            
            GetInventoryRecursive(folderID, Client.Self.AgentID, ref folders, ref items);

            int total_contents = folders.Count + items.Count;

            // check for too many items.
            if (total_contents > MAX_GIVE_ITEMS)
            {
                Logger.Log("Cannot give more than 42 items in a single inventory transfer.", Helpers.LogLevel.Info);
                return;
            }
            if (!items.Any())
            {
                Logger.Log("No items to transfer.", Helpers.LogLevel.Info);
                return;
            }

            var bucket = new byte[17 * (total_contents + 1)];
            int offset = 0; // account for first byte

            //Add folders (parent folder first)
            bucket[offset++] = (byte)AssetType.Folder;
            Buffer.BlockCopy(folderID.GetBytes(), 0, bucket, offset, 16);
            offset += 16;
            foreach (var folder in folders)
            {
                bucket[offset++] = (byte)AssetType.Folder;
                Buffer.BlockCopy(folder.UUID.GetBytes(), 0, bucket, offset, 16);
                offset += 16;
            }

            //Add items to bucket after folders
            foreach (var item in items)
            {
                bucket[offset++] = (byte)item.AssetType;
                Buffer.BlockCopy(item.UUID.GetBytes(), 0, bucket, offset, 16);
                offset += 16;
            }

            Client.Self.InstantMessage(
                    Client.Self.Name,
                    recipient,
                    folderName,
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

            // Remove from store if items were no copy
            foreach (InventoryItem item in items)
            {
                if (Store.Items.ContainsKey(item.UUID) && Store[item.UUID] is InventoryItem)
                {
                    InventoryItem invItem = (InventoryItem)Store[item.UUID];
                    if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                    {
                        Store.RemoveNodeFor(invItem);
                    }
                }
            }
        }

        #endregion Rez/Give

        #region Task

        /// <summary>
        /// Copy or move an <see cref="InventoryItem"/> from agent inventory to a task (primitive) inventory
        /// </summary>
        /// <param name="objectLocalID">The target object</param>
        /// <param name="item">The item to copy or move from inventory</param>
        /// <returns></returns>
        /// <remarks>For items with copy permissions a copy of the item is placed in the tasks inventory,
        /// for no-copy items the object is moved to the tasks inventory</remarks>
        // DocTODO: what does the return UUID correlate to if anything?
        public UUID UpdateTaskInventory(uint objectLocalID, InventoryItem item)
        {
            UUID transactionID = UUID.Random();

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
        /// <param name="objectID">The tasks <seealso cref="UUID"/></param>
        /// <param name="objectLocalID">The tasks simulator local ID</param>
        /// <param name="timeoutMS">milliseconds to wait for reply from simulator</param>
        /// <returns>A list containing the inventory items inside the task or null
        /// if a timeout occurs</returns>
        /// <remarks>This request blocks until the response from the simulator arrives 
        /// or timeoutMS is exceeded</remarks>
        public List<InventoryBase> GetTaskInventory(UUID objectID, UInt32 objectLocalID, Int32 timeoutMS)
        {
            String filename = null;
            AutoResetEvent taskReplyEvent = new AutoResetEvent(false);

            EventHandler<TaskInventoryReplyEventArgs> callback =
                delegate(object sender, TaskInventoryReplyEventArgs e)
                {
                    if (e.ItemID == objectID)
                    {
                        filename = e.AssetFilename;
                        taskReplyEvent.Set();
                    }
                };

            TaskInventoryReply += callback;

            RequestTaskInventory(objectLocalID);

            if (taskReplyEvent.WaitOne(timeoutMS, false))
            {
                TaskInventoryReply -= callback;

                if (!String.IsNullOrEmpty(filename))
                {
                    byte[] assetData = null;
                    ulong xferID = 0;
                    AutoResetEvent taskDownloadEvent = new AutoResetEvent(false);

                    EventHandler<XferReceivedEventArgs> xferCallback =
                        delegate(object sender, XferReceivedEventArgs e)
                        {
                            if (e.Xfer.XferID == xferID)
                            {
                                assetData = e.Xfer.AssetData;
                                taskDownloadEvent.Set();
                            }
                        };

                    Client.Assets.XferReceived += xferCallback;

                    // Start the actual asset xfer
                    xferID = Client.Assets.RequestAssetXfer(filename, true, false, UUID.Zero, AssetType.Unknown, true);

                    if (taskDownloadEvent.WaitOne(timeoutMS, false))
                    {
                        Client.Assets.XferReceived -= xferCallback;

                        String taskList = Utils.BytesToString(assetData);
                        return ParseTaskInventory(taskList);
                    }
                    else
                    {
                        Logger.Log("Timed out waiting for task inventory download for " + filename, Helpers.LogLevel.Warning, Client);
                        Client.Assets.XferReceived -= xferCallback;
                        return null;
                    }
                }
                else
                {
                    Logger.DebugLog("Task is empty for " + objectLocalID, Client);
                    return new List<InventoryBase>(0);
                }
            }
            else
            {
                Logger.Log("Timed out waiting for task inventory reply for " + objectLocalID, Helpers.LogLevel.Warning, Client);
                TaskInventoryReply -= callback;
                return null;
            }
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory from the 
        /// current simulator
        /// </summary>
        /// <param name="objectLocalID">The LocalID of the object</param>
        /// <seealso cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID)
        {
            RequestTaskInventory(objectLocalID, Client.Network.CurrentSim);
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <param name="simulator">A reference to the simulator object that contains the object</param>
        /// <seealso cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID, Simulator simulator)
        {
            var request = new RequestTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = {LocalID = objectLocalID}
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
        /// the <seealso cref="TaskInventoryReply"/> event</remarks>
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
        /// <param name="item">An <seealso cref="InventoryItem"/> which represents a script object from the agents inventory</param>
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
        // DocTODO: what does the return UUID correlate to if anything?
        public UUID CopyScriptToTask(uint objectLocalID, InventoryItem item, bool enableScript)
        {
            UUID transactionID = UUID.Random();

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
        /// <seealso cref="ScriptRunningReply"/>
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
            SetScriptRunningPacket request = new SetScriptRunningPacket
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

        private uint RegisterItemCreatedCallback(ItemCreatedCallback callback)
        {
            lock (_CallbacksLock)
            {
                if (_CallbackPos == UInt32.MaxValue)
                    _CallbackPos = 0;

                _CallbackPos++;

                if (_ItemCreatedCallbacks.ContainsKey(_CallbackPos))
                    Logger.Log("Overwriting an existing ItemCreatedCallback", Helpers.LogLevel.Warning, Client);

                _ItemCreatedCallbacks[_CallbackPos] = callback;

                return _CallbackPos;
            }
        }

        private uint RegisterItemsCopiedCallback(ItemCopiedCallback callback)
        {
            lock (_CallbacksLock)
            {
                if (_CallbackPos == UInt32.MaxValue)
                    _CallbackPos = 0;

                _CallbackPos++;

                if (_ItemCopiedCallbacks.ContainsKey(_CallbackPos))
                    Logger.Log("Overwriting an existing ItemsCopiedCallback", Helpers.LogLevel.Warning, Client);

                _ItemCopiedCallbacks[_CallbackPos] = callback;

                return _CallbackPos;
            }
        }

        /// <summary>
        /// Create a CRC from an InventoryItem
        /// </summary>
        /// <param name="iitem">The source InventoryItem</param>
        /// <returns>A uint representing the source InventoryItem as a CRC</returns>
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
        /// Wrapper for creating a new <seealso cref="InventoryItem"/> object
        /// </summary>
        /// <param name="type">The type of item from the <seealso cref="InventoryType"/> enum</param>
        /// <param name="id">The <seealso cref="UUID"/> of the newly created object</param>
        /// <returns>An <seealso cref="InventoryItem"/> object with the type and id passed</returns>
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
                default: return new InventoryItem(type, id);
            }
        }

        public InventoryItem SafeCreateInventoryItem(InventoryType InvType, UUID ItemID)
        {
            InventoryItem ret = null;

            if (_Store.Contains(ItemID))
                ret = _Store[ItemID] as InventoryItem;

            return ret ?? (ret = CreateInventoryItem(InvType, ItemID));
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
                int sep = line.IndexOf(' ');
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
                value = String.Empty;
                return true;
            }

            key = null;
            value = null;
            return false;
        }

        /// <summary>
        /// Parse the results of a RequestTaskInventory() response
        /// </summary>
        /// <param name="taskData">A string which contains the data from the task reply</param>
        /// <returns>A List containing the items contained within the tasks inventory</returns>
        public static List<InventoryBase> ParseTaskInventory(string taskData)
        {
            List<InventoryBase> items = new List<InventoryBase>();
            int lineNum = 0;
            string[] lines = taskData.Replace("\r\n", "\n").Split('\n');

            while (lineNum < lines.Length)
            {
                string key, value;
                if (ParseLine(lines[lineNum++], out key, out value))
                {
                    if (key == "inv_object")
                    {
                        #region inv_object

                        // In practice this appears to only be used for folders
                        UUID itemID = UUID.Zero;
                        UUID parentID = UUID.Zero;
                        string name = String.Empty;
                        AssetType assetType = AssetType.Unknown;

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
                            InventoryFolder folder = new InventoryFolder(itemID)
                            {
                                Name = name,
                                ParentUUID = parentID
                            };

                            items.Add(folder);
                        }
                        else
                        {
                            InventoryItem item = new InventoryItem(itemID)
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
                        UUID itemID = UUID.Zero;
                        UUID assetID = UUID.Zero;
                        UUID parentID = UUID.Zero;
                        UUID creatorID = UUID.Zero;
                        UUID ownerID = UUID.Zero;
                        UUID lastOwnerID = UUID.Zero;
                        UUID groupID = UUID.Zero;
                        bool groupOwned = false;
                        string name = String.Empty;
                        string desc = String.Empty;
                        AssetType assetType = AssetType.Unknown;
                        InventoryType inventoryType = InventoryType.Unknown;
                        DateTime creationDate = Utils.Epoch;
                        uint flags = 0;
                        Permissions perms = Permissions.NoPermissions;
                        SaleType saleType = SaleType.Not;
                        int salePrice = 0;

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
                                                if (UInt32.TryParse(value, out val))
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
                                                Int32.TryParse(value, out salePrice);
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
                                    UInt32.TryParse(value, out flags);
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
                                    if (UInt32.TryParse(value, out timestamp))
                                        creationDate = Utils.UnixTimeToDateTime(timestamp);
                                    else
                                        Logger.Log("Failed to parse creation_date " + value, Helpers.LogLevel.Warning);
                                }
                            }
                        }

                        InventoryItem item = CreateInventoryItem(inventoryType, itemID);
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
                        Logger.Log("Unrecognized token " + key + " in: " + Environment.NewLine + taskData,
                            Helpers.LogLevel.Error);
                    }
                }
            }

            return items;
        }

        #endregion Helper Functions

        #region Internal Callbacks

        void Self_IM(object sender, InstantMessageEventArgs e)
        {
            // TODO: MainAvatar.InstantMessageDialog.GroupNotice can also be an inventory offer, should we
            // handle it here?

            if (m_InventoryObjectOffered != null &&
                (e.IM.Dialog == InstantMessageDialog.InventoryOffered
                || e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered))
            {
                AssetType type = AssetType.Unknown;
                UUID objectID = UUID.Zero;
                bool fromTask = false;

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
                UUID destinationFolderID = FindFolderForType(type);

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

                    InventoryObjectOfferedEventArgs args = new InventoryObjectOfferedEventArgs(e.IM, type, objectID, fromTask, destinationFolderID);

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

                    Client.Network.SendPacket(imp, e.Simulator);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }
        }

        private void CreateItemFromAssetResponse(CapsClient client, OSD result, Exception error)
        {
            object[] args = (object[])client.UserData;
            ItemCreatedFromAssetCallback callback = (ItemCreatedFromAssetCallback)args[0];
            byte[] itemData = (byte[])args[1];
            int millisecondsTimeout = (int)args[2];
            OSDMap request = (OSDMap)args[3];

            if (result == null)
            {
                try { callback(false, error.Message, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                return;
            }

            if (result.Type == OSDType.Unknown)
            {
                try
                {
                    callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }

            OSDMap contents = (OSDMap)result;

            string status = contents["state"].AsString().ToLower();

            if (status == "upload")
            {
                string uploadURL = contents["uploader"].AsString();

                Logger.DebugLog("CreateItemFromAsset: uploading to " + uploadURL);

                // This makes the assumption that all uploads go to CurrentSim, to avoid
                // the problem of HttpRequestState not knowing anything about simulators
                CapsClient upload = new CapsClient(new Uri(uploadURL), "CreateItemFromAsset");
                upload.OnComplete += CreateItemFromAssetResponse;
                upload.UserData = new object[] { callback, itemData, millisecondsTimeout, request };
                upload.BeginGetResponse(itemData, "application/octet-stream", millisecondsTimeout);
            }
            else if (status == "complete")
            {
                Logger.DebugLog("CreateItemFromAsset: completed");

                if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
                {
                    // Request full update on the item in order to update the local store
                    RequestFetchInventory(contents["new_inventory_item"].AsUUID(), Client.Self.AgentID);

                    try { callback(true, String.Empty, contents["new_inventory_item"].AsUUID(), contents["new_asset"].AsUUID()); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
                else
                {
                    try { callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero); }
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


        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason, LoginResponseData replyData)
        {
            if (!loginSuccess) return;

            // Initialize the store here so we know who owns it:
            _Store = new Inventory(Client, this, Client.Self.AgentID);
            Logger.DebugLog("Setting InventoryRoot to " + replyData.InventoryRoot.ToString(), Client);
            InventoryFolder rootFolder = new InventoryFolder(replyData.InventoryRoot)
            {
                Name = String.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.RootFolder = rootFolder;

            foreach (InventoryFolder folder in replyData.InventorySkeleton)
                _Store.UpdateNodeFor(folder);

            InventoryFolder libraryRootFolder = new InventoryFolder(replyData.LibraryRoot)
            {
                Name = string.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.LibraryFolder = libraryRootFolder;

            foreach (InventoryFolder folder in replyData.LibrarySkeleton)
                _Store.UpdateNodeFor(folder);
        }

        private void UploadInventoryAssetResponse(CapsClient client, OSD result, Exception error)
        {
            OSDMap contents = result as OSDMap;
            KeyValuePair<InventoryUploadedAssetCallback, byte[]> kvp = (KeyValuePair<InventoryUploadedAssetCallback, byte[]>)(((object[])client.UserData)[0]);
            InventoryUploadedAssetCallback callback = kvp.Key;
            byte[] itemData = (byte[])kvp.Value;

            if (error == null && contents != null)
            {
                string status = contents["state"].AsString();

                if (status == "upload")
                {
                    Uri uploadURL = contents["uploader"].AsUri();

                    if (uploadURL != null)
                    {
                        // This makes the assumption that all uploads go to CurrentSim, to avoid
                        // the problem of HttpRequestState not knowing anything about simulators
                        CapsClient upload = new CapsClient(uploadURL, "UploadItemResponse");
                        upload.OnComplete += UploadInventoryAssetResponse;
                        upload.UserData = new object[2] { kvp, (UUID)(((object[])client.UserData)[1]) };
                        upload.BeginGetResponse(itemData, "application/octet-stream", Client.Settings.CAPS_TIMEOUT);
                    }
                    else
                    {
                        try { callback(false, "Missing uploader URL", UUID.Zero, UUID.Zero); }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                }
                else if (status == "complete")
                {
                    if (contents.ContainsKey("new_asset"))
                    {
                        // Request full item update so we keep store in sync
                        RequestFetchInventory((UUID)(((object[])client.UserData)[1]), contents["new_asset"].AsUUID());

                        try { callback(true, String.Empty, (UUID)(((object[])client.UserData)[1]), contents["new_asset"].AsUUID()); }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                    else
                    {
                        try { callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero); }
                        catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                    }
                }
                else
                {
                    try { callback(false, status, UUID.Zero, UUID.Zero); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
            }
            else
            {
                string message = "Unrecognized or empty response";

                if (error != null)
                {
                    if (error is WebException exception)
                        message = ((HttpWebResponse)exception.Response).StatusDescription;

                    if (message == null || message == "None")
                        message = error.Message;
                }

                try { callback(false, message, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }
        }

        private void UpdateScriptAgentInventoryResponse(CapsClient client, OSD result, Exception error)
        {
            KeyValuePair<ScriptUpdatedCallback, byte[]> kvp = (KeyValuePair<ScriptUpdatedCallback, byte[]>)(((object[])client.UserData)[0]);
            ScriptUpdatedCallback callback = kvp.Key;
            byte[] itemData = (byte[])kvp.Value;

            if (result == null)
            {
                try { callback(false, error.Message, false, null, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                return;
            }

            OSDMap contents = (OSDMap)result;

            string status = contents["state"].AsString();
            if (status == "upload")
            {
                string uploadURL = contents["uploader"].AsString();

                CapsClient upload = new CapsClient(new Uri(uploadURL), "ScriptAgentInventoryResponse");
                upload.OnComplete += UpdateScriptAgentInventoryResponse;
                upload.UserData = new object[2] { kvp, (UUID)(((object[])client.UserData)[1]) };
                upload.BeginGetResponse(itemData, "application/octet-stream", Client.Settings.CAPS_TIMEOUT);
            }
            else if (status == "complete" && callback != null)
            {
                if (contents.ContainsKey("new_asset"))
                {
                    // Request full item update so we keep store in sync
                    RequestFetchInventory((UUID)(((object[])client.UserData)[1]), contents["new_asset"].AsUUID());


                    try
                    {
                        List<string> compileErrors = null;

                        if (contents.ContainsKey("errors"))
                        {
                            OSDArray errors = (OSDArray)contents["errors"];
                            compileErrors = new List<string>(errors.Count);
                            compileErrors.AddRange(errors.Select(t => t.AsString()));
                        }

                        callback(true,
                            status,
                            contents["compiled"].AsBoolean(),
                            compileErrors,
                            (UUID)(((object[])client.UserData)[1]),
                            contents["new_asset"].AsUUID());
                    }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
                else
                {
                    try { callback(false, "Failed to parse asset UUID", false, null, UUID.Zero, UUID.Zero); }
                    catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
                }
            }
            else if (callback != null)
            {
                try { callback(false, status, false, null, UUID.Zero, UUID.Zero); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
            }
        }
        #endregion Internal Handlers

        #region Packet Handlers

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void SaveAssetIntoInventoryHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_SaveAssetToInventory != null)
            {
                Packet packet = e.Packet;

                SaveAssetIntoInventoryPacket save = (SaveAssetIntoInventoryPacket)packet;
                OnSaveAssetToInventory(new SaveAssetToInventoryEventArgs(save.InventoryData.ItemID, save.InventoryData.NewAssetID));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void InventoryDescendentsHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            InventoryDescendentsPacket reply = (InventoryDescendentsPacket)packet;

            if (reply.AgentData.Descendents > 0)
            {
                // InventoryDescendantsReply sends a null folder if the parent doesnt contain any folders
                if (reply.FolderData[0].FolderID != UUID.Zero)
                {
                    // Iterate folders in this packet
                    foreach (InventoryDescendentsPacket.FolderDataBlock data in reply.FolderData)
                    {
                        // If folder already exists then ignore, we assume the version cache
                        // logic is working and if the folder is stale then it should not be present.
                        if (!_Store.Contains(data.FolderID))
                        {
                            InventoryFolder folder = new InventoryFolder(data.FolderID)
                            {
                                ParentUUID = data.ParentID,
                                Name = Utils.BytesToString(data.Name),
                                PreferredType = (FolderType) data.Type,
                                OwnerID = reply.AgentData.OwnerID
                            };

                            _Store[folder.UUID] = folder;
                        }
                    }
                }

                // InventoryDescendantsReply sends a null item if the parent doesnt contain any items.
                if (reply.ItemData[0].ItemID != UUID.Zero)
                {
                    // Iterate items in this packet
                    foreach (InventoryDescendentsPacket.ItemDataBlock data in reply.ItemData)
                    {
                        if (data.ItemID != UUID.Zero)
                        {
                            InventoryItem item;
                            /* 
                             * Objects that have been attached in-world prior to being stored on the 
                             * asset server are stored with the InventoryType of 0 (Texture) 
                             * instead of 17 (Attachment) 
                             * 
                             * This corrects that behavior by forcing Object Asset types that have an 
                             * invalid InventoryType with the proper InventoryType of Attachment.
                             */
                            if ((AssetType)data.Type == AssetType.Object
                                && (InventoryType)data.InvType == InventoryType.Texture)
                            {
                                item = CreateInventoryItem(InventoryType.Attachment, data.ItemID);
                                item.InventoryType = InventoryType.Attachment;
                            }
                            else
                            {
                                item = CreateInventoryItem((InventoryType)data.InvType, data.ItemID);
                                item.InventoryType = (InventoryType)data.InvType;
                            }

                            item.ParentUUID = data.FolderID;
                            item.CreatorID = data.CreatorID;
                            item.AssetType = (AssetType)data.Type;
                            item.AssetUUID = data.AssetID;
                            item.CreationDate = Utils.UnixTimeToDateTime((uint)data.CreationDate);
                            item.Description = Utils.BytesToString(data.Description);
                            item.Flags = data.Flags;
                            item.Name = Utils.BytesToString(data.Name);
                            item.GroupID = data.GroupID;
                            item.GroupOwned = data.GroupOwned;
                            item.Permissions = new Permissions(
                                data.BaseMask,
                                data.EveryoneMask,
                                data.GroupMask,
                                data.NextOwnerMask,
                                data.OwnerMask);
                            item.SalePrice = data.SalePrice;
                            item.SaleType = (SaleType)data.SaleType;
                            item.OwnerID = reply.AgentData.OwnerID;

                            _Store[item.UUID] = item;
                        }
                    }
                }
            }

            InventoryFolder parentFolder = null;

            if (_Store.Contains(reply.AgentData.FolderID) &&
                _Store[reply.AgentData.FolderID] is InventoryFolder)
            {
                parentFolder = (InventoryFolder) _Store[reply.AgentData.FolderID];
            }
            else
            {
                Logger.Log("Don't have a reference to FolderID " + reply.AgentData.FolderID.ToString() +
                    " or it is not a folder", Helpers.LogLevel.Error, Client);
                return;
            }

            if (reply.AgentData.Version < parentFolder.Version)
            {
                Logger.Log("Got an outdated InventoryDescendents packet for folder " + parentFolder.Name +
                    ", this version = " + reply.AgentData.Version + ", latest version = " + parentFolder.Version,
                    Helpers.LogLevel.Warning, Client);
                return;
            }

            parentFolder.Version = reply.AgentData.Version;
            // FIXME: reply.AgentData.Descendants is not parentFolder.DescendentCount if we didn't 
            // request items and folders
            parentFolder.DescendentCount = reply.AgentData.Descendents;
            _Store.GetNodeFor(reply.AgentData.FolderID).NeedsUpdate = false;

            #region FindObjectsByPath Handling

            lock (_Searches)
            {
                if (_Searches.Count > 0)
                {
                    StartSearch:
                    // Iterate over all of the outstanding searches
                    for (int i = 0; i < _Searches.Count; i++)
                    {
                        InventorySearch search = _Searches[i];
                        List<InventoryBase> folderContents = _Store.GetContents(search.Folder);

                        // Iterate over all of the inventory objects in the base search folder
                        for (int j = 0; j < folderContents.Count; j++)
                        {
                            // Check if this inventory object matches the current path node
                            if (folderContents[j].Name == search.Path[search.Level])
                            {
                                if (search.Level == search.Path.Length - 1)
                                {
                                    Logger.DebugLog("Finished path search of " + String.Join("/", search.Path), Client);

                                    // This is the last node in the path, fire the callback and clean up
                                    if (m_FindObjectByPathReply != null)
                                    {
                                        OnFindObjectByPathReply(new FindObjectByPathReplyEventArgs(String.Join("/", search.Path),
                                            folderContents[j].UUID));
                                    }

                                    // Remove this entry and restart the loop since we are changing the collection size
                                    _Searches.RemoveAt(i);
                                    goto StartSearch;
                                }
                                else
                                {
                                    // We found a match but it is not the end of the path, request the next level
                                    Logger.DebugLog(
                                        $"Matched level {search.Level}/{search.Path.Length - 1} in a path search of {String.Join("/", search.Path)}", Client);

                                    search.Folder = folderContents[j].UUID;
                                    search.Level++;
                                    _Searches[i] = search;

                                    RequestFolderContents(search.Folder, search.Owner, true, true,
                                        InventorySortOrder.ByName);
                                }
                            }
                        }
                    }
                }
            }

            #endregion FindObjectsByPath Handling

            // Callback for inventory folder contents being updated
            OnFolderUpdated(new FolderUpdatedEventArgs(parentFolder.UUID, true));
        }

        /// <summary>
        /// UpdateCreateInventoryItem packets are received when a new inventory item 
        /// is created. This may occur when an object that's rezzed in world is
        /// taken into inventory, when an item is created using the CreateInventoryItem
        /// packet, or when an object is purchased
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void UpdateCreateInventoryItemHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            if (!(packet is UpdateCreateInventoryItemPacket reply)) return;

            foreach (UpdateCreateInventoryItemPacket.InventoryDataBlock dataBlock in reply.InventoryData)
            {
                if (dataBlock.InvType == (sbyte) InventoryType.Folder)
                {
                    Logger.Log(
                        "Received InventoryFolder in an UpdateCreateInventoryItem packet, this should not happen!",
                        Helpers.LogLevel.Error, Client);
                    continue;
                }

                InventoryItem item = CreateInventoryItem((InventoryType) dataBlock.InvType, dataBlock.ItemID);
                item.AssetType = (AssetType) dataBlock.Type;
                item.AssetUUID = dataBlock.AssetID;
                item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
                item.CreatorID = dataBlock.CreatorID;
                item.Description = Utils.BytesToString(dataBlock.Description);
                item.Flags = dataBlock.Flags;
                item.GroupID = dataBlock.GroupID;
                item.GroupOwned = dataBlock.GroupOwned;
                item.Name = Utils.BytesToString(dataBlock.Name);
                item.OwnerID = dataBlock.OwnerID;
                item.ParentUUID = dataBlock.FolderID;
                item.Permissions = new Permissions(
                    dataBlock.BaseMask,
                    dataBlock.EveryoneMask,
                    dataBlock.GroupMask,
                    dataBlock.NextOwnerMask,
                    dataBlock.OwnerMask);
                item.SalePrice = dataBlock.SalePrice;
                item.SaleType = (SaleType) dataBlock.SaleType;

                /* 
                     * When attaching new objects, an UpdateCreateInventoryItem packet will be
                     * returned by the server that has a FolderID/ParentUUID of zero. It is up
                     * to the client to make sure that the item gets a good folder, otherwise
                     * it will end up inaccesible in inventory.
                     */
                if (item.ParentUUID == UUID.Zero)
                {
                    // assign default folder for type
                    item.ParentUUID = FindFolderForType(item.AssetType);

                    Logger.Log(
                        "Received an item through UpdateCreateInventoryItem with no parent folder, assigning to folder " +
                        item.ParentUUID, Helpers.LogLevel.Info);

                    // send update to the sim
                    RequestUpdateItem(item);
                }

                // Update the local copy
                _Store[item.UUID] = item;

                // Look for an "item created" callback
                ItemCreatedCallback createdCallback;
                if (_ItemCreatedCallbacks.TryGetValue(dataBlock.CallbackID, out createdCallback))
                {
                    _ItemCreatedCallbacks.Remove(dataBlock.CallbackID);

                    try
                    {
                        createdCallback(true, item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                    }
                }

                // TODO: Is this callback even triggered when items are copied?
                // Look for an "item copied" callback
                ItemCopiedCallback copyCallback;
                if (_ItemCopiedCallbacks.TryGetValue(dataBlock.CallbackID, out copyCallback))
                {
                    _ItemCopiedCallbacks.Remove(dataBlock.CallbackID);

                    try
                    {
                        copyCallback(item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                    }
                }

                //This is triggered when an item is received from a task
                if (m_TaskItemReceived != null)
                {
                    OnTaskItemReceived(new TaskItemReceivedEventArgs(item.UUID, dataBlock.FolderID,
                        item.CreatorID, item.AssetUUID, item.InventoryType));
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MoveInventoryItemHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            var move = (MoveInventoryItemPacket)packet;

            foreach (MoveInventoryItemPacket.InventoryDataBlock data in move.InventoryData)
            {
                // FIXME: Do something here
                string newName = Utils.BytesToString(data.NewName);

                Logger.Log(
                    $"MoveInventoryItemHandler: Item {data.ItemID.ToString()} is moving to Folder {data.FolderID.ToString()} with new name \"{newName}\"." +
                    " Someone write this function!", 
                    Helpers.LogLevel.Warning, Client);
            }
        }

        protected void BulkUpdateInventoryCapHandler(string capsKey, Interfaces.IMessage message, Simulator simulator)
        {
            var msg = (BulkUpdateInventoryMessage)message;

            foreach (BulkUpdateInventoryMessage.FolderDataInfo newFolder in msg.FolderData)
            {
                if (newFolder.FolderID == UUID.Zero) continue;

                InventoryFolder folder;
                if (!_Store.Contains(newFolder.FolderID))
                {
                    folder = new InventoryFolder(newFolder.FolderID);
                }
                else
                {
                    folder = (InventoryFolder)_Store[newFolder.FolderID];
                }

                folder.Name = newFolder.Name;
                folder.ParentUUID = newFolder.ParentID;
                folder.PreferredType = newFolder.Type;
                _Store[folder.UUID] = folder;
            }

            foreach (BulkUpdateInventoryMessage.ItemDataInfo newItem in msg.ItemData)
            {
                if (newItem.ItemID == UUID.Zero) continue;
                InventoryType invType = newItem.InvType;

                lock (_ItemInventoryTypeRequest)
                {
                    InventoryType storedType = 0;
                    if (_ItemInventoryTypeRequest.TryGetValue(newItem.CallbackID, out storedType))
                    {
                        _ItemInventoryTypeRequest.Remove(newItem.CallbackID);
                        invType = storedType;
                    }
                }
                InventoryItem item = SafeCreateInventoryItem(invType, newItem.ItemID);

                item.AssetType = newItem.Type;
                item.AssetUUID = newItem.AssetID;
                item.CreationDate = newItem.CreationDate;
                item.CreatorID = newItem.CreatorID;
                item.Description = newItem.Description;
                item.Flags = newItem.Flags;
                item.GroupID = newItem.GroupID;
                item.GroupOwned = newItem.GroupOwned;
                item.Name = newItem.Name;
                item.OwnerID = newItem.OwnerID;
                item.ParentUUID = newItem.FolderID;
                item.Permissions.BaseMask = newItem.BaseMask;
                item.Permissions.EveryoneMask = newItem.EveryoneMask;
                item.Permissions.GroupMask = newItem.GroupMask;
                item.Permissions.NextOwnerMask = newItem.NextOwnerMask;
                item.Permissions.OwnerMask = newItem.OwnerMask;
                item.SalePrice = newItem.SalePrice;
                item.SaleType = newItem.SaleType;

                _Store[item.UUID] = item;

                // Look for an "item created" callback
                ItemCreatedCallback callback;
                if (_ItemCreatedCallbacks.TryGetValue(newItem.CallbackID, out callback))
                {
                    _ItemCreatedCallbacks.Remove(newItem.CallbackID);

                    try { callback(true, item); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                }

                // Look for an "item copied" callback
                ItemCopiedCallback copyCallback;
                if (_ItemCopiedCallbacks.TryGetValue(newItem.CallbackID, out copyCallback))
                {
                    _ItemCopiedCallbacks.Remove(newItem.CallbackID);

                    try { copyCallback(item); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                }

            }

        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void BulkUpdateInventoryHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            if (!(packet is BulkUpdateInventoryPacket update)) return;

            if (update.FolderData.Length > 0 && update.FolderData[0].FolderID != UUID.Zero)
            {
                foreach (BulkUpdateInventoryPacket.FolderDataBlock dataBlock in update.FolderData)
                {
                    InventoryFolder folder;
                    if (!_Store.Contains(dataBlock.FolderID))
                    {
                        folder = new InventoryFolder(dataBlock.FolderID);
                    }
                    else
                    {
                        folder = (InventoryFolder) _Store[dataBlock.FolderID];
                    }

                    if (dataBlock.Name != null)
                    {
                        folder.Name = Utils.BytesToString(dataBlock.Name);
                    }
                    folder.OwnerID = update.AgentData.AgentID;
                    folder.ParentUUID = dataBlock.ParentID;
                    _Store[folder.UUID] = folder;
                }
            }

            if (update.ItemData.Length > 0 && update.ItemData[0].ItemID != UUID.Zero)
            {
                foreach (BulkUpdateInventoryPacket.ItemDataBlock dataBlock in update.ItemData)
                {
                    InventoryItem item =
                        SafeCreateInventoryItem((InventoryType) dataBlock.InvType, dataBlock.ItemID);

                    item.AssetType = (AssetType) dataBlock.Type;
                    if (dataBlock.AssetID != UUID.Zero) item.AssetUUID = dataBlock.AssetID;
                    item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
                    item.CreatorID = dataBlock.CreatorID;
                    item.Description = Utils.BytesToString(dataBlock.Description);
                    item.Flags = dataBlock.Flags;
                    item.GroupID = dataBlock.GroupID;
                    item.GroupOwned = dataBlock.GroupOwned;
                    item.Name = Utils.BytesToString(dataBlock.Name);
                    item.OwnerID = dataBlock.OwnerID;
                    item.ParentUUID = dataBlock.FolderID;
                    item.Permissions = new Permissions(
                        dataBlock.BaseMask,
                        dataBlock.EveryoneMask,
                        dataBlock.GroupMask,
                        dataBlock.NextOwnerMask,
                        dataBlock.OwnerMask);
                    item.SalePrice = dataBlock.SalePrice;
                    item.SaleType = (SaleType) dataBlock.SaleType;

                    _Store[item.UUID] = item;

                    // Look for an "item created" callback
                    ItemCreatedCallback callback;
                    if (_ItemCreatedCallbacks.TryGetValue(dataBlock.CallbackID, out callback))
                    {
                        _ItemCreatedCallbacks.Remove(dataBlock.CallbackID);

                        try
                        {
                            callback(true, item);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                        }
                    }

                    // Look for an "item copied" callback
                    ItemCopiedCallback copyCallback;
                    if (_ItemCopiedCallbacks.TryGetValue(dataBlock.CallbackID, out copyCallback))
                    {
                        _ItemCopiedCallbacks.Remove(dataBlock.CallbackID);

                        try
                        {
                            copyCallback(item);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                        }
                    }
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void FetchInventoryReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            if (!(packet is FetchInventoryReplyPacket reply)) return;

            foreach (FetchInventoryReplyPacket.InventoryDataBlock dataBlock in reply.InventoryData)
            {
                if (dataBlock.InvType == (sbyte) InventoryType.Folder)
                {
                    Logger.Log("Received FetchInventoryReply for an inventory folder, this should not happen!",
                        Helpers.LogLevel.Error, Client);
                    continue;
                }

                InventoryItem item = CreateInventoryItem((InventoryType) dataBlock.InvType, dataBlock.ItemID);
                item.AssetType = (AssetType) dataBlock.Type;
                item.AssetUUID = dataBlock.AssetID;
                item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
                item.CreatorID = dataBlock.CreatorID;
                item.Description = Utils.BytesToString(dataBlock.Description);
                item.Flags = dataBlock.Flags;
                item.GroupID = dataBlock.GroupID;
                item.GroupOwned = dataBlock.GroupOwned;
                item.InventoryType = (InventoryType) dataBlock.InvType;
                item.Name = Utils.BytesToString(dataBlock.Name);
                item.OwnerID = dataBlock.OwnerID;
                item.ParentUUID = dataBlock.FolderID;
                item.Permissions = new Permissions(
                    dataBlock.BaseMask,
                    dataBlock.EveryoneMask,
                    dataBlock.GroupMask,
                    dataBlock.NextOwnerMask,
                    dataBlock.OwnerMask);
                item.SalePrice = dataBlock.SalePrice;
                item.SaleType = (SaleType) dataBlock.SaleType;
                item.UUID = dataBlock.ItemID;

                _Store[item.UUID] = item;

                // Fire the callback for an item being fetched
                OnItemReceived(new ItemReceivedEventArgs(item));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ReplyTaskInventoryHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_TaskInventoryReply != null)
            {
                Packet packet = e.Packet;

                ReplyTaskInventoryPacket reply = (ReplyTaskInventoryPacket)packet;

                OnTaskInventoryReply(new TaskInventoryReplyEventArgs(reply.InventoryData.TaskID, reply.InventoryData.Serial,
                    Utils.BytesToString(reply.InventoryData.Filename)));
            }
        }

        protected void ScriptRunningReplyMessageHandler(string capsKey, Interfaces.IMessage message, Simulator simulator)
        {
            if (m_ScriptRunningReply != null)
            {
                ScriptRunningReplyMessage msg = (ScriptRunningReplyMessage)message;
                OnScriptRunningReply(new ScriptRunningReplyEventArgs(msg.ObjectID, msg.ItemID, msg.Mono, msg.Running));
            }
        }

        #endregion Packet Handlers
    }

    #region EventArgs

    public class InventoryObjectOfferedEventArgs : EventArgs
    {
        /// <summary>Set to true to accept offer, false to decline it</summary>
        public bool Accept { get; set; }
        /// <summary>The folder to accept the inventory into, if null default folder for <see cref="AssetType"/> will be used</summary>
        public UUID FolderID { get; set; }

        public InstantMessage Offer { get; }

        public AssetType AssetType { get; }

        public UUID ObjectID { get; }

        public bool FromTask { get; }

        public InventoryObjectOfferedEventArgs(InstantMessage offerDetails, AssetType type, UUID objectID, bool fromTask, UUID folderID)
        {
            this.Accept = false;
            this.FolderID = folderID;
            this.Offer = offerDetails;
            this.AssetType = type;
            this.ObjectID = objectID;
            this.FromTask = fromTask;
        }
    }

    public class FolderUpdatedEventArgs : EventArgs
    {
        public UUID FolderID { get; }

        public bool Success { get; }

        public FolderUpdatedEventArgs(UUID folderID, bool success)
        {
            this.FolderID = folderID;
            this.Success = success;
        }
    }

    public class ItemReceivedEventArgs : EventArgs
    {
        public InventoryItem Item { get; }

        public ItemReceivedEventArgs(InventoryItem item)
        {
            this.Item = item;
        }
    }

    public class FindObjectByPathReplyEventArgs : EventArgs
    {
        public String Path { get; }

        public UUID InventoryObjectID { get; }

        public FindObjectByPathReplyEventArgs(string path, UUID inventoryObjectID)
        {
            this.Path = path;
            this.InventoryObjectID = inventoryObjectID;
        }
    }

    /// <summary>
    /// Callback when an inventory object is accepted and received from a
    /// task inventory. This is the callback in which you actually get
    /// the ItemID, as in ObjectOfferedCallback it is null when received
    /// from a task.
    /// </summary>
    public class TaskItemReceivedEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public UUID FolderID { get; }

        public UUID CreatorID { get; }

        public UUID AssetID { get; }

        public InventoryType Type { get; }

        public TaskItemReceivedEventArgs(UUID itemID, UUID folderID, UUID creatorID, UUID assetID, InventoryType type)
        {
            this.ItemID = itemID;
            this.FolderID = folderID;
            this.CreatorID = creatorID;
            this.AssetID = assetID;
            this.Type = type;
        }
    }

    public class TaskInventoryReplyEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public Int16 Serial { get; }

        public String AssetFilename { get; }

        public TaskInventoryReplyEventArgs(UUID itemID, short serial, string assetFilename)
        {
            this.ItemID = itemID;
            this.Serial = serial;
            this.AssetFilename = assetFilename;
        }
    }

    public class SaveAssetToInventoryEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public UUID NewAssetID { get; }

        public SaveAssetToInventoryEventArgs(UUID itemID, UUID newAssetID)
        {
            this.ItemID = itemID;
            this.NewAssetID = newAssetID;
        }
    }

    public class ScriptRunningReplyEventArgs : EventArgs
    {
        public UUID ObjectID { get; }

        public UUID ScriptID { get; }

        public bool IsMono { get; }

        public bool IsRunning { get; }

        public ScriptRunningReplyEventArgs(UUID objectID, UUID sctriptID, bool isMono, bool isRunning)
        {
            this.ObjectID = objectID;
            this.ScriptID = sctriptID;
            this.IsMono = isMono;
            this.IsRunning = isRunning;
        }
    }
    #endregion
}
