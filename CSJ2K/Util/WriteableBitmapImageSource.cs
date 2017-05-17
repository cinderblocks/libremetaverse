// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util
{
    using System;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    using CSJ2K.j2k.image;
    using CSJ2K.j2k.image.input;

    internal class WriteableBitmapImageSource : ImgReader
    {
        #region FIELDS

        private readonly WriteableBitmap wbm;

        private readonly int rb;

        private readonly int redIdx;

        private readonly int greenIdx;

        private readonly int blueIdx;

        private readonly int pixelSize;

        private readonly bool isPremultiplied;

        #endregion

        #region CONSTRUCTORS

        private WriteableBitmapImageSource(WriteableBitmap wbm)
        {
            this.wbm = wbm;
            this.w = wbm.PixelWidth;
            this.h = wbm.PixelHeight;
            this.nc = GetNumberOfComponents(wbm.Format);
            this.rb = GetRangeBits(wbm.Format);

            this.DefineHelpers(wbm.Format);
        }

        #endregion

        #region METHODS

        public override void close()
        {
            // Do nothing.
        }

        public override bool isOrigSigned(int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            return false;
        }

        public override int getFixedPoint(int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            return 0;
        }

        public override DataBlk getInternCompData(DataBlk blk, int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            blk.offset = 0;
            blk.scanw = blk.w;
            blk.progressive = false;
            blk.Data = this.GetDataArray(blk.ulx, blk.uly, blk.w, blk.h);

            return blk;
        }

        public override DataBlk getCompData(DataBlk blk, int c)
        {
            var newBlk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
            return this.getInternCompData(newBlk, c);
        }

        public override int getNomRangeBits(int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            return this.rb;
        }

        public static ImgReader Create(object imageObject)
        {
            var wbm = imageObject as WriteableBitmap;
            return wbm == null ? null : new WriteableBitmapImageSource(wbm);
        }

        private void DefineHelpers(PixelFormat format)
        {
            throw new NotImplementedException();
        }

        private Array GetDataArray(int x0, int y0, int w, int h)
        {
            throw new NotImplementedException();
        }

        private static int GetNumberOfComponents(PixelFormat format)
        {
            if (format.Equals(PixelFormats.BlackWhite) || format.Equals(PixelFormats.Gray8))
            {
                return 1;
            }

            if (format.Equals(PixelFormats.Bgra32) || format.Equals(PixelFormats.Bgr24)
                || format.Equals(PixelFormats.Bgr32) || format.Equals(PixelFormats.Pbgra32)
                || format.Equals(PixelFormats.Rgb24))
            {
                return 3;
            }

            throw new ArgumentOutOfRangeException("format");
        }

        private static int GetRangeBits(PixelFormat format)
        {
            return format.BitsPerPixel;
        }

        #endregion
    }
}
