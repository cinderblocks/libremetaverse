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
using System.Threading.Tasks;
using NUnit.Framework;
using LibreMetaverse.Marketplace;
using LibreMetaverse.Tests.TestHelpers;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the SLM merchant-outbox REST API, accessed via the region's "DirectDelivery"
    /// capability. Wire format verified against the reference viewer's
    /// LLMarketplaceData::createSLMListingCoro/updateSLMListingCoro/getSLMListingsCoro
    /// (llmarketplacefunctions.cpp): listing_folder_id/version_folder_id/count_on_hand live under
    /// a nested "inventory_info" object, activation state is the "is_listed" boolean (not a
    /// "listing_status" string), and there is no title/description/price field at all -- those are
    /// edited on the Marketplace website, not through this API.
    /// </summary>
    [TestFixture]
    public class MarketplaceManagerTests
    {
        private const string CapBase = "http://test.invalid/direct-delivery";

        // Fixed UUIDs used in JSON payloads so assertions can verify roundtrip parsing.
        private const string FolderUUID1 = "11111111-1111-1111-1111-111111111111";
        private const string VersionUUID1 = "22222222-2222-2222-2222-222222222222";
        private const string FolderUUID2 = "33333333-3333-3333-3333-333333333333";
        private const string VersionUUID2 = "44444444-4444-4444-4444-444444444444";

        private FakeGridClient _client;
        private MarketplaceManager _manager;

        private static string Url(string path) => $"{CapBase}/{path}";

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("DirectDelivery", new Uri(CapBase));
            _manager = new MarketplaceManager(_client);
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private void AddResponse(string path, HttpStatusCode status, string json)
            => _client.AddHttpResponse(new Uri(Url(path)), status, json, "application/json");

        // ── FetchListingsAsync ────────────────────────────────────────────────────

        [Test]
        public async Task FetchListingsAsync_ListingsArrayResponse_PopulatesBothCaches()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101,\"is_listed\":true,\"edit_url\":\"http://mp.sl.com/edit/101\"," +
                "\"inventory_info\":{" +
                $"\"listing_folder_id\":\"{FolderUUID1}\",\"version_folder_id\":\"{VersionUUID1}\",\"count_on_hand\":3" +
                "}}]}");

            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById.ContainsKey(101), Is.True);
            Assert.That(_manager.ListingsByFolder.ContainsKey(new UUID(FolderUUID1)), Is.True);

            var listing = _manager.ListingsById[101];
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Listed));
            Assert.That(listing.StockCount, Is.EqualTo(3));
            Assert.That(listing.VersionFolderUUID, Is.EqualTo(new UUID(VersionUUID1)));
            Assert.That(listing.EditUrl, Is.EqualTo("http://mp.sl.com/edit/101"));
        }

        [Test]
        public async Task FetchListingsAsync_UnlistedEntry_ParsesAsUnlisted()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":102,\"is_listed\":false," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID2}\"}}" + "}]}");

            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById[102].Status, Is.EqualTo(MarketplaceListingStatus.Unlisted));
        }

        [Test]
        public async Task FetchListingsAsync_FiresListingsSyncedEvent()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\"}}" + "}]}");

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
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\"}}" + "}]}");
            await _manager.FetchListingsAsync();
            Assert.That(_manager.ListingsById.ContainsKey(101), Is.True);

            _client.AddHttpResponse(new Uri(Url("listings")), HttpStatusCode.OK,
                "{\"listings\":[{\"id\":202," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID2}\"}}" + "}]}", "application/json");
            await _manager.FetchListingsAsync();

            Assert.That(_manager.ListingsById.ContainsKey(101), Is.False);
            Assert.That(_manager.ListingsById.ContainsKey(202), Is.True);
        }

        // ── CreateListingAsync ────────────────────────────────────────────────────

        [Test]
        public async Task CreateListingAsync_SendsNestedInventoryInfoNotFlatUuidField()
        {
            // Create/update responses wrap results in a "listings" array, same as the list-fetch
            // response -- not a singular "listing" object (verified against createSLMListingCoro).
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":202," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID2}\"}}" + "}]}");

            var folderUUID = new UUID(FolderUUID2);
            var versionUUID = new UUID(VersionUUID2);
            var result = await _manager.CreateListingAsync(folderUUID, versionUUID, "My Listing", 5);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ListingId, Is.EqualTo(202));

            var (method, uri, body) = _client.CapturedRequests[0];
            Assert.That(method, Is.EqualTo(System.Net.Http.HttpMethod.Post));
            Assert.That(uri.ToString(), Is.EqualTo(Url("listings")));
            Assert.That(body, Does.Contain("\"name\":\"My Listing\""));
            Assert.That(body, Does.Contain($"\"listing_folder_id\":\"{FolderUUID2}\""));
            Assert.That(body, Does.Contain($"\"version_folder_id\":\"{VersionUUID2}\""));
            Assert.That(body, Does.Contain("\"count_on_hand\":5"));
            Assert.That(body, Does.Not.Contain("listing_folder_uuid"));
        }

        [Test]
        public async Task CreateListingAsync_FiresListingChangedEvent()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":202," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID2}\"}}" + "}]}");

            MarketplaceListingChangedEventArgs changedArgs = null;
            _manager.ListingChanged += (s, e) => changedArgs = e;

            await _manager.CreateListingAsync(new UUID(FolderUUID2), new UUID(VersionUUID2), "Name");

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

            var result = await _manager.CreateListingAsync(UUID.Random(), UUID.Random(), "Name");

            Assert.That(result, Is.Null);
            Assert.That(errorArgs, Is.Not.Null);
        }

        // ── DeleteListingAsync ────────────────────────────────────────────────────

        [Test]
        public async Task DeleteListingAsync_UsesSingularListingRoute()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\"}}" + "}]}");
            await _manager.FetchListingsAsync();
            Assert.That(_manager.TryGetById(101, out _), Is.True);

            AddResponse("listing/101", HttpStatusCode.OK, string.Empty);
            var result = await _manager.DeleteListingAsync(101);

            Assert.That(result, Is.True);
            Assert.That(_manager.TryGetById(101, out _), Is.False);
            Assert.That(_manager.TryGetByFolder(new UUID(FolderUUID1), out _), Is.False);
        }

        [Test]
        public async Task DeleteListingAsync_HttpError_ReturnsFalseAndFiresErrorEvent()
        {
            AddResponse("listing/999", HttpStatusCode.NotFound, string.Empty);

            MarketplaceErrorEventArgs errorArgs = null;
            _manager.Error += (s, e) => errorArgs = e;

            var result = await _manager.DeleteListingAsync(999);

            Assert.That(result, Is.False);
            Assert.That(errorArgs, Is.Not.Null);
        }

        // ── ActivateListingAsync / DeactivateListingAsync ────────────────────────

        private async Task SeedCachedListingAsync(int listingId, string folderUuid, string versionUuid, int countOnHand)
        {
            AddResponse("listings", HttpStatusCode.OK,
                $"{{\"listings\":[{{\"id\":{listingId},\"is_listed\":false," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{folderUuid}\",\"version_folder_id\":\"{versionUuid}\"," +
                $"\"count_on_hand\":{countOnHand}}}}}]}}");
            await _manager.FetchListingsAsync();
        }

        [Test]
        public async Task ActivateListingAsync_SendsIsListedTrueAndCachedInventoryInfoToSingularRoute()
        {
            await SeedCachedListingAsync(101, FolderUUID1, VersionUUID1, 7);

            AddResponse("listing/101", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101,\"is_listed\":true," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\",\"version_folder_id\":\"{VersionUUID1}\",\"count_on_hand\":7}}" + "}]}");

            var result = await _manager.ActivateListingAsync(101);

            Assert.That(result, Is.True);
            var (method, uri, body) = _client.CapturedRequests[_client.CapturedRequests.Count - 1];
            Assert.That(method, Is.EqualTo(System.Net.Http.HttpMethod.Put));
            Assert.That(uri.ToString(), Is.EqualTo(Url("listing/101")));
            Assert.That(body, Does.Contain("\"is_listed\":true"));
            Assert.That(body, Does.Contain("\"id\":101"));
            // Reference viewer's updateSLMListingCoro always resends the full inventory_info too.
            Assert.That(body, Does.Contain($"\"listing_folder_id\":\"{FolderUUID1}\""));
            Assert.That(body, Does.Contain($"\"version_folder_id\":\"{VersionUUID1}\""));
            Assert.That(body, Does.Contain("\"count_on_hand\":7"));

            Assert.That(_manager.TryGetById(101, out var listing), Is.True);
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Listed));
        }

        [Test]
        public async Task ActivateListingAsync_NoCachedListing_ReturnsFalseWithoutSendingRequest()
        {
            var result = await _manager.ActivateListingAsync(999);

            Assert.That(result, Is.False);
            Assert.That(_client.CapturedRequests, Is.Empty);
        }

        [Test]
        public async Task ActivateListingAsync_HttpError_ReturnsFalse()
        {
            await SeedCachedListingAsync(101, FolderUUID1, VersionUUID1, 0);
            AddResponse("listing/101", HttpStatusCode.BadRequest, string.Empty);

            var result = await _manager.ActivateListingAsync(101);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task DeactivateListingAsync_SendsIsListedFalse()
        {
            await SeedCachedListingAsync(101, FolderUUID1, VersionUUID1, 0);

            AddResponse("listing/101", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101,\"is_listed\":false," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\"}}" + "}]}");

            await _manager.DeactivateListingAsync(101);

            var (_, _, body) = _client.CapturedRequests[_client.CapturedRequests.Count - 1];
            Assert.That(body, Does.Contain("\"is_listed\":false"));

            Assert.That(_manager.TryGetById(101, out var listing), Is.True);
            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Unlisted));
        }

        // ── Cache helpers ─────────────────────────────────────────────────────────

        [Test]
        public async Task TryGetByFolder_AfterFetch_ReturnsMatchingListing()
        {
            AddResponse("listings", HttpStatusCode.OK,
                "{\"listings\":[{\"id\":101," +
                $"\"inventory_info\":{{\"listing_folder_id\":\"{FolderUUID1}\"}}" + "}]}");
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
        public void TryGetById_EmptyCache_ReturnsFalse()
        {
            Assert.That(_manager.TryGetById(9999, out _), Is.False);
        }

        // ── EventArgs properties ──────────────────────────────────────────────────

        [Test]
        public void MarketplaceListingsSyncedEventArgs_StoresListings()
        {
            var listings = new List<MarketplaceListing>
            {
                new MarketplaceListing { ListingId = 1 }
            };
            var args = new MarketplaceListingsSyncedEventArgs(listings);

            Assert.That(args.Listings, Has.Count.EqualTo(1));
            Assert.That(args.Listings[0].ListingId, Is.EqualTo(1));
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

            Assert.That(listing.Status, Is.EqualTo(MarketplaceListingStatus.Unknown));
            Assert.That(listing.LastSyncUtc, Is.EqualTo(DateTime.MinValue));
            Assert.That(listing.EditUrl, Is.Null);
        }
    }
}
