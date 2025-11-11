/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Runtime.InteropServices;
using System.Globalization;

namespace OpenMetaverse
{
    /// <summary>
    /// A three-dimensional vector with doubleing-point values
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3d : IComparable<Vector3d>, IEquatable<Vector3d>
    {
        /// <summary>X value</summary>
        public double X;
        /// <summary>Y value</summary>
        public double Y;
        /// <summary>Z value</summary>
        public double Z;

        #region Constructors

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3d(double value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        /// <summary>
        /// Constructor, builds a vector from a byte array
        /// </summary>
        /// <param name="byteArray">Byte array containing three eight-byte doubles</param>
        /// <param name="pos">Beginning position in the byte array</param>
        public Vector3d(byte[] byteArray, int pos)
        {
            X = Y = Z = 0d;
            FromBytes(byteArray, pos);
        }

        public Vector3d(Vector3 vector)
        {
            X = vector.X;
            Y = vector.Y;
            Z = vector.Z;
        }

        public Vector3d(Vector3d vector)
        {
            X = vector.X;
            Y = vector.Y;
            Z = vector.Z;
        }

        #endregion Constructors

        #region Public Methods

        public double Length()
        {
            return Math.Sqrt(DistanceSquared(this, Zero));
        }

        public double LengthSquared()
        {
            return DistanceSquared(this, Zero);
        }

        public void Normalize()
        {
            this = Normalize(this);
        }

        /// <summary>
        /// Test if this vector is equal to another vector, within a given
        /// tolerance range
        /// </summary>
        /// <param name="vec">Vector to test against</param>
        /// <param name="tolerance">The acceptable magnitude of difference
        /// between the two vectors</param>
        /// <returns>True if the magnitude of difference between the two vectors
        /// is less than the given tolerance, otherwise false</returns>
        public bool ApproxEquals(Vector3d vec, double tolerance)
        {
            Vector3d diff = this - vec;
            return (diff.LengthSquared() <= tolerance * tolerance);
        }

        /// <summary>
        /// IComparable.CompareTo implementation
        /// </summary>
        public int CompareTo(Vector3d vector)
        {
            return this.Length().CompareTo(vector.Length());
        }

        /// <summary>
        /// Test if this vector is composed of all finite numbers
        /// </summary>
        public bool IsFinite()
        {
            return (Utils.IsFinite(X) && Utils.IsFinite(Y) && Utils.IsFinite(Z));
        }

        /// <summary>
        /// Builds a vector from a byte array
        /// </summary>
        /// <param name="byteArray">Byte array containing a 24 byte vector</param>
        /// <param name="pos">Beginning position in the byte array</param>
        public void FromBytes(byte[] byteArray, int pos)
        {
            var src = new Span<byte>(byteArray, pos, 24);

            if (!BitConverter.IsLittleEndian)
            {
                Span<byte> tmp = stackalloc byte[24];
                for (int i = 0; i < 3; i++)
                {
                    tmp[i * 8 + 0] = src[i * 8 + 7];
                    tmp[i * 8 + 1] = src[i * 8 + 6];
                    tmp[i * 8 + 2] = src[i * 8 + 5];
                    tmp[i * 8 + 3] = src[i * 8 + 4];
                    tmp[i * 8 + 4] = src[i * 8 + 3];
                    tmp[i * 8 + 5] = src[i * 8 + 2];
                    tmp[i * 8 + 6] = src[i * 8 + 1];
                    tmp[i * 8 + 7] = src[i * 8 + 0];
                }
                var dspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(tmp);
                X = dspan[0];
                Y = dspan[1];
                Z = dspan[2];
            }
            else
            {
                var dspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(src);
                X = dspan[0];
                Y = dspan[1];
                Z = dspan[2];
            }
        }

        /// <summary>
        /// Returns the raw bytes for this vector
        /// </summary>
        /// <returns>A 24 byte array containing X, Y, and Z</returns>
        public byte[] GetBytes()
        {
            byte[] byteArray = new byte[24];
            ToBytes(byteArray, 0);
            return byteArray;
        }

        /// <summary>
        /// Writes the raw bytes for this vector to a byte array
        /// </summary>
        /// <param name="dest">Destination byte array</param>
        /// <param name="pos">Position in the destination array to start
        /// writing. Must be at least 24 bytes before the end of the array</param>
        public void ToBytes(byte[] dest, int pos)
        {
            Span<double> vals = stackalloc double[3];
            vals[0] = X;
            vals[1] = Y;
            vals[2] = Z;

            var bytes = System.Runtime.InteropServices.MemoryMarshal.Cast<double, byte>(vals);

            if (!BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        dest[pos + i * 8 + j] = bytes[i * 8 + (7 - j)];
                    }
                }
            }
            else
            {
                bytes.CopyTo(new Span<byte>(dest, pos, 24));
            }
        }

