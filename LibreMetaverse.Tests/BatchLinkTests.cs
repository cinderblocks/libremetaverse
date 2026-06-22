using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Appearance;
using LibreMetaverse.Tests.TestHelpers;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Tests for batch link creation: CreateInventoryLinksAsync, CreateLinksAsync, AddLinks,
    /// and the GetCofLinkDescription helper. Verified against Alchemy viewer AIS3 implementation
    /// (indra/newview/llaisapi.cpp) for payload format and URL correctness.
    /// </summary>
    [TestFixture]
    public class BatchLinkTests
    {
        private static readonly Uri FakeCap = new Uri("http://fake-ais3.test/ais3");

        // ─── OSD helpers ───────────────────────────────────────────────────────────

        private static OSDMap MakePermissions() => new OSDMap
        {
            { "creator_id", OSD.FromUUID(UUID.Zero) },
            { "last_owner_id", OSD.FromUUID(UUID.Zero) },
            { "base_mask", 0 }, { "everyone_mask", 0 },
            { "group_mask", 0 }, { "next_owner_mask", 0 },
            { "owner_mask", 0 }, { "is_owner_group", false },
            { "group_id", OSD.FromUUID(UUID.Zero) }
        };

        private static OSDMap MakeSaleInfo() => new OSDMap
        {
            { "sale_price", 0 },
            { "sale_type", (int)SaleType.Not }
        };

        private static OSDMap MakeLinkOsd(UUID itemId, UUID parentId, UUID linkedId, string name,
            InventoryType invType = InventoryType.Wearable, AssetType assetType = AssetType.Link,
            string desc = "")
        {
            return new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "linked_id", OSD.FromUUID(linkedId) },
                { "name", name },
                { "desc", desc },
                { "agent_id", OSD.FromUUID(UUID.Random()) },
                { "inv_type", (int)invType },
                { "type", (int)assetType },
                { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "permissions", MakePermissions() },
                { "sale_info", MakeSaleInfo() }
            };
        }

        private static string MakeBatchLinksResponse(params (UUID ItemId, UUID ParentId, UUID LinkedId, string Name)[] links)
        {
            var linksMap = new OSDMap();
            for (var i = 0; i < links.Length; i++)
            {
                var (itemId, parentId, linkedId, name) = links[i];
                linksMap[i.ToString()] = MakeLinkOsd(itemId, parentId, linkedId, name);
            }
            var embedded = new OSDMap { { "links", linksMap } };
            var response = new OSDMap { { "_embedded", embedded } };
            return OSDParser.SerializeLLSDXmlString(response);
        }

        private static FakeGridClient MakeClientWithCap()
        {
            var client = new FakeGridClient();
            client.SetInventoryAndLibraryCaps(FakeCap, new Uri("http://fake-lib.test/lib/"));
            return client;
        }

        // ─── GetCofLinkDescription ─────────────────────────────────────────────────

        [Test]
        public void GetCofLinkDescription_ClothingWearable_ReturnsWearableTypeAndZeroLayer()
        {
            using var cof = new CurrentOutfitFolder(new GridClient());
            var shirt = new InventoryWearable(UUID.Random())
            {
                AssetType = AssetType.Clothing,
                WearableType = WearableType.Shirt, // value 4
                InventoryType = InventoryType.Wearable,
                Name = "Test Shirt"
            };
            Assert.That(cof.GetCofLinkDescription(shirt), Is.EqualTo($"{(int)WearableType.Shirt}00"));
        }

        [Test]
        public void GetCofLinkDescription_BodyPartShape_ReturnsEmpty()
        {
            using var cof = new CurrentOutfitFolder(new GridClient());
            var shape = new InventoryWearable(UUID.Random())
            {
                AssetType = AssetType.Bodypart,
                WearableType = WearableType.Shape,
                InventoryType = InventoryType.Wearable,
                Name = "Test Shape"
            };
            Assert.That(cof.GetCofLinkDescription(shape), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetCofLinkDescription_AttachmentObject_ReturnsEmpty()
        {
            using var cof = new CurrentOutfitFolder(new GridClient());
            var obj = new InventoryObject(UUID.Random())
            {
                AssetType = AssetType.Object,
                InventoryType = InventoryType.Object,
                Name = "Test Attachment"
            };
            Assert.That(cof.GetCofLinkDescription(obj), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GetCofLinkDescription_AllBodyPartTypes_ReturnEmpty()
        {
            using var cof = new CurrentOutfitFolder(new GridClient());
            var bodyPartTypes = new[] { WearableType.Shape, WearableType.Skin, WearableType.Eyes, WearableType.Hair };
            foreach (var wt in bodyPartTypes)
            {
                var item = new InventoryWearable(UUID.Random())
                {
                    AssetType = AssetType.Bodypart,
                    WearableType = wt,
                    InventoryType = InventoryType.Wearable,
                    Name = $"Test {wt}"
                };
                Assert.That(cof.GetCofLinkDescription(item), Is.EqualTo(string.Empty),
                    $"Expected empty description for body part type {wt}");
            }
        }

        // ─── CreateInventoryLinksAsync ─────────────────────────────────────────────

        [Test]
        public async Task CreateInventoryLinksAsync_SuccessResponse_ReturnsAllParsedLinks()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var ids = new[] { (UUID.Random(), UUID.Random(), UUID.Random(), "Link1"), (UUID.Random(), UUID.Random(), UUID.Random(), "Link2") };
            var responseBody = MakeBatchLinksResponse(ids);
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}", HttpStatusCode.OK, responseBody, "application/llsd+xml");

            var payload = new OSDMap { { "links", new OSDArray() } };
            var result = await client.AisClient.CreateInventoryLinksAsync(folderID, payload, CancellationToken.None);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(r => r.Name), Is.EquivalentTo(new[] { "Link1", "Link2" }));
        }

        [Test]
        public void CreateInventoryLinksAsync_ServerError_ThrowsHttpRequestException()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}", HttpStatusCode.InternalServerError, string.Empty);

            Assert.ThrowsAsync<HttpRequestException>(
                () => client.AisClient.CreateInventoryLinksAsync(folderID, new OSDMap(), CancellationToken.None));
        }

        [Test]
        public async Task CreateInventoryLinksAsync_NoCapability_ReturnsEmptyList()
        {
            using var client = new FakeGridClient(); // no caps set
            var result = await client.AisClient.CreateInventoryLinksAsync(UUID.Random(), new OSDMap(), CancellationToken.None);
            Assert.That(result, Is.Empty);
        }

        // ─── CreateLinksAsync (InventoryManager) ───────────────────────────────────

        [Test]
        public async Task CreateLinksAsync_EmptyList_InvokesCallbackWithTrueWithoutHttpCall()
        {
            using var client = MakeClientWithCap();
            bool? callbackResult = null;

            await client.Inventory.CreateLinksAsync(UUID.Random(),
                Enumerable.Empty<(InventoryBase, string)>(),
                result => callbackResult = result,
                CancellationToken.None);

            Assert.That(callbackResult, Is.True, "Empty list should immediately succeed");
            Assert.That(client.CapturedRequests, Is.Empty, "No HTTP call should be made for empty list");
        }

        [Test]
        public async Task CreateLinksAsync_AisAvailable_SendsSingleBatchPost()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var linkedItem1 = new InventoryWearable(UUID.Random()) { AssetType = AssetType.Clothing, InventoryType = InventoryType.Wearable, Name = "Shirt" };
            var linkedItem2 = new InventoryObject(UUID.Random()) { AssetType = AssetType.Object, InventoryType = InventoryType.Object, Name = "Object" };

            var responseXml = MakeBatchLinksResponse(
                (UUID.Random(), folderID, linkedItem1.UUID, "Shirt"),
                (UUID.Random(), folderID, linkedItem2.UUID, "Object"));
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}", HttpStatusCode.OK, responseXml, "application/llsd+xml");

            await client.Inventory.CreateLinksAsync(folderID,
                new[] { ((InventoryBase)linkedItem1, "400"), ((InventoryBase)linkedItem2, "") },
                null, CancellationToken.None);

            var posts = client.CapturedRequests.Where(r => r.Method == HttpMethod.Post).ToList();
            Assert.That(posts, Has.Count.EqualTo(1), "All links should be created in a single POST");
        }

        [Test]
        public async Task CreateLinksAsync_AisAvailable_PayloadContainsCorrectFieldsForEachLink()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var linkedItem = new InventoryWearable(UUID.Random())
            {
                AssetType = AssetType.Clothing,
                InventoryType = InventoryType.Wearable,
                Name = "Gloves"
            };
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}", HttpStatusCode.OK,
                MakeBatchLinksResponse((UUID.Random(), folderID, linkedItem.UUID, "Gloves")), "application/llsd+xml");

            await client.Inventory.CreateLinksAsync(folderID,
                new[] { ((InventoryBase)linkedItem, $"{(int)WearableType.Gloves}00") },
                null, CancellationToken.None);

            var postBody = client.CapturedRequests.First(r => r.Method == HttpMethod.Post).Body;
            var payload = OSDParser.DeserializeLLSDXml(postBody) as OSDMap;
            Assert.That(payload, Is.Not.Null);

            var links = payload!["links"] as OSDArray;
            Assert.That(links, Is.Not.Null.And.Count.EqualTo(1));

            var link = links![0] as OSDMap;
            Assert.That(link, Is.Not.Null);
            Assert.That(link!["linked_id"].AsUUID(), Is.EqualTo(linkedItem.UUID));
            Assert.That(link["name"].AsString(), Is.EqualTo("Gloves"));
            Assert.That(link["desc"].AsString(), Is.EqualTo($"{(int)WearableType.Gloves}00"));
            Assert.That((AssetType)link["type"].AsInteger(), Is.EqualTo(AssetType.Link));
            Assert.That((InventoryType)link["inv_type"].AsInteger(), Is.EqualTo(InventoryType.Wearable));
        }

        [Test]
        public async Task CreateLinksAsync_AisAvailable_CallbackTrueOnSuccess()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var linkedItem = new InventoryObject(UUID.Random()) { AssetType = AssetType.Object, InventoryType = InventoryType.Object, Name = "Hat" };
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}", HttpStatusCode.OK,
                MakeBatchLinksResponse((UUID.Random(), folderID, linkedItem.UUID, "Hat")), "application/llsd+xml");

            bool? callbackResult = null;
            await client.Inventory.CreateLinksAsync(folderID,
                new[] { ((InventoryBase)linkedItem, string.Empty) },
                r => callbackResult = r, CancellationToken.None);

            Assert.That(callbackResult, Is.True);
        }

        [Test]
        public async Task CreateLinksAsync_ServerError_CallbackFalse()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var linkedItem = new InventoryObject(UUID.Random()) { AssetType = AssetType.Object, InventoryType = InventoryType.Object, Name = "Hat" };
            client.AddHttpResponseForPath($"http://fake-ais3.test/ais3/category/{folderID}",
                HttpStatusCode.InternalServerError, string.Empty);

            bool? callbackResult = null;
            await client.Inventory.CreateLinksAsync(folderID,
                new[] { ((InventoryBase)linkedItem, string.Empty) },
                r => callbackResult = r, CancellationToken.None);

            Assert.That(callbackResult, Is.False);
        }

        // ─── URL correctness for read operations ───────────────────────────────────

        [Test]
        public async Task GetCategoryLinks_UsesGetWithNoTidParameter()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var responseXml = OSDParser.SerializeLLSDXmlString(new OSDMap { { "_embedded", new OSDMap() } });
            // Exact URL match — no ?tid= should appear
            client.AddHttpResponse(new Uri($"http://fake-ais3.test/ais3/category/{folderID}/links"),
                HttpStatusCode.OK, responseXml, "application/llsd+xml");

            var (s1, _, _, _) = await client.AisClient.GetCategoryLinksAsync(folderID.ToString(), CancellationToken.None);

            Assert.That(s1, Is.True, "GetCategoryLinks should GET /category/{id}/links with no ?tid=");
        }

        [Test]
        public async Task GetCategory_UsesGetWithNoTidParameter()
        {
            using var client = MakeClientWithCap();
            var folderID = UUID.Random();
            var responseXml = OSDParser.SerializeLLSDXmlString(new OSDMap { { "_embedded", new OSDMap() } });
            client.AddHttpResponse(new Uri($"http://fake-ais3.test/ais3/category/{folderID}"),
                HttpStatusCode.OK, responseXml, "application/llsd+xml");

            var (s1, _, _, _) = await client.AisClient.GetCategoryAsync(folderID.ToString(), CancellationToken.None);

            Assert.That(s1, Is.True, "GetCategory should GET /category/{id} with no ?tid=");
        }
    }
}
