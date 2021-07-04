/**
 * Copyright (c) 2021, Sjofn LLC.
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
using System.Drawing;
using System.Drawing.Imaging;
using OpenJpegDotNet;

namespace LibreMetaverse.Imaging
{

    internal static class ImageHelper
    {

        #region Methods

        public static OpenJpegDotNet.Image FromRaw(byte[] raw, int width, int height, int stride, int channels, bool interleaved)
        {
            if (raw == null)
                throw new ArgumentNullException(nameof(raw));

            var byteAllocated = 1;
            var colorSpace = ColorSpace.Srgb;
            var precision = 24u / (uint)channels;
            var gap = stride - width * channels;

            using (var compressionParameters = new CompressionParameters())
            {
                OpenJpeg.SetDefaultEncoderParameters(compressionParameters);

                var subsamplingDx = compressionParameters.SubsamplingDx;
                var subsamplingDy = compressionParameters.SubsamplingDy;

                var componentParametersArray = new ImageComponentParameters[channels];
                for (var i = 0; i < channels; i++)
                {
                    componentParametersArray[i] = new ImageComponentParameters
                    {
                        Precision = precision,
                        Bpp = precision,
                        Signed = false,
                        Dx = (uint)subsamplingDx,
                        Dy = (uint)subsamplingDy,
                        Width = (uint)width,
                        Height = (uint)height
                    };
                }

                var image = OpenJpeg.ImageCreate((uint)channels, componentParametersArray, colorSpace);
                if (image == null)
                    return null;

                image.X0 = (uint)compressionParameters.ImageOffsetX0;
                image.Y0 = (uint)compressionParameters.ImageOffsetY0;
                image.X1 = image.X0 == 0 ? (uint)(width - 1) * (uint)subsamplingDx + 1 : image.X0 + (uint)(width - 1) * (uint)subsamplingDx + 1;
                image.Y1 = image.Y0 == 0 ? (uint)(height - 1) * (uint)subsamplingDy + 1 : image.Y0 + (uint)(height - 1) * (uint)subsamplingDy + 1;

                unsafe
                {
                    fixed (byte* pRaw = &raw[0])
                    {
                        // Bitmap data is interleave.
                        // Convert it to planer
                        if (byteAllocated == 1)
                        {
                            if (interleaved)
                            {
                                for (var i = 0; i < channels; i++)
                                {
                                    var target = image.Components[i].Data;
                                    var pTarget = (int*)target;
                                    var source = pRaw + i;
                                    for (var y = 0; y < height; y++)
                                    {
                                        for (var x = 0; x < width; x++)
                                        {
                                            *pTarget = *source;
                                            pTarget++;
                                            source += channels;
                                        }

                                        source += gap;
                                    }
                                }
                            }
                            else
                            {
                                for (var i = 0; i < channels; i++)
                                {
                                    var target = image.Components[i].Data;
                                    var pTarget = (int*)target;
                                    var source = pRaw + i * (stride * height);
                                    for (var y = 0; y < height; y++)
                                    {
                                        for (var x = 0; x < width; x++)
                                        {
                                            *pTarget = *source;
                                            pTarget++;
                                            source++;
                                        }

                                        source += gap;
                                    }
                                }
                            }
                        }
                    }
                }

                return image;
            }
        }

        public static OpenJpegDotNet.Image FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var width = bitmap.Width;
            var height = bitmap.Height;
            var format = bitmap.PixelFormat;
            int channels;
            var byteAllocated = 0;
            ColorSpace colorSpace;
            switch (format)
            {
                case PixelFormat.Format24bppRgb:
                    channels = 3;
                    colorSpace = ColorSpace.Srgb;
                    byteAllocated = 1;
                    break;
                case PixelFormat.Format32bppArgb:
                    channels = 4;
                    colorSpace = ColorSpace.Srgb;
                    byteAllocated = 1;
                    break;
                case PixelFormat.Format8bppIndexed:
                    channels = 1;
                    colorSpace = ColorSpace.Srgb;
                    byteAllocated = 1;
                    break;
                default:
                    throw new NotSupportedException();
            }
            var precision = 24u / (uint)channels;

            BitmapData bitmapData = null;

            try
            {
                bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, format);
                var stride = bitmapData.Stride;
                var gap = stride - width * channels;
                var scan0 = bitmapData.Scan0;

                using (var compressionParameters = new CompressionParameters())
                {
                    OpenJpeg.SetDefaultEncoderParameters(compressionParameters);

                    var subsamplingDx = compressionParameters.SubsamplingDx;
                    var subsamplingDy = compressionParameters.SubsamplingDy;

                    var componentParametersArray = new ImageComponentParameters[channels];
                    for (var i = 0; i < channels; i++)
                    {
                        componentParametersArray[i] = new ImageComponentParameters
                        {
                            Precision = precision,
                            Bpp = precision,
                            Signed = false,
                            Dx = (uint)subsamplingDx,
                            Dy = (uint)subsamplingDy,
                            Width = (uint)width,
                            Height = (uint)height
                        };
                    }

                    var image = OpenJpeg.ImageCreate((uint)channels, componentParametersArray, colorSpace);
                    if (image == null)
                        return null;

                    image.X0 = (uint)compressionParameters.ImageOffsetX0;
                    image.Y0 = (uint)compressionParameters.ImageOffsetY0;
                    image.X1 = image.X0 == 0 ? (uint)(width - 1) * (uint)subsamplingDx + 1 : image.X0 + (uint)(width - 1) * (uint)subsamplingDx + 1;
                    image.Y1 = image.Y0 == 0 ? (uint)(height - 1) * (uint)subsamplingDy + 1 : image.Y0 + (uint)(height - 1) * (uint)subsamplingDy + 1;

                    unsafe
                    {
                        // Bitmap data is interleave.
                        // Convert it to planer
                        if (byteAllocated == 1)
                        {
                            for (var i = 0; i < channels; i++)
                            {
                                var target = image.Components[i].Data;
                                var pTarget = (int*)target;
                                var source = (byte*)scan0;
                                source += i;
                                for (var y = 0; y < height; y++)
                                {
                                    for (var x = 0; x < width; x++)
                                    {
                                        *pTarget = *source;
                                        pTarget++;
                                        source += channels;
                                    }

                                    source += gap;
                                }
                            }
                        }
                    }

                    return image;
                }
            }
            finally
            {
                if (bitmapData != null)
                    bitmap.UnlockBits(bitmapData);
            }
        }

        #endregion

    }

}