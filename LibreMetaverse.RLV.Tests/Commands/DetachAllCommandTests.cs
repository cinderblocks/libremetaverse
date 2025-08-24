using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class DetachAllCommandTests : RestrictionsBase
    {
        #region @detachall:<folder1/.../folderN>=force
        [Fact]
        public async Task DetachAllForce_Recursive()
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
            //        \= Glasses (worn pants)

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

            // Everything under the clothing folder, and all of its subfolders will be removed
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachall:Clothing=force", _sender.Id, _sender.Name);

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
        public async Task DetachAllForce_Recursive_EnforceRestrictions()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [LOCKED: Detach=n]
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn pants)
            //  |    |= Retro Pants (attached pelvis)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expected detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (worn pants)

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
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };
            await _rlv.ProcessMessage("@detachthis:Clothing=n", _sender.Id, _sender.Name);

            // Act - Nothing from 'Clothing' is detached because it's locked, but item in the 'Hats' folder
            //       inside of Clothing will be detached.
            await _rlv.ProcessMessage("@detachall:Clothing=force", _sender.Id, _sender.Name);

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
        public async Task DetachAllForce_Recursive_PrivateTargetDir()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .clothing
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
            //        \= Glasses (worn pants)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".clothing";

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

            // Everything under the clothing folder, and all of its subfolders will be removed
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachall:.clothing=force", _sender.Id, _sender.Name);

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
        public async Task DetachAllForce_Recursive_PrivateSubFolders()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .clothing
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
            //        \= Glasses (worn pants)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".clothing";
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

            // Everything under the .clothing folder, and all of its non-private subfolders will be removed, except the private .hats folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachall:.clothing=force", _sender.Id, _sender.Name);

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
        public async Task DetachAllForce_Recursive_NostripSubFolders()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn pants) <-- Expected detach
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- nostrip hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- No detach due to 'nostrip' in folder name
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (worn pants)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".clothing";
            sampleTree.Clothing_Hats_Folder.Name = "nostrip hats";

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

            // Everything under the .clothing folder, and all of its non-private subfolders will be removed, except the private .hats folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachall:.clothing=force", _sender.Id, _sender.Name);

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
        public async Task DetachAllForce_Recursive_NostripItemName()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (worn pants) <-- No detach due to 'nostrip' in the name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (worn pants)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;


            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Pants;

            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Everything under the .clothing folder, and all of its non-private subfolders will be removed, except the private .hats folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachall:Clothing=force", _sender.Id, _sender.Name);

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
