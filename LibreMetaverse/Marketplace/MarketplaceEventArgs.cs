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
using System.Collections.Generic;

namespace OpenMetaverse.Marketplace
{
    /// <summary>Raised after a successful fetch of all listings from the SLM backend.</summary>
    public class MarketplaceListingsSyncedEventArgs : EventArgs
    {
        /// <summary>Snapshot of all listings returned by the backend.</summary>
        public IReadOnlyList<MarketplaceListing> Listings { get; }

        public MarketplaceListingsSyncedEventArgs(IReadOnlyList<MarketplaceListing> listings)
            => Listings = listings;
    }

    /// <summary>Raised when a single listing is created, updated, or deleted.</summary>
    public class MarketplaceListingChangedEventArgs : EventArgs
    {
        /// <summary>The listing that changed. Null when the listing was deleted.</summary>
        public MarketplaceListing? Listing { get; }

        /// <summary>Listing ID of the changed listing (usable even when <see cref="Listing"/> is null).</summary>
        public int ListingId { get; }

        public MarketplaceListingChangedEventArgs(int listingId, MarketplaceListing? listing = null)
        {
            ListingId = listingId;
            Listing = listing;
        }
    }

    /// <summary>Raised when a Marketplace API call fails.</summary>
    public class MarketplaceErrorEventArgs : EventArgs
    {
        /// <summary>Human-readable description of the error.</summary>
        public string Message { get; }

        /// <summary>Underlying exception, if any.</summary>
        public Exception? Exception { get; }

        public MarketplaceErrorEventArgs(string message, Exception? ex = null)
        {
            Message = message;
            Exception = ex;
        }
    }
}
