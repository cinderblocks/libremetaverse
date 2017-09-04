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
using System.Drawing;
using System.Drawing.Drawing2D;

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

        public SculptMap(Bitmap bm, int lod)
        {
            var bmW = bm.Width;
            var bmH = bm.Height;

            if (bmW == 0 || bmH == 0)
                throw new Exception("SculptMap: bitmap has no data");

            var numLodPixels = lod * 2 * lod * 2; // (32 * 2)^2  = 64^2 pixels for default sculpt map image

            var needsScaling = false;

            var smallMap = bmW * bmH <= lod * lod;

            width = bmW;
            height = bmH;
            while (width * height > numLodPixels)
            {
                width >>= 1;
                height >>= 1;
                needsScaling = true;
            }


            try
            {
                if (needsScaling)
                    bm = ScaleImage(bm, width, height,
                        InterpolationMode.NearestNeighbor);
            }

            catch (Exception e)
            {
                throw new Exception("Exception in ScaleImage(): e: " + e);
            }

            if (width * height > lod * lod)
            {
                width >>= 1;
                height >>= 1;
            }

            var numBytes = smallMap ? width * height : (width + 1) * (height + 1);
            redBytes = new byte[numBytes];
            greenBytes = new byte[numBytes];
            blueBytes = new byte[numBytes];

            var byteNdx = 0;

            try
            {
                if (smallMap)
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var c = bm.GetPixel(x, y);

                        redBytes[byteNdx] = c.R;
                        greenBytes[byteNdx] = c.G;
                        blueBytes[byteNdx] = c.B;

                        ++byteNdx;
                    }
                else
                    for (var y = 0; y <= height; y++)
                    for (var x = 0; x <= width; x++)
                    {
                        var c = bm.GetPixel(x < width ? x * 2 : x * 2 - 1,
                            y < height ? y * 2 : y * 2 - 1);

                        redBytes[byteNdx] = c.R;
                        greenBytes[byteNdx] = c.G;
                        blueBytes[byteNdx] = c.B;

                        ++byteNdx;
                    }
            }
            catch (Exception e)
            {
                throw new Exception("Caught exception processing byte arrays in SculptMap(): e: " + e);
            }

            if (!smallMap)
            {
                width++;
                height++;
            }
        }

        public List<List<Coord>> ToRows(bool mirror)
        {
            var numRows = height;
            var numCols = width;

            var rows = new List<List<Coord>>(numRows);

            var pixScale = 1.0f / 255;

            int rowNdx, colNdx;
            var smNdx = 0;

            for (rowNdx = 0; rowNdx < numRows; rowNdx++)
            {
                var row = new List<Coord>(numCols);
                for (colNdx = 0; colNdx < numCols; colNdx++)
                {
                    if (mirror)
                        row.Add(new Coord(-(redBytes[smNdx] * pixScale - 0.5f), greenBytes[smNdx] * pixScale - 0.5f,
                            blueBytes[smNdx] * pixScale - 0.5f));
                    else
                        row.Add(new Coord(redBytes[smNdx] * pixScale - 0.5f, greenBytes[smNdx] * pixScale - 0.5f,
                            blueBytes[smNdx] * pixScale - 0.5f));

                    ++smNdx;
                }
                rows.Add(row);
            }
            return rows;
        }

        private Bitmap ScaleImage(Bitmap srcImage, int destWidth, int destHeight,
            InterpolationMode interpMode)
        {
            var scaledImage = new Bitmap(srcImage, destWidth, destHeight);
            scaledImage.SetResolution(96.0f, 96.0f);

            var grPhoto = Graphics.FromImage(scaledImage);
            grPhoto.InterpolationMode = interpMode;

            grPhoto.DrawImage(srcImage,
                new Rectangle(0, 0, destWidth, destHeight),
                new Rectangle(0, 0, srcImage.Width, srcImage.Height),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return scaledImage;
        }
    }
}