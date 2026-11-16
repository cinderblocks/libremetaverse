/*
 * Copyright (c) 2026, Sjofn LLC
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
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for <see cref="InventoryBase.ThumbnailUUID"/>, which closes the loop on the
    /// InventoryThumbnailUpload capability (see <see cref="UploadThumbnailTests"/>) by letting a
    /// successful upload -- and any item/folder fetched with a "thumbnail" field already set --
    /// round-trip into the local inventory model. Verified against the reference viewer
    /// (LLInventoryObject's "thumbnail" handling in llinventory.cpp): the wire shape is a nested
    /// "thumbnail":{"asset_id":&lt;uuid&gt;} map, omitted entirely when there is no thumbnail.
    /// </summary>
    [TestFixture]
    public class InventoryThumbnailUUIDTests
    {
        [Test]
        public void InventoryItem_FromOSD_ParsesThumbnailAssetId()
        {
            var itemId = UUID.Random();
            var thumbnailAsset = UUID.Random();
            var map = BaseItemMap(itemId, UUID.Random());
            map["thumbnail"] = new OSDMap { ["asset_id"] = thumbnailAsset };

            var item = InventoryItem.FromOSD(map);

            Assert.That(item.ThumbnailUUID, Is.EqualTo(thumbnailAsset));
        }

        [Test]
        public void InventoryItem_FromOSD_NoThumbnailField_LeavesThumbnailUUIDZero()
        {
            var map = BaseItemMap(UUID.Random(), UUID.Random());

            var item = InventoryItem.FromOSD(map);

            Assert.That(item.ThumbnailUUID, Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void InventoryItem_GetOSD_RoundTripsThumbnail()
        {
            var item = InventoryManager.CreateInventoryItem(InventoryType.Notecard, UUID.Random());
            item.ThumbnailUUID = UUID.Random();

            var osd = (OSDMap)item.GetOSD();

            Assert.That(osd.ContainsKey("thumbnail"), Is.True);
            var thumbnail = (OSDMap)osd["thumbnail"];
            Assert.That(thumbnail["asset_id"].AsUUID(), Is.EqualTo(item.ThumbnailUUID));
        }

        [Test]
        public void InventoryItem_GetOSD_NoThumbnail_OmitsKey()
        {
            var item = InventoryManager.CreateInventoryItem(InventoryType.Notecard, UUID.Random());

            var osd = (OSDMap)item.GetOSD();

            Assert.That(osd.ContainsKey("thumbnail"), Is.False);
        }

        [Test]
        public void InventoryFolder_FromOSD_ParsesThumbnailAssetId()
        {
            var thumbnailAsset = UUID.Random();
            var map = new OSDMap
            {
                ["category_id"] = UUID.Random(),
                ["parent_id"] = UUID.Random(),
                ["name"] = "Test Folder",
                ["type_default"] = (int)FolderType.None,
                ["descendents"] = 0,
                ["thumbnail"] = new OSDMap { ["asset_id"] = thumbnailAsset }
            };

            var folder = InventoryFolder.FromOSD(map);

            Assert.That(folder.ThumbnailUUID, Is.EqualTo(thumbnailAsset));
        }

        [Test]
        public void InventoryFolder_GetOSD_RoundTripsThumbnail()
        {
            var folder = new InventoryFolder(UUID.Random()) { ThumbnailUUID = UUID.Random() };

            var osd = (OSDMap)folder.GetOSD();

            Assert.That(osd.ContainsKey("thumbnail"), Is.True);
            var thumbnail = (OSDMap)osd["thumbnail"];
            Assert.That(thumbnail["asset_id"].AsUUID(), Is.EqualTo(folder.ThumbnailUUID));
        }

        [Test]
        public async Task UploadThumbnailAsync_AgentInventoryItemKnownLocally_UpdatesStoreThumbnailUUID()
        {
            const string capUrl = "http://test.invalid/inventory-thumbnail-upload";
            const string uploaderUrl = "http://test.invalid/inventory-thumbnail-upload/upload/abc123";

            var client = new FakeGridClient();
            try
            {
                client.AddCapability("InventoryThumbnailUpload", new Uri(capUrl));

                var itemId = UUID.Random();
                client.SeedInventoryFolder(UUID.Random(), "placeholder"); // ensures Store exists
                client.Inventory.Store![itemId] = InventoryManager.CreateInventoryItem(InventoryType.Notecard, itemId);

                var newAsset = UUID.Random();
                client.AddHttpResponse(new Uri(capUrl), HttpStatusCode.OK,
                    $"{{\"uploader\":\"{uploaderUrl}\"}}", "application/json");
                client.AddHttpResponse(new Uri(uploaderUrl), HttpStatusCode.OK,
                    $"{{\"state\":\"complete\",\"new_asset\":\"{newAsset}\"}}", "application/json");

                var result = await client.Inventory.UploadThumbnailAsync(itemId, UUID.Zero, new byte[] { 1, 2, 3 });

                Assert.That(result, Is.EqualTo(newAsset));
                Assert.That(client.Inventory.Store![itemId].ThumbnailUUID, Is.EqualTo(newAsset));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        private static OSDMap BaseItemMap(UUID itemId, UUID parentId) => new OSDMap
        {
            ["item_id"] = itemId,
            ["parent_id"] = parentId,
            ["name"] = "Test Item",
            ["desc"] = "A test item",
            ["type"] = (int)AssetType.Notecard,
            ["inv_type"] = (int)InventoryType.Notecard,
            ["flags"] = 0,
            ["created_at"] = 1700000000,
            ["asset_id"] = UUID.Random(),
            ["permissions"] = new OSDMap
            {
                ["creator_id"] = UUID.Random(),
                ["owner_id"] = UUID.Random(),
                ["last_owner_id"] = UUID.Random(),
                ["group_id"] = UUID.Zero,
                ["base_mask"] = (uint)PermissionMask.All,
                ["owner_mask"] = (uint)PermissionMask.All,
                ["group_mask"] = 0,
                ["everyone_mask"] = 0,
                ["next_owner_mask"] = (uint)PermissionMask.All,
                ["is_owner_group"] = false
            },
            ["sale_info"] = new OSDMap { ["sale_price"] = 0, ["sale_type"] = 0 }
        };
    }
}
