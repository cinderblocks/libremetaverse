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
using System.IO;
using LibreMetaverse.Imaging;
using SkiaSharp;

namespace LibreMetaverse.Imaging.Skia
{
    /// <summary>
    /// <see cref="ITextureCodec"/> implementation backed by SkiaSharp. Decodes arbitrary
    /// compressed image formats (PNG, JPEG, BMP, GIF, WebP, etc.) into a <see cref="ManagedImage"/>.
    /// </summary>
    public sealed class SkiaTextureCodec : ITextureCodec
    {
        public ManagedImage Decode(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
            {
                throw new InvalidOperationException("SkiaSharp was unable to decode the image data.");
            }

            return ToManagedImage(bitmap);
        }

        /// <summary>
        /// Converts an already-decoded <see cref="SKBitmap"/> into a <see cref="ManagedImage"/>.
        /// </summary>
        public static ManagedImage ToManagedImage(SKBitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

            // Decoded bitmaps are commonly premultiplied; reading their raw bytes directly would
            // give color values scaled down by alpha. Normalize to straight (unpremultiplied)
            // alpha first so the channel values below are the true, un-scaled colors.
            SKBitmap? converted = null;
            if (bitmap.AlphaType == SKAlphaType.Premul)
            {
                var unpremulInfo = new SKImageInfo(bitmap.Width, bitmap.Height, bitmap.ColorType, SKAlphaType.Unpremul);
                converted = new SKBitmap(unpremulInfo);
                using var srcPixmap = bitmap.PeekPixels();
                if (srcPixmap == null || !srcPixmap.ReadPixels(unpremulInfo, converted.GetPixels(), unpremulInfo.RowBytes))
                {
                    converted.Dispose();
                    converted = null;
                }
                else
                {
                    bitmap = converted;
                }
            }

            try
            {
                return ToManagedImageCore(bitmap);
            }
            finally
            {
                converted?.Dispose();
            }
        }

        private static ManagedImage ToManagedImageCore(SKBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var pixelCount = width * height;

            IntPtr basePtr = bitmap.GetPixels();
            if (basePtr == IntPtr.Zero)
                throw new NotSupportedException("Unable to access SKBitmap pixel buffer on this platform.");

            int rowBytes = bitmap.RowBytes;
            int bytesPerPixel = rowBytes / Math.Max(1, width);

            var image = new ManagedImage(width, height, 0);

            switch (bitmap.ColorType)
            {
                case SKColorType.Rgb565:
                    // 16-bit RGB 5-6-5
                    image.Channels = ManagedImage.ImageChannels.Color;
                    image.Red = new byte[pixelCount];
                    image.Green = new byte[pixelCount];
                    image.Blue = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                byte* p = row + x * 2;
                                int i = y * width + x;
                                ushort v = (ushort)(p[0] | (p[1] << 8));
                                int r5 = (v >> 11) & 0x1F;
                                int g6 = (v >> 5) & 0x3F;
                                int b5 = v & 0x1F;
                                image.Red[i] = (byte)((r5 * 255 + 15) / 31);
                                image.Green[i] = (byte)((g6 * 255 + 31) / 63);
                                image.Blue[i] = (byte)((b5 * 255 + 15) / 31);
                            }
                        }
                    }
                    break;

