using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class RemOutfitCommandTests : RestrictionsBase
    {
        #region @remoutfit[:<folder|layer>]=force
        [Fact]
        public async Task RemOutfit()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt) <-- Expect removed
            //  |    |= Retro Pants (worn pants) <-- Expect removed
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                 sampleTree.Root_Clothing_RetroPants.Id,
                 sampleTree.Root_Clothing_HappyShirt.Id,
                 sampleTree.Root_Accessories_Watch.Id,
                 sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_PrivateFolders()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt) <-- Expect removed
            //  |    |= Retro Pants (worn pants) <-- Expect removed
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".Clothing";

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                 sampleTree.Root_Clothing_RetroPants.Id,
                 sampleTree.Root_Clothing_HappyShirt.Id,
                 sampleTree.Root_Accessories_Watch.Id,
                 sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_ExternalItems()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt) <-- Expect removed
            //  |    |= Retro Pants (worn pants) <-- Expect removed
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //
            // External
            //   \= External Tattoo (worn tattoo) <-- Expect removed
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                 sampleTree.Root_Clothing_RetroPants.Id,
                 sampleTree.Root_Clothing_HappyShirt.Id,
                 sampleTree.Root_Accessories_Watch.Id,
                 sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                externalWearable.Id
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_ExternalItems_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (worn shirt) <-- Not removed due to 'nostrip' in item name
            //  |    |= Retro Pants (worn pants) <-- Expect removed
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Not removed due to 'nostrip' in folder name'
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //
            // External
            //   |= External Tattoo (worn tattoo) <-- Not removed due to 'nostrip' in item name
            //   \= nostrip External Tattoo (worn tattoo) <-- Expect removed
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var externalWearable2 = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "nostrip External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable, externalWearable2]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Watch.Id,
                externalWearable.Id
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_ExternalItems_NoStrip_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (worn shirt) <-- Not removed due to 'nostrip' in item name
            //  |    |= Retro Pants (worn pants) <-- Expect removed
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Removed because it's an item link and folder name is ignored
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //
            // External
            //   |= External Tattoo (worn tattoo) <-- Not removed due to 'nostrip' in item name
            //   \= nostrip External Tattoo (worn tattoo) <-- Expect removed
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.IsLink = true;

            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var externalWearable2 = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "nostrip External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable, externalWearable2]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Watch.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                externalWearable.Id
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_ExternalItems_ByType()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //
            // External
            //   \= External Tattoo (worn tattoo) <-- Expect removed
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Accessories_Watch.Id,
                externalWearable.Id
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:tattoo=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Folder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:Clothing/Hats=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Folder_PrivateFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- .Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = ".Hats";

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:Clothing/Hats=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Folder_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= nostrip Fancy Hat (worn tattoo) <-- Not removed due to 'nostrip' in item name
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name = "nostrip Fancy Hat";
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:Clothing/Hats=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Folder_NoStripFolderName()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Not removed due to 'nostrip' in folder name
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
            };

            // Act
            await _rlv.ProcessMessage($"@remoutfit:Clothing/{sampleTree.Clothing_Hats_Folder.Name}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Folder_NoStripFolderName_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Removed because it's an item link and folder name is ignored
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.IsLink = true;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id
            };

            // Act
            await _rlv.ProcessMessage($"@remoutfit:Clothing/{sampleTree.Clothing_Hats_Folder.Name}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Specific()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Accessories_Watch.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:tattoo=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Specific_PrivateFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- .Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo) <-- Expect removed
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo) <-- Expect removed
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = ".Hats";

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Accessories_Watch.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:tattoo=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Specific_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants (worn pants) <-- No unwear due to 'nostrip' in folder name
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat (worn pants) <-- Expect removed
            //   \-Accessories
            //        |= nostrip Watch (worn pants) <-- No unwear due to 'nostrip' in item name
            //        \= Glasses (worn tattoo)
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Accessories_Watch.Name = "nostrip Watch";
            sampleTree.Clothing_Folder.Name = "nostrip clothing";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:pants=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_Specific_NoStrip_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants (worn pants) <-- Expect remove because it's an item link and folder name is ignored
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat (worn pants) <-- Expect removed
            //   \-Accessories
            //        |= nostrip Watch (worn pants) <-- No unwear due to 'nostrip' in item name
            //        \= Glasses (worn tattoo)
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_RetroPants.IsLink = true;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Tattoo;

            sampleTree.Root_Accessories_Watch.Name = "nostrip Watch";
            sampleTree.Clothing_Folder.Name = "nostrip clothing";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>
            {
                sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@remoutfit:pants=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task RemOutfit_BodyPart_Specific()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn shirt)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn tattoo)
            //  |        \= Party Hat (worn skin, must not be removed)
            //   \-Accessories
            //        |= Watch (worn tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Skin;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.RemOutfitAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>();

            // Act
            await _rlv.ProcessMessage("@remoutfit:skin=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.RemOutfitAsync(
                    It.Is<IReadOnlyList<Guid>>(ids =>
                        ids != null &&
                        ids.Count == expected.Count &&
                        expected.SetEquals(ids)
                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );

            _actionCallbacks.VerifyNoOtherCalls();
        }

        #endregion

    }
}
