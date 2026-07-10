/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024-2026, Sjofn LLC.
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

namespace LibreMetaverse.Imaging
{
    public class ManagedImage
    {
        [Flags]
        public enum ImageChannels
        {
            Gray = 1,
            Color = 2,
            Alpha = 4,
            Bump = 8
        }

        /// <summary>
        /// Image width
        /// </summary>
        public int Width;

        /// <summary>
        /// Image height
        /// </summary>
        public int Height;

        /// <summary>
        /// Image channel flags
        /// </summary>
        public ImageChannels Channels;

        /// <summary>
        /// Red channel data
        /// </summary>
        public byte[] Red = Array.Empty<byte>();

        /// <summary>
        /// Green channel data
        /// </summary>
        public byte[] Green = Array.Empty<byte>();

        /// <summary>
        /// Blue channel data
        /// </summary>
        public byte[] Blue = Array.Empty<byte>();

        /// <summary>
        /// Alpha channel data
        /// </summary>
        public byte[] Alpha = Array.Empty<byte>();

        /// <summary>
        /// Bump channel data
        /// </summary>
        public byte[] Bump = Array.Empty<byte>();

        /// <summary>
        /// Create a new blank image
        /// </summary>
        /// <param name="width">width</param>
        /// <param name="height">height</param>
        /// <param name="channels">channel flags</param>
        public ManagedImage(int width, int height, ImageChannels channels)
        {
            Width = width;
            Height = height;
            Channels = channels;

            int n = width * height;

            if ((channels & ImageChannels.Gray) != 0)
            {
                Red = new byte[n];
            }
            else if ((channels & ImageChannels.Color) != 0)
            {
                Red = new byte[n];
                Green = new byte[n];
                Blue = new byte[n];
            }

            if ((channels & ImageChannels.Alpha) != 0)
                Alpha = new byte[n];

            if ((channels & ImageChannels.Bump) != 0)
                Bump = new byte[n];
        }

        /// <summary>
        /// Convert the channels in the image. Channels are created or destroyed as required.
        /// </summary>
        /// <param name="channels">new channel flags</param>
        public void ConvertChannels(ImageChannels channels)
        {
            if (Channels == channels)
                return;

            int n = Width * Height;
            ImageChannels add = (Channels ^ channels) & channels;
            ImageChannels del = (Channels ^ channels) & Channels;

            if ((add & ImageChannels.Color) != 0)
            {
                Red = new byte[n];
                Green = new byte[n];
                Blue = new byte[n];
            }
            else if ((del & ImageChannels.Color) != 0)
            {
                Red = Array.Empty<byte>();
                Green = Array.Empty<byte>();
                Blue = Array.Empty<byte>();
            }

            if ((add & ImageChannels.Alpha) != 0)
            {
                Alpha = new byte[n];
                FillArray(Alpha, 255);
            }
            else if ((del & ImageChannels.Alpha) != 0)
            {
                Alpha = Array.Empty<byte>();
            }

            if ((add & ImageChannels.Bump) != 0)
            {
                Bump = new byte[n];
            }
            else if ((del & ImageChannels.Bump) != 0)
            {
                Bump = Array.Empty<byte>();
            }

            Channels = channels;
        }

