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
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The render material extra parameter (PARAMS_RENDER_MATERIAL, 0x80): a U8 count, then for each
    /// entry a U8 face index and the material asset UUID (LLRenderMaterialParams::unpack).
    /// </summary>
    [TestFixture]
    public class RenderMaterialParamTests
    {
        private static byte[] ExtraParams(params (ushort type, byte[] data)[] parameters)
        {
            var bytes = new List<byte> { (byte)parameters.Length };
            foreach (var (type, data) in parameters)
            {
                bytes.AddRange(Utils.UInt16ToBytes(type));
                bytes.AddRange(Utils.UIntToBytes((uint)data.Length));
                bytes.AddRange(data);
            }
            return bytes.ToArray();
        }

        private static byte[] RenderMaterialParam(params (byte face, UUID id)[] entries)
        {
            var bytes = new List<byte> { (byte)entries.Length };
            foreach (var (face, id) in entries)
            {
                bytes.Add(face);
                bytes.AddRange(id.GetBytes());
            }
            return bytes.ToArray();
        }

        [Test]
        public void RenderMaterialParameter_GivesTheMaterialOfEachFace()
        {
            var a = UUID.Random();
            var b = UUID.Random();
            var data = ExtraParams(
                ((ushort)ExtraParamType.RenderMaterial, RenderMaterialParam((0, a), (3, b))),
                ((ushort)ExtraParamType.ExtendedMesh, Utils.UIntToBytes(1u)));

            var prim = new Primitive();
            var read = prim.SetExtraParamsFromBytes(data, 0);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(data.Length));
                Assert.That(prim.RenderMaterials, Is.Not.Null);
                Assert.That(prim.RenderMaterials!, Has.Count.EqualTo(2));
                Assert.That(prim.RenderMaterials![0], Is.EqualTo(a));
                Assert.That(prim.RenderMaterials![3], Is.EqualTo(b));
                Assert.That(prim.IsAnimatedObject, Is.True, "the parameter after it is still read");
            });
        }

        [Test]
        public void NoRenderMaterialParameter_LeavesRenderMaterialsNull()
        {
            var prim = new Primitive();
            prim.SetExtraParamsFromBytes(ExtraParams(((ushort)ExtraParamType.ExtendedMesh, Utils.UIntToBytes(0u))), 0);

            Assert.That(prim.RenderMaterials, Is.Null);
        }

        [Test]
        public void CountLargerThanTheParameter_ReadsOnlyTheEntriesThatFit()
        {
            var a = UUID.Random();
            var param = RenderMaterialParam((2, a));
            param[0] = 5; // claims five entries, holds one

            var materials = Primitive.ParseRenderMaterials(param, 0, param.Length);

            Assert.That(materials, Has.Count.EqualTo(1));
            Assert.That(materials[2], Is.EqualTo(a));
        }

        [Test]
        public void EmptyParameter_GivesNoMaterials()
        {
            Assert.That(Primitive.ParseRenderMaterials(new byte[] { 0 }, 0, 1), Is.Empty);
            Assert.That(Primitive.ParseRenderMaterials(new byte[0], 0, 0), Is.Empty);
        }

        private static Dictionary<int, UUID>? DecodeEncodeDecode(byte[] wire, out Dictionary<int, UUID>? first)
        {
            var decoded = new Primitive();
            decoded.SetExtraParamsFromBytes(wire, 0);
            first = decoded.RenderMaterials;

            var reread = new Primitive();
            reread.SetExtraParamsFromBytes(decoded.GetExtraParamsBytes(), 0);
            return reread.RenderMaterials;
        }

        [Test]
        public void RoundTrip_KeepsTheMaterialOfEachFace()
        {
            var wire = ExtraParams(((ushort)ExtraParamType.RenderMaterial,
                RenderMaterialParam((0, UUID.Random()), (3, UUID.Random()), (7, UUID.Random()))));

            var again = DecodeEncodeDecode(wire, out var first);

            Assert.That(first, Has.Count.EqualTo(3));
            Assert.That(again, Is.EqualTo(first));
        }

        [Test]
        public void RoundTrip_OfAnEmptyParameter_KeepsItEmpty()
        {
            var wire = ExtraParams(((ushort)ExtraParamType.RenderMaterial, RenderMaterialParam()));

            var again = DecodeEncodeDecode(wire, out var first);

            Assert.That(first, Is.Not.Null.And.Empty);
            Assert.That(again, Is.Not.Null.And.Empty);
        }

        [Test]
        public void RoundTrip_OfTheMaximumCount_KeepsEveryEntry()
        {
            var entries = new (byte face, UUID id)[Primitive.MaxRenderMaterialEntries];
            for (var i = 0; i < entries.Length; i++) entries[i] = ((byte)(i * 3), UUID.Random());
            var param = RenderMaterialParam(entries);
            var wire = ExtraParams(((ushort)ExtraParamType.RenderMaterial, param));

            var again = DecodeEncodeDecode(wire, out var first);

            Assert.Multiple(() =>
            {
                Assert.That(param.Length, Is.LessThanOrEqualTo(255), "the viewer's cap keeps the parameter under 255 bytes");
                Assert.That(first, Has.Count.EqualTo(Primitive.MaxRenderMaterialEntries));
                Assert.That(again, Is.EqualTo(first));
                Assert.That(Primitive.GetRenderMaterialsBytes(first!), Is.EqualTo(param), "encoded exactly as received");
            });
        }

        [Test]
        public void RoundTrip_AlongsideOtherParameters_KeepsThemAll()
        {
            var prim = new Primitive
            {
                Light = new Primitive.LightData { Radius = 5f, Intensity = 0.5f },
                RenderMaterials = new Dictionary<int, UUID> { [1] = UUID.Random(), [4] = UUID.Random() }
            };

            var reread = new Primitive();
            var bytes = prim.GetExtraParamsBytes();
            var read = reread.SetExtraParamsFromBytes(bytes, 0);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(bytes.Length));
                Assert.That(reread.Light, Is.Not.Null);
                Assert.That(reread.RenderMaterials, Is.EqualTo(prim.RenderMaterials));
            });
        }

        [Test]
        public void Encode_WritesFacesInOrder_AndNoMoreThanTheMaximum()
        {
            var materials = new Dictionary<int, UUID>();
            for (var face = 19; face >= 0; face--) materials[face] = UUID.Random();
            materials[300] = UUID.Random(); // not a face index the parameter can hold

            var decoded = Primitive.ParseRenderMaterials(Primitive.GetRenderMaterialsBytes(materials), 0,
                Primitive.GetRenderMaterialsBytes(materials).Length);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.Keys, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }));
                foreach (var face in decoded.Keys) Assert.That(decoded[face], Is.EqualTo(materials[face]));
            });
        }

        [Test]
        public void NoRenderMaterials_AddsNoParameter()
        {
            var reread = new Primitive();
            reread.SetExtraParamsFromBytes(new Primitive { Light = new Primitive.LightData() }.GetExtraParamsBytes(), 0);

            Assert.That(reread.RenderMaterials, Is.Null);
        }
    }
}
