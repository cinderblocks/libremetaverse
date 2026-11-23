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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// After a border crossing the region we left stays connected as a neighbor (Settings.MultipleSims). Its
    /// kill for our avatar usually arrives while it is still the current region, where it is not applied to our
    /// own avatar, so the crossing itself has to drop our avatar (and its attachments) from that region.
    /// </summary>
    [TestFixture]
    public class LeftRegionSelfCleanupTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly UUID Me = new UUID("11111111-2222-3333-4444-555555555555");
        private static readonly UUID Someone = new UUID("99999999-8888-7777-6666-555555555555");

        private FakeGridClient _client = null!;
        private Simulator _old = null!;
        private Simulator _new = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            _new = _client.Network.CurrentSim;
            _old = new Simulator(_client, new IPEndPoint(IPAddress.Loopback, 2), Utils.UIntsToLong(256 * 1000, 256 * 1001));
            typeof(AgentManager).GetProperty("AgentID", Flags)!.SetValue(_client.Self, Me);
            _client.Self.localID = 500;
        }

        [TearDown]
        public void TearDown()
        {
            try { _old.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
        }

        private static void AddAvatar(Simulator sim, uint localId, UUID id) =>
            sim.ObjectsAvatars[localId] = new Avatar { ID = id, LocalID = localId };

        private static void AddPrim(Simulator sim, uint localId, uint parentId) =>
            sim.ObjectsPrimitives[localId] = new Primitive { ID = UUID.Random(), LocalID = localId, ParentID = parentId };

        /// <summary>The old region as it is right after we left it: our avatar with an attachment (a linkset of two),
        /// another avatar with its own attachment, and an unrelated object.</summary>
        private void FillOldRegion()
        {
            AddAvatar(_old, 100, Me);
            AddPrim(_old, 101, 100);    // our attachment root
            AddPrim(_old, 102, 101);    // its child prim
            AddAvatar(_old, 200, Someone);
            AddPrim(_old, 201, 200);    // their attachment
            AddPrim(_old, 300, 0);      // a building
        }

        private async Task CompleteCrossing(Simulator from, Simulator to)
        {
            var agent = _client.Self;
            var agentType = typeof(AgentManager);
            var infoType = agentType.GetNestedType("CrossingInfo", BindingFlags.NonPublic)!;
            var stateType = agentType.GetNestedType("CrossingState", BindingFlags.Public)!;
            var crossing = Activator.CreateInstance(infoType, nonPublic: true)!;
            infoType.GetField("OldSimulator", Flags)!.SetValue(crossing, from);
            infoType.GetField("NewSimulator", Flags)!.SetValue(crossing, to);
            infoType.GetField("StartTime", Flags)!.SetValue(crossing, DateTime.UtcNow);
            infoType.GetField("State", Flags)!.SetValue(crossing, Enum.Parse(stateType, "Connecting"));
            agentType.GetField("_currentCrossing", Flags)!.SetValue(agent, crossing);
            agentType.GetMethod("NotifyMovementComplete", Flags)!.Invoke(agent, new object[] { to });
            var entered = (Task<bool>)agentType.GetMethod("EnterWaitingForCompleteAsync", Flags)!
                .Invoke(agent, new[] { crossing, to, (object)CancellationToken.None })!;
            Assert.That(await entered, Is.True);
        }

        [Test]
        public async Task CompletedCrossing_DropsOurAvatarAndAttachmentsFromTheRegionWeLeft()
        {
            FillOldRegion();
            AddAvatar(_new, 500, Me);

            await CompleteCrossing(_old, _new);

            Assert.That(_old.ObjectsAvatars.ContainsKey(100), Is.False, "our avatar in the region we left");
            Assert.That(_old.ObjectsPrimitives.ContainsKey(101), Is.False, "our attachment there");
            Assert.That(_old.ObjectsPrimitives.ContainsKey(102), Is.False, "its child prim");
            Assert.That(_old.ObjectsAvatars.ContainsKey(200), Is.True, "another avatar is kept");
            Assert.That(_old.ObjectsPrimitives.ContainsKey(201), Is.True, "their attachment is kept");
            Assert.That(_old.ObjectsPrimitives.ContainsKey(300), Is.True, "other objects are kept");
            Assert.That(_new.ObjectsAvatars.ContainsKey(500), Is.True, "our avatar in the region we entered");
            Assert.That(_client.Self.LocalID, Is.EqualTo(500u));
        }

        [Test]
        public async Task CompletedCrossing_RaisesKillEventsForTheDroppedObjects()
        {
            FillOldRegion();
            var killed = new List<(Simulator, uint)>();
            _client.Objects.KillObject += (_, e) => { lock (killed) killed.Add((e.Simulator, e.ObjectLocalID)); };

            await CompleteCrossing(_old, _new);

            Assert.That(killed, Is.EquivalentTo(new[] { (_old, 100u) }));
        }

        [Test]
        public async Task CompletedCrossing_WithoutOurAvatarInTheOldRegion_ChangesNothing()
        {
            AddAvatar(_old, 200, Someone);
            AddPrim(_old, 300, 0);
            var killed = 0;
            _client.Objects.KillObjects += (_, _) => Interlocked.Increment(ref killed);

            await CompleteCrossing(_old, _new);

            Assert.That(_old.ObjectsAvatars.Count, Is.EqualTo(1));
            Assert.That(_old.ObjectsPrimitives.Count, Is.EqualTo(1));
            Assert.That(killed, Is.EqualTo(0));
        }

        [Test]
        public void ForgetSelfIn_TheCurrentRegion_KeepsOurAvatar()
        {
            AddAvatar(_new, 500, Me);
            AddPrim(_new, 501, 500);

            _client.Self.ForgetSelfIn(_new);

            Assert.That(_new.ObjectsAvatars.ContainsKey(500), Is.True);
            Assert.That(_new.ObjectsPrimitives.ContainsKey(501), Is.True);
        }

        [Test]
        public void KillFromTheRegionWeAreLeaving_IsStillIgnoredForOurAvatar()
        {
            // the reason for the cleanup: while the old region is current, its kill for us is not applied
            _client.Network.CurrentSim = _old;
            _client.Self.localID = 100;
            FillOldRegion();
            var packet = new KillObjectPacket { ObjectData = new[] { new KillObjectPacket.ObjectDataBlock { ID = 100 } } };
            typeof(ObjectManager).GetMethod("KillObjectHandler", Flags)!
                .Invoke(_client.Objects, new object?[] { null, new PacketReceivedEventArgs(packet, _old) });

            Assert.That(_old.ObjectsAvatars.ContainsKey(100), Is.True);
        }
    }
}
