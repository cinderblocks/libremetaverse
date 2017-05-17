// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Linq;

    using CSJ2K.j2k.image;

    internal class BitmapImageSource : PortableImageSource
    {
        #region CONSTRUCTORS

        private BitmapImageSource(Bitmap bitmap)
            : base(
                bitmap.Width,
                bitmap.Height,
                GetNumberOfComponents(bitmap.PixelFormat),
                GetRangeBits(bitmap.PixelFormat),
                GetSignedArray(bitmap.PixelFormat),
                GetComponents(bitmap))
        {
        }

        #endregion

        #region METHODS


        internal static BlkImgDataSrc Create(object imageObject)
        {
            var bitmap = imageObject as Bitmap;
            return bitmap == null ? null : new BitmapImageSource(bitmap);
        }

        private static int GetNumberOfComponents(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format16bppGrayScale:
                case PixelFormat.Format1bppIndexed:
                case PixelFormat.Format4bppIndexed:
                case PixelFormat.Format8bppIndexed:
                    return 1;
                case PixelFormat.Format24bppRgb:
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException("pixelFormat");
            }
        }

        private static int GetRangeBits(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format16bppGrayScale:
                    return 16;
                case PixelFormat.Format1bppIndexed:
                    return 1;
                case PixelFormat.Format4bppIndexed:
                    return 4;
                case PixelFormat.Format8bppIndexed:
                case PixelFormat.Format24bppRgb:
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 8;
                default:
                    throw new ArgumentOutOfRangeException("pixelFormat");
            }
        }

        private static bool[] GetSignedArray(PixelFormat pixelFormat)
        {
            return Enumerable.Repeat(false, GetNumberOfComponents(pixelFormat)).ToArray();
        }

        private static int[][] GetComponents(Bitmap bitmap)
        {
            var w = bitmap.Width;
            var h = bitmap.Height;
            var nc = GetNumberOfComponents(bitmap.PixelFormat);

            var comps = new int[nc][];
            for (var c = 0; c < nc; ++c) comps[c] = new int[w * h];

            for (int y = 0, xy = 0; y < h; ++y)
            {
                for (var x = 0; x < w; ++x, ++xy)
                {
                    var color = bitmap.GetPixel(x, y);
                    for (var c = 0; c < nc; ++c)
                    {
                        comps[c][xy] = c == 0 ? color.R : c == 1 ? color.G : color.B;
                    }
                }
            }

            return comps;
        }

        #endregion
    }
}
