/*
 * Copyright (c) 2025, Sjofn LLC
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

using NUnit.Framework;
using OpenMetaverse;
using System;
using System.Text;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class InventoryManagerTests
    {
        [Test]
        public void EncryptDecrypt_RoundTrip()
        {
            var assetId = UUID.Random();
            var shadow = InventoryManager.EncryptAssetID(assetId);
            var decrypted = InventoryManager.DecryptShadowID(shadow);

            Assert.That(decrypted, Is.EqualTo(assetId), "DecryptShadowID(EncryptAssetID(asset)) should return the original asset UUID");
        }

        [Test]
        public void ItemCRC_IsDeterministic_AndChangesWhenFieldsChange()
        {
            var itemId = UUID.Random();
            var item = new InventoryItem(itemId)
            {
                AssetUUID = UUID.Random(),
                ParentUUID = UUID.Random(),
                CreatorID = UUID.Random(),
                OwnerID = UUID.Random(),
                GroupID = UUID.Random(),
                Flags = 0x12345678,
                InventoryType = InventoryType.Object,
                AssetType = AssetType.Object,
                CreationDate = DateTime.UtcNow,
                SalePrice = 42,
                SaleType = SaleType.Not,
                Permissions = Permissions.FullPermissions
            };

            var crc1 = InventoryManager.ItemCRC(item);
            var crc2 = InventoryManager.ItemCRC(item);

            Assert.That(crc2, Is.EqualTo(crc1), "ItemCRC should be deterministic for identical item state");

            // Change a field that is included in the CRC and ensure the CRC changes
            item.AssetUUID = UUID.Random();
            var crc3 = InventoryManager.ItemCRC(item);

            Assert.That(crc3, Is.Not.EqualTo(crc1), "ItemCRC should change when item fields change");
        }

        [Test]
        public void CreateInventoryItem_ReturnsCorrectSubclass()
        {
            var id = UUID.Random();
            var texture = InventoryManager.CreateInventoryItem(InventoryType.Texture, id);
            var notecard = InventoryManager.CreateInventoryItem(InventoryType.Notecard, id);
            int arbitrary = 999;
            var unknown = InventoryManager.CreateInventoryItem((InventoryType)arbitrary, id);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(texture, Is.InstanceOf<InventoryTexture>());
                Assert.That(notecard, Is.InstanceOf<InventoryNotecard>());
                Assert.That(unknown, Is.InstanceOf<InventoryItem>());
            }
        }
    }
}