        /// <summary>
        /// Resize or stretch the image using nearest neighbor (ugly) resampling
        /// </summary>
        /// <param name="width">new width</param>
        /// <param name="height">new height</param>
        public void ResizeNearestNeighbor(int width, int height)
        {
            if (width == Width && height == Height)
                return;

            byte[]? red = null, green = null, blue = null, alpha = null, bump = null;
            int n = width * height;
            int di = 0;
            // Allocate target channel buffers based on current channel flags
            if ((Channels & ImageChannels.Gray) != 0)
            {
                red = new byte[n];
            }
            else if ((Channels & ImageChannels.Color) != 0)
            {
                red = new byte[n];
                green = new byte[n];
                blue = new byte[n];
            }

            if ((Channels & ImageChannels.Alpha) != 0)
                alpha = new byte[n];

            if ((Channels & ImageChannels.Bump) != 0)
                bump = new byte[n];

            for (int y = 0; y < height; y++)
            {
                int srcY = (y * Height) / Math.Max(1, height); // compute source row
                for (int x = 0; x < width; x++)
                {
                    int srcX = (x * Width) / Math.Max(1, width); // compute source column
                    int si = srcY * Width + srcX;
                    // bounds guard
                    if (si < 0) si = 0;
                    else if (si >= Width * Height) si = Width * Height - 1;

                    if ((Channels & ImageChannels.Color) != 0 || (Channels & ImageChannels.Gray) != 0) red![di] = Red[si];
                    if ((Channels & ImageChannels.Color) != 0) green![di] = Green[si];
                    if ((Channels & ImageChannels.Color) != 0) blue![di] = Blue[si];
                    if ((Channels & ImageChannels.Alpha) != 0) alpha![di] = Alpha[si];
                    if ((Channels & ImageChannels.Bump) != 0) bump![di] = Bump[si];
                    di++;
                }
            }

            Width = width;
            Height = height;
            Red = red ?? Array.Empty<byte>();
            Green = green ?? Array.Empty<byte>();
            Blue = blue ?? Array.Empty<byte>();
            Alpha = alpha ?? Array.Empty<byte>();
            Bump = bump ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Resize or stretch the image using bilinear interpolation. Falls back to nearest
        /// neighbor when the source is too small to interpolate (a single row or column).
        /// </summary>
        /// <param name="width">new width</param>
        /// <param name="height">new height</param>
        public void ResizeBilinear(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (width == Width && height == Height)
                return;
            if (Width <= 1 || Height <= 1)
            {
                ResizeNearestNeighbor(width, height);
                return;
            }

            bool hasGray = (Channels & ImageChannels.Gray) != 0;
            bool hasColor = (Channels & ImageChannels.Color) != 0;
            bool hasAlpha = (Channels & ImageChannels.Alpha) != 0;
            bool hasBump = (Channels & ImageChannels.Bump) != 0;

            int n = width * height;
            byte[]? red = null, green = null, blue = null, alpha = null, bump = null;
            if (hasGray) red = new byte[n];
            else if (hasColor)
            {
                red = new byte[n];
                green = new byte[n];
                blue = new byte[n];
            }

            if (hasAlpha) alpha = new byte[n];
            if (hasBump) bump = new byte[n];

            float xScale = (float)(Width - 1) / Math.Max(1, width - 1);
            float yScale = (float)(Height - 1) / Math.Max(1, height - 1);

            int di = 0;
            for (int y = 0; y < height; y++)
            {
                float srcY = height > 1 ? y * yScale : 0f;
                for (int x = 0; x < width; x++)
                {
                    float srcX = width > 1 ? x * xScale : 0f;

                    if (hasGray)
                    {
                        red![di] = BilinearSample(Red, Width, Height, srcX, srcY);
                    }
                    else if (hasColor)
                    {
                        red![di] = BilinearSample(Red, Width, Height, srcX, srcY);
                        green![di] = BilinearSample(Green, Width, Height, srcX, srcY);
                        blue![di] = BilinearSample(Blue, Width, Height, srcX, srcY);
                    }

                    if (hasAlpha) alpha![di] = BilinearSample(Alpha, Width, Height, srcX, srcY);
                    if (hasBump) bump![di] = BilinearSample(Bump, Width, Height, srcX, srcY);
                    di++;
                }
            }

            Width = width;
            Height = height;
            Red = red ?? Array.Empty<byte>();
            Green = green ?? Array.Empty<byte>();
            Blue = blue ?? Array.Empty<byte>();
            Alpha = alpha ?? Array.Empty<byte>();
            Bump = bump ?? Array.Empty<byte>();
        }

        private static byte BilinearSample(byte[] src, int srcWidth, int srcHeight, float srcX, float srcY)
        {
            int x0 = (int)srcX;
            int y0 = (int)srcY;
            int x1 = Math.Min(x0 + 1, srcWidth - 1);
            int y1 = Math.Min(y0 + 1, srcHeight - 1);
            float fx = srcX - x0;
            float fy = srcY - y0;

            float top = src[y0 * srcWidth + x0] * (1 - fx) + src[y0 * srcWidth + x1] * fx;
            float bottom = src[y1 * srcWidth + x0] * (1 - fx) + src[y1 * srcWidth + x1] * fx;
            float value = top * (1 - fy) + bottom * fy;
            if (value < 0f) value = 0f;
            else if (value > 255f) value = 255f;
            return (byte)Math.Round(value);
        }

        /// <summary>
        /// Create a byte array containing 32-bit RGBA data with a bottom-left
        /// origin, suitable for feeding directly into OpenGL
        /// </summary>
        /// <returns>A byte array containing raw texture data</returns>
        public byte[] ExportRaw()
        {
            byte[] raw = new byte[Width * Height * 4];

            if ((Channels & ImageChannels.Alpha) != 0)
            {
                if ((Channels & ImageChannels.Color) != 0)
                {
                    // RGBA
                    for (int h = 0; h < Height; h++)
                    {
                        for (int w = 0; w < Width; w++)
                        {
                            int pos = (Height - 1 - h) * Width + w;
                            int srcPos = h * Width + w;

                            raw[pos * 4 + 0] = Red[srcPos];
                            raw[pos * 4 + 1] = Green[srcPos];
                            raw[pos * 4 + 2] = Blue[srcPos];
                            raw[pos * 4 + 3] = Alpha[srcPos];
                        }
                    }
                }
                else
                {
                    // Alpha only
                    for (int h = 0; h < Height; h++)
                    {
                        for (int w = 0; w < Width; w++)
                        {
                            int pos = (Height - 1 - h) * Width + w;
                            int srcPos = h * Width + w;

                            raw[pos * 4 + 0] = Alpha[srcPos];
                            raw[pos * 4 + 1] = Alpha[srcPos];
                            raw[pos * 4 + 2] = Alpha[srcPos];
                            raw[pos * 4 + 3] = byte.MaxValue;
                        }
                    }
                }
            }
            else
            {
                // RGB
                for (int h = 0; h < Height; h++)
                {
                    for (int w = 0; w < Width; w++)
                    {
                        int pos = (Height - 1 - h) * Width + w;
                        int srcPos = h * Width + w;

                        raw[pos * 4 + 0] = Red[srcPos];
                        raw[pos * 4 + 1] = Green[srcPos];
                        raw[pos * 4 + 2] = Blue[srcPos];
                        raw[pos * 4 + 3] = byte.MaxValue;
                    }
                }
            }

            return raw;
        }

        private static void FillArray(byte[]? array, byte value)
        {
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = value;
            }
        }

        public void Clear()
        {
            FillArray(Red, 0);
            FillArray(Green, 0);
            FillArray(Blue, 0);
            FillArray(Alpha, 0);
            FillArray(Bump, 0);
        }

        public ManagedImage Clone()
        {
            ManagedImage image = new ManagedImage(Width, Height, Channels);
            if (Red != null) image.Red = (byte[])Red.Clone();
            if (Green != null) image.Green = (byte[])Green.Clone();
            if (Blue != null) image.Blue = (byte[])Blue.Clone();
            if (Alpha != null) image.Alpha = (byte[])Alpha.Clone();
            if (Bump != null) image.Bump = (byte[])Bump.Clone();
            return image;
        }
    }
}
