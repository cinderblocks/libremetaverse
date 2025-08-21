using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class RemAttachCommandTests : RestrictionsBase
    {
        #region @detach @remattach[:<folder|attachpt|uuid>]=force
        [Theory]
        [InlineData("@detach=force")]
        [InlineData("@remattach=force")]
        public async Task RemAttach_RemoveAllAttachments(string command)
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
        public async Task RemAttach_RemoveAllAttachments_ExternalItems(string command)
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
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo);
            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Jaw Thing",
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Jaw,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
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
        [InlineData("@detach:chest=force")]
        [InlineData("@remattach:chest=force")]
        public async Task RemAttach_RemoveRlvAttachmentPoint(string command)
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
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
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
        [InlineData("@detach:skull=force")]
        [InlineData("@remattach:skull=force")]
        public async Task RemAttach_RemoveNone(string command)
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
        public async Task RemAttach_RemoveByUUID(string command)
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
        public async Task RemAttach_RemoveByUUID_IgnoreRestrictions(string command)
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
        public async Task RemAttach_RemoveByUUID_External(string command)
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
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Chest,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
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
