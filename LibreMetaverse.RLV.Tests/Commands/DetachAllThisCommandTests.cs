using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class DetachAllThisCommandTests : RestrictionsBase
    {
        #region @detachallthis[:<attachpt> or <clothing_layer>]=force
        [Fact]
        public async Task DetachAllThisForce_Default()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt [SENDER] (attached chest) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expected detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0004-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachallthis=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.DetachAsync(
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
        public async Task DetachAllThisForce_ById()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expected detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0004-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage($"@detachallthis:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.DetachAsync(
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
        public async Task DetachThisAllForce_ByRlvAttachmentPoint()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expected detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest) <-- Expected detach

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0004-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachallthis:chest=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.DetachAsync(
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
        public async Task DetachAllThisForce_ByRlvWearableType()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn pants) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expected detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (worn pants) <-- Expected detach

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachallthis:pants=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.DetachAsync(
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
        public async Task DetachAllThisForce_ByRlvWearableType_PrivateFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn pants) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- .hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (worn pants) <-- Expected detach

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = ".hats";

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Everything under the clothing and accessories folder will be detached, recursive. Hats will be excluded because they are in a private folder ".hats"
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachallthis:pants=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.DetachAsync(
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
