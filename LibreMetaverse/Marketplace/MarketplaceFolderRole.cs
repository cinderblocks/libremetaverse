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

namespace OpenMetaverse.Marketplace
{
    /// <summary>
    /// Classifies the role a folder plays within the Second Life Marketplace inventory hierarchy.
    /// </summary>
    /// <remarks>
    /// The Marketplace subtree is structured as:
    /// <code>
    /// My Inventory/
    ///   Marketplace Listings/       ← ListingsRoot  (FolderType.MarketplaceListings)
    ///     Listing Folder Name/      ← Listing       (direct child of ListingsRoot)
    ///       Version Folder/         ← Version       (FolderType.MarketplaceVersion)
    ///         Content Folder/       ← Stock         (FolderType.MarketplaceStock)
    ///         Other Content/        ← Content       (any other folder inside Version)
    /// </code>
    /// </remarks>
    public enum MarketplaceFolderRole
    {
        /// <summary>Not part of the Marketplace subtree.</summary>
        None,

        /// <summary>
        /// The single root folder for Marketplace listings
        /// (<see cref="FolderType.MarketplaceListings"/>).
        /// </summary>
        ListingsRoot,

        /// <summary>
        /// A listing folder — a direct child of the listings root. One listing folder
        /// corresponds to one Marketplace listing on the backend.
        /// </summary>
        Listing,

        /// <summary>
        /// The version folder inside a listing folder
        /// (<see cref="FolderType.MarketplaceVersion"/>).
        /// There should be exactly one per listing.
        /// </summary>
        Version,

        /// <summary>
        /// A stock folder inside a version folder
        /// (<see cref="FolderType.MarketplaceStock"/>).
        /// Used for limited-quantity / copy-protected products.
        /// </summary>
        Stock,

        /// <summary>
        /// Any other folder nested inside a version folder that is not a dedicated stock folder.
        /// </summary>
        Content
    }
}
