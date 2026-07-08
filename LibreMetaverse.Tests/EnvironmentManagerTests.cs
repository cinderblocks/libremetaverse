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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the ExtEnvironment (EEP) capability. Wire format verified against the reference
    /// viewer's LLEnvironment::coroRequestEnvironment/coroUpdateEnvironment/coroResetEnvironment
    /// (llenvironment.cpp): parcel_id is always a "parcelid" query-string parameter (never part of
    /// the request body), and resetting is an HTTP DELETE, not a POST with an empty body.
    /// </summary>
    [TestFixture]
    [Category("Environment")]
    public class EnvironmentManagerTests
    {
        private sealed class CapturedRequest
        {
            public string Method = string.Empty;
            public string RawUrl = string.Empty;
            public byte[] Body = Array.Empty<byte>();
        }

        private static (GridClient client, HttpListener listener, string prefix) SetupClientWithCap(string capName)
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var client = new GridClient();
            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);
            var caps = new Caps(sim, new Uri("http://127.0.0.1:1/seed"));
            caps._Caps[capName] = new Uri(prefix);
            sim.Caps = caps;
            client.Network.CurrentSim = sim;

            return (client, listener, prefix);
        }

        private static async Task<CapturedRequest> CaptureOneRequestAsync(HttpListener listener, int successStatus = 200)
        {
            var ctx = await listener.GetContextAsync();
            using var ms = new MemoryStream();
            await ctx.Request.InputStream.CopyToAsync(ms);

            var captured = new CapturedRequest
            {
                Method = ctx.Request.HttpMethod,
                RawUrl = ctx.Request.RawUrl ?? string.Empty,
                Body = ms.ToArray()
            };

            var replyMap = new OSDMap { ["success"] = OSD.FromBoolean(true) };
            var replyBytes = Encoding.UTF8.GetBytes(OSDParser.SerializeLLSDXmlString(replyMap));
            ctx.Response.StatusCode = successStatus;
            ctx.Response.ContentType = "application/llsd+xml";
            ctx.Response.ContentLength64 = replyBytes.Length;
            await ctx.Response.OutputStream.WriteAsync(replyBytes, 0, replyBytes.Length);
            ctx.Response.OutputStream.Close();

            return captured;
        }

        private static int GetFreePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        [Test]
        public async Task GetParcelEnvironment_UsesParcelidQueryParameter()
        {
            var (client, listener, _) = SetupClientWithCap("ExtEnvironment");
            try
            {
                var serverTask = CaptureOneRequestAsync(listener);
                var callTask = client.Environment.GetParcelEnvironmentAsync(42);

                var captured = await serverTask;
                await callTask;

                Assert.That(captured.Method, Is.EqualTo("GET"));
                Assert.That(captured.RawUrl, Does.Contain("parcelid=42"));
                Assert.That(captured.RawUrl, Does.Not.Contain("parcel_id"));
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task SetParcelEnvironment_SendsParcelidInQueryStringNotBody()
        {
            var (client, listener, _) = SetupClientWithCap("ExtEnvironment");
            try
            {
                var serverTask = CaptureOneRequestAsync(listener);
                var env = new EnvironmentData { DayLength = 7200 };
                var callTask = client.Environment.SetParcelEnvironmentAsync(7, env);

                var captured = await serverTask;
                await callTask;

                Assert.That(captured.Method, Is.EqualTo("POST"));
                Assert.That(captured.RawUrl, Does.Contain("parcelid=7"));

                var bodyMap = (OSDMap)OSDParser.Deserialize(captured.Body);
                Assert.That(bodyMap.ContainsKey("parcel_id"), Is.False, "parcel_id must not appear in the POST body");
                Assert.That(bodyMap.ContainsKey("environment"), Is.True);
                var envMap = (OSDMap)bodyMap["environment"];
                Assert.That(envMap["day_length"].AsInteger(), Is.EqualTo(7200));
                Assert.That(envMap.ContainsKey("sky_track"), Is.False, "sky_track is not a real ExtEnvironment field");
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task SetRegionEnvironment_OmitsQueryStringEntirely()
        {
            var (client, listener, _) = SetupClientWithCap("ExtEnvironment");
            try
            {
                var serverTask = CaptureOneRequestAsync(listener);
                var callTask = client.Environment.SetRegionEnvironmentAsync(new EnvironmentData());

                var captured = await serverTask;
                await callTask;

                Assert.That(captured.Method, Is.EqualTo("POST"));
                Assert.That(captured.RawUrl, Is.EqualTo("/"));
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task ResetParcelEnvironment_SendsHttpDeleteNotPost()
        {
            var (client, listener, _) = SetupClientWithCap("ExtEnvironment");
            try
            {
                var serverTask = CaptureOneRequestAsync(listener);
                var callTask = client.Environment.ResetParcelEnvironmentAsync(3);

                var captured = await serverTask;
                var succeeded = await callTask;

                Assert.That(captured.Method, Is.EqualTo("DELETE"));
                Assert.That(captured.RawUrl, Does.Contain("parcelid=3"));
                Assert.That(succeeded, Is.True);
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }
    }
}
