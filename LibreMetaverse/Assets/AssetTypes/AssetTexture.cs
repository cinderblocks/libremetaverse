/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2024, Sjofn LLC.
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
using CoreJ2K;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using LibreMetaverse.Imaging;

namespace LibreMetaverse.Assets
{
    /// <summary>
    /// Represents a texture
    /// </summary>
    public class AssetTexture : Asset
    {
        static AssetTexture()
        {
            ImageFactory.Register(new ManagedImageCreator());
        }

        /// <summary>Override the base classes AssetType</summary>
        public override AssetType AssetType => AssetType.Texture;

        /// <summary>A <see cref="ManagedImage"/> object containing image data</summary>
        public ManagedImage? Image;

        /// <summary></summary>
        public int Components;

        /// <summary>Initializes a new instance of an AssetTexture object</summary>
        public AssetTexture() { }

        /// <summary>
        /// Initializes a new instance of an AssetTexture object
        /// </summary>
        /// <param name="assetID">A unique <see cref="UUID"/> specific to this asset</param>
        /// <param name="assetData">A byte array containing the raw asset data</param>
        public AssetTexture(UUID assetID, byte[] assetData) : base(assetID, assetData) { }

        /// <summary>
        /// Initializes a new instance of an AssetTexture object
        /// </summary>
        /// <param name="image">A <see cref="ManagedImage"/> object containing texture data</param>
        public AssetTexture(ManagedImage image)
        {
            Image = image;
            Components = 0;
            if ((Image.Channels & ManagedImage.ImageChannels.Color) != 0)
                Components += 3;
            if ((Image.Channels & ManagedImage.ImageChannels.Gray) != 0)
                ++Components;
            if ((Image.Channels & ManagedImage.ImageChannels.Bump) != 0)
                ++Components;
            if ((Image.Channels & ManagedImage.ImageChannels.Alpha) != 0)
                ++Components;
        }

        /// <summary>
        /// Populates the <see cref="AssetData"/> byte array with a JPEG2000
        /// encoded image created from the data in <see cref="Image"/>
        /// </summary>
        public sealed override void Encode()
        {
            if (Image == null)
            {
                AssetData = Array.Empty<byte>();
                return;
            }

            AssetData = CompleteConfigurationPresets.Streaming.WithFileFormat(false).Encode(Image);
        }

        /// <summary>
        /// Decodes the JPEG2000 data in <see cref="AssetData"/>> to the
        /// <see cref="ManagedImage"/> object <see cref="Image"/>
        /// </summary>
        /// <returns>True if the decoding was successful, otherwise false</returns>
        public sealed override bool Decode()
        {
            if (AssetData == null || AssetData.Length <= 0) { return false; }

            this.Components = 0;

            // The size of the image, and so the memory and time the decoder needs, comes from the
            // header of the (untrusted) data. Check it before handing the data over.
            if (!TryValidateHeader(AssetData, out string? reason))
            {
                Logger.Warn($"Refusing to decode texture {AssetID}: {reason}");
                Image = null;
                return false;
            }

            try
            {
                Image = J2kImage.DecodeToImage<ManagedImage>(AssetData);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to decode texture {AssetID}", ex);
                Image = null;
                return false;
            }

            if (Image == null) { return false; }

            if ((Image.Channels & ManagedImage.ImageChannels.Color) != 0)
                Components += 3;
            if ((Image.Channels & ManagedImage.ImageChannels.Gray) != 0)
                ++Components;
            if ((Image.Channels & ManagedImage.ImageChannels.Bump) != 0)
                ++Components;
            if ((Image.Channels & ManagedImage.ImageChannels.Alpha) != 0)
                ++Components;

            return true;
        }

        /// <summary>Maximum width or height, in pixels, of a texture that will be decoded</summary>
        public static int MaxDimension = 2048;

        /// <summary>Maximum number of components (color, alpha, bump) of a texture that will be decoded</summary>
        public static int MaxComponents = 5;

        /// <summary>Maximum bit depth of a component of a texture that will be decoded</summary>
        public static int MaxBitDepth = 16;

        /// <summary>Maximum number of tiles of a texture that will be decoded</summary>
        public static int MaxTiles = 1024;

        /// <summary>Maximum number of wavelet decomposition levels of a texture that will be decoded</summary>
        public static int MaxDecompositionLevels = 32;

        private static readonly byte[] Jp2cBoxType = { (byte)'j', (byte)'p', (byte)'2', (byte)'c' };

        private static uint ReadU16(byte[] d, int pos) => (uint)((d[pos] << 8) | d[pos + 1]);

        private static uint ReadU32(byte[] d, int pos) =>
            ((uint)d[pos] << 24) | ((uint)d[pos + 1] << 16) | ((uint)d[pos + 2] << 8) | d[pos + 3];

