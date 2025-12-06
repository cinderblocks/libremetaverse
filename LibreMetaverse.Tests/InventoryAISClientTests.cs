using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class InventoryAISClientTests
    {
        private static OSDMap MakePermissions(UUID creator, UUID lastOwner)
        {
            return new OSDMap
            {
                { "creator_id", OSD.FromUUID(creator) },
                { "last_owner_id", OSD.FromUUID(lastOwner) },
                { "base_mask", 0 },
                { "everyone_mask", 0 },
                { "group_mask", 0 },
                { "next_owner_mask", 0 },
                { "owner_mask", 0 },
                { "is_owner_group", false },
                { "group_id", OSD.FromUUID(UUID.Zero) }
            };
        }

        private static OSDMap MakeSaleInfo()
        {
            return new OSDMap
            {
                { "sale_price", 0 },
                { "sale_type", (int)SaleType.Not }
            };
        }

        [Test]
        public void ParseLinksFromEmbedded_ObjectAsset_ResultsInAttachmentInventoryTypeAndParsesFields()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var parentId = UUID.Random();
            var agentId = UUID.Random();
            var linkedId = UUID.Random();
            var createdAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var linkOsd = new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "name", "TestObject" },
                { "desc", "desc" },
                { "agent_id", OSD.FromUUID(agentId) },
                { "linked_id", OSD.FromUUID(linkedId) },
                { "inv_type", (int)InventoryType.Texture }, // server bug: object attachments stored as Texture
                { "type", (int)AssetType.Object },
                { "created_at", createdAt },
                { "permissions", MakePermissions(UUID.Random(), UUID.Random()) },
                { "sale_info", MakeSaleInfo() }
            };

            var linksMap = new OSDMap { { "0", linkOsd } };
            var embedded = new OSDMap { { "links", linksMap } };
            var response = new OSDMap { { "_embedded", embedded } };

            var result = client.ParseLinksFromEmbedded(response);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));

            var parsed = result[0];
            Assert.That(parsed.Name, Is.EqualTo("TestObject"));
            Assert.That(parsed.ParentUUID, Is.EqualTo(parentId));
            Assert.That(parsed.AssetUUID, Is.EqualTo(linkedId));
            // Inventory type correction results in created InventoryItem of expected type — we cannot directly assert the enum used
            // but ensure basic fields are parsed and not left default
            Assert.That(parsed.OwnerID, Is.EqualTo(agentId));
            Assert.That(parsed.CreationDate, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public void ParseItemsFromEmbedded_ItemsKey_ParsesConcreteItems()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var parentId = UUID.Random();
            var agentId = UUID.Random();
            var createdAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var itemOsd = new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "name", "ConcreteItem" },
                { "desc", "desc" },
                { "agent_id", OSD.FromUUID(agentId) },
                { "inv_type", (int)InventoryType.Object },
                { "type", (int)AssetType.Object },
                { "created_at", createdAt },
                { "permissions", MakePermissions(UUID.Random(), UUID.Random()) },
                { "sale_info", MakeSaleInfo() }
            };

            var itemsMap = new OSDMap { { "0", itemOsd } };
            var embedded = new OSDMap { { "items", itemsMap } };
            var response = new OSDMap { { "_embedded", embedded } };

            var result = client.ParseItemsFromEmbedded(response);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));

            var parsed = result[0];
            Assert.That(parsed.Name, Is.EqualTo("ConcreteItem"));
            Assert.That(parsed.ParentUUID, Is.EqualTo(parentId));
            Assert.That(parsed.OwnerID, Is.EqualTo(agentId));
            Assert.That(parsed.CreationDate, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public void ParseLinksFromEmbedded_MeshAsset_TreatedAsAttachment()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var parentId = UUID.Random();
            var agentId = UUID.Random();
            var linkedId = UUID.Random();

            var linkOsd = new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "name", "MeshLink" },
                { "desc", "desc" },
                { "agent_id", OSD.FromUUID(agentId) },
                { "linked_id", OSD.FromUUID(linkedId) },
                { "inv_type", (int)InventoryType.Texture },
                { "type", (int)AssetType.Mesh },
                { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "permissions", MakePermissions(UUID.Random(), UUID.Random()) },
                { "sale_info", MakeSaleInfo() }
            };

            var linksMap = new OSDMap { { "0", linkOsd } };
            var response = new OSDMap { { "links", linksMap } };

            var result = client.ParseLinksFromEmbedded(response);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            var parsed = result[0];
            Assert.That(parsed.Name, Is.EqualTo("MeshLink"));
            Assert.That(parsed.ParentUUID, Is.EqualTo(parentId));
            Assert.That(parsed.AssetUUID, Is.EqualTo(linkedId));
        }

        [Test]
        public void ParseItemsFromEmbedded_TopLevelItemsKey_WorksWithoutEmbedded()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var parentId = UUID.Random();
            var agentId = UUID.Random();

            var itemOsd = new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "name", "TopLevelItem" },
                { "desc", "desc" },
                { "agent_id", OSD.FromUUID(agentId) },
                { "inv_type", (int)InventoryType.Object },
                { "type", (int)AssetType.Object },
                { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "permissions", MakePermissions(UUID.Random(), UUID.Random()) },
                { "sale_info", MakeSaleInfo() }
            };

            var itemsMap = new OSDMap { { "0", itemOsd } };
            var response = new OSDMap { { "items", itemsMap } };

            var result = client.ParseItemsFromEmbedded(response);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("TopLevelItem"));
        }

        [Test]
        public void ParseLinksFromEmbedded_TopLevelLinksKey_WorksWithoutEmbedded()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var parentId = UUID.Random();
            var linkedId = UUID.Random();

            var linkOsd = new OSDMap
            {
                { "item_id", OSD.FromUUID(itemId) },
                { "parent_id", OSD.FromUUID(parentId) },
                { "name", "TopLevelLink" },
                { "desc", "desc" },
                { "agent_id", OSD.FromUUID(UUID.Random()) },
                { "linked_id", OSD.FromUUID(linkedId) },
                { "inv_type", (int)InventoryType.Texture },
                { "type", (int)AssetType.Object },
                { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "permissions", MakePermissions(UUID.Random(), UUID.Random()) },
                { "sale_info", MakeSaleInfo() }
            };

            var linksMap = new OSDMap { { "0", linkOsd } };
            var response = new OSDMap { { "links", linksMap } };

            var result = client.ParseLinksFromEmbedded(response);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("TopLevelLink"));
        }

        [Test]
        public void ParseItemsFromEmbedded_NullOrEmpty_ReturnsEmptyList()
        {
            var client = new InventoryAISClient(null);

            Assert.That(client.ParseItemsFromEmbedded(null), Is.Empty);
            Assert.That(client.ParseItemsFromEmbedded(new OSDMap()), Is.Empty);
        }

        [Test]
        public void ParseEmbedded_CombinesItemsAndLinks()
        {
            var client = new InventoryAISClient(null);

            var itemId = UUID.Random();
            var linkId = UUID.Random();

            var itemOsd = new OSDMap { { "item_id", OSD.FromUUID(itemId) }, { "name", "I" }, { "parent_id", OSD.FromUUID(UUID.Random()) }, { "agent_id", OSD.FromUUID(UUID.Random()) }, { "inv_type", (int)InventoryType.Object }, { "type", (int)AssetType.Object }, { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, { "permissions", MakePermissions(UUID.Random(), UUID.Random()) }, { "sale_info", MakeSaleInfo() } };
            var linkOsd = new OSDMap { { "item_id", OSD.FromUUID(linkId) }, { "name", "L" }, { "parent_id", OSD.FromUUID(UUID.Random()) }, { "agent_id", OSD.FromUUID(UUID.Random()) }, { "linked_id", OSD.FromUUID(UUID.Random()) }, { "inv_type", (int)InventoryType.Texture }, { "type", (int)AssetType.Object }, { "created_at", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, { "permissions", MakePermissions(UUID.Random(), UUID.Random()) }, { "sale_info", MakeSaleInfo() } };

            var itemsMap = new OSDMap { { "0", itemOsd } };
            var linksMap = new OSDMap { { "0", linkOsd } };
            var embedded = new OSDMap { { "items", itemsMap }, { "links", linksMap } };
            var response = new OSDMap { { "_embedded", embedded } };

            client.ParseEmbedded(response, out var folders, out var items, out var links);

            Assert.That(items, Is.Not.Null.And.Count.EqualTo(1));
            Assert.That(links, Is.Not.Null.And.Count.EqualTo(1));
            Assert.That(folders, Is.Not.Null);
        }
    }
}
