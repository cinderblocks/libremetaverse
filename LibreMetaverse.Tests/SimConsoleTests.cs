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
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the SimConsoleAsync capability. Verified against the reference viewer's
    /// LLFloaterRegionDebugConsole (llfloaterregiondebugconsole.cpp): the POST response carries no
    /// output for the async capability -- the simulator's text arrives separately via a
    /// SimConsoleResponse event-queue message. An earlier version of this code instead tried to
    /// read the output directly from the POST's HTTP response body, which the real protocol never
    /// populates.
    /// </summary>
    /// <remarks>
    /// This previously drove a real <c>HttpListener</c> over a loopback socket, synchronizing on a
    /// fixed <c>Task.Delay(50)</c> before raising the out-of-band event. Under the full suite's
    /// parallel test load that occasionally wasn't enough lead time for the real POST to land,
    /// producing a rare flake (result null instead of the expected body). <see cref="FakeGridClient"/>
    /// replaces the real socket with an in-memory handler, which removes the timing dependency
    /// entirely: <see cref="EstateTools.SendSimConsoleCommandAsync"/> arms its
    /// <c>TaskCompletionSource</c> before awaiting the capability POST, so the event can be raised as
    /// soon as the POST has been observed rather than after a guessed delay.
    /// </remarks>
    [TestFixture]
    [Category("Estate")]
    public class SimConsoleTests
    {
        private const string CapUrl = "http://test.invalid/sim-console";

        private FakeGridClient _client = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("SimConsoleAsync", new Uri(CapUrl));
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        /// <summary>Waits for a deterministic, observable effect of the call having progressed past
        /// its capability POST, instead of assuming any fixed amount of real time is enough.</summary>
        private async Task WaitForThePostAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_client.CapturedRequests.Count == 0)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("SimConsoleAsync POST was never observed by the fake handler");
                }
                await Task.Delay(1);
            }
        }

        [Test]
        public async Task SendSimConsoleCommandAsync_IgnoresPostBody_UsesSimConsoleResponseEvent()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var callTask = _client.Estate.SendSimConsoleCommandAsync("show name", TimeSpan.FromSeconds(5));

            await WaitForThePostAsync(TimeSpan.FromSeconds(5));

            _client.Network.CapsEvents.RaiseEvent("SimConsoleResponse",
                new SimConsoleResponseMessage { Body = "Agent Name" }, _client.Network.CurrentSim);

            var result = await callTask;

            Assert.That(result, Is.EqualTo("Agent Name"));
        }

        [Test]
        public async Task SendSimConsoleCommandAsync_NoResponseEvent_ReturnsNullAfterTimeout()
        {
            _client.AddHttpResponse(new Uri(CapUrl), HttpStatusCode.OK, string.Empty, "application/json");

            var result = await _client.Estate.SendSimConsoleCommandAsync("show name", TimeSpan.FromMilliseconds(200));

            Assert.That(result, Is.Null);
        }

        [Test]
        public void SimConsoleResponseMessage_Deserialize_ReadsBodyField()
        {
            var map = new LibreMetaverse.StructuredData.OSDMap
            {
                ["body"] = LibreMetaverse.StructuredData.OSD.FromString("console output text")
            };

            var msg = new SimConsoleResponseMessage();
            msg.Deserialize(map);

            Assert.That(msg.Body, Is.EqualTo("console output text"));
        }
    }
}
