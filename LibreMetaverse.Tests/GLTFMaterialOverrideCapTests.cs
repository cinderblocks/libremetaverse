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
using LibreMetaverse.Assets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the ModifyMaterialParams capability. Verified against the reference viewer
    /// (LLGLTFMaterialList::flushUpdatesOnce/modifyMaterialCoro in llgltfmateriallist.cpp): the
    /// request body is a bare LLSD array of {object_id, side, [asset_id], [gltf_json]} update maps
    /// -- not wrapped in an outer object -- and the response is a single {"success","message"}
    /// result for the whole batch. An override/apply with no override material sends an empty
    /// "gltf_json" to clear the face's existing override.
    /// </summary>
    [TestFixture]
    public class GLTFMaterialOverrideCapTests
    {
        private const string CapUrl = "http://test.invalid/modify-material-params";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("ModifyMaterialParams", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task SetMaterialOverrideAsync_HappyPath_PostsBareArrayWithGltfJson()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var objectId = UUID.Random();
            var material = new AssetMaterial();
            material.SetBaseColorFactor(new Color4(0.1f, 0.2f, 0.3f, 1f), forOverride: true);

            var result = await _client.Objects.SetMaterialOverrideAsync(_client.Network.CurrentSim, objectId, 3, material);

            Assert.That(result, Is.True);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));

            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>object_id</key>"));
            Assert.That(body, Does.Contain(objectId.ToString()));
            Assert.That(body, Does.Contain("<key>side</key>"));
            Assert.That(body, Does.Contain("<key>gltf_json</key>"));
            Assert.That(body, Does.Not.Contain("<key>asset_id</key>"));
        }

        [Test]
        public async Task ClearMaterialOverrideAsync_SendsEmptyGltfJson()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var result = await _client.Objects.ClearMaterialOverrideAsync(_client.Network.CurrentSim, UUID.Random(), 0);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>gltf_json</key>"));
            Assert.That(body, Does.Contain("<string />").Or.Contain("<string></string>"));
            Assert.That(body, Does.Not.Contain("<key>asset_id</key>"));
        }

        [Test]
        public async Task ApplyMaterialAsync_WithoutOverride_SendsAssetIdOnly()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var assetId = UUID.Random();
            var result = await _client.Objects.ApplyMaterialAsync(_client.Network.CurrentSim, UUID.Random(), 1, assetId);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>asset_id</key>"));
            Assert.That(body, Does.Contain(assetId.ToString()));
            Assert.That(body, Does.Not.Contain("<key>gltf_json</key>"));
        }

        [Test]
        public async Task SendMaterialUpdatesAsync_MultipleEntries_SendsSingleArrayBody()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var updates = new[]
            {
                new GLTFMaterialUpdate { ObjectId = UUID.Random(), Side = 0 },
                new GLTFMaterialUpdate { ObjectId = UUID.Random(), Side = 1, AssetId = UUID.Random() }
            };

            var result = await _client.Objects.SendMaterialUpdatesAsync(_client.Network.CurrentSim, updates);

            Assert.That(result, Is.True);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1), "both updates should go in a single POST");
        }

        [Test]
        public async Task SendMaterialUpdatesAsync_ServerRejects_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"success\":false,\"message\":\"no rez rights\"}", "application/json");

            var result = await _client.Objects.ClearMaterialOverrideAsync(_client.Network.CurrentSim, UUID.Random(), 0);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task SendMaterialUpdatesAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                // Ensure there's a CurrentSim/Caps present but without ModifyMaterialParams registered.
                client.AddCapability("SomeOtherCap", new Uri("http://test.invalid/other"));

                var result = await client.Objects.ClearMaterialOverrideAsync(client.Network.CurrentSim, UUID.Random(), 0);

                Assert.That(result, Is.False);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }
    }
}
