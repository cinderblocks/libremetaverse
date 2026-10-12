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
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The particle system of a prim is kept in region archives as the bytes the simulator sends to viewers, in base64.
    /// The blobs here were written by OpenSim, from the CC0 archives described in LibreMetaverse.Archiving.Tests/OarTestData.
    /// </summary>
    [TestFixture]
    public class ParticleSystemXmlTests
    {
        private static readonly string[] OpenSimBlobs =
        {
            "AQAAAAAAAAACAAAAABQDBwACAAAABwAQAIAAgACAAIAAgAyAAAAAAAAAAAAAAAAAAAAAAC/ArBThh0kdpnhX5KT7ZwAjAQAAzACysrL/srKyAAAAAQE=",
            "AQAAAAAAAAACAAAAABQDBwACAAAABwAQAIAAgACAAIAAgAyAAAAAAAAAAAAAAAAAAAAAAFOK/u1yqEGSmzT2R8kn6mAjAQAAzACysrL/srKyAAAAAQE=",
            "AQAAAAAAAAACAAAAABQDBwACAAAABwAQAIAAgACAAIAAgAyAAAAAAAAAAAAAAAAAAAAAAKpXTTPTx0qlgYllA4opCpIjAQAAzACysrL/srKyAAAAAQE=",
            "AQAAAAAAAAACAAAAABQDBwACAAAABwAQAIAAgACAAIAAgAyAAAAAAAAAAAAAAAAAAAAAALsv0czioEcmmmqF/oTKFQ4jAQAAzACysrL/srKyAAAAAQE=",
            "AQAAAAIAAAAEAAAAAAAAEQAAAAAAZgAyAIAAgACAAIAAgAKAAAAAAAAAAAAAAAAAAAAAAIADv2lAfkCqj4g5f9mhLmYTAQAATAD//0wMzJmZAAEBAAA=",
            "AQAAAAIAAAAIAAAAAAEAAgAAAAwAEQAFAIAAgACAAIAAgECAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAvAQAAZgD//wBMZgAAAAAAAQE="
        };

        private static string Group(string particleSystemElement) =>
            "<SceneObjectGroup><SceneObjectPart><UUID><UUID>11111111-1111-1111-1111-111111111111</UUID></UUID>" + particleSystemElement + "</SceneObjectPart></SceneObjectGroup>";

        private static PrimObject Read(string particleSystemElement)
        {
            var prim = new AssetPrim();
            Assert.That(prim.DecodeXml(Group(particleSystemElement)), Is.True);
            return prim.Parent;
        }

        private static string WrittenParticleSystem(PrimObject prim)
        {
            var xml = new AssetPrim(prim, new List<PrimObject>()).EncodeXml();
            return XDocument.Parse(xml).Descendants("ParticleSystem").Single().Value;
        }

        [TestCaseSource(nameof(OpenSimBlobs))]
        public void AParticleSystemOpenSimWrote_IsWrittenBackByteForByte(string blob)
        {
            var prim = Read("<ParticleSystem>" + blob + "</ParticleSystem>");

            Assert.That(prim.Particles.IsSet, Is.True);
            Assert.That(WrittenParticleSystem(prim), Is.EqualTo(blob));
        }

        [Test]
        public void AParticleSystemOpenSimWrote_IsRead()
        {
            // A candle flame: pattern Angle, 50 particles per burst, yellow fading to transparent
            var p = Read("<ParticleSystem>" + OpenSimBlobs[4] + "</ParticleSystem>").Particles;

            Assert.Multiple(() =>
            {
                Assert.That(p.Crc, Is.EqualTo(1u));
                Assert.That(p.Pattern, Is.EqualTo((int)Primitive.ParticleSystem.SourcePattern.Angle));
                Assert.That(p.Flags, Is.EqualTo(2), "UseNewAngle");
                Assert.That(p.BurstParticleCount, Is.EqualTo(50));
                Assert.That(p.BurstRate, Is.EqualTo(0.06640625f));
                Assert.That(p.DataFlags, Is.EqualTo((int)(Primitive.ParticleSystem.ParticleDataFlags.InterpColor | Primitive.ParticleSystem.ParticleDataFlags.InterpScale
                                                          | Primitive.ParticleSystem.ParticleDataFlags.FollowSrc | Primitive.ParticleSystem.ParticleDataFlags.Emissive)));
                Assert.That(p.ParticleStartColor.R, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(p.ParticleStartColor.B, Is.EqualTo(76f / 255f).Within(1e-6f));
                Assert.That(p.ParticleStartColor.A, Is.EqualTo(12f / 255f).Within(1e-6f));
                Assert.That(p.TextureID, Is.EqualTo(UUID.Zero));
            });
        }

        [Test]
        public void ATargetIsRead()
        {
            // The first of the blobs has no texture and has a target
            var p = Read("<ParticleSystem>" + OpenSimBlobs[0] + "</ParticleSystem>").Particles;

            Assert.Multiple(() =>
            {
                Assert.That(p.TextureID, Is.EqualTo(UUID.Zero));
                Assert.That(p.TargetID, Is.EqualTo(UUID.Parse("2fc0ac14-e187-491d-a678-57e4a4fb6700")));
                Assert.That(p.Pattern, Is.EqualTo((int)Primitive.ParticleSystem.SourcePattern.Explode));
            });
        }

        [Test]
        public void AnEmptyElement_IsAPrimWithNoParticles_AndSoIsOneWrittenWithNone()
        {
            var read = Read("<ParticleSystem></ParticleSystem>");

            Assert.Multiple(() =>
            {
                Assert.That(read.Particles.IsSet, Is.False);
                Assert.That(WrittenParticleSystem(new PrimObject { ID = UUID.Random() }), Is.EqualTo(string.Empty));
                Assert.That(WrittenParticleSystem(read), Is.EqualTo(string.Empty));
            });
        }

        [TestCase("not base64 !!")]
        [TestCase("AAAA")]
        public void AParticleSystemThatCannotBeRead_IsLeftOut(string content)
        {
            Assert.That(Read("<ParticleSystem>" + content + "</ParticleSystem>").Particles.IsSet, Is.False);
        }

        [Test]
        public void GlowAndBlendFunctions_SurviveTheArchive()
        {
            var system = new Primitive.ParticleSystem
            {
                Pattern = Primitive.ParticleSystem.SourcePattern.Explode, BurstPartCount = 4, MaxAge = 2f, PartMaxAge = 3f,
                PartStartColor = new Color4(1f, 0f, 0f, 1f), PartEndColor = new Color4(0f, 0f, 1f, 0f),
                PartStartGlow = 0.5f, PartEndGlow = 1f,
                BlendFuncSource = (byte)Primitive.ParticleSystem.BlendFunc.One, BlendFuncDest = (byte)Primitive.ParticleSystem.BlendFunc.OneMinusSourceColor
            };
            var prim = new PrimObject { ID = UUID.Random(), Particles = PrimObject.ParticlesBlock.FromParticleSystem(system) };

            var blob = Convert.FromBase64String(WrittenParticleSystem(prim));
            var written = new Primitive.ParticleSystem(blob, 0);
            var back = Read("<ParticleSystem>" + Convert.ToBase64String(blob) + "</ParticleSystem>").Particles;

            Assert.Multiple(() =>
            {
                Assert.That(blob, Has.Length.GreaterThan(Primitive.ParticleSystem.LegacyDataBlockSize), "the form with the extra data");
                Assert.That(written.PartStartGlow, Is.EqualTo(0.5f).Within(1f / 255f));
                Assert.That(written.PartEndGlow, Is.EqualTo(1f).Within(1f / 255f));
                Assert.That(written.BlendFuncSource, Is.EqualTo((byte)Primitive.ParticleSystem.BlendFunc.One));
                Assert.That(written.BlendFuncDest, Is.EqualTo((byte)Primitive.ParticleSystem.BlendFunc.OneMinusSourceColor));
                Assert.That(back.ParticleStartGlow, Is.EqualTo(written.PartStartGlow));
                Assert.That(back.BlendFuncDest, Is.EqualTo((int)Primitive.ParticleSystem.BlendFunc.OneMinusSourceColor));
                Assert.That(back.ParticleMaxAge, Is.EqualTo(3f));
                Assert.That(back.MaxAge, Is.EqualTo(2f));
            });
        }

        private static PrimObject.ParticlesBlock EveryField() => new PrimObject.ParticlesBlock
        {
            Crc = 77, Flags = 3, Pattern = (int)Primitive.ParticleSystem.SourcePattern.AngleCone, MaxAge = 7f, StartAge = 1.5f,
            InnerAngle = 0.5f, OuterAngle = 1.25f, BurstRate = 0.5f, BurstRadius = 3f, BurstSpeedMin = 1f, BurstSpeedMax = 2f,
            BurstParticleCount = 9, AngularVelocity = new Vector3(1f, -1f, 0.5f), Acceleration = new Vector3(0f, 0f, -0.5f),
            TextureID = UUID.Parse("22222222-2222-2222-2222-222222222222"), TargetID = UUID.Parse("33333333-3333-3333-3333-333333333333"),
            DataFlags = (int)(Primitive.ParticleSystem.ParticleDataFlags.InterpColor | Primitive.ParticleSystem.ParticleDataFlags.Wind),
            ParticleMaxAge = 5f, ParticleStartColor = new Color4(1f, 0.5f, 0f, 1f), ParticleEndColor = new Color4(0f, 0f, 0f, 0f),
            ParticleStartScale = new Vector2(0.5f, 1f), ParticleEndScale = new Vector2(2f, 3f), ParticleStartGlow = 0f, ParticleEndGlow = 0f
        };

        [Test]
        public void TheBlockAndTheParticleSystem_ConvertEachFieldToItsOwnField()
        {
            // Burst radius was never copied, and the age of the source and the age of a particle were taken for each other
            var system = EveryField().ToParticleSystem();

            Assert.Multiple(() =>
            {
                Assert.That(system.MaxAge, Is.EqualTo(7f), "of the source");
                Assert.That(system.PartMaxAge, Is.EqualTo(5f), "of a particle");
                Assert.That(system.BurstRate, Is.EqualTo(0.5f));
                Assert.That(system.BurstRadius, Is.EqualTo(3f));
                Assert.That(system.BurstPartCount, Is.EqualTo(9));
                Assert.That(system.InnerAngle, Is.EqualTo(0.5f));
                Assert.That(system.OuterAngle, Is.EqualTo(1.25f));
                Assert.That(system.PartStartScaleY, Is.EqualTo(1f));
                Assert.That(system.PartEndScaleX, Is.EqualTo(2f));
                Assert.That(system.Texture, Is.EqualTo(UUID.Parse("22222222-2222-2222-2222-222222222222")));
                Assert.That(system.Target, Is.EqualTo(UUID.Parse("33333333-3333-3333-3333-333333333333")));
                Assert.That(system.BlendFuncSource, Is.EqualTo((byte)Primitive.ParticleSystem.BlendFunc.SourceAlpha));
            });

            var back = PrimObject.ParticlesBlock.FromParticleSystem(system);
            Assert.Multiple(() =>
            {
                Assert.That(back.MaxAge, Is.EqualTo(7f));
                Assert.That(back.ParticleMaxAge, Is.EqualTo(5f));
                Assert.That(back.BurstRadius, Is.EqualTo(3f));
                Assert.That(back.BurstRate, Is.EqualTo(0.5f));
                Assert.That(back.Crc, Is.EqualTo(77u));
                Assert.That(back.DataFlags, Is.EqualTo(EveryField().DataFlags));
            });
        }

        [Test]
        public void APrimitiveConvertedToAPrimObjectAndBack_KeepsItsParticleSystem()
        {
            var prim = new Primitive { ParticleSys = EveryField().ToParticleSystem(), Scale = Vector3.One };

            var back = PrimObject.FromPrimitive(prim).ToPrimitive().ParticleSys;

            Assert.Multiple(() =>
            {
                Assert.That(back.MaxAge, Is.EqualTo(7f));
                Assert.That(back.PartMaxAge, Is.EqualTo(5f));
                Assert.That(back.BurstRadius, Is.EqualTo(3f));
                Assert.That(back.BurstRate, Is.EqualTo(0.5f));
                Assert.That(back.GetBytes(), Is.EqualTo(prim.ParticleSys.GetBytes()));
            });
        }

        [Test]
        public void TheBlockSerializesWithItsNewFields_AndReadsWhatWasSerializedWithout()
        {
            var block = EveryField();
            block.ParticleStartGlow = 0.25f;
            block.BlendFuncDest = 4;

            var back = new PrimObject.ParticlesBlock();
            back.Deserialize(block.Serialize());

            var old = block.Serialize();
            foreach (var key in new[] { "crc", "particle_start_glow", "particle_end_glow", "blend_func_source", "blend_func_dest" })
                old.Remove(key);
            var fromOld = new PrimObject.ParticlesBlock();
            fromOld.Deserialize(old);

            Assert.Multiple(() =>
            {
                Assert.That(back.Crc, Is.EqualTo(77u));
                Assert.That(back.ParticleStartGlow, Is.EqualTo(0.25f));
                Assert.That(back.BlendFuncDest, Is.EqualTo(4));
                Assert.That(back.BurstRadius, Is.EqualTo(3f));
                Assert.That(fromOld.BlendFuncSource, Is.EqualTo((int)Primitive.ParticleSystem.BlendFunc.SourceAlpha), "the default, not zero");
                Assert.That(fromOld.BlendFuncDest, Is.EqualTo((int)Primitive.ParticleSystem.BlendFunc.OneMinusSourceAlpha));
                Assert.That(fromOld.Crc, Is.EqualTo(0u));
            });
        }

        [Test]
        public void IsSet_IsFalseForTheBlockOfAPrimWithNone()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new PrimObject.ParticlesBlock().IsSet, Is.False);
                Assert.That(new PrimObject.ParticlesBlock { Pattern = 1 }.IsSet, Is.True);
                Assert.That(new PrimObject.ParticlesBlock { TextureID = UUID.Random() }.IsSet, Is.True);
            });
        }
    }
}
