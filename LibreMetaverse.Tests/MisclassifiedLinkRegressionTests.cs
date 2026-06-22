/*
 * Copyright (c) 2026, Sjofn LLC.
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

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Regression tests for Issue #102: inventory links whose InventoryType is Texture
    /// (inv_type=0) but whose actual target is a wearable or attachment are silently dropped
    /// during COF (Current Outfit Folder) processing.
    ///
    /// Background: old SL clients stored some attachments with inv_type=0. Links to those items
    /// inherit the wrong InventoryType, so FromOSD emits InventoryTexture instead of
    /// InventoryWearable or InventoryAttachment. The COF processing switch had no case for this
    /// scenario, causing the item to be silently skipped.
    ///
    /// The fix adds a fallback case that detects any InventoryItem where IsLink() is true,
    /// resolves the target from the inventory store, and handles it by the target's real type.
    ///
    /// These tests verify the data-contract preconditions that the fix relies on:
    ///   1. IsLink() is true for the misclassified item.
    ///   2. ResolvedItemID returns the target inventory UUID.
    ///   3. The inventory store can be populated and queried by ResolvedItemID.
    ///   4. The store returns the correctly typed target object.
    ///
    /// The full end-to-end path (AppearanceManager.RequestAgentWornAsync) requires a live
    /// server and is covered by AppearanceLiveTests.
    /// </summary>
    [TestFixture]
    [Category("Inventory")]
    public class MisclassifiedLinkRegressionTests
    {
        private GridClient _client;
        private Inventory _store;

        [SetUp]
        public void SetUp()
        {
            _client = new GridClient();
            _store = new Inventory(_client, UUID.Random());
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        // ── IsLink() and ResolvedItemID ─────────────────────────────────────────

        [Test]
        public void IsLink_TextureItemWithLinkAssetType_ReturnsTrue()
        {
            // An InventoryTexture whose AssetType is Link must still be detected as a link.
            // The fix guard clause checks IsLink() first; if this returns false the item is skipped.
            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = UUID.Random()
            };

            Assert.That(misclassified.IsLink(), Is.True,
                "InventoryTexture with AssetType.Link should be detected as a link");
        }

        [Test]
        public void IsLink_NormalTexture_ReturnsFalse()
        {
            // Sanity baseline: a real texture (not a link) must not be treated as a link.
            var texture = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Texture,
                AssetUUID = UUID.Random()
            };

            Assert.That(texture.IsLink(), Is.False);
        }

        [Test]
        public void ResolvedItemID_OnMisclassifiedTextureLink_ReturnsAssetUUID()
        {
            // For a link, ResolvedItemID returns AssetUUID (the target inventory UUID).
            // The fix uses this value to query the store.
            var targetUUID = UUID.Random();
            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = targetUUID
            };

            Assert.That(misclassified.ResolvedItemID, Is.EqualTo(targetUUID));
        }

        // ── Inventory store lookup ─────────────────────────────────────────────

        [Test]
        public void InventoryStore_Contains_FindsTargetByResolvedItemID()
        {
            // After the real wearable is added to the store, Contains(resolvedItemID) must
            // return true — this is the guard condition before the store indexer is called.
            var targetUUID = UUID.Random();
            _store.UpdateNodeFor(new InventoryWearable(targetUUID)
            {
                WearableType = WearableType.Shirt,
                AssetType = AssetType.Clothing,
                AssetUUID = UUID.Random()
            });

            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = targetUUID
            };

            Assert.That(_store.Contains(misclassified.ResolvedItemID), Is.True);
        }

        [Test]
        public void InventoryStore_Indexer_ReturnsWearable_WhenTargetIsWearable()
        {
            // store[resolvedItemID] must return the InventoryWearable so the fix can add it to
            // the wearables dictionary. If the cast fails the item is silently dropped.
            var targetUUID = UUID.Random();
            _store.UpdateNodeFor(new InventoryWearable(targetUUID)
            {
                WearableType = WearableType.Shirt,
                AssetType = AssetType.Clothing,
                AssetUUID = UUID.Random(),
                Name = "Test Shirt"
            });

            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = targetUUID
            };

            var resolved = _store[misclassified.ResolvedItemID];

            Assert.That(resolved, Is.InstanceOf<InventoryWearable>());
            Assert.That(((InventoryWearable)resolved).WearableType, Is.EqualTo(WearableType.Shirt));
        }

        [Test]
        public void InventoryStore_Indexer_ReturnsAttachment_WhenTargetIsAttachment()
        {
            var targetUUID = UUID.Random();
            _store.UpdateNodeFor(new InventoryAttachment(targetUUID)
            {
                AttachmentPoint = AttachmentPoint.Chest,
                AssetType = AssetType.Object,
                AssetUUID = UUID.Random(),
                Name = "Test Attachment"
            });

            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = targetUUID
            };

            var resolved = _store[misclassified.ResolvedItemID];

            Assert.That(resolved, Is.InstanceOf<InventoryAttachment>());
            Assert.That(((InventoryAttachment)resolved).AttachmentPoint, Is.EqualTo(AttachmentPoint.Chest));
        }

        [Test]
        public void InventoryStore_Indexer_ReturnsObject_WhenTargetIsObject()
        {
            var targetUUID = UUID.Random();
            _store.UpdateNodeFor(new InventoryObject(targetUUID)
            {
                AttachPoint = AttachmentPoint.RightHand,
                AssetType = AssetType.Object,
                AssetUUID = UUID.Random(),
                Name = "Test Object"
            });

            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = targetUUID
            };

            var resolved = _store[misclassified.ResolvedItemID];

            Assert.That(resolved, Is.InstanceOf<InventoryObject>());
            Assert.That(((InventoryObject)resolved).AttachPoint, Is.EqualTo(AttachmentPoint.RightHand));
        }

        [Test]
        public void InventoryStore_NotContains_WhenTargetAbsent()
        {
            // If the target is not in the store the fix falls back to doing nothing (same as
            // the pre-fix behaviour for truly unresolvable links).
            var misclassified = new InventoryTexture(UUID.Random())
            {
                AssetType = AssetType.Link,
                AssetUUID = UUID.Random() // not in _store
            };

            Assert.That(_store.Contains(misclassified.ResolvedItemID), Is.False);
        }
    }
}
