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

namespace LibreMetaverse.Appearance
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
        private volatile bool initializedCOF = false;
        private bool disposed = false;

        // Protects access to client, COF and event registration
        private readonly object stateLock = new object();

        private InventoryFolder? _cof;
        /// <summary>
        /// The Current Outfit Folder inventory folder
        /// </summary>
        public InventoryFolder? COF
        {
            get { lock (stateLock) { return _cof; } }
            private set { lock (stateLock) { _cof = value; } }
        }

        /// <summary>
        /// Maximum number of clothing layers that can be worn simultaneously
        /// </summary>
        public int MaxClothingLayers => 60;

        private readonly SemaphoreSlim cofInitLock = new SemaphoreSlim(1, 1);
        private Task<bool>? cofInitTask;

        private CancellationTokenSource _sessionCts = new CancellationTokenSource();

        #endregion Fields

        #region Construction and disposal

        /// <summary>
        /// Creates a new Current Outfit Folder manager
        /// </summary>
        /// <param name="client">GridClient instance to use</param>
        public CurrentOutfitFolder(GridClient? client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            RegisterClientEvents(this.client);
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
            lock (stateLock)
            {
                if (disposed) { return; }

                if (disposing)
                {
                    UnregisterClientEvents(client);
                    _sessionCts.Cancel();
                    _sessionCts.Dispose();
                }

                disposed = true;
            }
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
            lock (stateLock)
            {
                client.Network.SimChanged += Network_OnSimChanged;
                client.Inventory.FolderUpdated += Inventory_FolderUpdated;
                client.Objects.KillObject += Objects_KillObject;
            }
        }

        private void UnregisterClientEvents(GridClient client)
        {
            lock (stateLock)
            {
                try { client.Network.SimChanged -= Network_OnSimChanged; } catch { }
                try { client.Inventory.FolderUpdated -= Inventory_FolderUpdated; } catch { }
                try { client.Objects.KillObject -= Objects_KillObject; } catch { }

                initializedCOF = false;
            }
        }

        /// <summary>
        /// Atomically update the GridClient used by this instance and re-register events.
        /// </summary>
        /// <param name="newClient"></param>
        protected void UpdateClient(GridClient? newClient)
        {
            if (newClient == null) throw new ArgumentNullException(nameof(newClient));

            lock (stateLock)
            {
                if (ReferenceEquals(client, newClient)) return;

                UnregisterClientEvents(client);
                client = newClient;
                RegisterClientEvents(client);
                // Reset COF so it will be reinitialized for the new client
                _cof = null;
                initializedCOF = false;
            }
        }
        private async void Inventory_FolderUpdated(object? sender, FolderUpdatedEventArgs e)
        {
            try
            {
                if (COF == null)
                {
                    return;
                }

                if (e.FolderID == COF.UUID && e.Success)
                {
                    var store = client.Inventory?.Store;
                    if (store != null && store.TryGetValue<InventoryFolder>(COF.UUID, out InventoryFolder? newCOF) && newCOF != null)
                    {
                        COF = newCOF;
                    }

                    var cofLinks = await GetCurrentOutfitLinksAsync().ConfigureAwait(false);

                    var items = new Dictionary<UUID, UUID>();
                    foreach (var link in cofLinks)
                    {
                        items[link.AssetUUID] = client.Self.AgentID;
                    }

                    if (items.Count > 0)
                    {
                        var inv = client.Inventory;
                        if (inv != null)
                        {
                            await inv.RequestFetchInventoryAsync(items, _sessionCts.Token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in Inventory_FolderUpdated: " + ex.Message, ex, client);
            }
        }

        private void Objects_KillObject(object? sender, KillObjectEventArgs e)
        {
            // Do NOT modify COF links here.
            //
            // KillObject fires for worn attachments both on explicit user-initiated detaches
            // AND during teleports (the departing sim destroys every rezzed object).  Removing
            // COF links in response to KillObject during a teleport would permanently delete
            // the links from the inventory server, causing attachments to be missing after the
            // agent arrives in the destination sim.
            //
            // The SL viewer (llagent.cpp / LLVOAvatarSelf) only modifies COF links from
            // deliberate user actions (Detach, ReplaceOutfit, AddToOutfit, etc.) and from the
            // server-driven BulkUpdateInventory / MoveInventoryItem callbacks that accompany a
            // real server-side detach — never from ObjectKill packets alone.
        }

        private void Network_OnSimChanged(object? sender, SimChangedEventArgs e)
        {
            // Reset COF initialization state so the new sim re-fetches the folder.
            // The COF UUID itself is stable across teleports (it lives in the agent's
            // inventory, not in the sim), but the COF version number and folder contents
            // must be re-queried from the inventory service after each region crossing.
            // We only reset the volatile flag; the semaphore-guarded cofInitTask is cleared
            // under cofInitLock so concurrent init callers see a clean slate.
            initializedCOF = false;
            cofInitLock.Wait();
            try
            {
                cofInitTask = null;
            }
            finally
            {
                cofInitLock.Release();
            }

            // Cancel any outstanding prefetch requests from the previous sim session
            // and issue a fresh CTS for the new session.
            var oldCts = Interlocked.Exchange(ref _sessionCts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();

            var sim = client.Network.CurrentSim;
            if (sim?.Caps != null)
            {
                sim.Caps.CapabilitiesReceived += Simulator_OnCapabilitiesReceived;
            }
        }

        private async void Simulator_OnCapabilitiesReceived(object? sender, CapabilitiesReceivedEventArgs e)
        {
            try
            {
                e.Simulator.Caps?.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived;

                var currentSim = client.Network.CurrentSim;
                if (e.Simulator == currentSim && !initializedCOF)
                {
                    await InitializeCurrentOutfitFolder().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in Simulator_OnCapabilitiesReceived: " + ex.Message, ex, client);
            }
        }

        #endregion Event handling

        #region Private methods

        private async Task<bool> InitializeCurrentOutfitFolderInternal(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Trace("COF initialization: requesting current outfit folder", client);

                var previousCOF = COF;
                COF = await client.Appearance.GetCurrentOutfitFolderAsync(cancellationToken).ConfigureAwait(false);

                if (COF == null)
                {
                    Logger.Warn("COF initialization: Appearance.GetCurrentOutfitFolderAsync returned null", client);
                    initializedCOF = false;
                    return false;
                }

                await client.Inventory.RequestFolderContentsAsync(COF.UUID, client.Self.AgentID,
                    true, true, InventorySortOrder.ByDate, cancellationToken).ConfigureAwait(false);

                bool isNewOrChanged = previousCOF == null || previousCOF.UUID != COF.UUID || previousCOF.Version != COF.Version;
                if (isNewOrChanged)
                    Logger.Info($"Initialized Current Outfit Folder with UUID {COF.UUID} v.{COF.Version}", client);
                else
                    Logger.Debug($"Current Outfit Folder re-confirmed: UUID {COF.UUID} v.{COF.Version} (unchanged)", client);

                initializedCOF = true;
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("COF initialization cancelled", client);
                initializedCOF = false;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("COF initialization failed: " + ex.Message, ex, client);
                initializedCOF = false;
                return false;
            }
        }

        private async Task<bool> InitializeCurrentOutfitFolder(CancellationToken cancellationToken = default)
        {
            if (initializedCOF)
            {
                return true;
            }

            Logger.Trace("COF initialization requested", client);

            // If another initialization is in progress, await it
            var existing = cofInitTask;
            if (existing != null)
            {
                Logger.Trace("COF initialization: awaiting existing initialization task", client);
                try
                {
                    return await existing.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"COF initialization: existing task failed with {ex.Message}; attempting new init", client);
                    // fall through and attempt to re-init
                }
            }

            await cofInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (initializedCOF)
                {
                    return true;
                }

                if (cofInitTask == null)
                {
                    cofInitTask = InitializeCurrentOutfitFolderInternal(cancellationToken);
                }
            }
            finally
            {
                cofInitLock.Release();
            }

            try
            {
                var result = await cofInitTask.ConfigureAwait(false);
                if (!result)
                {
                    Logger.Warn("COF initialization task completed but did not initialize COF", client);
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("COF initialization task was cancelled", client);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"COF initialization task failed: {ex.Message}", ex, client);
                // Clear task on failure so future calls can retry initialization
                cofInitTask = null;
                return false;
            }
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
        public async Task<List<InventoryItem>> GetCurrentOutfitLinksAsync(CancellationToken cancellationToken = default)
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

            if (client.Inventory?.Store == null)
            {
                Logger.Warn("Inventory store not initialized, cannot get COF links.", client);
                return new List<InventoryItem>();
            }

            if (!client.Inventory.Store.TryGetNodeFor(COF.UUID, out var cofNode))
            {
                Logger.Warn("Failed to find COF node in inventory store", client);
                return new List<InventoryItem>();
            }

            List<InventoryBase> cofContents;
            if (cofNode!.NeedsUpdate)
            {
                cofContents = await client.Inventory.RequestFolderContentsAsync(
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

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
            if (cofLinks.Find(itemLink => itemLink.AssetUUID == item.UUID) == null)
            {
                var newLink = await client.Inventory.CreateLinkAsync(COF.UUID, item.UUID, item.Name,
                    newDescription, item.InventoryType, UUID.Random(), cancellationToken).ConfigureAwait(false);
                if (newLink != null)
                    _ = client.Inventory.RequestFetchInventoryAsync(newLink.UUID, newLink.OwnerID, cancellationToken);
            }
        }

        internal string GetCofLinkDescription(InventoryItem item)
        {
            if (item is InventoryWearable wearable && !IsBodyPart(item))
                return $"{(int)wearable.WearableType}00";
            return string.Empty;
        }

        /// <summary>
        /// Creates COF links for multiple items in a single AIS3 request when available,
        /// skipping items already linked. Falls back to sequential creation for non-AIS3.
        /// </summary>
        protected async Task AddLinks(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                Logger.Warn("Cannot add links; COF hasn't been initialized.", client);
                return;
            }

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
            var existingAssetIds = new HashSet<UUID>(cofLinks.Select(l => l.AssetUUID));

            var linksToCreate = items
                .Where(item => !existingAssetIds.Contains(item.UUID))
                .Select(item => ((InventoryBase)item, GetCofLinkDescription(item)))
                .ToList();

            if (linksToCreate.Count == 0) return;

            await client.Inventory.CreateLinksAsync(COF.UUID, linksToCreate, null, cancellationToken).ConfigureAwait(false);
        }

        protected async Task RemoveLinksToByActualId(IEnumerable<UUID> actualItemIdsToRemoveLinksTo, CancellationToken cancellationToken = default)
        {
            var actualItemIdsSet = actualItemIdsToRemoveLinksTo.ToArray();

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
            var linkIdsToRemove = cofLinks
                .Where(n => n.IsLink() && actualItemIdsSet.Contains(n.ResolvedItemID))
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

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);

            var actualItemIDsToRemoveLinksTo = actualItemsToRemoveLinksTo
                .Select(n => n.ResolvedItemID);

            var linkIdsToRemove = cofLinks
                .Where(n => n.IsLink() && actualItemIDsToRemoveLinksTo.Contains(n.ResolvedItemID))
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
        public async Task<bool> CanAttachItemAsync(InventoryItem item, CancellationToken cancellationToken = default)
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

            var isInTrash = await IsObjectDescendentOfAsync(realItem, trashFolderId, cancellationToken);
            if (isInTrash)
            {
                Logger.Warn("Cannot attach an item that is currently in the trash.", client);
                return false;
            }

            var isInPlayerInventory = await IsObjectDescendentOfAsync(realItem, rootFolderId, cancellationToken);
            if (!isInPlayerInventory)
            {
                Logger.Warn("Cannot attach an item that is not in your inventory.", client);
                return false;
            }

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);

            if (cofLinks.FirstOrDefault(n => n.ResolvedItemID == item.ResolvedItemID) != null)
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
        public async Task<bool> CanDetachItemAsync(InventoryItem item, CancellationToken cancellationToken = default)
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

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
            if (cofLinks.FirstOrDefault(n => n.ResolvedItemID == realItem.UUID) == null)
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
        public async Task AttachAsync(InventoryItem item, AttachmentPoint point, bool replace, CancellationToken cancellationToken = default)
        {
            if (!await CanAttachItemAsync(item, cancellationToken))
            {
                return;
            }

            client.Appearance.Attach(item, point, replace);

            await policy.ReportItemChangeAsync(new List<InventoryItem>() { item }, new List<InventoryItem>(), cancellationToken);
            await AddLink(item, cancellationToken);
        }

        /// <summary>
        /// Remove attachment
        /// </summary>
        /// <param name="item">Inventory item to be detached</param>
        /// <param name="cancellationToken"></param>
        public async Task DetachAsync(InventoryItem item, CancellationToken cancellationToken = default)
        {
            if (!await CanDetachItemAsync(item, cancellationToken))
            {
                return;
            }

            client.Appearance.Detach(item);

            await policy.ReportItemChangeAsync(new List<InventoryItem>(), new List<InventoryItem>() { item }, cancellationToken);
            await RemoveLinksTo(new List<InventoryItem>() { item }, cancellationToken);
        }

        /// <summary>
        /// Gets a list of worn items of a specific wearable type
        /// </summary>
        /// <param name="type">Specific wearable type to find</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of all worn items of the specified wearable type</returns>
        public async Task<List<InventoryItem>> GetWornAtAsync(WearableType type, CancellationToken cancellationToken = default)
        {
            var wornItemsByAssetId = new Dictionary<UUID, InventoryItem>();

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
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

        /// <summary>
        /// Replaces the current outfit and updates COF links accordingly
        /// </summary>
        /// <param name="newOutfitFolderId">Folder ID containing the new outfit</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True on success</returns>
        public async Task<bool> ReplaceOutfitAsync(UUID newOutfitFolderId, CancellationToken cancellationToken = default)
        {
            const string generalErrorMessage = "Try refreshing your inventory or clearing your cache.";

            if (client.Inventory?.Store?.RootFolder == null)
            {
                Logger.Warn("Inventory store not initialized, cannot modify outfit.", client);
                return false;
            }

            var trashFolderId = client.Inventory.FindFolderForType(FolderType.Trash);
            var rootFolderId = client.Inventory.Store.RootFolder.UUID;

            var newOutfit = await client.Inventory.RequestFolderContentsAsync(
                newOutfitFolderId,
                client.Self.AgentID,
                true,
                true,
                InventorySortOrder.ByName,
                cancellationToken
            );
            if (newOutfit == null)
            {
                Logger.Warn($"Failed to request contents of replacement outfit folder. {generalErrorMessage}", client);
                return false;
            }

            if (client.Inventory?.Store == null || !client.Inventory.Store.TryGetNodeFor(newOutfitFolderId, out var newOutfitFolderNode) || newOutfitFolderNode?.Data == null)
            {
                Logger.Warn($"Failed to get node for replacement outfit folder. {generalErrorMessage}", client);
                return false;
            }

            var isOutfitInTrash = await IsObjectDescendentOfAsync(newOutfitFolderNode.Data, trashFolderId, cancellationToken);
            if (isOutfitInTrash)
            {
                Logger.Warn($"Cannot wear an outfit that is currently in the trash.", client);
                return false;
            }

            var isOutfitInInventory = await IsObjectDescendentOfAsync(newOutfitFolderNode.Data, rootFolderId, cancellationToken);
            if (!isOutfitInInventory)
            {
                Logger.Warn($"Cannot wear an outfit that is not currently in your inventory.", client);
                return false;
            }

            var currentOutfitFolder = await client.Appearance.GetCurrentOutfitFolderAsync(cancellationToken);
            if (currentOutfitFolder == null)
            {
                Logger.Warn($"Failed to find current outfit folder. {generalErrorMessage}", client);
                return false;
            }

            var currentOutfitContents = await client.Inventory.RequestFolderContentsAsync(
                currentOutfitFolder.UUID,
                currentOutfitFolder.OwnerID,
                true,
                true,
                InventorySortOrder.ByName,
                cancellationToken
            );
            if (currentOutfitContents == null)
            {
                Logger.Warn($"Failed to request contents of current outfit folder. {generalErrorMessage}", client);
                return false;
            }

            var newOutfitItemMap = new Dictionary<UUID, InventoryItem>();
            var existingBodypartLinks = new List<InventoryItem>();
            var bodypartsToWear = new Dictionary<WearableType, InventoryWearable>();
            var gesturesToActivate = new Dictionary<UUID, InventoryItem>();
            var numClothingLayers = 0;
            var numAttachedObjects = 0;

            var itemsBeingAdded = new Dictionary<UUID, InventoryItem>();
            var itemsBeingRemoved = new Dictionary<UUID, InventoryItem>();

            foreach (var item in newOutfit)
            {
                if (!(item is InventoryItem inventoryItem))
                {
                    continue;
                }

                if (inventoryItem.IsLink())
                {
                    continue;
                }

                var isInTrash = await IsObjectDescendentOfAsync(inventoryItem, trashFolderId, cancellationToken);
                if (isInTrash)
                {
                    continue;
                }

                var isInInventory = await IsObjectDescendentOfAsync(inventoryItem, rootFolderId, cancellationToken);
                if (!isInInventory)
                {
                    continue;
                }

                if (inventoryItem.AssetType == AssetType.Bodypart)
                {
                    if (!(item is InventoryWearable bodypartItem))
                    {
                        continue;
                    }

                    if (bodypartsToWear.ContainsKey(bodypartItem.WearableType))
                    {
                        continue;
                    }

                    bodypartsToWear[bodypartItem.WearableType] = bodypartItem;
                    continue;
                }
                else if (inventoryItem.AssetType == AssetType.Gesture)
                {
                    gesturesToActivate[inventoryItem.UUID] = inventoryItem;
                }
                else if (inventoryItem.AssetType == AssetType.Clothing)
                {
                    if (numClothingLayers >= MaxClothingLayers)
                    {
                        continue;
                    }

                    numClothingLayers++;
                }
                else if (inventoryItem.AssetType == AssetType.Object)
                {
                    if (numAttachedObjects >= client.Self.Benefits.AttachmentLimit)
                    {
                        continue;
                    }

                    ++numAttachedObjects;
                }

                itemsBeingAdded[inventoryItem.UUID] = inventoryItem;
                newOutfitItemMap[inventoryItem.UUID] = inventoryItem;
            }

            var existingLinkTargets = currentOutfitContents
                .OfType<InventoryItem>()
                .Where(n => !n.IsLink())
                .ToDictionary(k => k.UUID, v => v);
            var linksToRemove = new List<InventoryItem>();
            var gesturesToDeactivate = new HashSet<UUID>();

            foreach (var item in currentOutfitContents)
            {
                if (!(item is InventoryItem itemLink))
                {
                    continue;
                }

                if (!itemLink.IsLink())
                {
                    continue;
                }

                if (!existingLinkTargets.TryGetValue(itemLink.AssetUUID, out var realItem))
                {
                    linksToRemove.Add(itemLink);
                    continue;
                }

                if (newOutfitItemMap.ContainsKey(realItem.UUID))
                {
                    // We're already wearing the item that exists in the new outfit, don't re-add links to it
                    itemsBeingAdded.Remove(realItem.UUID);
                    continue;
                }

                if (realItem.AssetType == AssetType.Bodypart)
                {
                    existingBodypartLinks.Add(itemLink);
                    continue;
                }

                if (realItem.AssetType == AssetType.Gesture)
                {
                    if (!gesturesToActivate.ContainsKey(realItem.UUID))
                    {
                        gesturesToDeactivate.Add(realItem.UUID);
                    }
                }

                itemsBeingRemoved[realItem.UUID] = realItem;
                linksToRemove.Add(itemLink);
            }

            // Deactivate old gestures, activate new gestures
            foreach (var gestureId in gesturesToDeactivate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                client.Self.DeactivateGesture(gestureId);
            }
            foreach (var item in gesturesToActivate.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                client.Self.ActivateGesture(item.UUID, item.AssetUUID);
            }

            // Replace bodyparts, but keep old bodyparts if new outfit lacks them
            foreach (var existingLink in existingBodypartLinks)
            {
                if (existingLinkTargets.TryGetValue(existingLink.AssetUUID, out var realItem))
                {
                    if (realItem is InventoryWearable existingBodypart)
                    {
                        if (!bodypartsToWear.ContainsKey(existingBodypart.WearableType))
                        {
                            bodypartsToWear[existingBodypart.WearableType] = existingBodypart;
                            continue;
                        }
                    }

                    itemsBeingRemoved[realItem.UUID] = realItem;
                }

                linksToRemove.Add(existingLink);
            }

            // Bare minimum outfit check
            if (!bodypartsToWear.ContainsKey(WearableType.Shape) ||
                !bodypartsToWear.ContainsKey(WearableType.Skin) ||
                !bodypartsToWear.ContainsKey(WearableType.Eyes) ||
                !bodypartsToWear.ContainsKey(WearableType.Hair))
            {
                Logger.Error("New outfit must contain a Shape, Skin, Eyes, and Hair", client);
                return false;
            }

            // Clear out all existing current outfit links
            var toRemoveIds = linksToRemove
                .Select(n => n.UUID)
                .Distinct();
            await client.Inventory.RemoveItemsAsync(toRemoveIds, cancellationToken);

            // Add body parts from current outfit to new outfit if it's lacking those essential body parts
            foreach (var item in bodypartsToWear)
            {
                itemsBeingAdded.Add(item.Value.UUID, item.Value);
            }
            await AddLinks(itemsBeingAdded.Values, cancellationToken);

            // Add link to outfit folder we're putting on
            var outfitLink = await client.Inventory.CreateLinkAsync(
                currentOutfitFolder.UUID,
                newOutfitFolderNode.Data.UUID,
                newOutfitFolderNode.Data.Name,
                "",
                InventoryType.Folder,
                UUID.Random(),
                cancellationToken
            ).ConfigureAwait(false);
            if (outfitLink != null)
                _ = client.Inventory.RequestFetchInventoryAsync(outfitLink.UUID, outfitLink.OwnerID);

            // Wear new outfit
            var tcs = new TaskCompletionSource<bool>();
            void handleAppearanceSet(object? sender, AppearanceSetEventArgs e)
            {
                tcs.TrySetResult(true);
            }

            await policy.ReportItemChangeAsync(new List<InventoryItem>(), itemsBeingRemoved.Values.ToList(), cancellationToken);

            try
            {
                client.Appearance.AppearanceSet += handleAppearanceSet;
                await client.Appearance.ReplaceOutfitAsync(newOutfitItemMap.Values.ToList(), false).ConfigureAwait(false);

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000, cancellationToken));
                if (completedTask != tcs.Task)
                {
                    Logger.Error("Timed out while waiting for AppearanceSet confirmation. Are you changing outfits too quickly?", client);
                    return false;
                }
            }
            finally
            {
                client.Appearance.AppearanceSet -= handleAppearanceSet;
            }

            await policy.ReportItemChangeAsync(itemsBeingAdded.Values.ToList(), new List<InventoryItem>(), cancellationToken);
            return true;
        }

        /// <summary>
        /// Add items to current outfit
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <param name="replace">Should existing wearable of the same type be removed</param>
        /// <param name="cancellationToken"></param>
        public async Task AddToOutfitAsync(InventoryItem item, bool replace, CancellationToken cancellationToken = default)
        {
            await AddToOutfitAsync(new List<InventoryItem>(1) { item }, replace, cancellationToken);
        }

        /// <summary>
        /// Add items to current outfit
        /// </summary>
        /// <param name="requestedItemsToAdd">List of items to add</param>
        /// <param name="replace">Should existing wearable of the same type be removed</param>
        /// <param name="cancellationToken"></param>
        public async Task AddToOutfitAsync(List<InventoryItem> requestedItemsToAdd, bool replace, CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                Logger.Warn("Can't add to outfit link; COF hasn't been initialized.", client);
                return;
            }

            var inv = client.Inventory;
            if (inv?.Store?.RootFolder == null)
            {
                Logger.Warn("Inventory store not initialized, cannot modify outfit.", client);
                return;
            }

            var trashFolderId = inv.FindFolderForType(FolderType.Trash);
            var rootFolderId = inv.Store.RootFolder.UUID;

            var cofLinks = await GetCurrentOutfitLinksAsync(cancellationToken);
            var cofRealItems = new Dictionary<UUID, InventoryBase>();
            var cofLinkAssetIds = new HashSet<UUID>();
            var currentBodyparts = new Dictionary<WearableType, InventoryWearable>();
            var currentClothing = new Dictionary<WearableType, List<InventoryWearable>>();
            var currentAttachmentPoints = new Dictionary<AttachmentPoint, List<InventoryObject>>();
            var numClothingLayers = 0;
            var numAttachedObjects = 0;

            foreach (var item in cofLinks)
            {
                var realItem = ResolveInventoryLink(item) ?? item;
                if (realItem == null)
                {
                    continue;
                }

                cofRealItems[realItem.UUID] = realItem;
                cofLinkAssetIds.Add(item.AssetUUID);

                if (realItem is InventoryWearable wearable)
                {
                    if (realItem.AssetType == AssetType.Bodypart)
                    {
                        currentBodyparts[wearable.WearableType] = wearable;
                    }
                    else if (realItem.AssetType == AssetType.Clothing)
                    {
                        if (!currentClothing.TryGetValue(wearable.WearableType, out var currentWearablesOfType))
                        {
                            currentWearablesOfType = new List<InventoryWearable>();
                            currentClothing[wearable.WearableType] = currentWearablesOfType;
                            numClothingLayers++;
                        }

                        currentWearablesOfType.Add(wearable);
                    }
                }
                else if (realItem is InventoryObject inventoryObject)
                {
                    if (!currentAttachmentPoints.TryGetValue(inventoryObject.AttachPoint, out var attachedObjects))
                    {
                        attachedObjects = new List<InventoryObject>();
                        currentAttachmentPoints[inventoryObject.AttachPoint] = attachedObjects;
                    }

                    attachedObjects.Add(inventoryObject);
                    numAttachedObjects++;
                }
            }

            var itemsToRemove = new List<InventoryItem>();
            var itemsToAdd = new List<InventoryItem>();

            foreach (var item in requestedItemsToAdd)
            {
                var realItem = ResolveInventoryLink(item);
                if (realItem == null)
                {
                    continue;
                }

                var isItemInTrash = await IsObjectDescendentOfAsync(realItem, trashFolderId, cancellationToken);
                if (isItemInTrash)
                {
                    continue;
                }

                var isItemInInventory = await IsObjectDescendentOfAsync(realItem, rootFolderId, cancellationToken);
                if (!isItemInInventory)
                {
                    continue;
                }

                if (cofLinkAssetIds.Contains(realItem.UUID))
                {
                    continue;
                }
                if (itemsToAdd.FirstOrDefault(n => n.UUID == realItem.UUID) != null)
                {
                    continue;
                }

                if (realItem is InventoryWearable wearable)
                {
                    if (wearable.AssetType == AssetType.Clothing)
                    {
                        if (replace)
                        {
                            if (currentClothing.TryGetValue(wearable.WearableType, out var currentClothingOfType))
                            {
                                foreach (var clothingToRemove in currentClothingOfType)
                                {
                                    itemsToRemove.Add(clothingToRemove);
                                }
                            }
                        }
                        else
                        {
                            if (numClothingLayers >= MaxClothingLayers)
                            {
                                continue;
                            }

                            numClothingLayers++;
                        }
                    }
                    else if (wearable.AssetType == AssetType.Bodypart)
                    {
                        if (currentBodyparts.TryGetValue(wearable.WearableType, out var existingBodyPart))
                        {
                            itemsToRemove.Add(existingBodyPart);
                        }
                    }
                }
                else if (realItem.AssetType == AssetType.Gesture)
                {
                    client.Self.ActivateGesture(realItem.UUID, realItem.AssetUUID);
                }
                else if (realItem is InventoryObject objectToAdd)
                {
                    if (replace)
                    {
                        if (currentAttachmentPoints.TryGetValue(objectToAdd.AttachPoint, out var attachedObjectsToRemove))
                        {
                            foreach (var attachedObject in attachedObjectsToRemove)
                            {
                                itemsToRemove.Add(attachedObject);
                                --numAttachedObjects;
                            }
                        }
                    }

                    if (numAttachedObjects >= client.Self.Benefits.AttachmentLimit)
                    {
                        continue;
                    }

                    ++numAttachedObjects;
                }
                else
                {
                    continue;
                }

                itemsToAdd.Add(realItem);
            }

            if (itemsToRemove.Count > 0)
            {
                await RemoveLinksTo(itemsToRemove, cancellationToken);
            }

            await AddLinks(itemsToAdd, cancellationToken);

            client.Appearance.AddToOutfit(itemsToAdd, replace);
            _ = DelayedOutfitUpdateAsync(itemsToAdd, itemsToRemove, cancellationToken);
        }

        private async Task DelayedOutfitUpdateAsync(List<InventoryItem> itemsToAdd, List<InventoryItem> itemsToRemove, CancellationToken cancellationToken)
        {
            try { await Task.Delay(2000, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            try { await client.Appearance.RequestSetAppearance(true); } catch { }
            try { await policy.ReportItemChangeAsync(itemsToAdd, itemsToRemove, cancellationToken); } catch { }
        }

        /// <summary>
        /// Removes specified items from the current outfit. All COF links to these items will be removed from the COF.
        /// The specified items may either be actual items, or links to actual items. Links will be resolved to actual
        /// items internally.
        /// </summary>
        /// <param name="requestedItemsToRemove">List of items (or item links) we want to remove all links to from our COF</param>
        /// <param name="cancellationToken"></param>
        public async Task RemoveFromOutfitAsync(List<InventoryItem> requestedItemsToRemove, CancellationToken cancellationToken = default)
        {
            if (COF == null)
            {
                Logger.Warn("Can't remove from outfit; COF hasn't been initialized.", client);
                return;
            }

            var itemsToRemove = requestedItemsToRemove
                .Select(n => ResolveInventoryLink(n))
                .Where(n => n is InventoryItem it && !IsBodyPart(it) && policy.CanDetach(it))
                .Select(n => (InventoryItem)n!)
                .Distinct()
                .ToList();
            foreach (var item in itemsToRemove)
            {
                if (item.AssetType == AssetType.Gesture)
                {
                    client.Self.DeactivateGesture(item.UUID);
                }
            }

            try
            {
                await RemoveLinksTo(itemsToRemove, cancellationToken);
                await policy.ReportItemChangeAsync(new List<InventoryItem>(), itemsToRemove, cancellationToken);
            }
            finally
            {
                client.Appearance.RemoveFromOutfit(itemsToRemove);
            }
        }

        /// <summary>
        /// Removes specified item from the current outfit. Forwards to list-based RemoveFromOutfit.
        /// </summary>
        /// <param name="item">Item (or item link) we want to remove all links to from our COF</param>
        /// <param name="cancellationToken"></param>
        public async Task RemoveFromOutfitAsync(InventoryItem item, CancellationToken cancellationToken = default)
        {
            await RemoveFromOutfitAsync(new List<InventoryItem>(1) { item }, cancellationToken).ConfigureAwait(false);
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

            var attachmentValue = prim.NameValues
                .Where(n => n.Name == "AttachItemID")
                .Select(n => n.Value?.ToString())
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));

            return !string.IsNullOrEmpty(attachmentValue) ? new UUID(attachmentValue!) : UUID.Zero;
        }

        /// <summary>
        /// Retrieves the linked item from <paramref name="itemLink"/> if it is a link.
        /// </summary>
        /// <param name="itemLink">The link to an inventory item</param>
        /// <returns>The original inventory item, or null if the link could not be resolved</returns>
        public InventoryItem? ResolveInventoryLink(InventoryItem itemLink)
        {
            if (itemLink.AssetType != AssetType.Link)
            {
                return itemLink;
            }

            var store = client.Inventory?.Store;
            if (store == null)
            {
                _ = client.Inventory?.RequestFetchInventoryAsync(itemLink.AssetUUID, itemLink.OwnerID);
                return null;
            }

            if (!store.TryGetValue<InventoryItem>(itemLink.AssetUUID, out var inventoryItem))
            {
                // Fire-and-forget request for the linked item; do not block here
                if (client.Inventory != null)
                    _ = client.Inventory.RequestFetchInventoryAsync(itemLink.AssetUUID, itemLink.OwnerID);

                if (!store.TryGetValue<InventoryItem>(itemLink.AssetUUID, out inventoryItem))
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
        public async Task<InventoryBase?> FetchParentAsync(InventoryBase item, CancellationToken cancellationToken = default)
        {
            if (item.ParentUUID == UUID.Zero)
            {
                return null;
            }

            if (client.Inventory?.Store == null)
            {
                if (client.Inventory == null) return null;

                var fetchedParent = await client.Inventory.FetchItemHttpAsync(item.ParentUUID, item.OwnerID, cancellationToken);
                return fetchedParent;
            }

            if (!client.Inventory.Store.TryGetNodeFor(item.ParentUUID, out var parent))
            {
                var fetchedParent = await client.Inventory.FetchItemHttpAsync(item.ParentUUID, item.OwnerID, cancellationToken);
                return fetchedParent;
            }

            return parent!.Data;
        }

        /// <summary>
        /// Determines if inventory item <paramref name="item"/> is a descendant of inventory folder <paramref name="parentId"/>
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <param name="parentId">ID of the folder to check</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if <paramref name="item"/> exists as a child, or sub-child of folder <paramref name="parentId"/></returns>
        public async Task<bool> IsObjectDescendentOfAsync(InventoryBase item, UUID parentId, CancellationToken cancellationToken = default)
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

                parentItr = await FetchParentAsync(parentItr, cancellationToken);
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
