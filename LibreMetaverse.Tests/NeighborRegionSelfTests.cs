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

using System.Collections.Generic;
using System.Net;
using System.Reflection;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Local IDs are only unique within one region. With neighbor regions connected (Settings.MultipleSims) a
    /// neighbor can have an object with the same local ID as our avatar in the current region; that object
    /// must not be taken for us.
    /// </summary>
    [TestFixture]
    public class NeighborRegionSelfTests
    {
        private const uint SelfLocalId = 777u;
        private FakeGridClient _client = null!;
        private Simulator _current = null!;
        private Simulator _neighbor = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.Settings.World.AlwaysDecodeObjects = true;
            _client.AddCapability("Placeholder", new System.Uri("http://test.invalid/placeholder"));
            _current = _client.Network.CurrentSim;
            _neighbor = new Simulator(_client, new IPEndPoint(IPAddress.Loopback, 1), Utils.UIntsToLong(256 * 1001, 256 * 1000));
            _client.Self.localID = SelfLocalId;
            _client.Self.RelativePosition = new Vector3(10f, 20f, 30f);
        }

        [TearDown]
        public void TearDown()
        {
            try { _neighbor.Dispose(); } catch { }
            try { _client.Dispose(); } catch { }
        }

        private static byte[] TerseBlock(uint localId, Vector3 position)
        {
            var data = new List<byte>();
            data.AddRange(Utils.UIntToBytes(localId));
            data.Add(0);                    // state
            data.Add(0);                    // not an avatar
            data.AddRange(position.GetBytes());
            data.AddRange(new byte[6 + 6 + 8 + 6]); // velocity, acceleration, rotation, angular velocity
            return data.ToArray();
        }

        private void Invoke(string handler, Packet packet, Simulator sim)
        {
            var method = typeof(ObjectManager).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_client.Objects, new object?[] { null, new PacketReceivedEventArgs(packet, sim) });
        }

        private void Terse(Simulator sim, uint localId, Vector3 position)
        {
            var packet = new ImprovedTerseObjectUpdatePacket
            {
                RegionData = { RegionHandle = sim.Handle, TimeDilation = ushort.MaxValue },
                ObjectData = new[]
                {
                    new ImprovedTerseObjectUpdatePacket.ObjectDataBlock
                    {
                        Data = TerseBlock(localId, position), TextureEntry = Utils.EmptyBytes
                    }
                }
            };
            Invoke("ImprovedTerseObjectUpdateHandler", packet, sim);
        }

        [Test]
        public void TerseUpdate_FromANeighbor_WithOurLocalID_DoesNotMoveUs()
        {
            Terse(_neighbor, SelfLocalId, new Vector3(200f, 200f, 50f));

            Assert.That(_client.Self.RelativePosition, Is.EqualTo(new Vector3(10f, 20f, 30f)));
        }

        [Test]
        public void TerseUpdate_FromTheCurrentRegion_MovesUs()
        {
            Terse(_current, SelfLocalId, new Vector3(11f, 21f, 31f));

            Assert.That(_client.Self.RelativePosition, Is.EqualTo(new Vector3(11f, 21f, 31f)));
        }

        [Test]
        public void Kill_InANeighbor_OfAnObjectWithOurLocalID_RemovesThatObject()
        {
            _client.Objects.GetPrimitive(_neighbor, SelfLocalId, UUID.Random(), true);
            var killed = new List<(Simulator, uint)>();
            _client.Objects.KillObject += (_, e) => killed.Add((e.Simulator, e.ObjectLocalID));

            var kill = new KillObjectPacket { ObjectData = new[] { new KillObjectPacket.ObjectDataBlock { ID = SelfLocalId } } };
            Invoke("KillObjectHandler", kill, _neighbor);

            Assert.Multiple(() =>
            {
                Assert.That(_neighbor.ObjectsPrimitives.ContainsKey(SelfLocalId), Is.False);
                Assert.That(killed, Is.EqualTo(new[] { (_neighbor, SelfLocalId) }));
            });
        }

        [Test]
        public void Kill_InTheCurrentRegion_OfOurLocalID_IsIgnored()
        {
            var killed = new List<uint>();
            _client.Objects.KillObject += (_, e) => killed.Add(e.ObjectLocalID);

            var kill = new KillObjectPacket { ObjectData = new[] { new KillObjectPacket.ObjectDataBlock { ID = SelfLocalId } } };
            Invoke("KillObjectHandler", kill, _current);

            Assert.That(killed, Is.Empty);
        }

        [Test]
        public void SelfLocalIDIn_ANewRegion_ComesFromOurAvatarThere()
        {
            var self = UUID.Random();
            var other = UUID.Random();

            Assert.Multiple(() =>
            {
                Assert.That(AgentManager.SelfLocalIDIn(5, new[] { (9u, self), (12u, other) }, self), Is.EqualTo(9u),
                    "the region's entry for us");
                Assert.That(AgentManager.SelfLocalIDIn(5, new[] { (12u, other) }, self), Is.EqualTo(5u),
                    "no entry for us yet: keep what we have until its full update arrives");
                Assert.That(AgentManager.SelfLocalIDIn(9, new[] { (9u, self), (3u, self) }, self), Is.EqualTo(9u),
                    "the ID we hold is already ours there");
                Assert.That(AgentManager.SelfLocalIDIn(5, new[] { (3u, self), (9u, self) }, self), Is.EqualTo(9u),
                    "an older entry from an earlier visit has the lower ID");
            });
        }
    }
}
