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

using System.Net;
using System.Reflection;
using LibreMetaverse.Packets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the estate "setexperience" EstateOwnerMessage, which reports the estate's
    /// key/trusted/allowed/blocked experience lists (Region/Estate &gt; Experiences tab). Wire format
    /// verified against the reference implementation in indra/newview/llfloaterregioninfo.cpp
    /// (LLDispatchSetEstateExperience::operator() / getIDs).
    /// </summary>
    [TestFixture]
    [Category("Estate")]
    public class EstateExperienceReplyTests
    {
        private static EstateOwnerMessagePacket.ParamListBlock Param(byte[] data) =>
            new EstateOwnerMessagePacket.ParamListBlock { Parameter = data };

        [Test]
        public void SetExperience_ParsesBlockedTrustedAndAllowedLists()
        {
            var client = new GridClient();

            var blocked = UUID.Random();
            var trusted = UUID.Random();
            var allowed1 = UUID.Random();
            var allowed2 = UUID.Random();

            var packet = new EstateOwnerMessagePacket
            {
                AgentData = new EstateOwnerMessagePacket.AgentDataBlock
                {
                    AgentID = UUID.Random(), SessionID = UUID.Random(), TransactionID = UUID.Random()
                },
                MethodData = new EstateOwnerMessagePacket.MethodDataBlock
                {
                    Method = Utils.StringToBytes("setexperience"), Invoice = UUID.Zero
                },
                ParamList = new[]
                {
                    Param(Utils.StringToBytes("1")),  // estate_id
                    Param(Utils.StringToBytes("0")),  // send_to_agent_only
                    Param(Utils.StringToBytes("1")),  // num blocked
                    Param(Utils.StringToBytes("1")),  // num trusted
                    Param(Utils.StringToBytes("2")),  // num allowed
                    Param(blocked.GetBytes()),
                    Param(trusted.GetBytes()),
                    Param(allowed1.GetBytes()),
                    Param(allowed2.GetBytes())
                }
            };

            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);

            EstateExperienceReplyEventArgs? received = null;
            client.Estate.EstateExperienceReply += (_, e) => received = e;

            var handler = typeof(EstateTools).GetMethod("EstateOwnerMessageHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(handler, Is.Not.Null);
            handler!.Invoke(client.Estate, new object?[] { null, new PacketReceivedEventArgs(packet, sim) });

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Blocked, Is.EquivalentTo(new[] { blocked }));
            Assert.That(received.Trusted, Is.EquivalentTo(new[] { trusted }));
            Assert.That(received.Allowed, Is.EquivalentTo(new[] { allowed1, allowed2 }));
        }

        [Test]
        public void SetExperience_AllEmptyLists_ProducesEmptyResult()
        {
            var client = new GridClient();

            var packet = new EstateOwnerMessagePacket
            {
                AgentData = new EstateOwnerMessagePacket.AgentDataBlock
                {
                    AgentID = UUID.Random(), SessionID = UUID.Random(), TransactionID = UUID.Random()
                },
                MethodData = new EstateOwnerMessagePacket.MethodDataBlock
                {
                    Method = Utils.StringToBytes("setexperience"), Invoice = UUID.Zero
                },
                ParamList = new[]
                {
                    Param(Utils.StringToBytes("1")), // estate_id
                    Param(Utils.StringToBytes("0")), // send_to_agent_only
                    Param(Utils.StringToBytes("0")), // num blocked
                    Param(Utils.StringToBytes("0")), // num trusted
                    Param(Utils.StringToBytes("0"))  // num allowed
                }
            };

            var sim = new Simulator(client, new IPEndPoint(IPAddress.Loopback, 13), 0);

            EstateExperienceReplyEventArgs? received = null;
            client.Estate.EstateExperienceReply += (_, e) => received = e;

            var handler = typeof(EstateTools).GetMethod("EstateOwnerMessageHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            handler!.Invoke(client.Estate, new object?[] { null, new PacketReceivedEventArgs(packet, sim) });

            Assert.That(received, Is.Not.Null);
            Assert.That(received!.Blocked, Is.Empty);
            Assert.That(received.Trusted, Is.Empty);
            Assert.That(received.Allowed, Is.Empty);
        }
    }
}
