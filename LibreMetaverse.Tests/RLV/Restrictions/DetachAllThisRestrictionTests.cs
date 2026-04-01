using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class DetachAllThisRestrictionTests : RlvTestBase
    {
        #region @detachallthis[:<layer>|<attachpt>|<path_to_folder>]=<y/n>

        [Test]
        public async Task DetachAllThis()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \- Hats <-- Expected locked, no-detach
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat (Attached to spine) <-- No detach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.True);

            // #RLV/Clothing/Happy Shirt ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.True);

            // #RLV/Clothing/Retro Pants ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.True);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(2));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachAllThis_Recursive()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants (Attached pelvis) <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach
            //  |    \- Hats <-- Expected locked, no-detach
            //  |        |
            //  |        |- Sub Hats <-- Expected locked, no-detach
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat <-- No detach
            //   \-Accessories
            //        |= Watch
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

            Assert.That(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachAllThis_Recursive_Path()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach
            //  |    \- Hats <-- Expected locked, no-detach
            //  |        |
            //  |        |- Sub Hats <-- Expected locked, no-detach
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat <-- No detach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis:Clothing=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachAllThis_Recursive_Worn()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants (Worn pants) <-- No detach 
            //  |    \- Hats <-- Expected locked, no-detach
            //  |        |
            //  |        |- Sub Hats <-- Expected locked, no-detach
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat <-- No detach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis:pants=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachAllThis_Recursive_Attached()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants (Attached chest) <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach 
            //  |    \- Hats <-- Expected locked, no-detach
            //  |        |
            //  |        |- Sub Hats <-- Expected locked, no-detach
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat <-- No detach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Chest;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis:chest=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Folder.Name));
            Assert.That(clothingFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Folder.Id));
            Assert.That(clothingFolderLocked.CanDetach, Is.False);
            Assert.That(clothingFolderLocked.CanAttach, Is.True);
            Assert.That(clothingFolderLocked.IsLocked, Is.True);

            Assert.That(hatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Name));
            Assert.That(hatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Id));
            Assert.That(hatsFolderLocked.CanDetach, Is.False);
            Assert.That(hatsFolderLocked.CanAttach, Is.True);
            Assert.That(hatsFolderLocked.IsLocked, Is.True);

            Assert.That(subhatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Name));
            Assert.That(subhatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Id));
            Assert.That(subhatsFolderLocked.CanDetach, Is.False);
            Assert.That(subhatsFolderLocked.CanAttach, Is.True);
            Assert.That(subhatsFolderLocked.IsLocked, Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachAllThis_Recursive_Attached_AddRem()
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis:chest=n", _sender.Id, _sender.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage("@detachallthis:chest=y", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders, Is.Empty);
        }

        #endregion

    }
}
