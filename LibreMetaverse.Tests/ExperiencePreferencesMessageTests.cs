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
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for the agent's global experience allow/block preferences (GetExperiences /
    /// ExperiencePreferences capabilities) and the per-experience permission helpers built on top of
    /// them. Wire format verified against the reference viewer (llfloaterexperiences.cpp /
    /// llfloaterexperienceprofile.cpp): the allowed list is returned under the "experiences" key, not
    /// "allowed".
    /// </summary>
    [TestFixture]
    [Category("Experience")]
    public class ExperiencePreferencesMessageTests
    {
        [Test]
        public void Deserialize_ReadsAllowedListFromExperiencesKey()
        {
            var allowed = UUID.Random();
            var blocked = UUID.Random();

            var map = new OSDMap
            {
                ["experiences"] = new OSDArray { OSD.FromUUID(allowed) },
                ["blocked"] = new OSDArray { OSD.FromUUID(blocked) }
            };

            var msg = new LibreMetaverse.Messages.Linden.ExperiencePreferencesMessage();
            msg.Deserialize(map);

            Assert.That(msg.Allowed, Is.EquivalentTo(new[] { allowed }));
            Assert.That(msg.Blocked, Is.EquivalentTo(new[] { blocked }));
        }

        [Test]
        public void Deserialize_IgnoresLegacyAllowedKey()
        {
            // Regression guard: an earlier version of this message read "allowed" instead of the
            // server's actual "experiences" key, which silently produced an empty Allowed list.
            var map = new OSDMap
            {
                ["allowed"] = new OSDArray { OSD.FromUUID(UUID.Random()) },
                ["blocked"] = new OSDArray()
            };

            var msg = new LibreMetaverse.Messages.Linden.ExperiencePreferencesMessage();
            msg.Deserialize(map);

            Assert.That(msg.Allowed, Is.Empty);
        }

        [Test]
        public void Serialize_WritesAllowedListUnderExperiencesKey()
        {
            var msg = new LibreMetaverse.Messages.Linden.ExperiencePreferencesMessage();
            msg.Allowed.Add(UUID.Random());

            var map = msg.Serialize();

            Assert.That(map.ContainsKey("experiences"), Is.True);
            Assert.That(map.ContainsKey("allowed"), Is.False);
        }

        [Test]
        public void ExtractExperiencePermission_Allowed_ReturnsAllow()
        {
            var id = UUID.Random();
            var map = new OSDMap { ["experiences"] = new OSDArray { OSD.FromUUID(id) } };

            Assert.That(InvokeExtract(map, id), Is.EqualTo("Allow"));
        }

        [Test]
        public void ExtractExperiencePermission_Blocked_ReturnsBlock()
        {
            var id = UUID.Random();
            var map = new OSDMap { ["blocked"] = new OSDArray { OSD.FromUUID(id) } };

            Assert.That(InvokeExtract(map, id), Is.EqualTo("Block"));
        }

        [Test]
        public void ExtractExperiencePermission_NeitherList_ReturnsForget()
        {
            var id = UUID.Random();
            var map = new OSDMap
            {
                ["experiences"] = new OSDArray { OSD.FromUUID(UUID.Random()) },
                ["blocked"] = new OSDArray { OSD.FromUUID(UUID.Random()) }
            };

            Assert.That(InvokeExtract(map, id), Is.EqualTo("Forget"));
        }

        private static string InvokeExtract(OSDMap map, UUID experienceId)
        {
            var method = typeof(AgentManager).GetMethod("ExtractExperiencePermission",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method!.Invoke(null, new object[] { map, experienceId })!;
        }
    }
}
