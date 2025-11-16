/*
 * Copyright (c) 2025, Sjofn LLC.
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
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        // Async-first variants of several Request* methods that previously used callbacks or direct HttpCapsClient calls
        public async Task RequestCreateItemFromAssetAsync(byte[] data, string name, string description, AssetType assetType,
            InventoryType invType, UUID folderID, Permissions permissions, ItemCreatedFromAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("NewFileAgentInventory", false);
            if (cap == null)
            {
                throw new InvalidOperationException("NewFileAgentInventory capability is not currently available");
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

            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken).ConfigureAwait(false);
                CreateItemFromAssetResponse(callback, data, query, result, null);
            }
            catch (Exception ex)
            {
                CreateItemFromAssetResponse(callback, data, query, null, ex);
            }
        }

        public async Task RequestCopyItemFromNotecardAsync(UUID objectID, UUID notecardID, UUID folderID, UUID itemID, ItemCopiedCallback callback, CancellationToken cancellationToken = default)
        {
            _ItemCopiedCallbacks[0] = callback; // Notecards always use callback ID 0

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

                try
                {
                    await PostStringAsync(cap, message.Serialize(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // fallback to LLUDP path if capability call fails
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

        public async Task RequestUploadNotecardAssetAsync(byte[] data, UUID notecardID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateNotecardAgentInventory", false);
            if (cap == null)
            {
                throw new InvalidOperationException("Capability system not initialized to send asset");
            }

            var query = new OSDMap { { "item_id", OSD.FromUUID(notecardID) } };

            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken).ConfigureAwait(false);
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID, result, null);
            }
            catch (Exception ex)
            {
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID, null, ex);
            }
        }

        public async Task RequestUpdateNotecardTaskAsync(byte[] data, UUID notecardID, UUID taskID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateNotecardTaskInventory", false);
            if (cap == null)
            {
                throw new InvalidOperationException("UpdateNotecardTaskInventory capability is not currently available");
            }

            var query = new OSDMap
            {
                {"item_id", OSD.FromUUID(notecardID)},
                { "task_id", OSD.FromUUID(taskID)}
            };

            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken).ConfigureAwait(false);
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID, result, null);
            }
            catch (Exception ex)
            {
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), notecardID, null, ex);
            }
        }

        public async Task RequestUploadGestureAssetAsync(byte[] data, UUID gestureID, InventoryUploadedAssetCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateGestureAgentInventory", false);
            if (cap == null)
            {
                throw new InvalidOperationException("UpdateGestureAgentInventory capability is not currently available");
            }

            var query = new OSDMap { { "item_id", OSD.FromUUID(gestureID) } };

            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken).ConfigureAwait(false);
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), gestureID, result, null);
            }
            catch (Exception ex)
            {
                UploadInventoryAssetResponse(new KeyValuePair<InventoryUploadedAssetCallback, byte[]>(callback, data), gestureID, null, ex);
            }
        }

        public async Task RequestUpdateScriptAgentInventoryAsync(byte[] data, UUID itemID, bool mono, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateScriptAgent");
            if (cap == null)
                throw new InvalidOperationException("UpdateScriptAgent capability is not currently available");

            var request = new UpdateScriptAgentRequestMessage
            {
                ItemID = itemID,
                Target = mono ? "mono" : "lsl2"
            };

            try
            {
                var result = await PostStringAsync(cap, request.Serialize(), cancellationToken).ConfigureAwait(false);
                UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID, result, null);
            }
            catch (Exception ex)
            {
                UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID, null, ex);
            }
        }

        public async Task RequestUpdateScriptTaskAsync(byte[] data, UUID itemID, UUID taskID, bool mono, bool running, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            var cap = GetCapabilityURI("UpdateScriptTask");
            if (cap == null)
                throw new InvalidOperationException("UpdateScriptTask capability is not currently available");

            var msg = new UpdateScriptTaskUpdateMessage
            {
                ItemID = itemID,
                TaskID = taskID,
                ScriptRunning = running,
                Target = mono ? "mono" : "lsl2"
            };

            try
            {
                var result = await PostStringAsync(cap, msg.Serialize(), cancellationToken).ConfigureAwait(false);
                UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID, result, null);
            }
            catch (Exception ex)
            {
                UpdateScriptAgentInventoryResponse(new KeyValuePair<ScriptUpdatedCallback, byte[]>(callback, data), itemID, null, ex);
            }
        }

        /// <summary>
        /// Async-first variant to request a single inventory item. Uses FetchInventory2 capability when available.
        /// </summary>
        public Task RequestFetchInventoryAsync(UUID itemID, UUID ownerID, CancellationToken cancellationToken = default)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                return RequestFetchInventoryHttpAsync(itemID, ownerID, cancellationToken, null);
            }

            RequestFetchInventory(itemID, ownerID);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Async-first variant to request multiple inventory items. Uses FetchInventory2 capability when available.
        /// </summary>
        public Task RequestFetchInventoryAsync(Dictionary<UUID, UUID> items, CancellationToken cancellationToken = default, Action<List<InventoryItem>> callback = null)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                return RequestFetchInventoryHttpAsync(items, cancellationToken, callback);
            }

            RequestFetchInventory(items);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Async-first variant to request copying multiple items. Falls back to LLUDP copy packet.
        /// </summary>
        public Task RequestCopyItemsAsync(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
            UUID oldOwnerID, ItemCopiedCallback callback, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (targetFolders == null) throw new ArgumentNullException(nameof(targetFolders));
            if (items.Count != targetFolders.Count || (newNames != null && items.Count != newNames.Count))
                throw new ArgumentException("All list arguments must have an equal number of entries");

            // Try AISv3 Inventory API first
            var invCap = GetCapabilityURI("InventoryAPIv3", false);
            if (Client.AisClient.IsAvailable && invCap != null)
            {
                try
                {
                    var ops = new OSDArray(items.Count);
                    for (var i = 0; i < items.Count; ++i)
                    {
                        var op = new OSDMap
                        {
                            ["item_id"] = items[i],
                            ["folder_id"] = targetFolders[i]
                        };

                        if (newNames != null && !string.IsNullOrEmpty(newNames[i]))
                            op["new_name"] = newNames[i];

                        ops.Add(op);
                    }

                    var payload = new OSDMap { ["items"] = ops, ["agent_id"] = Client.Self.AgentID };

                    // Post to AIS inventory API; ignore result for now and let server emit normal copy callbacks
                    _ = PostCapAsync(invCap, payload, cancellationToken).ConfigureAwait(false);
                    return Task.CompletedTask;
                }
                catch (Exception)
                {
                    // Fall through to legacy LLUDP path on error
                }
            }

            // Legacy LLUDP path
            var callbackID = RegisterItemsCopiedCallback(callback);

            var copy = new CopyInventoryItemPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = new CopyInventoryItemPacket.InventoryDataBlock[items.Count]
            };

            for (var i = 0; i < items.Count; ++i)
            {
                copy.InventoryData[i] = new CopyInventoryItemPacket.InventoryDataBlock
                {
                    CallbackID = callbackID,
                    NewFolderID = targetFolders[i],
                    OldAgentID = oldOwnerID,
                    OldItemID = items[i],
                    NewName = !string.IsNullOrEmpty(newNames?[i])
                        ? Utils.StringToBytes(newNames[i])
                        : Utils.EmptyBytes
                };
            }

            Client.Network.SendPacket(copy);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Async-first wrapper for RequestCopyItem (single item)
        /// </summary>
        public Task RequestCopyItemAsync(UUID item, UUID newParent, string newName, ItemCopiedCallback callback, CancellationToken cancellationToken = default)
        {
            return RequestCopyItemAsync(item, newParent, newName, Client.Self.AgentID, callback, cancellationToken);
        }

        /// <summary>
        /// Async-first wrapper for RequestCopyItem with explicit old owner
        /// </summary>
        public Task RequestCopyItemAsync(UUID item, UUID newParent, string newName, UUID oldOwnerID, ItemCopiedCallback callback, CancellationToken cancellationToken = default)
        {
            var items = new List<UUID>(1) { item };
            var folders = new List<UUID>(1) { newParent };
            var names = new List<string>(1) { newName };

            return RequestCopyItemsAsync(items, folders, names, oldOwnerID, callback, cancellationToken);
        }

        public async Task<InventoryItem> FetchItemAsync(UUID itemID, UUID ownerID, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<InventoryItem>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object sender, ItemReceivedEventArgs e)
            {
                if (e.Item.UUID == itemID)
                    tcs.TrySetResult(e.Item);
            }

            ItemReceived += Callback;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                try
                {
                    RequestFetchInventory(itemID, ownerID);
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                finally
                {
                    ItemReceived -= Callback;
                }
            }
        }

        public async Task<List<InventoryBase>> FolderContentsAsync(UUID folder, UUID owner, bool fetchFolders, bool fetchItems,
            InventorySortOrder order, CancellationToken cancellationToken = default, bool followLinks = false)
        {
            List<InventoryBase> inventory = null;

            try
            {
                inventory = await RequestFolderContents(folder, owner, fetchFolders, fetchItems, order, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                inventory = null;
            }

            if (inventory == null)
            {
                inventory = _Store.GetContents(folder);
            }

            if (inventory != null && followLinks)
            {
                for (var i = 0; i < inventory.Count; ++i)
                {
                    if (!(inventory[i] is InventoryItem item)) continue;

                    if (item.IsLink())
                    {
                        if (!Store.Contains(item.AssetUUID))
                        {
                            var fetched = await FetchItemAsync(item.AssetUUID, owner, cancellationToken).ConfigureAwait(false);
                            if (fetched != null)
                                inventory[i] = fetched;
                        }
                    }
                }
            }

            return inventory;
        }

        public async Task<UUID> FindObjectByPathAsync(UUID baseFolder, UUID inventoryOwner, string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Empty path is not supported", nameof(path));

            var tcs = new TaskCompletionSource<UUID>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object sender, FindObjectByPathReplyEventArgs e)
            {
                if (e.Path == path)
                {
                    tcs.TrySetResult(e.InventoryObjectID);
                }
            }

            FindObjectByPathReply += Callback;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                try
                {
                    await RequestFindObjectByPath(baseFolder, inventoryOwner, path).ConfigureAwait(false);
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return UUID.Zero;
                }
                finally
                {
                    FindObjectByPathReply -= Callback;
                }
            }
        }

        public async Task<List<InventoryBase>> GetTaskInventoryAsync(UUID objectID, uint objectLocalID, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object sender, TaskInventoryReplyEventArgs e)
            {
                if (e.ItemID == objectID)
                    tcs.TrySetResult(e.AssetFilename);
            }

            TaskInventoryReply += Callback;

            try
            {
                RequestTaskInventory(objectLocalID);

                string filename;
                try
                {
                    using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
                    {
                        filename = await tcs.Task.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(filename))
                {
                    Logger.DebugLog($"Task is empty for {objectLocalID}", Client);
                    return new List<InventoryBase>(0);
                }

                var xferTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                ulong xferID = 0;

                void XferCallback(object sender, XferReceivedEventArgs e)
                {
                    if (e.Xfer.XferID == xferID)
                        xferTcs.TrySetResult(e.Xfer.AssetData);
                }

                Client.Assets.XferReceived += XferCallback;

                try
                {
                    xferID = Client.Assets.RequestAssetXfer(filename, true, false, UUID.Zero, AssetType.Unknown, true);

                    byte[] assetData;
                    try
                    {
                        using (cancellationToken.Register(() => xferTcs.TrySetCanceled(), useSynchronizationContext: false))
                        {
                            assetData = await xferTcs.Task.ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }

                    var taskList = Utils.BytesToString(assetData);
                    return ParseTaskInventory(taskList);
                }
                finally
                {
                    Client.Assets.XferReceived -= XferCallback;
                }
            }
            finally
            {
                TaskInventoryReply -= Callback;
            }
        }

        public async Task GiveFolderAsync(UUID folderID, string folderName, UUID recipient, bool doEffect, CancellationToken cancellationToken = default)
        {
            var folders = new List<InventoryFolder>();
            var items = new List<InventoryItem>();

            // Call the internal async recursive function defined in main file
            await GetInventoryRecursiveAsync(folderID, Client.Self.AgentID, folders, items, cancellationToken).ConfigureAwait(false);

            var total_contents = folders.Count + items.Count;

            // check for too many items.
            if (total_contents > MAX_GIVE_ITEMS)
            {
                Logger.Log($"Cannot give more than {MAX_GIVE_ITEMS} items in a single inventory transfer.", Helpers.LogLevel.Info);
                return;
            }
            if (items.Count == 0)
            {
                Logger.Log("No items to transfer.", Helpers.LogLevel.Info);
                return;
            }

            var bucket = new byte[17 * (total_contents + 1)];
            var offset = 0; // account for first byte

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
            foreach (var invItem in items.Where(item => Store.Contains(item.UUID) && Store[item.UUID] is InventoryItem)
                     .Select(item => (InventoryItem)Store[item.UUID])
                     .Where(invItem => (invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None))
            {
                Store.RemoveNodeFor(invItem);
            }
        }

        /// <summary>
        /// Asynchronously recurse inventory category and return folders and items. Does NOT contain parent folder being searched
        /// </summary>
        /// <param name="folderID">Inventory category to recursively search</param>
        /// <param name="owner">Owner of folder</param>
        /// <param name="cats">reference to list of categories</param>
        /// <param name="items">reference to list of items</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task GetInventoryRecursiveAsync(UUID folderID, UUID owner,
            List<InventoryFolder> cats, List<InventoryItem> items, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contents = await FolderContentsAsync(
                folderID, owner, true, true, InventorySortOrder.ByDate, cancellationToken).ConfigureAwait(false);

            if (contents == null) return;

            foreach (var entry in contents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (entry)
                {
                    case InventoryFolder folder:
                        cats.Add(folder);
                        await GetInventoryRecursiveAsync(folder.UUID, owner, cats, items, cancellationToken).ConfigureAwait(false);
                        break;
                    case InventoryItem _:
                        var fetched = await FetchItemAsync(entry.UUID, owner, cancellationToken).ConfigureAwait(false);
                        if (fetched != null)
                            items.Add(fetched);
                        break;
                    default: // shouldn't happen
                        Logger.Log("Retrieved inventory contents of invalid type", Helpers.LogLevel.Error);
                        break;
                }
            }
        }

        // Helper to POST a serialized string payload to a capability and deserialize the OSD response
        private async Task<OSD> PostStringAsync(Uri uri, string content, CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var tcs = new TaskCompletionSource<OSD>(TaskCreationOptions.RunContinuationsAsynchronously);

            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, content, cancellationToken,
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
                }).ConfigureAwait(false);

            return await tcs.Task.ConfigureAwait(false);
        }

    }
}
