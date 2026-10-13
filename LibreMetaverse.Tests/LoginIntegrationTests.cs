/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The client logging in to a login server that is written with the library's own login types: the server reads the
    /// request with <see cref="LoginRequest"/> and answers with <see cref="LoginResponseData.ToOSD"/>, over HTTP as LLSD XML.
    /// </summary>
    [TestFixture]
    [Category("Login")]
    public class LoginIntegrationTests
    {
        private sealed class LoginServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            public readonly string Prefix;
            public readonly List<(string Path, OSDMap Raw, LoginRequest Request)> Requests = new List<(string, OSDMap, LoginRequest)>();
            public Func<string, LoginRequest, LoginResponseData> Respond = (path, request) => new LoginResponseData { Login = LoginState.False, Reason = "key", Message = "no" };

            public LoginServer()
            {
                var port = FreePort();
                Prefix = $"http://127.0.0.1:{port}/";
                _listener.Prefixes.Add(Prefix);
                _listener.Start();
                _ = Task.Run(Loop);
            }

            public string Url(string path) => Prefix + path;

            private static int FreePort()
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint!).Port;
            }

            private async Task Loop()
            {
                while (!_cts.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
                    catch { return; }

                    try
                    {
                        byte[] body;
                        using (var ms = new MemoryStream())
                        {
                            await context.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                            body = ms.ToArray();
                        }

                        var raw = (OSDMap)OSDParser.Deserialize(body);
                        var request = LoginRequest.FromOSD(raw);
                        string path = context.Request.Url!.AbsolutePath;
                        lock (Requests) Requests.Add((path, raw, request));

                        var reply = OSDParser.SerializeLLSDXmlBytes(Respond(path, request).ToOSD());
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/llsd+xml";
                        context.Response.ContentLength64 = reply.Length;
                        await context.Response.OutputStream.WriteAsync(reply, 0, reply.Length).ConfigureAwait(false);
                    }
                    catch { }
                    finally
                    {
                        try { context.Response.OutputStream.Close(); } catch { }
                    }
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
                try { _listener.Close(); } catch { }
            }
        }

        // The timeout covers the whole login, the first one in a run includes starting up the HTTP client, and after a
        // successful login it covers the wait for a simulator that is not there
        private static GridClient NewClient(int loginTimeout = 30000)
        {
            var client = new GridClient();
            client.Settings.Timing.LoginTimeout = loginTimeout;
            return client;
        }

        private static LoginParams Params(GridClient client, string uri) =>
            new LoginParams(client, "Test", "Resident", "secret", "My Viewer", "1.2.3", uri);

        [Test]
        public async Task TheRequestOnTheWire_IsWhatTheClientHasAlwaysSent()
        {
            using var server = new LoginServer();
            var client = NewClient();
            var loginParams = Params(client, server.Url("login"));
            loginParams.Start = "home";
            client.Network.RegisterLoginResponseCallback((ok, redirect, message, reason, data) => { }, new[] { "an-option-a-callback-wants" });

            await client.Network.LoginAsync(loginParams);

            Assert.That(server.Requests, Has.Count.EqualTo(1));
            var (path, raw, request) = server.Requests[0];
            Assert.Multiple(() =>
            {
                Assert.That(path, Is.EqualTo("/login"));
                Assert.That(raw.Keys, Is.EqualTo(new[]
                {
                    "first", "last", "passwd", "start", "channel", "version", "platform", "platform_version", "mac",
                    "agree_to_tos", "read_critical", "viewer_digest", "id0", "last_exec_event", "options"
                }));
                Assert.That(request.FirstName, Is.EqualTo("Test"));
                Assert.That(request.LastName, Is.EqualTo("Resident"));
                Assert.That(request.Password, Is.EqualTo(Utils.MD5("secret")));
                Assert.That(request.Start, Is.EqualTo("home"));
                Assert.That(request.Channel, Is.EqualTo("My Viewer"));
                Assert.That(request.Version, Is.EqualTo("1.2.3"));
                Assert.That(request.AgreeToTos, Is.True);
                Assert.That(request.ReadCritical, Is.True);
                Assert.That(request.LastExecEvent, Is.EqualTo(LastExecStatus.Normal));
                Assert.That(request.HasMfa, Is.False);
                Assert.That(request.Options, Does.Contain("inventory-root").And.Contain("buddy-list"));
                Assert.That(request.Options.Last(), Is.EqualTo("an-option-a-callback-wants"));
                Assert.That(request.Options, Is.Unique);
            });
        }

        [Test]
        public async Task AClientThatDoesMultiFactor_SendsTheTokenAndTheHash()
        {
            using var server = new LoginServer();
            var client = NewClient();
            client.Settings.Connection.MfaEnabled = true;
            var loginParams = new LoginParams(client, new LoginCredential("Test", "Resident", "secret", "654321", "saved-hash"), "My Viewer", "1.2.3", server.Url("login"));

            await client.Network.LoginAsync(loginParams);

            var request = server.Requests.Single().Request;
            Assert.Multiple(() =>
            {
                Assert.That(request.HasMfa, Is.True);
                Assert.That(request.Token, Is.EqualTo("654321"));
                Assert.That(request.MfaHash, Is.EqualTo("saved-hash"));
            });
        }

        [Test]
        public async Task AFailedLogin_GivesTheClientTheReasonAndTheMessage()
        {
            using var server = new LoginServer();
            server.Respond = (path, request) => new LoginResponseData { Login = LoginState.False, Reason = "key", Message = "Could not authenticate your avatar." };
            var client = NewClient();

            bool ok = await client.Network.LoginAsync(Params(client, server.Url("login")));

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(client.Network.LoginStatusCode, Is.EqualTo(LoginStatus.Failed));
                Assert.That(client.Network.LoginErrorKey, Is.EqualTo("key"));
                Assert.That(client.Network.LoginMessage, Is.EqualTo("Could not authenticate your avatar."));
            });
        }

        [Test]
        public async Task ARedirect_SendsTheClientOnToTheNextUrl()
        {
            using var server = new LoginServer();
            server.Respond = (path, request) => path == "/first"
                ? new LoginResponseData { Login = LoginState.Indeterminate, Message = "Try the other server", NextUrl = server.Url("second"), NextMethod = "login_to_simulator", NextDuration = 0 }
                : new LoginResponseData { Login = LoginState.False, Reason = "presence", Message = "Second server says no" };
            var client = NewClient();

            await client.Network.LoginAsync(Params(client, server.Url("first")));

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests.Select(r => r.Path), Is.EqualTo(new[] { "/first", "/second" }));
                Assert.That(server.Requests[1].Request.FirstName, Is.EqualTo("Test"));
                Assert.That(client.Network.LoginErrorKey, Is.EqualTo("presence"));
            });
        }

        [Test]
        public async Task ARedirectedLogin_IsNotOverUntilTheNextServerHasAnswered()
        {
            using var server = new LoginServer();
            server.Respond = (path, request) =>
            {
                if (path == "/first")
                    return new LoginResponseData { Login = LoginState.Indeterminate, Message = "Try the other server", NextUrl = server.Url("second"), NextMethod = "login_to_simulator", NextDuration = 0 };
                Thread.Sleep(300);   // the second server is slow to answer
                return new LoginResponseData { Login = LoginState.False, Reason = "presence", Message = "Second server says no" };
            };
            var client = NewClient();

            bool ok = await client.Network.LoginAsync(Params(client, server.Url("first")));

            Assert.Multiple(() =>
            {
                Assert.That(ok, Is.False);
                Assert.That(server.Requests.Select(r => r.Path), Is.EqualTo(new[] { "/first", "/second" }));
                Assert.That(client.Network.LoginErrorKey, Is.EqualTo("presence"), "the login finished with the second server's answer, not before it");
                Assert.That(client.Network.LoginMessage, Is.EqualTo("Second server says no"));
            });
        }

        [Test]
        public async Task ASuccessfulLogin_IsReadByTheClientAsTheServerWroteIt()
        {
            using var server = new LoginServer();
            var sent = new LoginResponseData
            {
                Login = LoginState.True,
                AgentID = UUID.Random(),
                SessionID = UUID.Random(),
                SecureSessionID = UUID.Random(),
                FirstName = "Test",
                LastName = "Resident",
                StartLocation = "last",
                Message = "Welcome",
                CircuitCode = 424242,
                RegionX = 256000,
                RegionY = 256256,
                RegionSizeX = 256,
                RegionSizeY = 256,
                SimIP = IPAddress.Loopback,
                SimPort = 9,   // nothing is listening, so the client gives up on the simulator after it has read this
                SeedCapability = "http://127.0.0.1:9/seed",
                SecondsSinceEpoch = 1700000000,
                InventoryRoot = UUID.Random(),
                MaxAgentGroups = 42
            };
            sent.InventorySkeleton = new[] { new InventoryFolder(sent.InventoryRoot) { Name = "My Inventory", PreferredType = FolderType.Root, Version = 3, OwnerID = sent.AgentID } };
            server.Respond = (path, request) => sent;
            var client = NewClient();
            var received = new TaskCompletionSource<LoginResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Network.RegisterLoginResponseCallback((ok, redirect, message, reason, data) => received.TrySetResult(data));

            var login = client.Network.LoginAsync(Params(client, server.Url("login")));
            var data = await Task.WhenAny(received.Task, Task.Delay(20000)) == received.Task ? received.Task.Result : null;

            // The client now goes on to look for a simulator that is not there; there is nothing more to see
            client.Network.AbortLogin();
            await Task.WhenAny(login, Task.Delay(10000));

            Assert.That(data, Is.Not.Null, "the client was told of the login");
            Assert.Multiple(() =>
            {
                Assert.That(data.Success, Is.True);
                Assert.That(data.AgentID, Is.EqualTo(sent.AgentID));
                Assert.That(data.SessionID, Is.EqualTo(sent.SessionID));
                Assert.That(data.CircuitCode, Is.EqualTo(424242));
                Assert.That(data.SimIP, Is.EqualTo(IPAddress.Loopback));
                Assert.That(data.SimPort, Is.EqualTo((ushort)9));
                Assert.That(data.SeedCapability, Is.EqualTo("http://127.0.0.1:9/seed"));
                Assert.That(data.InventoryRoot, Is.EqualTo(sent.InventoryRoot));
                Assert.That(data.InventorySkeleton.Single().Name, Is.EqualTo("My Inventory"));
                Assert.That(data.MaxAgentGroups, Is.EqualTo(42));
                Assert.That(client.Network.CircuitCode, Is.EqualTo(424242u));
                Assert.That(client.Network.LoginSeedCapability, Is.EqualTo(new Uri("http://127.0.0.1:9/seed")));
            });
        }
    }
}
