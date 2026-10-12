/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn, LLC.
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

namespace LibreMetaverse
{
    /// <summary>
	/// Information about a region on the grid map
	/// </summary>
	public struct GridRegion
	{
        /// <summary>Sim X position on World Map</summary>
		public int X;
        /// <summary>Sim Y position on World Map</summary>
		public int Y;
        /// <summary>Sim Name (NOTE: In lowercase!)</summary>
		public string Name;
        /// <summary>Access level</summary>
		public SimAccess Access;
        /// <summary>Appears to always be zero (None)</summary>
        public RegionFlags RegionFlags;
        /// <summary>Water Height</summary>
		public byte WaterHeight;
        /// <summary></summary>
		public byte Agents;
        /// <summary>UUID of the World Map image</summary>
		public UUID MapImageID;
        /// <summary>Unique identifier for this region, a combination of the X 
        /// and Y position</summary>
		public ulong RegionHandle;

        public override string ToString()
        {
            return $"{Name} ({X}/{Y}), Handle: {RegionHandle}, MapImage: {MapImageID}, Access: {Access}";
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return (obj is GridRegion region) && Equals(region);
        }

        private bool Equals(GridRegion region)
        {
            return this.X == region.X && this.Y == region.Y;
        }
	}

    /// <summary>
    /// Visual chunk of the grid map
    /// </summary>
    public struct GridLayer
    {
        public int Bottom;
        public int Left;
        public int Top;
        public int Right;
        public UUID ImageID;

        public bool ContainsRegion(int x, int y)
        {
            return x >= Left && x <= Right && y >= Bottom && y <= Top;
        }
    }
}
