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



using System.Collections.Generic;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Extended mesh flags in <see cref="Primitive.GetOSD"/> / <see cref="Primitive.FromOSD"/>: "extended_mesh" is a
    /// {"flags"} map, as LLExtendedMeshParams::asLLSD writes it, and is left out when the flags are zero.
    /// </summary>
    [TestFixture]
    public class ExtendedMeshOSDTests
    {
        private static Primitive RoundTrip(Primitive prim) => Primitive.FromOSD(prim.GetOSD());

        [Test]
        public void RoundTrip_KeepsTheAnimatedObjectFlag()
        {
            var reread = RoundTrip(new Primitive { ExtendedMeshFlags = 0x1u });

            Assert.Multiple(() =>
            {
                Assert.That(reread.ExtendedMeshFlags, Is.EqualTo(0x1u));
                Assert.That(reread.IsAnimatedObject, Is.True);
            });
        }

        [Test]
        public void RoundTrip_ThroughLLSDXml_KeepsTheFlags()
        {
            var prim = new Primitive { ExtendedMeshFlags = 0x1u };

            var xml = OSDParser.SerializeLLSDXmlString(prim.GetOSD());
            var reread = Primitive.FromOSD(OSDParser.DeserializeLLSDXml(xml));

            Assert.That(reread.ExtendedMeshFlags, Is.EqualTo(0x1u));
        }

        [TestCase(0x1u)]
        [TestCase(0x2u)]
        [TestCase(0x80000000u)]
        [TestCase(0xFFFFFFFFu)]
        public void RoundTrip_KeepsEveryFlagBit(uint flags)
        {
            var prim = new Primitive { ExtendedMeshFlags = flags };

            var xml = OSDParser.SerializeLLSDXmlString(prim.GetOSD());

            Assert.Multiple(() =>
            {
                Assert.That(RoundTrip(prim).ExtendedMeshFlags, Is.EqualTo(flags));
                Assert.That(Primitive.FromOSD(OSDParser.DeserializeLLSDXml(xml)).ExtendedMeshFlags, Is.EqualTo(flags));
            });
        }

        [Test]
        public void ZeroFlags_AddNoKey_AndReadBackAsZero()
        {
            var osd = (OSDMap)new Primitive().GetOSD();

            Assert.Multiple(() =>
            {
                Assert.That(osd.ContainsKey("extended_mesh"), Is.False);
                Assert.That(Primitive.FromOSD(osd).ExtendedMeshFlags, Is.EqualTo(0u));
            });
        }

        [Test]
        public void GetOSD_WritesAFlagsMap()
        {
            var osd = (OSDMap)new Primitive { ExtendedMeshFlags = 0x1u }.GetOSD();

            var map = (OSDMap)osd["extended_mesh"];

            Assert.Multiple(() =>
            {
                Assert.That(map, Has.Count.EqualTo(1));
                Assert.That(map["flags"].Type, Is.EqualTo(OSDType.Integer));
                Assert.That(map["flags"].AsInteger(), Is.EqualTo(1));
            });
        }

        [Test]
        public void FromOSD_IgnoresAMalformedEntry()
        {
            var osd = (OSDMap)new Primitive().GetOSD();
            osd["extended_mesh"] = new OSDMap { ["other"] = OSD.FromInteger(1) };
            Assert.That(Primitive.FromOSD(osd).ExtendedMeshFlags, Is.EqualTo(0u));

            osd["extended_mesh"] = OSD.FromInteger(1);
            Assert.That(Primitive.FromOSD(osd).ExtendedMeshFlags, Is.EqualTo(0u));
        }

        [Test]
        public void RoundTrip_KeepsFlagsAlongsideRenderMaterials()
        {
            var prim = new Primitive
            {
                ExtendedMeshFlags = 0x1u,
                RenderMaterials = new Dictionary<int, UUID> { [0] = UUID.Random(), [2] = UUID.Random() }
            };

            var reread = RoundTrip(prim);

            Assert.Multiple(() =>
            {
                Assert.That(reread.ExtendedMeshFlags, Is.EqualTo(0x1u));
                Assert.That(reread.RenderMaterials, Is.EqualTo(prim.RenderMaterials));
            });
        }

        [Test]
        public void Avatar_RoundTrip_KeepsTheFlags()
        {
            var avatar = new Avatar { ExtendedMeshFlags = 0x1u };

            var reread = Avatar.FromOSD(avatar.GetOSD());

            Assert.That(reread.ExtendedMeshFlags, Is.EqualTo(0x1u));
        }
    }
}
