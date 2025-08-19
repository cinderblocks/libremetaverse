using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class AttachCommandTests : RestrictionsBase
    {
        #region @attachover @attachoverorreplace @attach:<folder1/.../folderN>=force
        [Theory]
        [InlineData("attach", true)]
        [InlineData("attachoverorreplace", true)]
        [InlineData("attachover", false)]
        public async Task AttachForce(string command, bool replaceExistingAttachments)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything in the Clothing/Hats folder
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:Clothing/Hats=force", _sender.Id, _sender.Name);

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
        [InlineData("attach", true)]
        [InlineData("attachoverorreplace", true)]
        [InlineData("attachover", false)]
        public async Task AttachForce_WithClothing(string command, bool replaceExistingAttachments)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything in the Clothing folder. Make sure clothing types (RlvWearableType) are also included
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_BusinessPants_Pelvis.Id, RlvAttachmentPoint.Pelvis, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_HappyShirt.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_RetroPants.Id, RlvAttachmentPoint.Default, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:Clothing=force", _sender.Id, _sender.Name);

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
        [InlineData("attach")]
        [InlineData("attachoverorreplace")]
        [InlineData("attachover")]
        public async Task AttachForce_AlreadyAttached(string command)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach nothing because everything in this folder is already attached
            var expected = new HashSet<AttachmentRequest>()
            {
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:Clothing=force", _sender.Id, _sender.Name);

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
        [InlineData("attach", true)]
        [InlineData("attachoverorreplace", true)]
        [InlineData("attachover", false)]
        public async Task AttachForce_PositionFromFolderName(string command, bool replaceExistingAttachments)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "Hats (spine)";

            // Item name overrides folder name
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.Name = "Fancy Hat (skull)";

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything under the "Clothing/Hats (spine)" folder, attaching everything to the Spine point unless the item explicitly specifies a different attachment point such as "Fancy Hat (skull)".
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Skull, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}=force", _sender.Id, _sender.Name);

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
        [InlineData("attach")]
        [InlineData("attachoverorreplace")]
        [InlineData("attachover")]
        public async Task AttachForce_FolderNameSpecifiesToAddInsteadOfReplace(string command)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Hats_Folder.Name = "+Hats";

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // Attach everything inside of the Clothing/Hats folder, but force 'add to' logic due to the + prefix on the hats folder
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, false),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, false),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}=force", _sender.Id, _sender.Name);

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
        [InlineData("attach", true)]
        [InlineData("attachoverorreplace", true)]
        [InlineData("attachover", false)]
        public async Task AttachForce_AttachPrivateParentFolder(string command, bool replaceExistingAttachments)
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Clothing_Folder.Name = ".clothing";

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            _actionCallbacks.Setup(e =>
                e.AttachAsync(It.IsAny<IReadOnlyList<AttachmentRequest>>(), It.IsAny<CancellationToken>())
            ).Returns(Task.CompletedTask);

            // This is allowed even though we're targeting a private folder. Only private subfolders are ignored
            var expected = new HashSet<AttachmentRequest>()
            {
                new(sampleTree.Root_Clothing_Hats_FancyHat_Chin.Id, RlvAttachmentPoint.Chin, replaceExistingAttachments),
                new(sampleTree.Root_Clothing_Hats_PartyHat_Spine.Id, RlvAttachmentPoint.Spine, replaceExistingAttachments),
            };

            // Act
            await _rlv.ProcessMessage($"@{command}:{sampleTree.Clothing_Folder.Name}/{sampleTree.Clothing_Hats_Folder.Name}=force", _sender.Id, _sender.Name);

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
