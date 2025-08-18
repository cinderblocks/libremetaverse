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

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

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
        public async Task DetachAllForce_Recursive_IgnoreRestrictions()
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

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Everything under the clothing folder, and all of its subfolders will be removed even though the clothing folder is restricted from
            //  being detached - commands bypass these restrictions
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };
            await _rlv.ProcessMessage("@detachthis:Clothing=n", _sender.Id, _sender.Name);

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

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

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

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

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
        #endregion

    }
}
