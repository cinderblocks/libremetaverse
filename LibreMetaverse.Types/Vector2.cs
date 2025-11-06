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

namespace OpenMetaverse
{
    /// <summary>
    /// A two-dimensional vector with floating-point values
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2 : IComparable<Vector2>, IEquatable<Vector2>
    {
        /// <summary>X value</summary>
        public float X;
        /// <summary>Y value</summary>
        public float Y;

        #region Constructors

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2(float value)
        {
            X = value;
            Y = value;
        }

        public Vector2(Vector2 vector)
        {
            X = vector.X;
            Y = vector.Y;
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>
        /// Test if this vector is equal to another vector, within a given
        /// tolerance range
        /// </summary>
        /// <param name="vec">Vector to test against</param>
        /// <param name="tolerance">The acceptable magnitude of difference
        /// between the two vectors</param>
        /// <returns>True if the magnitude of difference between the two vectors
        /// is less than the given tolerance, otherwise false</returns>
        public bool ApproxEquals(Vector2 vec, float tolerance)
        {
            Vector2 diff = this - vec;
            return (diff.LengthSquared() <= tolerance * tolerance);
        }

        /// <summary>
        /// Test if this vector is composed of all finite numbers
        /// </summary>
        public bool IsFinite()
        {
            return Utils.IsFinite(X) && Utils.IsFinite(Y);
        }

        /// <summary>
        /// IComparable.CompareTo implementation
        /// </summary>
        public int CompareTo(Vector2 vector)
        {
            return Length().CompareTo(vector.Length());
        }

        /// <summary>
        /// Builds a vector from a byte array
        /// </summary>
        /// <param name="byteArray">Byte array containing two four-byte floats</param>
        /// <param name="pos">Beginning position in the byte array</param>
        public void FromBytes(byte[] byteArray, int pos)
        {
            var src = new Span<byte>(byteArray, pos, 8);

            if (!BitConverter.IsLittleEndian)
            {
                // Big endian architecture
                Span<byte> tmp = stackalloc byte[8];
                for (int i = 0; i < 2; i++)
                {
                    tmp[i * 4 + 0] = src[i * 4 + 3];
                    tmp[i * 4 + 1] = src[i * 4 + 2];
                    tmp[i * 4 + 2] = src[i * 4 + 1];
                    tmp[i * 4 + 3] = src[i * 4 + 0];
                }
                var fspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(tmp);
                X = fspan[0];
                Y = fspan[1];
            }
            else
            {
                var fspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(src);
                X = fspan[0];
                Y = fspan[1];
            }
        }

        /// <summary>
        /// Returns the raw bytes for this vector
        /// </summary>
        /// <returns>An eight-byte array containing X and Y</returns>
        public byte[] GetBytes()
        {
            byte[] byteArray = new byte[8];
            ToBytes(byteArray, 0);
            return byteArray;
        }

        /// <summary>
        /// Writes the raw bytes for this vector to a byte array
        /// </summary>
        /// <param name="dest">Destination byte array</param>
        /// <param name="pos">Position in the destination array to start
        /// writing. Must be at least 8 bytes before the end of the array</param>
        public void ToBytes(byte[] dest, int pos)
        {
            Span<float> vals = stackalloc float[2];
            vals[0] = X;
            vals[1] = Y;

            var bytes = System.Runtime.InteropServices.MemoryMarshal.Cast<float, byte>(vals);

            if (!BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < 2; i++)
                {
                    dest[pos + i * 4 + 0] = bytes[i * 4 + 3];
                    dest[pos + i * 4 + 1] = bytes[i * 4 + 2];
                    dest[pos + i * 4 + 2] = bytes[i * 4 + 1];
                    dest[pos + i * 4 + 3] = bytes[i * 4 + 0];
                }
            }
            else
            {
                bytes.CopyTo(new Span<byte>(dest, pos, 8));
            }
        }

        public float Length()
        {
            return (float)Math.Sqrt(DistanceSquared(this, Zero));
        }

        public float LengthSquared()
        {
            return DistanceSquared(this, Zero);
        }

        public void Normalize()
        {
            this = Normalize(this);
        }

        #endregion Public Methods

        #region Static Methods

        public static Vector2 Add(Vector2 value1, Vector2 value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            return value1;
        }

        public static Vector2 Clamp(Vector2 value1, Vector2 min, Vector2 max)
        {
            return new Vector2(
                Utils.Clamp(value1.X, min.X, max.X),
                Utils.Clamp(value1.Y, min.Y, max.Y));
        }

        public static float Distance(Vector2 value1, Vector2 value2)
        {
            return (float)Math.Sqrt(DistanceSquared(value1, value2));
        }

        public static float DistanceSquared(Vector2 value1, Vector2 value2)
        {
            return
                (value1.X - value2.X) * (value1.X - value2.X) +
                (value1.Y - value2.Y) * (value1.Y - value2.Y);
        }

        public static Vector2 Divide(Vector2 value1, Vector2 value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            return value1;
        }

        public static Vector2 Divide(Vector2 value1, float divider)
        {
            float factor = 1 / divider;
            value1.X *= factor;
            value1.Y *= factor;
            return value1;
        }

        public static float Dot(Vector2 value1, Vector2 value2)
        {
            return value1.X * value2.X + value1.Y * value2.Y;
        }

        public static Vector2 Lerp(Vector2 value1, Vector2 value2, float amount)
        {
            return new Vector2(
                Utils.Lerp(value1.X, value2.X, amount),
                Utils.Lerp(value1.Y, value2.Y, amount));
        }

        public static Vector2 Max(Vector2 value1, Vector2 value2)
        {
            return new Vector2(
                Math.Max(value1.X, value2.X),
                Math.Max(value1.Y, value2.Y));
        }

        public static Vector2 Min(Vector2 value1, Vector2 value2)
        {
            return new Vector2(
                Math.Min(value1.X, value2.X),
                Math.Min(value1.Y, value2.Y));
        }

        public static Vector2 Multiply(Vector2 value1, Vector2 value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            return value1;
        }

        public static Vector2 Multiply(Vector2 value1, float scaleFactor)
        {
            value1.X *= scaleFactor;
            value1.Y *= scaleFactor;
            return value1;
        }

        public static Vector2 Negate(Vector2 value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            return value;
        }

        public static Vector2 Normalize(Vector2 value)
        {
            const float MAG_THRESHOLD = 0.0000001f;
            float factor = DistanceSquared(value, Zero);
            if (factor > MAG_THRESHOLD)
            {
                factor = 1f / (float)Math.Sqrt(factor);
                value.X *= factor;
                value.Y *= factor;
            }
            else
            {
                value.X = 0f;
                value.Y = 0f;
            }
            return value;
        }

        /// <summary>
        /// Parse a vector from a string
        /// </summary>
        /// <param name="val">A string representation of a 2D vector, enclosed 
        /// in arrow brackets and separated by commas</param>
        public static Vector2 Parse(string val)
        {
            if (val == null) throw new ArgumentNullException(nameof(val));
            string trimmed = val.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>')
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            string[] split = trimmed.Split(',');
            if (split.Length != 2) throw new FormatException("Input string was not in a correct format.");

            return new Vector2(
                float.Parse(split[0].Trim(), Utils.EnUsCulture),
                float.Parse(split[1].Trim(), Utils.EnUsCulture));
        }

        public static bool TryParse(string val, out Vector2 result)
        {
            try
            {
                result = Parse(val);
                return true;
            }
            catch (Exception)
            {
                result = Vector2.Zero;
                return false;
            }
        }

        /// <summary>
        /// Interpolates between two vectors using a cubic equation
        /// </summary>
        public static Vector2 SmoothStep(Vector2 value1, Vector2 value2, float amount)
        {
            return new Vector2(
                Utils.SmoothStep(value1.X, value2.X, amount),
                Utils.SmoothStep(value1.Y, value2.Y, amount));
        }

        public static Vector2 Subtract(Vector2 value1, Vector2 value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            return value1;
        }

        public static Vector2 Transform(Vector2 position, Matrix4 matrix)
        {
            position.X = (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M41;
            position.Y = (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M42;
            return position;
        }

        public static Vector2 TransformNormal(Vector2 position, Matrix4 matrix)
        {
            position.X = (position.X * matrix.M11) + (position.Y * matrix.M21);
            position.Y = (position.X * matrix.M12) + (position.Y * matrix.M22);
            return position;
        }

        #endregion Static Methods

        #region Overrides

        public override bool Equals(object obj)
        {
            return (obj is Vector2 vector2) && this == vector2;
        }

        public bool Equals(Vector2 other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            int hash = X.GetHashCode();
            hash = hash * 31 + Y.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Get a formatted string representation of the vector
        /// </summary>
        /// <returns>A string representation of the vector</returns>
        public override string ToString()
        {
            return string.Format(Utils.EnUsCulture, "<{0}, {1}>", X, Y);
        }

        /// <summary>
        /// Get a string representation of the vector elements with up to three
        /// decimal digits and separated by spaces only
        /// </summary>
        /// <returns>Raw string representation of the vector</returns>
        public string ToRawString()
        {
            return string.Format(Utils.EnUsCulture, "{0:F3} {1:F3}", X, Y);
        }

        #endregion Overrides

        #region Operators

        public static bool operator ==(Vector2 value1, Vector2 value2)
        {
            return value1.X == value2.X && value1.Y == value2.Y;
        }

        public static bool operator !=(Vector2 value1, Vector2 value2)
        {
            return value1.X != value2.X || value1.Y != value2.Y;
        }

        public static Vector2 operator +(Vector2 value1, Vector2 value2)
        {
            value1.X += value2.X;
            value1.Y += value2.Y;
            return value1;
        }

        public static Vector2 operator -(Vector2 value)
        {
            value.X = -value.X;
            value.Y = -value.Y;
            return value;
        }

        public static Vector2 operator -(Vector2 value1, Vector2 value2)
        {
            value1.X -= value2.X;
            value1.Y -= value2.Y;
            return value1;
        }

        public static Vector2 operator *(Vector2 value1, Vector2 value2)
        {
            value1.X *= value2.X;
            value1.Y *= value2.Y;
            return value1;
        }


        public static Vector2 operator *(Vector2 value, float scaleFactor)
        {
            value.X *= scaleFactor;
            value.Y *= scaleFactor;
            return value;
        }

        public static Vector2 operator /(Vector2 value1, Vector2 value2)
        {
            value1.X /= value2.X;
            value1.Y /= value2.Y;
            return value1;
        }


        public static Vector2 operator /(Vector2 value1, float divider)
        {
            float factor = 1 / divider;
            value1.X *= factor;
            value1.Y *= factor;
            return value1;
        }

        #endregion Operators

        /// <summary>A vector with a value of 0,0</summary>
        public static readonly Vector2 Zero = new Vector2();
        /// <summary>A vector with a value of 1,1</summary>
        public static readonly Vector2 One = new Vector2(1f, 1f);
        /// <summary>A vector with a value of 1,0</summary>
        public static readonly Vector2 UnitX = new Vector2(1f, 0f);
        /// <summary>A vector with a value of 0,1</summary>
        public static readonly Vector2 UnitY = new Vector2(0f, 1f);
    }
}
