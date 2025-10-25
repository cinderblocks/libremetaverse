﻿using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class RemAttachCommandTests : RestrictionsBase
    {
        #region @detach @remattach[:<folder|attachpt|uuid>]=force
        [Theory]
        [InlineData("@detach=force")]
        [InlineData("@remattach=force")]
        public async Task RemAttach(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expect detach
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expect detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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
                sampleTree.Root_Clothing_HappyShirt.Id,
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach=force")]
        [InlineData("@remattach=force")]
        public async Task RemAttach_NoStrip(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached groin) <-- Detach
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- No detach due to 'nostrip' in folder name
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0005-4aaa-8aaa-ffffffffffff");

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
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach=force")]
        [InlineData("@remattach=force")]
        public async Task RemAttach_NoStrip_LinkException(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached groin) <-- Detach
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Detached because it's an item link and folder name is ignored
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.IsLink = true;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0005-4aaa-8aaa-ffffffffffff");

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
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach=force")]
        [InlineData("@remattach=force")]
        public async Task RemAttach_ExternalItems(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expect detach
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expect detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // External
            //   |= External Tattoo (worn tattoo)
            //   \= External Jaw Thing (attached jaw) <-- Expect detach
            //
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;


            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);
            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Jaw Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Jaw,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable, externalAttachable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                sampleTree.Root_Clothing_HappyShirt.Id,
                externalAttachable.Id,
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:Clothing/Hats=force")]
        [InlineData("@remattach:Clothing/Hats=force")]
        public async Task RemAttach_ByFolder(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Expect detach
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:Clothing/nostrip Hats=force")]
        [InlineData("@remattach:Clothing/nostrip Hats=force")]
        public async Task RemAttach_ByFolder_NoStrip(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest)
            //  |    |= Retro Pants (worn pants)
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- No detach due to 'nostrip' in folder name
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:Clothing/nostrip Hats=force")]
        [InlineData("@remattach:Clothing/nostrip Hats=force")]
        public async Task RemAttach_ByFolder_NoStrip_LinkException(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest)
            //  |    |= Retro Pants (worn pants)
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin) <-- Detached because it's an item link and folder name is ignored
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.IsLink = true;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:chest=force")]
        [InlineData("@remattach:chest=force")]
        public async Task RemAttach_ByAttachmentPoint(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expect detach
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // External
            //   \= External Chest Thing (attached chest) <-- Expect detach
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalAttachable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id,
                externalAttachable.Id
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:chest=force")]
        [InlineData("@remattach:chest=force")]
        public async Task RemAttach_ByAttachmentPoint_NoStrip(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached chest) <-- Expect detach
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chest) <-- No detach due to 'nostrip' in folder name
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // External
            //   \= External Chest Thing (attached chest) <-- Expect detach
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0005-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var externalAttachable2 = new RlvInventoryItem(
                new Guid("12312312-0003-4aaa-8aaa-aaaaaaaaaaaa"),
                "nostrip External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0003-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalAttachable, externalAttachable2]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
                externalAttachable.Id
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:chest=force")]
        [InlineData("@remattach:chest=force")]
        public async Task RemAttach_ByAttachmentPoint_NoStrip_LinkException(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) <-- No detach due to 'nostrip' in item name
            //  |    |= Retro Pants (attached chest) <-- Expect detach
            //  |    \- nostrip Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chest) <-- Detached because it's an item link
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // External
            //   \= External Chest Thing (attached chest) <-- Expect detach
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "nostrip Hats";

            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.IsLink = true;

            sampleTree.Root_Clothing_RetroPants.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_RetroPants.AttachedPrimId = new Guid("11111111-0005-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var externalAttachable2 = new RlvInventoryItem(
                new Guid("12312312-0003-4aaa-8aaa-aaaaaaaaaaaa"),
                "nostrip External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0003-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalAttachable, externalAttachable2]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_RetroPants.Id,
                sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id,
                externalAttachable.Id
            };

            // Act
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("@detach:skull=force")]
        [InlineData("@remattach:skull=force")]
        public async Task RemAttach_ByAttachmentPoint_None(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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
            await _rlv.ProcessMessage(command, _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- Expected detach
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID_NostripInItemName(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= nostrip Happy Shirt (attached chest) [TARGET] <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_HappyShirt.Name = "nostrip Happy Shirt";

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID_NostripInFolderName(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) [TARGET] <-- No detach due to 'nostrip' in folder name
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

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
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID_NostripInFolderName_LinkException(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- nostrip Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) [TARGET] <-- Detach because it's an item link and ignores the folder name
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");
            sampleTree.Root_Clothing_HappyShirt.IsLink = true;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            sampleTree.Clothing_Folder.Name = "nostrip Clothing";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                sampleTree.Root_Clothing_HappyShirt.Id
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID_WithRestrictions(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest) <-- No detach - folder is locked (Detach=n)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

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

            await _rlv.ProcessMessage($"@detachallthis:{sampleTree.Clothing_Folder.Name}=n", _sender.Id, _sender.Name);

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

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

        [Theory]
        [InlineData("detach")]
        [InlineData("remattach")]
        public async Task RemAttach_ByUUID_External(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (attached chest)
            //  |    |= Retro Pants (worn pants)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // External
            //   \= External Chest Thing (attached chest) <-- Expect detach
            //
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Chest Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var inventoryMap = new InventoryMap(sharedFolder, [externalAttachable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            _actionCallbacks.Setup(e =>
                e.DetachAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<Guid>()
            {
                externalAttachable.Id
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{externalAttachable.AttachedPrimId}=force", _sender.Id, _sender.Name);

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
