using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class DetachMeCommandTests : RestrictionsBase
    {
        #region @detachme=force
        [Fact]
        public async Task DetachMeForce()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached spine)
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
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachme=force", sampleTree.Root_Clothing_RetroPants.AttachedPrimId!.Value, sampleTree.Root_Clothing_RetroPants.Name);

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
        public async Task DetachMeForce_WithNoStripItemName_SpecialException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached spine)
            //  |    |= nostrip Retro Pants (attached pelvis)  <-- detached, @detachme ignores nostrip tags
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";
            sampleTree.Root_Clothing_RetroPants.Name = "nostrip Retro Pants";

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachme=force", sampleTree.Root_Clothing_RetroPants.AttachedPrimId!.Value, sampleTree.Root_Clothing_RetroPants.Name);

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
        public async Task DetachMeForce_WithLockedFolderRestriction()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached spine)
            //  |    |= Retro Pants (attached pelvis)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Retro Pants (attached pelvis) <-- Simulated link to 'Retro Pants' object
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            // Add a copy of the same RetroPants into the Hats folder - this will simulate a linked item existing in a locked folder
            sampleTree.Clothing_Hats_Folder.AddItem(
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_RetroPants.Name,
                sampleTree.Root_Clothing_RetroPants.IsLink,
                sampleTree.Root_Clothing_RetroPants.AttachedTo,
                sampleTree.Root_Clothing_RetroPants.AttachedPrimId,
                sampleTree.Root_Clothing_RetroPants.WornOn,
                sampleTree.Root_Clothing_RetroPants.GestureState
            );

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
            };

            // Lock the 'Hats' folder, which contains an exact copy of the item we're going to detach. This should prevent the detaching of this item from the non locked folder
            await _rlv.ProcessMessage($"@detachthis:{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}=n", _sender.Id, _sender.Name);

            // Act
            await _rlv.ProcessMessage("@detachme=force", sampleTree.Root_Clothing_RetroPants.AttachedPrimId!.Value, sampleTree.Root_Clothing_RetroPants.Name);

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
        public async Task DetachMeForce_MultipleLinksSameItem()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached spine)
            //  |    |= Retro Pants (attached pelvis) <--- Detached, but only one result since they have the same ID
            //  |    |= Retro Pants (attached pelvis) <--/ 
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Retro Pants (attached pelvis) <-- Simulated link to 'Retro Pants' object
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            // Add a copy of the same RetroPants into the Hats folder
            sampleTree.Clothing_Hats_Folder.AddItem(
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_RetroPants.Name,
                sampleTree.Root_Clothing_RetroPants.IsLink,
                sampleTree.Root_Clothing_RetroPants.AttachedTo,
                sampleTree.Root_Clothing_RetroPants.AttachedPrimId,
                sampleTree.Root_Clothing_RetroPants.WornOn,
                sampleTree.Root_Clothing_RetroPants.GestureState
            );

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachme=force", sampleTree.Root_Clothing_RetroPants.AttachedPrimId!.Value, sampleTree.Root_Clothing_RetroPants.Name);

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
