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
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Marketplace;
using LibreMetaverse.Tests.TestHelpers;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class MarketplaceManagerTests
    {
        private const string ApiBase = "http://test.invalid/api/1/";

        // Fixed UUIDs used in JSON payloads so assertions can verify roundtrip parsing.
        private const string FolderUUID1 = "11111111-1111-1111-1111-111111111111";
        private const string VersionUUID1 = "22222222-2222-2222-2222-222222222222";
        private const string FolderUUID2 = "33333333-3333-3333-3333-333333333333";

        private GridClient _client;
        private FakeHttpMessageHandler _handler;
        private HttpClient _httpClient;
        private MarketplaceManager _manager;

        private string AgentId => _client.Self.AgentID.ToString();

        private string Url(string path) => $"{ApiBase.TrimEnd('/')}/users/{AgentId}/{path}";

        [SetUp]
        public void SetUp()
        {
            _client = new GridClient();
            _handler = new FakeHttpMessageHandler();
            _httpClient = new HttpClient(_handler);
            _manager = new MarketplaceManager(_client, _httpClient);
            _manager.ApiBase = ApiBase;
        }

        [TearDown]
        public void TearDown()
        {
            try { _httpClient?.Dispose(); } catch { }
            try { _handler?.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
        }

        private void AddResponse(string path, HttpStatusCode status, string json)
        {
            _handler.AddResponse(new Uri(Url(path)), status, json);
        }

        // ── FetchListingsAsync ────────────────────────────────────────────────────

        [Test]
        public async Task FetchListingsAsync_ListingsArrayResponse_PopulatesBothCaches()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101,\"title\":\"Test Item\",\"description\":\"Desc\",\"price_l$\":250," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\",\"version_folder_uuid\":\"{VersionUUID1}\"," +
                "\"listing_status\":\"listed\",\"inventory_stock_size\":3," +
                "\"edit_url\":\"http://mp.sl.com/edit/101\"}]}");

            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById.ContainsKey(101), Is.True);
            Assert.That(_manager.ListingsByFolder.ContainsKey(new UUID(FolderUUID1)), Is.True);

            var listing = _manager.ListingsById[101];
            Assert.That(listing.Title, Is.EqualTo("Test Item"));
            Assert.That(listing.Description, Is.EqualTo("Desc"));
            Assert.That(listing.PriceLinden, Is.EqualTo(250));
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Listed));
            Assert.That(listing.StockCount, Is.EqualTo(3));
            Assert.That(listing.VersionFolderUUID, Is.EqualTo(new UUID(VersionUUID1)));
            Assert.That(listing.EditUrl, Is.EqualTo("http://mp.sl.com/edit/101"));
        }

        [Test]
        public async Task FetchListingsAsync_SingularListingKeyResponse_PopulatesCache()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listing\":[{\"id\":102,\"title\":\"Other\"," +
                $"\"listing_folder_uuid\":\"{FolderUUID2}\",\"listing_status\":\"unlisted\"" + "}]}");

            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById.ContainsKey(102), Is.True);
            Assert.That(_manager.ListingsById[102].Status, Is.EqualTo(MarketplaceListingStatus.Unlisted));
        }

        [Test]
        public async Task FetchListingsAsync_FiresListingsSyncedEvent()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");

            MarketplaceListingsSyncedEventArgs raisedArgs = null;
            _manager.ListingsSynced += (s, e) => raisedArgs = e;

            await _manager.FetchListingsAsync();

            Assert.That(raisedArgs, Is.Not.Null);
            Assert.That(raisedArgs.Listings, Has.Count.EqualTo(1));
            Assert.That(raisedArgs.Listings[0].ListingId, Is.EqualTo(101));
        }

        [Test]
        public async Task FetchListingsAsync_HttpError_FiresErrorEventAndLeavesEmptyCache()
        {
            AddResponse("listings", HttpStatusCode.InternalServerError, string.Empty);

            MarketplaceErrorEventArgs errorArgs = null;
            _manager.Error += (s, e) => errorArgs = e;

            await _manager.FetchListingsAsync();

            Assert.That(errorArgs, Is.Not.Null);
            Assert.That(errorArgs.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(_manager.ListingsById, Is.Empty);
        }

        [Test]
        public async Task FetchListingsAsync_EmptyListingsArray_ResultsInEmptyCache()
        {
            AddResponse("listings", HttpStatusCode.OK, "{\"listings\":[]}");

            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById, Is.Empty);
        }

        [Test]
        public async Task FetchListingsAsync_SecondCall_ReplacesCacheWithNewData()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");
            await _manager.FetchListingsAsync();
            Assert.That(_manager.ListingsById.ContainsKey(101), Is.True);

            // Second fetch returns different data
            _handler.AddResponse(new Uri(Url("listings")), HttpStatusCode.OK,
                "{\"listings\":[{\"id\":202," +
                $"\"listing_folder_uuid\":\"{FolderUUID2}\"" + "}]}");
            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById.ContainsKey(101), Is.False);
            Assert.That(_manager.ListingsById.ContainsKey(202), Is.True);
        }

        // ── CreateListingAsync ────────────────────────────────────────────────────

        [Test]
        public async Task CreateListingAsync_SuccessResponse_ReturnsParsedListingAndAddsToCache()
        {
            var folderUUID = new UUID(FolderUUID2);
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listing\":{\"id\":202," +
                $"\"listing_folder_uuid\":\"{FolderUUID2}\"," +
                "\"listing_status\":\"unlisted\",\"price_l$\":50,\"title\":\"New\",\"description\":\"\"}}");

            var result = await _manager.CreateListingAsync(folderUUID);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ListingId, Is.EqualTo(202));
            Assert.That(_manager.TryGetById(202, out _), Is.True);
            Assert.That(_manager.TryGetByFolder(folderUUID, out _), Is.True);
        }

        [Test]
        public async Task CreateListingAsync_FiresListingChangedEvent()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listing\":{\"id\":202," +
                $"\"listing_folder_uuid\":\"{FolderUUID2}\"" + "}}");

            MarketplaceListingChangedEventArgs changedArgs = null;
            _manager.ListingChanged += (s, e) => changedArgs = e;

            await _manager.CreateListingAsync(new UUID(FolderUUID2));

            Assert.That(changedArgs, Is.Not.Null);
            Assert.That(changedArgs.ListingId, Is.EqualTo(202));
            Assert.That(changedArgs.Listing, Is.Not.Null);
            Assert.That(changedArgs.Listing.ListingFolderUUID, Is.EqualTo(new UUID(FolderUUID2)));
        }

        [Test]
        public async Task CreateListingAsync_HttpError_ReturnsNullAndFiresErrorEvent()
        {
            AddResponse("listings", HttpStatusCode.BadRequest, string.Empty);

            MarketplaceErrorEventArgs errorArgs = null;
            _manager.Error += (s, e) => errorArgs = e;

            var result = await _manager.CreateListingAsync(UUID.Random());

            Assert.That(result, Is.Null);
            Assert.That(errorArgs, Is.Not.Null);
        }

        // ── DeleteListingAsync ────────────────────────────────────────────────────

        [Test]
        public async Task DeleteListingAsync_SuccessResponse_ReturnsTrueAndRemovesFromBothCaches()
        {
            // Populate cache via fetch
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");
            await _manager.FetchListingsAsync();
            Assert.That(_manager.TryGetById(101, out _), Is.True);

            AddResponse("listings/101", HttpStatusCode.OK, string.Empty);
            var result = await _manager.DeleteListingAsync(101);

            Assert.That(result, Is.True);
            Assert.That(_manager.TryGetById(101, out _), Is.False);
            Assert.That(_manager.TryGetByFolder(new UUID(FolderUUID1), out _), Is.False);
        }

        [Test]
        public async Task DeleteListingAsync_FiresListingChangedEventWithNullListing()
        {
            AddResponse("listings/101", HttpStatusCode.OK, string.Empty);

            MarketplaceListingChangedEventArgs changedArgs = null;
            _manager.ListingChanged += (s, e) => changedArgs = e;

            await _manager.DeleteListingAsync(101);

            Assert.That(changedArgs, Is.Not.Null);
            Assert.That(changedArgs.ListingId, Is.EqualTo(101));
            Assert.That(changedArgs.Listing, Is.Null);
        }

        [Test]
        public async Task DeleteListingAsync_HttpError_ReturnsFalseAndFiresErrorEvent()
        {
            AddResponse("listings/999", HttpStatusCode.NotFound, string.Empty);

            MarketplaceErrorEventArgs errorArgs = null;
            _manager.Error += (s, e) => errorArgs = e;

            var result = await _manager.DeleteListingAsync(999);

            Assert.That(result, Is.False);
            Assert.That(errorArgs, Is.Not.Null);
        }

        // ── ActivateListingAsync ──────────────────────────────────────────────────

        [Test]
        public async Task ActivateListingAsync_SuccessResponse_ReturnsTrue()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\",\"listing_status\":\"listed\"" + "}}");

            var result = await _manager.ActivateListingAsync(101);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ActivateListingAsync_SuccessResponse_SetsListingStatusToListed()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\",\"listing_status\":\"listed\"" + "}}");

            await _manager.ActivateListingAsync(101);

            Assert.That(_manager.TryGetById(101, out var listing), Is.True);
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Listed));
        }

        [Test]
        public async Task ActivateListingAsync_HttpError_ReturnsFalse()
        {
            AddResponse("listings/101", HttpStatusCode.BadRequest, string.Empty);

            var result = await _manager.ActivateListingAsync(101);

            Assert.That(result, Is.False);
        }

        // ── DeactivateListingAsync ────────────────────────────────────────────────

        [Test]
        public async Task DeactivateListingAsync_SuccessResponse_ReturnsTrue()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\",\"listing_status\":\"unlisted\"" + "}}");

            var result = await _manager.DeactivateListingAsync(101);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task DeactivateListingAsync_SuccessResponse_SetsListingStatusToUnlisted()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\",\"listing_status\":\"unlisted\"" + "}}");

            await _manager.DeactivateListingAsync(101);

            Assert.That(_manager.TryGetById(101, out var listing), Is.True);
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Unlisted));
        }

        // ── UpdateListingAsync ────────────────────────────────────────────────────

        [Test]
        public async Task UpdateListingAsync_SuccessResponse_ReturnsParsedListingAndUpdatesCache()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"," +
                "\"title\":\"Updated Title\",\"price_l$\":999,\"listing_status\":\"listed\"}}");

            var result = await _manager.UpdateListingAsync(101, title: "Updated Title", priceLinden: 999);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Title, Is.EqualTo("Updated Title"));
            Assert.That(result.PriceLinden, Is.EqualTo(999));
            Assert.That(_manager.TryGetById(101, out var cached), Is.True);
            Assert.That(cached.Title, Is.EqualTo("Updated Title"));
        }

        [Test]
        public async Task UpdateListingAsync_FiresListingChangedEvent()
        {
            AddResponse("listings/101", HttpStatusCode.OK,
                "{\"listing\":{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}}");

            MarketplaceListingChangedEventArgs changedArgs = null;
            _manager.ListingChanged += (s, e) => changedArgs = e;

            await _manager.UpdateListingAsync(101, title: "New Title");

            Assert.That(changedArgs, Is.Not.Null);
            Assert.That(changedArgs.ListingId, Is.EqualTo(101));
            Assert.That(changedArgs.Listing, Is.Not.Null);
        }

        [Test]
        public async Task UpdateListingAsync_HttpError_ReturnsNull()
        {
            AddResponse("listings/101", HttpStatusCode.BadRequest, string.Empty);

            var result = await _manager.UpdateListingAsync(101, title: "Fail");

            Assert.That(result, Is.Null);
        }

        // ── Cache helpers ─────────────────────────────────────────────────────────

        [Test]
        public async Task TryGetByFolder_AfterFetch_ReturnsMatchingListing()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");
            await _manager.FetchListingsAsync();

            var found = _manager.TryGetByFolder(new UUID(FolderUUID1), out var listing);

            Assert.That(found, Is.True);
            Assert.That(listing, Is.Not.Null);
            Assert.That(listing.ListingId, Is.EqualTo(101));
        }

        [Test]
        public async Task TryGetByFolder_MissingUUID_ReturnsFalse()
        {
            AddResponse("listings", HttpStatusCode.OK, "{\"listings\":[]}");
            await _manager.FetchListingsAsync();

            Assert.That(_manager.TryGetByFolder(UUID.Random(), out _), Is.False);
        }

        [Test]
        public async Task TryGetById_AfterFetch_ReturnsMatchingListing()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");
            await _manager.FetchListingsAsync();

            var found = _manager.TryGetById(101, out var listing);

            Assert.That(found, Is.True);
            Assert.That(listing, Is.Not.Null);
            Assert.That(listing.ListingFolderUUID, Is.EqualTo(new UUID(FolderUUID1)));
        }

        [Test]
        public void TryGetById_EmptyCache_ReturnsFalse()
        {
            Assert.That(_manager.TryGetById(9999, out _), Is.False);
        }

        [Test]
        public async Task ListingsByFolder_AfterFetch_ContainsExpectedEntry()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"listing_folder_uuid\":\"{FolderUUID1}\"" + "}]}");
            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsByFolder.ContainsKey(new UUID(FolderUUID1)), Is.True);
            Assert.That(_manager.ListingsById.ContainsKey(101), Is.True);
        }

        // ── EventArgs properties ──────────────────────────────────────────────────

        [Test]
        public void MarketplaceListingsSyncedEventArgs_StoresListings()
        {
            var listings = new List<MarketplaceListing>
            {
                new MarketplaceListing { ListingId = 1, Title = "Item A" }
            };
            var args = new MarketplaceListingsSyncedEventArgs(listings);

            Assert.That(args.Listings, Has.Count.EqualTo(1));
            Assert.That(args.Listings[0].ListingId, Is.EqualTo(1));
            Assert.That(args.Listings[0].Title, Is.EqualTo("Item A"));
        }

        [Test]
        public void MarketplaceListingChangedEventArgs_StoresListingIdAndListing()
        {
            var listing = new MarketplaceListing { ListingId = 42 };
            var args = new MarketplaceListingChangedEventArgs(42, listing);

            Assert.That(args.ListingId, Is.EqualTo(42));
            Assert.That(args.Listing, Is.SameAs(listing));
        }

        [Test]
        public void MarketplaceListingChangedEventArgs_NullListing_IsValidForDeleteScenario()
        {
            var args = new MarketplaceListingChangedEventArgs(99, null);

            Assert.That(args.ListingId, Is.EqualTo(99));
            Assert.That(args.Listing, Is.Null);
        }

        [Test]
        public void MarketplaceErrorEventArgs_StoresMessageAndException()
        {
            var ex = new InvalidOperationException("inner error");
            var args = new MarketplaceErrorEventArgs("Something failed.", ex);

            Assert.That(args.Message, Is.EqualTo("Something failed."));
            Assert.That(args.Exception, Is.SameAs(ex));
        }

        [Test]
        public void MarketplaceErrorEventArgs_WithoutException_HasNullException()
        {
            var args = new MarketplaceErrorEventArgs("No exception here.");

            Assert.That(args.Message, Is.EqualTo("No exception here."));
            Assert.That(args.Exception, Is.Null);
        }

        // ── MarketplaceListing model ──────────────────────────────────────────────

        [Test]
        public void MarketplaceListing_DefaultValues_AreCorrect()
        {
            var listing = new MarketplaceListing();

            Assert.That(listing.Title, Is.EqualTo(string.Empty));
            Assert.That(listing.Description, Is.EqualTo(string.Empty));
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Unknown));
            Assert.That(listing.LastSyncUtc, Is.EqualTo(DateTime.MinValue));
            Assert.That(listing.EditUrl, Is.Null);
        }
    }
}
