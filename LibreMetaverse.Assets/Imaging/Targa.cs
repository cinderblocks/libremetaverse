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
using Pfim;

namespace LibreMetaverse.Imaging
{
    public static class Targa
    {
        /// <summary>Decode Truevision TGA file directly to <see cref="ManagedImage"/>, with no bitmap library involved</summary>
        public static ManagedImage DecodeToManagedImage(string fileName)
        {
            using (var image = Pfimage.FromFile(fileName))
            {
                return DecodeToManagedImage(image);
            }
        }

        /// <summary>Decode Truevision TGA stream directly to <see cref="ManagedImage"/>, with no bitmap library involved</summary>
        public static ManagedImage DecodeToManagedImage(Stream stream)
        {
            using (var image = Pfimage.FromStream(stream))
            {
                return DecodeToManagedImage(image);
            }
        }

        private static ManagedImage DecodeToManagedImage(IImage image)
        {
            var width = image.Width;
            var height = image.Height;
            var data = image.Data;
            var stride = image.Stride;

            switch (image.Format)
            {
                case ImageFormat.Rgb8:
                {
                    var mi = new ManagedImage(width, height, ManagedImage.ImageChannels.Gray);
                    for (var y = 0; y < height; y++)
                    {
                        Array.Copy(data, y * stride, mi.Red, y * width, width);
                    }
                    return mi;
                }
                case ImageFormat.R5g6b5:
                {
                    var mi = new ManagedImage(width, height, ManagedImage.ImageChannels.Color);
                    for (var y = 0; y < height; y++)
                    {
                        var row = y * stride;
                        for (var x = 0; x < width; x++)
                        {
                            var i = y * width + x;
                            var v = (ushort)(data[row + x * 2] | (data[row + x * 2 + 1] << 8));
                            int r5 = (v >> 11) & 0x1F;
                            int g6 = (v >> 5) & 0x3F;
                            int b5 = v & 0x1F;
                            mi.Red[i] = (byte)((r5 * 255 + 15) / 31);
                            mi.Green[i] = (byte)((g6 * 255 + 31) / 63);
                            mi.Blue[i] = (byte)((b5 * 255 + 15) / 31);
                        }
                    }
                    return mi;
                }
                case ImageFormat.Rgb24:
                {
                    var mi = new ManagedImage(width, height, ManagedImage.ImageChannels.Color);
                    for (var y = 0; y < height; y++)
                    {
                        var row = y * stride;
                        for (var x = 0; x < width; x++)
                        {
                            var i = y * width + x;
                            var p = row + x * 3;
                            mi.Red[i] = data[p + 2];
                            mi.Green[i] = data[p + 1];
                            mi.Blue[i] = data[p];
                        }
                    }
                    return mi;
                }
                case ImageFormat.Rgba32:
                {
                    var mi = new ManagedImage(width, height,
                        ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha);
                    for (var y = 0; y < height; y++)
                    {
                        var row = y * stride;
                        for (var x = 0; x < width; x++)
                        {
                            var i = y * width + x;
                            var p = row + x * 4;
                            mi.Blue[i] = data[p];
                            mi.Green[i] = data[p + 1];
                            mi.Red[i] = data[p + 2];
                            mi.Alpha[i] = data[p + 3];
                        }
                    }
                    return mi;
                }
                default:
                    throw new ArgumentException($"Cannot interpret format: {image.Format}");
            }
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