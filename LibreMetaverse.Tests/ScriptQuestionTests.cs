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

using System.Reflection;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// ScriptQuestion (llRequestPermissions, and llRequestExperiencePermissions through its Experience block)
    /// raised as <see cref="AgentManager.ScriptQuestion"/>.
    /// </summary>
    [TestFixture]
    public class ScriptQuestionTests
    {
        private static ScriptQuestionEventArgs? Raise(ScriptQuestionPacket packet)
        {
            using var client = new FakeGridClient();
            client.AddCapability("Placeholder", new System.Uri("http://test.invalid/placeholder"));
            ScriptQuestionEventArgs? raised = null;
            client.Self.ScriptQuestion += (_, e) => raised = e;

            var method = typeof(AgentManager).GetMethod("ScriptQuestionHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(client.Self, new object?[] { null, new PacketReceivedEventArgs(packet, client.Network.CurrentSim) });
            return raised;
        }

        private static ScriptQuestionPacket Question(UUID experience)
        {
            var packet = new ScriptQuestionPacket();
            packet.Data.TaskID = UUID.Random();
            packet.Data.ItemID = UUID.Random();
            packet.Data.ObjectName = Utils.StringToBytes("Door");
            packet.Data.ObjectOwner = Utils.StringToBytes("Some Owner");
            packet.Data.Questions = (int)(ScriptPermission.TriggerAnimation | ScriptPermission.Teleport);
            packet.Experience.ExperienceID = experience;
            return packet;
        }

        [Test]
        public void ExperiencePermissionRequest_CarriesTheExperience()
        {
            var experience = UUID.Random();
            var e = Raise(Question(experience));

            Assert.That(e, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(e!.ExperienceID, Is.EqualTo(experience));
                Assert.That(e.ObjectName, Is.EqualTo("Door"));
                Assert.That(e.Questions, Is.EqualTo(ScriptPermission.TriggerAnimation | ScriptPermission.Teleport));
            });
        }

        [Test]
        public void OrdinaryPermissionRequest_HasNoExperience()
        {
            var e = Raise(Question(UUID.Zero));

            Assert.That(e, Is.Not.Null);
            Assert.That(e!.ExperienceID, Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void ExperienceBlock_SurvivesTheWire()
        {
            var experience = UUID.Random();
            var bytes = Question(experience).ToBytes();
            int end = bytes.Length - 1;
            var decoded = (ScriptQuestionPacket)Packet.BuildPacket(bytes, ref end, null);

            Assert.That(Raise(decoded)!.ExperienceID, Is.EqualTo(experience));
        }
    }
}
