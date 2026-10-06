using System;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Datagrams are received into pooled buffers, so bytes past the end of a datagram are left over from
    /// an earlier one. A packet must never be built from them.
    /// </summary>
    [TestFixture]
    public class UdpPacketHeaderTests
    {
        [Test]
        public void ValidPacket_StillBuilds()
        {
            var ping = new StartPingCheckPacket();
            ping.PingID.PingID = 7;
            ping.PingID.OldestUnacked = 3;
            byte[] data = ping.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, data.Length);
            int packetEnd = data.Length - 1;

            var built = Packet.BuildPacket(buffer, ref packetEnd, null);

            Assert.That(built, Is.InstanceOf<StartPingCheckPacket>());
            Assert.That(((StartPingCheckPacket)built).PingID.PingID, Is.EqualTo(7));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(6)]
        public void DatagramShorterThanAHeader_IsNotBuiltFromLeftoverBytes(int length)
        {
            // a pooled buffer that previously held a complete packet
            var ping = new StartPingCheckPacket();
            byte[] data = ping.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, data.Length);
            int packetEnd = length - 1;

            Assert.Throws<MalformedDataException>(() => Packet.BuildPacket(buffer, ref packetEnd, null));
        }

        [Test]
        public void DatagramCutOffInsideALongHeader_IsRefused()
        {
            // Low frequency packets have a 10 byte header; keep only 8 bytes of one
            var packet = new AgentThrottlePacket();
            byte[] data = packet.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, data.Length);
            int packetEnd = 7;

            Assert.Throws<MalformedDataException>(() => Packet.BuildPacket(buffer, ref packetEnd, new byte[8192]));
        }

        [Test]
        public void AppendedAcks_AreParsed()
        {
            var ping = new StartPingCheckPacket();
            ping.Header.AppendedAcks = true;
            ping.Header.AckList = new uint[] { 11, 22, 33 };
            byte[] data = ping.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, data.Length);
            int packetEnd = data.Length - 1;

            var built = Packet.BuildPacket(buffer, ref packetEnd, null);

            Assert.That(built.Header.AckList, Is.EquivalentTo(new uint[] { 11, 22, 33 }));
        }

        [Test]
        public void AppendedAckCountLargerThanThePacket_IsRefused()
        {
            var ping = new StartPingCheckPacket();
            ping.Header.AppendedAcks = true;
            ping.Header.AckList = new uint[] { 11 };
            byte[] data = ping.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, data.Length);
            buffer[data.Length - 1] = 200; // claims 200 ACKs
            int packetEnd = data.Length - 1;

            Assert.Throws<MalformedDataException>(() => Packet.BuildPacket(buffer, ref packetEnd, null));
        }

        [Test]
        public void AppendedAcksFlagWithNothingAfterTheHeader_IsRefused()
        {
            var ping = new StartPingCheckPacket();
            byte[] data = ping.ToBytes();
            var buffer = new byte[1200];
            Array.Copy(data, buffer, 7);
            buffer[0] |= Helpers.MSG_APPENDED_ACKS;
            int packetEnd = 6;

            Assert.Throws<MalformedDataException>(() => Packet.BuildPacket(buffer, ref packetEnd, null));
        }
    }
}
