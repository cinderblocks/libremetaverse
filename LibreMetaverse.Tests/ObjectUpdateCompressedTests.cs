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

using System;
using System.Collections.Generic;
using System.Reflection;
using LibreMetaverse.Packets;
using LibreMetaverse.Tests.TestHelpers;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Decoding of <see cref="ObjectUpdateCompressedPacket"/> blocks. The compressed form carries a particle
    /// system as the 86-byte legacy block (LLViewerObject::processUpdateMessage, OUT_FULL_COMPRESSED) with the
    /// extra parameters, name values, shape and texture entry after it.
    /// </summary>
    [TestFixture]
    public class ObjectUpdateCompressedTests
    {
        private FakeGridClient _client = null!;
        private Simulator _sim = null!;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeGridClient();
            _client.Settings.World.AlwaysDecodeObjects = true;
            _client.AddCapability("Placeholder", new Uri("http://test.invalid/placeholder"));
            _sim = _client.Network.CurrentSim;
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private static Primitive.ParticleSystem SampleParticleSystem() => new Primitive.ParticleSystem
        {
            Pattern = Primitive.ParticleSystem.SourcePattern.Drop,
            MaxAge = 0f,
            BurstRate = 0.25f,
            BurstPartCount = 3,
            BurstRadius = 1f,
            PartMaxAge = 4f,
            PartStartColor = new Color4(1f, 1f, 1f, 1f),
            PartEndColor = new Color4(1f, 1f, 1f, 1f),
            PartStartScaleX = 0.25f,
            PartStartScaleY = 0.25f,
            PartEndScaleX = 0.25f,
            PartEndScaleY = 0.25f,
            PartDataFlags = Primitive.ParticleSystem.ParticleDataFlags.Ribbon
                            | Primitive.ParticleSystem.ParticleDataFlags.TargetLinear,
            Texture = UUID.Parse("11111111-2222-3333-4444-555555555555"),
            Target = UUID.Parse("66666666-7777-8888-9999-000000000000"),
            // Default glow and blend functions, so the system has the legacy (86-byte) form
            BlendFuncSource = (byte)Primitive.ParticleSystem.BlendFunc.SourceAlpha,
            BlendFuncDest = (byte)Primitive.ParticleSystem.BlendFunc.OneMinusSourceAlpha,
        };

        /// <summary>A minimal compressed object block: a box with a legacy particle system and a texture entry.</summary>
        private static byte[] BuildCompressedBlock(UUID id, uint localId, byte[] particles, UUID textureId)
        {
            var data = new List<byte>();
            data.AddRange(id.GetBytes());
            data.AddRange(Utils.UIntToBytes(localId));
            data.Add((byte)PCode.Prim);
            data.Add(0);                                        // state
            data.AddRange(new byte[4]);                         // CRC
            data.Add((byte)Material.Wood);
            data.Add((byte)ClickAction.Touch);
            data.AddRange(new Vector3(1f, 1f, 1f).GetBytes());  // scale
            data.AddRange(new Vector3(128f, 128f, 25f).GetBytes());
            data.AddRange(Quaternion.Identity.GetBytes());
            data.AddRange(Utils.UIntToBytes((uint)CompressedFlags.HasParticles));
            data.AddRange(UUID.Random().GetBytes());           // owner
            data.AddRange(particles);
            data.Add(0);                                        // extra params: none
            data.AddRange(new byte[1 + 2 + 2 + 11]);            // path
            data.AddRange(new byte[1 + 2 + 2 + 2]);             // profile
            var te = new Primitive.TextureEntry(textureId).GetBytes();
            data.AddRange(Utils.UIntToBytes((uint)te.Length));
            data.AddRange(te);
            return data.ToArray();
        }

        private void Deliver(byte[] block)
        {
            var packet = new ObjectUpdateCompressedPacket
            {
                RegionData = { RegionHandle = _sim.Handle, TimeDilation = ushort.MaxValue },
                ObjectData = new[] { new ObjectUpdateCompressedPacket.ObjectDataBlock { UpdateFlags = 0, Data = block } }
            };
            var method = typeof(ObjectManager).GetMethod("ObjectUpdateCompressedHandler",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(_client.Objects, new object?[] { null, new PacketReceivedEventArgs(packet, _sim) });
        }

        [Test]
        public void LegacyParticleSystem_FollowedByObjectData_IsDecoded()
        {
            var sent = SampleParticleSystem();
            var bytes = sent.GetBytes();
            Assert.That(bytes, Has.Length.EqualTo(Primitive.ParticleSystem.LegacyDataBlockSize), "test precondition");

            var textureId = UUID.Random();
            Deliver(BuildCompressedBlock(UUID.Random(), 4242u, bytes, textureId));

            Assert.That(_sim.ObjectsPrimitives.TryGetValue(4242u, out var prim), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(prim!.ParticleSys.Pattern, Is.EqualTo(Primitive.ParticleSystem.SourcePattern.Drop));
                Assert.That(prim.ParticleSys.BurstPartCount, Is.EqualTo(3));
                Assert.That(prim.ParticleSys.PartMaxAge, Is.EqualTo(4f).Within(0.01f));
                Assert.That(prim.ParticleSys.PartDataFlags, Is.EqualTo(sent.PartDataFlags));
                Assert.That(prim.ParticleSys.Texture, Is.EqualTo(sent.Texture));
                Assert.That(prim.ParticleSys.Target, Is.EqualTo(sent.Target));
                Assert.That(prim.Textures?.DefaultTexture?.TextureID, Is.EqualTo(textureId),
                    "the data after the particle block is still read from the right offset");
            });
        }

        [Test]
        public void ParticleSystem_WithExplicitLength_IgnoresTrailingData()
        {
            var bytes = SampleParticleSystem().GetBytes();
            Assert.That(bytes, Has.Length.EqualTo(Primitive.ParticleSystem.LegacyDataBlockSize), "test precondition");
            var buffer = new byte[5 + bytes.Length + 40];
            Buffer.BlockCopy(bytes, 0, buffer, 5, bytes.Length);
            for (var i = 5 + bytes.Length; i < buffer.Length; i++) buffer[i] = 0xAB;

            var withLength = new Primitive.ParticleSystem(buffer, 5, Primitive.ParticleSystem.LegacyDataBlockSize);
            var inferred = new Primitive.ParticleSystem(buffer, 5);

            Assert.Multiple(() =>
            {
                Assert.That(withLength.BurstPartCount, Is.EqualTo(3));
                Assert.That(withLength.Texture, Is.EqualTo(UUID.Parse("11111111-2222-3333-4444-555555555555")));
                Assert.That(inferred.BurstPartCount, Is.EqualTo(0),
                    "without a length the trailing bytes are taken as part of the block, which is not a valid size");
            });
        }
    }
}
