/*
 * Copyright (c) 2009-2014, Radegast Development Team
 * Copyright (c) 2019-2025, Sjofn LLC
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
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Appearance
{
    /// <summary>
    /// Provides functionality for setting an avatar's initial outfit during first login by copying a predefined outfit
    /// from the library and applying it to the current outfit folder (COF).
    /// </summary>
    /// <remarks>The InitialOutfit class is typically used during the first login process to ensure that a
    /// user starts with a specific outfit. It manages inventory folders and items related to outfits, creates necessary
    /// inventory folders if they do not exist, and applies the selected initial outfit by copying it from the library
    /// to the user's inventory. This class requires valid references to a GridClient and a CurrentOutfitFolder. Thread
    /// safety is considered when applying the outfit, as the operation is performed on a background thread.</remarks>
    public class InitialOutfit
    {
        private readonly GridClient client;
        private readonly CurrentOutfitFolder cof;
        private readonly Inventory Store;

        public InitialOutfit(GridClient client, CurrentOutfitFolder cof)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.cof = cof ?? throw new ArgumentNullException(nameof(cof));
            Store = client.Inventory.Store;
        }

        /// <summary>
        /// Progress report for initial outfit operations
        /// </summary>
        public class InitialOutfitProgress
        {
            public InitialOutfitPhase Phase { get; set; }
            public int TotalItems { get; set; }
            public int ItemsCopied { get; set; }
            public string CurrentItemName { get; set; }
            public string Message { get; set; }
        }

        public enum InitialOutfitPhase
        {
            Counting,
            Copying,
            Applying,
            Complete
        }

        public static InventoryNode FindNodeByName(InventoryNode root, string name)
        {
            if (root == null) return null;
            if (root.Data != null && root.Data.Name == name)
            {
                return root;
            }

            foreach (var node in root.Nodes.Values)
            {
                var found = FindNodeByName(node, name);
                if (found != null) return found;
            }

            return null;
        }

        [Obsolete("Use CreateFolderAsync instead", true)]
        public UUID CreateFolder(UUID parent, string name, FolderType type)
        {
            throw new NotSupportedException("Synchronous CreateFolder is removed. Use CreateFolderAsync instead.");
        }

        public Task<UUID> CreateFolderAsync(UUID parent, string name, FolderType type, CancellationToken cancellationToken = default)
        {
            // Inventory.CreateFolder is synchronous; run on a threadpool thread to avoid blocking callers
            return Task.Run(() =>
            {
                try
                {
                    return client.Inventory.CreateFolder(parent, name, type);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"CreateFolderAsync failed: {ex.Message}", client);
                    return UUID.Zero;
                }
            }, cancellationToken);
        }

        private async Task<List<InventoryBase>> FetchFolderAsync(InventoryFolder folder, CancellationToken cancellationToken = default)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    return await client.Inventory.RequestFolderContents(folder.UUID, folder.OwnerID,
                        true, true, InventorySortOrder.ByName, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"FetchFolderAsync failed for {folder?.Name}: {ex.Message}", client);
                    return new List<InventoryBase>();
                }
            }
        }

        public async Task CheckSystemFoldersAsync(CancellationToken cancellationToken = default)
        {
            // Check if we have clothing folder
            var clothingID = client.Inventory.FindFolderForType(FolderType.Clothing);
            if (clothingID == Store.RootFolder.UUID)
            {
                await CreateFolderAsync(Store.RootFolder.UUID, "Clothing", FolderType.Clothing, cancellationToken).ConfigureAwait(false);
            }

            // Check if we have trash folder
            var trashID = client.Inventory.FindFolderForType(FolderType.Trash);
            if (trashID == Store.RootFolder.UUID)
            {
                await CreateFolderAsync(Store.RootFolder.UUID, "Trash", FolderType.Trash, cancellationToken).ConfigureAwait(false);
            }
        }

        [Obsolete("Use CopyFolderAsync instead", true)]
        public UUID CopyFolder(InventoryFolder folder, UUID destination)
        {
            throw new NotSupportedException("Synchronous CopyFolder is removed. Use CopyFolderAsync instead.");
        }

        public async Task<UUID> CopyFolderAsync(InventoryFolder folder, UUID destination, CancellationToken cancellationToken = default, IProgress<InitialOutfitProgress> progress = null)
        {
            var newFolderID = await CreateFolderAsync(destination, folder.Name, folder.PreferredType, cancellationToken).ConfigureAwait(false);

            var total = await CountItemsAsync(folder, cancellationToken).ConfigureAwait(false);
            var items = await FetchFolderAsync(folder, cancellationToken).ConfigureAwait(false);
            var itemsCopied = 0;
            progress?.Report(new InitialOutfitProgress { Phase = InitialOutfitPhase.Counting, TotalItems = total, ItemsCopied = 0 });

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item is InventoryItem)
                {
                    try
                    {
                        progress?.Report(new InitialOutfitProgress { Phase = InitialOutfitPhase.Copying, TotalItems = total, ItemsCopied = itemsCopied, CurrentItemName = item.Name });
                        var success = await CopyItemAsync(item.UUID, newFolderID, item.Name, item.OwnerID, TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                        itemsCopied++;
                        progress?.Report(new InitialOutfitProgress { Phase = InitialOutfitPhase.Copying, TotalItems = total, ItemsCopied = itemsCopied, CurrentItemName = item.Name });

                        if (success)
                        {
                            Logger.Info($"Copied item {item.Name}", client);
                        }
                        else
                        {
                            Logger.Warn($"Failed to copy item {item.Name}", client);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Exception copying item {item.Name}: {ex.Message}", client);
                    }
                }
                else if (item is InventoryFolder inventoryFolder)
                {
                    await CopyFolderAsync(inventoryFolder, newFolderID, cancellationToken, progress).ConfigureAwait(false);
                }
            }

            // report final completion progress
            progress?.Report(new InitialOutfitProgress { Phase = InitialOutfitPhase.Complete, TotalItems = total, ItemsCopied = itemsCopied, Message = "Folder copy complete" });

            return newFolderID;
        }

        private Task<bool> CopyItemAsync(UUID itemId, UUID destFolderId, string name, UUID ownerId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Callback(InventoryBase newItem)
            {
                try { tcs.TrySetResult(newItem != null); } catch { }
            }

            try
            {
                client.Inventory.RequestCopyItem(itemId, destFolderId, name, ownerId, Callback);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            var delayTask = Task.Delay(timeout, cancellationToken);
            return Task.Run(async () =>
            {
                var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }, cancellationToken);
        }

        [Obsolete("Use SetInitialOutfitAsync instead", true)]
        public void SetInitialOutfit(string outfit)
        {
            throw new NotSupportedException("Synchronous SetInitialOutfit is removed. Use SetInitialOutfitAsync instead.");
        }

        public Task SetInitialOutfitAsync(string outfit, CancellationToken cancellationToken = default, IProgress<InitialOutfitProgress> progress = null)
        {
            return PerformInitAsync(outfit, cancellationToken, progress);
        }

        private async Task PerformInitAsync(string initialOutfitName, CancellationToken cancellationToken, IProgress<InitialOutfitProgress> progress = null)
        {
            Logger.Debug("Starting initial outfit async (first login)", client);
            var outfitFolder = FindNodeByName(Store.LibraryRootNode, initialOutfitName);

            if (outfitFolder == null)
            {
                Logger.Warn($"Initial outfit '{initialOutfitName}' not found in library", client);
                return;
            }

            await CheckSystemFoldersAsync(cancellationToken).ConfigureAwait(false);

            var clothingFolderId = client.Inventory.FindFolderForType(FolderType.Clothing);
            UUID newClothingFolder = await CopyFolderAsync((InventoryFolder)outfitFolder.Data, clothingFolderId, cancellationToken, progress).ConfigureAwait(false);

            if (newClothingFolder == UUID.Zero)
            {
                Logger.Warn("Failed to create new clothing folder for initial outfit", client);
                return;
            }

            // Wear the outfit by replacing COF
            try
            {
                await cof.ReplaceOutfit(newClothingFolder, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply initial outfit: {ex.Message}", ex, client);
            }

            Logger.Debug("Initial outfit async (first login) exiting", client);
        }

        private async Task<int> CountItemsAsync(InventoryFolder folder, CancellationToken cancellationToken = default)
        {
            var count = 0;
            var contents = await FetchFolderAsync(folder, cancellationToken).ConfigureAwait(false);
            foreach (var item in contents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is InventoryItem) count++;
                else if (item is InventoryFolder f)
                {
                    count += await CountItemsAsync(f, cancellationToken).ConfigureAwait(false);
                }
            }
            return count;
        }
    }
}
