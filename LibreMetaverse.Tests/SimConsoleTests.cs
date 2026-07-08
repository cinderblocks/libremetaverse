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
using System.Net.Sockets;
using System.Threading.Tasks;
using LibreMetaverse.Messages.Linden;
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
    [TestFixture]
    [Category("Estate")]
    public class SimConsoleTests
    {
        private static (GridClient client, Simulator sim, HttpListener listener, string prefix) SetupClientWithSimConsoleCap()
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var client = new GridClient();
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);
            var caps = new Caps(sim, new Uri("http://127.0.0.1:1/seed"));
            caps._Caps["SimConsoleAsync"] = new Uri(prefix);
            sim.Caps = caps;
            client.Network.CurrentSim = sim;

            return (client, sim, listener, prefix);
        }

        private static async Task RespondOkAsync(HttpListener listener)
        {
            var ctx = await listener.GetContextAsync();
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = 0;
            ctx.Response.OutputStream.Close();
        }

        private static int GetFreePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        [Test]
        public async Task SendSimConsoleCommandAsync_IgnoresPostBody_UsesSimConsoleResponseEvent()
        {
            var (client, sim, listener, _) = SetupClientWithSimConsoleCap();
            try
            {
                var serverTask = RespondOkAsync(listener);
                var callTask = client.Estate.SendSimConsoleCommandAsync("show name",
                    TimeSpan.FromSeconds(5));

                await serverTask;

                // Give the POST call a moment to move past the (ignored) response before the
                // SimConsoleResponse event arrives, mirroring the real out-of-band delivery.
                await Task.Delay(50);

                client.Network.CapsEvents.RaiseEvent("SimConsoleResponse",
                    new SimConsoleResponseMessage { Body = "Agent Name" }, sim);

                var result = await callTask;

                Assert.That(result, Is.EqualTo("Agent Name"));
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task SendSimConsoleCommandAsync_NoResponseEvent_ReturnsNullAfterTimeout()
        {
            var (client, _, listener, _) = SetupClientWithSimConsoleCap();
            try
            {
                var serverTask = RespondOkAsync(listener);
                var callTask = client.Estate.SendSimConsoleCommandAsync("show name",
                    TimeSpan.FromMilliseconds(200));

                await serverTask;
                var result = await callTask;

                Assert.That(result, Is.Null);
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
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
