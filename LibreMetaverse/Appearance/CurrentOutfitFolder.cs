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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse.Appearance
{
    /// <summary>
    /// Manages the Current Outfit Folder (COF) which tracks what an avatar is currently wearing.
    /// Provides high-level operations for managing wearables and attachments.
    /// </summary>
    public class CurrentOutfitFolder : IDisposable
    {
        #region Fields

        protected GridClient client;
        private readonly CompositeCurrentOutfitPolicy policy = new CompositeCurrentOutfitPolicy();
        private bool initializedCOF = false;
        private bool disposed = false;

        /// <summary>
        /// The Current Outfit Folder inventory folder
        /// </summary>
        public InventoryFolder COF { get; private set; }

        /// <summary>
        /// Maximum number of clothing layers that can be worn simultaneously
        /// </summary>
        public int MaxClothingLayers => 60;

        #endregion Fields

        #region Construction and disposal

        /// <summary>
        /// Creates a new Current Outfit Folder manager
        /// </summary>
        /// <param name="client">GridClient instance to use</param>
        public CurrentOutfitFolder(GridClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            RegisterClientEvents(client);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) { return; }

            if (disposing)
            {
                UnregisterClientEvents(client);
            }

            disposed = true;
        }

        #endregion Construction and disposal

        #region Policies

        /// <summary>
        /// Add a policy to control attachment/detachment permissions
        /// </summary>
        /// <param name="policyToAdd">Policy to add</param>
        /// <returns>The added policy</returns>
        public ICurrentOutfitPolicy AddPolicy(ICurrentOutfitPolicy policyToAdd)
        {
            policy.AddPolicy(policyToAdd);
            return policyToAdd;
        }

        /// <summary>
        /// Remove a policy
        /// </summary>
        /// <param name="policyToRemove">Policy to remove</param>
        public void RemovePolicy(ICurrentOutfitPolicy policyToRemove)
        {
            policy.RemovePolicy(policyToRemove);
        }

        #endregion

        #region Event handling

        private void RegisterClientEvents(GridClient client)
        {
            client.Network.SimChanged += Network_OnSimChanged;
            client.Inventory.FolderUpdated += Inventory_FolderUpdated;
            client.Objects.KillObject += Objects_KillObject;
        }

        private void UnregisterClientEvents(GridClient client)
        {
            client.Network.SimChanged -= Network_OnSimChanged;
            client.Inventory.FolderUpdated -= Inventory_FolderUpdated;
            client.Objects.KillObject -= Objects_KillObject;

            initializedCOF = false;
        }

        private void Inventory_FolderUpdated(object sender, FolderUpdatedEventArgs e)
        {
            if (COF == null)
            {
                return;
            }

            if (e.FolderID == COF.UUID && e.Success)
            {
                if (client.Inventory.Store.TryGetValue<InventoryFolder>(COF.UUID, out var newCOF))
                {
                    COF = newCOF;
                }

                var cofLinks = GetCurrentOutfitLinks().Result;

                var items = new Dictionary<UUID, UUID>();
                foreach (var link in cofLinks)
                {
                    items[link.AssetUUID] = client.Self.AgentID;
                }

                if (items.Count > 0)
                {
                    client.Inventory.RequestFetchInventory(items);
                }
            }
        }

        private void Objects_KillObject(object sender, KillObjectEventArgs e)
        {
            if (client.Network.CurrentSim != e.Simulator)
            {
                return;
            }

            if (client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(e.ObjectLocalID, out var prim))
            {
                var invItemId = GetAttachmentItemID(prim);
                if (invItemId != UUID.Zero)
                {
                    RemoveLinksToByActualId(new List<UUID>() { invItemId }).Wait();
                }
            }
        }

        private void Network_OnSimChanged(object sender, SimChangedEventArgs e)
        {
            client.Network.CurrentSim.Caps.CapabilitiesReceived += Simulator_OnCapabilitiesReceived;
        }

        private void Simulator_OnCapabilitiesReceived(object sender, CapabilitiesReceivedEventArgs e)
        {
            e.Simulator.Caps.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived;

            if (e.Simulator == client.Network.CurrentSim && !initializedCOF)
            {
                InitializeCurrentOutfitFolder().Wait();
            }
        }

        #endregion Event handling

        #region Private methods

        private async Task<bool> InitializeCurrentOutfitFolder(CancellationToken cancellationToken = default)
        {
            COF = await client.Appearance.GetCurrentOutfitFolder(cancellationToken);

            if (COF == null)
            {
                return false;
            }

            await client.Inventory.RequestFolderContents(COF.UUID, client.Self.AgentID,
                true, true, InventorySortOrder.ByDate, cancellationToken);

            Logger.Info($"Initialized Current Outfit Folder with UUID {COF.UUID} v.{COF.Version}", client);

            initializedCOF = COF != null;
            return initializedCOF;
        }

        private bool IsBodyPart(InventoryItem item)
        {
            var realItem = ResolveInventoryLink(item);

            if (!(realItem is InventoryWearable wearable))
            {
                return false;
            }

            return wearable.WearableType == WearableType.Shape ||
                   wearable.WearableType == WearableType.Skin ||
                   wearable.WearableType == WearableType.Eyes ||
                   wearable.WearableType == WearableType.Hair;
        }

        /// <summary>
        /// Return links found in Current Outfit Folder
        /// </summary>
        /// <returns>List of <see cref="InventoryItem"/> that can be part of appearance (attachments, wearables)</returns>
        /// <param name="cancellationToken"></param>
        public async Task<List<InventoryItem>> GetCurrentOutfitLinks(CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                await InitializeCurrentOutfitFolder(cancellationToken);
            }

            if (COF == null)
            {
                Logger.Warn($"COF is null", client);
                return new List<InventoryItem>();
            }

            if (!client.Inventory.Store.TryGetNodeFor(COF.UUID, out var cofNode))
            {
                Logger.Warn("Failed to find COF node in inventory store", client);
                return new List<InventoryItem>();
            }

            List<InventoryBase> cofContents;
            if (cofNode.NeedsUpdate)
            {
                cofContents = await client.Inventory.RequestFolderContents(
                    COF.UUID, COF.OwnerID, true, true, InventorySortOrder.ByName,
                    cancellationToken);
            }
            else
            {
                cofContents = client.Inventory.Store.GetContents(COF);
            }

            var cofLinks = cofContents.OfType<InventoryItem>()
                .Where(n => n.IsLink()).ToList();

            return cofLinks;
        }

        protected async Task AddLink(InventoryItem item, CancellationToken cancellationToken = default)
        {
            if (item is InventoryWearable wearableItem && !IsBodyPart(item))
            {
                var layer = 0;
                var description = $"{(int)wearableItem.WearableType}{layer:00}";
                await AddLink(item, description, cancellationToken);
            }
            else
            {
                await AddLink(item, string.Empty, cancellationToken);
            }
        }

        protected async Task AddLink(InventoryItem item, string newDescription, CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                Logger.Warn("Cannot add link; COF hasn't been initialized.", client);
                return;
            }

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);
            if (cofLinks.Find(itemLink => itemLink.AssetUUID == item.UUID) == null)
            {
                client.Inventory.CreateLink(COF.UUID, item.UUID, item.Name,
                    newDescription, item.InventoryType, UUID.Random(),
                    (success, newItem) =>
                    {
                        if (success)
                        {
                            client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID, cancellationToken);
                        }
                    },
                    cancellationToken
                );
            }
        }

        protected async Task RemoveLinksToByActualId(IEnumerable<UUID> actualItemIdsToRemoveLinksTo, CancellationToken cancellationToken = default)
        {
            var actualItemIdsSet = actualItemIdsToRemoveLinksTo.ToArray();

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);
            var linkIdsToRemove = cofLinks
                .Where(n => n.IsLink() && actualItemIdsSet.Contains(n.ActualUUID))
                .Select(n => n.UUID)
                .Distinct()
                .ToList();

            if (linkIdsToRemove.Count > 0)
            {
                await client.Inventory.RemoveItemsAsync(linkIdsToRemove, cancellationToken);
            }
        }

        protected async Task RemoveLinksTo(List<InventoryItem> actualItemsToRemoveLinksTo, CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                Logger.Warn("Cannot remove link; COF hasn't been initialized.", client);
                return;
            }

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);

            var actualItemIDsToRemoveLinksTo = actualItemsToRemoveLinksTo
                .Select(n => n.ActualUUID);

            var linkIdsToRemove = cofLinks
                .Where(n => n.IsLink() && actualItemIDsToRemoveLinksTo.Contains(n.ActualUUID))
                .Select(n => n.UUID)
                .Distinct()
                .ToList();

            if (linkIdsToRemove.Count > 0)
            {
                await client.Inventory.RemoveItemsAsync(linkIdsToRemove, cancellationToken);
            }
        }

        #endregion Private methods

        #region Public methods

        /// <summary>
        /// Determines if we can attach the specified object
        /// </summary>
        /// <param name="item">Object to check</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if we are able to attach this object</returns>
        public async Task<bool> CanAttachItem(InventoryItem item, CancellationToken cancellationToken = default)
        {
            var trashFolderId = client.Inventory.FindFolderForType(FolderType.Trash);
            var rootFolderId = client.Inventory.FindFolderForType(FolderType.Root);

            var realItem = ResolveInventoryLink(item);
            if (realItem == null)
            {
                Logger.Warn($"Cannot attach an item because the link could not be resolved.", client);
                return false;
            }

            if (!policy.CanAttach(realItem))
            {
                return false;
            }

            var isInTrash = await IsObjectDescendentOf(realItem, trashFolderId, cancellationToken);
            if (isInTrash)
            {
                Logger.Warn("Cannot attach an item that is currently in the trash.", client);
                return false;
            }

            var isInPlayerInventory = await IsObjectDescendentOf(realItem, rootFolderId, cancellationToken);
            if (!isInPlayerInventory)
            {
                Logger.Warn("Cannot attach an item that is not in your inventory.", client);
                return false;
            }

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);

            if (cofLinks.FirstOrDefault(n => n.ActualUUID == item.ActualUUID) != null)
            {
                return false;
            }

            if (item is InventoryObject)
            {
                var numAttachedObjects = cofLinks
                    .Count(n => n is InventoryObject);

                if (numAttachedObjects + 1 >= client.Self.Benefits.AttachmentLimit)
                {
                    return false;
                }
            }
            else if (item is InventoryWearable)
            {
                var numClothingLayers = cofLinks
                    .Count(n => n is InventoryWearable);

                numClothingLayers++;

                if (numClothingLayers + 1 >= MaxClothingLayers)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines if we can detach the specified object
        /// </summary>
        /// <param name="item">Object to check</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if we are able to detach this object</returns>
        public async Task<bool> CanDetachItem(InventoryItem item, CancellationToken cancellationToken = default)
        {
            if (!policy.CanDetach(item))
            {
                return false;
            }

            var realItem = ResolveInventoryLink(item);

            if (realItem == null)
            {
                return false;
            }

            if (IsBodyPart(realItem))
            {
                return false;
            }

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);
            if (cofLinks.FirstOrDefault(n => n.ActualUUID == realItem.UUID) == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempt to attach an object to a specific attachment point
        /// </summary>
        /// <param name="item">Item to be attached</param>
        /// <param name="point">Attachment point</param>
        /// <param name="replace">Replace existing attachment at that point first?</param>
        /// <param name="cancellationToken"></param>
        public async Task Attach(InventoryItem item, AttachmentPoint point, bool replace, CancellationToken cancellationToken = default)
        {
            if (!await CanAttachItem(item, cancellationToken))
            {
                return;
            }

            client.Appearance.Attach(item, point, replace);

            await policy.ReportItemChange(new List<InventoryItem>() { item }, new List<InventoryItem>(), cancellationToken);
            await AddLink(item, cancellationToken);
        }

        /// <summary>
        /// Remove attachment
        /// </summary>
        /// <param name="item">Inventory item to be detached</param>
        /// <param name="cancellationToken"></param>
        public async Task Detach(InventoryItem item, CancellationToken cancellationToken = default)
        {
            if (!await CanDetachItem(item, cancellationToken))
            {
                return;
            }

            client.Appearance.Detach(item);

            await policy.ReportItemChange(new List<InventoryItem>(), new List<InventoryItem>() { item }, cancellationToken);
            await RemoveLinksTo(new List<InventoryItem>() { item }, cancellationToken);
        }

        /// <summary>
        /// Gets a list of worn items of a specific wearable type
        /// </summary>
        /// <param name="type">Specific wearable type to find</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of all worn items of the specified wearable type</returns>
        public async Task<List<InventoryItem>> GetWornAt(WearableType type, CancellationToken cancellationToken = default)
        {
            var wornItemsByAssetId = new Dictionary<UUID, InventoryItem>();

            var cofLinks = await GetCurrentOutfitLinks(cancellationToken);
            foreach (var link in cofLinks)
            {
                var realItem = ResolveInventoryLink(link);
                if (realItem == null)
                {
                    continue;
                }

                if (!(realItem is InventoryWearable wearable))
                {
                    continue;
                }

                if (wearable.WearableType == type)
                {
                    wornItemsByAssetId[wearable.AssetUUID] = wearable;
                }
            }

            return wornItemsByAssetId.Values.ToList();
        }

        #endregion Public methods

        #region Utility methods

        /// <summary>
        /// Get the inventory ID of an attached prim
        /// </summary>
        /// <param name="prim">Prim to check</param>
        /// <returns>Inventory ID of the object. UUID.Zero if not found</returns>
        public static UUID GetAttachmentItemID(Primitive prim)
        {
            if (prim.NameValues == null)
            {
                return UUID.Zero;
            }

            var attachmentId = prim.NameValues
                .Where(n => n.Name == "AttachItemID")
                .Select(n => new UUID(n.Value.ToString()))
                .FirstOrDefault();

            return attachmentId;
        }

        /// <summary>
        /// Retrieves the linked item from <paramref name="itemLink"/> if it is a link.
        /// </summary>
        /// <param name="itemLink">The link to an inventory item</param>
        /// <returns>The original inventory item, or null if the link could not be resolved</returns>
        public InventoryItem ResolveInventoryLink(InventoryItem itemLink)
        {
            if (itemLink.AssetType != AssetType.Link)
            {
                return itemLink;
            }

            if (!client.Inventory.Store.TryGetValue<InventoryItem>(itemLink.AssetUUID, out var inventoryItem))
            {
                client.Inventory.RequestFetchInventory(itemLink.AssetUUID, itemLink.OwnerID);

                if (!client.Inventory.Store.TryGetValue<InventoryItem>(itemLink.AssetUUID, out inventoryItem))
                {
                    return null;
                }
            }

            return inventoryItem;
        }

        /// <summary>
        /// Retrieves the parent of <paramref name="item"/>
        /// </summary>
        /// <param name="item">Item to retrieve the parent of</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The parent of <paramref name="item"/>, or null if item has no parent or parent does not exist</returns>
        public async Task<InventoryBase> FetchParent(InventoryBase item, CancellationToken cancellationToken = default)
        {
            if (item.ParentUUID == UUID.Zero)
            {
                return null;
            }

            if (!client.Inventory.Store.TryGetNodeFor(item.ParentUUID, out var parent))
            {
                var fetchedParent = await client.Inventory.FetchItemHttpAsync(item.ParentUUID, item.OwnerID, cancellationToken);
                return fetchedParent;
            }

            return parent.Data;
        }

        /// <summary>
        /// Determines if inventory item <paramref name="item"/> is a descendant of inventory folder <paramref name="parentId"/>
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <param name="parentId">ID of the folder to check</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if <paramref name="item"/> exists as a child, or sub-child of folder <paramref name="parentId"/></returns>
        public async Task<bool> IsObjectDescendentOf(InventoryBase item, UUID parentId, CancellationToken cancellationToken = default)
        {
            const int kArbitraryDepthLimit = 255;

            if (parentId == UUID.Zero)
            {
                return false;
            }

            var parentItr = item;
            for (var i = 0; i < kArbitraryDepthLimit; ++i)
            {
                if (parentItr.ParentUUID == parentId)
                {
                    return true;
                }

                parentItr = await FetchParent(parentItr, cancellationToken);
                if (parentItr == null)
                {
                    return false;
                }
            }

            return false;
        }

        #endregion
    }
}
