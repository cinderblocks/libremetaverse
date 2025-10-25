﻿using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class DetachThisCommandTests : RestrictionsBase
    {
        #region @detachthis[:<attachpt> or <clothing_layer> or <uuid> or <path>]=force
        [Fact]
        public async Task DetachThisForce_Default()
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
            //  |        |= Fancy Hat (attached chin)
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

            // Everything under the clothing folder will be detached because happy shirt exists in the clothing folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

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
        public async Task DetachThisForce_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt [SENDER] (attached chest) <-- no detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
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

            // Everything under the clothing folder will be detached because happy shirt exists in the clothing folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

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
        public async Task DetachThisForce_NoStripFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt [SENDER] (attached chest)  <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (attached pelvis)  <-- No detach due to 'nostrip' in folder name
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

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

            // Everything under the clothing folder will be detached because happy shirt exists in the clothing folder
            var expected = new HashSet<Guid>()
            {
            };

            // Act
            await _rlv.ProcessMessage("@detachthis=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

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
        public async Task DetachThisForce_NoStripFolder_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt [SENDER] (attached chest)  <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (attached pelvis)  <-- Detached because it's an item link and ignores the folder name
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_RetroPants.IsLink = true;

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

            // Everything under the clothing folder will be detached because happy shirt exists in the clothing folder
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id
            };

            // Act
            await _rlv.ProcessMessage("@detachthis=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

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
        public async Task DetachThisForce_ById()
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
            //  |        |= Fancy Hat (attached chin)
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
            };

            // Act
            await _rlv.ProcessMessage($"@detachthis:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ById_NostripItemName()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) [TARGET] <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
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
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage($"@detachthis:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ById_NostripFolderName()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) [TARGET] <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (attached pelvis) <-- No detach due to 'nostrip' in folder name
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

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
            };

            // Act
            await _rlv.ProcessMessage($"@detachthis:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ById_NostripFolderName_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) [TARGET] <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (attached pelvis) <-- Detached because it's an item link and folder name is ignored
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached chest)

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_RetroPants.IsLink = true;

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
                sampleTree.Root_Clothing_RetroPants.Id
            };

            // Act
            await _rlv.ProcessMessage($"@detachthis:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvAttachmentPoint()
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
            //  |        |= Fancy Hat (attached chin)
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

            // Everything under the clothing and accessories folder will be detached, not recursive
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:chest=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvAttachmentPoint_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \- nostrip Accessories
            //        |= Watch
            //        \= Glasses (attached chest) <-- No detach due to 'nostrip' in folder name

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Accessories_Folder.Name = "nostrip Accessories";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
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

            // Everything under the clothing and accessories folder will be detached, not recursive
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:chest=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvAttachmentPoint_NoStrip_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \- nostrip Accessories
            //        |= Watch
            //        \= Glasses (attached chest) <-- Detached because it's an item link and folder name is ignored

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Accessories_Folder.Name = "nostrip Accessories";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0004-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Accessories_Glasses.IsLink = true;

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
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:chest=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvWearableType()
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
            //  |        |= Fancy Hat (attached chin)
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

            // Everything under the clothing and accessories folder will be detached, not recursive
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:pants=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvWearableType_NoStrip()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (worn pants) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \- nostrip Accessories
            //        |= Watch
            //        \= Glasses (worn pants) <-- No detach due to 'nostrip' in folder name

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Accessories_Folder.Name = "nostrip Accessories";

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Pants;

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Everything under the clothing and accessories folder will be detached, not recursive
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:pants=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvWearableType_NoStrip_LinkException()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (worn pants) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached pelvis) <-- Expected detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \- nostrip Accessories
            //        |= Watch
            //        \= Glasses (worn pants) <-- detach because it's an item link. folder name ignored

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Accessories_Folder.Name = "nostrip Accessories";

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Glasses.IsLink = true;

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
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
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:pants=force", _sender.Id, _sender.Name);

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
        public async Task DetachThisForce_ByRlvWearableType_PrivateFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- .clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (worn pants)
            //  |    |= Retro Pants (attached pelvis)
            //  |    \- Hats
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

            // Only accessories will be removed even though pants exist in our clothing folder. The clothing folder is private ".clothing"
            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Accessories_Glasses.Id,
            };

            // Act
            await _rlv.ProcessMessage("@detachthis:pants=force", _sender.Id, _sender.Name);

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
