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
using System.Reflection;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// AgentSettings.FastRegionCrossing: crossing in to a neighbor we already have a child agent in keeps its
    /// circuit, capabilities and our appearance state. Off by default, and only during a crossing.
    /// </summary>
    [TestFixture]
    public class FastRegionCrossingTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly Uri Seed = new Uri("http://fake/seed");

        private FakeGridClient _client = null!;
        private Simulator _neighbor = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            _neighbor = new Simulator(_client, new IPEndPoint(IPAddress.Loopback, 2), Utils.UIntsToLong(256 * 1000, 256 * 1001));
        }

        [TearDown]
        public void TearDown()
        {
            try { _neighbor.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
        }

        private void SetCrossingState(string? state)
        {
            var agentType = typeof(AgentManager);
            object? crossing = null;
            if (state != null)
            {
                var infoType = agentType.GetNestedType("CrossingInfo", BindingFlags.NonPublic)!;
                crossing = Activator.CreateInstance(infoType, nonPublic: true)!;
                infoType.GetField("State", Flags)!.SetValue(crossing,
                    Enum.Parse(agentType.GetNestedType("CrossingState", BindingFlags.Public)!, state));
            }
            agentType.GetField("_currentCrossing", Flags)!.SetValue(_client.Self, crossing);
        }

        private void MarkCircuitUp(Simulator sim)
        {
            typeof(Simulator).GetField("connected", Flags)!.SetValue(sim, true);
            typeof(Simulator).GetField("handshakeComplete", Flags)!.SetValue(sim, true);
        }

        [Test]
        public void IsOffByDefault()
        {
            Assert.That(new Settings(_client).Agent.FastRegionCrossing, Is.False);
        }

        [TestCase("PreparingCross", true)]
        [TestCase("Connecting", true)]
        [TestCase("Recovering", true)]
        [TestCase("WaitingForComplete", true)]
        [TestCase("Completed", false)]
        [TestCase("Failed", false)]
        [TestCase("Idle", false)]
        [TestCase(null, false)]
        public void IsCrossingRegion_FollowsTheCrossingState(string? state, bool expected)
        {
            SetCrossingState(state);
            Assert.That(_client.Self.IsCrossingRegion, Is.EqualTo(expected));
        }

        [TestCase(false, "Connecting", true, false)]
        [TestCase(true, null, true, false)]
        [TestCase(true, "Connecting", false, false)]
        [TestCase(true, "Connecting", true, true)]
        public void CircuitIsReusedOnlyWhenEnabledCrossingAndUp(bool enabled, string? state, bool circuitUp, bool expected)
        {
            _client.Settings.Agent.FastRegionCrossing = enabled;
            SetCrossingState(state);
            if (circuitUp) MarkCircuitUp(_neighbor);

            Assert.That(_client.Network.ReuseCircuitForMoveIn(_neighbor), Is.EqualTo(expected));
        }

        [TestCase(false, "Connecting", false)]
        [TestCase(true, null, false)]
        [TestCase(true, "Connecting", true)]
        public void SameSeedOnMoveIn_KeepsCapsOnlyWhenEnabledAndCrossing(bool enabled, string? state, bool kept)
        {
            _neighbor.SetSeedCaps(Seed);
            var caps = _neighbor.Caps;
            _client.Settings.Agent.FastRegionCrossing = enabled;
            SetCrossingState(state);

            _neighbor.SetSeedCaps(Seed, changedSim: true);

            Assert.That(ReferenceEquals(_neighbor.Caps, caps), Is.EqualTo(kept));
            Assert.That(_neighbor.Caps, Is.Not.Null);
        }

        [Test]
        public void NewSeedOnMoveIn_ReplacesCapsEvenWhenEnabled()
        {
            _neighbor.SetSeedCaps(Seed);
            var caps = _neighbor.Caps;
            _client.Settings.Agent.FastRegionCrossing = true;
            SetCrossingState("Connecting");

            _neighbor.SetSeedCaps(new Uri("http://fake/other-seed"), changedSim: true);

            Assert.That(_neighbor.Caps, Is.Not.SameAs(caps));
        }

        [TestCase(false, "Connecting", true)]
        [TestCase(true, null, true)]
        [TestCase(true, "Connecting", false)]
        public void SimChanged_ResetsAppearanceStateUnlessFastCrossing(bool enabled, string? state, bool reset)
        {
            var appearance = _client.Appearance;
            var sent = typeof(AppearanceManager).GetField("_lastSentOutfitCOFVersion", Flags)!;
            sent.SetValue(appearance, 7);
            _client.Settings.Agent.FastRegionCrossing = enabled;
            SetCrossingState(state);

            typeof(AppearanceManager).GetMethod("Network_OnSimChanged", Flags)!
                .Invoke(appearance, new object?[] { null, new SimChangedEventArgs(_neighbor) });

            Assert.That(sent.GetValue(appearance), Is.EqualTo(reset ? -1 : 7));
        }
    }
}
