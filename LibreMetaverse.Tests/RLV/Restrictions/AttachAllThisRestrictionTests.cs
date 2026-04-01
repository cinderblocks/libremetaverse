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
    public class AttachAllThisRestrictionTests : RlvTestBase
    {
        #region @attachallthis[:<layer>|<attachpt>|<path_to_folder>]=<y/n>

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.True);

            // #RLV/Clothing/Happy Shirt ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.True);

            // #RLV/Clothing/Retro Pants ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.True);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(2));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis:Clothing=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis:pants=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED) - Folder locked due to RetroPants being worn as 'pants'
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis:chest=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - Parent folder locked recursively
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED) - Folder locked due to HappyShirt attachment of 'chest'
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(3));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_SubHats_Folder.Id, out var subhatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Folder.Name));
            Assert.That(clothingFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Folder.Id));
            Assert.That(clothingFolderLocked.CanDetach, Is.True);
            Assert.That(clothingFolderLocked.CanAttach, Is.False);
            Assert.That(clothingFolderLocked.IsLocked, Is.True);

            Assert.That(hatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Name));
            Assert.That(hatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Id));
            Assert.That(hatsFolderLocked.CanDetach, Is.True);
            Assert.That(hatsFolderLocked.CanAttach, Is.False);
            Assert.That(hatsFolderLocked.IsLocked, Is.True);

            Assert.That(subhatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Name));
            Assert.That(subhatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Id));
            Assert.That(subhatsFolderLocked.CanDetach, Is.True);
            Assert.That(subhatsFolderLocked.CanAttach, Is.False);
            Assert.That(subhatsFolderLocked.IsLocked, Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachallthis:chest=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage("@attachallthis:chest=y", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders, Is.Empty);
        }

        #endregion
    }
}
