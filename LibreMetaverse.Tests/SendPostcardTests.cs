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
using System.Net.Http;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the SendPostcard capability. Verified against the reference viewer
    /// (llpostcard.cpp / llpanelsnapshotpostcard.cpp): the metadata body has no "from" field --
    /// LLPostcardUploadInfo::generatePostBody only ever writes pos-global/to/name/subject/msg -- and
    /// sending a postcard is a two-phase upload (LLBufferedAssetUploadInfo): the metadata POST
    /// response returns an "uploader" URL that the raw JPEG bytes must be POSTed to next, and only
    /// a final "state":"complete" response counts as success. An earlier version of this code
    /// invented a "from" (sender email) field that the protocol has no room for, and never
    /// performed the second leg of the upload at all, so no image could ever reach the server.
    /// </summary>
    [TestFixture]
    public class SendPostcardTests
    {
        private const string CapUrl = "http://test.invalid/send-postcard";
        private const string UploaderUrl = "http://test.invalid/send-postcard/upload/abc123";

        private FakeGridClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("SendPostcard", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        [Test]
        public async Task SendPostcardAsync_HappyPath_PostsMetadataThenImageAndReturnsTrue()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK,
                "{\"state\":\"complete\"}", "application/json");

            var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
            bool result = await _client.Self.SendPostcardAsync(jpegBytes, "friend@example.com", "Jim Radford",
                "Postcard from the edge", "Hello, how are you today?", Vector3.One);

            Assert.That(result, Is.True);

            var requests = _client.CapturedRequests;
            Assert.That(requests.Count, Is.EqualTo(2));

            var metadataRequest = requests[0];
            Assert.That(metadataRequest.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(metadataRequest.Uri, Is.EqualTo(new Uri(CapUrl)));
            Assert.That(metadataRequest.Body, Does.Not.Contain("<key>from</key>"));
            Assert.That(metadataRequest.Body, Does.Contain("to"));
            Assert.That(metadataRequest.Body, Does.Contain("name"));
            Assert.That(metadataRequest.Body, Does.Contain("subject"));
            Assert.That(metadataRequest.Body, Does.Contain("msg"));
            Assert.That(metadataRequest.Body, Does.Contain("pos-global"));

            var imageRequest = requests[1];
            Assert.That(imageRequest.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(imageRequest.Uri, Is.EqualTo(new Uri(UploaderUrl)));
        }

        [Test]
        public async Task SendPostcardAsync_UploaderMissingFromResponse_ReturnsFalseWithoutSecondRequest()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, "{\"state\":\"upload\"}", "application/json");

            bool result = await _client.Self.SendPostcardAsync(new byte[] { 1, 2, 3 }, "friend@example.com",
                "Jim Radford", "Subject", "Message", Vector3.Zero);

            Assert.That(result, Is.False);
            Assert.That(_client.CapturedRequests.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task SendPostcardAsync_UploadDoesNotComplete_ReturnsFalse()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                $"{{\"state\":\"upload\",\"uploader\":\"{UploaderUrl}\"}}", "application/json");
            _client.AddHttpResponse(new Uri(UploaderUrl), HttpStatusCode.OK, "{\"state\":\"failed\"}", "application/json");

            bool result = await _client.Self.SendPostcardAsync(new byte[] { 1, 2, 3 }, "friend@example.com",
                "Jim Radford", "Subject", "Message", Vector3.Zero);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task SendPostcardAsync_NoCapability_ReturnsFalseWithoutRequest()
        {
            var client = new FakeGridClient();
            try
            {
                bool result = await client.Self.SendPostcardAsync(new byte[] { 1, 2, 3 }, "friend@example.com",
                    "Jim Radford", "Subject", "Message", Vector3.Zero);

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
