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
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Marketplace
{
    /// <summary>
    /// Manages communication with the Second Life Marketplace REST API.
    /// Provides listing fetch, create, delete, activate, and deactivate operations,
    /// and fires events when the local listing cache changes.
    /// </summary>
    /// <remarks>
    /// Authentication uses the viewer agent UUID and session ID, sent as
    /// <c>X-SLi-AgentId</c> and <c>X-SLi-Auth</c> HTTP headers — the same
    /// credential pair used by the SL C++ viewer for marketplace API calls.
    /// </remarks>
    public class MarketplaceManager
    {
        // SLM REST API base path.  Append "users/{agentUUID}/listings[/{listingId}]".
        private const string DefaultApiBase = "https://marketplace.secondlife.com/api/1/";

        private readonly GridClient _client;
        private readonly HttpClient _http;

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

        /// <summary>
        /// Base URL of the SLM REST API.  Defaults to <c>https://marketplace.secondlife.com/api/1/</c>.
        /// Can be overridden for OpenSim grids or testing.
        /// </summary>
        public string ApiBase { get; set; } = DefaultApiBase;

        public MarketplaceManager(GridClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
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
                var url = BuildUserUrl("listings");
                using var request = BuildRequest(HttpMethod.Get, url);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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
        public async Task<MarketplaceListing?> CreateListingAsync(UUID listingFolderUUID, CancellationToken ct = default)
        {
            try
            {
                var body = new OSDMap { ["listing"] = new OSDMap { ["listing_folder_uuid"] = listingFolderUUID } };
                var url = BuildUserUrl("listings");
                using var request = BuildRequest(HttpMethod.Post, url, body);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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
                var url = BuildUserUrl($"listings/{listingId}");
                using var request = BuildRequest(HttpMethod.Delete, url);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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
            => SetListingStatusAsync(listingId, "listed", ct);

        /// <summary>
        /// Deactivates (unlists) a Marketplace listing, hiding it from buyers.
        /// </summary>
        public Task<bool> DeactivateListingAsync(int listingId, CancellationToken ct = default)
            => SetListingStatusAsync(listingId, "unlisted", ct);

        /// <summary>
        /// Updates the title, description, and price of a listing.
        /// </summary>
        public async Task<MarketplaceListing?> UpdateListingAsync(int listingId,
            string? title = null, string? description = null, int? priceLinden = null,
            CancellationToken ct = default)
        {
            try
            {
                var inner = new OSDMap();
                if (title != null) inner["title"] = title;
                if (description != null) inner["description"] = description;
                if (priceLinden.HasValue) inner["price_l$"] = priceLinden.Value;

                var body = new OSDMap { ["listing"] = inner };
                var url = BuildUserUrl($"listings/{listingId}");
                using var request = BuildRequest(HttpMethod.Put, url, body);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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
                _error?.Invoke(this, new MarketplaceErrorEventArgs($"Failed to update listing {listingId}.", ex));
                return null;
            }
        }

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

        private async Task<bool> SetListingStatusAsync(int listingId, string status, CancellationToken ct)
        {
            try
            {
                var body = new OSDMap
                {
                    ["listing"] = new OSDMap { ["listing_status"] = status }
                };
                var url = BuildUserUrl($"listings/{listingId}");
                using var request = BuildRequest(HttpMethod.Put, url, body);
                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
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
                    new MarketplaceErrorEventArgs($"Failed to set listing {listingId} status to {status}.", ex));
                return false;
            }
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string url, OSDMap? body = null)
        {
            var request = new HttpRequestMessage(method, url);

            // Auth headers — same approach as the SL C++ viewer for web service calls
            request.Headers.TryAddWithoutValidation("X-SLi-AgentId", _client.Self.AgentID.ToString());
            request.Headers.TryAddWithoutValidation("X-SLi-Auth", _client.Self.SessionID.ToString());

            if (body != null)
            {
                var json = OSDParser.SerializeJsonString(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private string BuildUserUrl(string path)
            => $"{ApiBase.TrimEnd('/')}/users/{_client.Self.AgentID}/{path}";

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

            // Response may use "listing" (singular array) or "listings"
            OSD? arrayNode = null;
            if (root.ContainsKey("listings")) arrayNode = root["listings"];
            else if (root.ContainsKey("listing")) arrayNode = root["listing"];

            if (arrayNode is OSDArray array)
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
            else if (arrayNode is OSDMap singleMap)
            {
                var l = ParseListingMap(singleMap);
                if (l != null) result.Add(l);
            }

            return result;
        }

        private static MarketplaceListing? ParseSingleListing(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = OSDParser.DeserializeJson(json) as OSDMap;
            if (root == null) return null;
            var inner = root.ContainsKey("listing") ? root["listing"] as OSDMap : root;
            return inner == null ? null : ParseListingMap(inner);
        }

        private static MarketplaceListing? ParseListingMap(OSDMap map)
        {
            if (!map.ContainsKey("id")) return null;

            var listing = new MarketplaceListing
            {
                ListingId = map["id"].AsInteger(),
                Title = map.ContainsKey("title") ? map["title"].AsString() : string.Empty,
                Description = map.ContainsKey("description") ? map["description"].AsString() : string.Empty,
                PriceLinden = map.ContainsKey("price_l$") ? map["price_l$"].AsInteger() : 0,
                StockCount = map.ContainsKey("inventory_stock_size") ? map["inventory_stock_size"].AsInteger() : 0,
                EditUrl = map.ContainsKey("edit_url") ? map["edit_url"].AsString() : null,
                LastSyncUtc = DateTime.UtcNow
            };

            if (map.ContainsKey("listing_folder_uuid"))
                listing.ListingFolderUUID = map["listing_folder_uuid"].AsUUID();

            if (map.ContainsKey("version_folder_uuid"))
                listing.VersionFolderUUID = map["version_folder_uuid"].AsUUID();

            if (map.ContainsKey("listing_status"))
            {
                listing.Status = map["listing_status"].AsString().ToLowerInvariant() == "listed"
                    ? MarketplaceListingStatus.Listed
                    : MarketplaceListingStatus.Unlisted;
            }

            return listing;
        }
    }
}
