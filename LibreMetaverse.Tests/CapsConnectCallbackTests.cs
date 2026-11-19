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

#if NET5_0_OR_GREATER
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="GridClient.SetCapsConnectCallback"/>: caps requests open their connection through the callback, keep
    /// the request's host name, and still go through the usual caps client pipeline.
    /// </summary>
    [TestFixture]
    [Category("Http")]
    public class CapsConnectCallbackTests
    {
        [Test]
        public async Task Request_ConnectsThroughCallback_WithOriginalHost()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            string? seenHost = null, seenAgent = null;
            var serverTask = Task.Run(async () =>
            {
                using var conn = await listener.AcceptTcpClientAsync();
                var stream = conn.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII);
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) seenHost = line.Substring(5).Trim();
                    if (line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase)) seenAgent = line.Substring(11).Trim();
                }
                const string body = "<llsd><map/></llsd>";
                var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/llsd+xml\r\n" +
                    $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
                await stream.WriteAsync(reply, 0, reply.Length);
            });

            var client = new GridClient();
            var calls = 0;
            DnsEndPoint? asked = null;
            client.SetCapsConnectCallback(async (context, token) =>
            {
                Interlocked.Increment(ref calls);
                asked = context.DnsEndPoint;
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), token);
                return new NetworkStream(socket, ownsSocket: true);
            });
            try
            {
                // a host name that does not resolve: only the callback can reach the listener
                var uri = new Uri($"http://caps.example.invalid:{port}/cap");
                var (response, data) = await client.HttpCapsClient.GetAsync(uri, CancellationToken.None);
                await serverTask;

                Assert.That(response, Is.Not.Null);
                Assert.That((int)response!.StatusCode, Is.EqualTo(200));
                Assert.That(data, Is.Not.Null.And.Not.Empty);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(asked!.Host, Is.EqualTo("caps.example.invalid"));
                Assert.That(asked.Port, Is.EqualTo(port));
                Assert.That(seenHost, Is.EqualTo($"caps.example.invalid:{port}"));
                Assert.That(seenAgent, Is.EqualTo(Settings.UserAgent));
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task Https_StillValidatesTheServerCertificate()
        {
            // a self-signed certificate for the requested host: the default security settings must still reject it
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=caps.example.invalid", rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var san = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
            san.AddDnsName("caps.example.invalid");
            req.CertificateExtensions.Add(san.Build());
            using var made = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(made.Export(
                System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                using var conn = await listener.AcceptTcpClientAsync();
                using var ssl = new System.Net.Security.SslStream(conn.GetStream());
                try { await ssl.AuthenticateAsServerAsync(cert); } catch (Exception) { /* the client aborts the handshake */ }
            });

            var client = new GridClient();
            client.SetCapsConnectCallback(async (_, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), token);
                return new NetworkStream(socket, ownsSocket: true);
            });
            try
            {
                Assert.That(async () => await client.HttpCapsClient.GetAsync(new Uri($"https://caps.example.invalid:{port}/cap"), CancellationToken.None),
                    Throws.InstanceOf<HttpRequestException>());
                await serverTask;
            }
            finally
            {
                client.HttpCapsClient.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public void SetCapsConnectCallback_ReplacesAndDisposesThePreviousClient()
        {
            var client = new GridClient();
            var first = client.HttpCapsClient;
            client.SetCapsConnectCallback((_, _) => throw new InvalidOperationException("not used"));
            var second = client.HttpCapsClient;
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(() => first.CancelPendingRequests(), Throws.TypeOf<ObjectDisposedException>());

            client.SetCapsConnectCallback(null);
            Assert.That(client.HttpCapsClient, Is.Not.SameAs(second));
            Assert.That(client.HttpCapsClient.Timeout, Is.EqualTo(second.Timeout));
            client.HttpCapsClient.Dispose();
        }

        [Test]
        public void Callback_Failure_SurfacesAsHttpRequestException()
        {
            var client = new GridClient();
            client.SetCapsConnectCallback((_, _) => throw new IOException("resolver down"));
            try
            {
                Assert.That(async () => await client.HttpCapsClient.GetAsync(new Uri("http://caps.example.invalid/cap"), CancellationToken.None),
                    Throws.InstanceOf<HttpRequestException>());
            }
            finally { client.HttpCapsClient.Dispose(); }
        }
    }
}
#endif
