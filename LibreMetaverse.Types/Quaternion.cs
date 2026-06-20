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

namespace LibreMetaverse
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Quaternion : IEquatable<Quaternion>
    {
        /// <summary>X value</summary>
        public readonly float X;

        /// <summary>Y value</summary>
        public readonly float Y;

        /// <summary>Z value</summary>
        public readonly float Z;

        /// <summary>W value</summary>
        public readonly float W;

        #region Constructors

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quaternion(Vector3 vectorPart, float scalarPart)
        {
            X = vectorPart.X;
            Y = vectorPart.Y;
            Z = vectorPart.Z;
            W = scalarPart;
        }

        /// <summary>
        /// Build a quaternion from normalized float values
        /// </summary>
        public Quaternion(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;

            float xyzsum = 1 - X * X - Y * Y - Z * Z;
            W = (xyzsum > 0) ? (float)Math.Sqrt(xyzsum) : 0;
        }

        /// <summary>
        /// Constructor, builds a quaternion object from a byte array
        /// </summary>
        /// <param name="byteArray">Byte array containing four four-byte floats</param>
        /// <param name="pos">Offset in the byte array to start reading at</param>
        /// <param name="normalized">Whether the source data is normalized or
        /// not. If this is true 12 bytes will be read, otherwise 16 bytes will
        /// be read.</param>
        public Quaternion(byte[] byteArray, int pos, bool normalized)
        {
            if (!normalized)
            {
                X = Utils.ReadSingleLittleEndian(byteArray, pos + 0);
                Y = Utils.ReadSingleLittleEndian(byteArray, pos + 4);
                Z = Utils.ReadSingleLittleEndian(byteArray, pos + 8);
                W = Utils.ReadSingleLittleEndian(byteArray, pos + 12);
            }
            else
            {
                var src = new Span<byte>(byteArray, pos, 12);
                if (!BitConverter.IsLittleEndian)
                {
                    Span<byte> tmp = stackalloc byte[12];
                    for (int i = 0; i < 3; i++)
                    {
                        tmp[i * 4 + 0] = src[i * 4 + 3];
                        tmp[i * 4 + 1] = src[i * 4 + 2];
                        tmp[i * 4 + 2] = src[i * 4 + 1];
                        tmp[i * 4 + 3] = src[i * 4 + 0];
                    }
                    var fspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(tmp);
                    X = fspan[0];
                    Y = fspan[1];
                    Z = fspan[2];
                }
                else
                {
                    var fspan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(src);
                    X = fspan[0];
                    Y = fspan[1];
                    Z = fspan[2];
                }

                float xyzsum = 1f - X * X - Y * Y - Z * Z;
                W = (xyzsum > 0f) ? (float)Math.Sqrt(xyzsum) : 0f;
            }
        }

        #endregion Constructors

        #region Public Methods

        public bool ApproxEquals(Quaternion quat, float tolerance)
        {
            Quaternion diff = this - quat;
            return (diff.LengthSquared() <= tolerance * tolerance);
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
        }

        public float LengthSquared()
        {
            return (X * X + Y * Y + Z * Z + W * W);
        }

        /// <summary>
        /// Returns a new Quaternion parsed from bytes starting at <paramref name="pos"/>.
        /// </summary>
        public static Quaternion FromBytes(byte[] byteArray, int pos, bool normalized)
            => new Quaternion(byteArray, pos, normalized);

        /// <summary>
        /// Normalize this quaternion and serialize it to a byte array
        /// </summary>
        public byte[] GetBytes()
        {
            byte[] bytes = new byte[12];
            ToBytes(bytes, 0);
            return bytes;
        }

        /// <summary>
        /// Writes the raw bytes for this quaternion to a byte array
        /// </summary>
        public void ToBytes(byte[] dest, int pos)
        {
            float norm = (float)Math.Sqrt(X * X + Y * Y + Z * Z + W * W);

            if (norm != 0f)
            {
                norm = 1f / norm;

                float x, y, z;
                if (W >= 0f)
                {
                    x = X; y = Y; z = Z;
                }
                else
                {
                    x = -X; y = -Y; z = -Z;
                }

                Span<float> vals = stackalloc float[3];
                vals[0] = norm * x;
                vals[1] = norm * y;
                vals[2] = norm * z;

                Utils.WriteSingleLittleEndian(dest, pos + 0, vals[0]);
                Utils.WriteSingleLittleEndian(dest, pos + 4, vals[1]);
                Utils.WriteSingleLittleEndian(dest, pos + 8, vals[2]);
            }
            else
            {
                throw new InvalidOperationException($"Quaternion {ToString()} normalized to zero");
            }
        }

        /// <summary>
        /// Convert this quaternion to euler angles
        /// </summary>
        public void GetEulerAngles(out float roll, out float pitch, out float yaw)
        {
            roll = 0f;
            pitch = 0f;
            yaw = 0f;

            Quaternion t = new Quaternion(this.X * this.X, this.Y * this.Y, this.Z * this.Z, this.W * this.W);

            float m = (t.X + t.Y + t.Z + t.W);
            if (Math.Abs(m) < 0.001d) return;
            float n = 2 * (this.Y * this.W + this.X * this.Z);
            float p = m * m - n * n;

            if (p > 0f)
            {
                roll = (float)Math.Atan2(2.0f * (this.X * this.W - this.Y * this.Z), (-t.X - t.Y + t.Z + t.W));
                pitch = (float)Math.Atan2(n, Math.Sqrt(p));
                yaw = (float)Math.Atan2(2.0f * (this.Z * this.W - this.X * this.Y), t.X - t.Y - t.Z + t.W);
            }
            else if (n > 0f)
            {
                roll = 0f;
                pitch = (float)(Math.PI / 2d);
                yaw = (float)Math.Atan2((this.Z * this.W + this.X * this.Y), 0.5f - t.X - t.Y);
            }
            else
            {
                roll = 0f;
                pitch = -(float)(Math.PI / 2d);
                yaw = (float)Math.Atan2((this.Z * this.W + this.X * this.Y), 0.5f - t.X - t.Z);
            }
        }

        /// <summary>Convert quaternion to euler angles vector</summary>
        public Vector3 ToEulerVector()
        {
            GetEulerAngles(out float r, out float p, out float y);
            return new Vector3(r, p, y);
        }

        /// <summary>
        /// Convert this quaternion to an angle around an axis
        /// </summary>
        public void GetAxisAngle(out Vector3 axis, out float angle)
        {
            Quaternion q = Normalize(this);

            float sin = (float)Math.Sqrt(1.0f - q.W * q.W);
            if (sin >= 0.001)
            {
                float invSin = 1.0f / sin;
                if (q.W < 0) invSin = -invSin;
                axis = new Vector3(q.X, q.Y, q.Z) * invSin;

                angle = 2.0f * (float)Math.Acos(q.W);
                if (angle > Math.PI)
                    angle = 2.0f * (float)Math.PI - angle;
            }
            else
            {
                axis = Vector3.UnitX;
                angle = 0f;
            }
        }

        #endregion Public Methods

        #region Static Methods

        public static Quaternion Add(Quaternion q1, Quaternion q2)
        {
            return new Quaternion(q1.X + q2.X, q1.Y + q2.Y, q1.Z + q2.Z, q1.W + q2.W);
        }

        /// <summary>Returns the conjugate (spatial inverse) of a quaternion</summary>
        public static Quaternion Conjugate(Quaternion quaternion)
        {
            return new Quaternion(-quaternion.X, -quaternion.Y, -quaternion.Z, quaternion.W);
        }

        /// <summary>Build a quaternion from an axis and an angle of rotation around that axis</summary>
        public static Quaternion CreateFromAxisAngle(float axisX, float axisY, float axisZ, float angle)
        {
            return CreateFromAxisAngle(new Vector3(axisX, axisY, axisZ), angle);
        }

        /// <summary>Build a quaternion from an axis and an angle of rotation around that axis</summary>
        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            axis = Vector3.Normalize(axis);
            angle *= 0.5f;
            float s = (float)Math.Sin(angle);
            return Normalize(new Quaternion(axis.X * s, axis.Y * s, axis.Z * s, (float)Math.Cos(angle)));
        }

        /// <summary>Creates a quaternion from a vector containing roll, pitch, and yaw in radians</summary>
        public static Quaternion CreateFromEulers(Vector3 eulers)
        {
            return CreateFromEulers(eulers.X, eulers.Y, eulers.Z);
        }

        /// <summary>Creates a quaternion from roll, pitch, and yaw euler angles in radians</summary>
        public static Quaternion CreateFromEulers(float roll, float pitch, float yaw)
        {
            if (roll > Utils.TWO_PI || pitch > Utils.TWO_PI || yaw > Utils.TWO_PI)
                throw new ArgumentException("Euler angles must be in radians");

            double atCos = Math.Cos(roll / 2f);
            double atSin = Math.Sin(roll / 2f);
            double leftCos = Math.Cos(pitch / 2f);
            double leftSin = Math.Sin(pitch / 2f);
            double upCos = Math.Cos(yaw / 2f);
            double upSin = Math.Sin(yaw / 2f);
            double atLeftCos = atCos * leftCos;
            double atLeftSin = atSin * leftSin;
            return new Quaternion(
                (float)(atSin * leftCos * upCos + atCos * leftSin * upSin),
                (float)(atCos * leftSin * upCos - atSin * leftCos * upSin),
                (float)(atLeftCos * upSin + atLeftSin * upCos),
                (float)(atLeftCos * upCos - atLeftSin * upSin)
            );
        }

        public static Quaternion CreateFromRotationMatrix(Matrix4 matrix)
        {
            float num8 = (matrix.M11 + matrix.M22) + matrix.M33;
            if (num8 > 0f)
            {
                float num = (float)Math.Sqrt(num8 + 1f);
                float w = num * 0.5f;
                num = 0.5f / num;
                return new Quaternion(
                    (matrix.M23 - matrix.M32) * num,
                    (matrix.M31 - matrix.M13) * num,
                    (matrix.M12 - matrix.M21) * num,
                    w);
            }
            if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
            {
                float num7 = (float)Math.Sqrt(((1f + matrix.M11) - matrix.M22) - matrix.M33);
                float num4 = 0.5f / num7;
                return new Quaternion(
                    0.5f * num7,
                    (matrix.M12 + matrix.M21) * num4,
                    (matrix.M13 + matrix.M31) * num4,
                    (matrix.M23 - matrix.M32) * num4);
            }
            if (matrix.M22 > matrix.M33)
            {
                float num6 = (float)Math.Sqrt(((1f + matrix.M22) - matrix.M11) - matrix.M33);
                float num3 = 0.5f / num6;
                return new Quaternion(
                    (matrix.M21 + matrix.M12) * num3,
                    0.5f * num6,
                    (matrix.M32 + matrix.M23) * num3,
                    (matrix.M31 - matrix.M13) * num3);
            }
            float num5 = (float)Math.Sqrt(((1f + matrix.M33) - matrix.M11) - matrix.M22);
            float num2 = 0.5f / num5;
            return new Quaternion(
                (matrix.M31 + matrix.M13) * num2,
                (matrix.M32 + matrix.M23) * num2,
                0.5f * num5,
                (matrix.M12 - matrix.M21) * num2);
        }

        public static Quaternion Divide(Quaternion q1, Quaternion q2)
        {
            return Quaternion.Inverse(q1) * q2;
        }

        public static float Dot(Quaternion q1, Quaternion q2)
        {
            return (q1.X * q2.X) + (q1.Y * q2.Y) + (q1.Z * q2.Z) + (q1.W * q2.W);
        }

        /// <summary>Conjugates and renormalizes a vector</summary>
        public static Quaternion Inverse(Quaternion quaternion)
        {
            float norm = quaternion.LengthSquared();
            if (norm == 0f)
                return new Quaternion(0f, 0f, 0f, 0f);
            float oonorm = 1f / norm;
            var conj = Conjugate(quaternion);
            return new Quaternion(conj.X * oonorm, conj.Y * oonorm, conj.Z * oonorm, conj.W * oonorm);
        }

        /// <summary>Spherical linear interpolation between two quaternions</summary>
        public static Quaternion Slerp(Quaternion q1, Quaternion q2, float amount)
        {
            float angle = Dot(q1, q2);

            if (angle < 0f)
            {
                q1 *= -1f;
                angle *= -1f;
            }

            float scale;
            float invscale;

            if ((angle + 1f) > 0.05f)
            {
                if ((1f - angle) >= 0.05f)
                {
                    // slerp
                    float theta = (float)Math.Acos(angle);
                    float invsintheta = 1f / (float)Math.Sin(theta);
                    scale = (float)Math.Sin(theta * (1f - amount)) * invsintheta;
                    invscale = (float)Math.Sin(theta * amount) * invsintheta;
                }
                else
                {
                    // lerp
                    scale = 1f - amount;
                    invscale = amount;
                }
            }
            else
            {
                q2 = new Quaternion(-q1.Y, q1.X, -q1.W, q1.Z);
                scale = (float)Math.Sin(Utils.PI * (0.5f - amount));
                invscale = (float)Math.Sin(Utils.PI * amount);
            }

            return (q1 * scale) + (q2 * invscale);
        }

        public static Quaternion Subtract(Quaternion q1, Quaternion q2)
        {
            return new Quaternion(q1.X - q2.X, q1.Y - q2.Y, q1.Z - q2.Z, q1.W - q2.W);
        }

        public static Quaternion Multiply(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
                a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
            );
        }

        public static Quaternion Multiply(Quaternion quaternion, float scaleFactor)
        {
            return new Quaternion(
                quaternion.X * scaleFactor,
                quaternion.Y * scaleFactor,
                quaternion.Z * scaleFactor,
                quaternion.W * scaleFactor);
        }

        public static Quaternion Negate(Quaternion quaternion)
        {
            return new Quaternion(-quaternion.X, -quaternion.Y, -quaternion.Z, -quaternion.W);
        }

        public static Quaternion Normalize(Quaternion q)
        {
            const float MAG_THRESHOLD = 0.0000001f;
            float mag = q.Length();
            if (mag > MAG_THRESHOLD)
            {
                float oomag = 1f / mag;
                return new Quaternion(q.X * oomag, q.Y * oomag, q.Z * oomag, q.W * oomag);
            }
            return new Quaternion(0f, 0f, 0f, 1f);
        }

        public static Quaternion Parse(string val)
        {
            if (val == null) throw new ArgumentNullException(nameof(val));
            string trimmed = val.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>')
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            string[] split = trimmed.Split(',');
            if (split.Length != 3 && split.Length != 4) throw new FormatException("Input string was not in a correct format.");

            if (split.Length == 3)
            {
                return new Quaternion(
                    float.Parse(split[0].Trim(), Utils.EnUsCulture),
                    float.Parse(split[1].Trim(), Utils.EnUsCulture),
                    float.Parse(split[2].Trim(), Utils.EnUsCulture));
            }
            else
            {
                return new Quaternion(
                    float.Parse(split[0].Trim(), Utils.EnUsCulture),
                    float.Parse(split[1].Trim(), Utils.EnUsCulture),
                    float.Parse(split[2].Trim(), Utils.EnUsCulture),
                    float.Parse(split[3].Trim(), Utils.EnUsCulture));
            }
        }

        public static bool TryParse(string val, out Quaternion result)
        {
            try
            {
                result = Parse(val);
                return true;
            }
            catch (Exception)
            {
                result = new Quaternion();
                return false;
            }
        }

        #endregion Static Methods

        #region Overrides

        public override bool Equals(object? obj)
        {
            return (obj is Quaternion quaternion) && this == quaternion;
        }

        public bool Equals(Quaternion other)
        {
            return W == other.W
                && X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override int GetHashCode()
        {
            return (X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode());
        }

        public override string ToString()
        {
            return string.Format(Utils.EnUsCulture, "<{0}, {1}, {2}, {3}>", X, Y, Z, W);
        }

        public string ToRawString()
        {
            return string.Format(Utils.EnUsCulture, "{0:F3} {1:F3} {2:F3} {3:F3}", X, Y, Z, W);
        }

        #endregion Overrides

        #region Operators

        public static bool operator ==(Quaternion q1, Quaternion q2) => q1.Equals(q2);

        public static bool operator !=(Quaternion q1, Quaternion q2) => !(q1 == q2);

        public static Quaternion operator +(Quaternion q1, Quaternion q2) => Add(q1, q2);

        public static Quaternion operator -(Quaternion q) => Negate(q);

        public static Quaternion operator -(Quaternion q1, Quaternion q2) => Subtract(q1, q2);

        public static Quaternion operator *(Quaternion a, Quaternion b) => Multiply(a, b);

        public static Quaternion operator *(Quaternion q, float scaleFactor) => Multiply(q, scaleFactor);

        public static Quaternion operator /(Quaternion q1, Quaternion q2) => Divide(q1, q2);

        #endregion Operators

        /// <summary>A quaternion with a value of 0,0,0,1</summary>
        public static readonly Quaternion Identity = new Quaternion(0f, 0f, 0f, 1f);
    }
}
