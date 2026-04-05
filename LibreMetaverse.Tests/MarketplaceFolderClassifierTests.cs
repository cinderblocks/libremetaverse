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

using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Marketplace;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class MarketplaceFolderClassifierTests
    {
        private GridClient _client;
        private Inventory _inventory;
        private UUID _rootId;
        private UUID _listingsRootId;
        private UUID _listingFolderId;
        private UUID _versionFolderId;
        private UUID _stockFolderId;
        private UUID _contentFolderId;
        private UUID _itemId;

        [SetUp]
        public void SetUp()
        {
            _client = new GridClient();
            _inventory = new Inventory(_client, UUID.Random());
            BuildStandardTree();
        }

        [TearDown]
        public void TearDown()
        {
            try { _client.Dispose(); } catch { }
        }

        private void BuildStandardTree()
        {
            // My Inventory (root)
            _rootId = UUID.Random();
            _inventory.RootFolder = new InventoryFolder(_rootId)
            {
                Name = "My Inventory",
                ParentUUID = UUID.Zero
            };

            // Marketplace Listings root (ListingsRoot)
            _listingsRootId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(_listingsRootId)
            {
                Name = "Marketplace Listings",
                ParentUUID = _rootId,
                PreferredType = FolderType.MarketplaceListings
            });

            // Listing folder (Listing — direct child of ListingsRoot)
            _listingFolderId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(_listingFolderId)
            {
                Name = "My Listing",
                ParentUUID = _listingsRootId
            });

            // Version folder (Version)
            _versionFolderId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(_versionFolderId)
            {
                Name = "Version 1",
                ParentUUID = _listingFolderId,
                PreferredType = FolderType.MarketplaceVersion
            });

            // Stock folder inside version (Stock)
            _stockFolderId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(_stockFolderId)
            {
                Name = "Stock",
                ParentUUID = _versionFolderId,
                PreferredType = FolderType.MarketplaceStock
            });

            // Plain content folder inside version (Content)
            _contentFolderId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(_contentFolderId)
            {
                Name = "Content",
                ParentUUID = _versionFolderId
            });

            // Deliverable item inside version folder
            _itemId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryItem(_itemId)
            {
                Name = "Product",
                ParentUUID = _versionFolderId
            });
        }

        // ── GetRole ──────────────────────────────────────────────────────────────

        [Test]
        public void GetRole_ListingsRootFolder_ReturnsListingsRoot()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(_listingsRootId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.ListingsRoot));
        }

        [Test]
        public void GetRole_ListingFolder_ReturnsListing()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(_listingFolderId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.Listing));
        }

        [Test]
        public void GetRole_VersionFolder_ReturnsVersion()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(_versionFolderId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.Version));
        }

        [Test]
        public void GetRole_StockFolder_ReturnsStock()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(_stockFolderId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.Stock));
        }

        [Test]
        public void GetRole_ContentFolder_ReturnsContent()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(_contentFolderId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.Content));
        }

        [Test]
        public void GetRole_UnknownUUID_ReturnsNone()
        {
            Assert.That(MarketplaceFolderClassifier.GetRole(UUID.Random(), _inventory),
                Is.EqualTo(MarketplaceFolderRole.None));
        }

        [Test]
        public void GetRole_RegularFolderOutsideMarketplace_ReturnsNone()
        {
            var regularId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(regularId)
            {
                Name = "Objects",
                ParentUUID = _rootId
            });

            Assert.That(MarketplaceFolderClassifier.GetRole(regularId, _inventory),
                Is.EqualTo(MarketplaceFolderRole.None));
        }

        // ── IsMarketplaceFolder ───────────────────────────────────────────────────

        [Test]
        public void IsMarketplaceFolder_ListingFolder_ReturnsTrue()
        {
            Assert.That(MarketplaceFolderClassifier.IsMarketplaceFolder(_listingFolderId, _inventory),
                Is.True);
        }

        [Test]
        public void IsMarketplaceFolder_VersionFolder_ReturnsTrue()
        {
            Assert.That(MarketplaceFolderClassifier.IsMarketplaceFolder(_versionFolderId, _inventory),
                Is.True);
        }

        [Test]
        public void IsMarketplaceFolder_RegularFolderOutsideMarketplace_ReturnsFalse()
        {
            var regularId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(regularId)
            {
                Name = "Objects",
                ParentUUID = _rootId
            });

            Assert.That(MarketplaceFolderClassifier.IsMarketplaceFolder(regularId, _inventory),
                Is.False);
        }

        [Test]
        public void IsMarketplaceFolder_UnknownUUID_ReturnsFalse()
        {
            Assert.That(MarketplaceFolderClassifier.IsMarketplaceFolder(UUID.Random(), _inventory),
                Is.False);
        }

        // ── GetListingsRoot ───────────────────────────────────────────────────────

        [Test]
        public void GetListingsRoot_HasMarketplaceListingsFolder_ReturnsItsUUID()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingsRoot(_inventory),
                Is.EqualTo(_listingsRootId));
        }

        [Test]
        public void GetListingsRoot_NoMarketplaceListingsFolder_ReturnsZero()
        {
            var client = new GridClient();
            var inv = new Inventory(client, UUID.Random());
            var rootId = UUID.Random();
            inv.RootFolder = new InventoryFolder(rootId)
            {
                Name = "My Inventory",
                ParentUUID = UUID.Zero
            };
            inv.UpdateNodeFor(new InventoryFolder(UUID.Random())
            {
                Name = "Objects",
                ParentUUID = rootId
            });

            try
            {
                Assert.That(MarketplaceFolderClassifier.GetListingsRoot(inv), Is.EqualTo(UUID.Zero));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        [Test]
        public void GetListingsRoot_NoRootFolder_ReturnsZero()
        {
            var client = new GridClient();
            var inv = new Inventory(client, UUID.Random());
            try
            {
                Assert.That(MarketplaceFolderClassifier.GetListingsRoot(inv), Is.EqualTo(UUID.Zero));
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        // ── GetListingFolder ──────────────────────────────────────────────────────

        [Test]
        public void GetListingFolder_CalledOnListingFolder_ReturnsSelf()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingFolder(_listingFolderId, _inventory),
                Is.EqualTo(_listingFolderId));
        }

        [Test]
        public void GetListingFolder_CalledOnVersionFolder_ReturnsListingFolder()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingFolder(_versionFolderId, _inventory),
                Is.EqualTo(_listingFolderId));
        }

        [Test]
        public void GetListingFolder_CalledOnStockFolder_ReturnsListingFolder()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingFolder(_stockFolderId, _inventory),
                Is.EqualTo(_listingFolderId));
        }

        [Test]
        public void GetListingFolder_CalledOnListingsRoot_ReturnsZero()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingFolder(_listingsRootId, _inventory),
                Is.EqualTo(UUID.Zero));
        }

        [Test]
        public void GetListingFolder_CalledOnUnknownFolder_ReturnsZero()
        {
            Assert.That(MarketplaceFolderClassifier.GetListingFolder(UUID.Random(), _inventory),
                Is.EqualTo(UUID.Zero));
        }

        // ── GetVersionFolder ──────────────────────────────────────────────────────

        [Test]
        public void GetVersionFolder_ListingHasVersionFolder_ReturnsVersionUUID()
        {
            Assert.That(MarketplaceFolderClassifier.GetVersionFolder(_listingFolderId, _inventory),
                Is.EqualTo(_versionFolderId));
        }

        [Test]
        public void GetVersionFolder_ListingHasNoVersionFolder_ReturnsZero()
        {
            var emptyListingId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(emptyListingId)
            {
                Name = "Empty Listing",
                ParentUUID = _listingsRootId
            });

            Assert.That(MarketplaceFolderClassifier.GetVersionFolder(emptyListingId, _inventory),
                Is.EqualTo(UUID.Zero));
        }

        // ── GetStockCount ─────────────────────────────────────────────────────────

        [Test]
        public void GetStockCount_VersionFolderWithOneItem_ReturnsOne()
        {
            Assert.That(MarketplaceFolderClassifier.GetStockCount(_versionFolderId, _inventory),
                Is.EqualTo(1));
        }

        [Test]
        public void GetStockCount_VersionFolderWithOnlySubfolders_ReturnsZero()
        {
            var emptyVersionId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(emptyVersionId)
            {
                Name = "Version Empty",
                ParentUUID = _listingFolderId,
                PreferredType = FolderType.MarketplaceVersion
            });
            _inventory.UpdateNodeFor(new InventoryFolder(UUID.Random())
            {
                Name = "SubFolder",
                ParentUUID = emptyVersionId
            });

            Assert.That(MarketplaceFolderClassifier.GetStockCount(emptyVersionId, _inventory),
                Is.EqualTo(0));
        }

        [Test]
        public void GetStockCount_ZeroUUID_ReturnsZero()
        {
            Assert.That(MarketplaceFolderClassifier.GetStockCount(UUID.Zero, _inventory),
                Is.EqualTo(0));
        }

        [Test]
        public void GetStockCount_MultipleItems_ReturnsCorrectCount()
        {
            var versionId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(versionId)
            {
                Name = "Version Multi",
                ParentUUID = _listingFolderId,
                PreferredType = FolderType.MarketplaceVersion
            });
            _inventory.UpdateNodeFor(new InventoryItem(UUID.Random()) { Name = "Item A", ParentUUID = versionId });
            _inventory.UpdateNodeFor(new InventoryItem(UUID.Random()) { Name = "Item B", ParentUUID = versionId });
            _inventory.UpdateNodeFor(new InventoryItem(UUID.Random()) { Name = "Item C", ParentUUID = versionId });

            Assert.That(MarketplaceFolderClassifier.GetStockCount(versionId, _inventory),
                Is.EqualTo(3));
        }

        // ── GetAllListingFolderIds ────────────────────────────────────────────────

        [Test]
        public void GetAllListingFolderIds_SingleListingFolder_ReturnsOne()
        {
            var ids = MarketplaceFolderClassifier.GetAllListingFolderIds(_inventory);
            Assert.That(ids, Has.Count.EqualTo(1));
            Assert.That(ids, Does.Contain(_listingFolderId));
        }

        [Test]
        public void GetAllListingFolderIds_TwoListingFolders_ReturnsBoth()
        {
            var secondListingId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(secondListingId)
            {
                Name = "Second Listing",
                ParentUUID = _listingsRootId
            });

            var ids = MarketplaceFolderClassifier.GetAllListingFolderIds(_inventory);

            Assert.That(ids, Has.Count.EqualTo(2));
            Assert.That(ids, Does.Contain(_listingFolderId));
            Assert.That(ids, Does.Contain(secondListingId));
        }

        [Test]
        public void GetAllListingFolderIds_NoMarketplaceRoot_ReturnsEmpty()
        {
            var client = new GridClient();
            var inv = new Inventory(client, UUID.Random());
            try
            {
                var ids = MarketplaceFolderClassifier.GetAllListingFolderIds(inv);
                Assert.That(ids, Is.Empty);
            }
            finally
            {
                try { client.Dispose(); } catch { }
            }
        }

        // ── ValidateListing ───────────────────────────────────────────────────────

        [Test]
        public void ValidateListing_WellFormedListing_ReturnsValid()
        {
            Assert.That(MarketplaceFolderClassifier.ValidateListing(_listingFolderId, _inventory),
                Is.EqualTo(MarketplaceValidationFlags.Valid));
        }

        [Test]
        public void ValidateListing_NoVersionFolder_ReturnsMissingVersionFolder()
        {
            var emptyListingId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(emptyListingId)
            {
                Name = "No Version Listing",
                ParentUUID = _listingsRootId
            });

            Assert.That(MarketplaceFolderClassifier.ValidateListing(emptyListingId, _inventory),
                Is.EqualTo(MarketplaceValidationFlags.MissingVersionFolder));
        }

        [Test]
        public void ValidateListing_MultipleVersionFolders_ReturnsMultipleVersionFolders()
        {
            var listingId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(listingId)
            {
                Name = "Multi-Version Listing",
                ParentUUID = _listingsRootId
            });
            _inventory.UpdateNodeFor(new InventoryFolder(UUID.Random())
            {
                Name = "Version A",
                ParentUUID = listingId,
                PreferredType = FolderType.MarketplaceVersion
            });
            _inventory.UpdateNodeFor(new InventoryFolder(UUID.Random())
            {
                Name = "Version B",
                ParentUUID = listingId,
                PreferredType = FolderType.MarketplaceVersion
            });

            Assert.That(MarketplaceFolderClassifier.ValidateListing(listingId, _inventory),
                Is.EqualTo(MarketplaceValidationFlags.MultipleVersionFolders));
        }

        [Test]
        public void ValidateListing_EmptyVersionFolder_ReturnsEmptyListing()
        {
            var listingId = UUID.Random();
            _inventory.UpdateNodeFor(new InventoryFolder(listingId)
            {
                Name = "Empty Listing",
                ParentUUID = _listingsRootId
            });
            _inventory.UpdateNodeFor(new InventoryFolder(UUID.Random())
            {
                Name = "Version 1",
                ParentUUID = listingId,
                PreferredType = FolderType.MarketplaceVersion
            });

            Assert.That(MarketplaceFolderClassifier.ValidateListing(listingId, _inventory),
                Is.EqualTo(MarketplaceValidationFlags.EmptyListing));
        }

        [Test]
        public void ValidateListing_UnknownUUID_ReturnsInvalidStructure()
        {
            Assert.That(MarketplaceFolderClassifier.ValidateListing(UUID.Random(), _inventory),
                Is.EqualTo(MarketplaceValidationFlags.InvalidStructure));
        }

        [Test]
        public void ValidateListing_ValidFlags_AreBitmaskComposable()
        {
            var combined = MarketplaceValidationFlags.MissingVersionFolder | MarketplaceValidationFlags.EmptyListing;
            Assert.That(combined.HasFlag(MarketplaceValidationFlags.MissingVersionFolder), Is.True);
            Assert.That(combined.HasFlag(MarketplaceValidationFlags.EmptyListing), Is.True);
            Assert.That(combined.HasFlag(MarketplaceValidationFlags.MultipleVersionFolders), Is.False);
        }
    }
}
