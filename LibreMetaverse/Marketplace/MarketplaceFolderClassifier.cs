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
using System.Linq;

namespace OpenMetaverse.Marketplace
{
    /// <summary>
    /// Pure-logic utilities for classifying inventory folders within the Second Life
    /// Marketplace hierarchy and for validating listing structure.
    /// </summary>
    /// <remarks>
    /// All methods are stateless and side-effect-free; they derive answers solely from
    /// the supplied <see cref="Inventory"/> store snapshot.
    /// </remarks>
    public static class MarketplaceFolderClassifier
    {
        // ── Role classification ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="MarketplaceFolderRole"/> of <paramref name="folderId"/>
        /// within the Marketplace hierarchy.
        /// </summary>
        public static MarketplaceFolderRole GetRole(UUID folderId, Inventory inventory)
        {
            if (!inventory.TryGetValue<InventoryFolder>(folderId, out var folder))
                return MarketplaceFolderRole.None;

            switch (folder.PreferredType)
            {
                case FolderType.MarketplaceListings:
                    return MarketplaceFolderRole.ListingsRoot;
                case FolderType.MarketplaceVersion:
                    return MarketplaceFolderRole.Version;
                case FolderType.MarketplaceStock:
                    return MarketplaceFolderRole.Stock;
            }

            if (folder.ParentUUID == UUID.Zero)
                return MarketplaceFolderRole.None;

            // Direct child of the listings root → listing folder
            if (inventory.TryGetValue<InventoryFolder>(folder.ParentUUID, out var parent)
                && parent.PreferredType == FolderType.MarketplaceListings)
            {
                return MarketplaceFolderRole.Listing;
            }

            // Child of a version folder → content/stock folder
            if (inventory.TryGetValue<InventoryFolder>(folder.ParentUUID, out var parentFolder)
                && parentFolder.PreferredType == FolderType.MarketplaceVersion)
            {
                return MarketplaceFolderRole.Content;
            }

            return MarketplaceFolderRole.None;
        }

        /// <summary>
        /// Returns true when the folder with <paramref name="folderId"/> is anywhere inside
        /// the Marketplace subtree (role is not <see cref="MarketplaceFolderRole.None"/>).
        /// </summary>
        public static bool IsMarketplaceFolder(UUID folderId, Inventory inventory)
            => GetRole(folderId, inventory) != MarketplaceFolderRole.None;

        // ── Hierarchy navigation ─────────────────────────────────────────────────

        /// <summary>
        /// Finds the UUID of the Marketplace listings root folder, or
        /// <see cref="UUID.Zero"/> if none exists in the inventory.
        /// </summary>
        public static UUID GetListingsRoot(Inventory inventory)
        {
            var root = inventory.RootFolder;
            if (root == null) return UUID.Zero;

            foreach (var item in inventory.GetContents(root.UUID))
            {
                if (item is InventoryFolder f && f.PreferredType == FolderType.MarketplaceListings)
                    return f.UUID;
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Returns the UUID of the listing folder that contains <paramref name="folderId"/>,
        /// walking up the tree as needed. Returns <see cref="UUID.Zero"/> if the folder is
        /// not inside a listing.
        /// </summary>
        public static UUID GetListingFolder(UUID folderId, Inventory inventory)
        {
            var current = folderId;
            for (int depth = 0; depth < 16; depth++)
            {
                if (!inventory.TryGetValue<InventoryFolder>(current, out var folder))
                    break;

                var role = GetRole(current, inventory);
                if (role == MarketplaceFolderRole.Listing) return current;
                if (role == MarketplaceFolderRole.ListingsRoot || role == MarketplaceFolderRole.None)
                    break;

                current = folder.ParentUUID;
                if (current == UUID.Zero) break;
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Returns the UUID of the version folder that is a direct child of
        /// <paramref name="listingFolderUUID"/>, or <see cref="UUID.Zero"/> if not found.
        /// When multiple version folders exist, returns the first one found.
        /// </summary>
        public static UUID GetVersionFolder(UUID listingFolderUUID, Inventory inventory)
        {
            foreach (var item in inventory.GetContents(listingFolderUUID))
            {
                if (item is InventoryFolder f && f.PreferredType == FolderType.MarketplaceVersion)
                    return f.UUID;
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Counts the number of deliverable items (non-folder inventory items) directly
        /// inside the version folder. Returns 0 if the version folder does not exist.
        /// </summary>
        public static int GetStockCount(UUID versionFolderUUID, Inventory inventory)
        {
            if (versionFolderUUID == UUID.Zero) return 0;
            return inventory.GetContents(versionFolderUUID)
                .Count(item => item is InventoryItem);
        }

        // ── Reverse lookups ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the UUIDs of all listing folders (direct children of the listings root).
        /// </summary>
        public static IReadOnlyList<UUID> GetAllListingFolderIds(Inventory inventory)
        {
            var rootId = GetListingsRoot(inventory);
            if (rootId == UUID.Zero) return Array.Empty<UUID>();

            var result = new List<UUID>();
            foreach (var item in inventory.GetContents(rootId))
            {
                if (item is InventoryFolder) result.Add(item.UUID);
            }
            return result;
        }

        // ── Validation ────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the structural integrity of a listing folder.
        /// Returns a bitmask of <see cref="MarketplaceValidationFlags"/> describing any problems.
        /// </summary>
        public static MarketplaceValidationFlags ValidateListing(UUID listingFolderUUID, Inventory inventory)
        {
            if (!inventory.TryGetValue<InventoryFolder>(listingFolderUUID, out _))
                return MarketplaceValidationFlags.InvalidStructure;

            var children = inventory.GetContents(listingFolderUUID);
            var versionFolders = children
                .OfType<InventoryFolder>()
                .Where(f => f.PreferredType == FolderType.MarketplaceVersion)
                .ToList();

            var flags = MarketplaceValidationFlags.Valid;

            if (versionFolders.Count == 0)
            {
                flags |= MarketplaceValidationFlags.MissingVersionFolder;
            }
            else if (versionFolders.Count > 1)
            {
                flags |= MarketplaceValidationFlags.MultipleVersionFolders;
            }
            else
            {
                var versionContents = inventory.GetContents(versionFolders[0].UUID);
                if (!versionContents.Any(item => item is InventoryItem))
                    flags |= MarketplaceValidationFlags.EmptyListing;
            }

            return flags;
        }
    }

    /// <summary>
    /// Bitmask of validation issues found in a listing folder's structure.
    /// </summary>
    [Flags]
    public enum MarketplaceValidationFlags
    {
        /// <summary>No structural problems detected.</summary>
        Valid = 0,

        /// <summary>The listing folder contains no version folder.</summary>
        MissingVersionFolder = 1 << 0,

        /// <summary>The listing folder contains more than one version folder.</summary>
        MultipleVersionFolders = 1 << 1,

        /// <summary>The version folder contains no deliverable items.</summary>
        EmptyListing = 1 << 2,

        /// <summary>Backend listing ID has not been associated with this folder.</summary>
        NotRegisteredWithServer = 1 << 3,

        /// <summary>Local folder UUID does not match the backend-recorded listing folder UUID.</summary>
        ServerMetadataMismatch = 1 << 4,

        /// <summary>The folder is not in a valid position within the Marketplace subtree.</summary>
        InvalidStructure = 1 << 5,
    }
}
