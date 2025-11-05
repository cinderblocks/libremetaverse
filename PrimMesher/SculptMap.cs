/*
 * Copyright (c) Contributors
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace LibreMetaverse.PrimMesher
{
    public class SculptMap
    {
        public byte[] blueBytes;
        public byte[] greenBytes;
        public int height;
        public byte[] redBytes;
        public int width;

        public SculptMap()
        {
        }

        public SculptMap(SKBitmap bm, int lod)
        {
            if (bm == null) throw new ArgumentNullException(nameof(bm));

            var bmW = bm.Width;
            var bmH = bm.Height;

            if (bmW == 0 || bmH == 0)
                throw new Exception("SculptMap: bitmap has no data");

            // desired pixel budget for LOD
            var numLodPixels = (lod * 2) * (lod * 2);

            var needsScaling = false;
            var smallMap = bmW * bmH <= lod * lod;

            // compute target width/height by repeatedly halving until under budget
            width = bmW;
            height = bmH;
            while (width * height > numLodPixels)
            {
                width >>= 1;
                height >>= 1;
                needsScaling = true;
            }

            SKBitmap scaledBitmap = null;
            SKBitmap srcBitmap = bm;

            try
            {
                if (needsScaling)
                {
                    scaledBitmap = ScaleImage(bm, width, height);
                    // use scaled bitmap for pixel reads, keep original alive for caller
                    srcBitmap = scaledBitmap;
                }

                // final shrink if still larger than lod*lod
                if (width * height > lod * lod)
                {
                    width >>= 1;
                    height >>= 1;
                }

                // allocate arrays: smallMap uses exact size, otherwise allocate (width+1)*(height+1)
                var numBytes = smallMap ? width * height : (width + 1) * (height + 1);
                redBytes = new byte[numBytes];
                greenBytes = new byte[numBytes];
                blueBytes = new byte[numBytes];

                var pix = srcBitmap.PeekPixels(); // low-overhead access to pixel data
                var byteNdx = 0;

                if (smallMap)
                {
                    // tight loop: avoid bounds checks and repeated property access
                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            var c = pix.GetPixelColor(x, y);
                            redBytes[byteNdx] = c.Red;
                            greenBytes[byteNdx] = c.Green;
                            blueBytes[byteNdx] = c.Blue;
                            ++byteNdx;
                        }
                    }
                }
                else
                {
                    // we sample a 2x grid into a (width+1)x(height+1) buffer as original logic
                    for (var y = 0; y <= height; y++)
                    {
                        // compute sample Y (clamped to source)
                        var sy = (y < height) ? (y * 2) : (y * 2 - 1);
                        for (var x = 0; x <= width; x++)
                        {
                            var sx = (x < width) ? (x * 2) : (x * 2 - 1);
                            var c = pix.GetPixelColor(sx, sy);
                            redBytes[byteNdx] = c.Red;
                            greenBytes[byteNdx] = c.Green;
                            blueBytes[byteNdx] = c.Blue;
                            ++byteNdx;
                        }
                    }
                }

                // when not smallMap the consumer expects width/height to be incremented
                if (!smallMap)
                {
                    width++;
                    height++;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Caught exception processing byte arrays in SculptMap(): e: " + e);
            }
            finally
            {
                // dispose only the scaled bitmap we created locally
                if (scaledBitmap != null)
                    scaledBitmap.Dispose();
            }
        }

        public List<List<Coord>> ToRows(bool mirror)
        {
            var numRows = height;
            var numCols = width;

            var rows = new List<List<Coord>>(numRows);

            const float pixScale = 1.0f / 255.0f;

            var smNdx = 0;
            for (var rowNdx = 0; rowNdx < numRows; rowNdx++)
            {
                var row = new List<Coord>(numCols);
                for (var colNdx = 0; colNdx < numCols; colNdx++)
                {
                    var r = redBytes[smNdx] * pixScale - 0.5f;
                    var g = greenBytes[smNdx] * pixScale - 0.5f;
                    var b = blueBytes[smNdx] * pixScale - 0.5f;

                    row.Add(mirror ? new Coord(-r, g, b) : new Coord(r, g, b));
                    ++smNdx;
                }
                rows.Add(row);
            }
            return rows;
        }

        private SKBitmap ScaleImage(SKBitmap srcImage, int destWidth, int destHeight)
        {
            var info = new SKImageInfo(destWidth, destHeight);
            var scaledImage = new SKBitmap(info);
            srcImage.ScalePixels(scaledImage.PeekPixels(), new SKSamplingOptions(SKFilterMode.Linear));
            return scaledImage;
        }
    }
}