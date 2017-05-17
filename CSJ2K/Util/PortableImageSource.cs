// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

namespace CSJ2K.Util    
{
    using System;

    using CSJ2K.j2k;
    using CSJ2K.j2k.image;

    public class PortableImageSource : BlkImgDataSrc
    {
        #region FIELDS

        private readonly int w;

        private readonly int h;

        private readonly int nc;

        private readonly int rb;

        private readonly bool[] sgnd;

        private readonly int[][] comps;

        #endregion

        #region CONSTRUCTORS

        public PortableImageSource(int w, int h, int nc, int rb, bool[] sgnd, int[][] comps)
        {
            this.w = w;
            this.h = h;
            this.nc = nc;
            this.rb = rb;
            this.sgnd = sgnd;
            this.comps = comps;
        }

        #endregion

        #region PROPERTIES

        public int TileWidth
        {
            get
            {
                return this.w;
            }
        }

        public int TileHeight
        {
            get
            {
                return this.h;
            }
        }

        public int NomTileWidth
        {
            get
            {
                return this.w;
            }
        }

        public int NomTileHeight {
            get
            {
                return this.h;
            }
        }

        public int ImgWidth {
            get
            {
                return this.w;
            }
        }

        public int ImgHeight {
            get
            {
                return this.h;
            }
        }

        public int NumComps {
            get
            {
                return this.nc;
            }
        }

        public int TileIdx {
            get
            {
                return 0;
            }
        }

        public int TilePartULX {
            get
            {
                return 0;
            }
        }

        public int TilePartULY {
            get
            {
                return 0;
            }
        }

        public int ImgULX {
            get
            {
                return 0;
            }
        }

        public int ImgULY {
            get
            {
                return 0;
            }
        }

        #endregion

        #region METHODS

        public int getCompSubsX(int c)
        {
            return 1;
        }

        public int getCompSubsY(int c)
        {
            return 1;
        }

        public int getTileCompWidth(int t, int c)
        {
            if (t != 0)
            {
                throw new System.InvalidOperationException("Asking a tile-component width for a tile index" + " greater than 0 whereas there is only one tile");
            }
            return this.w;
        }

        public int getTileCompHeight(int t, int c)
        {
            if (t != 0)
            {
                throw new System.InvalidOperationException("Asking a tile-component width for a tile index" + " greater than 0 whereas there is only one tile");
            }
            return this.h;
        }

        public int getCompImgWidth(int c)
        {
            return this.w;
        }

        public int getCompImgHeight(int c)
        {
            return this.h;
        }

        public int getNomRangeBits(int c)
        {
            return this.rb;
        }

        public void setTile(int x, int y)
        {
            if (x != 0 || y != 0)
            {
                throw new System.ArgumentException();
            }
        }

        public void nextTile()
        {
            throw new NoNextElementException();
        }

        public Coord getTile(Coord co)
        {
            if (co != null)
            {
                co.x = 0;
                co.y = 0;
                return co;
            }

            return new Coord(0, 0);
        }

        public int getCompULX(int c)
        {
            return 0;
        }

        public int getCompULY(int c)
        {
            return 0;
        }

        public Coord getNumTiles(Coord co)
        {
            if (co != null)
            {
                co.x = 1;
                co.y = 1;
                return co;
            }

            return new Coord(1, 1);
        }

        public int getNumTiles()
        {
            return 1;
        }

        public int getFixedPoint(int c)
        {
            return 0;
        }

        public DataBlk getInternCompData(DataBlk blk, int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            var data = new int[blk.w * blk.h];
            for (int y = blk.uly, k = 0; y < blk.uly + blk.h; ++y)
            {
                for (int x = blk.ulx, xy = blk.uly * this.w + blk.ulx; x < blk.ulx + blk.w; ++x, ++k, ++xy)
                {
                    data[k] = this.comps[c][xy];
                }
            }

            blk.offset = 0;
            blk.scanw = blk.w;
            blk.progressive = false;
            blk.Data = data;

            return blk;
        }

        public DataBlk getCompData(DataBlk blk, int c)
        {
            var newBlk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
            return this.getInternCompData(newBlk, c);
        }

        public void close()
        {
            // Do nothing.
        }

        public bool isOrigSigned(int c)
        {
            if (c < 0 || c >= this.nc)
            {
                throw new ArgumentOutOfRangeException("c");
            }

            return this.sgnd[c];
        }

        #endregion
    }
}
