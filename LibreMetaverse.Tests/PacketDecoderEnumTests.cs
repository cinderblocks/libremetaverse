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
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="PacketDecoder"/> prints some fields by the names of enums that belong to the managers. These
    /// are worked out here from the enums themselves, not copied from the decoder, so the text is checked against
    /// what the enum says whichever assembly the decoder is in.
    /// </summary>
    [TestFixture]
    public class PacketDecoderEnumTests
    {
        private static string Decode(string key, object value)
        {
            Assert.That(PacketDecoder.TrySpecialDecode(key, value, out var text), Is.True, key + " has a decoder");
            return text;
        }

        // The decoder lays a field out as: name right-aligned in 30, the value, then the enum's flag names and its type
        private static string Line(string name, object value, int valueWidth, object flags, int flagsWidth, string type) =>
            string.Format("{0,30}: {1} {2} [{3}]", name, value.ToString().PadRight(valueWidth), ("(" + flags + ")").PadRight(flagsWidth), type);

        [Test]
        public void ScriptControlChange_NamesTheControlsTaken()
        {
            var flags = AgentManager.ControlFlags.AGENT_CONTROL_AT_POS | AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG;

            Assert.That(Decode("ScriptControlChangePacket.Data.Controls", (uint)flags),
                Is.EqualTo(Line("Controls", (uint)flags, 10, flags, 29, "AgentManager.ControlFlags")));
        }

        [Test]
        public void AgentUpdate_NamesTheControlFlags()
        {
            var flags = AgentManager.ControlFlags.AGENT_CONTROL_UP_POS | AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS;

            Assert.That(Decode("AgentUpdatePacket.AgentData.ControlFlags", (uint)flags),
                Is.EqualTo(Line("ControlFlags", (uint)flags, 10, flags, 29, "UInt32")));
        }

        [Test]
        public void DirClassifiedQuery_NamesTheQueryFlags()
        {
            var flags = DirectoryManager.ClassifiedQueryFlags.Mature | DirectoryManager.ClassifiedQueryFlags.PG;

            Assert.That(Decode("DirClassifiedQueryPacket.QueryData.QueryFlags", (uint)flags),
                Is.EqualTo(Line("QueryFlags", (uint)flags, 10, flags, 29, "ClassifiedQueryFlags")));
        }

        [Test]
        public void DirLandQuery_NamesTheFindFlagsAndTheSearchType()
        {
            var find = DirectoryManager.DirFindFlags.People | DirectoryManager.DirFindFlags.Events;
            var search = DirectoryManager.SearchTypeFlags.Auction | DirectoryManager.SearchTypeFlags.Mainland;

            Assert.Multiple(() =>
            {
                Assert.That(Decode("DirLandQueryPacket.QueryData.QueryFlags", (uint)find),
                    Is.EqualTo(Line("QueryFlags", (uint)find, 10, find, 29, "DirectoryManager.DirFindFlags")));
                Assert.That(Decode("DirLandQueryPacket.QueryData.SearchType", (uint)search),
                    Is.EqualTo(Line("SearchType", (uint)search, 10, search, 29, "DirectoryManager.SearchTypeFlags")));
            });
        }

        [Test]
        public void DirClassifiedReply_NamesTheClassifiedFlags()
        {
            var flags = DirectoryManager.ClassifiedFlags.Mature | DirectoryManager.ClassifiedFlags.Enabled;

            Assert.That(Decode("DirClassifiedReplyPacket.QueryReplies.ClassifiedFlags", (byte)flags),
                Is.EqualTo(Line("ClassifiedFlags", (byte)flags, 10, flags, 29, "ClassifiedFlags")));
        }

        [Test]
        public void DirEventsReply_NamesTheEventFlags()
        {
            var flags = DirectoryManager.EventFlags.Mature;

            Assert.That(Decode("DirEventsReplyPacket.QueryReplies.EventFlags", (uint)flags),
                Is.EqualTo(Line("EventFlags", (uint)flags, 3, flags, 36, "EventFlags")));
        }

        [Test]
        public void ADecoderRegisteredByAnApplication_IsUsedForItsKey()
        {
            PacketDecoder.RegisterFieldDecoder("TestMessagePacket.TestBlock1.Test1", (name, data) => "mine:" + name + "=" + data);
            try
            {
                Assert.That(Decode("TestMessagePacket.TestBlock1.Test1", 7u), Is.EqualTo("mine:Test1=7"));
            }
            finally
            {
                PacketDecoder.UnregisterFieldDecoder("TestMessagePacket.TestBlock1.Test1");
            }
            Assert.That(PacketDecoder.TrySpecialDecode("TestMessagePacket.TestBlock1.Test1", 7u, out _), Is.False);
        }
    }
}
