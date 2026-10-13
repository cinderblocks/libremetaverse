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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Channels;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// How a <see cref="Simulator"/> keeps its UDP circuit reliable: numbering the datagrams it sends, remembering the
    /// reliable ones until they are acknowledged, resending, acknowledging what it receives, and not acting twice on
    /// a packet it has already had. The simulator is a real one on a real loopback socket, with a plain UdpClient as
    /// the far end, so what is checked is what goes on the wire. These tests describe the circuit as it is, so that it
    /// can be moved out of the Simulator class without changing how it behaves.
    /// </summary>
    /// <remarks>
    /// Checked against the viewer's source (indra/llmessage: message.cpp, llcircuit.cpp, llcircuit.h, message.h, net.h,
    /// lltemplatemessagebuilder.cpp):
    /// <list type="bullet">
    /// <item>Same as the viewer: the flag bits (0x80 zero-coded, 0x40 reliable, 0x20 resent, 0x10 acks appended); a header
    /// of flags, a big-endian four byte sequence number and an extra byte; the MTU of 1200; sequence numbers that start at
    /// 1 and count every packet, reliable or not; reliable packets kept until acknowledged; acks appended oldest first with the
    /// count in the last byte, at most 250; a datagram whose ack count the size cannot hold dropped; acks taken from any packet;
    /// a reliable packet acknowledged, and a duplicate resend acknowledged again and discarded; three resends of the same
    /// packet number with the resent flag, then dropped; a variable block of at most 255 (MAX_BLOCKS); the oldest unacknowledged
    /// sequence in a ping, or the last sequence sent when there is none.</item>
    /// <item>Differs from the viewer, and is kept as it is: the resend timeout is a setting (the viewer uses five times the
    /// averaged ping, at least a second); sequence numbers do not wrap at 0x01000000; only the address of the sender is
    /// compared, not the port; a duplicate that is not marked as a resend is dropped too (the viewer drops only the marked ones,
    /// and forgets what it has received after 60 seconds or when a ping says the sender no longer needs it, where this holds the
    /// last 999); acks are also appended to a PacketAck; a packet marked zero-coded is encoded even if that does not make it
    /// smaller (the viewer sends it plain unless it does); PacketAcks are sent up to 255 to a packet (the viewer, 251).</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    [Category("Circuit")]
    public class SimulatorCircuitTests
    {
        #region Harness

        private sealed class CircuitSimulator : Simulator
        {
            public CircuitSimulator(GridClient client, IPEndPoint address) : base(client, address, 0) { }
            public void Deliver(UDPPacketBuffer buffer) => PacketReceived(buffer);
            public IPAddress SocketBindAddress => BindAddress;
            public int SocketReceiveQueueCapacity => ReceiveQueueCapacity;
        }

        /// <summary>A datagram taken off the wire at the far end.</summary>
        private sealed class Wire
        {
            public byte[] Raw = Array.Empty<byte>();
            public Packet Packet = null!;
            public bool Reliable => (Raw[0] & Helpers.MSG_RELIABLE) != 0;
            public bool Resent => (Raw[0] & Helpers.MSG_RESENT) != 0;
            public bool ZeroCoded => (Raw[0] & Helpers.MSG_ZEROCODED) != 0;
            public bool AppendedAcks => (Raw[0] & Helpers.MSG_APPENDED_ACKS) != 0;
            public uint Sequence => (uint)((Raw[1] << 24) | (Raw[2] << 16) | (Raw[3] << 8) | Raw[4]);
            /// <summary>The appended acknowledgements in the order they are on the wire (the parser gives them last first).</summary>
            public uint[] Acks
            {
                get
                {
                    if (!AppendedAcks) return Array.Empty<uint>();
                    int count = Raw[Raw.Length - 1];
                    int start = Raw.Length - 1 - count * 4;
                    return Enumerable.Range(0, count).Select(i => (uint)((Raw[start + i * 4] << 24) | (Raw[start + i * 4 + 1] << 16) | (Raw[start + i * 4 + 2] << 8) | Raw[start + i * 4 + 3])).ToArray();
                }
            }
        }

        private sealed class Circuit : IDisposable
        {
            private static readonly FieldInfo InboxField = typeof(NetworkManager).GetField("_packetInbox", BindingFlags.NonPublic | BindingFlags.Instance)!;
            private static readonly FieldInfo OutboxField = typeof(NetworkManager).GetField("_packetOutbox", BindingFlags.NonPublic | BindingFlags.Instance)!;

            public readonly GridClient Client = new GridClient();
            public readonly CircuitSimulator Sim;
            public readonly UdpClient FarEnd = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            public readonly IPEndPoint FarEndPoint;
            public readonly Channel<NetworkManager.IncomingPacket> Inbox = Channel.CreateUnbounded<NetworkManager.IncomingPacket>();
            public readonly Channel<NetworkManager.OutgoingPacket> Outbox = Channel.CreateUnbounded<NetworkManager.OutgoingPacket>();

            public Circuit(bool throttleOutgoing = false, int resendTimeoutMs = 100)
            {
                Client.Settings.Packets.ThrottleOutgoing = throttleOutgoing;
                Client.Settings.Packets.MaxPendingAcks = 1000;
                Client.Settings.Packets.MaxResendCount = 3;
                Client.Settings.Timing.ResendTimeout = resendTimeoutMs;
                Client.Settings.Logging.LogResends = false;

                FarEndPoint = (IPEndPoint)FarEnd.Client.LocalEndPoint!;
                InboxField.SetValue(Client.Network, Inbox);
                OutboxField.SetValue(Client.Network, Outbox);

                Sim = new CircuitSimulator(Client, FarEndPoint);
                Sim.Start();
            }

            public void Dispose()
            {
                Sim.Stop();
                FarEnd.Dispose();
            }

            /// <summary>What the simulator's timer does every tick: send acknowledgements, then resend what is overdue.</summary>
            public void Tick()
            {
                Sim.SendAcks();
                Sim.ResendUnacked();
            }

            public void ResendOnly() => Sim.ResendUnacked();

            public Wire? Next(int timeoutMs = 1000)
            {
                if (!FarEnd.Client.Poll(timeoutMs * 1000, SelectMode.SelectRead)) return null;
                var buffer = new byte[2048];
                int n = FarEnd.Client.Receive(buffer);
                var raw = buffer.Take(n).ToArray();
                // Building the packet takes the acks and their flag off the array it is given
                var parsed = raw.ToArray();
                int end = parsed.Length - 1;
                return new Wire { Raw = raw, Packet = Packet.BuildPacket(parsed, ref end, new byte[8192]) };
            }

            /// <summary>
            /// What the far end receives: the first datagram is waited for as long as a slow machine may need, and the rest
            /// until nothing more has come for a while.
            /// </summary>
            public List<Wire> All(int quietMs = 250, int firstMs = 5000)
            {
                var all = new List<Wire>();
                Wire? w = Next(firstMs);
                while (w != null)
                {
                    all.Add(w);
                    w = Next(quietMs);
                }
                return all;
            }

            /// <summary>What arrives in the next while, for a test that expects little or nothing.</summary>
            public List<Wire> Quiet(int ms)
            {
                var all = new List<Wire>();
                Wire? w;
                while ((w = Next(ms)) != null) all.Add(w);
                return all;
            }

            public List<Packet> Delivered()
            {
                var list = new List<Packet>();
                while (Inbox.Reader.TryRead(out var incoming)) list.Add(incoming.Packet!);
                return list;
            }

            public List<uint> Unacked()
            {
                return Sim.Circuit.GetUnackedSequences().ToList();
            }

            /// <summary>Raw bytes of a datagram as the far end would send it.</summary>
            public static byte[] Datagram(Packet packet, uint sequence, bool reliable = true, bool resent = false, params uint[] appendedAcks)
            {
                packet.Header.Reliable = reliable;
                packet.Header.Resent = resent;
                packet.Header.Sequence = sequence;
                byte[] raw = packet.ToBytes();
                if (appendedAcks.Length == 0) return raw;

                var all = new byte[raw.Length + appendedAcks.Length * 4 + 1];
                raw.CopyTo(all, 0);
                int pos = raw.Length;
                foreach (uint ack in appendedAcks)
                {
                    Utils.UIntToBytesBig(ack, all, pos);
                    pos += 4;
                }
                all[pos] = (byte)appendedAcks.Length;
                all[0] |= Helpers.MSG_APPENDED_ACKS;
                return all;
            }

            public void Receive(byte[] raw, IPEndPoint? from = null)
            {
                var buffer = new UDPPacketBuffer(from ?? FarEndPoint, raw.Length);
                buffer.CopyFrom(raw, raw.Length);
                buffer.DataLength = raw.Length;
                Sim.Deliver(buffer);
            }

            public void ReceiveReliable(uint sequence, bool resent = false) =>
                Receive(Datagram(new StartPingCheckPacket { PingID = { PingID = (byte)sequence, OldestUnacked = 0 } }, sequence, true, resent));
        }

        private static ChatFromViewerPacket Chat(string text, bool reliable = true)
        {
            var chat = new ChatFromViewerPacket { Header = { Reliable = reliable } };
            chat.AgentData.AgentID = UUID.Random();
            chat.ChatData.Message = Utils.StringToBytes(text);
            return chat;
        }

        private static StartPingCheckPacket Ping() => new StartPingCheckPacket { Header = { Reliable = false } };

        #endregion Harness

        #region Sending

        [Test]
        public void ReliablePackets_AreNumberedFromOne_AndRememberedUntilAcknowledged()
        {
            using var c = new Circuit();

            c.Sim.SendPacket(Chat("one"));
            c.Sim.SendPacket(Chat("two"));
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(wire.Select(w => w.Sequence), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(wire.All(w => w.Reliable), Is.True);
                Assert.That(c.Sim.Sequence, Is.EqualTo(2));
                Assert.That(c.Unacked(), Is.EqualTo(new uint[] { 1, 2 }));
            });
        }

        [Test]
        public void UnreliablePackets_AreNumbered_ButNotRemembered()
        {
            using var c = new Circuit();

            c.Sim.SendPacket(Ping());
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(wire, Has.Count.EqualTo(1));
                Assert.That(wire[0].Reliable, Is.False);
                Assert.That(wire[0].Sequence, Is.EqualTo(1u));
                Assert.That(c.Unacked(), Is.Empty);
            });
        }

        [Test]
        public void ReliableAndUnreliablePackets_ShareOneSequence()
        {
            using var c = new Circuit();

            c.Sim.SendPacket(Chat("a"));
            c.Sim.SendPacket(Ping());
            c.Sim.SendPacket(Chat("b"));
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(wire.Select(w => w.Sequence), Is.EqualTo(new uint[] { 1, 2, 3 }));
                Assert.That(c.Unacked(), Is.EqualTo(new uint[] { 1, 3 }));
            });
        }

        [Test]
        public void TheSequenceIsAPublicFieldThatApplicationsCanSet()
        {
            using var c = new Circuit();
            c.Sim.Sequence = 41;

            c.Sim.SendPacket(Chat("next"));

            Assert.That(c.Next()!.Sequence, Is.EqualTo(42u));
        }

        [Test]
        public void ThePacketThatIsSent_ArrivesAsItWasBuilt()
        {
            using var c = new Circuit();
            var chat = Chat("hello there");

            c.Sim.SendPacket(chat);
            var received = c.Next()!.Packet as ChatFromViewerPacket;

            Assert.That(received, Is.Not.Null);
            Assert.That(Utils.BytesToString(received!.ChatData.Message), Is.EqualTo("hello there"));
            Assert.That(received.AgentData.AgentID, Is.EqualTo(chat.AgentData.AgentID));
        }

        [Test]
        public void AZeroCodedPacket_IsSentEncoded()
        {
            using var c = new Circuit();
            var chat = Chat("z");
            chat.Header.Zerocoded = true;

            c.Sim.SendPacket(chat);
            var wire = c.Next()!;

            Assert.That(wire.ZeroCoded, Is.True);
            Assert.That(wire.Packet, Is.InstanceOf<ChatFromViewerPacket>());
        }

        [Test]
        public void APacketThatGrowsPastTheMtuWhenZeroCoded_IsSentWithoutEncoding()
        {
            using var c = new Circuit();
            // Isolated zeros each take two bytes once encoded, so this is under the MTU as it is and over it encoded
            var data = new byte[1100];
            data[0] = (byte)(Helpers.MSG_ZEROCODED | Helpers.MSG_RELIABLE);
            for (int i = 6; i < data.Length; i += 2) data[i] = 1;

            c.Sim.SendPacketData(data, data.Length, PacketType.ChatFromViewer, doZerocode: true);
            var raw = c.FarEnd.Client.Poll(1_000_000, SelectMode.SelectRead) ? new byte[2048] : null;
            Assert.That(raw, Is.Not.Null);
            int n = c.FarEnd.Client.Receive(raw!);

            Assert.Multiple(() =>
            {
                Assert.That(n, Is.EqualTo(1100));
                Assert.That(raw![0] & Helpers.MSG_ZEROCODED, Is.EqualTo(0), "the flag is taken off");
                Assert.That(raw[0] & Helpers.MSG_RELIABLE, Is.Not.EqualTo(0));
            });
        }

        #endregion Sending

        #region Acknowledging what is received

        [Test]
        public void AcknowledgementsWaitingToBeSent_AreAppendedToTheNextPacket()
        {
            using var c = new Circuit();
            c.ReceiveReliable(7);
            c.ReceiveReliable(9);

            c.Sim.SendPacket(Ping());
            var wire = c.Next()!;

            Assert.Multiple(() =>
            {
                Assert.That(wire.AppendedAcks, Is.True);
                Assert.That(wire.Acks, Is.EqualTo(new uint[] { 7, 9 }));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(0), "they have gone");
                Assert.That(c.Next(100), Is.Null);
            });
        }

        [Test]
        public void AppendedAcknowledgements_StopAt250_WhateverRoomIsLeft()
        {
            // The viewer's MAX_ACKS, and under the 255 that the count byte after them could hold
            using var c = new Circuit();
            c.Client.Settings.Packets.MaxPendingAcks = 100000;
            for (uint i = 1; i <= 600; i++) c.ReceiveReliable(i);

            c.Sim.SendPacket(Ping());
            var wire = c.Next()!;

            Assert.Multiple(() =>
            {
                Assert.That(wire.Acks, Has.Length.EqualTo(250));
                Assert.That(wire.Acks, Is.EqualTo(Enumerable.Range(1, 250).Select(i => (uint)i)));
                Assert.That(wire.Raw.Length, Is.LessThanOrEqualTo(Packet.MTU));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(600 - 250), "the rest are still waiting");
            });
        }

        [Test]
        public void AppendedAcknowledgements_FillTheRoomLeftInThePacket_WhenThatIsLessThan250()
        {
            using var c = new Circuit();
            c.Client.Settings.Packets.MaxPendingAcks = 100000;
            for (uint i = 1; i <= 400; i++) c.ReceiveReliable(i);

            c.Sim.SendPacket(Chat(new string('x', 600)));
            var wire = c.Next()!;
            int count = wire.Acks.Length;
            int body = wire.Raw.Length - 1 - 4 * count;

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.GreaterThan(0).And.LessThan(250));
                Assert.That(wire.Raw.Length, Is.LessThanOrEqualTo(Packet.MTU));
                Assert.That(body + 4 * count + 5, Is.GreaterThanOrEqualTo(Packet.MTU), "there was no room for another");
                Assert.That(body + 4 * (count - 1) + 5, Is.LessThan(Packet.MTU), "until the last one went in");
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(400 - count));
            });
        }

        [Test]
        public void SendAcks_SendsAPacketAckForEveryAckWaiting_AndNothingTheSecondTime()
        {
            using var c = new Circuit();
            for (uint i = 100; i < 105; i++) c.ReceiveReliable(i);

            int sent = c.Sim.SendAcks();
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(sent, Is.EqualTo(5));
                Assert.That(wire, Has.Count.EqualTo(1));
                Assert.That(wire[0].Packet.Type, Is.EqualTo(PacketType.PacketAck));
                Assert.That(wire[0].Reliable, Is.False);
                Assert.That(((PacketAckPacket)wire[0].Packet).Packets.Select(b => b.ID), Is.EqualTo(new uint[] { 100, 101, 102, 103, 104 }));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(0));
            });
        }

        [Test]
        public void SendAcks_WithMoreAcksThanOnePacketHolds_SplitsThemAcrossPackets()
        {
            using var c = new Circuit();
            for (uint i = 1; i <= 300; i++) c.ReceiveReliable(i);

            int sent = c.Sim.SendAcks();
            var wire = c.All();
            var ids = wire.SelectMany(w => ((PacketAckPacket)w.Packet).Packets.Select(b => b.ID)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(sent, Is.EqualTo(300));
                Assert.That(wire, Has.Count.GreaterThan(1));
                Assert.That(wire.All(w => w.Raw.Length <= Packet.MTU), Is.True);
                Assert.That(ids, Is.EquivalentTo(Enumerable.Range(1, 300).Select(i => (uint)i)));
            });
        }

        [Test]
        public void AReliablePacket_IsAcknowledged_AndAnUnreliableOneIsNot()
        {
            using var c = new Circuit();

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 5, reliable: false));
            Assert.That(c.Sim.SendAcks(), Is.EqualTo(0));

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 6, reliable: true));
            Assert.That(c.Sim.SendAcks(), Is.EqualTo(1));
            Assert.That(((PacketAckPacket)c.Next()!.Packet).Packets.Single().ID, Is.EqualTo(6u));
        }

        [Test]
        public void WhenEnoughAcksAreWaiting_TheyAreSentWithoutWaitingForTheTimer()
        {
            using var c = new Circuit();
            c.Client.Settings.Packets.MaxPendingAcks = 3;

            c.ReceiveReliable(1);
            c.ReceiveReliable(2);
            Assert.That(c.Next(100), Is.Null, "two are not enough");

            c.ReceiveReliable(3);
            var wire = c.Next()!;

            Assert.That(wire.Packet.Type, Is.EqualTo(PacketType.PacketAck));
            Assert.That(((PacketAckPacket)wire.Packet).Packets.Select(b => b.ID), Is.EqualTo(new uint[] { 1, 2, 3 }));
        }

        #endregion Acknowledging what is received

        #region What the far end acknowledges

        [Test]
        public void APacketAck_TakesThoseSequencesOffTheListOfUnacknowledgedPackets()
        {
            using var c = new Circuit();
            for (int i = 0; i < 3; i++) c.Sim.SendPacket(Chat("x" + i));

            var ack = new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 2 }, new PacketAckPacket.PacketsBlock { ID = 99 } } };
            c.Receive(Circuit.Datagram(ack, 1, reliable: false));

            Assert.That(c.Unacked(), Is.EqualTo(new uint[] { 1, 3 }), "99 was never sent, and does no harm");
        }

        [Test]
        public void AcknowledgementsAppendedToAPacket_TakeThemOffToo()
        {
            using var c = new Circuit();
            for (int i = 0; i < 3; i++) c.Sim.SendPacket(Chat("x" + i));

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 1, reliable: false, resent: false, 1u, 3u));

            Assert.That(c.Unacked(), Is.EqualTo(new uint[] { 2 }));
        }

        [Test]
        public void TheOldestUnacknowledgedSequence_IsWhatAPingSays()
        {
            using var c = new Circuit();
            for (int i = 0; i < 3; i++) c.Sim.SendPacket(Chat("x" + i));
            c.All();

            uint OldestUnackedOnTheWire()
            {
                c.Sim.SendPing();
                var ping = c.All().Select(w => w.Packet).OfType<StartPingCheckPacket>().Last();
                return ping.PingID.OldestUnacked;
            }

            var first = OldestUnackedOnTheWire();
            c.Receive(Circuit.Datagram(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 1 } } }, 1, reliable: false));
            var afterFirst = OldestUnackedOnTheWire();
            c.Receive(Circuit.Datagram(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 3 } } }, 2, reliable: false));
            var afterThird = OldestUnackedOnTheWire();
            c.Receive(Circuit.Datagram(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 2 } } }, 3, reliable: false));
            var afterAll = OldestUnackedOnTheWire();

            // With nothing left unacknowledged it says the last sequence sent (the three packets and the three pings)
            Assert.That(new[] { first, afterFirst, afterThird, afterAll }, Is.EqualTo(new uint[] { 1, 2, 2, 6 }));
        }

        [Test]
        public void WithNothingUnacknowledged_APingSaysTheLastSequenceSent_AsTheViewerDoes()
        {
            using var c = new Circuit();

            c.Sim.SendPing();
            var beforeAnythingWasSent = ((StartPingCheckPacket)c.Next()!.Packet).PingID.OldestUnacked;
            c.Sim.SendPacket(Ping());
            c.Sim.SendPacket(Ping());
            c.All();
            c.Sim.SendPing();
            var afterFour = ((StartPingCheckPacket)c.Next()!.Packet).PingID.OldestUnacked;

            Assert.That(beforeAnythingWasSent, Is.EqualTo(0u));
            Assert.That(afterFour, Is.EqualTo(3u), "the ping, and the two sent after it");
        }

        [Test]
        public async Task UseCircuitCode_WaitsForTheAcknowledgementOfItsPacket()
        {
            using var c = new Circuit();
            c.Client.Settings.Timing.LoginTimeout = 5000;

            var task = c.Sim.UseCircuitCodeAsync(waitForAck: true);
            var wire = c.Next()!;
            Assert.That(wire.Packet.Type, Is.EqualTo(PacketType.UseCircuitCode));
            Assert.That(task.IsCompleted, Is.False);

            c.Receive(Circuit.Datagram(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = wire.Sequence } } }, 1, reliable: false));

            Assert.That(await task, Is.True);
        }

        [Test]
        public async Task UseCircuitCode_GivesUpWhenNothingAcknowledgesIt()
        {
            using var c = new Circuit();
            c.Client.Settings.Timing.LoginTimeout = 200;

            Assert.That(await c.Sim.UseCircuitCodeAsync(waitForAck: true), Is.False);
        }

        #endregion What the far end acknowledges

        #region Receiving

        [Test]
        public void PacketsReceived_AreHandedToTheNetworkInOrder_WithTheSimulatorTheyCameFrom()
        {
            using var c = new Circuit();
            c.Receive(Circuit.Datagram(new StartPingCheckPacket { PingID = { PingID = 1 } }, 1));
            c.Receive(Circuit.Datagram(new StartPingCheckPacket { PingID = { PingID = 2 } }, 2));
            c.Receive(Circuit.Datagram(new CompletePingCheckPacket { PingID = { PingID = 3 } }, 3, reliable: false));

            var delivered = c.Delivered();

            Assert.That(delivered.Select(p => p.Type), Is.EqualTo(new[] { PacketType.StartPingCheck, PacketType.StartPingCheck, PacketType.CompletePingCheck }));
            Assert.That(((StartPingCheckPacket)delivered[1]).PingID.PingID, Is.EqualTo(2));
        }

        [Test]
        public void TheSimulatorOfAPacketHandedToTheNetwork_IsTheOneItCameFrom()
        {
            using var c = new Circuit();
            c.ReceiveReliable(1);

            Assert.That(c.Inbox.Reader.TryRead(out var incoming), Is.True);
            Assert.That(incoming!.Simulator, Is.SameAs(c.Sim));
        }

        [Test]
        public void AReliablePacketReceivedTwice_IsHandedOverOnce_ButAcknowledgedBothTimes()
        {
            using var c = new Circuit();

            c.ReceiveReliable(10);
            c.ReceiveReliable(10, resent: true);

            Assert.Multiple(() =>
            {
                Assert.That(c.Delivered(), Has.Count.EqualTo(1));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(2));
                Assert.That(c.Sim.Stats.GetReceivedResends(), Is.EqualTo(1));
            });
        }

        [Test]
        public void ADuplicateNotMarkedAsAResend_IsAlsoDropped()
        {
            using var c = new Circuit();

            c.ReceiveReliable(10);
            c.ReceiveReliable(10);

            Assert.That(c.Delivered(), Has.Count.EqualTo(1));
            Assert.That(c.Sim.Stats.GetReceivedResends(), Is.EqualTo(0));
        }

        [Test]
        public void AnUnreliablePacket_IsNeverTakenForADuplicate()
        {
            using var c = new Circuit();

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 10, reliable: false));
            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 10, reliable: false));

            Assert.That(c.Delivered(), Has.Count.EqualTo(2));
        }

        [Test]
        public void ADatagramFromAnotherAddress_IsIgnored()
        {
            using var c = new Circuit();

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 1), new IPEndPoint(IPAddress.Parse("127.0.0.2"), c.FarEndPoint.Port));

            Assert.Multiple(() =>
            {
                Assert.That(c.Delivered(), Is.Empty);
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(0));
            });
        }

        [Test]
        public void AnotherPortOnTheSameAddress_IsAccepted()
        {
            // Only the address is compared
            using var c = new Circuit();

            c.Receive(Circuit.Datagram(new StartPingCheckPacket(), 1), new IPEndPoint(IPAddress.Loopback, c.FarEndPoint.Port + 1));

            Assert.That(c.Delivered(), Has.Count.EqualTo(1));
        }

        [Test]
        public void ReceivingAPacket_ClearsTheDisconnectCandidateFlag()
        {
            using var c = new Circuit();
            c.Sim.DisconnectCandidate = true;

            c.ReceiveReliable(1);

            Assert.That(c.Sim.DisconnectCandidate, Is.False);
        }

        [Test]
        public void MalformedData_IsDropped_WithoutAnAcknowledgement()
        {
            using var c = new Circuit();

            Assert.DoesNotThrow(() => c.Receive(new byte[] { Helpers.MSG_RELIABLE, 0, 0, 0, 1, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));

            Assert.That(c.Delivered(), Is.Empty);
            Assert.That(c.Sim.SendAcks(), Is.EqualTo(0));
        }

        [Test]
        public void ADatagramThatClaimsMoreAcksThanItCanHold_IsDropped_WithoutAnAcknowledgement()
        {
            // The viewer discards it as malformed when the count byte is more than the size allows
            using var c = new Circuit();
            var raw = Circuit.Datagram(new StartPingCheckPacket(), 1, reliable: true);
            raw[0] |= Helpers.MSG_APPENDED_ACKS;
            var claimed = raw.Concat(new byte[] { 200 }).ToArray();

            Assert.DoesNotThrow(() => c.Receive(claimed));

            Assert.That(c.Delivered(), Is.Empty);
            Assert.That(c.Sim.SendAcks(), Is.EqualTo(0));
        }

        [Test]
        public void AZeroCodedPacket_IsDecoded()
        {
            using var c = new Circuit();
            var chat = Chat("zeros", reliable: true);
            chat.AgentData.AgentID = UUID.Zero;
            chat.AgentData.SessionID = UUID.Zero;
            chat.Header.Zerocoded = true;
            chat.Header.Sequence = 3;
            byte[] raw = chat.ToBytes();
            var encoded = new byte[raw.Length + 64];
            int length = Helpers.ZeroEncode(raw, raw.Length, encoded);

            c.Receive(encoded.Take(length).ToArray());
            var delivered = c.Delivered();

            Assert.That(length, Is.LessThan(raw.Length), "the zeros made it smaller");
            Assert.That(Utils.BytesToString(((ChatFromViewerPacket)delivered.Single()).ChatData.Message), Is.EqualTo("zeros"));
        }

        [Test]
        public void TheArchiveOfPacketsReceived_ForgetsTheOldestOnce1000AreHeld()
        {
            using var c = new Circuit();
            c.Client.Settings.Packets.MaxPendingAcks = 100000;
            for (uint i = 1; i <= 1000; i++) c.ReceiveReliable(i);
            c.Delivered();

            c.ReceiveReliable(1);
            var oldestAgain = c.Delivered().Count;
            c.ReceiveReliable(1000);
            var newestAgain = c.Delivered().Count;

            Assert.That(oldestAgain, Is.EqualTo(1), "forgotten, so taken for new");
            Assert.That(newestAgain, Is.EqualTo(0));
        }

        #endregion Receiving

        #region Resending

        [Test]
        public async Task AnUnacknowledgedPacket_IsResentAfterTheTimeout_WithTheSameSequenceAndTheResentFlag()
        {
            // A long timeout, so that a slow machine does not make the packet overdue before it is checked
            using var c = new Circuit(resendTimeoutMs: 400);
            c.Sim.SendPacket(Chat("again"));
            var first = c.Next()!;

            c.ResendOnly();
            Assert.That(c.Next(50), Is.Null, "not before the timeout");

            await Task.Delay(500);
            c.ResendOnly();
            var second = c.Next()!;

            Assert.Multiple(() =>
            {
                Assert.That(first.Resent, Is.False);
                Assert.That(second.Resent, Is.True);
                Assert.That(second.Sequence, Is.EqualTo(first.Sequence));
                Assert.That(second.Reliable, Is.True);
                Assert.That(c.Unacked(), Is.EqualTo(new[] { first.Sequence }));
                Assert.That(c.Sim.Stats.GetResentPackets(), Is.EqualTo(1));
                Assert.That(c.Sim.Sequence, Is.EqualTo(1), "a resend takes no new number");
            });
        }

        [Test]
        public async Task AnAcknowledgedPacket_IsNotResent()
        {
            using var c = new Circuit();
            c.Sim.SendPacket(Chat("acked"));
            var sent = c.Next()!;
            c.Receive(Circuit.Datagram(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = sent.Sequence } } }, 1, reliable: false));

            await Task.Delay(150);
            c.ResendOnly();

            Assert.That(c.Next(100), Is.Null);
        }

        [Test]
        public async Task APacketIsResentUpToTheLimit_ThenGivenUp()
        {
            using var c = new Circuit();
            c.Sim.SendPacket(Chat("lost"));
            var wire = new List<Wire> { c.Next()! };

            for (int round = 0; round < 6; round++)
            {
                await Task.Delay(150);
                c.ResendOnly();
                // The first three rounds resend the packet, and the datagram is waited for; after that there is nothing
                if (round < 3) wire.Add(c.Next(5000)!);
            }
            wire.AddRange(c.Quiet(150));

            Assert.Multiple(() =>
            {
                Assert.That(wire, Has.Count.EqualTo(1 + 3), "sent once and resent three times");
                Assert.That(wire.Skip(1).All(w => w.Resent && w.Sequence == wire[0].Sequence), Is.True);
                Assert.That(c.Unacked(), Is.Empty, "then dropped");
                Assert.That(c.Sim.Stats.GetResentPackets(), Is.EqualTo(3));
            });
        }

        [Test]
        public async Task ThePacketsOverdueAreResentTogether_InTheOrderTheyWereSent()
        {
            using var c = new Circuit();
            for (int i = 0; i < 3; i++) c.Sim.SendPacket(Chat("p" + i));
            c.All();

            await Task.Delay(150);
            c.ResendOnly();

            Assert.That(c.All().Select(w => w.Sequence), Is.EqualTo(new uint[] { 1, 2, 3 }));
        }

        [Test]
        public async Task AResentPacket_IsTheOriginalBytesWithTheResentFlag_AndCarriesNoAcknowledgements()
        {
            // The viewer keeps the packet as it was before acknowledgements were appended and sends that again
            using var c = new Circuit();
            c.ReceiveReliable(55);
            c.Sim.SendPacket(Chat("with an ack on it"));
            var first = c.Next()!;
            Assert.That(first.Acks, Is.EqualTo(new uint[] { 55 }), "the first send carried it");

            c.ReceiveReliable(56);
            await Task.Delay(150);
            c.ResendOnly();
            var resent = c.Next()!;

            var original = first.Raw.Take(first.Raw.Length - 1 - 4).ToArray();
            original[0] = (byte)((original[0] | Helpers.MSG_RESENT) & ~Helpers.MSG_APPENDED_ACKS);

            Assert.Multiple(() =>
            {
                Assert.That(resent.Raw, Is.EqualTo(original));
                Assert.That(resent.AppendedAcks, Is.False);
                Assert.That(resent.Sequence, Is.EqualTo(first.Sequence));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(1), "56 is still waiting to be acknowledged");
            });
        }

        [Test]
        public async Task AResentPacket_DoesNotPickUpAcknowledgementsThatAreWaiting()
        {
            using var c = new Circuit();
            c.Sim.SendPacket(Chat("no acks yet"));
            var first = c.Next()!;
            c.ReceiveReliable(70);

            await Task.Delay(150);
            c.ResendOnly();
            var resent = c.Next()!;

            Assert.Multiple(() =>
            {
                Assert.That(first.AppendedAcks, Is.False);
                Assert.That(resent.AppendedAcks, Is.False);
                Assert.That(resent.Raw, Has.Length.EqualTo(first.Raw.Length));
                Assert.That(c.Sim.SendAcks(), Is.EqualTo(1));
            });
        }

        #endregion Resending

        #region The outgoing queue

        [Test]
        public void WhenOutgoingPacketsAreThrottled_ReliablePacketsWaitInTheQueueUnsent()
        {
            using var c = new Circuit(throttleOutgoing: true);

            c.Sim.SendPacket(Chat("queued"));

            Assert.Multiple(() =>
            {
                Assert.That(c.Next(100), Is.Null);
                Assert.That(c.Outbox.Reader.Count, Is.EqualTo(1));
                Assert.That(c.Unacked(), Is.Empty, "not remembered, or numbered, until it is sent");
                Assert.That(c.Sim.Sequence, Is.EqualTo(0));
            });
        }

        [Test]
        public void PacketAcksAndLogoutRequests_GoStraightToTheWire_EvenWhenThrottled()
        {
            using var c = new Circuit(throttleOutgoing: true);

            c.Sim.SendPacket(new PacketAckPacket { Header = { Reliable = false }, Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 1 } } });
            c.Sim.SendPacket(new LogoutRequestPacket());
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(wire.Select(w => w.Packet.Type), Is.EqualTo(new[] { PacketType.PacketAck, PacketType.LogoutRequest }));
                Assert.That(c.Outbox.Reader.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public void ThePacketsInTheQueue_AreNumberedWhenTheyAreSentAndNotWhenTheyAreQueued()
        {
            using var c = new Circuit(throttleOutgoing: true);
            c.Sim.SendPacket(Chat("first"));
            c.Sim.SendPacket(Chat("second"));
            Assert.That(c.Outbox.Reader.TryRead(out var first), Is.True);
            Assert.That(c.Outbox.Reader.TryRead(out var second), Is.True);

            c.Sim.SendPacketFinal(second!);
            c.Sim.SendPacketFinal(first!);
            var wire = c.All();

            Assert.Multiple(() =>
            {
                Assert.That(Utils.BytesToString(((ChatFromViewerPacket)wire[0].Packet).ChatData.Message), Is.EqualTo("second"));
                Assert.That(wire.Select(w => w.Sequence), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(c.Unacked(), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(second!.SequenceNumber, Is.EqualTo(1u));
            });
        }

        #endregion The outgoing queue

        #region The socket settings

        [Test]
        public void TheSimulatorsSocket_IsBoundAndSizedAsTheStaticSettingsSay_ReadAtTheTime()
        {
            var original = (Settings.BindAddress, Settings.UdpReceiveQueueCapacity);
            try
            {
                using var c = new Circuit();
                Settings.BindAddress = IPAddress.Loopback;
                Settings.UdpReceiveQueueCapacity = 7;

                Assert.That(c.Sim.SocketBindAddress, Is.EqualTo(IPAddress.Loopback));
                Assert.That(c.Sim.SocketReceiveQueueCapacity, Is.EqualTo(7));

                Settings.BindAddress = IPAddress.Any;
                Settings.UdpReceiveQueueCapacity = 99;
                Assert.That(c.Sim.SocketBindAddress, Is.EqualTo(IPAddress.Any));
                Assert.That(c.Sim.SocketReceiveQueueCapacity, Is.EqualTo(99));
            }
            finally
            {
                (Settings.BindAddress, Settings.UdpReceiveQueueCapacity) = original;
            }
        }

        #endregion The socket settings

        #region The agent throttle

        [Test]
        public void TheAgentThrottle_IsTheRatesOfTheTransport_WithTheClientsDefaultTotal()
        {
            var client = new GridClient();

            Assert.Multiple(() =>
            {
                Assert.That(client.Throttle, Is.InstanceOf<ThrottleRates>());
                Assert.That(client.Throttle.Task, Is.EqualTo(360448f).Within(0.5f));
                Assert.That(client.Throttle.Resend, Is.EqualTo(150000f), "clamped");
                Assert.That(client.Throttle.Total, Is.EqualTo(1_328_896f).Within(1f));
            });
        }

        [Test]
        public void AgentThrottleSet_SendsAnAgentThrottlePacketWithTheRatesToTheSimulator()
        {
            using var c = new Circuit();
            c.Client.Throttle.Task = 123456f;
            c.Client.Throttle.Texture = 65432f;

            c.Client.Throttle.Set(c.Sim);
            var wire = c.All().Select(w => w.Packet).OfType<AgentThrottlePacket>().Single();
            var rates = new ThrottleRates(wire.Throttle.Throttles, 0);

            Assert.Multiple(() =>
            {
                Assert.That(wire.Throttle.Throttles, Is.EqualTo(c.Client.Throttle.ToBytes()));
                Assert.That(rates.Task, Is.EqualTo(123456f));
                Assert.That(rates.Texture, Is.EqualTo(65432f));
            });
        }

        [Test]
        public void AgentThrottleSet_WithNoSimulator_DoesNothing()
        {
            var client = new GridClient();

            Assert.DoesNotThrow(() => client.Throttle.Set());
            Assert.DoesNotThrow(() => client.Throttle.Set(null));
        }

        [Test]
        public void AnAgentThrottleDecodedFromBytes_HasNoClientAndDoesNotSend()
        {
            var bytes = new ThrottleRates { Task = 90000f }.ToBytes();

            var throttle = new AgentThrottle(bytes, 0);

            Assert.That(throttle.Task, Is.EqualTo(90000f));
            Assert.DoesNotThrow(() => throttle.Set(null));
        }

        #endregion The agent throttle
    }
}
