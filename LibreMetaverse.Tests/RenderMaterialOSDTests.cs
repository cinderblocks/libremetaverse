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
    /// Render materials in <see cref="Primitive.GetOSD"/> / <see cref="Primitive.FromOSD"/>: "render_material" is
    /// an array with a {"te_idx", "id"} map per face, in face order, and is left out when there are none.
    /// </summary>
    [TestFixture]
    public class RenderMaterialOSDTests
    {
        private static Primitive RoundTrip(Primitive prim) => Primitive.FromOSD(prim.GetOSD());

        [Test]
        public void RoundTrip_KeepsTheMaterialOfEachFace()
        {
            var prim = new Primitive
            {
                RenderMaterials = new Dictionary<int, UUID> { [0] = UUID.Random(), [3] = UUID.Random(), [7] = UUID.Random() }
            };

            Assert.That(RoundTrip(prim).RenderMaterials, Is.EqualTo(prim.RenderMaterials));
        }

        [Test]
        public void RoundTrip_ThroughLLSDXml_KeepsTheMaterialOfEachFace()
        {
            var prim = new Primitive
            {
                RenderMaterials = new Dictionary<int, UUID> { [1] = UUID.Random(), [4] = UUID.Random() }
            };

            var xml = OSDParser.SerializeLLSDXmlString(prim.GetOSD());
            var reread = Primitive.FromOSD(OSDParser.DeserializeLLSDXml(xml));

            Assert.That(reread.RenderMaterials, Is.EqualTo(prim.RenderMaterials));
        }

        [Test]
        public void NoRenderMaterials_StaysNull_AndAddsNoKey()
        {
            var osd = (OSDMap)new Primitive().GetOSD();

            Assert.Multiple(() =>
            {
                Assert.That(osd.ContainsKey("render_material"), Is.False);
                Assert.That(Primitive.FromOSD(osd).RenderMaterials, Is.Null);
            });
        }

        [Test]
        public void EmptyRenderMaterials_StayEmpty()
        {
            var reread = RoundTrip(new Primitive { RenderMaterials = new Dictionary<int, UUID>() });

            Assert.That(reread.RenderMaterials, Is.Not.Null.And.Empty);
        }

        [Test]
        public void GetOSD_WritesAMapPerFace_InFaceOrder()
        {
            var a = UUID.Random();
            var b = UUID.Random();
            var prim = new Primitive { RenderMaterials = new Dictionary<int, UUID> { [5] = b, [2] = a } };

            var array = (OSDArray)((OSDMap)prim.GetOSD())["render_material"];

            Assert.Multiple(() =>
            {
                Assert.That(array, Has.Count.EqualTo(2));
                Assert.That(((OSDMap)array[0])["te_idx"].AsInteger(), Is.EqualTo(2));
                Assert.That(((OSDMap)array[0])["id"].AsUUID(), Is.EqualTo(a));
                Assert.That(((OSDMap)array[1])["te_idx"].AsInteger(), Is.EqualTo(5));
                Assert.That(((OSDMap)array[1])["id"].AsUUID(), Is.EqualTo(b));
            });
        }

        [Test]
        public void FromOSD_SkipsEntriesWithoutBothFields()
        {
            var id = UUID.Random();
            var osd = (OSDMap)new Primitive().GetOSD();
            osd["render_material"] = new OSDArray
            {
                new OSDMap { ["te_idx"] = OSD.FromInteger(1), ["id"] = OSD.FromUUID(id) },
                new OSDMap { ["te_idx"] = OSD.FromInteger(2) },
                new OSDMap { ["id"] = OSD.FromUUID(UUID.Random()) },
                OSD.FromString("not a map")
            };

            var materials = Primitive.FromOSD(osd).RenderMaterials;

            Assert.That(materials, Is.EqualTo(new Dictionary<int, UUID> { [1] = id }));
        }
    }
}
