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
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the InventoryThumbnailUpload capability. Verified against the reference viewer
    /// (LLFloaterSimpleSnapshot::uploadImageUploadFile / post_thumbnail_image_coro in
    /// llfloatersimplesnapshot.cpp): a two-phase upload where the metadata POST body is
    /// {item_id, task_id} for a task-inventory item, {category_id} for a local folder, or bare
    /// {item_id} for an agent inventory item; the response carries an "uploader" URL that the raw
    /// image bytes are POSTed to next, and only a final "state":"complete" response (carrying
    /// "new_asset") counts as success.
    /// </summary>
    [TestFixture]
    public class UploadThumbnailTests
    {
        private const string CapUrl = "http://test.invalid/inventory-thumbnail-upload";
        private const string UploaderUrl = "http://test.invalid/inventory-thumbnail-upload/upload/abc123";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("InventoryThumbnailUpload", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private void AddHappyPathResponses(UUID newAsset)
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_asset\":\"{newAsset}\"}}", "application/json");
        }

        [Test]
        public async Task UploadThumbnailAsync_AgentInventoryItem_SendsBareItemIdAndReturnsNewAsset()
        {
            var itemId = UUID.Random();
            var newAsset = UUID.Random();
            AddHappyPathResponses(newAsset);

            var result = await _client.Inventory.UploadThumbnailAsync(itemId, UUID.Zero, new byte[] { 1, 2, 3 });

            Assert.That(result, Is.EqualTo(newAsset));

            var requests = _client.CapturedRequests;
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[0].Body, Does.Contain("<key>item_id</key>"));
            Assert.That(requests[0].Body, Does.Not.Contain("<key>task_id</key>"));
            Assert.That(requests[0].Body, Does.Not.Contain("<key>category_id</key>"));
            Assert.That(requests[1].Uri, Is.EqualTo(new Uri(UploaderUrl)));
        }

        [Test]
        public async Task UploadThumbnailAsync_TaskInventoryItem_SendsItemIdAndTaskId()
        {
            var itemId = UUID.Random();
            var taskId = UUID.Random();
            var newAsset = UUID.Random();
            AddHappyPathResponses(newAsset);

            var result = await _client.Inventory.UploadThumbnailAsync(itemId, taskId, new byte[] { 1, 2, 3 });

            Assert.That(result, Is.EqualTo(newAsset));

            var metadataBody = _client.CapturedRequests[0].Body;
            Assert.That(metadataBody, Does.Contain("<key>item_id</key>"));
            Assert.That(metadataBody, Does.Contain("<key>task_id</key>"));
            Assert.That(metadataBody, Does.Not.Contain("<key>category_id</key>"));
        }

        [Test]
        public async Task UploadThumbnailAsync_KnownLocalFolder_SendsCategoryId()
        {
            var folderId = UUID.Random();
            var newAsset = UUID.Random();
            _client.SeedInventoryFolder(folderId, "Test Folder");
            AddHappyPathResponses(newAsset);

            var result = await _client.Inventory.UploadThumbnailAsync(folderId, UUID.Zero, new byte[] { 1, 2, 3 });

            Assert.That(result, Is.EqualTo(newAsset));

            var metadataBody = _client.CapturedRequests[0].Body;
            Assert.That(metadataBody, Does.Contain("<key>category_id</key>"));
            Assert.That(metadataBody, Does.Not.Contain("<key>item_id</key>"));
            Assert.That(metadataBody, Does.Not.Contain("<key>task_id</key>"));
        }

        [Test]
        public async Task UploadThumbnailAsync_NoCapability_ReturnsNullWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var result = await client.Inventory.UploadThumbnailAsync(UUID.Random(), UUID.Zero, new byte[] { 1 });

                Assert.That(result, Is.Null);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task UploadThumbnailAsync_MetadataResponseMissingUploader_ReturnsNullWithoutSecondRequest()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"state\":\"upload\"}", "application/json");

            var result = await _client.Inventory.UploadThumbnailAsync(UUID.Random(), UUID.Zero, new byte[] { 1 });

            Assert.That(result, Is.Null);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task UploadThumbnailAsync_UploadDoesNotComplete_ReturnsNull()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                "{\"state\":\"failed\",\"message\":\"bad image\"}", "application/json");

            var result = await _client.Inventory.UploadThumbnailAsync(UUID.Random(), UUID.Zero, new byte[] { 1 });

            Assert.That(result, Is.Null);
        }
    }
}
