/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    public class TerrainPatch
    {
        #region Enums and Structs

        public enum LayerType : byte
        {
            Land            = 0x4C, // 'L'
            LandExtended    = 0x4D, // 'M'
            Water           = 0x57, // 'W'
            WaterExtended   = 0x57, // 'X'
            Wind            = 0x37, // '7'
            WindExtended    = 0x39, // '9'
            Cloud           = 0x38, // '8'
            CloudExtended   = 0x3A  // ':'
        }

        public struct GroupHeader
        {
            public int Stride;
            public int PatchSize;
            public LayerType Type;
        }

        public struct Header
        {
            public float DCOffset;
            public int Range;
            public int QuantWBits;
            public int PatchIDs;
            public uint WordBits;

            // true if PatchIDs are 32 bits and not 10
            public bool LargeRegion { get; set; }

            public int X
            {
                get { return PatchIDs >> (LargeRegion ? 16 : 5); }
                set { PatchIDs += (value << (LargeRegion ? 16 : 5)); }
            }

            public int Y
            {
                get { return PatchIDs & (LargeRegion ? 0xffff : 0x1F); }
                set { PatchIDs |= value & (LargeRegion ? 0xffff : 0x1F); }
            }

            public int setPatchIDs(int xx, int yy) {
                if (LargeRegion)
                    PatchIDs = (xx << 16) | (yy & 0xffff);
                else
                    PatchIDs = (xx << 5) | (yy & 0x1f);
                return PatchIDs;
            }
        }

        #endregion Enums and Structs

        /// <summary>X position of this patch</summary>
        public int X;
        /// <summary>Y position of this patch</summary>
        public int Y;
        /// <summary>A 16x16 array of floats holding decompressed layer data</summary>
        public float[] Data;
    }

    public static class TerrainCompressor
    {
        public const int PATCHES_PER_EDGE = 16;
        public const int END_OF_PATCHES = 97;

        private const float OO_SQRT2 = 0.7071067811865475244008443621049f;
        private const int STRIDE = 264;

        private const int ZERO_CODE = 0x0;
        private const int ZERO_EOB = 0x2;
        private const int POSITIVE_VALUE = 0x6;
        private const int NEGATIVE_VALUE = 0x7;

        private static readonly float[] DequantizeTable16 = new float[16 * 16];
        private static readonly float[] DequantizeTable32 = new float[16 * 16];
        private static readonly float[] CosineTable16 = new float[16 * 16];
        //private static readonly float[] CosineTable32 = new float[16 * 16];
        private static readonly int[] CopyMatrix16 = new int[16 * 16];
        private static readonly int[] CopyMatrix32 = new int[16 * 16];
        private static readonly float[] QuantizeTable16 = new float[16 * 16];

        static TerrainCompressor()
        {
            // Initialize the decompression tables
            BuildDequantizeTable16();
            SetupCosines16();
            BuildCopyMatrix16();
            BuildQuantizeTable16();
        }

        public static LayerDataPacket CreateLayerDataPacket(TerrainPatch[] patches, TerrainPatch.LayerType type)
        {
            LayerDataPacket layer = new LayerDataPacket {LayerID = {Type = (byte) type}};

            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader
            {
                Stride = STRIDE,
                PatchSize = 16,
                Type = type
            };

            // Should be enough to fit even the most poorly packed data
            byte[] data = new byte[patches.Length * 16 * 16 * 2];
            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(header.Stride, 16);
            bitpack.PackBits(header.PatchSize, 8);
            bitpack.PackBits((int)header.Type, 8);

            foreach (TerrainPatch t in patches)
                CreatePatch(bitpack, t.Data, t.X, t.Y);

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);

            return layer;
        }

        /// <summary>
        /// Creates a LayerData packet for compressed land data given a full
        /// simulator heightmap and an array of indices of patches to compress
        /// </summary>
        /// <param name="heightmap">A 256 * 256 array of floating point values
        /// specifying the height at each meter in the simulator.
        /// This can be larger if it is a varregion.</param>
        /// <param name="patches">Array of indexes in the 16x16 grid of patches
        /// for this simulator. For example if 1 and 17 are specified, patches
        /// x=1,y=0 and x=1,y=1 are sent</param>
        /// <returns></returns>
        public static LayerDataPacket CreateLandPacket(float[] heightmap, int[] patches)
        {
            var layerType = TerrainPatch.LayerType.Land;
            if (heightmap.Length > Simulator.DefaultRegionSizeY *  Simulator.DefaultRegionSizeY)
                layerType = TerrainPatch.LayerType.LandExtended;
            LayerDataPacket layer = new LayerDataPacket {LayerID = {Type = (byte) layerType}};

            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader
            {
                Stride = STRIDE,
                PatchSize = 16,
                Type = TerrainPatch.LayerType.Land
            };

            byte[] data = new byte[1536];
            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(header.Stride, 16);
            bitpack.PackBits(header.PatchSize, 8);
            bitpack.PackBits((int)header.Type, 8);

            foreach (int t in patches)
                CreatePatchFromHeightmap(bitpack, heightmap, t % 16, (t - (t % 16)) / 16);

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);

            return layer;
        }

        public static LayerDataPacket CreateLandPacket(float[] patchData, int x, int y)
        {
            LayerDataPacket layer = new LayerDataPacket {LayerID = {Type = (byte) TerrainPatch.LayerType.Land}};

            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader
            {
                Stride = STRIDE,
                PatchSize = 16,
                Type = TerrainPatch.LayerType.Land
            };

            byte[] data = new byte[1536];
            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(header.Stride, 16);
            bitpack.PackBits(header.PatchSize, 8);
            bitpack.PackBits((int)header.Type, 8);

            CreatePatch(bitpack, patchData, x, y);

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);

            return layer;
        }

        public static LayerDataPacket CreateLandPacket(float[,] patchData, int x, int y)
        {
            LayerDataPacket layer = new LayerDataPacket {LayerID = {Type = (byte) TerrainPatch.LayerType.Land}};

            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader
            {
                Stride = STRIDE,
                PatchSize = 16,
                Type = TerrainPatch.LayerType.Land
            };

            byte[] data = new byte[1536];
            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(header.Stride, 16);
            bitpack.PackBits(header.PatchSize, 8);
            bitpack.PackBits((int)header.Type, 8);

            CreatePatch(bitpack, patchData, x, y);

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);

            return layer;
        }

        private static void CreatePatch(BitPack output, float[] patchData, int x, int y)
        {
            CreatePatch(output, patchData, x, y, false);
        }
        private static void CreatePatch(BitPack output, float[] patchData, int x, int y, bool largeRegion)
        {
            if (patchData.Length != 16 * 16)
                throw new ArgumentException("Patch data must be a 16x16 array");

            TerrainPatch.Header header = PrescanPatch(patchData);
            header.LargeRegion = largeRegion;
            header.QuantWBits = 136;
            header.setPatchIDs(x, y);

            // NOTE: No idea what prequant and postquant should be or what they do
            int[] patch = CompressPatch(patchData, header, 10);
            int wbits = EncodePatchHeader(output, header, patch);
            EncodePatch(output, patch, 0, wbits);
        }

        public static void CreatePatch(BitPack output, float[,] patchData, int x, int y)
        {
            CreatePatch(output, patchData, x, y, false);
        }
        public static void CreatePatch(BitPack output, float[,] patchData, int x, int y, bool largeRegion)
        {
            if (patchData.Length != 16 * 16)
                throw new ArgumentException("Patch data must be a 16x16 array");

            var header = PrescanPatch(patchData);
            header.LargeRegion = largeRegion;
            header.QuantWBits = 136;
            header.setPatchIDs(x, y);

            // NOTE: No idea what prequant and postquant should be or what they do
            int[] patch = CompressPatch(patchData, header, 10);
            int wbits = EncodePatchHeader(output, header, patch);
            EncodePatch(output, patch, 0, wbits);
        }

        /// <summary>
        /// Add a patch of terrain to a BitPacker
        /// </summary>
        /// <param name="output">BitPacker to write the patch to</param>
        /// <param name="heightmap">Heightmap of the simulator, must be a 256 *
        /// 256 float array</param>
        /// <param name="x">X offset of the patch to create, valid values are
        /// from 0 to 15</param>
        /// <param name="y">Y offset of the patch to create, valid values are
        /// from 0 to 15</param>
        public static void CreatePatchFromHeightmap(BitPack output, float[] heightmap, int x, int y)
        {
            CreatePatchFromHeightmap(output, heightmap, x, y, false);
        }
        public static void CreatePatchFromHeightmap(BitPack output, float[] heightmap, int x, int y, bool largeRegion)
        {
            if (heightmap.Length != 256 * 256)
                throw new ArgumentException("Heightmap data must be 256x256");

            if (x < 0 || x > 15 || y < 0 || y > 15)
                throw new ArgumentException("X and Y patch offsets must be from 0 to 15");

            var header = PrescanPatch(heightmap, x, y);
            header.LargeRegion = largeRegion;
            header.QuantWBits = 136;
            header.setPatchIDs(x, y);

            // NOTE: No idea what prequant and postquant should be or what they do
            int[] patch = CompressPatch(heightmap, x, y, header, 10);
            int wbits = EncodePatchHeader(output, header, patch);
            EncodePatch(output, patch, 0, wbits);
        }

        // Scan the height map to get the range of values so the values can be compressed.
        // Returns a TerrainPatch.Header with  the range information filled in.
        private static TerrainPatch.Header PrescanPatch(float[] patch)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;

            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    float val = patch[j * 16 + i];
                    if (val > zmax) zmax = val;
                    if (val < zmin) zmin = val;
                }
            }

            header.DCOffset = zmin;
            header.Range = (int)((zmax - zmin) + 1.0f);

            return header;
        }

        // Scan the height map to get the range of values so the values can be compressed.
        // Returns a TerrainPatch.Header with  the range information filled in.
        private static TerrainPatch.Header PrescanPatch(float[,] patch)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;

            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    float val = patch[j, i];
                    if (val > zmax) zmax = val;
                    if (val < zmin) zmin = val;
                }
            }

            header.DCOffset = zmin;
            header.Range = (int)((zmax - zmin) + 1.0f);

            return header;
        }

        // Scan the height map to get the range of values so the values can be compressed.
        // Returns a TerrainPatch.Header with  the range information filled in.
        private static TerrainPatch.Header PrescanPatch(float[] heightmap, int patchX, int patchY)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;

            for (int j = patchY * 16; j < (patchY + 1) * 16; j++)
            {
                for (int i = patchX * 16; i < (patchX + 1) * 16; i++)
                {
                    float val = heightmap[j * 256 + i];
                    if (val > zmax) zmax = val;
                    if (val < zmin) zmin = val;
                }
            }

            header.DCOffset = zmin;
            header.Range = (int)((zmax - zmin) + 1.0f);

            return header;
        }

        public static TerrainPatch.Header DecodePatchHeader(BitPack bitpack)
        {
            return DecodePatchHeader(bitpack, false);

        }
        public static TerrainPatch.Header DecodePatchHeader(BitPack bitpack, bool largeRegion)
        {
            TerrainPatch.Header header = new TerrainPatch.Header {QuantWBits = bitpack.UnpackBits(8)};

            // Quantized word bits
            if (header.QuantWBits == END_OF_PATCHES)
                return header;

            // DC offset
            header.DCOffset = bitpack.UnpackFloat();

            // Range
            header.Range = bitpack.UnpackBits(16);

            // Patch IDs (10 bits)

            header.LargeRegion = largeRegion;
            header.PatchIDs = bitpack.UnpackBits(largeRegion ? 32 : 10);

            // Word bits
            header.WordBits = (uint)((header.QuantWBits & 0x0f) + 2);

            return header;
        }

        private static int EncodePatchHeader(BitPack output, TerrainPatch.Header header, int[] patch)
        {
            int temp;
            int wbits = (header.QuantWBits & 0x0f) + 2;
            uint maxWbits = (uint)wbits + 5;
            uint minWbits = ((uint)wbits >> 1);

            wbits = (int)minWbits;

            foreach (int t in patch)
            {
                temp = t;

                if (temp == 0) continue;

                // Get the absolute value
                if (temp < 0) temp *= -1;

                for (int j = (int)maxWbits; j > (int)minWbits; j--)
                {
                    if ((temp & (1 << j)) != 0)
                    {
                        if (j > wbits) wbits = j;
                        break;
                    }
                }
            }

            wbits += 1;

            header.QuantWBits &= 0xf0;

            if (wbits > 17 || wbits < 2)
            {
                Logger.Log("Bits needed per word in EncodePatchHeader() are outside the allowed range",
                    Helpers.LogLevel.Error);
            }

            header.QuantWBits |= (wbits - 2);

            output.PackBits(header.QuantWBits, 8);
            output.PackFloat(header.DCOffset);
            output.PackBits(header.Range, 16);
            output.PackBits(header.PatchIDs, header.LargeRegion ? 32 : 10); // the IDs are larger for large regions

            return wbits;
        }

        private static void IDCTColumn16(float[] linein, float[] lineout, int column)
        {
            for (int n = 0; n < 16; n++)
            {
                var total = OO_SQRT2 * linein[column];

                for (int u = 1; u < 16; u++)
                {
                    var usize = u * 16;
                    total += linein[usize + column] * CosineTable16[usize + n];
                }

                lineout[16 * n + column] = total;
            }
        }

        private static void IDCTLine16(float[] linein, float[] lineout, int line)
        {
            const float oosob = 2.0f / 16.0f;
            int lineSize = line * 16;

            for (int n = 0; n < 16; n++)
            {
                var total = OO_SQRT2 * linein[lineSize];

                for (int u = 1; u < 16; u++)
                {
                    total += linein[lineSize + u] * CosineTable16[u * 16 + n];
                }

                lineout[lineSize + n] = total * oosob;
            }
        }

        private static void DCTLine16(float[] linein, float[] lineout, int line)
        {
            float total = 0.0f;
            int lineSize = line * 16;

            for (int n = 0; n < 16; n++)
            {
                total += linein[lineSize + n];
            }

            lineout[lineSize] = OO_SQRT2 * total;

            for (int u = 1; u < 16; u++)
            {
                total = 0.0f;

                for (int n = 0; n < 16; n++)
                {
                    total += linein[lineSize + n] * CosineTable16[u * 16 + n];
                }

                lineout[lineSize + u] = total;
            }
        }

        private static void DCTColumn16(float[] linein, int[] lineout, int column)
        {
            float total = 0.0f;
            const float oosob = 2.0f / 16.0f;

            for (int n = 0; n < 16; n++)
            {
                total += linein[16 * n + column];
            }

            lineout[CopyMatrix16[column]] = (int)(OO_SQRT2 * total * oosob * QuantizeTable16[column]);

            for (int u = 1; u < 16; u++)
            {
                total = 0.0f;

                for (int n = 0; n < 16; n++)
                {
                    total += linein[16 * n + column] * CosineTable16[u * 16 + n];
                }

                lineout[CopyMatrix16[16 * u + column]] = (int)(total * oosob * QuantizeTable16[16 * u + column]);
            }
        }

        public static void DecodePatch(int[] patches, BitPack bitpack, TerrainPatch.Header header, int size)
        {
            for (int n = 0; n < size * size; n++)
            {
                // ?
                var temp = bitpack.UnpackBits(1);
                if (temp != 0)
                {
                    // Value or EOB
                    temp = bitpack.UnpackBits(1);
                    if (temp != 0)
                    {
                        // Value
                        temp = bitpack.UnpackBits(1);
                        if (temp != 0)
                        {
                            // Negative
                            temp = bitpack.UnpackBits((int)header.WordBits);
                            patches[n] = temp * -1;
                        }
                        else
                        {
                            // Positive
                            temp = bitpack.UnpackBits((int)header.WordBits);
                            patches[n] = temp;
                        }
                    }
                    else
                    {
                        // Set the rest to zero
                        // TODO: This might not be necessary
                        for (int o = n; o < size * size; o++)
                        {
                            patches[o] = 0;
                        }
                        break;
                    }
                }
                else
                {
                    patches[n] = 0;
                }
            }
        }

        private static void EncodePatch(BitPack output, int[] patch, int postquant, int wbits)
        {
            if (postquant > 16 * 16 || postquant < 0)
            {
                Logger.Log("Postquant is outside the range of allowed values in EncodePatch()", Helpers.LogLevel.Error);
                return;
            }

            if (postquant != 0) patch[16 * 16 - postquant] = 0;

            for (int i = 0; i < 16 * 16; i++)
            {
                var temp = patch[i];

                if (temp == 0)
                {
                    var eob = true;

                    for (int j = i; j < 16 * 16 - postquant; j++)
                    {
                        if (patch[j] != 0)
                        {
                            eob = false;
                            break;
                        }
                    }

                    if (eob)
                    {
                        output.PackBits(ZERO_EOB, 2);
                        return;
                    }
                    output.PackBits(ZERO_CODE, 1);
                }
                else
                {
                    if (temp < 0)
                    {
                        temp *= -1;

                        if (temp > (1 << wbits)) temp = (1 << wbits);

                        output.PackBits(NEGATIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                    else
                    {
                        if (temp > (1 << wbits)) temp = (1 << wbits);

                        output.PackBits(POSITIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                }
            }
        }

        public static float[] DecompressPatch(int[] patches, TerrainPatch.Header header, TerrainPatch.GroupHeader group)
        {
            float[] block = new float[group.PatchSize * group.PatchSize];
            float[] output = new float[group.PatchSize * group.PatchSize];
            int prequant = (header.QuantWBits >> 4) + 2;
            int quantize = 1 << prequant;
            float ooq = 1.0f / (float)quantize;
            float mult = ooq * (float)header.Range;
            float addval = mult * (float)(1 << (prequant - 1)) + header.DCOffset;

            if (group.PatchSize == 16)
            {
                for (int n = 0; n < 16 * 16; n++)
                {
                    block[n] = patches[CopyMatrix16[n]] * DequantizeTable16[n];
                }

                float[] ftemp = new float[16 * 16];

                for (int o = 0; o < 16; o++)
                    IDCTColumn16(block, ftemp, o);
                for (int o = 0; o < 16; o++)
                    IDCTLine16(ftemp, block, o);
            }
            else
            {
                for (int n = 0; n < 32 * 32; n++)
                {
                    block[n] = patches[CopyMatrix32[n]] * DequantizeTable32[n];
                }

                Logger.Log("Implement IDCTPatchLarge", Helpers.LogLevel.Error);
            }

            for (int j = 0; j < block.Length; j++)
            {
                output[j] = block[j] * mult + addval;
            }

            return output;
        }

        private static int[] CompressPatch(float[] patchData, TerrainPatch.Header header, int prequant)
        {
            float[] block = new float[16 * 16];
            int wordsize = prequant;
            float oozrange = 1.0f / (float)header.Range;
            float range = (float)(1 << prequant);
            float premult = oozrange * range;
            float sub = (float)(1 << (prequant - 1)) + header.DCOffset * premult;

            header.QuantWBits = wordsize - 2;
            header.QuantWBits |= (prequant - 2) << 4;

            int k = 0;
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                    block[k++] = patchData[j * 16 + i] * premult - sub;
            }

            float[] ftemp = new float[16 * 16];
            int[] itemp = new int[16 * 16];

            for (int o = 0; o < 16; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < 16; o++)
                DCTColumn16(ftemp, itemp, o);

            return itemp;
        }

        private static int[] CompressPatch(float[,] patchData, TerrainPatch.Header header, int prequant)
        {
            float[] block = new float[16 * 16];
            int wordsize = prequant;
            float oozrange = 1.0f / (float)header.Range;
            float range = (float)(1 << prequant);
            float premult = oozrange * range;
            float sub = (float)(1 << (prequant - 1)) + header.DCOffset * premult;

            header.QuantWBits = wordsize - 2;
            header.QuantWBits |= (prequant - 2) << 4;

            int k = 0;
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                    block[k++] = patchData[j, i] * premult - sub;
            }

            float[] ftemp = new float[16 * 16];
            int[] itemp = new int[16 * 16];

            for (int o = 0; o < 16; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < 16; o++)
                DCTColumn16(ftemp, itemp, o);

            return itemp;
        }

        private static int[] CompressPatch(float[] heightmap, int patchX, int patchY, TerrainPatch.Header header, int prequant)
        {
            float[] block = new float[16 * 16];
            int wordsize = prequant;
            float oozrange = 1.0f / (float)header.Range;
            float range = (float)(1 << prequant);
            float premult = oozrange * range;
            float sub = (float)(1 << (prequant - 1)) + header.DCOffset * premult;

            header.QuantWBits = wordsize - 2;
            header.QuantWBits |= (prequant - 2) << 4;

            int k = 0;
            for (int j = patchY * 16; j < (patchY + 1) * 16; j++)
            {
                for (int i = patchX * 16; i < (patchX + 1) * 16; i++)
                    block[k++] = heightmap[j * 256 + i] * premult - sub;
            }

            float[] ftemp = new float[16 * 16];
            int[] itemp = new int[16 * 16];

            for (int o = 0; o < 16; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < 16; o++)
                DCTColumn16(ftemp, itemp, o);

            return itemp;
        }


        #region Initialization

        private static void BuildDequantizeTable16()
        {
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    DequantizeTable16[j * 16 + i] = 1.0f + 2.0f * (float)(i + j);
                }
            }
        }

        private static void BuildQuantizeTable16()
        {
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    QuantizeTable16[j * 16 + i] = 1.0f / (1.0f + 2.0f * ((float)i + (float)j));
                }
            }
        }

        private static void SetupCosines16()
        {
            const float hposz = (float)Math.PI * 0.5f / 16.0f;

            for (int u = 0; u < 16; u++)
            {
                for (int n = 0; n < 16; n++)
                {
                    CosineTable16[u * 16 + n] = (float)Math.Cos((2.0f * (float)n + 1.0f) * (float)u * hposz);
                }
            }
        }

        private static void BuildCopyMatrix16()
        {
            bool diag = false;
            bool right = true;
            int i = 0;
            int j = 0;
            int count = 0;

            while (i < 16 && j < 16)
            {
                CopyMatrix16[j * 16 + i] = count++;

                if (!diag)
                {
                    if (right)
                    {
                        if (i < 16 - 1) i++;
                        else j++;

                        right = false;
                        diag = true;
                    }
                    else
                    {
                        if (j < 16 - 1) j++;
                        else i++;

                        right = true;
                        diag = true;
                    }
                }
                else
                {
                    if (right)
                    {
                        i++;
                        j--;
                        if (i == 16 - 1 || j == 0) diag = false;
                    }
                    else
                    {
                        i--;
                        j++;
                        if (j == 16 - 1 || i == 0) diag = false;
                    }
                }
            }
        }

        #endregion Initialization
    }
}
