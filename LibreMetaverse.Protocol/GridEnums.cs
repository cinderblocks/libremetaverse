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

using System;

namespace LibreMetaverse
{
    /// <summary>
    /// Map layer request type
    /// </summary>
    public enum GridLayerType : uint
    {
        /// <summary>Objects and terrain are shown</summary>
        Objects = 0,
        /// <summary>Only the terrain is shown, no objects</summary>
        Terrain = 1,
        /// <summary>Overlay showing land for sale and for auction</summary>
        LandForSale = 2
    }

    /// <summary>
    /// Type of grid item, such as telehub, event, popular location, etc.
    /// </summary>
    public enum GridItemType : uint
    {
        /// <summary>Telehub</summary>
        Telehub = 1,
        /// <summary>PG rated event</summary>
        PgEvent = 2,
        /// <summary>Mature rated event</summary>
        MatureEvent = 3,
        /// <summary>Popular location</summary>
        Popular = 4,
        /// <summary>Locations of avatar groups in a region</summary>
        AgentLocations = 6,
        /// <summary>Land for sale</summary>
        LandForSale = 7,
        /// <summary>Classified ad</summary>
        Classified = 8,
        /// <summary>Adult rated event</summary>
        AdultEvent = 9,
        /// <summary>Adult land for sale</summary>
        AdultLandForSale = 10
    }
}
