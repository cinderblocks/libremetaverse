using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class AttachThisCommandTests : RestrictionsBase
    {
        #region @attachthisoverorreplace @attachthisover @attachthis[:<attachpt> or <clothing_layer> or <uuid>]=force
        [Theory]
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_Default(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Attached to chin)
            //  |        \= Party Hat <-- Expect attach to spine
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything in #RLV/Clothing/Hats because that's where the source item (fancy hat) is calling @attachthis from
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name);

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
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_ById(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expect attach
            //  |    |= Happy Shirt (Attached to chest)
            //  |    |= Retro Pants  <-- Expect attach
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
        [InlineData("attachthis")]
        [InlineData("attachthisoverorreplace")]
        [InlineData("attachthisover")]
        public async Task AttachThis_FolderNameSpecifiesToAddInsteadOfReplace(string command)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- +clothing
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt <-- Expect 'add-to' default
            //  |    |= Retro Pants <-- Expect request 'add-to' default
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

            sampleTree.Clothing_Folder.Name = "+clothing";
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything in #RLV/+clothing because that's where the source item (business pants) is calling @attachthis from, but use 'add-to' logic instead of 'replace' logic
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_HappyShirt.Id, RlvAttachmentPoint.Default, false),
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, false),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name);

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
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_FolderNameSpecifiesRlvAttachmentPoint(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- (skull) hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Attached to chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name = "Party Hat";
            sampleTree.Clothing_Hats_Folder.Name = "(skull) hats";

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything in #RLV/Clothing/+Hats because that's where the source item (fancy hat) is calling @attachthis from,+
            // but attach "party hat" to the skull because it doesn't specify an attachment point but the folder name does
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Skull, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name);

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
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_FromHiddenSubfolder(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- .hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Attached to chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = ".hats";

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name = "Fancy Hat (chin)";
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments)
            };

            // Act
            await _rlv.ProcessMessage($"@{command}=force", sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name);

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
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_AttachPoint(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected request to attach
            //  |    |= Happy Shirt (attached to 'spine')
            //  |    |= Retro Pants <-- Expected request to attach
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Expected request to attach
            //  |        \= Party Hat (attached to 'spine')
            //   \-Accessories
            //        |= Watch
            //        \= Glasses

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.Name = "Happy Shirt (spine)";
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name = "Party Hat (spine)";
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

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
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:spine=force", _sender.Id, _sender.Name);

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
        [InlineData("attachthis", true)]
        [InlineData("attachthisoverorreplace", true)]
        [InlineData("attachthisover", false)]
        public async Task AttachThis_RlvWearableType(string command, bool replaceExistingAttachments)
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants <-- Expected to be attached
            //  |    |= Happy Shirt <-- Expected to be attached
            //  |    |= Retro Pants (Worn as Tattoo)
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses <-- Expected to be attached

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Tattoo;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Accessories_Glasses.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_HappyShirt.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
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
