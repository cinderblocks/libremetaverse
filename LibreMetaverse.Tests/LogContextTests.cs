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
using System.Net;
using System.Reflection;
using LibreMetaverse;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="Logger"/> asks for the name of whoever is logging through <see cref="ILogContext"/>, not through the
    /// grid client and the simulator, so that it does not depend on either. These tests use a context of their own to
    /// show that, and the real client and simulator to show they give what they gave before.
    /// </summary>
    [TestFixture]
    [NonParallelizable] // the logger and its level are shared by the whole process
    public class LogContextTests
    {
        private sealed class Context : ILogContext
        {
            public string LogClientName { get; set; }
            public string LogRegionName { get; set; }
        }

        private sealed class Capture : ILoggerFactory, ILogger
        {
            public readonly List<string> Entries = new List<string>();
            public readonly List<object> Scopes = new List<object>();

            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => this;
            public void Dispose() { }
            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state)
            {
                Scopes.Add(state);
                return null;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Entries.Add(formatter(state, exception));
            }
        }

        private Capture _capture;
        private LogLevel _level;

        [SetUp]
        public void SetUp()
        {
            _level = Settings.LogLevel;
            Settings.LogLevel = LogLevel.Trace;
            _capture = new Capture();
            Logger.SetLoggerFactory(_capture, "test");
        }

        [TearDown]
        public void TearDown()
        {
            Settings.LogLevel = _level;
            Logger.SetLoggerFactory(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, "test");
            _capture.Dispose();
        }

        private static string Region(object scope) => ((IDictionary<string, object>)scope)["Region"] as string;
        private static string Client(object scope) => ((IDictionary<string, object>)scope)["Client"] as string;

        [Test]
        public void Log_TagsTheEntryWithTheContextsName()
        {
            Logger.Log("hello", LogLevel.Information, new Context { LogClientName = "Alice Resident" });

            Assert.That(_capture.Entries, Is.EqualTo(new[] { "[Alice Resident] hello" }));
        }

        [Test]
        public void Log_ConvenienceMethods_TagTheEntryToo()
        {
            var context = new Context { LogClientName = "Alice Resident" };

            Logger.Info("one", context);
            Logger.Warn("two", new InvalidOperationException("boom"), context);

            Assert.That(_capture.Entries, Is.EqualTo(new[] { "[Alice Resident] one", "[Alice Resident] two" }));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Log_ContextWithNoName_TagsNothing(string name)
        {
            Logger.Log("hello", LogLevel.Information, new Context { LogClientName = name });
            Logger.Log("again", LogLevel.Information, (ILogContext)null);

            Assert.That(_capture.Entries, Is.EqualTo(new[] { "hello", "again" }));
        }

        [Test]
        public void BeginClientScope_CarriesTheContextsName()
        {
            using (Logger.BeginClientScope(new Context { LogClientName = "Alice Resident" })) { }

            Assert.That(_capture.Scopes, Has.Count.EqualTo(1));
            Assert.That(Client(_capture.Scopes[0]), Is.EqualTo("Alice Resident"));
        }

        [Test]
        public void BeginClientScope_ContextWithNoName_MakesNoScope()
        {
            using (Logger.BeginClientScope(new Context())) { }

            Assert.That(_capture.Scopes, Is.Empty);
        }

        [Test]
        public void BeginRegionScope_CarriesTheContextsRegion()
        {
            using (Logger.BeginRegionScope(new Context { LogRegionName = "Ahern" })) { }

            Assert.That(_capture.Scopes, Has.Count.EqualTo(1));
            Assert.That(Region(_capture.Scopes[0]), Is.EqualTo("Ahern"));
        }

        [Test]
        public void UseScopeAsync_RunsTheActionInsideTheScope()
        {
            bool ran = false;
            Logger.UseClientScopeAsync(new Context { LogClientName = "Alice Resident" }, () =>
            {
                ran = _capture.Scopes.Count == 1;
                return System.Threading.Tasks.Task.CompletedTask;
            }).GetAwaiter().GetResult();

            Assert.That(ran, Is.True);
        }

        [Test]
        public void GridClient_GivesItsAvatarsName_WhenTheSettingAsksForNames()
        {
            using var client = new GridClient();
            typeof(AgentManager).GetField("fullName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(client.Self, "Alice Resident");
            var context = (ILogContext)client;

            client.Settings.Logging.LogNames = true;
            Assert.That(context.LogClientName, Is.EqualTo("Alice Resident"));
            Assert.That(context.LogRegionName, Is.Null);

            client.Settings.Logging.LogNames = false;
            Assert.That(context.LogClientName, Is.Null);
        }

        [Test]
        public void GridClient_IsAcceptedWhereverALogContextIs()
        {
            using var client = new GridClient();
            typeof(AgentManager).GetField("fullName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(client.Self, "Alice Resident");

            Logger.Info("from the client", client);

            Assert.That(_capture.Entries, Is.EqualTo(new[] { "[Alice Resident] from the client" }));
        }

        [Test]
        public void Simulator_GivesItsNameOrElseItsHandle_AndItsClientsName()
        {
            using var client = new GridClient();
            typeof(AgentManager).GetField("fullName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(client.Self, "Alice Resident");
            var simulator = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 1099511628032UL);
            var context = (ILogContext)simulator;

            Assert.That(context.LogRegionName, Is.EqualTo("1099511628032"));
            simulator.Name = "Ahern";
            Assert.That(context.LogRegionName, Is.EqualTo("Ahern"));
            Assert.That(context.LogClientName, Is.EqualTo("Alice Resident"));

            using (Logger.BeginRegionScope(simulator)) { }
            Assert.That(Region(_capture.Scopes[0]), Is.EqualTo("Ahern"));
        }
    }
}
