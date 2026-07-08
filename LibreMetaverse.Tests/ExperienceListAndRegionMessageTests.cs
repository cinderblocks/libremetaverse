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

using LibreMetaverse.Messages.Linden;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Coverage for ExperienceListMessage (AgentExperiences/GetAdminExperiences/GetCreatorExperiences/
    /// GroupExperiences) and RegionExperiencesMessage (RegionExperiences). Wire format verified
    /// against the reference viewer: LLFloaterExperiences::refreshContents/updateInfo and
    /// LLExperienceCache::getGroupExperiencesCoro read "experience_ids" (not "experience_keys" --
    /// that key is only used by GetExperienceInfo/FindExperienceByName, which return full
    /// experience detail objects). LLPanelRegionExperiences::processResponse (shared by
    /// both the RegionExperiences capability and the estate-level "setexperience" message) reads
    /// "blocked"/"trusted"/"allowed" (not "contrib").
    /// </summary>
    [TestFixture]
    [Category("Experience")]
    public class ExperienceListAndRegionMessageTests
    {
        [Test]
        public void ExperienceListMessage_Deserialize_ReadsExperienceIdsKey()
        {
            var id = UUID.Random();
            var map = new OSDMap { ["experience_ids"] = new OSDArray { OSD.FromUUID(id) } };

            var msg = new ExperienceListMessage();
            msg.Deserialize(map);

            Assert.That(msg.ExperienceIDs, Is.EquivalentTo(new[] { id }));
        }

        [Test]
        public void ExperienceListMessage_Deserialize_IgnoresLegacyExperienceKeysKey()
        {
            var map = new OSDMap { ["experience_keys"] = new OSDArray { OSD.FromUUID(UUID.Random()) } };

            var msg = new ExperienceListMessage();
            msg.Deserialize(map);

            Assert.That(msg.ExperienceIDs, Is.Empty);
        }

        [Test]
        public void ExperienceListMessage_Serialize_WritesExperienceIdsKey()
        {
            var msg = new ExperienceListMessage();
            msg.ExperienceIDs.Add(UUID.Random());

            var map = msg.Serialize();

            Assert.That(map.ContainsKey("experience_ids"), Is.True);
            Assert.That(map.ContainsKey("experience_keys"), Is.False);
        }

        [Test]
        public void RegionExperiencesMessage_Deserialize_ReadsBlockedTrustedAllowedAndDefault()
        {
            var blocked = UUID.Random();
            var trusted = UUID.Random();
            var allowed = UUID.Random();
            var defaultExp = UUID.Random();

            var map = new OSDMap
            {
                ["blocked"] = new OSDArray { OSD.FromUUID(blocked) },
                ["trusted"] = new OSDArray { OSD.FromUUID(trusted) },
                ["allowed"] = new OSDArray { OSD.FromUUID(allowed) },
                ["default"] = OSD.FromUUID(defaultExp)
            };

            var msg = new RegionExperiencesMessage();
            msg.Deserialize(map);

            Assert.That(msg.Blocked, Is.EquivalentTo(new[] { blocked }));
            Assert.That(msg.Trusted, Is.EquivalentTo(new[] { trusted }));
            Assert.That(msg.Allowed, Is.EquivalentTo(new[] { allowed }));
            Assert.That(msg.Default, Is.EqualTo(defaultExp));
        }

        [Test]
        public void RegionExperiencesMessage_Deserialize_IgnoresLegacyContribKey()
        {
            var map = new OSDMap
            {
                ["blocked"] = new OSDArray(),
                ["trusted"] = new OSDArray(),
                ["contrib"] = new OSDArray { OSD.FromUUID(UUID.Random()) }
            };

            var msg = new RegionExperiencesMessage();
            msg.Deserialize(map);

            Assert.That(msg.Allowed, Is.Empty);
        }

        [Test]
        public void RegionExperiencesMessage_Serialize_WritesAllowedKeyNotContrib()
        {
            var msg = new RegionExperiencesMessage();
            msg.Allowed.Add(UUID.Random());

            var map = msg.Serialize();

            Assert.That(map.ContainsKey("allowed"), Is.True);
            Assert.That(map.ContainsKey("contrib"), Is.False);
        }
    }
}