        #endregion Public Methods

        #region Static Methods

        public static Vector3d Add(Vector3d value1, Vector3d value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            value1.Z += value2.Z;
            return value1;
        }

        public static Vector3d Clamp(Vector3d value1, Vector3d min, Vector3d max)
        {
            return new Vector3d(
                Utils.Clamp(value1.X, min.X, max.X),
                Utils.Clamp(value1.Y, min.Y, max.Y),
                Utils.Clamp(value1.Z, min.Z, max.Z));
        }

        public static Vector3d Cross(Vector3d value1, Vector3d value2)
        {
            return new Vector3d(
                value1.Y * value2.Z - value2.Y * value1.Z,
                value1.Z * value2.X - value2.Z * value1.X,
                value1.X * value2.Y - value2.X * value1.Y);
        }

        public static double Distance(Vector3d value1, Vector3d value2)
        {
            return Math.Sqrt(DistanceSquared(value1, value2));
        }

        public static double DistanceSquared(Vector3d value1, Vector3d value2)
        {
            return
                (value1.X - value2.X) * (value1.X - value2.X) +
                (value1.Y - value2.Y) * (value1.Y - value2.Y) +
                (value1.Z - value2.Z) * (value1.Z - value2.Z);
        }

        public static Vector3d Divide(Vector3d value1, Vector3d value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            value1.Z /= value2.Z;
            return value1;
        }

        public static Vector3d Divide(Vector3d value1, double value2)
        {
            double factor = 1d / value2;
            value1.X *= factor;
            value1.Y *= factor;
            value1.Z *= factor;
            return value1;
        }

        public static double Dot(Vector3d value1, Vector3d value2)
        {
            return value1.X * value2.X + value1.Y * value2.Y + value1.Z * value2.Z;
        }

        public static Vector3d Lerp(Vector3d value1, Vector3d value2, double amount)
        {
            return new Vector3d(
                Utils.Lerp(value1.X, value2.X, amount),
                Utils.Lerp(value1.Y, value2.Y, amount),
                Utils.Lerp(value1.Z, value2.Z, amount));
        }

        public static Vector3d Max(Vector3d value1, Vector3d value2)
        {
            return new Vector3d(
                Math.Max(value1.X, value2.X),
                Math.Max(value1.Y, value2.Y),
                Math.Max(value1.Z, value2.Z));
        }

        public static Vector3d Min(Vector3d value1, Vector3d value2)
        {
            return new Vector3d(
                Math.Min(value1.X, value2.X),
                Math.Min(value1.Y, value2.Y),
                Math.Min(value1.Z, value2.Z));
        }

        public static Vector3d Multiply(Vector3d value1, Vector3d value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            value1.Z *= value2.Z;
            return value1;
        }

        public static Vector3d Multiply(Vector3d value1, double scaleFactor)
        {
            value1.X *= scaleFactor;
            value1.Y *= scaleFactor;
            value1.Z *= scaleFactor;
            return value1;
        }

        public static Vector3d Negate(Vector3d value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            value.Z = -value.Z;
            return value;
        }

        public static Vector3d Normalize(Vector3d value)
        {
            double factor = Distance(value, Zero);
            if (factor > double.Epsilon)
            {
                factor = 1d / factor;
                value.X *= factor;
                value.Y *= factor;
                value.Z *= factor;
            }
            else
            {
                value.X = 0d;
                value.Y = 0d;
                value.Z = 0d;
            }
            return value;
        }

        /// <summary>
        /// Parse a vector from a string
        /// </summary>
        /// <param name="val">A string representation of a 3D vector, enclosed 
        /// in arrow brackets and separated by commas</param>
        public static Vector3d Parse(string val)
        {
            char[] splitChar = { ',' };
            string[] split = val.Replace("<", string.Empty).Replace(">", string.Empty).Split(splitChar);
            return new Vector3d(
                double.Parse(split[0].Trim(), Utils.EnUsCulture),
                double.Parse(split[1].Trim(), Utils.EnUsCulture),
                double.Parse(split[2].Trim(), Utils.EnUsCulture));
        }

