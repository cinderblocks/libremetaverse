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

using LibreMetaverse.Assets;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static LibreMetaverse.HttpCapsClient;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        public async Task MoveFolderAsync(UUID folderID, UUID newParentID, CancellationToken cancellationToken = default)
        {
            using (var writeLock = _storeLock.WriteLock())
            {
                if (_Store != null && _Store.TryGetValue(folderID, out var storeItem) && storeItem is InventoryFolder inv)
                {
                    inv.ParentUUID = newParentID;
                    _Store.UpdateNodeFor(inv);
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                await Client.AisClient.MoveCategoryAsync(folderID, newParentID, cancellationToken).ConfigureAwait(false);
                return;
            }

            var move = new MoveInventoryFolderPacket
            {
                AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID, Stamp = false },
                InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[1]
            };
            move.InventoryData[0] = new MoveInventoryFolderPacket.InventoryDataBlock { FolderID = folderID, ParentID = newParentID };
            Client.Network.SendPacket(move);
        }

        public async Task MoveFoldersAsync(Dictionary<UUID, UUID> foldersNewParents, CancellationToken cancellationToken = default)
        {
            using (var writeLock = _storeLock.WriteLock())
            {
                foreach (var entry in foldersNewParents)
                {
                    if (_Store != null && _Store.TryGetValue(entry.Key, out var storeItem) && storeItem is InventoryFolder inv)
                    {
                        inv.ParentUUID = entry.Value;
                        _Store.UpdateNodeFor(inv);
                    }
                }
            }

            if (Client.AisClient.IsAvailable)
            {
                var tasks = foldersNewParents.Select(kv => Client.AisClient.MoveCategoryAsync(kv.Key, kv.Value, cancellationToken)).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                return;
            }

            var move = new MoveInventoryFolderPacket
            {
                AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID, Stamp = false },
                InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[foldersNewParents.Count]
            };
            var index = 0;
            foreach (var folder in foldersNewParents)
                move.InventoryData[index++] = new MoveInventoryFolderPacket.InventoryDataBlock { FolderID = folder.Key, ParentID = folder.Value };
            Client.Network.SendPacket(move);
        }

        public Task MoveItemAsync(UUID itemID, UUID folderID, CancellationToken cancellationToken = default)
            => MoveItemAsync(itemID, folderID, string.Empty, cancellationToken);

        public async Task MoveItemAsync(UUID itemID, UUID folderID, string newName, CancellationToken cancellationToken = default)
        {
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
                Logger.Warn($"MoveItemAsync local update failed: {ex.Message}", Client);
            }

            if (string.IsNullOrEmpty(newName) && Client.AisClient.IsAvailable)
            {
                await Client.AisClient.MoveItemAsync(itemID, folderID, cancellationToken).ConfigureAwait(false);
                return;
            }

            var move = new MoveInventoryItemPacket
            {
                AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID, Stamp = false },
                InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[1]
            };
            move.InventoryData[0] = new MoveInventoryItemPacket.InventoryDataBlock { ItemID = itemID, FolderID = folderID, NewName = Utils.StringToBytes(newName) };
            Client.Network.SendPacket(move);
        }

        public async Task MoveItemsAsync(Dictionary<UUID, UUID> itemsNewFolders, CancellationToken cancellationToken = default)
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
                var tasks = itemsNewFolders.Select(kv => Client.AisClient.MoveItemAsync(kv.Key, kv.Value, cancellationToken)).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                return;
            }

            var move = new MoveInventoryItemPacket
            {
                AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID, Stamp = false },
                InventoryData = new MoveInventoryItemPacket.InventoryDataBlock[itemsNewFolders.Count]
            };
            var idx = 0;
            foreach (var item in itemsNewFolders)
                move.InventoryData[idx++] = new MoveInventoryItemPacket.InventoryDataBlock { ItemID = item.Key, FolderID = item.Value };
            Client.Network.SendPacket(move);
        }

        

        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestCreateItemFromAssetAsync(
            byte[] data, string name, string description, AssetType assetType, InventoryType invType,
            UUID folderID, Permissions permissions,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("NewFileAgentInventory", false);
            if (cap == null)
                throw new InvalidOperationException("NewFileAgentInventory capability is not currently available");

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
                {"expected_upload_cost", OSD.FromInteger(GetUploadCostForAssetType(assetType))}
            };

            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await CreateItemFromAssetAsync(data, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        public async Task<InventoryBase?> RequestCopyItemFromNotecardAsync(UUID objectID, UUID notecardID, UUID folderID, UUID itemID,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var tcs = new TaskCompletionSource<InventoryBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            _ItemCopiedCallbacks[0] = copied => tcs.TrySetResult(copied); // Notecards always use callback ID 0

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
                    await PostStringAsync(cap, message.Serialize(), cancellationToken, progress).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    SendCopyFromNotecardPacket(objectID, notecardID, folderID, itemID);
                }
            }
            else
            {
                SendCopyFromNotecardPacket(objectID, notecardID, folderID, itemID);
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        private void SendCopyFromNotecardPacket(UUID objectID, UUID notecardID, UUID folderID, UUID itemID)
        {
            var copy = new CopyInventoryFromNotecardPacket
            {
                AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID },
                NotecardData = { ObjectID = objectID, NotecardItemID = notecardID },
                InventoryData = new CopyInventoryFromNotecardPacket.InventoryDataBlock[1]
            };
            copy.InventoryData[0] = new CopyInventoryFromNotecardPacket.InventoryDataBlock { FolderID = folderID, ItemID = itemID };
            Client.Network.SendPacket(copy);
        }

        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestUploadNotecardAssetAsync(byte[] data, UUID notecardID,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("UpdateNotecardAgentInventory", false);
            if (cap == null)
                throw new InvalidOperationException("Capability system not initialized to send asset");

            var query = new OSDMap { { "item_id", OSD.FromUUID(notecardID) } };
            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await PerformInventoryUploadAsync(data, notecardID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestUpdateNotecardTaskAsync(byte[] data, UUID notecardID, UUID taskID,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("UpdateNotecardTaskInventory", false);
            if (cap == null)
                throw new InvalidOperationException("UpdateNotecardTaskInventory capability is not currently available");

            var query = new OSDMap { {"item_id", OSD.FromUUID(notecardID)}, {"task_id", OSD.FromUUID(taskID)} };
            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await PerformInventoryUploadAsync(data, notecardID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestUploadGestureAssetAsync(byte[] data, UUID gestureID,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("UpdateGestureAgentInventory", false);
            if (cap == null)
                throw new InvalidOperationException("UpdateGestureAgentInventory capability is not currently available");

            var query = new OSDMap { { "item_id", OSD.FromUUID(gestureID) } };
            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await PerformInventoryUploadAsync(data, gestureID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        /// <summary>
        /// Saves a GLTF material to an existing agent inventory item via the
        /// UpdateMaterialAgentInventory capability. Mirrors
        /// LLMaterialEditor::updateInventoryItem's agent-inventory branch (llmaterialeditor.cpp): a
        /// two-phase upload where the metadata POST (item_id) returns an "uploader" URL that the
        /// material's minified GLTF JSON is then POSTed to.
        /// </summary>
        /// <param name="material">The material to save; its JSON encoding (<see cref="AssetMaterial.ToJson"/>)
        /// is what gets uploaded</param>
        /// <param name="materialItemID">UUID of the existing inventory item to update</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Optional upload progress reporter</param>
        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestUpdateMaterialAgentInventoryAsync(
            AssetMaterial material, UUID materialItemID, CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            var cap = GetCapabilityURI("UpdateMaterialAgentInventory", false);
            if (cap == null)
                throw new InvalidOperationException("UpdateMaterialAgentInventory capability is not currently available");

            var data = System.Text.Encoding.UTF8.GetBytes(material.ToJson());
            var query = new OSDMap { { "item_id", OSD.FromUUID(materialItemID) } };
            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await PerformInventoryUploadAsync(data, materialItemID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        /// <summary>
        /// Saves a GLTF material to an existing task (object) inventory item via the
        /// UpdateMaterialTaskInventory capability. Mirrors
        /// LLMaterialEditor::updateInventoryItem's task-inventory branch (llmaterialeditor.cpp).
        /// </summary>
        /// <param name="material">The material to save; its JSON encoding (<see cref="AssetMaterial.ToJson"/>)
        /// is what gets uploaded</param>
        /// <param name="materialItemID">UUID of the existing task-inventory item to update</param>
        /// <param name="taskID">UUID of the object (task) containing the item</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Optional upload progress reporter</param>
        public async Task<(bool success, string status, UUID itemID, UUID assetID)> RequestUpdateMaterialTaskInventoryAsync(
            AssetMaterial material, UUID materialItemID, UUID taskID, CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            var cap = GetCapabilityURI("UpdateMaterialTaskInventory", false);
            if (cap == null)
                throw new InvalidOperationException("UpdateMaterialTaskInventory capability is not currently available");

            var data = System.Text.Encoding.UTF8.GetBytes(material.ToJson());
            var query = new OSDMap { { "item_id", OSD.FromUUID(materialItemID) }, { "task_id", OSD.FromUUID(taskID) } };
            try
            {
                var result = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                return await PerformInventoryUploadAsync(data, materialItemID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, UUID.Zero, UUID.Zero); }
        }

        public async Task<(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string>? compileMessages, UUID itemID, UUID assetID)> RequestUpdateScriptAgentInventoryAsync(
            byte[] data, UUID itemID, bool mono, CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("UpdateScriptAgent");
            if (cap == null)
                throw new InvalidOperationException("UpdateScriptAgent capability is not currently available");

            var request = new UpdateScriptAgentRequestMessage { ItemID = itemID, Target = mono ? "mono" : "lsl2" };
            try
            {
                var result = await PostCapAsync(cap, request.Serialize(), cancellationToken, progress).ConfigureAwait(false);
                return await PerformScriptUploadAsync(data, itemID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, false, null, UUID.Zero, UUID.Zero); }
        }

        public async Task<(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string>? compileMessages, UUID itemID, UUID assetID)> RequestUpdateScriptTaskAsync(
            byte[] data, UUID itemID, UUID taskID, bool mono, bool running, CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var cap = GetCapabilityURI("UpdateScriptTask");
            if (cap == null)
                throw new InvalidOperationException("UpdateScriptTask capability is not currently available");

            var msg = new UpdateScriptTaskUpdateMessage { ItemID = itemID, TaskID = taskID, ScriptRunning = running, Target = mono ? "mono" : "lsl2" };
            try
            {
                var result = await PostCapAsync(cap, msg.Serialize(), cancellationToken, progress).ConfigureAwait(false);
                return await PerformScriptUploadAsync(data, itemID, result, cancellationToken, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return (false, ex.Message, false, null, UUID.Zero, UUID.Zero); }
        }

        /// <summary>
        /// Async-first variant to request a single inventory item. Uses FetchInventory2 capability when available.
        /// </summary>
        public Task RequestFetchInventoryAsync(UUID itemID, UUID ownerID, CancellationToken cancellationToken = default)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                return RequestFetchInventoryHttpAsync(itemID, ownerID, cancellationToken, null!);
            }

            RequestFetchInventory(itemID, ownerID, cancellationToken);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Async-first variant to request multiple inventory items. Uses FetchInventory2 capability when available.
        /// </summary>
        public Task RequestFetchInventoryAsync(Dictionary<UUID, UUID> items, CancellationToken cancellationToken = default, 
            Action<List<InventoryItem>>? callback = null)
        {
            if (GetCapabilityURI("FetchInventory2") != null)
            {
                return RequestFetchInventoryHttpAsync(items, cancellationToken, callback);
            }

            RequestFetchInventory(items, cancellationToken);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Copy multiple inventory items. Redirects to <see cref="RequestCopyItemsWithResultAsync"/>.
        /// </summary>
        public Task<CopyItemsResult> RequestCopyItemsAsync(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
            UUID oldOwnerID, CancellationToken cancellationToken = default)
            => RequestCopyItemsWithResultAsync(items, targetFolders, newNames, oldOwnerID, cancellationToken);

        /// <summary>Copy a single inventory item.</summary>
        public Task<InventoryBase?> RequestCopyItemAsync(UUID item, UUID newParent, string newName,
            CancellationToken cancellationToken = default)
            => RequestCopyItemAsync(item, newParent, newName, Client.Self.AgentID, cancellationToken);

        /// <summary>Copy a single inventory item with explicit old owner.</summary>
        public async Task<InventoryBase?> RequestCopyItemAsync(UUID item, UUID newParent, string newName, UUID oldOwnerID,
            CancellationToken cancellationToken = default)
        {
            var result = await RequestCopyItemsWithResultAsync(
                new List<UUID>(1) { item },
                new List<UUID>(1) { newParent },
                new List<string>(1) { newName },
                oldOwnerID, cancellationToken).ConfigureAwait(false);
            return result.CopiedItems?.Count > 0 ? result.CopiedItems[0] : null;
        }

        public async Task<InventoryItem?> FetchItemAsync(UUID itemID, UUID ownerID, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<InventoryItem?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object? sender, ItemReceivedEventArgs e)
            {
                if (e.Item.UUID == itemID)
                    tcs.TrySetResult(e.Item);
            }

            ItemReceived += Callback;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                try
                {
                    await RequestFetchInventoryAsync(itemID, ownerID, cancellationToken).ConfigureAwait(false);
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
            List<InventoryBase>? inventory = null;

            try
            {
                inventory = await RequestFolderContentsAsync(folder, owner, fetchFolders, fetchItems, order, cancellationToken).ConfigureAwait(false);
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
                inventory = _Store?.GetContents(folder) ?? new List<InventoryBase>();
            }

            if (followLinks)
            {
                for (var i = 0; i < inventory.Count; ++i)
                {
                    if (!(inventory[i] is InventoryItem item)) continue;

                    if (!item.IsLink()) continue;

                    var store = Store;
                    // If the real item is already in the local store, substitute it immediately
                    // so callers always receive fully-typed items with correct metadata (e.g.
                    // AttachmentPoint on InventoryAttachment) rather than bare link objects.
                    if (store != null && store.TryGetValue<InventoryItem>(item.AssetUUID, out var cached) && cached != null && !cached.IsLink())
                    {
                        inventory[i] = cached;
                    }
                    else
                    {
                        var fetched = await FetchItemAsync(item.AssetUUID, owner, cancellationToken).ConfigureAwait(false);
                        if (fetched != null)
                            inventory[i] = fetched;
                    }
                }
            }

            return inventory!;
        }

        public async Task<UUID> FindObjectByPathAsync(UUID baseFolder, UUID inventoryOwner, string path, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Empty path is not supported", nameof(path));

            var tcs = new TaskCompletionSource<UUID>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object? sender, FindObjectByPathReplyEventArgs e)
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
                    await RequestFindObjectByPathAsync(baseFolder, inventoryOwner, path, cancellationToken).ConfigureAwait(false);
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

        public async Task<List<InventoryBase>> GetTaskInventoryAsync(UUID objectID, uint objectLocalID,
            Simulator? simulator = null, CancellationToken cancellationToken = default)
        {
            var sim = simulator ?? Client.Network.CurrentSim;

            // Mirrors the reference viewer's LLViewerObject::fetchInventoryFromServer(): when the
            // RequestTaskInventory capability is present, use it exclusively instead of the legacy
            // UDP RequestTaskInventory message + Xfer download below (see
            // LLViewerObject::fetchInventoryFromCapCoro in llviewerobject.cpp).
            Uri? cap = sim?.Caps?.CapabilityURI("RequestTaskInventory");
            if (cap != null)
            {
                return await GetTaskInventoryViaCapAsync(objectID, cap, cancellationToken).ConfigureAwait(false);
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(object? sender, TaskInventoryReplyEventArgs e)
            {
                if (e.ItemID == objectID)
                    tcs.TrySetResult(e.AssetFilename);
            }

            TaskInventoryReply += Callback;

            try
            {
                RequestTaskInventory(objectLocalID, simulator ?? Client.Network.CurrentSim);

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
                    return new List<InventoryBase>(0);
                }

                if (string.IsNullOrEmpty(filename))
                {
                    Logger.DebugLog($"Task is empty for {objectLocalID}", Client);
                    return new List<InventoryBase>(0);
                }

                var xferTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                ulong xferID = 0;

                void XferCallback(object? sender, XferReceivedEventArgs e)
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
                        return new List<InventoryBase>(0);
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

        /// <summary>
        /// Fetches a task's inventory via the RequestTaskInventory capability. Mirrors
        /// LLViewerObject::fetchInventoryFromCapCoro (llviewerobject.cpp): GET
        /// "?task_id=&lt;uuid&gt;" returns an LLSD map with a "contents" array of item maps in the
        /// same shape used elsewhere for AIS3 items (item_id/parent_id/permissions/sale_info/etc,
        /// see <see cref="InventoryItem.FromOSD"/>), plus an "inventory_serial" field the reference
        /// viewer uses to detect and re-request stale results -- LM does not currently cache
        /// per-task inventory serials, so that staleness optimization is intentionally omitted here.
        /// The reference viewer also synthesizes a "Contents" root category locally since the
        /// server doesn't send one; this does the same so callers see the same shape as the legacy
        /// UDP+Xfer path.
        /// </summary>
        private async Task<List<InventoryBase>> GetTaskInventoryViaCapAsync(UUID objectID, Uri cap,
            CancellationToken cancellationToken)
        {
            var items = new List<InventoryBase>
            {
                new InventoryFolder(objectID) { Name = "Contents", ParentUUID = UUID.Zero }
            };

            try
            {
                var requestUri = new Uri($"{cap}?task_id={objectID}");
                var (response, data) = await Client.HttpCapsClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"RequestTaskInventory non-success status: {response.StatusCode}", Client);
                    return items;
                }
                if (data == null) { return items; }

                if (!(OSDParser.Deserialize(data) is OSDMap map) || !(map["contents"] is OSDArray contents))
                {
                    Logger.Warn($"Unable to load task inventory via RequestTaskInventory cap for {objectID}", Client);
                    return items;
                }

                foreach (OSD entry in contents)
                {
                    if (!(entry is OSDMap itemMap) || !itemMap.ContainsKey("item_id")) { continue; }

                    var item = InventoryItem.FromOSD(itemMap);
                    if (itemMap.ContainsKey("shadow_id"))
                    {
                        item.AssetUUID = DecryptShadowID(itemMap["shadow_id"].AsUUID());
                    }
                    items.Add(item);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed fetching task inventory via cap for {objectID}", ex, Client);
            }

            return items;
        }

        /// <summary>
        /// Uploads a thumbnail image for an inventory item, folder, or task-inventory item via the
        /// InventoryThumbnailUpload capability. Mirrors
        /// LLFloaterSimpleSnapshot::uploadImageUploadFile / post_thumbnail_image_coro
        /// (llfloatersimplesnapshot.cpp): a two-phase upload -- POST metadata identifying the
        /// target to the capability (which returns an "uploader" URL), then POST the raw image
        /// bytes to that URL; a final "state":"complete" response carries the new thumbnail asset
        /// UUID under "new_asset". The metadata shape depends on the target, exactly matching the
        /// reference viewer's branch in uploadImageUploadFile: {item_id, task_id} for a
        /// task-inventory item, {category_id} for a local folder, or bare {item_id} for an agent
        /// inventory item. The caller is responsible for J2K-encoding the image data (LibreMetaverse
        /// has no rendering/snapshot pipeline of its own); per THUMBNAIL_SNAPSHOT_DIM_MAX/MIN in the
        /// reference viewer the image should be between 64x64 and 256x256.
        /// </summary>
        /// <param name="inventoryID">UUID of the item or folder to set the thumbnail on</param>
        /// <param name="taskID">UUID of the task (object) the item lives in, or <see cref="UUID.Zero"/>
        /// for agent inventory items/folders</param>
        /// <param name="j2cImageData">Raw J2K-encoded thumbnail image bytes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="progress">Optional upload progress reporter</param>
        /// <returns>The new thumbnail asset UUID, or null if the capability is unavailable or the
        /// upload fails</returns>
        public async Task<UUID?> UploadThumbnailAsync(UUID inventoryID, UUID taskID, byte[] j2cImageData,
            CancellationToken cancellationToken = default, IProgress<HttpCapsClient.ProgressReport>? progress = null)
        {
            if (j2cImageData == null) throw new ArgumentNullException(nameof(j2cImageData));
            cancellationToken.ThrowIfCancellationRequested();

            var cap = GetCapabilityURI("InventoryThumbnailUpload");
            if (cap == null) { return null; }

            var metadata = new OSDMap();
            if (taskID != UUID.Zero)
            {
                metadata["item_id"] = OSD.FromUUID(inventoryID);
                metadata["task_id"] = OSD.FromUUID(taskID);
            }
            else if (Store != null && Store.Contains(inventoryID) && Store[inventoryID] is InventoryFolder)
            {
                metadata["category_id"] = OSD.FromUUID(inventoryID);
            }
            else
            {
                metadata["item_id"] = OSD.FromUUID(inventoryID);
            }

            try
            {
                var metaResult = await PostCapAsync(cap, metadata, cancellationToken, progress).ConfigureAwait(false);
                if (!(metaResult is OSDMap metaMap) || !metaMap.ContainsKey("uploader"))
                {
                    Logger.Warn("InventoryThumbnailUpload response contained no uploader URL.", Client);
                    return null;
                }

                var uploaderUri = new Uri(metaMap["uploader"].AsString());
                var uploadResult = await PostBytesAsync(uploaderUri, "application/octet-stream", j2cImageData,
                    cancellationToken, progress).ConfigureAwait(false);

                if (!(uploadResult is OSDMap resultMap) || resultMap["state"].AsString() != "complete")
                {
                    Logger.Warn($"InventoryThumbnailUpload did not complete for {inventoryID}: {uploadResult}", Client);
                    return null;
                }

                return resultMap["new_asset"].AsUUID();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error($"Failed uploading thumbnail for {inventoryID}", ex, Client);
                return null;
            }
        }

        public async Task GiveFolderAsync(UUID folderID, string folderName, UUID recipient, bool doEffect,
            CancellationToken cancellationToken = default)
        {
            var folders = new List<InventoryFolder>();
            var items = new List<InventoryItem>();

            // Call the internal async recursive function defined in main file
            await GetInventoryRecursiveAsync(folderID, Client.Self.AgentID, folders, items, cancellationToken).ConfigureAwait(false);

            var total_contents = folders.Count + items.Count;

            // check for too many items.
            if (total_contents > MAX_GIVE_ITEMS)
            {
                Logger.Info($"Cannot give more than {MAX_GIVE_ITEMS} items in a single inventory transfer.");
                return;
            }
            if (items.Count == 0)
            {
                Logger.Info("No items to transfer.");
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
                Client.Network.CurrentSim?.ID ?? UUID.Zero,
                bucket);

            if (doEffect)
            {
                Client.Self.BeamEffect(Client.Self.AgentID, recipient, Vector3d.Zero,
                    Client.Settings.DefaultEffectColor, 1f, UUID.Random());
            }

            // Remove from store if items were no copy
            var store = Store;
            if (store != null)
            {
                foreach (var item in items)
                {
                    if (store.TryGetValue(item.UUID, out var node) && node is InventoryItem invItem && (invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                    {
                        store.RemoveNodeFor(invItem);
                    }
                }
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
            var visited = new HashSet<UUID>();
            await GetInventoryRecursiveInternalAsync(folderID, owner, cats, items, visited, 0, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal implementation of recursive inventory fetch with loop detection
        /// </summary>
        private async Task GetInventoryRecursiveInternalAsync(UUID folderID, UUID owner,
            List<InventoryFolder> cats, List<InventoryItem> items, HashSet<UUID> visited, int depth,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Defensive loop detection: Check if we've already visited this folder
            if (!visited.Add(folderID))
            {
                Logger.Warn($"Inventory loop detected: Folder {folderID} has already been visited. Circular parentage reference detected at depth {depth}.", Client);
                return;
            }

            // Defensive depth limit to prevent stack overflow even without circular refs
            const int maxDepth = 512;
            if (depth > maxDepth)
            {
                Logger.Warn($"Inventory traversal exceeded maximum depth of {maxDepth} at folder {folderID}. Possible circular reference or extremely deep hierarchy.", Client);
                return;
            }

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
                        await GetInventoryRecursiveInternalAsync(folder.UUID, owner, cats, items, visited, depth + 1, cancellationToken).ConfigureAwait(false);
                        break;
                    case InventoryItem _:
                        var fetched = await FetchItemAsync(entry.UUID, owner, cancellationToken).ConfigureAwait(false);
                        if (fetched != null)
                            items.Add(fetched);
                        break;
                    default: // shouldn't happen
                        Logger.Error("Retrieved inventory contents of invalid type");
                        break;
                }
            }
        }

        /// <summary>
        /// Result for CreateItemFromAsset operations containing detailed status
        /// </summary>
        public class CreateItemFromAssetResult
        {
            /// <summary>True if operation completed successfully</summary>
            public bool Success { get; set; }
            /// <summary>Human-readable status returned from server (eg 'upload' or 'complete')</summary>
            public string? Status { get; set; }
            /// <summary>UUID of the created inventory item (if available)</summary>
            public UUID ItemID { get; set; }
            /// <summary>UUID of the created asset (if available)</summary>
            public UUID AssetID { get; set; }
            /// <summary>Any exception that occurred during the operation</summary>
            public Exception? Error { get; set; }
            /// <summary>Raw OSD result returned from capability calls (when available)</summary>
            public OSD? RawResult { get; set; }
        }

        /// <summary>
        /// Result returned when requesting a folder update
        /// </summary>
        public class FolderUpdateResult
        {
            /// <summary>The folder that was updated</summary>
            public UUID FolderID { get; set; }
            /// <summary>True when the folder update succeeded</summary>
            public bool Success { get; set; }
            /// <summary>Contents of the folder at the time of the update (may be null)</summary>
            public List<InventoryBase>? Contents { get; set; }
        }

        /// <summary>
        /// Async API that creates an inventory item by uploading an asset and returns a rich result object.
        /// This replaces the older callback-only flow with a Task-based result including status and IDs.
        /// </summary>
        public async Task<CreateItemFromAssetResult> CreateItemFromAssetAsync(byte[] data, string name, string description, 
            AssetType assetType, InventoryType invType, UUID folderID, Permissions permissions,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var result = new CreateItemFromAssetResult
            {
                Success = false,
                Status = "",
                ItemID = UUID.Zero,
                AssetID = UUID.Zero,
                Error = null,
                RawResult = null
            };

            var cap = GetCapabilityURI("NewFileAgentInventory", false);
            if (cap == null)
            {
                result.Status = "capability_missing";
                result.Error = new InvalidOperationException("NewFileAgentInventory capability is not currently available");
                return result;
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
                {"expected_upload_cost", OSD.FromInteger(GetUploadCostForAssetType(assetType))}
            };

            try
            {
                // Initial capability call
                var osd = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                result.RawResult = osd;
                if (osd == null)
                {
                    result.Status = "no_response";
                    return result;
                }

                var contents = osd as OSDMap;
                if (contents == null)
                {
                    result.Status = "invalid_response";
                    return result;
                }

                var status = contents.ContainsKey("state") ? contents["state"].AsString().ToLowerInvariant() : string.Empty;
                result.Status = status;

                if (status == "upload")
                {
                    // Upload the raw bytes to the provided uploader URL and re-evaluate result
                    var uploadUrl = contents.ContainsKey("uploader") ? contents["uploader"].AsString() : null;
                    if (string.IsNullOrEmpty(uploadUrl))
                    {
                        result.Status = "missing_uploader_url";
                        return result;
                    }

                    var uploadUri = new Uri(uploadUrl);
                    var uploadRes = await PostBytesAsync(uploadUri, "application/octet-stream", data, cancellationToken, progress).ConfigureAwait(false);
                    result.RawResult = uploadRes;
                    if (uploadRes is OSDMap uploadMap)
                    {
                        contents = uploadMap;
                        status = contents.ContainsKey("state") ? contents["state"].AsString().ToLowerInvariant() : status;
                        result.Status = status;
                    }
                }

                if (status == "complete")
                {
                    if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
                    {
                        result.ItemID = contents["new_inventory_item"].AsUUID();
                        result.AssetID = contents["new_asset"].AsUUID();

                        // Request full update on the item so local store stays in sync
                        try
                        {
                            RequestFetchInventory(result.ItemID, Client.Self.AgentID, cancellationToken);
                        }
                        catch { /* best-effort */ }

                        result.Success = true;
                        return result;
                    }

                    result.Status = "missing_ids";
                    return result;
                }

                // Non-success status from server
                result.Success = false;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                result.Status = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Returns true when the current account is permitted to upload large textures (2048×2048 or larger).
        /// Requires a Premium or higher membership (<see cref="AccountLevelBenefits.PremiumAccess"/> &gt; 0)
        /// and the <c>NewFileAgentInventoryVariablePrice</c> capability from the current simulator.
        /// Check this before calling <see cref="CreateItemFromAssetVariablePriceAsync"/>.
        /// </summary>
        public bool CanUploadLargeTextures =>
            Client.Self.Benefits.PremiumAccess > 0 &&
            GetCapabilityURI("NewFileAgentInventoryVariablePrice") != null;

        /// <summary>
        /// Uploads an asset using the <c>NewFileAgentInventoryVariablePrice</c> capability, which supports
        /// high-resolution textures (2048×2048 and 4096×4096) available to Premium/Premium Plus members.
        /// The server quotes the upload price before charging; the optional <paramref name="confirmCost"/>
        /// delegate can be used to inspect or reject the quoted fee.
        /// </summary>
        /// <param name="data">Raw JPEG2000 asset bytes.</param>
        /// <param name="name">Inventory item name.</param>
        /// <param name="description">Inventory item description.</param>
        /// <param name="assetType">Asset type (typically <see cref="AssetType.Texture"/>).</param>
        /// <param name="invType">Inventory type (typically <see cref="InventoryType.Texture"/>).</param>
        /// <param name="folderID">Destination folder UUID.</param>
        /// <param name="permissions">Permissions to set on the new item.</param>
        /// <param name="confirmCost">
        /// Optional delegate called with the server-quoted upload price in L$. Return <c>true</c> to proceed
        /// with the upload, <c>false</c> to cancel. If <c>null</c>, the upload always proceeds.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional upload progress reporter.</param>
        /// <returns>
        /// A <see cref="CreateItemFromAssetResult"/> with the new item UUID and asset UUID on success.
        /// </returns>
        public async Task<CreateItemFromAssetResult> CreateItemFromAssetVariablePriceAsync(
            byte[] data, string name, string description,
            AssetType assetType, InventoryType invType, UUID folderID, Permissions permissions,
            Func<int, bool>? confirmCost = null,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            var result = new CreateItemFromAssetResult
            {
                Success = false,
                Status = "",
                ItemID = UUID.Zero,
                AssetID = UUID.Zero,
                Error = null,
                RawResult = null
            };

            if (Client.Self.Benefits.PremiumAccess <= 0)
            {
                result.Status = "membership_required";
                result.Error = new InvalidOperationException(
                    "Large texture uploads require Premium or higher membership " +
                    "(Benefits.PremiumAccess is 0 for this account).");
                return result;
            }

            var cap = GetCapabilityURI("NewFileAgentInventoryVariablePrice", false);
            if (cap == null)
            {
                result.Status = "capability_missing";
                result.Error = new InvalidOperationException(
                    "NewFileAgentInventoryVariablePrice capability is not currently available");
                return result;
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
                {"next_owner_mask", OSD.FromInteger((int) permissions.NextOwnerMask)}
            };

            try
            {
                // Step 1 — ask the server for the upload price
                var osd = await PostCapAsync(cap, query, cancellationToken, progress).ConfigureAwait(false);
                result.RawResult = osd;
                if (osd is not OSDMap contents)
                {
                    result.Status = "invalid_response";
                    return result;
                }

                var state = contents.ContainsKey("state") ? contents["state"].AsString() : string.Empty;
                result.Status = state;

                if (state != "confirm_upload")
                {
                    result.Status = $"unexpected_state:{state}";
                    return result;
                }

                var uploadPrice = contents.ContainsKey("upload_price") ? contents["upload_price"].AsInteger() : 0;
                var rsvpUrl = contents.ContainsKey("rsvp") ? contents["rsvp"].AsUri() : null;

                if (rsvpUrl == null || rsvpUrl.ToString() == "about:blank")
                {
                    result.Status = "missing_rsvp_url";
                    return result;
                }

                // Step 2 — let the caller approve the quoted price
                if (confirmCost != null && !confirmCost(uploadPrice))
                {
                    result.Status = "cost_rejected";
                    result.Error = new OperationCanceledException(
                        $"Upload cancelled: quoted price {uploadPrice} L$ was rejected by confirmCost delegate");
                    return result;
                }

                // Step 3 — POST bytes to rsvp URL; server charges the fee and creates the asset
                var uploadRes = await PostBytesAsync(rsvpUrl, "application/octet-stream", data, cancellationToken, progress)
                    .ConfigureAwait(false);
                result.RawResult = uploadRes;

                if (uploadRes is not OSDMap uploadMap)
                {
                    result.Status = "invalid_upload_response";
                    return result;
                }

                state = uploadMap.ContainsKey("state") ? uploadMap["state"].AsString() : string.Empty;
                result.Status = state;

                if (state == "complete" &&
                    uploadMap.ContainsKey("new_inventory_item") && uploadMap.ContainsKey("new_asset"))
                {
                    result.ItemID = uploadMap["new_inventory_item"].AsUUID();
                    result.AssetID = uploadMap["new_asset"].AsUUID();

                    try { RequestFetchInventory(result.ItemID, Client.Self.AgentID, cancellationToken); }
                    catch { /* best-effort */ }

                    result.Success = true;
                    return result;
                }

                result.Success = false;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                result.Status = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Request an inventory folder contents update and await the corresponding FolderUpdated event.
        /// Returns a richer result containing success and the folder contents when available.
        /// </summary>
        public async Task<FolderUpdateResult> RequestFolderUpdateAsync(UUID folderID, UUID ownerID, bool fetchFolders = true, 
            bool fetchItems = true, InventorySortOrder order = InventorySortOrder.ByName, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<FolderUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? sender, FolderUpdatedEventArgs e)
            {
                if (e.FolderID == folderID)
                {
                    // build best-effort result; do not block the event handler
                    _ = Task.Run(async () =>
                    {
                        List<InventoryBase>? contents = null;
                        try
                        {
                            contents = await RequestFolderContentsAsync(folderID, ownerID, fetchFolders, fetchItems, order, cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignore, we'll still return the success flag
                        }

                        var res = new FolderUpdateResult
                        {
                            FolderID = folderID,
                            Success = e.Success,
                            Contents = contents
                        };

                        tcs.TrySetResult(res);
                    }, cancellationToken);
                }
            }

            FolderUpdated += Handler;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                try
                {
                    // Trigger the fetch which will cause the server to emit FolderUpdated
                    _ = RequestFolderContentsAsync(folderID, ownerID, fetchFolders, fetchItems, order, cancellationToken);

                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    FolderUpdated -= Handler;
                }
            }
        }

        /// <summary>
        /// Await the next inventory object offer. Returns the event args for the offer.
        /// </summary>
        public async Task<InventoryObjectOfferedEventArgs> WaitForNextInventoryOfferAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<InventoryObjectOfferedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? sender, InventoryObjectOfferedEventArgs e)
            {
                tcs.TrySetResult(e);
            }

            InventoryObjectOffered += Handler;

            using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
            {
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    InventoryObjectOffered -= Handler;
                }
            }
        }

        /// <summary>
        /// Result for copy items operations
        /// </summary>
        public class CopyItemsResult
        {
            /// <summary>True if the request was accepted/submitted successfully</summary>
            public bool Success { get; set; }
            /// <summary>Collection of items copied back by the server (might be null or partial)</summary>
            public List<InventoryBase>? CopiedItems { get; set; }
            /// <summary>If an exception occurred during the operation, populated with the error</summary>
            public Exception? Error { get; set; }
        }

        /// <summary>
        /// Async-first wrapper for copying multiple items that returns a rich result object.
        /// For legacy LLUDP path this will await the per-item copy callbacks and collect returned items
        /// until all expected items are received or the operation times out/cancelled. For AISv3 path
        /// the method will POST the request and return success if the POST succeeded (server may still
        /// emit callbacks which are not correlated to this method).
        /// </summary>
        public async Task<CopyItemsResult> RequestCopyItemsWithResultAsync(List<UUID> items, List<UUID> targetFolders, List<string> newNames,
            UUID oldOwnerID, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (targetFolders == null) throw new ArgumentNullException(nameof(targetFolders));
            if (items.Count != targetFolders.Count || (newNames != null && items.Count != newNames.Count))
                throw new ArgumentException("All list arguments must have an equal number of entries");

            var result = new CopyItemsResult { Success = false, CopiedItems = new List<InventoryBase>(), Error = null };

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

                    // POST and return success if it completes without throwing
                    await PostCapAsync(invCap, payload, cancellationToken).ConfigureAwait(false);
                    result.Success = true;
                    return result;
                }
                catch (Exception ex)
                {
                    // Fall through to legacy LLUDP path on error
                    result.Error = ex;
                }
            }

            // Legacy LLUDP path: register a callback to capture copied items
            var tcs = new TaskCompletionSource<CopyItemsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var collected = new List<InventoryBase>();
            uint callbackId = 0;

            // Our wrapper callback will be registered with RegisterItemsCopiedCallback
            ItemCopiedCallback cb = (invBase) =>
            {
                try
                {
                    if (invBase == null)
                    {
                        // Server indicated failure/timeout for this callback id
                        // If we have any collected items return partial success, otherwise failure
                        if (collected.Count > 0)
                        {
                            tcs.TrySetResult(new CopyItemsResult { Success = true, CopiedItems = new List<InventoryBase>(collected) });
                        }
                        else
                        {
                            tcs.TrySetResult(new CopyItemsResult { Success = false, CopiedItems = null });
                        }
                        return;
                    }

                    collected.Add(invBase);

                    // If we've collected all expected items, complete
                    if (collected.Count >= items.Count)
                    {
                        tcs.TrySetResult(new CopyItemsResult { Success = true, CopiedItems = new List<InventoryBase>(collected) });
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            callbackId = RegisterItemsCopiedCallback(cb);

            try
            {
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
                        CallbackID = callbackId,
                        NewFolderID = targetFolders[i],
                        OldAgentID = oldOwnerID,
                        OldItemID = items[i],
                        NewName = (newNames != null && !string.IsNullOrEmpty(newNames[i]))
                            ? Utils.StringToBytes(newNames[i]!)
                            : Utils.EmptyBytes
                    };
                }

                Client.Network.SendPacket(copy);

                using (cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false))
                {
                    // Await completion or cancellation
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                return new CopyItemsResult { Success = false, CopiedItems = null, Error = ex };
            }
        }
    }
}

