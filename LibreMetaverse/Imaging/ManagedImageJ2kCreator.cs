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
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.image.input;
using CoreJ2K.Util;

namespace LibreMetaverse.Imaging
{
    /// <summary>
    /// CoreJ2K image backend that decodes/encodes directly against <see cref="ManagedImage"/>'s
    /// byte-plane buffers. Unlike a bitmap-library-backed creator, this never allocates an
    /// intermediate bitmap object of any kind.
    /// </summary>
    public sealed class ManagedImageCreator : ImageCreator<ManagedImage>
    {
        /// <summary>
        /// Builds a <see cref="ManagedImage"/> directly from the interleaved, already byte-scaled
        /// sample buffer produced by CoreJ2K's decode fast path. Component order/count matches the
        /// convention used elsewhere in this codebase (see <see cref="ManagedImage(InterleavedImage)"/>):
        /// 1=Gray, 2=Gray+Alpha, 3=Color, 4=Color+Alpha, 5=Color+Bump+Alpha.
        /// </summary>
        public override IImage Create(int width, int height, int numComponents, byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Invalid dimensions");

            var pixelCount = width * height;
            ManagedImage.ImageChannels channels;
            switch (numComponents)
            {
                case 1:
                    channels = ManagedImage.ImageChannels.Gray;
                    break;
                case 2:
                    channels = ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha;
                    break;
                case 3:
                    channels = ManagedImage.ImageChannels.Color;
                    break;
                case 4:
                    channels = ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Alpha;
                    break;
                case 5:
                    channels = ManagedImage.ImageChannels.Color | ManagedImage.ImageChannels.Bump | ManagedImage.ImageChannels.Alpha;
                    break;
                default:
                    throw new NotSupportedException($"ManagedImage decode target does not support {numComponents} components.");
            }

            var image = new ManagedImage(width, height, channels);
            switch (numComponents)
            {
                case 1:
                    for (var i = 0; i < pixelCount; i++)
                    {
                        image.Red[i] = bytes[i];
                    }
                    break;
                case 2:
                    for (var i = 0; i < pixelCount; i++)
                    {
                        var o = i * 2;
                        image.Red[i] = bytes[o];
                        image.Alpha[i] = bytes[o + 1];
                    }
                    break;
                case 3:
                    for (var i = 0; i < pixelCount; i++)
                    {
                        var o = i * 3;
                        image.Red[i] = bytes[o];
                        image.Green[i] = bytes[o + 1];
                        image.Blue[i] = bytes[o + 2];
                    }
                    break;
                case 4:
                    for (var i = 0; i < pixelCount; i++)
                    {
                        var o = i * 4;
                        image.Red[i] = bytes[o];
                        image.Green[i] = bytes[o + 1];
                        image.Blue[i] = bytes[o + 2];
                        image.Alpha[i] = bytes[o + 3];
                    }
                    break;
                case 5:
                    for (var i = 0; i < pixelCount; i++)
                    {
                        var o = i * 5;
                        image.Red[i] = bytes[o];
                        image.Green[i] = bytes[o + 1];
                        image.Blue[i] = bytes[o + 2];
                        image.Bump[i] = bytes[o + 3];
                        image.Alpha[i] = bytes[o + 4];
                    }
                    break;
            }

            return new ManagedImageJ2kImage(image);
        }

        public override BlkImgDataSrc ToPortableImageSource(object imageObject)
        {
            if (imageObject is ManagedImage mi) return new ImgReaderManagedImage(mi);
            if (imageObject is null) throw new ArgumentNullException(nameof(imageObject));
            throw new ArgumentException($"Expected {nameof(ManagedImage)} but got {imageObject.GetType()}", nameof(imageObject));
        }
    }

    /// <summary>
    /// Trivial <see cref="IImage"/> wrapper so <c>J2kImage.DecodeToImage&lt;ManagedImage&gt;</c> can
    /// hand back the already-built <see cref="ManagedImage"/> unchanged via <c>As&lt;ManagedImage&gt;()</c>.
    /// </summary>
    internal sealed class ManagedImageJ2kImage : ImageBase<ManagedImage>
    {
        private readonly ManagedImage _image;

        internal ManagedImageJ2kImage(ManagedImage image)
            : base(image.Width, image.Height, 0, Array.Empty<byte>())
        {
            _image = image;
        }

        protected override object GetImageObject() => _image;
    }

    /// <summary>
    /// Encode-side <see cref="BlkImgDataSrc"/> adapter reading directly from a <see cref="ManagedImage"/>'s
    /// byte planes. Always presents 4 components (R,G,B,A), applying the same channel-substitution rules
    /// <see cref="ManagedImage.ExportBitmap"/> used to (alpha-only images replicate alpha to RGB with full
    /// alpha; images without an alpha channel get a constant 255 alpha), so encoded output is unchanged
    /// from the previous SkiaSharp-mediated path.
    /// </summary>
    internal sealed class ImgReaderManagedImage : ImgReader
    {
        private const int DcOffset = 128;

        private readonly ManagedImage _image;
        private readonly bool _hasColor;
        private readonly bool _hasAlpha;

        internal ImgReaderManagedImage(ManagedImage image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            w = image.Width;
            h = image.Height;
            nc = 4;
            _hasColor = (image.Channels & ManagedImage.ImageChannels.Color) != 0;
            _hasAlpha = (image.Channels & ManagedImage.ImageChannels.Alpha) != 0;
        }

        public override void Close()
        {
        }

        public override int GetNomRangeBits(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return 8;
        }

        public override int GetFixedPoint(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return 0;
        }

        public override bool IsOrigSigned(int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));
            return false;
        }

        private byte GetSample(int c, int pixelIndex)
        {
            if (_hasAlpha)
            {
                if (_hasColor)
                {
                    switch (c)
                    {
                        case 0: return _image.Red[pixelIndex];
                        case 1: return _image.Green[pixelIndex];
                        case 2: return _image.Blue[pixelIndex];
                        default: return _image.Alpha[pixelIndex];
                    }
                }
                // Alpha-only: replicate to RGB, matching ManagedImage.ExportBitmap.
                return c == 3 ? byte.MaxValue : _image.Alpha[pixelIndex];
            }

            switch (c)
            {
                case 0: return _image.Red[pixelIndex];
                case 1: return _image.Green[pixelIndex];
                case 2: return _image.Blue[pixelIndex];
                default: return byte.MaxValue;
            }
        }

        public override DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            if (compIndex < 0 || compIndex >= nc) throw new ArgumentOutOfRangeException(nameof(compIndex));

            if (blk.DataType != DataBlk.TYPE_INT)
            {
                blk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
            }

            var arr = new int[blk.w * blk.h];
            var idx = 0;
            for (var y = blk.uly; y < blk.uly + blk.h; y++)
            {
                var rowBase = y * w;
                for (var x = blk.ulx; x < blk.ulx + blk.w; x++)
                {
                    arr[idx++] = GetSample(compIndex, rowBase + x) - DcOffset;
                }
            }

            if (blk is DataBlkInt dbiBlk)
            {
                dbiBlk.DataInt = arr;
            }
            else
            {
                blk.Data = arr;
            }
            blk.offset = 0;
            blk.scanw = blk.w;
            blk.progressive = false;
            return blk;
        }

        public override DataBlk GetCompData(DataBlk blk, int c)
        {
            // No internal buffering to protect against caller mutation, so this is
            // equivalent to GetInternCompData.
            return GetInternCompData(blk, c);
        }

        public override string ToString() => $"ImgReaderManagedImage: WxH={w}x{h}, Components={nc}";
    }
}
