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

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
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
                Logger.Log("Cannot give more than 42 items in a single inventory transfer.", Helpers.LogLevel.Info);
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
    }
}
