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
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="ReliableCircuit"/> by itself: no socket, no client, and a clock the test moves. The same behaviour is
    /// checked through a real Simulator in the LibreMetaverse tests (SimulatorCircuitTests), and against the viewer's source.
    /// </summary>
    [TestFixture]
    [Category("Transport")]
    public class ReliableCircuitTests
    {
        #region Harness

        private sealed class TestCircuit : ReliableCircuit
        {
            public int Clock = 1000;
            public int Timeout = 100;
            public int MaxResends = 3;
            public int MaxAcks = 1000;
            public bool Queue;
            public readonly List<byte[]> Sent = new List<byte[]>();
            public readonly List<OutgoingDatagram> Queued = new List<OutgoingDatagram>();
            public readonly List<OutgoingDatagram> Resent = new List<OutgoingDatagram>();
            public readonly List<OutgoingDatagram> GivenUp = new List<OutgoingDatagram>();
            public readonly List<OutgoingDatagram> Acknowledged = new List<OutgoingDatagram>();
            public readonly List<(PacketType Type, int Length)> Prepared = new List<(PacketType, int)>();
            public readonly List<Packet> Received = new List<Packet>();
            public int Datagrams;

            public TestCircuit(Action<UDPPacketBuffer> transmit, Func<uint>? next = null, int archive = 1000)
                : base(new IPEndPoint(IPAddress.Loopback, 9000), transmit, next, archive)
            {
            }

            protected override int TickCount => Clock;
            protected override int ResendTimeout => Timeout;
            protected override int MaxResendCount => MaxResends;
            protected override int MaxPendingAcks => MaxAcks;
            protected override bool TryQueue(OutgoingDatagram datagram)
            {
                if (!Queue) return false;
                Queued.Add(datagram);
                return true;
            }
            protected override void OnPacketPrepared(PacketType type, int dataLength) => Prepared.Add((type, dataLength));
            protected override void OnDatagramReceived(int bytes, Packet packet) => Received.Add(packet);
            protected override void OnResent(OutgoingDatagram datagram) => Resent.Add(datagram);
            protected override void OnResendGivenUp(OutgoingDatagram datagram) => GivenUp.Add(datagram);
            protected override void OnAcknowledged(OutgoingDatagram datagram) => Acknowledged.Add(datagram);
        }

        /// <summary>A circuit whose transmit action adds what it sends to a list.</summary>
        private static TestCircuit NewCircuit(out List<byte[]> wire, Func<uint>? next = null, int archive = 1000)
        {
            var sent = new List<byte[]>();
            wire = sent;
            return new TestCircuit(b => sent.Add(b.Data.Take(b.DataLength).ToArray()), next, archive);
        }

        private sealed class WireTestCircuit : ReliableCircuit
        {
            public WireTestCircuit(Action<UDPPacketBuffer> transmit) : base(new IPEndPoint(IPAddress.Loopback, 9000), transmit) { }
        }

        private static uint Sequence(byte[] raw) => (uint)((raw[1] << 24) | (raw[2] << 16) | (raw[3] << 8) | raw[4]);
        private static bool Flag(byte[] raw, byte flag) => (raw[0] & flag) != 0;
        private static Packet Parse(byte[] raw)
        {
            var copy = raw.ToArray();
            int end = copy.Length - 1;
            return Packet.BuildPacket(copy, ref end, new byte[8192]);
        }
        private static uint[] AckedOnWire(byte[] raw)
        {
            if (!Flag(raw, Helpers.MSG_APPENDED_ACKS)) return Array.Empty<uint>();
            int count = raw[raw.Length - 1];
            int start = raw.Length - 1 - count * 4;
            return Enumerable.Range(0, count).Select(i => (uint)((raw[start + i * 4] << 24) | (raw[start + i * 4 + 1] << 16) | (raw[start + i * 4 + 2] << 8) | raw[start + i * 4 + 3])).ToArray();
        }

        private static ChatFromViewerPacket Chat(string text, bool reliable = true)
        {
            var chat = new ChatFromViewerPacket { Header = { Reliable = reliable } };
            chat.AgentData.AgentID = UUID.Random();
            chat.ChatData.Message = Utils.StringToBytes(text);
            return chat;
        }

        private static StartPingCheckPacket Ping(bool reliable = false) => new StartPingCheckPacket { Header = { Reliable = reliable } };

        private static UDPPacketBuffer Inbound(Packet packet, uint sequence, bool reliable = true, bool resent = false, params uint[] appendedAcks)
        {
            packet.Header.Reliable = reliable;
            packet.Header.Resent = resent;
            packet.Header.Sequence = sequence;
            byte[] raw = packet.ToBytes();
            if (appendedAcks.Length > 0)
            {
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
                raw = all;
            }
            var buffer = new UDPPacketBuffer(new IPEndPoint(IPAddress.Loopback, 9000), raw.Length);
            buffer.CopyFrom(raw, raw.Length);
            buffer.DataLength = raw.Length;
            return buffer;
        }

        #endregion Harness

        #region Sending

        [Test]
        public void ReliablePackets_AreNumberedFromOne_AndRememberedUntilAcknowledged()
        {
            var c = NewCircuit(out var wire);

            c.SendPacket(Chat("one"));
            c.SendPacket(Chat("two"));

            Assert.Multiple(() =>
            {
                Assert.That(wire.Select(Sequence), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(wire.All(w => Flag(w, Helpers.MSG_RELIABLE)), Is.True);
                Assert.That(c.GetUnackedSequences(), Is.EqualTo(new uint[] { 1, 2 }));
                Assert.That(c.UnackedCount, Is.EqualTo(2));
                Assert.That(c.OldestUnacked, Is.EqualTo(1u));
            });
        }

        [Test]
        public void UnreliablePackets_AreNumbered_ButNotRemembered()
        {
            var c = NewCircuit(out var wire);

            c.SendPacket(Chat("a"));
            c.SendPacket(Ping());
            c.SendPacket(Chat("b"));

            Assert.Multiple(() =>
            {
                Assert.That(wire.Select(Sequence), Is.EqualTo(new uint[] { 1, 2, 3 }));
                Assert.That(c.GetUnackedSequences(), Is.EqualTo(new uint[] { 1, 3 }));
            });
        }

        [Test]
        public void TheOwnerCanGiveTheNumbers()
        {
            uint next = 100;
            var c = NewCircuit(out var wire, () => ++next);

            c.SendPacket(Ping());
            c.SendPacket(Ping());

            Assert.That(wire.Select(Sequence), Is.EqualTo(new uint[] { 101, 102 }));
        }

        [Test]
        public void ADatagramIsSentToTheOtherEnd_AsItWasBuilt()
        {
            var sent = new List<UDPPacketBuffer>();
            var c = new WireTestCircuit(sent.Add);

            c.SendPacket(Chat("hello"));

            Assert.Multiple(() =>
            {
                Assert.That(sent, Has.Count.EqualTo(1));
                Assert.That(sent[0].RemoteEndPoint, Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 9000)));
                Assert.That(Utils.BytesToString(((ChatFromViewerPacket)Parse(sent[0].Data.Take(sent[0].DataLength).ToArray())).ChatData.Message), Is.EqualTo("hello"));
            });
        }

        [Test]
        public void APacketThatGrowsPastTheMtuWhenZeroCoded_IsSentWithoutEncoding()
        {
            var c = NewCircuit(out var wire);
            var data = new byte[1100];
            data[0] = (byte)(Helpers.MSG_ZEROCODED | Helpers.MSG_RELIABLE);
            for (int i = 6; i < data.Length; i += 2) data[i] = 1;

            c.SendPacketData(data, data.Length, PacketType.ChatFromViewer, doZerocode: true);

            Assert.That(wire[0], Has.Length.EqualTo(1100));
            Assert.That(Flag(wire[0], Helpers.MSG_ZEROCODED), Is.False);
        }

        [Test]
        public void AQueuedPacket_IsNumberedWhenItIsFinallySent_AndAckAndLogoutPacketsAreNeverQueued()
        {
            var c = NewCircuit(out var wire);
            c.Queue = true;

            c.SendPacket(Chat("first"));
            c.SendPacket(Chat("second"));
            c.SendPacket(new PacketAckPacket { Header = { Reliable = false }, Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 1 } } });
            c.SendPacket(new LogoutRequestPacket());
            Assert.Multiple(() =>
            {
                Assert.That(c.Queued, Has.Count.EqualTo(2));
                Assert.That(wire.Select(w => Parse(w).Type), Is.EqualTo(new[] { PacketType.PacketAck, PacketType.LogoutRequest }));
            });

            wire.Clear();
            c.SendFinal(c.Queued[1]);
            c.SendFinal(c.Queued[0]);

            Assert.Multiple(() =>
            {
                Assert.That(Utils.BytesToString(((ChatFromViewerPacket)Parse(wire[0])).ChatData.Message), Is.EqualTo("second"));
                Assert.That(c.Queued.Select(d => d.SequenceNumber), Is.EqualTo(new uint[] { 4, 3 }), "after the ack and the logout, which went at once");
            });
        }

        [Test]
        public void TheOwnerIsToldOfEachPacketSentOrQueued_WithTheLengthOfItsDatagram()
        {
            var c = NewCircuit(out var wire);

            c.SendPacket(Ping());
            c.Queue = true;
            c.SendPacket(Chat("queued"));

            Assert.Multiple(() =>
            {
                Assert.That(c.Prepared.Select(p => p.Type), Is.EqualTo(new[] { PacketType.StartPingCheck, PacketType.ChatFromViewer }));
                Assert.That(c.Prepared[0].Length, Is.EqualTo(wire[0].Length));
            });
        }

        #endregion Sending

        #region Acknowledgements

        private static void ReceiveReliable(TestCircuit c, uint sequence, bool resent = false) =>
            c.Receive(Inbound(new StartPingCheckPacket(), sequence, true, resent));

        [Test]
        public void AcknowledgementsWaiting_AreAppendedToTheNextPacketOldestFirst_AtMost250()
        {
            var c = NewCircuit(out var wire);
            for (uint i = 1; i <= 600; i++) ReceiveReliable(c, i);

            c.SendPacket(Ping());

            Assert.Multiple(() =>
            {
                Assert.That(AckedOnWire(wire[0]), Is.EqualTo(Enumerable.Range(1, 250).Select(i => (uint)i)));
                Assert.That(wire[0].Length, Is.LessThanOrEqualTo(Packet.MTU));
                Assert.That(c.PendingAckCount, Is.EqualTo(350));
            });
        }

        [Test]
        public void AppendedAcknowledgements_FillTheRoomLeftInThePacket_WhenThatIsLessThan250()
        {
            var c = NewCircuit(out var wire);
            for (uint i = 1; i <= 400; i++) ReceiveReliable(c, i);

            c.SendPacket(Chat(new string('x', 600)));
            int count = AckedOnWire(wire[0]).Length;
            int body = wire[0].Length - 1 - 4 * count;

            Assert.Multiple(() =>
            {
                Assert.That(count, Is.GreaterThan(0).And.LessThan(250));
                Assert.That(wire[0].Length, Is.LessThanOrEqualTo(Packet.MTU));
                Assert.That(body + 4 * count + 5, Is.GreaterThanOrEqualTo(Packet.MTU), "there was no room for another");
                Assert.That(body + 4 * (count - 1) + 5, Is.LessThan(Packet.MTU), "until the last one went in");
                Assert.That(c.PendingAckCount, Is.EqualTo(400 - count));
            });
        }

        [Test]
        public void SendAcks_SendsEveryAckWaiting_InPacketsOfAtMost255()
        {
            var c = NewCircuit(out var wire);
            for (uint i = 1; i <= 300; i++) ReceiveReliable(c, i);

            int sent = c.SendAcks();
            var ids = wire.SelectMany(w => ((PacketAckPacket)Parse(w)).Packets.Select(b => b.ID)).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(sent, Is.EqualTo(300));
                Assert.That(wire, Has.Count.EqualTo(2));
                Assert.That(ids, Is.EqualTo(Enumerable.Range(1, 300).Select(i => (uint)i)));
                Assert.That(c.SendAcks(), Is.EqualTo(0));
            });
        }

        [Test]
        public void WhenEnoughAcksAreWaiting_TheyAreSentAtOnce()
        {
            var c = NewCircuit(out var wire);
            c.MaxAcks = 3;

            ReceiveReliable(c, 1);
            ReceiveReliable(c, 2);
            Assert.That(wire, Is.Empty);
            ReceiveReliable(c, 3);

            Assert.That(((PacketAckPacket)Parse(wire.Single())).Packets.Select(b => b.ID), Is.EqualTo(new uint[] { 1, 2, 3 }));
        }

        [Test]
        public void OnlyReliablePacketsAreAcknowledged()
        {
            var c = NewCircuit(out _);

            c.Receive(Inbound(new StartPingCheckPacket(), 5, reliable: false));
            Assert.That(c.PendingAckCount, Is.EqualTo(0));

            c.Receive(Inbound(new StartPingCheckPacket(), 6, reliable: true));
            Assert.That(c.PendingAckCount, Is.EqualTo(1));
        }

        [Test]
        public void AcknowledgementsReceived_TakeThePacketsOffTheList_AndTheOwnerIsToldOfEach()
        {
            var c = NewCircuit(out _);
            for (int i = 0; i < 4; i++) c.SendPacket(Chat("x" + i));

            c.Receive(Inbound(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 2 }, new PacketAckPacket.PacketsBlock { ID = 99 } } }, 1, reliable: false));
            c.Receive(Inbound(new StartPingCheckPacket(), 2, reliable: false, resent: false, 1u, 4u));

            Assert.Multiple(() =>
            {
                Assert.That(c.GetUnackedSequences(), Is.EqualTo(new uint[] { 3 }));
                Assert.That(c.Acknowledged.Select(d => d.SequenceNumber), Is.EquivalentTo(new uint[] { 1, 2, 4 }), "99 was never sent");
                Assert.That(c.OldestUnacked, Is.EqualTo(3u));
            });
        }

        [Test]
        public void ADuplicate_IsDroppedButAcknowledgedAgain()
        {
            var c = NewCircuit(out _);

            var first = c.Receive(Inbound(new StartPingCheckPacket(), 10));
            var again = c.Receive(Inbound(new StartPingCheckPacket(), 10, resent: true));
            var unmarked = c.Receive(Inbound(new StartPingCheckPacket(), 10));

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(again, Is.Null);
                Assert.That(unmarked, Is.Null);
                Assert.That(c.PendingAckCount, Is.EqualTo(3));
            });
        }

        [Test]
        public void MalformedData_GivesNullAndNoAcknowledgement()
        {
            var c = NewCircuit(out _);
            var raw = new byte[] { Helpers.MSG_RELIABLE, 0, 0, 0, 1, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            var buffer = new UDPPacketBuffer(new IPEndPoint(IPAddress.Loopback, 9000), raw.Length);
            buffer.CopyFrom(raw, raw.Length);
            buffer.DataLength = raw.Length;

            Assert.That(c.Receive(buffer), Is.Null);
            Assert.That(c.PendingAckCount, Is.EqualTo(0));
            Assert.That(c.Received, Is.Empty);
        }

        [Test]
        public void TheOwnerIsToldOfEveryDatagramThatDecodes_EvenADuplicate()
        {
            var c = NewCircuit(out _);

            ReceiveReliable(c, 1);
            ReceiveReliable(c, 1);

            Assert.That(c.Received, Has.Count.EqualTo(2));
        }

        #endregion Acknowledgements

        #region Resending

        [Test]
        public void AnUnacknowledgedPacket_IsResentAfterTheTimeout_AsFirstSentWithTheResentFlag_UpToTheLimit_ThenGivenUp()
        {
            var c = NewCircuit(out var wire);
            ReceiveReliable(c, 40);
            c.SendPacket(Chat("lost"));
            var first = wire[0];
            Assert.That(AckedOnWire(first), Is.EqualTo(new uint[] { 40 }), "the first send carried an ack");

            c.Clock += 50;
            c.ResendUnacked();
            Assert.That(wire, Has.Count.EqualTo(1), "not before the timeout");

            for (int round = 0; round < 3; round++)
            {
                ReceiveReliable(c, (uint)(41 + round));
                c.Clock += 150;
                c.ResendUnacked();
            }
            Assert.That(wire, Has.Count.EqualTo(4), "three resends");

            var original = first.Take(first.Length - 1 - 4).ToArray();
            original[0] = (byte)((original[0] | Helpers.MSG_RESENT) & ~Helpers.MSG_APPENDED_ACKS);
            c.Clock += 150;
            c.ResendUnacked();

            Assert.Multiple(() =>
            {
                Assert.That(wire.Skip(1), Is.All.EqualTo(original), "the original bytes, with no acks");
                Assert.That(wire, Has.Count.EqualTo(4), "and no more");
                Assert.That(c.UnackedCount, Is.EqualTo(0));
                Assert.That(c.Resent, Has.Count.EqualTo(3));
                Assert.That(c.GivenUp, Has.Count.EqualTo(1));
                Assert.That(c.GivenUp[0].SequenceNumber, Is.EqualTo(1u));
                Assert.That(c.GivenUp[0].ResendCount, Is.EqualTo(3));
            });
        }

        [Test]
        public void AnAcknowledgedPacket_IsNotResent()
        {
            var c = NewCircuit(out var wire);
            c.SendPacket(Chat("acked"));
            c.Receive(Inbound(new PacketAckPacket { Packets = new[] { new PacketAckPacket.PacketsBlock { ID = 1 } } }, 1, reliable: false));

            c.Clock += 1000;
            c.ResendUnacked();

            Assert.That(wire, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheOverduePacketsAreResentTogether_InTheOrderTheyWereSent()
        {
            var c = NewCircuit(out var wire);
            for (int i = 0; i < 3; i++) c.SendPacket(Chat("p" + i));
            wire.Clear();

            c.Clock += 150;
            c.ResendUnacked();

            Assert.That(wire.Select(Sequence), Is.EqualTo(new uint[] { 1, 2, 3 }));
        }

        [Test]
        public void TheResendTimeout_IsMeasuredFromTheLastSend()
        {
            var c = NewCircuit(out var wire);
            c.SendPacket(Chat("p"));
            c.Clock += 150;
            c.ResendUnacked();
            wire.Clear();

            c.Clock += 50;
            c.ResendUnacked();
            Assert.That(wire, Is.Empty);

            c.Clock += 100;
            c.ResendUnacked();
            Assert.That(wire, Has.Count.EqualTo(1));
        }

        #endregion Resending

        #region Two circuits

        [Test]
        public void TwoCircuitsOverALinkThatLosesAThird_DeliverEveryReliablePacketExactlyOnce()
        {
            var rng = new Random(12345);
            int onWire = 0, dropped = 0;
            LoopCircuit a = null!, b = null!;
            a = new LoopCircuit(buf => Carry(buf, () => b));
            b = new LoopCircuit(buf => Carry(buf, () => a));

            void Carry(UDPPacketBuffer buf, Func<LoopCircuit> to)
            {
                onWire++;
                if (rng.NextDouble() < 0.30) { dropped++; return; }
                var copy = new UDPPacketBuffer(new IPEndPoint(IPAddress.Loopback, 9000), buf.DataLength);
                copy.CopyFrom(buf.Data, buf.DataLength);
                copy.DataLength = buf.DataLength;
                to().Deliver(copy);
            }

            const int N = 200;
            for (int i = 0; i < N; i++)
            {
                a.SendPacket(Chat("A" + i));
                b.SendPacket(Chat("B" + i));
            }

            for (int tick = 0; tick < 500 && (a.UnackedCount > 0 || b.UnackedCount > 0 || tick < 3); tick++)
            {
                a.SendAcks(); b.SendAcks();
                a.Clock += 60; b.Clock += 60;
                a.ResendUnacked(); b.ResendUnacked();
            }
            a.SendAcks(); b.SendAcks();

            Assert.Multiple(() =>
            {
                Assert.That(dropped, Is.GreaterThan(100), "the link really lost datagrams");
                Assert.That(b.Got, Is.EquivalentTo(Enumerable.Range(0, N).Select(i => "A" + i)));
                Assert.That(a.Got, Is.EquivalentTo(Enumerable.Range(0, N).Select(i => "B" + i)));
                Assert.That(a.Delivered, Is.EqualTo(N));
                Assert.That(b.Delivered, Is.EqualTo(N));
                Assert.That(a.UnackedCount + b.UnackedCount, Is.EqualTo(0));
                Assert.That(a.GaveUp + b.GaveUp, Is.EqualTo(0));
            });
        }

        private sealed class LoopCircuit : ReliableCircuit
        {
            public int Clock = 1000;
            public readonly List<string> Got = new List<string>();
            public int Delivered, GaveUp;

            public LoopCircuit(Action<UDPPacketBuffer> transmit) : base(new IPEndPoint(IPAddress.Loopback, 9000), transmit) { }

            protected override int TickCount => Clock;
            protected override int ResendTimeout => 100;
            protected override int MaxResendCount => 50;
            protected override int MaxPendingAcks => 1000;
            protected override void OnResendGivenUp(OutgoingDatagram datagram) => GaveUp++;

            public void Deliver(UDPPacketBuffer buffer)
            {
                if (Receive(buffer) is ChatFromViewerPacket chat)
                {
                    Delivered++;
                    Got.Add(Utils.BytesToString(chat.ChatData.Message));
                }
            }
        }

        #endregion Two circuits
    }
}
