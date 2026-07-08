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
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the RequestTaskInventory capability. Verified against the reference viewer
    /// (LLViewerObject::fetchInventoryFromCapCoro / fetchInventoryFromServer in llviewerobject.cpp):
    /// when the capability is present it is used exclusively (GET "?task_id=&lt;uuid&gt;", response
    /// is an LLSD map with a "contents" array of item maps in the same shape used elsewhere for
    /// AIS3 items), and the legacy UDP RequestTaskInventory message + Xfer download is only used as
    /// a fallback when the capability is absent. The reference viewer also synthesizes a "Contents"
    /// root category locally since the server doesn't send one.
    /// </summary>
    [TestFixture]
    public class RequestTaskInventoryCapTests
    {
        private const string CapUrl = "http://test.invalid/request-task-inventory";

        private static string ItemJson(UUID itemId, UUID parentId, string assetOrShadowKey, UUID assetOrShadowValue) =>
            "{" +
            $"\"item_id\":\"{itemId}\"," +
            $"\"parent_id\":\"{parentId}\"," +
            "\"name\":\"Test Item\"," +
            "\"desc\":\"A test item\"," +
            "\"type\":7," +
            "\"inv_type\":7," +
            "\"flags\":0," +
            "\"created_at\":1700000000," +
            "\"agent_id\":\"11111111-1111-1111-1111-111111111111\"," +
            $"\"{assetOrShadowKey}\":\"{assetOrShadowValue}\"," +
            "\"permissions\":{" +
            "\"creator_id\":\"11111111-1111-1111-1111-111111111111\"," +
            "\"owner_id\":\"11111111-1111-1111-1111-111111111111\"," +
            "\"last_owner_id\":\"11111111-1111-1111-1111-111111111111\"," +
            "\"group_id\":\"00000000-0000-0000-0000-000000000000\"," +
            "\"base_mask\":2147483647,\"owner_mask\":2147483647,\"group_mask\":0,\"everyone_mask\":0,\"next_owner_mask\":2147483647," +
            "\"is_owner_group\":false}," +
            "\"sale_info\":{\"sale_price\":0,\"sale_type\":0}" +
            "}";

        [Test]
        public async Task GetTaskInventoryAsync_CapAvailable_ReturnsContentsFolderAndDecryptsShadowId()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("RequestTaskInventory", new Uri(CapUrl));

                var objectId = UUID.Random();
                var itemId = UUID.Random();
                var assetId = UUID.Random();
                var shadowId = InventoryManager.EncryptAssetID(assetId);

                var body = "{\"inventory_serial\":3,\"contents\":[" +
                           ItemJson(itemId, objectId, "shadow_id", shadowId) +
                           "]}";
                client.AddHttpResponseForPath(CapUrl, HttpStatusCode.OK, body, "application/json");

                var result = await client.Inventory.GetTaskInventoryAsync(objectId, 12345);

                // Exactly one HTTP request, to the cap URL with the task_id query param
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(1));
                Assert.That(client.CapturedRequests[0].Uri.Query, Does.Contain($"task_id={objectId}"));

                // Synthesized "Contents" folder plus the one real item
                Assert.That(result.Count, Is.EqualTo(2));
                var folder = result.OfType<InventoryFolder>().Single();
                Assert.That(folder.Name, Is.EqualTo("Contents"));
                Assert.That(folder.UUID, Is.EqualTo(objectId));

                var item = result.OfType<InventoryItem>().Single();
                Assert.That(item.UUID, Is.EqualTo(itemId));
                Assert.That(item.ParentUUID, Is.EqualTo(objectId));
                Assert.That(item.Name, Is.EqualTo("Test Item"));
                Assert.That(item.AssetUUID, Is.EqualTo(assetId), "shadow_id should be XOR-decrypted back to the real asset UUID");
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task GetTaskInventoryAsync_PlainAssetId_ParsesWithoutDecryption()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("RequestTaskInventory", new Uri(CapUrl));

                var objectId = UUID.Random();
                var itemId = UUID.Random();
                var assetId = UUID.Random();

                var body = "{\"inventory_serial\":1,\"contents\":[" +
                           ItemJson(itemId, objectId, "asset_id", assetId) +
                           "]}";
                client.AddHttpResponseForPath(CapUrl, HttpStatusCode.OK, body, "application/json");

                var result = await client.Inventory.GetTaskInventoryAsync(objectId, 12345);

                var item = result.OfType<InventoryItem>().Single();
                Assert.That(item.AssetUUID, Is.EqualTo(assetId));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task GetTaskInventoryAsync_NonSuccessStatus_ReturnsContentsFolderOnly()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("RequestTaskInventory", new Uri(CapUrl));
                client.AddHttpResponseForPath(CapUrl, HttpStatusCode.InternalServerError, string.Empty);

                var objectId = UUID.Random();
                var result = await client.Inventory.GetTaskInventoryAsync(objectId, 12345);

                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result[0], Is.InstanceOf<InventoryFolder>());
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task GetTaskInventoryAsync_NoCapability_DoesNotMakeHttpRequest()
        {
            var client = new FakeGridClient();
            try
            {
                // No RequestTaskInventory capability registered -- the legacy UDP+Xfer path would
                // be used instead, which never completes in this fake-network test environment, so
                // cancel almost immediately and just verify the HTTP cap path was never attempted.
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(50);

                var result = await client.Inventory.GetTaskInventoryAsync(UUID.Random(), 12345,
                    cancellationToken: cts.Token);

                Assert.That(result, Is.Empty);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }
    }
}
