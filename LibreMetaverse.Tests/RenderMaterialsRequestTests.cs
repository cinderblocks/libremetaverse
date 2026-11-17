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
using System.Threading.Tasks;
using LibreMetaverse.StructuredData;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The request body of <see cref="ObjectManager.RequestMaterialsAsync(Simulator, System.Collections.Generic.IEnumerable{UUID}, System.Threading.CancellationToken)"/>.
    /// The viewer (LLMaterialMgr::processGetQueue) sends a zipped LLSD array of material IDs, each as
    /// LLMaterialID::asLLSD(): 16 bytes of binary.
    /// </summary>
    [TestFixture]
    public class RenderMaterialsRequestTests
    {
        private static readonly Uri CapUri = new Uri("http://test.invalid/rendermaterials");

        [Test]
        public async Task RequestedMaterialIds_AreSentAsBinary()
        {
            using var client = new FakeGridClient();
            client.AddCapability("RenderMaterials", CapUri);
            client.AddHttpResponse(CapUri, HttpStatusCode.OK, string.Empty, "application/llsd+xml");

            var ids = new[] { UUID.Random(), UUID.Random() };
            await client.Objects.RequestMaterialsAsync(client.Network.CurrentSim, ids);

            var post = client.CapturedRequests.Single(r => r.Uri == CapUri);
            var body = (OSDMap)OSDParser.Deserialize(System.Text.Encoding.UTF8.GetBytes(post.Body));
            var sent = (OSDArray)Helpers.ZDecompressOSD(body["Zipped"].AsBinary());

            Assert.That(sent.Count, Is.EqualTo(2));
            for (var i = 0; i < ids.Length; i++)
            {
                Assert.That(sent[i], Is.InstanceOf<OSDBinary>(), $"entry {i}");
                Assert.That(sent[i].AsBinary(), Is.EqualTo(ids[i].GetBytes()), $"entry {i}");
            }
        }
    }
}
