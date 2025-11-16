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
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void SaveAssetIntoInventoryHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_SaveAssetToInventory != null)
            {
                var packet = e.Packet;

                var save = (SaveAssetIntoInventoryPacket)packet;
                OnSaveAssetToInventory(new SaveAssetToInventoryEventArgs(save.InventoryData.ItemID,
                    save.InventoryData.NewAssetID));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void InventoryDescendentsHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;

            var reply = (InventoryDescendentsPacket)packet;

            if (reply.AgentData.Descendents > 0)
            {
                // InventoryDescendantsReply sends a null folder if the parent doesn't contain any folders
                if (reply.FolderData[0].FolderID != UUID.Zero)
                {
                    // Iterate folders in this packet
                    foreach (var data in reply.FolderData)
                    {
                        // If folder already exists then ignore, we assume the version cache
                        // logic is working and if the folder is stale then it should not be present.
                        if (!_Store.Contains(data.FolderID))
                        {
                            var folder = new InventoryFolder(data.FolderID)
                            {
                                ParentUUID = data.ParentID,
                                Name = Utils.BytesToString(data.Name),
                                PreferredType = (FolderType)data.Type,
                                OwnerID = reply.AgentData.OwnerID
                            };

                            _Store[folder.UUID] = folder;
                        }
                    }
                }

                // InventoryDescendantsReply sends a null item if the parent doesn't contain any items.
                if (reply.ItemData[0].ItemID != UUID.Zero)
                {
                    // Iterate items in this packet
                    foreach (var data in reply.ItemData)
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
                            if ((InventoryType)data.InvType == InventoryType.Texture &&
                                (AssetType)data.Type == AssetType.Object
                                || (AssetType)data.Type == AssetType.Mesh)
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
                _Store[reply.AgentData.FolderID] is InventoryFolder invFolder)
            {
                parentFolder = invFolder;
            }
            else
            {
                Logger.Log($"No reference for FolderID {reply.AgentData.FolderID} or it is not a folder",
                    Helpers.LogLevel.Error, Client);
                return;
            }

            if (reply.AgentData.Version < parentFolder.Version)
            {
                Logger.Log($"Received outdated InventoryDescendents packet for folder {parentFolder.Name}, " +
                           $"this version = {reply.AgentData.Version}, latest version = {parentFolder.Version}",
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
                    // Iterate over all outstanding searches
                    for (var i = 0; i < _Searches.Count; ++i)
                    {
                        var search = _Searches[i];
                        var folderContents = _Store.GetContents(search.Folder);

                        // Iterate over all inventory objects in the base search folder
                        foreach (var content in folderContents.Where(content =>
                                     content.Name == search.Path[search.Level]))
                        {
                            if (search.Level == search.Path.Length - 1)
                            {
                                Logger.DebugLog("Finished path search of " + string.Join("/", search.Path), Client);

                                // This is the last node in the path, fire the callback and clean up
                                if (m_FindObjectByPathReply != null)
                                {
                                    OnFindObjectByPathReply(new FindObjectByPathReplyEventArgs(
                                        string.Join("/", search.Path),
                                        content.UUID));
                                }

                                // Remove this entry and restart the loop since we are changing the collection size
                                _Searches.RemoveAt(i);
                                goto StartSearch;
                            }
                            else
                            {
                                // We found a match, but it is not the end of the path; request the next level
                                Logger.DebugLog(
                                    $"Matched level {search.Level}/{search.Path.Length - 1} " +
                                    $"in a path search of {string.Join("/", search.Path)}", Client);

                                search.Folder = content.UUID;
                                search.Level++;
                                _Searches[i] = search;

                                Task task = RequestFolderContents(search.Folder, search.Owner, true, true,
                                    InventorySortOrder.ByName);
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
        /// taken into inventory, when an item is created using the <see cref="CreateInventoryItem"/>
        /// packet, or when an object has been purchased
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void UpdateCreateInventoryItemHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            if (!(packet is UpdateCreateInventoryItemPacket reply)) return;

            foreach (var dataBlock in reply.InventoryData)
            {
                if (dataBlock.InvType == (sbyte)InventoryType.Folder)
                {
                    Logger.Log(
                        "Received InventoryFolder in an UpdateCreateInventoryItem packet, this should not happen!",
                        Helpers.LogLevel.Error, Client);
                    continue;
                }

                var item = CreateInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);
                item.AssetType = (AssetType)dataBlock.Type;
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
                item.SaleType = (SaleType)dataBlock.SaleType;

                /*
                 * When attaching new objects, an UpdateCreateInventoryItem packet will be
                 * returned by the server that has a FolderID/ParentUUID of zero. It is up
                 * to the client to make sure that the item gets a good folder, otherwise
                 * it will end up inaccessible in inventory.
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
                    _ItemCreatedCallbacks.TryRemove(dataBlock.CallbackID, out _);

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
                    _ItemCopiedCallbacks.TryRemove(dataBlock.CallbackID, out _);

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
            var packet = e.Packet;

            var move = (MoveInventoryItemPacket)packet;

            foreach (var data in move.InventoryData)
            {
                // FIXME: Do something here
                var newName = Utils.BytesToString(data.NewName);

                Logger.Log(
                    $"MoveInventoryItemHandler: Item {data.ItemID} is moving to Folder {data.FolderID} with new name \"{newName}\"." +
                    " Someone write this function!",
                    Helpers.LogLevel.Warning, Client);
            }
        }

        protected void BulkUpdateInventoryCapHandler(string capsKey, Interfaces.IMessage message, Simulator simulator)
        {
            var msg = (BulkUpdateInventoryMessage)message;

            foreach (var newFolder in msg.FolderData)
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

            foreach (var newItem in msg.ItemData)
            {
                if (newItem.ItemID == UUID.Zero) continue;
                var invType = newItem.InvType;

                lock (_ItemInventoryTypeRequest)
                {
                    InventoryType storedType = 0;
                    if (_ItemInventoryTypeRequest.TryGetValue(newItem.CallbackID, out storedType))
                    {
                        _ItemInventoryTypeRequest.TryRemove(newItem.CallbackID, out _);
                        invType = storedType;
                    }
                }

                var item = CreateOrRetrieveInventoryItem(invType, newItem.ItemID);

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
                    _ItemCreatedCallbacks.TryRemove(newItem.CallbackID, out _);

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
                if (_ItemCopiedCallbacks.TryGetValue(newItem.CallbackID, out copyCallback))
                {
                    _ItemCopiedCallbacks.TryRemove(newItem.CallbackID, out _);

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

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void BulkUpdateInventoryHandler(object sender, PacketReceivedEventArgs e)
        {
            var packet = e.Packet;

            if (!(packet is BulkUpdateInventoryPacket update)) return;

            if (update.FolderData.Length > 0 && update.FolderData[0].FolderID != UUID.Zero)
            {
                foreach (var dataBlock in update.FolderData)
                {
                    InventoryFolder folder;
                    if (!_Store.Contains(dataBlock.FolderID))
                    {
                        folder = new InventoryFolder(dataBlock.FolderID);
                    }
                    else
                    {
                        folder = (InventoryFolder)_Store[dataBlock.FolderID];
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
                foreach (var dataBlock in update.ItemData)
                {
                    var item =
                        CreateOrRetrieveInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);

                    item.AssetType = (AssetType)dataBlock.Type;
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
                    item.SaleType = (SaleType)dataBlock.SaleType;

                    _Store[item.UUID] = item;

                    // Look for an "item created" callback
                    ItemCreatedCallback callback;
                    if (_ItemCreatedCallbacks.TryGetValue(dataBlock.CallbackID, out callback))
                    {
                        _ItemCreatedCallbacks.TryRemove(dataBlock.CallbackID, out _);

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
                        _ItemCopiedCallbacks.TryRemove(dataBlock.CallbackID, out _);

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
            var packet = e.Packet;
            if (!(packet is FetchInventoryReplyPacket reply)) return;

            foreach (var dataBlock in reply.InventoryData)
            {
                if (dataBlock.InvType == (sbyte)InventoryType.Folder)
                {
                    Logger.Log("Received FetchInventoryReply for an inventory folder, this should not happen!",
                        Helpers.LogLevel.Error, Client);
                    continue;
                }

                var item = CreateInventoryItem((InventoryType)dataBlock.InvType, dataBlock.ItemID);
                item.AssetType = (AssetType)dataBlock.Type;
                item.AssetUUID = dataBlock.AssetID;
                item.CreationDate = Utils.UnixTimeToDateTime(dataBlock.CreationDate);
                item.CreatorID = dataBlock.CreatorID;
                item.Description = Utils.BytesToString(dataBlock.Description);
                item.Flags = dataBlock.Flags;
                item.GroupID = dataBlock.GroupID;
                item.GroupOwned = dataBlock.GroupOwned;
                item.InventoryType = (InventoryType)dataBlock.InvType;
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
                item.SaleType = (SaleType)dataBlock.SaleType;
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
                var packet = e.Packet;

                var reply = (ReplyTaskInventoryPacket)packet;

                OnTaskInventoryReply(new TaskInventoryReplyEventArgs(reply.InventoryData.TaskID,
                    reply.InventoryData.Serial,
                    Utils.BytesToString(reply.InventoryData.Filename)));
            }
        }

        protected void ScriptRunningReplyMessageHandler(string capsKey, Interfaces.IMessage message,
            Simulator simulator)
        {
            if (m_ScriptRunningReply != null)
            {
                var msg = (ScriptRunningReplyMessage)message;
                OnScriptRunningReply(new ScriptRunningReplyEventArgs(msg.ObjectID, msg.ItemID, msg.Mono, msg.Running));
            }
        }
    }
}
