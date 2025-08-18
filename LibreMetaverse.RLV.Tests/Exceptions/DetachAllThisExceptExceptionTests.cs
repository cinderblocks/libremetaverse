using Moq;

namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class DetachAllThisExceptExceptionTests : RestrictionsBase
    {
        #region @detachallthis_except:<folder>=<rem/add>

        [Fact]
        public async Task DetachAllThis_Recursive_ExceptAll()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Expected locked]
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Expected locked, but has exceptions]
            //  |        |
            //  |        |- Sub Hats [Expected locked, but has exceptions]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachallthis_except:Clothing/Hats=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat () - Parent folder locked recursively, but has exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat () - Parent folder locked recursively, but has exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch, true));
        }

        [Fact]
        public async Task DetachAllThis_Recursive_ExceptAll_Recursive()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Expected locked, but has exceptions]
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Expected locked, but has exceptions]
            //  |        |
            //  |        |- Sub Hats [Expected locked, but has exceptions]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat () - Parent folder locked recursively, but parent has recursive exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));

            // #RLV/Clothing/Hats/Fancy Hat () - Parent folder locked recursively, but parent has recursive exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));

            // #RLV/Clothing/Business Pants ()  - Locked, but folder has exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));

            // #RLV/Clothing/Happy Shirt () - Locked, but folder has exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt, true));

            // #RLV/Clothing/Retro Pants () - Locked, but folder has exception
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants, true));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses, true));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Equal(sampleTree.Clothing_Folder.Name, clothingFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Folder.Id, clothingFolderLocked.Id);
            Assert.True(clothingFolderLocked.CanDetach);
            Assert.True(clothingFolderLocked.CanAttach);
            Assert.True(clothingFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_Folder.Name, hatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_Folder.Id, hatsFolderLocked.Id);
            Assert.True(hatsFolderLocked.CanDetach);
            Assert.True(hatsFolderLocked.CanAttach);
            Assert.True(hatsFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Name, subhatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Id, subhatsFolderLocked.Id);
            Assert.True(subhatsFolderLocked.CanDetach);
            Assert.True(subhatsFolderLocked.CanAttach);
            Assert.True(subhatsFolderLocked.IsLocked);

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Empty(clothingFolderLocked.AttachRestrictions);
            Assert.Single(clothingFolderLocked.DetachExceptions);
            Assert.Single(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Empty(hatsFolderLocked.AttachRestrictions);
            Assert.Single(hatsFolderLocked.DetachExceptions);
            Assert.Single(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Empty(subhatsFolderLocked.AttachRestrictions);
            Assert.Single(subhatsFolderLocked.DetachExceptions);
            Assert.Single(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task DetachAllThis_Recursive_ExceptAll_Recursive_AddRem()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing [Expected locked]
            //  |    |= Business Pants (Attached to pelvis)
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats [Expected locked]
            //  |        |
            //  |        |- Sub Hats [Expected locked]
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            _queryCallbacks.Setup(e =>
                e.TryGetSharedFolderAsync(default)
            ).ReturnsAsync((true, sharedFolder));

            Assert.True(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=rem", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt, true));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants, true));
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses, true));
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch, true));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Equal(sampleTree.Clothing_Folder.Name, clothingFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Folder.Id, clothingFolderLocked.Id);
            Assert.False(clothingFolderLocked.CanDetach);
            Assert.True(clothingFolderLocked.CanAttach);
            Assert.True(clothingFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_Folder.Name, hatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_Folder.Id, hatsFolderLocked.Id);
            Assert.False(hatsFolderLocked.CanDetach);
            Assert.True(hatsFolderLocked.CanAttach);
            Assert.True(hatsFolderLocked.IsLocked);

            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Name, subhatsFolderLocked.Name);
            Assert.Equal(sampleTree.Clothing_Hats_SubHats_Folder.Id, subhatsFolderLocked.Id);
            Assert.False(subhatsFolderLocked.CanDetach);
            Assert.True(subhatsFolderLocked.CanAttach);
            Assert.True(subhatsFolderLocked.IsLocked);

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Empty(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Single(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Empty(hatsFolderLocked.AttachRestrictions);
            Assert.Empty(hatsFolderLocked.DetachExceptions);
            Assert.Single(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Empty(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Single(subhatsFolderLocked.DetachRestrictions);
        }

        #endregion
    }
}
