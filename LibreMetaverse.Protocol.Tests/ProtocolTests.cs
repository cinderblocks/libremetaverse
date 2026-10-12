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
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using LibreMetaverse.Messages;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The protocol assembly on its own: this project does not reference the LibreMetaverse client, so everything
    /// here is what a proxy or a packet tool gets without it.
    /// </summary>
    [TestFixture]
    public class ProtocolTests
    {
        // A logger that says every level is on, because PacketToString only describes a packet fully when Trace is
        private sealed class EverythingOn : ILoggerFactory, ILogger
        {
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => this;
            public void Dispose() { }
            public bool IsEnabled(LogLevel logLevel) => true;
            public IDisposable BeginScope<TState>(TState state) => null;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
        }

        private LogLevel _level;
        private EverythingOn _logger;

        [SetUp]
        public void SetUp()
        {
            _level = ProcessSettings.LogLevel;
            ProcessSettings.LogLevel = LogLevel.Trace;
            _logger = new EverythingOn();
            Logger.SetLoggerFactory(_logger, "test");
        }

        [TearDown]
        public void TearDown()
        {
            ProcessSettings.LogLevel = _level;
            Logger.SetLoggerFactory(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, "test");
            _logger.Dispose();
        }

        /// <summary>The bytes of a packet as they go on the wire: zero-encoded if the packet says it is</summary>
        private static byte[] Wire(Packet packet)
        {
            var data = packet.ToBytes();
            if (!packet.Header.Zerocoded) return data;
            var buffer = new byte[4096];
            int length = Helpers.ZeroEncode(data, data.Length, buffer);
            Array.Resize(ref buffer, length);
            return buffer;
        }

        private static Packet Parse(byte[] bytes)
        {
            int end = bytes.Length - 1;
            return Packet.BuildPacket(bytes, ref end, new byte[4096]);
        }

        private static ChatFromViewerPacket Chat(string text, int channel)
        {
            return new ChatFromViewerPacket
            {
                AgentData = { AgentID = UUID.Random(), SessionID = UUID.Random() },
                ChatData = { Message = Encoding.UTF8.GetBytes(text + "\0"), Type = 1, Channel = channel }
            };
        }

        [Test]
        public void Packet_ToBytes_ThenBuildPacket_GivesTheSamePacket()
        {
            var packet = Chat("hello there", 42);
            packet.Header.Reliable = true;
            packet.Header.Sequence = 7;

            var parsed = Parse(Wire(packet));

            Assert.That(parsed, Is.TypeOf<ChatFromViewerPacket>());
            var chat = (ChatFromViewerPacket)parsed;
            Assert.Multiple(() =>
            {
                Assert.That(chat.AgentData.AgentID, Is.EqualTo(packet.AgentData.AgentID));
                Assert.That(chat.AgentData.SessionID, Is.EqualTo(packet.AgentData.SessionID));
                Assert.That(Encoding.UTF8.GetString(chat.ChatData.Message), Is.EqualTo("hello there\0"));
                Assert.That(chat.ChatData.Type, Is.EqualTo((byte)1));
                Assert.That(chat.ChatData.Channel, Is.EqualTo(42));
                Assert.That(chat.Header.Reliable, Is.True);
                Assert.That(chat.Header.Sequence, Is.EqualTo(7u));
                Assert.That(chat.Type, Is.EqualTo(PacketType.ChatFromViewer));
            });
        }

        [Test]
        public void ZeroCodedPacket_SurvivesTheTrip_AndIsSmallerOnTheWire()
        {
            // AgentUpdate is zerocoded and mostly zeros when it is new, which is what zerocoding is for
            var packet = new AgentUpdatePacket();
            packet.AgentData.AgentID = UUID.Random();
            packet.AgentData.ControlFlags = 5;
            packet.AgentData.CameraAtAxis = new Vector3(1f, 0f, 0f);
            packet.AgentData.BodyRotation = Quaternion.Identity;
            packet.AgentData.HeadRotation = Quaternion.Identity;

            var bytes = Wire(packet);
            var parsed = (AgentUpdatePacket)Parse(bytes);

            Assert.Multiple(() =>
            {
                Assert.That(packet.Header.Zerocoded, Is.True);
                Assert.That(parsed.AgentData.AgentID, Is.EqualTo(packet.AgentData.AgentID));
                Assert.That(parsed.AgentData.ControlFlags, Is.EqualTo(5u));
                Assert.That(parsed.AgentData.CameraAtAxis, Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(parsed.AgentData.CameraUpAxis, Is.EqualTo(Vector3.Zero));
                Assert.That(bytes.Length, Is.LessThan(packet.ToBytes().Length));
            });
        }

        [Test]
        public void BuildPacket_ByType_GivesAnEmptyPacketOfThatType()
        {
            var packet = Packet.BuildPacket(PacketType.ChatFromViewer);

            Assert.That(packet, Is.TypeOf<ChatFromViewerPacket>());
        }

        [Test]
        public void PacketToString_NamesThePacketAndItsFields()
        {
            var text = PacketDecoder.PacketToString(Chat("hello there", 42));

            Assert.That(text, Does.Contain("ChatFromViewer"));
            Assert.That(text, Does.Contain("Channel"));
            Assert.That(text, Does.Contain("42"));
        }

        [Test]
        public void PacketToString_WithoutTheClient_PrintsManagerFlagsAsNumbers()
        {
            // The names of the control flags belong to the client, which a protocol tool does not have
            var packet = new AgentUpdatePacket();
            packet.AgentData.ControlFlags = 5;

            var text = PacketDecoder.PacketToString(packet);

            Assert.That(text, Does.Contain("ControlFlags"));
            Assert.That(text, Does.Not.Contain("AGENT_CONTROL"));
        }

        [Test]
        public void ADecoderRegistered_IsUsedWhenAPacketIsPrinted_AndNotAfterItIsTakenBack()
        {
            var packet = new AgentUpdatePacket();
            packet.AgentData.ControlFlags = 5;

            PacketDecoder.RegisterFieldDecoder("ControlFlags", (name, data) => "<<" + name + " is " + data + ">>");
            try
            {
                Assert.That(PacketDecoder.PacketToString(packet), Does.Contain("<<ControlFlags is 5>>"));
            }
            finally
            {
                Assert.That(PacketDecoder.UnregisterFieldDecoder("ControlFlags"), Is.True);
            }

            Assert.That(PacketDecoder.PacketToString(packet), Does.Not.Contain("<<"));
            Assert.That(PacketDecoder.UnregisterFieldDecoder("ControlFlags"), Is.False);
        }

        [Test]
        public void RegisterFieldDecoder_RefusesNothing()
        {
            Assert.Throws<ArgumentException>(() => PacketDecoder.RegisterFieldDecoder("", (n, d) => ""));
            Assert.Throws<ArgumentNullException>(() => PacketDecoder.RegisterFieldDecoder("Some.Field", null));
        }

        [Test]
        public void EventQueueMessage_IsDecodedByName_AndSerializesBack()
        {
            var ip = IPAddress.Parse("10.1.2.3");
            var message = new EnableSimulatorMessage
            {
                Simulators = new[]
                {
                    new EnableSimulatorMessage.SimulatorInfoBlock { RegionHandle = 1099511628032UL, IP = ip, Port = 9000, RegionSizeX = 256, RegionSizeY = 512 }
                }
            };

            var decoded = MessageUtils.DecodeEvent("EnableSimulator", message.Serialize()) as EnableSimulatorMessage;

            Assert.That(decoded, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(decoded.Simulators, Has.Length.EqualTo(1));
                Assert.That(decoded.Simulators[0].RegionHandle, Is.EqualTo(1099511628032UL));
                Assert.That(decoded.Simulators[0].IP, Is.EqualTo(ip));
                Assert.That(decoded.Simulators[0].Port, Is.EqualTo(9000));
                Assert.That(decoded.Simulators[0].RegionSizeY, Is.EqualTo(512u));
            });
        }

        [Test]
        public void EventQueueMessage_WithNoClass_IsNull()
        {
            Assert.That(MessageUtils.DecodeEvent("NoSuchEvent", new OSDMap()), Is.Null);
        }

        [Test]
        public void TheEnumsTheMessagesUse_AreHere()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)SimAccess.Mature, Is.EqualTo(21));
                Assert.That(ParcelFlags.AllowFly, Is.Not.EqualTo(ParcelFlags.None));
                Assert.That(Enum.IsDefined(typeof(InstantMessageDialog), InstantMessageDialog.MessageFromAgent), Is.True);
                Assert.That(new AgentDisplayName(), Is.Not.Null);
            });
        }

        [Test]
        public void AVariableBlockIsSplitAt255Blocks_BecauseItsCountIsOneByte()
        {
            // PacketAck blocks are four bytes, so more than 255 fit in a datagram; the count byte would wrap and the rest be lost
            var ack = new PacketAckPacket { Packets = Enumerable.Range(1, 300).Select(i => new PacketAckPacket.PacketsBlock { ID = (uint)i }).ToArray() };

            var datagrams = ack.ToBytesMultiple();
            var ids = new List<uint>();
            foreach (var datagram in datagrams)
            {
                int end = datagram.Length - 1;
                var rebuilt = (PacketAckPacket)Packet.BuildPacket(datagram, ref end, null);
                ids.AddRange(rebuilt.Packets.Select(b => b.ID));
                Assert.That(rebuilt.Packets.Length, Is.LessThanOrEqualTo(255));
            }

            Assert.Multiple(() =>
            {
                Assert.That(datagrams, Has.Length.EqualTo(2));
                Assert.That(ids, Is.EqualTo(Enumerable.Range(1, 300).Select(i => (uint)i)));
            });
        }

        [Test]
        public void AVariableBlockOfFewerThan255_IsNotSplitWhenItFits()
        {
            var ack = new PacketAckPacket { Packets = Enumerable.Range(1, 255).Select(i => new PacketAckPacket.PacketsBlock { ID = (uint)i }).ToArray() };

            Assert.That(ack.ToBytesMultiple(), Has.Length.EqualTo(1));
        }
    }
}
