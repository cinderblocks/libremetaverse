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
    /// The extended mesh extra parameter (PARAMS_EXTENDED_MESH, 0x70): a single U32 of flags
    /// (LLExtendedMeshParams::pack), bit 0 being ANIMATED_MESH_ENABLED_FLAG.
    /// </summary>
    [TestFixture]
    public class ExtendedMeshParamTests
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

        /// <summary>The type and data of each parameter in an encoded extra params block</summary>
        private static List<(ushort type, byte[] data)> Parameters(byte[] bytes)
        {
            var parameters = new List<(ushort, byte[])>();
            var pos = 1;
            for (var k = 0; k < bytes[0]; k++)
            {
                var type = Utils.BytesToUInt16(bytes, pos);
                var length = (int)Utils.BytesToUInt(bytes, pos + 2);
                var data = new byte[length];
                System.Array.Copy(bytes, pos + 6, data, 0, length);
                parameters.Add((type, data));
                pos += 6 + length;
            }
            Assert.That(pos, Is.EqualTo(bytes.Length));
            return parameters;
        }

        private static Primitive Reread(Primitive prim)
        {
            var reread = new Primitive();
            var bytes = prim.GetExtraParamsBytes();
            Assert.That(reread.SetExtraParamsFromBytes(bytes, 0), Is.EqualTo(bytes.Length));
            return reread;
        }

        [Test]
        public void RoundTrip_OfAnAnimatedObject_IsEncodedExactlyAsReceived()
        {
            var wire = ExtraParams(((ushort)ExtraParamType.ExtendedMesh, Utils.UIntToBytes(1u)));

            var decoded = new Primitive();
            decoded.SetExtraParamsFromBytes(wire, 0);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.IsAnimatedObject, Is.True);
                Assert.That(Parameters(decoded.GetExtraParamsBytes()),
                    Has.Exactly(1).EqualTo(((ushort)ExtraParamType.ExtendedMesh, Utils.UIntToBytes(1u))),
                    "encoded exactly as received");
                Assert.That(Reread(decoded).IsAnimatedObject, Is.True);
            });
        }

        [Test]
        public void Encode_WritesTheFlagsAsALittleEndianU32()
        {
            var bytes = new Primitive { ExtendedMeshFlags = 0x01020304u }.GetExtraParamsBytes();

            Assert.That(Parameters(bytes), Has.Exactly(1).EqualTo(
                ((ushort)0x70, new byte[] { 0x04, 0x03, 0x02, 0x01 })));
        }

        [Test]
        public void RoundTrip_KeepsEveryFlagBit()
        {
            var reread = Reread(new Primitive { ExtendedMeshFlags = 0xDEADBEEFu });

            Assert.That(reread.ExtendedMeshFlags, Is.EqualTo(0xDEADBEEFu));
        }

        [Test]
        public void NoFlags_AddsNoParameter()
        {
            Assert.That(Parameters(new Primitive().GetExtraParamsBytes()).ConvertAll(p => p.type),
                Has.No.Member((ushort)ExtraParamType.ExtendedMesh));
            Assert.That(Reread(new Primitive()).IsAnimatedObject, Is.False);
        }

        [Test]
        public void RoundTrip_AlongsideOtherParameters_KeepsThemAll()
        {
            var sculptTexture = UUID.Random();
            var material = UUID.Random();
            var prim = new Primitive
            {
                ExtendedMeshFlags = 1u,
                Light = new Primitive.LightData { Radius = 5f, Intensity = 0.5f },
                Sculpt = new Primitive.SculptData { SculptTexture = sculptTexture, Type = SculptType.Mesh },
                RenderMaterials = new Dictionary<int, UUID> { [2] = material }
            };

            var reread = Reread(prim);

            Assert.Multiple(() =>
            {
                Assert.That(reread.IsAnimatedObject, Is.True);
                Assert.That(reread.Light, Is.Not.Null);
                Assert.That(reread.Sculpt, Is.Not.Null);
                Assert.That(reread.Sculpt!.Type, Is.EqualTo(SculptType.Mesh));
                Assert.That(reread.Sculpt!.SculptTexture, Is.EqualTo(sculptTexture));
                Assert.That(reread.RenderMaterials, Is.EqualTo(prim.RenderMaterials));
            });
        }
    }
}
