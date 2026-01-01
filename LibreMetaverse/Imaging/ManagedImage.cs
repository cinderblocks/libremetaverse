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
using SkiaSharp;
using System.Runtime.InteropServices;

namespace OpenMetaverse.Imaging
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
        public byte[] Red;
        
        /// <summary>
        /// Green channel data
        /// </summary>
        public byte[] Green;
        
        /// <summary>
        /// Blue channel data
        /// </summary>
        public byte[] Blue;

        /// <summary>
        /// Alpha channel data
        /// </summary>
        public byte[] Alpha;
        
        /// <summary>
        /// Bump channel data
        /// </summary>
        public byte[] Bump;

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
        /// Converts an array of 32-bit color values to an array of 8-bit color values
        /// </summary>
        /// <param name="sourceData">The input array of 32-bit color values</param>
        /// <param name="destinationData">The output array of 8-bit color values</param>
        private static void ConvertTo8BitChannel(int[] sourceData, out byte[] destinationData)
        {
            destinationData = new byte[sourceData.Length];
            for (var i = 0; i < sourceData.Length; i++)
            {
                int v = sourceData[i];
                if (v < 0) { v = 0; }
                else if (v > 255) { v = 255; }
                destinationData[i] = (byte)v;
            }
        }

        /// <summary>
        /// Constructs ManagedImage class from <see cref=InterleavedImage"/>
        /// Currently only supporting 8-bit channels;
        /// </summary>
        /// <param name="image">Input <see cref="InterleavedImage"/></param>
        public ManagedImage(InterleavedImage image)
        {
            Width = image.Width;
            Height = image.Height;

            var numComp = image.NumberOfComponents;
            switch (numComp)
            {
                case 1:
                    Channels = ImageChannels.Gray;
                    ConvertTo8BitChannel(image.GetComponent(0), out Red);
                    break;
                case 2:
                    Channels = ImageChannels.Gray | ImageChannels.Alpha;
                    ConvertTo8BitChannel(image.GetComponent(0), out Red);
                    ConvertTo8BitChannel(image.GetComponent(1), out Alpha);
                    break;
                case 3:
                    Channels = ImageChannels.Color;
                    ConvertTo8BitChannel(image.GetComponent(0), out Red);
                    ConvertTo8BitChannel(image.GetComponent(1), out Green);
                    ConvertTo8BitChannel(image.GetComponent(2), out Blue);
                    break;
                case 4:
                    Channels = ImageChannels.Alpha | ImageChannels.Color;
                    ConvertTo8BitChannel(image.GetComponent(0), out Red);
                    ConvertTo8BitChannel(image.GetComponent(1), out Green);
                    ConvertTo8BitChannel(image.GetComponent(2), out Blue);
                    ConvertTo8BitChannel(image.GetComponent(3), out Alpha);
                    break;
                case 5:
                    Channels = ImageChannels.Alpha | ImageChannels.Color | ImageChannels.Bump;
                    ConvertTo8BitChannel(image.GetComponent(0), out Red);
                    ConvertTo8BitChannel(image.GetComponent(1), out Green);
                    ConvertTo8BitChannel(image.GetComponent(2), out Blue);
                    ConvertTo8BitChannel(image.GetComponent(3), out Bump);
                    ConvertTo8BitChannel(image.GetComponent(4), out Alpha);
                    break;
            }
        }

        /// <summary>
        /// Constructs ManagedImage class from <see cref="SKBitmap"/>
        /// </summary>
        /// <param name="bitmap">Input <see cref="SKBitmap"/></param>
        public ManagedImage(SKBitmap bitmap)
        {
            Width = bitmap.Width;
            Height = bitmap.Height;
            var pixelCount = Width * Height;

            // Access pixel buffer via pointer and respect RowBytes to handle padding
            IntPtr basePtr = bitmap.GetPixels();
            if (basePtr == IntPtr.Zero)
                throw new NotSupportedException("Unable to access SKBitmap pixel buffer on this platform.");

            int rowBytes = bitmap.RowBytes;
            int bytesPerPixel = rowBytes / Math.Max(1, Width);

            switch (bitmap.ColorType)
            {
                case SKColorType.Rgb565:
                    // 16-bit RGB 5-6-5
                    Channels = ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                byte* p = row + x * 2;
                                int i = y * Width + x;
                                ushort v = (ushort)(p[0] | (p[1] << 8));
                                int r5 = (v >> 11) & 0x1F;
                                int g6 = (v >> 5) & 0x3F;
                                int b5 = v & 0x1F;
                                Red[i] = (byte)((r5 * 255 + 15) / 31);
                                Green[i] = (byte)((g6 * 255 + 31) / 63);
                                Blue[i] = (byte)((b5 * 255 + 15) / 31);
                            }
                        }
                    }
                    break;

                case SKColorType.Bgra8888:
                    Channels = ImageChannels.Alpha | ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                byte* p = row + x * bytesPerPixel;
                                int i = y * Width + x;
                                // For 8-bit BGRA layout this maps directly. For 10-bit packed formats
                                // the 1010102 formats are handled in the combined 1010102 case below.
                                Blue[i] = p[0];
                                Green[i] = p[1];
                                Red[i] = p[2];
                                Alpha[i] = p[3];
                            }
                        }
                    }
                    break;

                case SKColorType.Rgba8888:
                    Channels = ImageChannels.Alpha | ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                byte* p = row + x * bytesPerPixel;
                                int i = y * Width + x;
                                Red[i] = p[0];
                                Green[i] = p[1];
                                Blue[i] = p[2];
                                Alpha[i] = p[3];
                            }
                        }
                    }
                    break;

                case SKColorType.Rgba1010102:
                case SKColorType.Bgra1010102:
                    // 10-bit per channel formats (packed into 4 bytes). Handle both Rgba1010102 and Bgra1010102
                    Channels = ImageChannels.Alpha | ImageChannels.Color;
                    Red = new byte[pixelCount];
                    Green = new byte[pixelCount];
                    Blue = new byte[pixelCount];
                    Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        bool isRgbaOrder = bitmap.ColorType == SKColorType.Rgba1010102;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                byte* p = row + x * 4;
                                int i = y * Width + x;
                                uint v = (uint)(p[0] | (p[1] << 8) | (p[2] << 16) | (p[3] << 24));

                                if (isRgbaOrder)
                                {
                                    int r10 = (int)(v & 0x3FF);
                                    int g10 = (int)((v >> 10) & 0x3FF);
                                    int b10 = (int)((v >> 20) & 0x3FF);
                                    int a2 = (int)((v >> 30) & 0x3);
                                    Red[i] = (byte)((r10 * 255 + 511) / 1023);
                                    Green[i] = (byte)((g10 * 255 + 511) / 1023);
                                    Blue[i] = (byte)((b10 * 255 + 511) / 1023);
                                    Alpha[i] = (byte)(a2 * 85); // map 0..3 -> 0,85,170,255
                                }
                                else
                                {
                                    int b10 = (int)(v & 0x3FF);
                                    int g10 = (int)((v >> 10) & 0x3FF);
                                    int r10 = (int)((v >> 20) & 0x3FF);
                                    int a2 = (int)((v >> 30) & 0x3);
                                    Red[i] = (byte)((r10 * 255 + 511) / 1023);
                                    Green[i] = (byte)((g10 * 255 + 511) / 1023);
                                    Blue[i] = (byte)((b10 * 255 + 511) / 1023);
                                    Alpha[i] = (byte)(a2 * 85);
                                }
                            }
                        }
                    }
                    break;

                case SKColorType.Gray8:
                    Channels = ImageChannels.Gray;
                    Red = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                int i = y * Width + x;
                                Red[i] = row[x * bytesPerPixel];
                            }
                        }
                    }
                    break;

                case SKColorType.Alpha8:
                    Channels = ImageChannels.Alpha;
                    Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < Height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < Width; x++)
                            {
                                int i = y * Width + x;
                                Alpha[i] = row[x * bytesPerPixel];
                            }
                        }
                    }
                    break;

                default:
                    // Fallback: use bytesPerPixel and assume BGR(A) or Gray layout
                    var bpp = bytesPerPixel;
                    if (bpp == 4)
                    {
                        Channels = ImageChannels.Alpha | ImageChannels.Color;
                        Red = new byte[pixelCount];
                        Green = new byte[pixelCount];
                        Blue = new byte[pixelCount];
                        Alpha = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < Height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < Width; x++)
                                {
                                    byte* p = row + x * bpp;
                                    int i = y * Width + x;
                                    Blue[i] = p[0];
                                    Green[i] = p[1];
                                    Red[i] = p[2];
                                    Alpha[i] = p[3];
                                }
                            }
                        }
                    }
                    else if (bpp == 3)
                    {
                        Channels = ImageChannels.Color;
                        Red = new byte[pixelCount];
                        Green = new byte[pixelCount];
                        Blue = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < Height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < Width; x++)
                                {
                                    byte* p = row + x * bpp;
                                    int i = y * Width + x;
                                    Blue[i] = p[0];
                                    Green[i] = p[1];
                                    Red[i] = p[2];
                                }
                            }
                        }
                    }
                    else if (bpp == 1)
                    {
                        Channels = ImageChannels.Gray;
                        Red = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < Height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < Width; x++)
                                {
                                    int i = y * Width + x;
                                    Red[i] = row[x];
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Pixel format {bitmap.ColorType} (bytesPerPixel={bpp}) is not supported.");
                    }
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
                Red = null;
                Green = null;
                Blue = null;
            }

            if ((add & ImageChannels.Alpha) != 0)
            {
                Alpha = new byte[n];
                FillArray(Alpha, 255);
            }
            else if ((del & ImageChannels.Alpha) != 0)
            {
                Alpha = null;
            }

            if ((add & ImageChannels.Bump) != 0)
            {
                Bump = new byte[n];
            }
            else if ((del & ImageChannels.Bump) != 0)
            {
                Bump = null;
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

            byte[]
                red = null, 
                green = null, 
                blue = null, 
                alpha = null, 
                bump = null;
            int n = width * height;
            int di = 0;

            if (Red != null) red = new byte[n];
            if (Green != null) green = new byte[n];
            if (Blue != null) blue = new byte[n];
            if (Alpha != null) alpha = new byte[n];
            if (Bump != null) bump = new byte[n];
            
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

                    if (Red != null) red[di] = Red[si];
                    if (Green != null) green[di] = Green[si];
                    if (Blue != null) blue[di] = Blue[si];
                    if (Alpha != null) alpha[di] = Alpha[si];
                    if (Bump != null) bump[di] = Bump[si];
                    di++;
                }
            }

            Width = width;
            Height = height;
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
            Bump = bump;
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

        /// <summary>
        /// Create <see cref="SKBitmap"/> from ManagedImage
        /// </summary>
        /// <returns><see cref="SKBitmap"/></returns>
        public SKBitmap ExportBitmap()
        {
            var raw = new byte[Width * Height * 4];

            if ((Channels & ImageChannels.Alpha) != 0)
            {
                if ((Channels & ImageChannels.Color) != 0)
                {
                    // RGBA -> BGRA
                    for (var pos = 0; pos < Height * Width; pos++)
                    {
                        raw[pos * 4 + 0] = Blue[pos];
                        raw[pos * 4 + 1] = Green[pos];
                        raw[pos * 4 + 2] = Red[pos];
                        raw[pos * 4 + 3] = Alpha[pos];
                    }
                }
                else
                {
                    // Alpha only -> replicate to RGB, full alpha
                    for (var pos = 0; pos < Height * Width; pos++)
                    {
                        raw[pos * 4 + 0] = Alpha[pos];
                        raw[pos * 4 + 1] = Alpha[pos];
                        raw[pos * 4 + 2] = Alpha[pos];
                        raw[pos * 4 + 3] = byte.MaxValue;
                    }
                }
            }
            else
            {
                // RGB -> BGRA
                for (var pos = 0; pos < Height * Width; pos++)
                {
                    raw[pos * 4 + 0] = Blue[pos];
                    raw[pos * 4 + 1] = Green[pos];
                    raw[pos * 4 + 2] = Red[pos];
                    raw[pos * 4 + 3] = byte.MaxValue;
                }
            }

            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var bmp = new SKBitmap(info);

            IntPtr ptr = bmp.GetPixels();
            if (ptr == IntPtr.Zero)
            {
                throw new NotSupportedException("Unable to access SKBitmap pixel buffer on this platform.");
            }

            // Copy raw BGRA bytes directly into bitmap buffer
            Marshal.Copy(raw, 0, ptr, raw.Length);

            return bmp;
        }

        [Obsolete("ExportTGA() is deprecated, please use Targa.Encode() instead.")]
        public byte[] ExportTGA()
        {
            byte[] tga = new byte[Width * Height * ((Channels & ImageChannels.Alpha) == 0 ? 3 : 4) + 32];
            int di = 0;
            tga[di++] = 0; // idlength
            tga[di++] = 0; // colormaptype = 0: no colormap
            tga[di++] = 2; // image type = 2: uncompressed RGB
            tga[di++] = 0; // color map spec is five zeroes for no color map
            tga[di++] = 0; // color map spec is five zeroes for no color map
            tga[di++] = 0; // color map spec is five zeroes for no color map
            tga[di++] = 0; // color map spec is five zeroes for no color map
            tga[di++] = 0; // color map spec is five zeroes for no color map
            tga[di++] = 0; // x origin = two bytes
            tga[di++] = 0; // x origin = two bytes
            tga[di++] = 0; // y origin = two bytes
            tga[di++] = 0; // y origin = two bytes
            tga[di++] = (byte)(Width & 0xFF); // width - low byte
            tga[di++] = (byte)(Width >> 8); // width - hi byte
            tga[di++] = (byte)(Height & 0xFF); // height - low byte
            tga[di++] = (byte)(Height >> 8); // height - hi byte
            tga[di++] = (byte)((Channels & ImageChannels.Alpha) == 0 ? 24 : 32); // 24/32 bits per pixel
            tga[di++] = (byte)((Channels & ImageChannels.Alpha) == 0 ? 32 : 40); // image descriptor byte

            int n = Width * Height;

            if ((Channels & ImageChannels.Alpha) != 0)
            {
                if ((Channels & ImageChannels.Color) != 0)
                {
                    // RGBA
                    for (int i = 0; i < n; i++)
                    {
                        tga[di++] = Blue[i];
                        tga[di++] = Green[i];
                        tga[di++] = Red[i];
                        tga[di++] = Alpha[i];
                    }
                }
                else
                {
                    // Alpha only
                    for (int i = 0; i < n; i++)
                    {
                        tga[di++] = Alpha[i];
                        tga[di++] = Alpha[i];
                        tga[di++] = Alpha[i];
                        tga[di++] = byte.MaxValue;
                    }
                }
            }
            else
            {
                // RGB
                for (int i = 0; i < n; i++)
                {
                    tga[di++] = Blue[i];
                    tga[di++] = Green[i];
                    tga[di++] = Red[i];
                }
            }

            return tga;
        }

        private static void FillArray(byte[] array, byte value)
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
