using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class AttachAllThisCommandTests : RestrictionsBase
    {
        #region @attachallthisover @attachallthisoverorreplace @attachallthis[:<attachpt> or <clothing_layer>]=force
        [Theory]
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_Recursive(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt (SENDER, Worn on chest)
            //  |    |= Retro Pants <-- Expected attach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected attach
            //  |        \= Party Hat <-- Expected attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_Recursive_ById(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt (SENDER, Worn on chest)
            //  |    |= Retro Pants <-- Expected attach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected attach
            //  |        \= Party Hat <-- Expected attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Root_Clothing_HappyShirt.AttachedPrimId}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_Recursive_WithHiddenSubfolder(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt (SENDER, Worn on chest)
            //  |    |= Retro Pants <-- Expected attach
            //  |    \- .hats
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

            sampleTree.Clothing_Hats_Folder.Name = ".hats";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_Recursive_FolderNameSpecifiesToAddInsteadOfReplace(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt (SENDER, Worn on chest)
            //  |    |= Retro Pants <-- Expected attach
            //  |    \- +hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected add-to attach
            //  |        \= Party Hat <-- Expected add-to attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "+hats";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, false),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, false),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_HappyShirt.AttachedPrimId!.Value, sampleTree.Root_Clothing_HappyShirt.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_AttachPoint(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt (SENDER, attached to spine)
            //  |    |= Retro Pants <-- Expected attach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected attach
            //  |        \= Party Hat <-- Expected attach
            //   \-Accessories
            //        |= Watch (Attached to spine)
            //        \= Glasses <-- Expected attach


            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.Name = "Happy Shirt (skull)";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Skull;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Watch.Name = "Watch (skull)";
            sampleTree.Root_Accessories_Watch.AttachedTo = RlvAttachmentPoint.Skull;
            sampleTree.Root_Accessories_Watch.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
                new(sampleTree.Root_Accessories_Glasses.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:skull=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
        [InlineData("attachallthis", true)]
        [InlineData("attachallthisoverorreplace", true)]
        [InlineData("attachallthisover", false)]
        public async Task AttachAllThisForce_RlvWearableType(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected attach
            //  |    |= Happy Shirt <-- Expected attach
            //  |    |= Retro Pants (Worn as Tattoo)
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected attach
            //  |        \= Party Hat <-- Expected attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Tattoo;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_HappyShirt.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:tattoo=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AttachAsync(
                    It.Is<IReadOnlyList<AttachmentRequest>>(ids =>
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
