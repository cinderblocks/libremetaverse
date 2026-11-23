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

using LibreMetaverse.Materials;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// <see cref="LegacyMaterial"/> reads <c>SpecColor</c> from a RenderMaterials reply. The simulator
    /// sends the components as 0-255 integers; a real-valued 0-1 array is also accepted.
    /// </summary>
    [TestFixture]
    public class LegacyMaterialSpecColorTests
    {
        // A RenderMaterials "Material" map as the simulator sends it: transforms as integers x 10000,
        // SpecColor as 0-255 integers (here 255, 128, 64, 255).
        private const string RenderMaterialsSpecColorXml = """
            <llsd><map>
              <key>NormMap</key><uuid>8f3c3a52-6d2e-4c3b-9f00-1a2b3c4d5e6f</uuid>
              <key>NormOffsetX</key><integer>0</integer><key>NormOffsetY</key><integer>0</integer>
              <key>NormRepeatX</key><integer>10000</integer><key>NormRepeatY</key><integer>10000</integer>
              <key>NormRotation</key><integer>0</integer>
              <key>SpecMap</key><uuid>1d6b4f3e-0c2a-4b5d-8e7f-0a9b8c7d6e5f</uuid>
              <key>SpecOffsetX</key><integer>0</integer><key>SpecOffsetY</key><integer>0</integer>
              <key>SpecRepeatX</key><integer>10000</integer><key>SpecRepeatY</key><integer>10000</integer>
              <key>SpecRotation</key><integer>0</integer>
              <key>SpecColor</key><array><integer>255</integer><integer>128</integer><integer>64</integer><integer>255</integer></array>
              <key>SpecExp</key><integer>51</integer>
              <key>EnvIntensity</key><integer>26</integer>
              <key>DiffuseAlphaMode</key><integer>0</integer>
              <key>AlphaMaskCutoff</key><integer>0</integer>
            </map></llsd>
            """;

        private static LegacyMaterial Parse(OSD material)
        {
            return new LegacyMaterial(new OSDMap
            {
                ["ID"] = OSD.FromBinary(UUID.Random().GetBytes()),
                ["Material"] = material
            });
        }

        [Test]
        public void IntegerSpecColor_FromARenderMaterialsReply_IsScaledToZeroOne()
        {
            var materialMap = (OSDMap)OSDParser.DeserializeLLSDXml(RenderMaterialsSpecColorXml);
            Assert.That(materialMap["SpecColor"].AsColor4(), Is.EqualTo(new Color4(1f, 1f, 1f, 1f)),
                "test precondition: AsColor4 clamps each 0-255 integer to 1");

            var material = Parse(materialMap);

            Assert.Multiple(() =>
            {
                Assert.That(material.SpecularColor, Is.EqualTo(new Color4(1f, 128 / 255f, 64 / 255f, 1f)));
                Assert.That(material.SpecularExponent, Is.EqualTo((byte)51));
                Assert.That(material.EnvironmentIntensity, Is.EqualTo((byte)26));
                Assert.That(material.SpecularMap, Is.EqualTo(new UUID("1d6b4f3e-0c2a-4b5d-8e7f-0a9b8c7d6e5f")));
            });
        }

        [Test]
        public void RealSpecColor_IsLeftInZeroOne()
        {
            var materialMap = (OSDMap)OSDParser.DeserializeLLSDXml(RenderMaterialsSpecColorXml);
            materialMap["SpecColor"] = OSD.FromColor4(new Color4(1f, 0.5f, 0.25f, 1f));

            var material = Parse(materialMap);

            Assert.That(material.SpecularColor, Is.EqualTo(new Color4(1f, 0.5f, 0.25f, 1f)));
        }
    }
}
