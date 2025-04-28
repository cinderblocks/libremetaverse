/*
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
using System.IO;
using System.Runtime.InteropServices;
using Pfim;
using SkiaSharp;

namespace OpenMetaverse.Imaging
{
    public static class Targa
    {
        /// <summary>Decode Truevision TGA file to <see cref="SKBitmap" /></summary>
        public static SKBitmap Decode(string fileName)
        {
            using (var image = Pfimage.FromFile(fileName))
            {
                return Decode(image);
            }
        }

        /// <summary>Decode Truevision TGA stream to <see cref="SKBitmap" /></summary>
        public static SKBitmap Decode(Stream stream)
        {
            using (var image = Pfimage.FromStream(stream))
            {
                return Decode(image);
            }
        }

        private static SKBitmap Decode(IImage image)
        {
            SKColorType colorType;
            var data = image.Data;
            var dataLen = image.DataLen;
            var stride = image.Stride;
            switch (image.Format)
            {
                case ImageFormat.Rgb8:
                    colorType = SKColorType.Gray8;
                    break;
                case ImageFormat.R5g6b5: // needs swizzled
                    colorType = SKColorType.Rgb565;
                    break;
                case ImageFormat.Rgba16: // needs swizzled
                    colorType = SKColorType.Argb4444;
                    break;
                case ImageFormat.Rgb24: // skia doesn't support 24-bit (boo!), upscale to 32-bit
                    var pixels = image.DataLen / 3;
                    dataLen = pixels * 4;
                    data = new byte[dataLen];
                    for (var i = 0; i < pixels; ++i)
                    {
                        data[i * 4] = image.Data[i * 3];
                        data[i * 4 + 1] = image.Data[i * 3 + 1];
                        data[i * 4 + 2] = image.Data[i * 3 + 2];
                        data[i * 4 + 3] = 255;
                    }
                    stride = image.Width * 4;
                    colorType = SKColorType.Bgra8888;
                    break;
                case ImageFormat.Rgba32:
                    colorType = SKColorType.Bgra8888;
                    break;
                default:
                    throw new ArgumentException($"Cannot interpret format: {image.Format}");
            }
            var imageInfo = new SKImageInfo(image.Width, image.Height, colorType);
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
            using (var skdata = SKData.Create(ptr, dataLen, (address, context) => handle.Free()))
            {
                using (var skImage = SKImage.FromPixels(imageInfo, skdata, stride))
                {
                    return SKBitmap.FromImage(skImage);
                }
            }
        }

        public static byte[] Encode(SKBitmap image)
        {
            if (image == null) { return null; }

            var imageType = (byte)(image.ColorType == SKColorType.Gray8 || image.ColorType == SKColorType.Alpha8
                ? 0x03 : 0x02);
            var imageDescriptor = (byte)(image.AlphaType <= SKAlphaType.Opaque ? 0 : 0x8); 

            var tga = new byte[image.Width * image.Height * image.BytesPerPixel + 18];
            var di = 0;
            tga[di++] = 0x00; // id length
            tga[di++] = 0x00; // colormap type = 0: no colormap
            tga[di++] = imageType; // image type
            tga[di++] = 0x00; // color map spec is five zeroes for no color map
            tga[di++] = 0x00; // color map spec is five zeroes for no color map
            tga[di++] = 0x00; // color map spec is five zeroes for no color map
            tga[di++] = 0x00; // color map spec is five zeroes for no color map
            tga[di++] = 0x00; // color map spec is five zeroes for no color map
            tga[di++] = 0; // x origin = two bytes
            tga[di++] = 0; // x origin = two bytes
            tga[di++] = 0; // y origin = two bytes
            tga[di++] = 0; // y origin = two bytes
            tga[di++] = (byte)(image.Width & 0xFF); // width - low byte
            tga[di++] = (byte)(image.Width >> 8); // width - hi byte
            tga[di++] = (byte)(image.Height & 0xFF); // height - low byte
            tga[di++] = (byte)(image.Height >> 8); // height - hi byte
            tga[di++] = (byte)(image.BytesPerPixel*8); // pixel depth, includes attribute bits
            tga[di++] = imageDescriptor; // image descriptor byte

            for (var y = image.Height - 1; y >= 0; --y) // takes lines from bottom lines to top (mirrored horizontally)
            {
                for (var x = 0; x < image.Width; ++x)
                {
                    var c = image.GetPixel(x, y);
                    if (image.ColorType == SKColorType.Gray8)
                    {
                        tga[di++] = c.Red;
                    } else if (image.ColorType == SKColorType.Alpha8)
                    {
                        tga[di++] = c.Alpha;
                    }
                    else
                    {
                        tga[di++] = c.Blue;
                        tga[di++] = c.Green;
                        tga[di++] = c.Red;
                        
                        tga[di++] = (byte)(image.AlphaType > SKAlphaType.Opaque ? 0x0 : c.Alpha);
                    }
                        
                }
            }

            return tga;
        }

        /// <summary>Encode <see cref="ManagedImage"/> to Truevision TGA byte array</summary>
        public static byte[] Encode(ManagedImage image)
        {
            byte channels = 0;
            if ((image.Channels & ManagedImage.ImageChannels.Color) != 0)
            {
                channels += 3;
            }
            else if ((image.Channels & ManagedImage.ImageChannels.Gray) != 0)
            {
                ++channels;
            }

            if ((image.Channels & ManagedImage.ImageChannels.Alpha) != 0)
            {
                ++channels;
            }

            var tga = new byte[image.Width * image.Height * channels + 32];
            var di = 0;
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
            tga[di++] = (byte)(image.Width & 0xFF); // width - low byte
            tga[di++] = (byte)(image.Width >> 8); // width - hi byte
            tga[di++] = (byte)(image.Height & 0xFF); // height - low byte
            tga[di++] = (byte)(image.Height >> 8); // height - hi byte
            tga[di++] = (byte)(channels * 8); // bits per pixel
            tga[di++] = (byte)((image.Channels & ManagedImage.ImageChannels.Alpha) == 0 ? 0x0 : 0x20); // image descriptor byte

            int n = image.Width * image.Height;

            if ((image.Channels & ManagedImage.ImageChannels.Alpha) != 0)
            {
                if ((image.Channels & ManagedImage.ImageChannels.Color) != 0)
                {
                    // RGBA
                    for (var i = 0; i < n; i++)
                    {
                        tga[di++] = image.Blue[i];
                        tga[di++] = image.Green[i];
                        tga[di++] = image.Red[i];
                        tga[di++] = image.Alpha[i];
                    }
                }
                else
                {
                    // Alpha only
                    for (var i = 0; i < n; i++)
                    {
                        tga[di++] = image.Alpha[i];
                        tga[di++] = image.Alpha[i];
                        tga[di++] = image.Alpha[i];
                        tga[di++] = byte.MaxValue;
                    }
                }
            }
            else if ((image.Channels & ManagedImage.ImageChannels.Gray) != 0)
            {
                for (var i = 0; i < n; i++)
                {
                    tga[di++] = image.Red[i];
                }
            }
            else
            {
                // RGB
                for (var i = 0; i < n; i++)
                {
                    tga[di++] = image.Blue[i];
                    tga[di++] = image.Green[i];
                    tga[di++] = image.Red[i];
                }
            }

            return tga;
        }
    }
}