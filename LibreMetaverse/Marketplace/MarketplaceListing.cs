/*
 * Copyright (c) 2025-2026, Sjofn LLC.
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

namespace OpenMetaverse.Marketplace
{
    /// <summary>
    /// Active/inactive state of a listing as reported by the SLM backend.
    /// </summary>
    public enum MarketplaceListingStatus
    {
        /// <summary>Listing is visible and active for buyers.</summary>
        Listed,
        /// <summary>Listing exists but is hidden from buyers.</summary>
        Unlisted,
        /// <summary>Status has not yet been retrieved from the backend.</summary>
        Unknown
    }

    /// <summary>
    /// Backend record for a single Second Life Marketplace listing.
    /// Combines the SLM REST API data with the local inventory folder UUID.
    /// </summary>
    public class MarketplaceListing
    {
        /// <summary>Integer listing ID assigned by the SLM backend.</summary>
        public int ListingId { get; set; }

        /// <summary>UUID of the listing folder in the agent's inventory.</summary>
        public UUID ListingFolderUUID { get; set; }

        /// <summary>UUID of the version folder inside the listing folder.</summary>
        public UUID VersionFolderUUID { get; set; }

        /// <summary>Listing title shown on the Marketplace web page.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Listing description shown on the Marketplace web page.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Price in Linden Dollars.</summary>
        public int PriceLinden { get; set; }

        /// <summary>Active/inactive status from the SLM backend.</summary>
        public MarketplaceListingStatus Status { get; set; } = MarketplaceListingStatus.Unknown;

        /// <summary>Number of deliverable copies currently in stock as reported by the backend.</summary>
        public int StockCount { get; set; }

        /// <summary>URL to edit this listing on the Marketplace web site, if available.</summary>
        public string? EditUrl { get; set; }

        /// <summary>UTC timestamp of the last successful sync with the SLM backend.</summary>
        public DateTime LastSyncUtc { get; set; } = DateTime.MinValue;
    }
}
