using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class RemOutfitCommandTests : RestrictionsBase
    {
        #region @remoutfit[:<folder|layer>]=force
        [Fact]
        public async Task RemOutfitForce()
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
        public async Task RemOutfitForce_ExternalItems()
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
            //        |= Watch (worn tattoo)
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
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo);

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
        public async Task RemOutfitForce_ExternalItems_ByType()
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
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo);

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
        public async Task RemOutfitForce_Folder()
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
        public async Task RemOutfitForce_Specific()
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
        public async Task RemOutfitForce_BodyPart_Specific()
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
