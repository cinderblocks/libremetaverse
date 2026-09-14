using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Regression coverage for InventoryManager.FolderContentsAsync's followLinks resolution.
    /// See GitHub issue #186: IsLink() is true for both AssetType.Link and AssetType.LinkFolder,
    /// so a folder-link entry (e.g. a saved-outfit subfolder inside Current Outfit Folder) used
    /// to be handed to FetchItemAsync, which requests an item by UUID and waits on an ItemReceived
    /// event that a folder target can never trigger. A successful HTTP response with no matching
    /// item left the wait pending until the caller's cancellation token fired.
    /// </summary>
    [TestFixture]
    [Category("Inventory")]
    public class FolderLinkResolutionTests
    {
        private static readonly Uri DescendentsCap = new Uri("http://fake-inv.test/descendents");
        private static readonly Uri FetchItemCap = new Uri("http://fake-inv.test/fetch");

        private static OSDMap MakePermissions(UUID ownerId) => new OSDMap
        {
            { "creator_id", OSD.FromUUID(ownerId) },
            { "owner_id", OSD.FromUUID(ownerId) },
            { "last_owner_id", OSD.FromUUID(ownerId) },
            { "base_mask", 0x7FFFFFFF }, { "everyone_mask", 0 },
            { "group_mask", 0 }, { "next_owner_mask", 0x7FFFFFFF },
            { "owner_mask", 0x7FFFFFFF }, { "is_owner_group", false },
            { "group_id", OSD.FromUUID(UUID.Zero) }
        };

        private static OSDMap MakeSaleInfo() => new OSDMap
        {
            { "sale_price", 0 },
            { "sale_type", (int)SaleType.Not }
        };

        private static OSDMap MakeItemOsd(UUID itemId, UUID parentId, UUID ownerId, UUID assetId,
            string name, InventoryType invType, AssetType assetType) => new OSDMap
        {
            { "item_id", OSD.FromUUID(itemId) },
            { "parent_id", OSD.FromUUID(parentId) },
            { "asset_id", OSD.FromUUID(assetId) },
            { "name", name },
            { "desc", string.Empty },
            { "flags", 0 },
            { "inv_type", (int)invType },
            { "type", (int)assetType },
            { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "permissions", MakePermissions(ownerId) },
            { "sale_info", MakeSaleInfo() }
        };

        private static FakeGridClient MakeClientWithCaps()
        {
            var client = new FakeGridClient();
            client.AddCapability("FetchInventoryDescendents2", DescendentsCap);
            client.AddCapability("FetchInventory2", FetchItemCap);
            return client;
        }

        [Test]
        [CancelAfter(5000)]
        public async Task FolderContentsAsync_FollowLinks_PreservesFolderLink_WithoutFetchingAsItem(CancellationToken cancellationToken)
        {
            var client = MakeClientWithCaps();
            var ownerId = client.Self.AgentID;
            var folderId = UUID.Random();
            var linkedFolderId = UUID.Random();
            var linkItemId = UUID.Random();

            var folderLinkOsd = MakeItemOsd(linkItemId, folderId, ownerId, linkedFolderId,
                "Saved Outfit Link", InventoryType.Category, AssetType.LinkFolder);

            var folderDesc = new OSDMap
            {
                { "folder_id", OSD.FromUUID(folderId) },
                { "owner_id", OSD.FromUUID(ownerId) },
                { "version", 1 },
                { "descendents", 1 },
                { "items", new OSDArray { folderLinkOsd } }
            };
            var descendentsResponse = new OSDMap { { "folders", new OSDArray { folderDesc } } };

            client.AddHttpResponse(DescendentsCap, HttpStatusCode.OK,
                OSDParser.SerializeLLSDXmlString(descendentsResponse), "application/llsd+xml");

            // What the live server actually does for a folder UUID sent to FetchInventory2: a
            // successful response with no matching item. The buggy resolver waited on this
            // forever (until cancellation); the fix must never call it for a folder-link target.
            client.AddHttpResponse(FetchItemCap, HttpStatusCode.OK,
                OSDParser.SerializeLLSDXmlString(new OSDMap { { "items", new OSDArray() } }), "application/llsd+xml");

            var contents = await client.Inventory.FolderContentsAsync(
                folderId, ownerId, true, true, InventorySortOrder.ByName, cancellationToken, followLinks: true);

            Assert.That(contents, Has.Count.EqualTo(1));
            var resolved = contents[0] as InventoryItem;
            Assert.That(resolved, Is.Not.Null, "Folder-link entry should remain an InventoryItem");
            Assert.That(resolved!.AssetType, Is.EqualTo(AssetType.LinkFolder),
                "Folder-link entry should be preserved as-is rather than replaced by a (nonexistent) fetch result");
            Assert.That(resolved.AssetUUID, Is.EqualTo(linkedFolderId));

            Assert.That(client.CapturedRequests.Any(r => r.Uri == FetchItemCap), Is.False,
                "FetchInventory2 must never be called for a folder-link target; that request hangs until cancellation");
        }

        [Test]
        [CancelAfter(5000)]
        public async Task FolderContentsAsync_FollowLinks_ResolvesOrdinaryItemLink_FromCache(CancellationToken cancellationToken)
        {
            var client = MakeClientWithCaps();
            var ownerId = client.Self.AgentID;
            var folderId = UUID.Random();
            var targetItemId = UUID.Random();
            var linkItemId = UUID.Random();

            // Inventory.Store is normally only created by the login-reply handler; force it
            // into existence the same way SeedInventoryFolder does.
            client.SeedInventoryFolder(UUID.Random(), "bootstrap");

            // The real target item is already known locally (e.g. from a prior fetch).
            var cachedTarget = new InventoryWearable(targetItemId)
            {
                Name = "Real Shirt",
                AssetType = AssetType.Clothing,
                InventoryType = InventoryType.Wearable,
                WearableType = WearableType.Shirt,
                ParentUUID = UUID.Random()
            };
            client.Inventory.Store[targetItemId] = cachedTarget;

            var itemLinkOsd = MakeItemOsd(linkItemId, folderId, ownerId, targetItemId,
                "Shirt Link", InventoryType.Wearable, AssetType.Link);

            var folderDesc = new OSDMap
            {
                { "folder_id", OSD.FromUUID(folderId) },
                { "owner_id", OSD.FromUUID(ownerId) },
                { "version", 1 },
                { "descendents", 1 },
                { "items", new OSDArray { itemLinkOsd } }
            };
            var descendentsResponse = new OSDMap { { "folders", new OSDArray { folderDesc } } };

            client.AddHttpResponse(DescendentsCap, HttpStatusCode.OK,
                OSDParser.SerializeLLSDXmlString(descendentsResponse), "application/llsd+xml");

            var contents = await client.Inventory.FolderContentsAsync(
                folderId, ownerId, true, true, InventorySortOrder.ByName, cancellationToken, followLinks: true);

            Assert.That(contents, Has.Count.EqualTo(1));
            Assert.That(contents[0], Is.SameAs(cachedTarget),
                "An ordinary item link with a cached target should be substituted with the real, fully-typed item");

            Assert.That(client.CapturedRequests.Any(r => r.Uri == FetchItemCap), Is.False,
                "A cached item link should resolve from the local store without an HTTP fetch");
        }
    }
}
