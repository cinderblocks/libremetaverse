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
    /// Coverage for the ModifyRegion capability (PBR terrain material overrides). Verified against
    /// the reference viewer (LLPBRTerrainFeatures::queryRegionCoro/queueModify in
    /// llpbrterrainfeatures.cpp): GET/POST body is {"overrides":[ov0,ov1,ov2,ov3]}, one entry per
    /// terrain texture blend slot, each either {} (no override) or the compact tex/bc/ec/mf/rf/am/
    /// ac/ds/ti override shape (LLGLTFMaterial::getOverrideLLSD) -- this is NOT the same wire shape
    /// as ModifyMaterialParams, which uses full GLTF JSON strings instead.
    /// </summary>
    [TestFixture]
    public class TerrainMaterialOverrideTests
    {
        private const string CapUrl = "http://test.invalid/modify-region";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("ModifyRegion", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task GetTerrainMaterialOverridesAsync_ParsesFourSlotsWithSomeEmpty()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"success\":true,\"overrides\":[{},{\"mf\":0.25},{},{}]}", "application/json");

            var overrides = await _client.Terrain.GetTerrainMaterialOverridesAsync();

            Assert.That(overrides, Is.Not.Null);
            Assert.That(overrides!.Length, Is.EqualTo(4));
            Assert.That(overrides[0], Is.Null);
            Assert.That(overrides[1], Is.Not.Null);
            Assert.That(overrides[1]!.MetallicFactor, Is.EqualTo(0.25f));
            Assert.That(overrides[2], Is.Null);
            Assert.That(overrides[3], Is.Null);
        }

        [Test]
        public async Task GetTerrainMaterialOverridesAsync_ServerReportsFailure_ReturnsNull()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                "{\"success\":false,\"message\":\"nope\"}", "application/json");

            var overrides = await _client.Terrain.GetTerrainMaterialOverridesAsync();

            Assert.That(overrides, Is.Null);
        }

        [Test]
        public async Task GetTerrainMaterialOverridesAsync_NoCapability_ReturnsNullWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("SomeOtherCap", new Uri("http://test.invalid/other"));

                var overrides = await client.Terrain.GetTerrainMaterialOverridesAsync();

                Assert.That(overrides, Is.Null);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task SetTerrainMaterialOverridesAsync_MixOfNullAndSet_PostsFourEntries()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"success\":true}", "application/json");

            var mat = new AssetMaterial();
            mat.SetRoughnessFactor(0.1f, forOverride: true);

            var overrides = new AssetMaterial?[] { null, mat, null, null };
            var result = await _client.Terrain.SetTerrainMaterialOverridesAsync(overrides);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>overrides</key>"));
            Assert.That(body, Does.Contain("<key>rf</key>"));
        }

        [Test]
        public void SetTerrainMaterialOverridesAsync_WrongLength_Throws()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _client.Terrain.SetTerrainMaterialOverridesAsync(new AssetMaterial?[] { null, null }));
        }

        [Test]
        public async Task SetTerrainMaterialOverridesAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                client.AddCapability("SomeOtherCap", new Uri("http://test.invalid/other"));

                var overrides = new AssetMaterial?[] { null, null, null, null };
                var result = await client.Terrain.SetTerrainMaterialOverridesAsync(overrides);

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
