using Moq;

namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class DetachThisExceptExceptionTests : RestrictionsBase
    {
        #region @detachthis_except:<folder>=<rem/add>

        [Fact]
        public async Task DetachAllThis_Recursive_Except()
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.True(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachthis_except:Clothing/Hats=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine));

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin));

            // #RLV/Clothing/Business Pants ()  - Locked, but folder has exception
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis));

            // #RLV/Clothing/Happy Shirt () - Locked, but folder has exception
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt));

            // #RLV/Clothing/Retro Pants () - Locked, but folder has exception
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants));

            // #RLV/Accessories/Glasses ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses));

            // #RLV/Accessories/Watch ()
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

            Assert.Empty(clothingFolderLocked.AttachExceptions);
            Assert.Empty(clothingFolderLocked.AttachRestrictions);
            Assert.Empty(clothingFolderLocked.DetachExceptions);
            Assert.Single(clothingFolderLocked.DetachRestrictions);

            Assert.Empty(hatsFolderLocked.AttachExceptions);
            Assert.Empty(hatsFolderLocked.AttachRestrictions);
            Assert.Single(hatsFolderLocked.DetachExceptions);
            Assert.Single(hatsFolderLocked.DetachRestrictions);

            Assert.Empty(subhatsFolderLocked.AttachExceptions);
            Assert.Empty(subhatsFolderLocked.AttachRestrictions);
            Assert.Empty(subhatsFolderLocked.DetachExceptions);
            Assert.Single(subhatsFolderLocked.DetachRestrictions);
        }

        [Fact]
        public async Task DetachAllThis_Recursive_Except_AddRem()
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.True(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachthis_except:Clothing/Hats=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));
            Assert.True(await _rlv.ProcessMessage($"@detachthis_except:Clothing/Hats=rem", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name));

            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt));
            Assert.False(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants));
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses));
            Assert.True(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch));

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.Equal(3, lockedFolders.Count);

            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked));
            Assert.True(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked));

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