        public static bool TryParse(string val, out Vector3d result)
        {
            try
            {
                result = Parse(val);
                return true;
            }
            catch (Exception)
            {
                result = Vector3d.Zero;
                return false;
            }
        }

        /// <summary>
        /// Interpolates between two vectors using a cubic equation
        /// </summary>
        public static Vector3d SmoothStep(Vector3d value1, Vector3d value2, double amount)
        {
            return new Vector3d(
                Utils.SmoothStep(value1.X, value2.X, amount),
                Utils.SmoothStep(value1.Y, value2.Y, amount),
                Utils.SmoothStep(value1.Z, value2.Z, amount));
        }

        public static Vector3d Subtract(Vector3d value1, Vector3d value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            value1.Z -= value2.Z;
            return value1;
        }

        #endregion Static Methods

        #region Overrides

        public override bool Equals(object obj)
        {
            return (obj is Vector3d v3d) && this == v3d;
        }

        public bool Equals(Vector3d other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        /// <summary>
        /// Get a formatted string representation of the vector
        /// </summary>
        /// <returns>A string representation of the vector</returns>
        public override string ToString()
        {
            return string.Format(Utils.EnUsCulture, "<{0}, {1}, {2}>", X, Y, Z);
        }

        /// <summary>
        /// Get a string representation of the vector elements with up to three
        /// decimal digits and separated by spaces only
        /// </summary>
        /// <returns>Raw string representation of the vector</returns>
        public string ToRawString()
        {
            CultureInfo enUs = new CultureInfo("en-us");
            enUs.NumberFormat.NumberDecimalDigits = 3;

            return string.Format(enUs, "{0} {1} {2}", X, Y, Z);
        }

        #endregion Overrides

        #region Operators

        public static bool operator ==(Vector3d value1, Vector3d value2)
        {
            return value1.X == value2.X
                && value1.Y == value2.Y
                && value1.Z == value2.Z;
        }

        public static bool operator !=(Vector3d value1, Vector3d value2)
        {
            return !(value1 == value2);
        }

        public static Vector3d operator +(Vector3d value1, Vector3d value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            value1.Z += value2.Z;
            return value1;
        }

        public static Vector3d operator -(Vector3d value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            value.Z = -value.Z;
            return value;
        }

        public static Vector3d operator -(Vector3d value1, Vector3d value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            value1.Z -= value2.Z;
            return value1;
        }

        public static Vector3d operator *(Vector3d value1, Vector3d value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            value1.Z *= value2.Z;
            return value1;
        }

        public static Vector3d operator *(Vector3d value, double scaleFactor)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            value.Z *= scaleFactor;
            return value;
        }

        public static Vector3d operator /(Vector3d value1, Vector3d value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            value1.Z /= value2.Z;
            return value1;
        }

        public static Vector3d operator /(Vector3d value, double divider)
        {
            double factor = 1d / divider;
            value.X *= factor;
            value.Y *= factor;
            value.Z *= factor;
            return value;
        }

        /// <summary>
        /// Cross product between two vectors
        /// </summary>
        public static Vector3d operator %(Vector3d value1, Vector3d value2)
        {
            return Cross(value1, value2);
        }

        /// <summary>
        /// Implicit casting for Vector3 > Vector3d
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator Vector3d(Vector3 value)
        {
            return new Vector3d(value);
        }

        #endregion Operators

        /// <summary>A vector with a value of 0,0,0</summary>
        public static readonly Vector3d Zero = new Vector3d();
        /// <summary>A vector with a value of 1,1,1</summary>
        public static readonly Vector3d One = new Vector3d();
        /// <summary>A unit vector facing forward (X axis), value of 1,0,0</summary>
        public static readonly Vector3d UnitX = new Vector3d(1d, 0d, 0d);
        /// <summary>A unit vector facing left (Y axis), value of 0,1,0</summary>
        public static readonly Vector3d UnitY = new Vector3d(0d, 1d, 0d);
        /// <summary>A unit vector facing up (Z axis), value of 0,0,1</summary>
        public static readonly Vector3d UnitZ = new Vector3d(0d, 0d, 1d);
    }
}
