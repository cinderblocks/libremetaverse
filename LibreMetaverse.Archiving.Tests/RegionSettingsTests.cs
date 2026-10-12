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
using System.IO;
using System.Text;
using System.Xml;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("OarFile")]
    public class RegionSettingsTests
    {
        private const string Indented = @"<?xml version=""1.0"" encoding=""utf-16""?>
<RegionSettings>
  <General>
    <AllowDamage>True</AllowDamage>
    <AllowLandResell>False</AllowLandResell>
    <MaturityRating>2</MaturityRating>
    <AgentLimit>25</AgentLimit>
    <ObjectBonus>1.5</ObjectBonus>
  </General>
  <GroundTextures>
    <Texture1>b8d3965a-ad78-bf43-699b-bff8eca6c975</Texture1>
    <ElevationLowSW>10.5</ElevationLowSW>
    <ElevationHighNE>60</ElevationHighNE>
  </GroundTextures>
  <Terrain>
    <WaterHeight>20.25</WaterHeight>
    <TerrainLowerLimit>-100</TerrainLowerLimit>
    <UseEstateSun>True</UseEstateSun>
    <SunPosition>24.38</SunPosition>
  </Terrain>
  <Telehub />
</RegionSettings>";

        private static RegionSettings Read(string xml, Encoding encoding = null) =>
            RegionSettings.FromStream(new MemoryStream((encoding ?? new UTF8Encoding(false)).GetBytes(xml)));

        private static void AssertIndentedValues(RegionSettings s)
        {
            Assert.Multiple(() =>
            {
                Assert.That(s.AllowDamage, Is.True);
                Assert.That(s.AllowLandResell, Is.False);
                Assert.That(s.MaturityRating, Is.EqualTo(2));
                Assert.That(s.AgentLimit, Is.EqualTo(25));
                Assert.That(s.ObjectBonus, Is.EqualTo(1.5f));
                Assert.That(s.TerrainDetail0, Is.EqualTo(UUID.Parse("b8d3965a-ad78-bf43-699b-bff8eca6c975")));
                Assert.That(s.TerrainStartHeight00, Is.EqualTo(10.5f));
                Assert.That(s.TerrainHeightRange11, Is.EqualTo(60f));
                Assert.That(s.WaterHeight, Is.EqualTo(20.25f));
                Assert.That(s.TerrainLowerLimit, Is.EqualTo(-100f));
                Assert.That(s.UseEstateSun, Is.True);
            });
        }

        [Test]
        public void FromStream_Utf8BytesUnderAUtf16Declaration_AreRead()
        {
            // How OpenSim writes the file: the text goes through a StringWriter, which says utf-16, and is saved as UTF-8
            AssertIndentedValues(Read(Indented));
        }

        [Test]
        public void FromStream_ABomIsHonoured()
        {
            var bytes = new UTF8Encoding(true).GetPreamble();
            var body = Encoding.UTF8.GetBytes(Indented.Replace("utf-16", "utf-8"));
            var all = new byte[bytes.Length + body.Length];
            bytes.CopyTo(all, 0);
            body.CopyTo(all, bytes.Length);

            AssertIndentedValues(RegionSettings.FromStream(new MemoryStream(all)));
        }

        [Test]
        public void FromStream_RealUtf16WithABom_IsRead()
        {
            var xml = Indented;
            var bytes = new UnicodeEncoding(false, true).GetPreamble();
            var body = new UnicodeEncoding(false, false).GetBytes(xml);
            var all = new byte[bytes.Length + body.Length];
            bytes.CopyTo(all, 0);
            body.CopyTo(all, bytes.Length);

            AssertIndentedValues(RegionSettings.FromStream(new MemoryStream(all)));
        }

        [Test]
        public void FromStream_XmlWithNoWhiteSpaceBetweenElements_ReadsEveryElement()
        {
            // The old loop read one element and skipped the next unless white space came between them
            var compact = System.Text.RegularExpressions.Regex.Replace(Indented, @">\s+<", "><");
            Assert.That(compact, Does.Not.Contain("\n"));

            AssertIndentedValues(Read(compact));
        }

        [Test]
        public void FromStream_InAnyOrder_AndWithSomeMissing_GivesWhatIsThere()
        {
            var s = Read("<RegionSettings><Terrain><WaterHeight>5</WaterHeight></Terrain><General><AgentLimit>9</AgentLimit></General></RegionSettings>");

            Assert.Multiple(() =>
            {
                Assert.That(s.WaterHeight, Is.EqualTo(5f));
                Assert.That(s.AgentLimit, Is.EqualTo(9));
                Assert.That(s.MaturityRating, Is.EqualTo(0));
                Assert.That(s.TerrainDetail0, Is.EqualTo(UUID.Zero));
            });
        }

        [Test]
        public void FromStream_AValueThatIsNotValid_IsLeftAtItsDefault()
        {
            var s = Read("<RegionSettings><General><AgentLimit>lots</AgentLimit><MaturityRating>2</MaturityRating></General></RegionSettings>");

            Assert.That(s.AgentLimit, Is.EqualTo(0));
            Assert.That(s.MaturityRating, Is.EqualTo(2));
        }

        [Test]
        public void FromStream_ADocumentThatIsNotRegionSettings_Throws()
        {
            Assert.Throws<XmlException>(() => Read("<Something />"));
        }

        [Test]
        public void ToXml_ThenFromStream_GivesBackTheSettings()
        {
            var file = Path.Combine(Path.GetTempPath(), "lm-regionsettings-" + Guid.NewGuid() + ".xml");
            var original = new RegionSettings
            {
                AllowDamage = true, BlockFly = true, MaturityRating = 2, AgentLimit = 33, ObjectBonus = 1.25f,
                TerrainDetail0 = UUID.Random(), TerrainDetail3 = UUID.Random(),
                TerrainStartHeight01 = 3.5f, TerrainHeightRange10 = 71.25f,
                WaterHeight = 19.5f, TerrainRaiseLimit = 80f, TerrainLowerLimit = -40f, UseEstateSun = true, FixedSun = true
            };

            try
            {
                original.ToXML(file);
                RegionSettings read;
                using (var stream = File.OpenRead(file)) read = RegionSettings.FromStream(stream);

                Assert.Multiple(() =>
                {
                    Assert.That(read.AllowDamage, Is.True);
                    Assert.That(read.BlockFly, Is.True);
                    Assert.That(read.AllowLandResell, Is.False);
                    Assert.That(read.MaturityRating, Is.EqualTo(2));
                    Assert.That(read.AgentLimit, Is.EqualTo(33));
                    Assert.That(read.ObjectBonus, Is.EqualTo(1.25f));
                    Assert.That(read.TerrainDetail0, Is.EqualTo(original.TerrainDetail0));
                    Assert.That(read.TerrainDetail3, Is.EqualTo(original.TerrainDetail3));
                    Assert.That(read.TerrainStartHeight01, Is.EqualTo(3.5f));
                    Assert.That(read.TerrainHeightRange10, Is.EqualTo(71.25f));
                    Assert.That(read.WaterHeight, Is.EqualTo(19.5f));
                    Assert.That(read.TerrainRaiseLimit, Is.EqualTo(80f));
                    Assert.That(read.TerrainLowerLimit, Is.EqualTo(-40f));
                    Assert.That(read.UseEstateSun, Is.True);
                    Assert.That(read.FixedSun, Is.True);
                });
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
