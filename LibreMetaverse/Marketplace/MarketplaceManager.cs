/*
 * Copyright (c) 2025-2026, Sjofn LLC.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Marketplace
{
    /// <summary>
    /// Manages communication with the Second Life Marketplace (SLM) merchant-outbox REST API.
    /// Provides listing fetch, create, delete, activate, and deactivate operations,
    /// and fires events when the local listing cache changes.
    /// </summary>
    /// <remarks>
    /// Accessed via the region's "DirectDelivery" capability (see
    /// LLMarketplaceData::getSLMConnectURL in the SL C++ viewer) -- there is no standalone,
    /// session-independent REST endpoint. Routes are appended directly to the capability URL:
    /// "/listings" (list/create), "/listing/{id}" (update/delete), "/associate_inventory/{id}".
    /// </remarks>
    public class MarketplaceManager
    {
        private readonly GridClient _client;

        // Cache: listing-folder UUID → listing data
        private readonly ConcurrentDictionary<UUID, MarketplaceListing> _byFolderUUID = new();
        // Cache: listing ID → listing data
        private readonly ConcurrentDictionary<int, MarketplaceListing> _byListingId = new();

        #region Events

        private readonly object _syncedLock = new();
        private EventHandler<MarketplaceListingsSyncedEventArgs>? _listingsSynced;

        /// <summary>Raised after all listings are successfully fetched from the backend.</summary>
        public event EventHandler<MarketplaceListingsSyncedEventArgs> ListingsSynced
        {
            add { lock (_syncedLock) { _listingsSynced += value; } }
            remove { lock (_syncedLock) { _listingsSynced -= value; } }
        }

        private readonly object _changedLock = new();
        private EventHandler<MarketplaceListingChangedEventArgs>? _listingChanged;

        /// <summary>Raised when a single listing is created, updated, or deleted.</summary>
        public event EventHandler<MarketplaceListingChangedEventArgs> ListingChanged
        {
            add { lock (_changedLock) { _listingChanged += value; } }
            remove { lock (_changedLock) { _listingChanged -= value; } }
        }

        private readonly object _errorLock = new();
        private EventHandler<MarketplaceErrorEventArgs>? _error;

        /// <summary>Raised when a Marketplace API call fails.</summary>
        public event EventHandler<MarketplaceErrorEventArgs> Error
        {
            add { lock (_errorLock) { _error += value; } }
            remove { lock (_errorLock) { _error -= value; } }
        }

        #endregion

        /// <summary>Read-only snapshot of all cached listings, keyed by listing-folder UUID.</summary>
        public IReadOnlyDictionary<UUID, MarketplaceListing> ListingsByFolder => _byFolderUUID;

        /// <summary>Read-only snapshot of all cached listings, keyed by listing ID.</summary>
        public IReadOnlyDictionary<int, MarketplaceListing> ListingsById => _byListingId;

        public MarketplaceManager(GridClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches all listings for the current agent from the SLM backend
        /// and updates the local cache.
        /// </summary>
        public async Task FetchListingsAsync(CancellationToken ct = default)
        {
            try
            {
                var cap = GetCapabilityUrl();
                if (cap == null) { return; }

                using var request = BuildRequest(HttpMethod.Get, $"{cap}/listings");
                using var response = await _client.HttpCapsClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var listings = ParseListingsArray(json);
                ReplaceCache(listings);
                _listingsSynced?.Invoke(this, new MarketplaceListingsSyncedEventArgs(listings));
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _error?.Invoke(this, new MarketplaceErrorEventArgs("Failed to fetch listings.", ex));
            }
        }

        /// <summary>
        /// Creates a new Marketplace listing associated with <paramref name="listingFolderUUID"/>.
        /// </summary>
        /// <param name="listingFolderUUID">UUID of the inventory listing folder</param>
        /// <param name="versionFolderUUID">UUID of the version folder inside the listing folder</param>
        /// <param name="name">Listing name (typically the listing folder's inventory name)</param>
        /// <param name="countOnHand">Number of deliverable copies currently in stock</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<MarketplaceListing?> CreateListingAsync(UUID listingFolderUUID, UUID versionFolderUUID,
            string name, int countOnHand = 0, CancellationToken ct = default)
        {
            try
            {
                var cap = GetCapabilityUrl();
                if (cap == null) { return null; }

                var body = new OSDMap
                {
                    ["listing"] = new OSDMap
                    {
                        ["name"] = name,
                        ["inventory_info"] = new OSDMap
                        {
                            ["listing_folder_id"] = listingFolderUUID,
                            ["version_folder_id"] = versionFolderUUID,
                            ["count_on_hand"] = countOnHand
                        }
                    }
                };
                using var request = BuildRequest(HttpMethod.Post, $"{cap}/listings", body);
                using var response = await _client.HttpCapsClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var listing = ParseSingleListing(json);
                if (listing != null)
                {
                    AddOrUpdateCache(listing);
                    _listingChanged?.Invoke(this, new MarketplaceListingChangedEventArgs(listing.ListingId, listing));
                }
                return listing;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _error?.Invoke(this, new MarketplaceErrorEventArgs("Failed to create listing.", ex));
                return null;
            }
        }

        /// <summary>
        /// Deletes a Marketplace listing by its backend <paramref name="listingId"/>.
        /// </summary>
        public async Task<bool> DeleteListingAsync(int listingId, CancellationToken ct = default)
        {
            try
            {
                var cap = GetCapabilityUrl();
                if (cap == null) { return false; }

                using var request = BuildRequest(HttpMethod.Delete, $"{cap}/listing/{listingId}");
                using var response = await _client.HttpCapsClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                RemoveFromCache(listingId);
                _listingChanged?.Invoke(this, new MarketplaceListingChangedEventArgs(listingId, null));
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _error?.Invoke(this, new MarketplaceErrorEventArgs($"Failed to delete listing {listingId}.", ex));
                return false;
            }
        }

        /// <summary>
        /// Activates (lists) a Marketplace listing, making it visible to buyers.
        /// </summary>
        public Task<bool> ActivateListingAsync(int listingId, CancellationToken ct = default)
            => SetListingStatusAsync(listingId, true, ct);

        /// <summary>
        /// Deactivates (unlists) a Marketplace listing, hiding it from buyers.
        /// </summary>
        public Task<bool> DeactivateListingAsync(int listingId, CancellationToken ct = default)
            => SetListingStatusAsync(listingId, false, ct);

        // ── Cache helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Tries to find a cached listing by its inventory folder UUID.
        /// </summary>
        public bool TryGetByFolder(UUID folderUUID, out MarketplaceListing? listing)
            => _byFolderUUID.TryGetValue(folderUUID, out listing);

        /// <summary>
        /// Tries to find a cached listing by its backend listing ID.
        /// </summary>
        public bool TryGetById(int listingId, out MarketplaceListing? listing)
            => _byListingId.TryGetValue(listingId, out listing);

        // ── Private helpers ────────────────────────────────────────────────────────

        private Uri? GetCapabilityUrl()
        {
            var cap = _client.Network?.CurrentSim?.Caps?.CapabilityURI("DirectDelivery");
            if (cap == null)
            {
                _error?.Invoke(this, new MarketplaceErrorEventArgs("DirectDelivery capability not available."));
            }
            return cap;
        }

        private async Task<bool> SetListingStatusAsync(int listingId, bool isListed, CancellationToken ct)
        {
            try
            {
                var cap = GetCapabilityUrl();
                if (cap == null) { return false; }

                // The reference viewer's updateSLMListingCoro always resends the full inventory_info
                // (listing_folder_id/version_folder_id/count_on_hand) alongside id/is_listed on every
                // PUT, not is_listed alone -- so we need the current cached values here too.
                if (!TryGetById(listingId, out var existing) || existing == null)
                {
                    _error?.Invoke(this, new MarketplaceErrorEventArgs(
                        $"Cannot set listing {listingId} status: no cached listing data. Call FetchListingsAsync first."));
                    return false;
                }

                var body = new OSDMap
                {
                    ["listing"] = new OSDMap
                    {
                        ["id"] = listingId,
                        ["is_listed"] = isListed,
                        ["inventory_info"] = new OSDMap
                        {
                            ["listing_folder_id"] = existing.ListingFolderUUID,
                            ["version_folder_id"] = existing.VersionFolderUUID,
                            ["count_on_hand"] = existing.StockCount
                        }
                    }
                };
                using var request = BuildRequest(HttpMethod.Put, $"{cap}/listing/{listingId}", body);
                using var response = await _client.HttpCapsClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var listing = ParseSingleListing(json);
                if (listing != null)
                {
                    AddOrUpdateCache(listing);
                    _listingChanged?.Invoke(this, new MarketplaceListingChangedEventArgs(listing.ListingId, listing));
                }
                return true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _error?.Invoke(this,
                    new MarketplaceErrorEventArgs($"Failed to set listing {listingId} status to is_listed={isListed}.", ex));
                return false;
            }
        }

        private static HttpRequestMessage BuildRequest(HttpMethod method, string url, OSDMap? body = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (body != null)
            {
                // preserveDefaults: SerializeJsonString omits "false"/0/empty-string values by default,
                // which would silently drop is_listed=false from a deactivate request.
                var json = OSDParser.SerializeJsonString(body, true);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private void ReplaceCache(IReadOnlyList<MarketplaceListing> listings)
        {
            _byFolderUUID.Clear();
            _byListingId.Clear();
            foreach (var l in listings) AddOrUpdateCache(l);
        }

        private void AddOrUpdateCache(MarketplaceListing listing)
        {
            _byFolderUUID[listing.ListingFolderUUID] = listing;
            _byListingId[listing.ListingId] = listing;
        }

        private void RemoveFromCache(int listingId)
        {
            if (_byListingId.TryRemove(listingId, out var listing))
                _byFolderUUID.TryRemove(listing.ListingFolderUUID, out _);
        }

        // ── JSON parsing ───────────────────────────────────────────────────────────

        private static List<MarketplaceListing> ParseListingsArray(string json)
        {
            var result = new List<MarketplaceListing>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var root = OSDParser.DeserializeJson(json) as OSDMap;
            if (root == null) return result;

            if (root["listings"] is OSDArray array)
            {
                foreach (var item in array)
                {
                    if (item is OSDMap map)
                    {
                        var l = ParseListingMap(map);
                        if (l != null) result.Add(l);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses the response to a create (POST /listings) or update (PUT /listing/{id}) call.
        /// Both wrap their result in a "listings" array (see createSLMListingCoro /
        /// updateSLMListingCoro in the reference viewer) -- not a singular "listing" object.
        /// </summary>
        private static MarketplaceListing? ParseSingleListing(string json)
        {
            var listings = ParseListingsArray(json);
            return listings.Count > 0 ? listings[0] : null;
        }

        private static MarketplaceListing? ParseListingMap(OSDMap map)
        {
            if (!map.ContainsKey("id")) return null;

            var listing = new MarketplaceListing
            {
                ListingId = map["id"].AsInteger(),
                Status = map.ContainsKey("is_listed") && map["is_listed"].AsBoolean()
                    ? MarketplaceListingStatus.Listed
                    : MarketplaceListingStatus.Unlisted,
                EditUrl = map.ContainsKey("edit_url") ? map["edit_url"].AsString() : null,
                LastSyncUtc = DateTime.UtcNow
            };

            if (map["inventory_info"] is OSDMap invInfo)
            {
                if (invInfo.ContainsKey("listing_folder_id"))
                    listing.ListingFolderUUID = invInfo["listing_folder_id"].AsUUID();
                if (invInfo.ContainsKey("version_folder_id"))
                    listing.VersionFolderUUID = invInfo["version_folder_id"].AsUUID();
                if (invInfo.ContainsKey("count_on_hand"))
                    listing.StockCount = invInfo["count_on_hand"].AsInteger();
            }

            return listing;
        }
    }
}
