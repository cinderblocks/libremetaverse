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
    /// <summary>
    /// An 8-bit color structure including an alpha channel
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Color4 : IComparable<Color4>, IEquatable<Color4>
    {
        /// <summary>Red</summary>
        public readonly float R;
        /// <summary>Green</summary>
        public readonly float G;
        /// <summary>Blue</summary>
        public readonly float B;
        /// <summary>Alpha</summary>
        public readonly float A;

        #region Constructors

        public Color4(byte r, byte g, byte b, byte a)
        {
            const float quanta = 1.0f / 255.0f;

            R = r * quanta;
            G = g * quanta;
            B = b * quanta;
            A = a * quanta;
        }

        public Color4(float r, float g, float b, float a)
        {
            if (r > 1f || g > 1f || b > 1f || a > 1f)
                throw new ArgumentException(
                    $"Attempting to initialize Color4 with out of range values <{r},{g},{b},{a}>");

            R = Utils.Clamp(r, 0f, 1f);
            G = Utils.Clamp(g, 0f, 1f);
            B = Utils.Clamp(b, 0f, 1f);
            A = Utils.Clamp(a, 0f, 1f);
        }

        /// <summary>
        /// Builds a color from a byte array
        /// </summary>
        /// <param name="byteArray">Byte array containing a 4 byte color</param>
        /// <param name="pos">Beginning position in the byte array</param>
        /// <param name="inverted">True if the byte array stores inverted values</param>
        public Color4(byte[] byteArray, int pos, bool inverted)
        {
            const float quanta = 1.0f / 255.0f;
            if (inverted)
            {
                R = (255 - byteArray[pos]) * quanta;
                G = (255 - byteArray[pos + 1]) * quanta;
                B = (255 - byteArray[pos + 2]) * quanta;
                A = (255 - byteArray[pos + 3]) * quanta;
            }
            else
            {
                R = byteArray[pos] * quanta;
                G = byteArray[pos + 1] * quanta;
                B = byteArray[pos + 2] * quanta;
                A = byteArray[pos + 3] * quanta;
            }
        }

        /// <summary>
        /// Builds a color from a byte array with optional alpha inversion
        /// </summary>
        public Color4(byte[] byteArray, int pos, bool inverted, bool alphaInverted)
        {
            const float quanta = 1.0f / 255.0f;
            if (inverted)
            {
                R = (255 - byteArray[pos]) * quanta;
                G = (255 - byteArray[pos + 1]) * quanta;
                B = (255 - byteArray[pos + 2]) * quanta;
                A = (255 - byteArray[pos + 3]) * quanta;
            }
            else
            {
                R = byteArray[pos] * quanta;
                G = byteArray[pos + 1] * quanta;
                B = byteArray[pos + 2] * quanta;
                A = byteArray[pos + 3] * quanta;
            }

            if (alphaInverted)
                A = 1.0f - A;
        }

        #endregion Constructors

        #region Public Methods

        /// <summary>IComparable.CompareTo implementation</summary>
        public int CompareTo(Color4 color)
        {
            var thisHue = GetHue();
            var thatHue = color.GetHue();

            if (thisHue < 0f && thatHue < 0f)
                return R == color.R ? A.CompareTo(color.A) : R.CompareTo(color.R);
            return thisHue == thatHue ? A.CompareTo(color.A) : thisHue.CompareTo(thatHue);
        }

        /// <summary>
        /// Returns a new Color4 parsed from 4 bytes starting at <paramref name="pos"/>.
        /// </summary>
        public static Color4 FromBytes(byte[] byteArray, int pos, bool inverted)
            => new Color4(byteArray, pos, inverted);

        /// <summary>
        /// Returns a new Color4 parsed from 4 bytes starting at <paramref name="pos"/>.
        /// </summary>
        public static Color4 FromBytes(byte[] byteArray, int pos, bool inverted, bool alphaInverted)
            => new Color4(byteArray, pos, inverted, alphaInverted);

        public byte[] GetBytes()
        {
            return GetBytes(false);
        }

        public byte[] GetBytes(bool inverted)
        {
            var byteArray = new byte[4];
            ToBytes(byteArray, 0, inverted);
            return byteArray;
        }

        public byte[] GetFloatBytes()
        {
            var bytes = new byte[16];
            ToFloatBytes(bytes, 0);
            return bytes;
        }

        public void ToBytes(byte[] dest, int pos)
        {
            ToBytes(dest, pos, false);
        }

        /// <summary>Serializes this color into four bytes in a byte array</summary>
        public void ToBytes(byte[] dest, int pos, bool inverted)
        {
            dest[pos + 0] = Utils.FloatToByte(R, 0f, 1f);
            dest[pos + 1] = Utils.FloatToByte(G, 0f, 1f);
            dest[pos + 2] = Utils.FloatToByte(B, 0f, 1f);
            dest[pos + 3] = Utils.FloatToByte(A, 0f, 1f);

            if (!inverted) return;

            dest[pos + 0] = (byte)(255 - dest[pos + 0]);
            dest[pos + 1] = (byte)(255 - dest[pos + 1]);
            dest[pos + 2] = (byte)(255 - dest[pos + 2]);
            dest[pos + 3] = (byte)(255 - dest[pos + 3]);
        }

        public void ToFloatBytes(byte[] dest, int pos)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(R), 0, dest, pos + 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(G), 0, dest, pos + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(B), 0, dest, pos + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(A), 0, dest, pos + 12, 4);
        }

        public float GetHue()
        {
            const float HUE_MAX = 360f;

            var max = Math.Max(Math.Max(R, G), B);
            var min = Math.Min(Math.Min(R, G), B);

            double TOLERANCE = Math.Abs(max * .00001);

            if (Math.Abs(max - min) < TOLERANCE)
                return -1f;
            if (Math.Abs(R - max) < TOLERANCE)
            {
                var bDelta = (((max - B) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                var gDelta = (((max - G) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                return bDelta - gDelta;
            }
            if (Math.Abs(G - max) < TOLERANCE)
            {
                var rDelta = (((max - R) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                var bDelta = (((max - B) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                return (HUE_MAX / 3f) + rDelta - bDelta;
            }
            else
            {
                var gDelta = (((max - G) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                var rDelta = (((max - R) * (HUE_MAX / 6f)) + ((max - min) / 2f)) / (max - min);
                return ((2f * HUE_MAX) / 3f) + gDelta - rDelta;
            }
        }

        #endregion Public Methods

        #region Static Methods

        /// <summary>Create an RGB color from a hue, saturation, value combination</summary>
        public static Color4 FromHSV(double hue, double saturation, double value)
        {
            var r = 0d;
            var g = 0d;
            var b = 0d;

            if (saturation == 0d)
            {
                r = value;
                g = value;
                b = value;
            }
            else
            {
                var sectorPos = hue / 60d;
                var sectorNumber = (int)(Math.Floor(sectorPos));
                var fractionalSector = sectorPos - sectorNumber;

                var p = value * (1d - saturation);
                var q = value * (1d - (saturation * fractionalSector));
                var t = value * (1d - (saturation * (1d - fractionalSector)));

                switch (sectorNumber)
                {
                    case 0: r = value; g = t; b = p; break;
                    case 1: r = q; g = value; b = p; break;
                    case 2: r = p; g = value; b = t; break;
                    case 3: r = p; g = q; b = value; break;
                    case 4: r = t; g = p; b = value; break;
                    case 5: r = value; g = p; b = q; break;
                }
            }

            return new Color4((float)r, (float)g, (float)b, 1f);
        }

        /// <summary>Performs linear interpolation between two colors</summary>
        public static Color4 Lerp(Color4 value1, Color4 value2, float amount)
        {
            return new Color4(
                Utils.Clamp(Utils.Lerp(value1.R, value2.R, amount), 0f, 1f),
                Utils.Clamp(Utils.Lerp(value1.G, value2.G, amount), 0f, 1f),
                Utils.Clamp(Utils.Lerp(value1.B, value2.B, amount), 0f, 1f),
                Utils.Clamp(Utils.Lerp(value1.A, value2.A, amount), 0f, 1f));
        }

        #endregion Static Methods

        #region Overrides

        public override string ToString()
        {
            return string.Format(Utils.EnUsCulture, "<{0}, {1}, {2}, {3}>", R, G, B, A);
        }

        public string ToRGBString()
        {
            return string.Format(Utils.EnUsCulture, "<{0}, {1}, {2}>", R, G, B);
        }

        public override bool Equals(object? obj)
        {
            return (obj is Color4 color4) && this == color4;
        }

        public bool Equals(Color4 other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return R.GetHashCode() ^ G.GetHashCode() ^ B.GetHashCode() ^ A.GetHashCode();
        }

        #endregion Overrides

        #region Operators

        public static bool operator ==(Color4 lhs, Color4 rhs)
        {
            return (lhs.R == rhs.R) && (lhs.G == rhs.G) && (lhs.B == rhs.B) && (lhs.A == rhs.A);
        }

        public static bool operator !=(Color4 lhs, Color4 rhs)
        {
            return !(lhs == rhs);
        }

        public static Color4 operator +(Color4 lhs, Color4 rhs)
        {
            return new Color4(
                Utils.Clamp(lhs.R + rhs.R, 0f, 1f),
                Utils.Clamp(lhs.G + rhs.G, 0f, 1f),
                Utils.Clamp(lhs.B + rhs.B, 0f, 1f),
                Utils.Clamp(lhs.A + rhs.A, 0f, 1f));
        }

        public static Color4 operator -(Color4 lhs, Color4 rhs)
        {
            return new Color4(
                Utils.Clamp(lhs.R - rhs.R, 0f, 1f),
                Utils.Clamp(lhs.G - rhs.G, 0f, 1f),
                Utils.Clamp(lhs.B - rhs.B, 0f, 1f),
                Utils.Clamp(lhs.A - rhs.A, 0f, 1f));
        }

        public static Color4 operator *(Color4 lhs, Color4 rhs)
        {
            return new Color4(
                Utils.Clamp(lhs.R * rhs.R, 0f, 1f),
                Utils.Clamp(lhs.G * rhs.G, 0f, 1f),
                Utils.Clamp(lhs.B * rhs.B, 0f, 1f),
                Utils.Clamp(lhs.A * rhs.A, 0f, 1f));
        }

        #endregion Operators

        /// <summary>A Color4 with zero RGB values and fully opaque (alpha 1.0)</summary>
        public static readonly Color4 Black = new Color4(0f, 0f, 0f, 1f);

        /// <summary>A Color4 with full RGB values (1.0) and fully opaque (alpha 1.0)</summary>
        public static readonly Color4 White = new Color4(1f, 1f, 1f, 1f);
    }
}
