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
using System;
using System.Collections;
using System.Reflection;

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

        [TestCase(23, "Widget", "widget")]
        [TestCase(24, "Person", "person")]
        [TestCase(25, "Settings", "settings")]
        [TestCase(26, "Material", "material")]
        [TestCase(27, "GLTF", "gltf")]
        [TestCase(28, "GLTFBin", "glbin")]
        public void InventoryTypeCanonicalTail_HasExpectedWireValueAndName(int value, string enumName, string wireName)
        {
            var inventoryType = (InventoryType)value;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Enum.GetName(typeof(InventoryType), inventoryType), Is.EqualTo(enumName));
                Assert.That(Utils.InventoryTypeToString(inventoryType), Is.EqualTo(wireName));
                Assert.That(Utils.StringToInventoryType(wireName), Is.EqualTo(inventoryType));
            }
        }

        [Test]
        public void CreateInventoryItem_MaterialWireValue_ReturnsMaterial()
        {
            var item = InventoryManager.CreateInventoryItem((InventoryType)26, UUID.Random());

            Assert.That(item, Is.InstanceOf<InventoryMaterial>());
        }

        [TestCase(25, 10, 25)]
        [TestCase(0, 10, 0)]
        [TestCase(null, 10, 10)]
        public void GetUploadCostForAssetType_TextureUsesPresentBenefitOrBaseFallback(
            int? textureUploadCost, int baseUploadCost, int expected)
        {
            var actual = GetUploadCostForAssetType(
                AssetType.Texture, "texture_upload_cost", textureUploadCost, baseUploadCost);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(AssetType.Animation, "animation_upload_cost", 7, 7)]
        [TestCase(AssetType.Animation, "animation_upload_cost", 0, 0)]
        [TestCase(AssetType.Animation, "animation_upload_cost", null, 10)]
        [TestCase(AssetType.Sound, "sound_upload_cost", 8, 8)]
        [TestCase(AssetType.Sound, "sound_upload_cost", 0, 0)]
        [TestCase(AssetType.Sound, "sound_upload_cost", null, 10)]
        [TestCase(AssetType.Object, "mesh_upload_cost", 9, 9)]
        [TestCase(AssetType.Object, "mesh_upload_cost", 0, 0)]
        [TestCase(AssetType.Object, "mesh_upload_cost", null, 10)]
        public void GetUploadCostForAssetType_NonTextureBenefit_UsesPresentBenefitOrBaseFallback(
            AssetType assetType, string benefitKey, int? benefitCost, int expected)
        {
            // A present benefit cost of 0 (e.g. a free-upload account tier) must be declared as
            // 0, not silently replaced by Settings.UploadCost -- the same class of bug fixed for
            // Texture in GetUploadCostForAssetType_TextureUsesPresentBenefitOrBaseFallback, which
            // applies equally here since all four benefit costs share the same -1-when-absent
            // wire contract (see AccountLevelBenefits). Declaring a fee the simulator won't
            // actually charge causes the real upload request to be rejected.
            var actual = GetUploadCostForAssetType(assetType, benefitKey, benefitCost, 10);

            Assert.That(actual, Is.EqualTo(expected));
        }

        private static int GetUploadCostForAssetType(
            AssetType assetType, string benefitKey, int? benefitCost, int baseUploadCost)
        {
            var client = new GridClient();
            client.Settings.UploadCost = baseUploadCost;

            var values = new Hashtable();
            if (benefitCost.HasValue)
                values[benefitKey] = benefitCost.Value;

            var benefitsProperty = typeof(AgentManager).GetProperty(nameof(AgentManager.Benefits));
            Assert.That(benefitsProperty, Is.Not.Null);
            benefitsProperty!.SetValue(client.Self, new AccountLevelBenefits(values));

            var method = typeof(InventoryManager).GetMethod(
                "GetUploadCostForAssetType", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            return (int)method!.Invoke(client.Inventory, new object[] { assetType })!;
        }
    }
}
