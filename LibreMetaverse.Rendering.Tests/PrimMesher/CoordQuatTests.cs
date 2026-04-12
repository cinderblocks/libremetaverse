using System;
using NUnit.Framework;
using LibreMetaverse.PrimMesher;

namespace LibreMetaverse.Rendering.Tests.PrimMesher
{
    [TestFixture]
    public class CoordTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Coord_Length_KnownVector()
        {
            var c = new Coord(3f, 4f, 0f);
            Assert.That(c.Length(), Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void Coord_Length_UnitVector()
        {
            var c = new Coord(1f, 0f, 0f);
            Assert.That(c.Length(), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Coord_Normalize_GivesUnitLength()
        {
            var c = new Coord(3f, 4f, 5f);
            var n = c.Normalize();
            Assert.That(n.Length(), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Coord_Normalize_ZeroVector_DoesNotThrow()
        {
            var c = new Coord(0f, 0f, 0f);
            Assert.DoesNotThrow(() => c.Normalize());
        }

        [Test]
        public void Coord_Normalize_ZeroVector_GivesZero()
        {
            var c = new Coord(0f, 0f, 0f);
            var n = c.Normalize();
            Assert.That(n.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(n.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(n.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Coord_Cross_XaxisTimesYaxis_GivesZaxis()
        {
            var x = new Coord(1f, 0f, 0f);
            var y = new Coord(0f, 1f, 0f);
            var z = Coord.Cross(x, y);
            Assert.That(z.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(z.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(z.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Coord_Cross_YaxisTimesXaxis_GivesNegZaxis()
        {
            var x = new Coord(1f, 0f, 0f);
            var y = new Coord(0f, 1f, 0f);
            var z = Coord.Cross(y, x);
            Assert.That(z.Z, Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void Coord_Add_IsCommutative()
        {
            var a = new Coord(1f, 2f, 3f);
            var b = new Coord(4f, 5f, 6f);
            var ab = a + b;
            var ba = b + a;
            Assert.That(ab.X, Is.EqualTo(ba.X).Within(Tolerance));
            Assert.That(ab.Y, Is.EqualTo(ba.Y).Within(Tolerance));
            Assert.That(ab.Z, Is.EqualTo(ba.Z).Within(Tolerance));
        }
    }

    [TestFixture]
    public class QuatTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Quat_AxisAngle_IsNormalized()
        {
            var q = new Quat(new Coord(0f, 0f, 1f), MathF.PI / 2f);
            var len = q.Length();
            Assert.That(len, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Quat_Identity_RotatesCoordUnchanged()
        {
            // Identity quaternion: (0,0,0,1)
            var q = new Quat(0f, 0f, 0f, 1f);
            var v = new Coord(1f, 2f, 3f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(v.X).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(v.Y).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(v.Z).Within(Tolerance));
        }

        [Test]
        public void Coord_RotateByQuat_90DegAroundXaxis_YbecomesZ()
        {
            // (0,1,0) rotated 90° around X-axis should give (0,0,1)
            var q = new Quat(new Coord(1f, 0f, 0f), MathF.PI / 2f);
            var v = new Coord(0f, 1f, 0f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(1f).Within(Tolerance));
        }

        /// <summary>
        /// Regression test for the Z-component quaternion rotation bug where
        /// the term "- q.X*q.X*v.Z" was missing from the Coord*Quat operator,
        /// causing the Z output to be incorrect when q.X != 0.
        /// Before the fix: (0,0,1) × q(90°,X) incorrectly returned Z ≈ 0.5.
        /// After the fix: Z must be 0 (Y must be -1).
        /// </summary>
        [Test]
        public void Coord_RotateByQuat_90DegAroundXaxis_ZbecomesNegY_BugRegression()
        {
            // (0,0,1) rotated 90° around X-axis should give (0,-1,0)
            var q = new Quat(new Coord(1f, 0f, 0f), MathF.PI / 2f);
            var v = new Coord(0f, 0f, 1f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Tolerance), "Z was non-zero before the q.X*q.X*v.Z term was restored");
        }

        [Test]
        public void Coord_RotateByQuat_90DegAroundZaxis_XbecomesY()
        {
            // (1,0,0) rotated 90° around Z-axis should give (0,1,0)
            var q = new Quat(new Coord(0f, 0f, 1f), MathF.PI / 2f);
            var v = new Coord(1f, 0f, 0f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Coord_RotateByQuat_90DegAroundYaxis_ZbecomesX()
        {
            // (0,0,1) rotated 90° around Y-axis should give (1,0,0)
            var q = new Quat(new Coord(0f, 1f, 0f), MathF.PI / 2f);
            var v = new Coord(0f, 0f, 1f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Coord_RotateByQuat_180DegAroundZaxis_NegatesXandY()
        {
            var q = new Quat(new Coord(0f, 0f, 1f), MathF.PI);
            var v = new Coord(1f, 1f, 0f);
            var r = v * q;
            Assert.That(r.X, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Coord_RotateByQuat_PreservesLength()
        {
            var q = new Quat(new Coord(1f, 1f, 1f).Normalize(), MathF.PI / 3f);
            var v = new Coord(3f, 4f, 5f);
            var originalLen = v.Length();
            var r = v * q;
            Assert.That(r.Length(), Is.EqualTo(originalLen).Within(Tolerance));
        }

        [Test]
        public void Quat_Multiply_TwoNineties_AroundZ_Gives180()
        {
            var q90 = new Quat(new Coord(0f, 0f, 1f), MathF.PI / 2f);
            var q180 = q90 * q90;
            var v = new Coord(1f, 0f, 0f);
            var r = v * q180;
            Assert.That(r.X, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(r.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(r.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Quat_Multiply_IsAssociative()
        {
            var q1 = new Quat(new Coord(1f, 0f, 0f), MathF.PI / 4f);
            var q2 = new Quat(new Coord(0f, 1f, 0f), MathF.PI / 3f);
            var q3 = new Quat(new Coord(0f, 0f, 1f), MathF.PI / 6f);
            var v = new Coord(1f, 2f, 3f);

            var r1 = v * (q1 * (q2 * q3));
            var r2 = v * ((q1 * q2) * q3);

            Assert.That(r1.X, Is.EqualTo(r2.X).Within(Tolerance));
            Assert.That(r1.Y, Is.EqualTo(r2.Y).Within(Tolerance));
            Assert.That(r1.Z, Is.EqualTo(r2.Z).Within(Tolerance));
        }
    }
}
