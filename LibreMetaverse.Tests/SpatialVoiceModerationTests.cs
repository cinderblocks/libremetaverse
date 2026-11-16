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
    /// Coverage for the SpatialVoiceModerationRequest capability. Verified against the reference
    /// viewer (LLNearbyVoiceModeration::requestMuteIndividual / requestMuteAll in
    /// llnearbyvoicemoderation.cpp): POST {"operand":"mute"|"unmute","agent_id":&lt;uuid&gt;} to
    /// mute/unmute one resident's nearby voice, or POST {"operand":"mute_all"|"unmute_all"} (no
    /// agent_id) to mute/unmute everyone in the region.
    /// </summary>
    [TestFixture]
    [Category("Estate")]
    public class SpatialVoiceModerationTests
    {
        private const string CapUrl = "http://test.invalid/spatial-voice-moderation";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("SpatialVoiceModerationRequest", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task SetNearbyVoiceMutedAsync_Mute_PostsOperandAndAgentId()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");
            var agentId = UUID.Random();

            var result = await _client.Estate.SetNearbyVoiceMutedAsync(agentId, true);

            Assert.That(result, Is.True);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("<key>operand</key>"));
            Assert.That(body, Does.Contain("mute"));
            Assert.That(body, Does.Contain("<key>agent_id</key>"));
            Assert.That(body, Does.Contain(agentId.ToString()));
        }

        [Test]
        public async Task SetNearbyVoiceMutedAsync_Unmute_PostsUnmuteOperand()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var result = await _client.Estate.SetNearbyVoiceMutedAsync(UUID.Random(), false);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("unmute"));
        }

        [Test]
        public async Task SetNearbyVoiceMutedForAllAsync_Mute_PostsMuteAllWithoutAgentId()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var result = await _client.Estate.SetNearbyVoiceMutedForAllAsync(true);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("mute_all"));
            Assert.That(body, Does.Not.Contain("<key>agent_id</key>"));
        }

        [Test]
        public async Task SetNearbyVoiceMutedForAllAsync_Unmute_PostsUnmuteAll()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var result = await _client.Estate.SetNearbyVoiceMutedForAllAsync(false);

            Assert.That(result, Is.True);
            var body = _client.CapturedRequests[0].Body;
            Assert.That(body, Does.Contain("unmute_all"));
        }

        [Test]
        public async Task SetNearbyVoiceMutedAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                var result = await client.Estate.SetNearbyVoiceMutedAsync(UUID.Random(), true);

                Assert.That(result, Is.False);
                Assert.That(client.CapturedRequests.Count, Is.EqualTo(0));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task SetNearbyVoiceMutedAsync_NonSuccessStatus_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.InternalServerError, string.Empty);

            var result = await _client.Estate.SetNearbyVoiceMutedAsync(UUID.Random(), true);

            Assert.That(result, Is.False);
        }
    }
}
