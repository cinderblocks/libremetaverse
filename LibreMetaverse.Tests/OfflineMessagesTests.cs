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
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the ReadOfflineMsgs capability. Verified against the reference viewer
    /// (llimprocessing.cpp requestOfflineMessagesCoro): the response content may currently be either
    /// a bare array (whose first element is the actual message array) or a map with the messages
    /// under a "messages" key -- both forms are valid depending on the grid's server-side rollout
    /// state. An earlier version of this code only accepted the bare-array shape and treated the
    /// map shape as a hard failure, silently discarding it and always falling back to the legacy
    /// UDP RetrieveInstantMessages path.
    /// </summary>
    [TestFixture]
    public class OfflineMessagesTests
    {
        private const string CapUrl = "http://test.invalid/read-offline-msgs";

        private static FakeGridClient MakeClientWithCap()
        {
            var client = new FakeGridClient();
            client.AddCapability("ReadOfflineMsgs", new Uri(CapUrl));
            client.AddCapability("AcceptFriendship", new Uri("http://test.invalid/accept-friendship"));
            client.AddCapability("AcceptGroupInvite", new Uri("http://test.invalid/accept-group-invite"));
            return client;
        }

        private static string MessageJson(string fromName, string text) =>
            "{\"from_agent_id\":\"11111111-1111-1111-1111-111111111111\"," +
            $"\"from_agent_name\":\"{fromName}\"," +
            "\"to_agent_id\":\"22222222-2222-2222-2222-222222222222\"," +
            "\"region_id\":\"33333333-3333-3333-3333-333333333333\"," +
            "\"dialog\":0," +
            "\"transaction-id\":\"44444444-4444-4444-4444-444444444444\"," +
            "\"timestamp\":0," +
            $"\"message\":\"{text}\"," +
            "\"local_x\":1.0,\"local_y\":2.0,\"local_z\":3.0}";

        [Test]
        public async Task RetrieveInstantMessagesAsync_BareArrayOfArraysResponse_RaisesInstantMessage()
        {
            var client = MakeClientWithCap();
            try
            {
                client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                    $"[[{MessageJson("Jim", "hello")}]]", "application/json");

                var received = new List<InstantMessage>();
                client.Self.IM += (s, e) => received.Add(e.IM);

                await client.Self.RetrieveInstantMessagesAsync();

                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Message, Is.EqualTo("hello"));
                Assert.That(received[0].FromAgentName, Is.EqualTo("Jim"));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public async Task RetrieveInstantMessagesAsync_MessagesMapResponse_RaisesInstantMessage()
        {
            var client = MakeClientWithCap();
            try
            {
                client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK,
                    $"{{\"messages\":[{MessageJson("Jim", "hello via map")}]}}", "application/json");

                var received = new List<InstantMessage>();
                client.Self.IM += (s, e) => received.Add(e.IM);

                await client.Self.RetrieveInstantMessagesAsync();

                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Message, Is.EqualTo("hello via map"));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }
    }
}
