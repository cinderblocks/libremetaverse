using Moq;

namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class AttachAllThisRestrictionTests : RestrictionsBase
    {
        #region @attachallthis[:<layer>|<attachpt>|<path_to_folder>]=<y/n>

        [Fact]
        public async Task AttachAllThis()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Locked]
            //  |        |
            //  |        |- Sub Hats [Locked]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat (attached chin)
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(2, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Single(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Empty(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachAllThis_Recursive()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [LOCKED]
            //  |    |= Business Pants (attached chin)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [LOCKED]
            //  |        |
            //  |        |- Sub Hats [LOCKED]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Single(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Empty(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachAllThis_Recursive_Path()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Locked]
            //  |    |= Business Pants (attached chin)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Locked]
            //  |        |
            //  |        |- Sub Hats [Locked]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis:Clothing=n", _sender.Id, _sender.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Single(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Empty(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachAllThis_Recursive_Worn()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Locked]
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants (Worn as pants)
            //  |    \- Hats [Locked]
            //  |        |
            //  |        |- Sub Hats [Locked]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis:pants=n", _sender.Id, _sender.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Single(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Empty(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachAllThis_Recursive_Attached()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Locked]
            //  |    |= Business Pants (Attached to chest)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Locked]
            //  |        |
            //  |        |- Sub Hats [Locked]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis:chest=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Equal(sampleTree.Clothing_Folder.Name, clothingFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Folder.Id, clothingFolderLocked.Id);
            Assert.True(clothingFolderLocked.CanDetach);
            Assert.False(clothingFolderLocked.CanAttach);
            Assert.True(clothingFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_Folder.Name, hatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_Folder.Id, hatsFolderLocked.Id);
            Assert.True(hatsFolderLocked.CanDetach);
            Assert.False(hatsFolderLocked.CanAttach);
            Assert.True(hatsFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Name, subhatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Id, subhatsFolderLocked.Id);
            Assert.True(subhatsFolderLocked.CanDetach);
            Assert.False(subhatsFolderLocked.CanAttach);
            Assert.True(subhatsFolderLocked.IsLocked);

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Single(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Empty(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachAllThis_Recursive_Attached_AddRem()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants (Attached to chest)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
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
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachallthis:chest=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage("@attachallthis:chest=y", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Empty(lockedFolders);
        }

        #endregion
    }
}