                case SKColorType.Bgra8888:
                    image.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color;
                    image.Red = new byte[pixelCount];
                    image.Green = new byte[pixelCount];
                    image.Blue = new byte[pixelCount];
                    image.Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                byte* p = row + x * bytesPerPixel;
                                int i = y * width + x;
                                // For 8-bit BGRA layout this maps directly. For 10-bit packed formats
                                // the 1010102 formats are handled in the combined 1010102 case below.
                                image.Blue[i] = p[0];
                                image.Green[i] = p[1];
                                image.Red[i] = p[2];
                                image.Alpha[i] = p[3];
                            }
                        }
                    }
                    break;

                case SKColorType.Rgba8888:
                    image.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color;
                    image.Red = new byte[pixelCount];
                    image.Green = new byte[pixelCount];
                    image.Blue = new byte[pixelCount];
                    image.Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                byte* p = row + x * bytesPerPixel;
                                int i = y * width + x;
                                image.Red[i] = p[0];
                                image.Green[i] = p[1];
                                image.Blue[i] = p[2];
                                image.Alpha[i] = p[3];
                            }
                        }
                    }
                    break;

                case SKColorType.Rgba1010102:
                case SKColorType.Bgra1010102:
                    // 10-bit per channel formats (packed into 4 bytes). Handle both Rgba1010102 and Bgra1010102
                    image.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color;
                    image.Red = new byte[pixelCount];
                    image.Green = new byte[pixelCount];
                    image.Blue = new byte[pixelCount];
                    image.Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        bool isRgbaOrder = bitmap.ColorType == SKColorType.Rgba1010102;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                byte* p = row + x * 4;
                                int i = y * width + x;
                                uint v = (uint)(p[0] | (p[1] << 8) | (p[2] << 16) | (p[3] << 24));

                                if (isRgbaOrder)
                                {
                                    int r10 = (int)(v & 0x3FF);
                                    int g10 = (int)((v >> 10) & 0x3FF);
                                    int b10 = (int)((v >> 20) & 0x3FF);
                                    int a2 = (int)((v >> 30) & 0x3);
                                    image.Red[i] = (byte)((r10 * 255 + 511) / 1023);
                                    image.Green[i] = (byte)((g10 * 255 + 511) / 1023);
                                    image.Blue[i] = (byte)((b10 * 255 + 511) / 1023);
                                    image.Alpha[i] = (byte)(a2 * 85); // map 0..3 -> 0,85,170,255
                                }
                                else
                                {
                                    int b10 = (int)(v & 0x3FF);
                                    int g10 = (int)((v >> 10) & 0x3FF);
                                    int r10 = (int)((v >> 20) & 0x3FF);
                                    int a2 = (int)((v >> 30) & 0x3);
                                    image.Red[i] = (byte)((r10 * 255 + 511) / 1023);
                                    image.Green[i] = (byte)((g10 * 255 + 511) / 1023);
                                    image.Blue[i] = (byte)((b10 * 255 + 511) / 1023);
                                    image.Alpha[i] = (byte)(a2 * 85);
                                }
                            }
                        }
                    }
                    break;

                case SKColorType.Gray8:
                    image.Channels = ManagedImage.ImageChannels.Gray;
                    image.Red = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                int i = y * width + x;
                                image.Red[i] = row[x * bytesPerPixel];
                            }
                        }
                    }
                    break;

                case SKColorType.Alpha8:
                    image.Channels = ManagedImage.ImageChannels.Alpha;
                    image.Alpha = new byte[pixelCount];

                    unsafe
                    {
                        byte* start = (byte*)basePtr;

                        for (int y = 0; y < height; y++)
                        {
                            byte* row = start + y * rowBytes;
                            for (int x = 0; x < width; x++)
                            {
                                int i = y * width + x;
                                image.Alpha[i] = row[x * bytesPerPixel];
                            }
                        }
                    }
                    break;

                default:
                    // Fallback: use bytesPerPixel and assume BGR(A) or Gray layout
                    var bpp = bytesPerPixel;
                    if (bpp == 4)
                    {
                        image.Channels = ManagedImage.ImageChannels.Alpha | ManagedImage.ImageChannels.Color;
                        image.Red = new byte[pixelCount];
                        image.Green = new byte[pixelCount];
                        image.Blue = new byte[pixelCount];
                        image.Alpha = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < width; x++)
                                {
                                    byte* p = row + x * bpp;
                                    int i = y * width + x;
                                    image.Blue[i] = p[0];
                                    image.Green[i] = p[1];
                                    image.Red[i] = p[2];
                                    image.Alpha[i] = p[3];
                                }
                            }
                        }
                    }
                    else if (bpp == 3)
                    {
                        image.Channels = ManagedImage.ImageChannels.Color;
                        image.Red = new byte[pixelCount];
                        image.Green = new byte[pixelCount];
                        image.Blue = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < width; x++)
                                {
                                    byte* p = row + x * bpp;
                                    int i = y * width + x;
                                    image.Blue[i] = p[0];
                                    image.Green[i] = p[1];
                                    image.Red[i] = p[2];
                                }
                            }
                        }
                    }
                    else if (bpp == 1)
                    {
                        image.Channels = ManagedImage.ImageChannels.Gray;
                        image.Red = new byte[pixelCount];

                        unsafe
                        {
                            byte* start = (byte*)basePtr;

                            for (int y = 0; y < height; y++)
                            {
                                byte* row = start + y * rowBytes;
                                for (int x = 0; x < width; x++)
                                {
                                    int i = y * width + x;
                                    image.Red[i] = row[x];
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

            return image;
        }
    }
}
