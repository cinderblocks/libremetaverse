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
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the UpdateMaterialAgentInventory/UpdateMaterialTaskInventory capabilities.
    /// Verified against the reference viewer (LLMaterialEditor::updateInventoryItem in
    /// llmaterialeditor.cpp): a two-phase upload using the same LLBufferedAssetUploadInfo shape as
    /// UpdateNotecardAgentInventory/UpdateNotecardTaskInventory -- POST {"item_id"} (plus "task_id"
    /// for the task variant) to the capability, which returns an "uploader" URL. The second POST
    /// sends a binary LLSD map containing version, type, and the material JSON in data; a final
    /// "state":"complete" response carries the new asset UUID under "new_asset".
    /// </summary>
    [TestFixture]
    public class GLTFMaterialUploadTests
    {
        private const string AgentCapUrl = "http://test.invalid/update-material-agent-inventory";
        private const string TaskCapUrl = "http://test.invalid/update-material-task-inventory";
        private const string UploaderUrl = "http://test.invalid/update-material/upload/abc123";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("UpdateMaterialAgentInventory", new Uri(AgentCapUrl));
            _client.AddCapability("UpdateMaterialTaskInventory", new Uri(TaskCapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task RequestUpdateMaterialAgentInventoryAsync_HappyPath_PostsWrappedAssetAndReturnsNewAsset()
        {
            var itemId = UUID.Random();
            var newAsset = UUID.Random();

            _client.AddHttpResponse(new Uri(AgentCapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_asset\":\"{newAsset}\"}}", "application/json");

            var material = new AssetMaterial();
            var result = await _client.Inventory.RequestUpdateMaterialAgentInventoryAsync(material, itemId);

            Assert.That(result.success, Is.True);
            Assert.That(result.assetID, Is.EqualTo(newAsset));

            var requests = _client.CapturedRequests;
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[0].Body, Does.Contain("<key>item_id</key>"));
            Assert.That(requests[0].Body, Does.Not.Contain("<key>task_id</key>"));
            Assert.That(requests[1].Uri, Is.EqualTo(new Uri(UploaderUrl)));
        }

        [Test]
        public async Task RequestUpdateMaterialTaskInventoryAsync_HappyPath_PostsItemIdAndTaskId()
        {
            var itemId = UUID.Random();
            var taskId = UUID.Random();
            var newAsset = UUID.Random();

            _client.AddHttpResponse(new Uri(TaskCapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_asset\":\"{newAsset}\"}}", "application/json");

            var material = new AssetMaterial();
            var result = await _client.Inventory.RequestUpdateMaterialTaskInventoryAsync(material, itemId, taskId);

            Assert.That(result.success, Is.True);
            Assert.That(result.assetID, Is.EqualTo(newAsset));

            var metadataBody = _client.CapturedRequests[0].Body;
            Assert.That(metadataBody, Does.Contain("<key>item_id</key>"));
            Assert.That(metadataBody, Does.Contain("<key>task_id</key>"));
        }

        [Test]
        public void RequestUpdateMaterialAgentInventoryAsync_NoCapability_Throws()
        {
            var client = new FakeGridClient();
            try
            {
                var material = new AssetMaterial();
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await client.Inventory.RequestUpdateMaterialAgentInventoryAsync(material, UUID.Random()));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task RequestUpdateMaterialAgentInventoryAsync_UploadedAssetIsBinaryLlsdWrappedGltf()
        {
            var itemId = UUID.Random();
            var newAsset = UUID.Random();

            _client.AddHttpResponse(new Uri(AgentCapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                $"{{\"state\":\"complete\",\"new_asset\":\"{newAsset}\"}}", "application/json");

            var material = new AssetMaterial { Name = "My Material \u0394" };
            material.SetBaseColorFactor(new Color4(0.2f, 0.4f, 0.6f, 1f));
            var expectedJson = material.ToJson();
            await _client.Inventory.RequestUpdateMaterialAgentInventoryAsync(material, itemId);

            var uploadedAsset = _client.CapturedRequestBodies[1];
            var binaryHeader = Encoding.ASCII.GetBytes("<?llsd/binary?>\n");
            Assert.That(uploadedAsset.Take(binaryHeader.Length), Is.EqualTo(binaryHeader));
            Assert.That(Encoding.ASCII.GetString(uploadedAsset).IndexOf(
                "<?llsd/binary?>", binaryHeader.Length, StringComparison.Ordinal), Is.EqualTo(-1));

            var asset = OSDParser.DeserializeLLSDBinary(uploadedAsset);
            Assert.That(asset, Is.TypeOf<OSDMap>());
            var assetMap = (OSDMap)asset;
            Assert.That(assetMap.Count, Is.EqualTo(3));
            Assert.That(assetMap["version"].Type, Is.EqualTo(OSDType.String));
            Assert.That(assetMap["version"].AsString(), Is.EqualTo("1.1"));
            Assert.That(assetMap["type"].Type, Is.EqualTo(OSDType.String));
            Assert.That(assetMap["type"].AsString(), Is.EqualTo("GLTF 2.0"));
            Assert.That(assetMap["data"].Type, Is.EqualTo(OSDType.String));
            Assert.That(assetMap["data"].AsString(), Is.EqualTo(expectedJson));

            var roundTripped = new AssetMaterial(UUID.Random(),
                Encoding.UTF8.GetBytes(assetMap["data"].AsString()));

            Assert.That(roundTripped.Name, Is.EqualTo("My Material \u0394"));
            Assert.That(roundTripped.BaseColorFactor, Is.EqualTo(material.BaseColorFactor));
        }
    }
}
