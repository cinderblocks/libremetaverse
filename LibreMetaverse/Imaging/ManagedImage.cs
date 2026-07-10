/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024-2025, Sjofn LLC.
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
using CoreJ2K.Util;

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
        /// Constructs ManagedImage class from <see cref="InterleavedImage"/>
        /// Currently only supporting 8-bit channels;
        /// </summary>
        /// <param name="image">Input <see cref="InterleavedImage"/></param>
        public ManagedImage(InterleavedImage image)
        {
            Width = image.Width;
            Height = image.Height;

            var pixelCount = Width * Height;
            var numComp = image.NumberOfComponents;
            switch (numComp)
            {
                case 1:
                    Channels = ImageChannels.Gray;
                    Red = new byte[pixelCount];
                    image.ToComponentBytes(0, Red);
                    break;
                case 2:
                    Channels = ImageChannels.Gray | ImageChannels.Alpha;
                    Red = new byte[pixelCount];
                    Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, Red);
                    image.ToComponentBytes(1, Alpha);
                    break;
                case 3:
                    Channels = ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    image.ToComponentBytes(0, Red);
                    image.ToComponentBytes(1, Green);
                    image.ToComponentBytes(2, Blue);
                    break;
                case 4:
                    Channels = ImageChannels.Alpha | ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, Red);
                    image.ToComponentBytes(1, Green);
                    image.ToComponentBytes(2, Blue);
                    image.ToComponentBytes(3, Alpha);
                    break;
                case 5:
                    Channels = ImageChannels.Alpha | ImageChannels.Color | ImageChannels.Bump;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    Bump = new byte[pixelCount];
                    Alpha = new byte[pixelCount];
                    image.ToComponentBytes(0, Red);
                    image.ToComponentBytes(1, Green);
                    image.ToComponentBytes(2, Blue);
                    image.ToComponentBytes(3, Bump);
                    image.ToComponentBytes(4, Alpha);
                    break;
            }
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
