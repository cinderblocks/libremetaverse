/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
    public readonly struct Matrix4 : IEquatable<Matrix4>
    {
        public readonly float M11, M12, M13, M14;
        public readonly float M21, M22, M23, M24;
        public readonly float M31, M32, M33, M34;
        public readonly float M41, M42, M43, M44;

        #region Properties

        public Vector3 AtAxis => new Vector3(M11, M21, M31);
        public Vector3 LeftAxis => new Vector3(M12, M22, M32);
        public Vector3 UpAxis => new Vector3(M13, M23, M33);

        #endregion Properties

        #region Constructors

        public Matrix4(
            float m11, float m12, float m13, float m14,
            float m21, float m22, float m23, float m24,
            float m31, float m32, float m33, float m34,
            float m41, float m42, float m43, float m44)
        {
            M11 = m11; M12 = m12; M13 = m13; M14 = m14;
            M21 = m21; M22 = m22; M23 = m23; M24 = m24;
            M31 = m31; M32 = m32; M33 = m33; M34 = m34;
            M41 = m41; M42 = m42; M43 = m43; M44 = m44;
        }

        public Matrix4(float roll, float pitch, float yaw)
        {
            this = CreateFromEulers(roll, pitch, yaw);
        }

        public Matrix4(Matrix4 m)
        {
            M11 = m.M11; M12 = m.M12; M13 = m.M13; M14 = m.M14;
            M21 = m.M21; M22 = m.M22; M23 = m.M23; M24 = m.M24;
            M31 = m.M31; M32 = m.M32; M33 = m.M33; M34 = m.M34;
            M41 = m.M41; M42 = m.M42; M43 = m.M43; M44 = m.M44;
        }

        #endregion Constructors

        #region Public Methods

        public float Determinant()
        {
            return
                M14 * M23 * M32 * M41 - M13 * M24 * M32 * M41 - M14 * M22 * M33 * M41 + M12 * M24 * M33 * M41 +
                M13 * M22 * M34 * M41 - M12 * M23 * M34 * M41 - M14 * M23 * M31 * M42 + M13 * M24 * M31 * M42 +
                M14 * M21 * M33 * M42 - M11 * M24 * M33 * M42 - M13 * M21 * M34 * M42 + M11 * M23 * M34 * M42 +
                M14 * M22 * M31 * M43 - M12 * M24 * M31 * M43 - M14 * M21 * M32 * M43 + M11 * M24 * M32 * M43 +
                M12 * M21 * M34 * M43 - M11 * M22 * M34 * M43 - M13 * M22 * M31 * M44 + M12 * M23 * M31 * M44 +
                M13 * M21 * M32 * M44 - M11 * M23 * M32 * M44 - M12 * M21 * M33 * M44 + M11 * M22 * M33 * M44;
        }

        public float Determinant3x3()
        {
            float diag1 = M11 * M22 * M33;
            float diag2 = M12 * M23 * M31;
            float diag3 = M13 * M21 * M32;
            float diag4 = M31 * M22 * M13;
            float diag5 = M32 * M23 * M11;
            float diag6 = M33 * M21 * M12;
            return diag1 + diag2 + diag3 - (diag4 + diag5 + diag6);
        }

        public float Trace()
        {
            return M11 + M22 + M33 + M44;
        }

        /// <summary>Convert this matrix to euler rotations</summary>
        public void GetEulerAngles(out float roll, out float pitch, out float yaw)
        {
            double angleX, angleZ;
            double cx, cz;
            double sx, sz;

            var angleY = Math.Asin(Utils.Clamp(M13, -1f, 1f));
            var cy = Math.Cos(angleY);

            if (Math.Abs(cy) > 0.005f)
            {
                cx = M33 / cy;
                sx = (-M23) / cy;
                angleX = (float)Math.Atan2(sx, cx);

                cz = M11 / cy;
                sz = (-M12) / cy;
                angleZ = (float)Math.Atan2(sz, cz);
            }
            else
            {
                angleX = 0;
                cz = M22;
                sz = M21;
                angleZ = Math.Atan2(sz, cz);
            }

            if (angleX < 0) angleX += 2d * Math.PI;
            if (angleY < 0) angleY += 2d * Math.PI;
            if (angleZ < 0) angleZ += 2d * Math.PI;

            roll = (float)angleX;
            pitch = (float)angleY;
            yaw = (float)angleZ;
        }

        /// <summary>Convert this matrix to a quaternion rotation</summary>
        public Quaternion GetQuaternion()
        {
            float trace = Trace(); // Trace() includes M44; +1f is already folded in for rotation matrices

            if (trace > float.Epsilon)
            {
                float s = 0.5f / (float)Math.Sqrt(trace);
                return new Quaternion(
                    (M32 - M23) * s,
                    (M13 - M31) * s,
                    (M21 - M12) * s,
                    0.25f / s);
            }
            if (M11 > M22 && M11 > M33)
            {
                float s = 2.0f * (float)Math.Sqrt(1.0f + M11 - M22 - M33);
                return new Quaternion(
                    0.25f * s,
                    (M12 + M21) / s,
                    (M13 + M31) / s,
                    (M23 - M32) / s);
            }
            if (M22 > M33)
            {
                float s = 2.0f * (float)Math.Sqrt(1.0f + M22 - M11 - M33);
                return new Quaternion(
                    (M12 + M21) / s,
                    0.25f * s,
                    (M23 + M32) / s,
                    (M13 - M31) / s);
            }
            else
            {
                float s = 2.0f * (float)Math.Sqrt(1.0f + M33 - M11 - M22);
                return new Quaternion(
                    (M13 + M31) / s,
                    (M23 + M32) / s,
                    0.25f * s,
                    (M12 - M21) / s);
            }
        }

        public bool Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation)
        {
            translation = new Vector3(this.M41, this.M42, this.M43);

            float xs = (Math.Sign(M11 * M12 * M13 * M14) < 0) ? -1 : 1;
            float ys = (Math.Sign(M21 * M22 * M23 * M24) < 0) ? -1 : 1;
            float zs = (Math.Sign(M31 * M32 * M33 * M34) < 0) ? -1 : 1;

            float sx = xs * (float)Math.Sqrt(this.M11 * this.M11 + this.M12 * this.M12 + this.M13 * this.M13);
            float sy = ys * (float)Math.Sqrt(this.M21 * this.M21 + this.M22 * this.M22 + this.M23 * this.M23);
            float sz = zs * (float)Math.Sqrt(this.M31 * this.M31 + this.M32 * this.M32 + this.M33 * this.M33);
            scale = new Vector3(sx, sy, sz);

            if (sx == 0.0 || sy == 0.0 || sz == 0.0)
            {
                rotation = Quaternion.Identity;
                return false;
            }

            Matrix4 m1 = new Matrix4(
                this.M11 / sx, M12 / sx, M13 / sx, 0,
                this.M21 / sy, M22 / sy, M23 / sy, 0,
                this.M31 / sz, M32 / sz, M33 / sz, 0,
                0, 0, 0, 1);

            rotation = Quaternion.CreateFromRotationMatrix(m1);
            return true;
        }

        #endregion Public Methods

        #region Static Methods

        public static Matrix4 Add(Matrix4 matrix1, Matrix4 matrix2)
        {
            return new Matrix4(
                matrix1.M11 + matrix2.M11, matrix1.M12 + matrix2.M12, matrix1.M13 + matrix2.M13, matrix1.M14 + matrix2.M14,
                matrix1.M21 + matrix2.M21, matrix1.M22 + matrix2.M22, matrix1.M23 + matrix2.M23, matrix1.M24 + matrix2.M24,
                matrix1.M31 + matrix2.M31, matrix1.M32 + matrix2.M32, matrix1.M33 + matrix2.M33, matrix1.M34 + matrix2.M34,
                matrix1.M41 + matrix2.M41, matrix1.M42 + matrix2.M42, matrix1.M43 + matrix2.M43, matrix1.M44 + matrix2.M44);
        }

        public static Matrix4 CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float x = axis.X;
            float y = axis.Y;
            float z = axis.Z;
            float sin = (float)Math.Sin(angle);
            float cos = (float)Math.Cos(angle);
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;

            return new Matrix4(
                xx + (cos * (1f - xx)),        (xy - (cos * xy)) + (sin * z),  (xz - (cos * xz)) - (sin * y),  0f,
                (xy - (cos * xy)) - (sin * z), yy + (cos * (1f - yy)),         (yz - (cos * yz)) + (sin * x),  0f,
                (xz - (cos * xz)) + (sin * y), (yz - (cos * yz)) - (sin * x),  zz + (cos * (1f - zz)),         0f,
                0f,                            0f,                              0f,                              1f);
        }

        /// <summary>Construct a matrix from euler rotation values in radians</summary>
        public static Matrix4 CreateFromEulers(float roll, float pitch, float yaw)
        {
            var a = (float)Math.Cos(roll);
            var b = (float)Math.Sin(roll);
            var c = (float)Math.Cos(pitch);
            var d = (float)Math.Sin(pitch);
            var e = (float)Math.Cos(yaw);
            var f = (float)Math.Sin(yaw);
            var ad = a * d;
            var bd = b * d;

            return new Matrix4(
                c * e,       -c * f,       d,    0f,
                bd * e + a * f, -bd * f + a * e, -b * c, 0f,
                -ad * e + b * f, ad * f + b * e,  a * c,  0f,
                0f,           0f,           0f,   1f);
        }

        public static Matrix4 CreateFromQuaternion(Quaternion quaternion)
        {
            float xx = quaternion.X * quaternion.X;
            float yy = quaternion.Y * quaternion.Y;
            float zz = quaternion.Z * quaternion.Z;
            float xy = quaternion.X * quaternion.Y;
            float zw = quaternion.Z * quaternion.W;
            float zx = quaternion.Z * quaternion.X;
            float yw = quaternion.Y * quaternion.W;
            float yz = quaternion.Y * quaternion.Z;
            float xw = quaternion.X * quaternion.W;

            return new Matrix4(
                1f - (2f * (yy + zz)), 2f * (xy + zw),        2f * (zx - yw),        0f,
                2f * (xy - zw),        1f - (2f * (zz + xx)), 2f * (yz + xw),        0f,
                2f * (zx + yw),        2f * (yz - xw),        1f - (2f * (yy + xx)), 0f,
                0f,                    0f,                    0f,                    1f);
        }

        public static Matrix4 CreateLookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
        {
            Vector3 z = Vector3.Normalize(cameraPosition - cameraTarget);
            Vector3 x = Vector3.Normalize(Vector3.Cross(cameraUpVector, z));
            Vector3 y = Vector3.Cross(z, x);

            return new Matrix4(
                x.X, y.X, z.X, 0f,
                x.Y, y.Y, z.Y, 0f,
                x.Z, y.Z, z.Z, 0f,
                -Vector3.Dot(x, cameraPosition), -Vector3.Dot(y, cameraPosition), -Vector3.Dot(z, cameraPosition), 1f);
        }

        public static Matrix4 CreateRotationX(float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Matrix4(
                1f,  0f,   0f,  0f,
                0f,  cos,  sin, 0f,
                0f,  -sin, cos, 0f,
                0f,  0f,   0f,  1f);
        }

        public static Matrix4 CreateRotationY(float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Matrix4(
                cos,  0f, -sin, 0f,
                0f,   1f,  0f,  0f,
                sin,  0f,  cos, 0f,
                0f,   0f,  0f,  1f);
        }

        public static Matrix4 CreateRotationZ(float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Matrix4(
                cos,  sin, 0f, 0f,
                -sin, cos, 0f, 0f,
                0f,   0f,  1f, 0f,
                0f,   0f,  0f, 1f);
        }

        public static Matrix4 CreateScale(Vector3 scale)
        {
            return new Matrix4(
                scale.X, 0f,      0f,      0f,
                0f,      scale.Y, 0f,      0f,
                0f,      0f,      scale.Z, 0f,
                0f,      0f,      0f,      1f);
        }

        public static Matrix4 CreateTranslation(Vector3 position)
        {
            return new Matrix4(
                1f,         0f,         0f,         0f,
                0f,         1f,         0f,         0f,
                0f,         0f,         1f,         0f,
                position.X, position.Y, position.Z, 1f);
        }

        public static Matrix4 CreateWorld(Vector3 position, Vector3 forward, Vector3 up)
        {
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
            up = Vector3.Normalize(Vector3.Cross(right, forward));

            return new Matrix4(
                right.X,     right.Y,     right.Z,     0f,
                up.X,        up.Y,        up.Z,        0f,
                -forward.X,  -forward.Y,  -forward.Z,  0f,
                position.X,  position.Y,  position.Z,  1f);
        }

        public static Matrix4 Divide(Matrix4 matrix1, Matrix4 matrix2)
        {
            return new Matrix4(
                matrix1.M11 / matrix2.M11, matrix1.M12 / matrix2.M12, matrix1.M13 / matrix2.M13, matrix1.M14 / matrix2.M14,
                matrix1.M21 / matrix2.M21, matrix1.M22 / matrix2.M22, matrix1.M23 / matrix2.M23, matrix1.M24 / matrix2.M24,
                matrix1.M31 / matrix2.M31, matrix1.M32 / matrix2.M32, matrix1.M33 / matrix2.M33, matrix1.M34 / matrix2.M34,
                matrix1.M41 / matrix2.M41, matrix1.M42 / matrix2.M42, matrix1.M43 / matrix2.M43, matrix1.M44 / matrix2.M44);
        }

        public static Matrix4 Divide(Matrix4 matrix1, float divider)
        {
            float ood = 1f / divider;
            return new Matrix4(
                matrix1.M11 * ood, matrix1.M12 * ood, matrix1.M13 * ood, matrix1.M14 * ood,
                matrix1.M21 * ood, matrix1.M22 * ood, matrix1.M23 * ood, matrix1.M24 * ood,
                matrix1.M31 * ood, matrix1.M32 * ood, matrix1.M33 * ood, matrix1.M34 * ood,
                matrix1.M41 * ood, matrix1.M42 * ood, matrix1.M43 * ood, matrix1.M44 * ood);
        }

        public static Matrix4 Lerp(Matrix4 matrix1, Matrix4 matrix2, float amount)
        {
            return new Matrix4(
                matrix1.M11 + (matrix2.M11 - matrix1.M11) * amount, matrix1.M12 + (matrix2.M12 - matrix1.M12) * amount,
                matrix1.M13 + (matrix2.M13 - matrix1.M13) * amount, matrix1.M14 + (matrix2.M14 - matrix1.M14) * amount,
                matrix1.M21 + (matrix2.M21 - matrix1.M21) * amount, matrix1.M22 + (matrix2.M22 - matrix1.M22) * amount,
                matrix1.M23 + (matrix2.M23 - matrix1.M23) * amount, matrix1.M24 + (matrix2.M24 - matrix1.M24) * amount,
                matrix1.M31 + (matrix2.M31 - matrix1.M31) * amount, matrix1.M32 + (matrix2.M32 - matrix1.M32) * amount,
                matrix1.M33 + (matrix2.M33 - matrix1.M33) * amount, matrix1.M34 + (matrix2.M34 - matrix1.M34) * amount,
                matrix1.M41 + (matrix2.M41 - matrix1.M41) * amount, matrix1.M42 + (matrix2.M42 - matrix1.M42) * amount,
                matrix1.M43 + (matrix2.M43 - matrix1.M43) * amount, matrix1.M44 + (matrix2.M44 - matrix1.M44) * amount);
        }

        public static Matrix4 Multiply(Matrix4 matrix1, Matrix4 matrix2)
        {
            return new Matrix4(
                matrix1.M11 * matrix2.M11 + matrix1.M12 * matrix2.M21 + matrix1.M13 * matrix2.M31 + matrix1.M14 * matrix2.M41,
                matrix1.M11 * matrix2.M12 + matrix1.M12 * matrix2.M22 + matrix1.M13 * matrix2.M32 + matrix1.M14 * matrix2.M42,
                matrix1.M11 * matrix2.M13 + matrix1.M12 * matrix2.M23 + matrix1.M13 * matrix2.M33 + matrix1.M14 * matrix2.M43,
                matrix1.M11 * matrix2.M14 + matrix1.M12 * matrix2.M24 + matrix1.M13 * matrix2.M34 + matrix1.M14 * matrix2.M44,

                matrix1.M21 * matrix2.M11 + matrix1.M22 * matrix2.M21 + matrix1.M23 * matrix2.M31 + matrix1.M24 * matrix2.M41,
                matrix1.M21 * matrix2.M12 + matrix1.M22 * matrix2.M22 + matrix1.M23 * matrix2.M32 + matrix1.M24 * matrix2.M42,
                matrix1.M21 * matrix2.M13 + matrix1.M22 * matrix2.M23 + matrix1.M23 * matrix2.M33 + matrix1.M24 * matrix2.M43,
                matrix1.M21 * matrix2.M14 + matrix1.M22 * matrix2.M24 + matrix1.M23 * matrix2.M34 + matrix1.M24 * matrix2.M44,

                matrix1.M31 * matrix2.M11 + matrix1.M32 * matrix2.M21 + matrix1.M33 * matrix2.M31 + matrix1.M34 * matrix2.M41,
                matrix1.M31 * matrix2.M12 + matrix1.M32 * matrix2.M22 + matrix1.M33 * matrix2.M32 + matrix1.M34 * matrix2.M42,
                matrix1.M31 * matrix2.M13 + matrix1.M32 * matrix2.M23 + matrix1.M33 * matrix2.M33 + matrix1.M34 * matrix2.M43,
                matrix1.M31 * matrix2.M14 + matrix1.M32 * matrix2.M24 + matrix1.M33 * matrix2.M34 + matrix1.M34 * matrix2.M44,

                matrix1.M41 * matrix2.M11 + matrix1.M42 * matrix2.M21 + matrix1.M43 * matrix2.M31 + matrix1.M44 * matrix2.M41,
                matrix1.M41 * matrix2.M12 + matrix1.M42 * matrix2.M22 + matrix1.M43 * matrix2.M32 + matrix1.M44 * matrix2.M42,
                matrix1.M41 * matrix2.M13 + matrix1.M42 * matrix2.M23 + matrix1.M43 * matrix2.M33 + matrix1.M44 * matrix2.M43,
                matrix1.M41 * matrix2.M14 + matrix1.M42 * matrix2.M24 + matrix1.M43 * matrix2.M34 + matrix1.M44 * matrix2.M44);
        }

        public static Matrix4 Multiply(Matrix4 matrix1, float scaleFactor)
        {
            return new Matrix4(
                matrix1.M11 * scaleFactor, matrix1.M12 * scaleFactor, matrix1.M13 * scaleFactor, matrix1.M14 * scaleFactor,
                matrix1.M21 * scaleFactor, matrix1.M22 * scaleFactor, matrix1.M23 * scaleFactor, matrix1.M24 * scaleFactor,
                matrix1.M31 * scaleFactor, matrix1.M32 * scaleFactor, matrix1.M33 * scaleFactor, matrix1.M34 * scaleFactor,
                matrix1.M41 * scaleFactor, matrix1.M42 * scaleFactor, matrix1.M43 * scaleFactor, matrix1.M44 * scaleFactor);
        }

        public static Matrix4 Negate(Matrix4 matrix)
        {
            return new Matrix4(
                -matrix.M11, -matrix.M12, -matrix.M13, -matrix.M14,
                -matrix.M21, -matrix.M22, -matrix.M23, -matrix.M24,
                -matrix.M31, -matrix.M32, -matrix.M33, -matrix.M34,
                -matrix.M41, -matrix.M42, -matrix.M43, -matrix.M44);
        }

        public static Matrix4 Subtract(Matrix4 matrix1, Matrix4 matrix2)
        {
            return new Matrix4(
                matrix1.M11 - matrix2.M11, matrix1.M12 - matrix2.M12, matrix1.M13 - matrix2.M13, matrix1.M14 - matrix2.M14,
                matrix1.M21 - matrix2.M21, matrix1.M22 - matrix2.M22, matrix1.M23 - matrix2.M23, matrix1.M24 - matrix2.M24,
                matrix1.M31 - matrix2.M31, matrix1.M32 - matrix2.M32, matrix1.M33 - matrix2.M33, matrix1.M34 - matrix2.M34,
                matrix1.M41 - matrix2.M41, matrix1.M42 - matrix2.M42, matrix1.M43 - matrix2.M43, matrix1.M44 - matrix2.M44);
        }

        public static Matrix4 Transform(Matrix4 value, Quaternion rotation)
        {
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float a = (1f - rotation.Y * y2) - rotation.Z * z2;
            float b = rotation.X * y2 - rotation.W * z2;
            float c = rotation.X * z2 + rotation.W * y2;
            float d = rotation.X * y2 + rotation.W * z2;
            float e = (1f - rotation.X * x2) - rotation.Z * z2;
            float f = rotation.Y * z2 - rotation.W * x2;
            float g = rotation.X * z2 - rotation.W * y2;
            float h = rotation.Y * z2 + rotation.W * x2;
            float ii = (1f - rotation.X * x2) - rotation.Y * y2;

            return new Matrix4(
                ((value.M11 * a) + (value.M12 * b)) + (value.M13 * c),
                ((value.M11 * d) + (value.M12 * e)) + (value.M13 * f),
                ((value.M11 * g) + (value.M12 * h)) + (value.M13 * ii),
                value.M14,

                ((value.M21 * a) + (value.M22 * b)) + (value.M23 * c),
                ((value.M21 * d) + (value.M22 * e)) + (value.M23 * f),
                ((value.M21 * g) + (value.M22 * h)) + (value.M23 * ii),
                value.M24,

                ((value.M31 * a) + (value.M32 * b)) + (value.M33 * c),
                ((value.M31 * d) + (value.M32 * e)) + (value.M33 * f),
                ((value.M31 * g) + (value.M32 * h)) + (value.M33 * ii),
                value.M34,

                ((value.M41 * a) + (value.M42 * b)) + (value.M43 * c),
                ((value.M41 * d) + (value.M42 * e)) + (value.M43 * f),
                ((value.M41 * g) + (value.M42 * h)) + (value.M43 * ii),
                value.M44);
        }

        public static Matrix4 Transpose(Matrix4 matrix)
        {
            return new Matrix4(
                matrix.M11, matrix.M21, matrix.M31, matrix.M41,
                matrix.M12, matrix.M22, matrix.M32, matrix.M42,
                matrix.M13, matrix.M23, matrix.M33, matrix.M43,
                matrix.M14, matrix.M24, matrix.M34, matrix.M44);
        }

        public static Matrix4 Inverse3x3(Matrix4 matrix)
        {
            if (matrix.Determinant3x3() == 0f)
                throw new ArgumentException("Singular matrix inverse not possible");

            return (Adjoint3x3(matrix) / matrix.Determinant3x3());
        }

        public static Matrix4 Adjoint3x3(Matrix4 matrix)
        {
            Span<float> a = stackalloc float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    a[i * 4 + j] = (float)(Math.Pow(-1, i + j) * Minor(matrix, i, j).Determinant3x3());
            return Transpose(new Matrix4(
                a[0], a[1], a[2], a[3],
                a[4], a[5], a[6], a[7],
                a[8], a[9], a[10], a[11],
                a[12], a[13], a[14], a[15]));
        }

        public static Matrix4 Inverse(Matrix4 matrix)
        {
            if (matrix.Determinant() == 0f)
                throw new ArgumentException("Singular matrix inverse not possible");

            return (Adjoint(matrix) / matrix.Determinant());
        }

        public static Matrix4 Adjoint(Matrix4 matrix)
        {
            Span<float> a = stackalloc float[16];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    a[i * 4 + j] = (float)(Math.Pow(-1, i + j) * Minor(matrix, i, j).Determinant3x3());
            return Transpose(new Matrix4(
                a[0], a[1], a[2], a[3],
                a[4], a[5], a[6], a[7],
                a[8], a[9], a[10], a[11],
                a[12], a[13], a[14], a[15]));
        }

        public static Matrix4 Minor(Matrix4 matrix, int row, int col)
        {
            Span<float> vals = stackalloc float[16];
            int m = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i == row) continue;
                int n = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (j == col) continue;
                    vals[m * 4 + n] = matrix[i, j];
                    n++;
                }
                m++;
            }
            return new Matrix4(
                vals[0], vals[1], vals[2], vals[3],
                vals[4], vals[5], vals[6], vals[7],
                vals[8], vals[9], vals[10], vals[11],
                0f, 0f, 0f, 0f);
        }

        #endregion Static Methods

        #region Overrides

        public override bool Equals(object? obj)
        {
            return (obj is Matrix4 matrix4) && this.Equals(matrix4);
        }

        public bool Equals(Matrix4 other)
        {
            return M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
                   M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
                   M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
                   M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;
        }

        public override int GetHashCode()
        {
            return
                M11.GetHashCode() ^ M12.GetHashCode() ^ M13.GetHashCode() ^ M14.GetHashCode() ^
                M21.GetHashCode() ^ M22.GetHashCode() ^ M23.GetHashCode() ^ M24.GetHashCode() ^
                M31.GetHashCode() ^ M32.GetHashCode() ^ M33.GetHashCode() ^ M34.GetHashCode() ^
                M41.GetHashCode() ^ M42.GetHashCode() ^ M43.GetHashCode() ^ M44.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format(Utils.EnUsCulture,
                "|{0}, {1}, {2}, {3}|\n|{4}, {5}, {6}, {7}|\n|{8}, {9}, {10}, {11}|\n|{12}, {13}, {14}, {15}|",
                M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, M41, M42, M43, M44);
        }

        #endregion Overrides

        #region Operators

        public static bool operator ==(Matrix4 left, Matrix4 right) => left.Equals(right);

        public static bool operator !=(Matrix4 left, Matrix4 right) => !left.Equals(right);

        public static Matrix4 operator +(Matrix4 left, Matrix4 right) => Add(left, right);

        public static Matrix4 operator -(Matrix4 matrix) => Negate(matrix);

        public static Matrix4 operator -(Matrix4 left, Matrix4 right) => Subtract(left, right);

        public static Matrix4 operator *(Matrix4 left, Matrix4 right) => Multiply(left, right);

        public static Matrix4 operator *(Matrix4 left, float scalar) => Multiply(left, scalar);

        public static Matrix4 operator /(Matrix4 left, Matrix4 right) => Divide(left, right);

        public static Matrix4 operator /(Matrix4 matrix, float divider) => Divide(matrix, divider);

        public Vector4 this[int row]
        {
            get
            {
                switch (row)
                {
                    case 0: return new Vector4(M11, M12, M13, M14);
                    case 1: return new Vector4(M21, M22, M23, M24);
                    case 2: return new Vector4(M31, M32, M33, M34);
                    case 3: return new Vector4(M41, M42, M43, M44);
                    default: throw new IndexOutOfRangeException("Matrix4 row index must be from 0-3");
                }
            }
        }

        public float this[int row, int column]
        {
            get
            {
                switch (row)
                {
                    case 0:
                        switch (column)
                        {
                            case 0: return M11;
                            case 1: return M12;
                            case 2: return M13;
                            case 3: return M14;
                            default: throw new IndexOutOfRangeException("Matrix4 row and column values must be from 0-3");
                        }
                    case 1:
                        switch (column)
                        {
                            case 0: return M21;
                            case 1: return M22;
                            case 2: return M23;
                            case 3: return M24;
                            default: throw new IndexOutOfRangeException("Matrix4 row and column values must be from 0-3");
                        }
                    case 2:
                        switch (column)
                        {
                            case 0: return M31;
                            case 1: return M32;
                            case 2: return M33;
                            case 3: return M34;
                            default: throw new IndexOutOfRangeException("Matrix4 row and column values must be from 0-3");
                        }
                    case 3:
                        switch (column)
                        {
                            case 0: return M41;
                            case 1: return M42;
                            case 2: return M43;
                            case 3: return M44;
                            default: throw new IndexOutOfRangeException("Matrix4 row and column values must be from 0-3");
                        }
                    default:
                        throw new IndexOutOfRangeException("Matrix4 row and column values must be from 0-3");
                }
            }
        }

        #endregion Operators

        /// <summary>A 4x4 matrix containing all zeroes</summary>
        public static readonly Matrix4 Zero = new Matrix4();

        /// <summary>A 4x4 identity matrix</summary>
        public static readonly Matrix4 Identity = new Matrix4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f);
    }
}