        /// <summary>
        /// Checks the main header of a JPEG 2000 image, either a bare codestream or one in a JP2 file,
        /// against the limits above without decoding any image data.
        /// </summary>
        /// <param name="data">The JPEG 2000 data</param>
        /// <param name="reason">Why the data was refused, or null if it is acceptable</param>
        /// <returns>True if the declared image is well formed and within limits</returns>
        internal static bool TryValidateHeader(byte[] data, out string? reason)
        {
            reason = null;

            int pos = FindCodestream(data);
            if (pos < 0) { reason = "no JPEG 2000 codestream found"; return false; }

            // SOC marker followed by the SIZ marker segment
            if (data.Length - pos < 4 || data[pos] != 0xFF || data[pos + 1] != 0x4F
                || data[pos + 2] != 0xFF || data[pos + 3] != 0x51)
            {
                reason = "codestream does not start with SOC and SIZ markers";
                return false;
            }
            pos += 4;

            // Lsiz, Rsiz, Xsiz, Ysiz, XOsiz, YOsiz, XTsiz, YTsiz, XTOsiz, YTOsiz, Csiz
            if (data.Length - pos < 38) { reason = "truncated SIZ marker segment"; return false; }

            uint lsiz = ReadU16(data, pos);
            uint xsiz = ReadU32(data, pos + 4);
            uint ysiz = ReadU32(data, pos + 8);
            uint xOsiz = ReadU32(data, pos + 12);
            uint yOsiz = ReadU32(data, pos + 16);
            uint xTsiz = ReadU32(data, pos + 20);
            uint yTsiz = ReadU32(data, pos + 24);
            uint xTOsiz = ReadU32(data, pos + 28);
            uint yTOsiz = ReadU32(data, pos + 32);
            uint csiz = ReadU16(data, pos + 36);

            if (csiz < 1 || csiz > MaxComponents) { reason = $"{csiz} components"; return false; }
            if (lsiz != 38 + 3 * csiz) { reason = "inconsistent SIZ marker segment length"; return false; }
            if (data.Length - pos < lsiz) { reason = "truncated SIZ marker segment"; return false; }

            if (xOsiz >= xsiz || yOsiz >= ysiz) { reason = "empty image area"; return false; }
            long width = (long)xsiz - xOsiz;
            long height = (long)ysiz - yOsiz;
            if (width > MaxDimension || height > MaxDimension)
            {
                reason = $"image size {width}x{height} exceeds {MaxDimension}x{MaxDimension}";
                return false;
            }

            if (xTsiz == 0 || yTsiz == 0) { reason = "zero tile size"; return false; }
            if (xTOsiz > xOsiz || yTOsiz > yOsiz
                || (long)xTOsiz + xTsiz <= xOsiz || (long)yTOsiz + yTsiz <= yOsiz)
            {
                reason = "tile grid does not cover the image";
                return false;
            }
            long tilesX = ((long)xsiz - xTOsiz + xTsiz - 1) / xTsiz;
            long tilesY = ((long)ysiz - yTOsiz + yTsiz - 1) / yTsiz;
            if (tilesX * tilesY > MaxTiles) { reason = $"{tilesX * tilesY} tiles"; return false; }

            for (int c = 0; c < csiz; c++)
            {
                int bitDepth = (data[pos + 38 + 3 * c] & 0x7F) + 1;
                if (bitDepth > MaxBitDepth) { reason = $"bit depth {bitDepth}"; return false; }
                if (data[pos + 39 + 3 * c] == 0 || data[pos + 40 + 3 * c] == 0)
                {
                    reason = "zero component subsampling";
                    return false;
                }
            }

            // Walk the rest of the main header, up to the first tile-part, for the
            // decomposition levels in the COD and COC marker segments.
            pos += (int)lsiz;
            while (data.Length - pos >= 4 && data[pos] == 0xFF && data[pos + 1] != 0x90 && data[pos + 1] != 0xD9)
            {
                byte marker = data[pos + 1];
                int length = (int)ReadU16(data, pos + 2);
                if (length < 2 || data.Length - pos - 2 < length)
                {
                    reason = "truncated marker segment";
                    return false;
                }

                int levelsAt = -1;
                if (marker == 0x52) // COD
                    levelsAt = pos + 9;
                else if (marker == 0x53) // COC
                    levelsAt = pos + 4 + (csiz < 257 ? 1 : 2) + 1;

                if (levelsAt >= 0)
                {
                    if (levelsAt >= pos + 2 + length) { reason = "truncated COD/COC marker segment"; return false; }
                    if (data[levelsAt] > MaxDecompositionLevels)
                    {
                        reason = $"{data[levelsAt]} decomposition levels";
                        return false;
                    }
                }

                pos += 2 + length;
            }

            return true;
        }

        /// <summary>
        /// Finds the offset of the codestream in raw JPEG 2000 data, which is either the
        /// data itself or the contents of the contiguous codestream box of a JP2 file.
        /// </summary>
        private static int FindCodestream(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0x4F) { return 0; }

            // JP2: a sequence of boxes, each a length and a four byte type
            long pos = 0;
            while (data.Length - pos >= 8)
            {
                long boxLength = ReadU32(data, (int)pos);
                int headerLength = 8;
                if (boxLength == 1)
                {
                    if (data.Length - pos < 16) { return -1; }
                    boxLength = ((long)ReadU32(data, (int)pos + 8) << 32) | ReadU32(data, (int)pos + 12);
                    headerLength = 16;
                }
                else if (boxLength == 0)
                {
                    boxLength = data.Length - pos; // extends to the end of the file
                }

                if (boxLength < headerLength) { return -1; }

                bool isCodestream = true;
                for (int i = 0; i < 4; i++)
                    isCodestream &= data[pos + 4 + i] == Jp2cBoxType[i];
                if (isCodestream) { return (int)(pos + headerLength); }

                pos += boxLength;
            }

            return -1;
        }
    }
}
