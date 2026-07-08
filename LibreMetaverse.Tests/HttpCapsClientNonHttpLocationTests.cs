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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Regression coverage for https://github.com/cinderblocks/libremetaverse/issues/113 -- AISv3's
    /// SlamFolder endpoint replies with a "slcaps://" Location/Content-Location header, which crashes
    /// .NET Framework's default HttpClientHandler (it wraps HttpWebRequest, which throws
    /// "Only 'http' and 'https' schemes are allowed" while building the response) even though the
    /// request itself succeeded. GridClient.HttpCapsClient must tolerate this on every target framework.
    /// </summary>
    [TestFixture]
    [Category("Http")]
    public class HttpCapsClientNonHttpLocationTests
    {
        [Test]
        public async Task PutAsync_ResponseWithNonHttpLocationHeader_DoesNotThrow()
        {
            using var listener = new HttpListener();
            var prefix = $"http://127.0.0.1:{GetFreePort()}/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            var serverTask = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                ctx.Response.StatusCode = 201;
                const string slcaps = "slcaps://11111111-1111-1111-1111-111111111111/category/x";
                ctx.Response.Headers.Add("Location", slcaps);
                ctx.Response.Headers.Add("Content-Location", slcaps);
                var body = Encoding.UTF8.GetBytes("<llsd><map/></llsd>");
                ctx.Response.ContentType = "application/llsd+xml";
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
                ctx.Response.OutputStream.Close();
            });

            var client = new GridClient();
            try
            {
                using var content = new StringContent("test", Encoding.UTF8, "application/llsd+xml");

                HttpResponseMessage? reply = null;
                Assert.DoesNotThrowAsync(async () =>
                {
                    reply = await client.HttpCapsClient.PutAsync(new Uri(prefix), content);
                });

                Assert.That(reply, Is.Not.Null);
                Assert.That((int)reply!.StatusCode, Is.EqualTo(201));

                await serverTask;
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        private static int GetFreePort()
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
    }
}
