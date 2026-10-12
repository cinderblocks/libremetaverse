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
using System.Linq;
using System.Reflection;
using LibreMetaverse.Materials;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// The small utilities that moved out of the client: <see cref="CoordinateFrame"/> and <see cref="SlurlParser"/> are in
    /// <c>LibreMetaverse.Types</c>, <see cref="Sounds"/> and <see cref="LegacyMaterial"/> in <c>LibreMetaverse.Assets</c>.
    /// </summary>
    [TestFixture]
    [Category("Utilities")]
    public class MovedUtilityTests
    {
        private const float Tolerance = 1e-5f;

        private static void AssertVector(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), $"{actual} should be {expected}");

        #region CoordinateFrame

        [Test]
        public void ANewFrame_IsAtItsOriginWithTheWorldAxes()
        {
            var frame = new CoordinateFrame(new Vector3(1, 2, 3));

            Assert.Multiple(() =>
            {
                Assert.That(frame.Origin, Is.EqualTo(new Vector3(1, 2, 3)));
                Assert.That(frame.XAxis, Is.EqualTo(CoordinateFrame.X_AXIS));
                Assert.That(frame.YAxis, Is.EqualTo(CoordinateFrame.Y_AXIS));
                Assert.That(frame.ZAxis, Is.EqualTo(CoordinateFrame.Z_AXIS));
            });
        }

        [Test]
        public void ANonFiniteOriginOrAxis_IsRefused()
        {
            var nan = new Vector3(float.NaN, 0, 0);
            var frame = new CoordinateFrame(Vector3.Zero);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => _ = new CoordinateFrame(nan));
                Assert.Throws<ArgumentException>(() => _ = new CoordinateFrame(Vector3.Zero, nan, Vector3.UnitY, Vector3.UnitZ));
                Assert.Throws<ArgumentException>(() => frame.Origin = nan);
                Assert.Throws<ArgumentException>(() => frame.XAxis = nan);
                Assert.Throws<ArgumentException>(() => frame.YAxis = nan);
                Assert.Throws<ArgumentException>(() => frame.ZAxis = nan);
            });
        }

        [Test]
        public void TheFrameOfAQuaternion_HasTheAxesOfItsMatrix()
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(Math.PI / 2));
            var matrix = Matrix4.CreateFromQuaternion(rotation);

            var frame = new CoordinateFrame(Vector3.Zero, rotation);

            AssertVector(frame.XAxis, matrix.AtAxis);
            AssertVector(frame.YAxis, matrix.LeftAxis);
            AssertVector(frame.ZAxis, matrix.UpAxis);
        }

        [Test]
        public void Yaw_TurnsTheFrameAboutItsUpAxis_AndKeepsItOrthonormal()
        {
            var frame = new CoordinateFrame(Vector3.Zero);

            frame.Yaw((float)(Math.PI / 2));

            AssertVector(frame.ZAxis, Vector3.UnitZ);
            AssertVector(frame.XAxis, Vector3.UnitY);
            AssertVector(frame.YAxis, -Vector3.UnitX);
            Assert.Multiple(() =>
            {
                Assert.That(Vector3.Dot(frame.XAxis, frame.YAxis), Is.EqualTo(0f).Within(Tolerance));
                Assert.That(frame.XAxis.Length(), Is.EqualTo(1f).Within(Tolerance));
                Assert.That(frame.YAxis.Length(), Is.EqualTo(1f).Within(Tolerance));
            });
        }

        [Test]
        public void ATurnOfThirtyDegrees_StillMakesTheAxesOrthogonalAndTurnedByThatAngle()
        {
            float angle = (float)(Math.PI / 6);
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            var frame = new CoordinateFrame(Vector3.Zero);

            frame.Yaw(angle);

            AssertVector(frame.XAxis, new Vector3(c, s, 0));
            AssertVector(frame.YAxis, new Vector3(-s, c, 0));
            AssertVector(frame.ZAxis, Vector3.UnitZ);
        }

        [Test]
        public void ManySmallTurns_AddUpToOneFullTurn()
        {
            var frame = new CoordinateFrame(Vector3.Zero);

            for (int i = 0; i < 36; i++) frame.Yaw((float)(Math.PI / 18));

            AssertVector(frame.XAxis, Vector3.UnitX);
            AssertVector(frame.YAxis, Vector3.UnitY);
        }

        [Test]
        public void Rotating_RepairsAxesThatAreNotUnitLengthOrNotSquare()
        {
            var frame = new CoordinateFrame(Vector3.Zero)
            {
                XAxis = new Vector3(3, 0, 0),
                YAxis = new Vector3(1, 2, 0)
            };

            frame.Rotate(Matrix4.Identity);

            AssertVector(frame.XAxis, Vector3.UnitX);
            AssertVector(frame.YAxis, Vector3.UnitY);
            AssertVector(frame.ZAxis, Vector3.UnitZ);
        }

        [Test]
        public void ResetAxes_ReturnsToTheWorldAxes_AndLeavesTheOrigin()
        {
            var frame = new CoordinateFrame(new Vector3(5, 6, 7));
            frame.Yaw(1f);
            frame.Pitch(0.5f);

            frame.ResetAxes();

            Assert.Multiple(() =>
            {
                Assert.That(frame.Origin, Is.EqualTo(new Vector3(5, 6, 7)));
                Assert.That(frame.XAxis, Is.EqualTo(CoordinateFrame.X_AXIS));
                Assert.That(frame.YAxis, Is.EqualTo(CoordinateFrame.Y_AXIS));
                Assert.That(frame.ZAxis, Is.EqualTo(CoordinateFrame.Z_AXIS));
            });
        }

        [Test]
        public void LookAt_PointsTheFrameFromTheOriginToTheTarget()
        {
            var frame = new CoordinateFrame(Vector3.Zero);

            frame.LookAt(new Vector3(1, 1, 1), new Vector3(1, 5, 1));

            Assert.That(frame.Origin, Is.EqualTo(new Vector3(1, 1, 1)));
            AssertVector(frame.XAxis, Vector3.UnitY);
            AssertVector(frame.ZAxis, Vector3.UnitZ);
        }

        [Test]
        public void LookingStraightUp_DoesNotCollapseTheFrame()
        {
            var frame = new CoordinateFrame(Vector3.Zero, Vector3.UnitZ);

            Assert.Multiple(() =>
            {
                Assert.That(frame.XAxis.IsFinite() && frame.YAxis.IsFinite() && frame.ZAxis.IsFinite(), Is.True);
                Assert.That(frame.YAxis.Length(), Is.EqualTo(1f).Within(Tolerance), "the left axis was given a length");
            });
        }

        #endregion CoordinateFrame

        #region Sounds

        [Test]
        public void EveryNamedSound_IsInTheDictionaryUnderItsName()
        {
            var fields = typeof(Sounds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(UUID)).ToList();

            var dictionary = Sounds.ToDictionary();

            Assert.Multiple(() =>
            {
                Assert.That(fields, Is.Not.Empty);
                Assert.That(dictionary, Has.Count.EqualTo(fields.Count), "no two sounds share an id");
                foreach (var field in fields)
                    Assert.That(dictionary[(UUID)field.GetValue(null)!], Is.EqualTo(field.Name));
            });
        }

        [Test]
        public void ASound_IsTheIdTheWikiListsForIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Sounds.BELL_TING, Is.EqualTo(new UUID("ed124764-705d-d497-167a-182cd9fa2e6c")));
                Assert.That(Sounds.ZIPPER, Is.EqualTo(new UUID("6cf2be26-90cb-2669-a599-f5ab7698225f")));
            });
        }

        #endregion Sounds

        #region LegacyMaterial

        private static OSDMap MaterialMap(OSD id)
        {
            var material = new OSDMap
            {
                ["NormMap"] = OSD.FromUUID(new UUID("11111111-1111-1111-1111-111111111111")),
                ["NormOffsetX"] = 5000,
                ["NormOffsetY"] = -5000,
                ["NormRepeatX"] = 10000,
                ["NormRepeatY"] = 20000,
                ["NormRotation"] = 15708,
                ["SpecMap"] = OSD.FromUUID(new UUID("22222222-2222-2222-2222-222222222222")),
                ["SpecOffsetX"] = 1,
                ["SpecOffsetY"] = 2,
                ["SpecRepeatX"] = 3,
                ["SpecRepeatY"] = 4,
                ["SpecRotation"] = 5,
                ["SpecColor"] = OSD.FromColor4(new Color4(1f, 0.5f, 0.25f, 1f)),
                ["SpecExp"] = 51,
                ["EnvIntensity"] = 17,
                ["AlphaMaskCutoff"] = 90,
                ["DiffuseAlphaMode"] = 2
            };
            return new OSDMap { ["ID"] = id, ["Material"] = material };
        }

        [Test]
        public void AMaterial_ReadsItsFieldsAndScalesTheFixedPointOnes()
        {
            var id = new UUID("33333333-3333-3333-3333-333333333333");

            var material = new LegacyMaterial(MaterialMap(OSD.FromUUID(id)));

            Assert.Multiple(() =>
            {
                Assert.That(material.ID, Is.EqualTo(id));
                Assert.That(material.NormalMap, Is.EqualTo(new UUID("11111111-1111-1111-1111-111111111111")));
                Assert.That(material.NormalMapOffsetX, Is.EqualTo(0.5).Within(1e-9));
                Assert.That(material.NormalMapOffsetY, Is.EqualTo(-0.5).Within(1e-9));
                Assert.That(material.NormalMapRepeatX, Is.EqualTo(1.0).Within(1e-9));
                Assert.That(material.NormalMapRepeatY, Is.EqualTo(2.0).Within(1e-9));
                Assert.That(material.NormalMapRotation, Is.EqualTo(1.5708).Within(1e-9));
                Assert.That(material.SpecularMap, Is.EqualTo(new UUID("22222222-2222-2222-2222-222222222222")));
                Assert.That(material.SpecularMapOffsetX, Is.EqualTo(0.0001).Within(1e-9));
                Assert.That(material.SpecularMapRotation, Is.EqualTo(0.0005).Within(1e-9));
                Assert.That(material.SpecularExponent, Is.EqualTo((byte)51));
                Assert.That(material.EnvironmentIntensity, Is.EqualTo((byte)17));
                Assert.That(material.AlphaMaskCutoff, Is.EqualTo((byte)90));
                Assert.That(material.DiffuseAlphaMode, Is.EqualTo(LegacyMaterialAlphaMode.Mask));
            });
        }

        [Test]
        public void TheIdOfAMaterial_MayBeBinary_OrAnArray_OrAUuid()
        {
            var id = new UUID("44444444-4444-4444-4444-444444444444");
            byte[] bytes = id.GetBytes();
            var array = new OSDArray(bytes.Select(b => (OSD)(int)b).ToList());

            Assert.Multiple(() =>
            {
                Assert.That(new LegacyMaterial(MaterialMap(OSD.FromBinary(bytes))).ID, Is.EqualTo(id));
                Assert.That(new LegacyMaterial(MaterialMap(array)).ID, Is.EqualTo(id));
                Assert.That(new LegacyMaterial(MaterialMap(OSD.FromUUID(id))).ID, Is.EqualTo(id));
            });
        }

        [Test]
        public void AMaterialWithoutAnIdOrBody_IsRefused()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<InvalidOperationException>(() => _ = new LegacyMaterial(new OSDMap { ["Material"] = new OSDMap() }));
                Assert.Throws<InvalidOperationException>(() => _ = new LegacyMaterial(new OSDMap { ["ID"] = OSD.FromUUID(UUID.Random()) }));
                Assert.Throws<InvalidOperationException>(() => _ = new LegacyMaterial(MaterialMap(OSD.FromString("not an id"))));
            });
        }

        #endregion LegacyMaterial
    }
}
