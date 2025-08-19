/*
 * Copyright (c) 2024, Sjofn LLC
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

using CoreJ2K;
using CoreJ2K.j2k.util;
using SkiaSharp;

namespace OpenMetaverse.Imaging
{
    public static class J2K
    {
        private static readonly string[][] EncoderPInfo =
            {
                new string[]
                    {
                        "debug", null,
                        "Print debugging messages when an error is encountered.",
                        "off"
                    },
                new string[]
                    {
                        "disable_jp2_extension", "[on|off]",
                        "JJ2000 automatically adds .jp2 extension when using 'file_format' option. This option disables it when on.",
                        "off"
                    },
                new string[]
                    {
                        "file_format", "[on|off]",
                        "Puts the JPEG 2000 codestream in a JP2 file format wrapper.",
                        "off"
                    },
                new string[]
                    {
                        "pph_tile", "[on|off]",
                        "Packs the packet headers in the tile headers.", 
                        "off"
                    },
                new string[]
                    {
                        "pph_main", "[on|off]",
                        "Packs the packet headers in the main header.", 
                        "off"
                    },
                new string[]
                    {
                        "tile_parts", "<packets per tile-part>",
                        "This option specifies the maximum number of packets to have in one tile-part. 0 means include all packets in first tile-part of each tile",
                        "0"
                    },
                new string[]
                    {
                        "tiles", "<nominal tile width> <nominal tile height>",
                        "This option specifies the maximum tile dimensions to use. If both dimensions are 0 then no tiling is used.",
                        "0 0"
                    },
                new string[]
                    {
                        "ref", "<x> <y>",
                        "Sets the origin of the image in the canvas system. It sets the coordinate of the top-left corner of the image reference grid, with respect to the canvas origin",
                        "0 0"
                    },
                new string[]
                    {
                        "tref", "<x> <y>",
                        "Sets the origin of the tile partitioning on the reference grid, with respect to the canvas origin. The value of 'x' ('y') specified can not be larger than the 'x' one specified in the ref option.",
                        "0 0"
                    },
                new string[]
                    {
                        "rate", "<output bitrate in bpp>",
                        "This is the output bitrate of the codestream in bits per pixel. When equal to -1, no image information (beside quantization effects) is discarded during compression. Note: In the case where '-file_format' option is used, the resulting file may have a larger bitrate.",
                        "-1"
                    },
                new string[]
                    {
                        "lossless", "[on|off]",
                        "Specifies a lossless compression for the encoder. This options is equivalent to use reversible quantization ('-Qtype reversible') and 5x3 wavelet filters pair ('-Ffilters w5x3'). Note that this option cannot be used with '-rate'. When this option is off, the quantization type and the filters pair is defined by '-Qtype' and '-Ffilters' respectively.",
                        "off"
                    },
                new string[]
                    {
                        "verbose", null,
                        "Prints information about the obtained bit stream.", 
                        "off"
                    },
                new string[]
                    {
                        "v", "[on|off]", "Prints version and copyright information.",
                        "off"
                    },
                new string[]
                    {
                        "u", "[on|off]",
                        "Prints usage information. "
                        + "If specified all other arguments (except 'v') are ignored",
                        "off"
                    },
            };
        
        public static ParameterList GetDefaultEncoderParameterList()
        {
            return J2kImage.GetDefaultEncoderParameterList(EncoderPInfo);
        }
        
        public static byte[] ToBytes(SKBitmap bitmap)
        {
            return J2kImage.ToBytes(bitmap, GetDefaultEncoderParameterList());
        }
    }
}