using Moq;

namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class AttachThisRestrictionTests : RestrictionsBase
    {
        #region @attachthis[:<layer>|<attachpt>|<path_to_folder>]=<y/n>
        [Fact]
        public async Task AttachThis()
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
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat (Attached to spine)
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name));

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
            Assert.Single(lockedFolders);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachThis_NotRecursive()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [LOCKED, no-attach]
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt <-- No Attach
            //  |    |= Retro Pants <-- No Attach
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

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            // This should lock the #RLV/Clothing folder because the Business Pants are issuing the command, which is in the Clothing folder.
            //   Business Pants cannot be attached, but hats are still attachable.
            Assert.True(await _rlv.ProcessMessage("@attachthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

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
            Assert.Single(lockedFolders);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachThis_ByPath()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [LOCKED - NO-ATTACH]
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- Cannot attach
            //  |        \= Party Hat <-- Cannot attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@attachthis:Clothing/Hats=n", _sender.Id, _sender.Name));

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
            Assert.Single(lockedFolders);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachThis_ByRlvAttachmentPoint()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [LOCKED, no-attach]
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt <-- No attach
            //  |    |= Retro Pants <-- No attach
            //  |    \- Hats Clothing [LOCKED, no-attach]
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat  (Attached to pelvis)
            //  |        \= Party Hat <-- No attach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.True(await _rlv.ProcessMessage("@attachthis:pelvis=n", _sender.Id, _sender.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - folder was locked because PartyHat (groin)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED) - folder was locked because BusinessPants (groin)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(2, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Single(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Empty(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Single(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Empty(clothingFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachThis_ByWornLayer()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats Clothing 
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories [LOCKED, no-attach]
            //        |= Watch (Worn as tattoo)
            //        \= Glasses
            //

            // TryGetRlvInventoryTree
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.True(await _rlv.ProcessMessage("@attachthis:tattoo=n", _sender.Id, _sender.Name));

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants ()
            Assert.True(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses (LOCKED) - folder was locked from Watch (tattoo)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch (LOCKED)
            Assert.False(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Single(lockedFolders);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Accessories_Folder.Id, out var accessoriesFolderLocked));

            Assert.Equal(sampleTree.Accessories_Folder.Name, accessoriesFolderLocked.Name);
            Assert.Equal(sampleTree.Accessories_Folder.Id, accessoriesFolderLocked.Id);
            Assert.True(accessoriesFolderLocked.CanDetach);
            Assert.False(accessoriesFolderLocked.CanAttach);
            Assert.True(accessoriesFolderLocked.IsLocked);

            Assert.Empty(accessoriesFolderLocked.AttachExceptions);
            Assert.Single(accessoriesFolderLocked.AttachRestrictions);
            Assert.Empty(accessoriesFolderLocked.DetachExceptions);
            Assert.Empty(accessoriesFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task AttachThis_ByWornLayer_AddRem()
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
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as tattoo)
            //        \= Glasses
            //

            // TryGetRlvInventoryTree
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.True(await _rlv.ProcessMessage("@attachthis:tattoo=n", _sender.Id, _sender.Name));
            Assert.True(await _rlv.ProcessMessage("@attachthis:tattoo=y", _sender.Id, _sender.Name));

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
