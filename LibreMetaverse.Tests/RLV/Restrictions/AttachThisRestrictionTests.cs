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
    public class AttachThisRestrictionTests : RlvTestBase
    {
        #region @attachthis[:<layer>|<attachpt>|<path_to_folder>]=<y/n>
        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the #RLV/Clothing folder because the Business Pants are issuing the command, which is in the Clothing folder.
            //   Business Pants cannot be attached, but hats are still attachable.
            Assert.That(await _rlv.ProcessMessage("@attachthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@attachthis:Clothing/Hats=n", _sender.Id, _sender.Name), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.That(await _rlv.ProcessMessage("@attachthis:pelvis=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - folder was locked because PartyHat (groin)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED) - folder was locked because BusinessPants (groin)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(2));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Is.Empty);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.That(await _rlv.ProcessMessage("@attachthis:tattoo=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_PartyHat_Spine, true), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.True);

            // #RLV/Clothing/Business Pants ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_BusinessPants_Pelvis, true), Is.True);

            // #RLV/Clothing/Happy Shirt ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.True);

            // #RLV/Clothing/Retro Pants ()
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_RetroPants, true), Is.True);

            // #RLV/Accessories/Glasses (LOCKED) - folder was locked from Watch (tattoo)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Glasses, true), Is.False);

            // #RLV/Accessories/Watch (LOCKED)
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Accessories_Watch, true), Is.False);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Accessories_Folder.Id, out var accessoriesFolderLocked), Is.True);

            Assert.That(accessoriesFolderLocked.Name, Is.EqualTo(sampleTree.Accessories_Folder.Name));
            Assert.That(accessoriesFolderLocked.Id, Is.EqualTo(sampleTree.Accessories_Folder.Id));
            Assert.That(accessoriesFolderLocked.CanDetach, Is.True);
            Assert.That(accessoriesFolderLocked.CanAttach, Is.False);
            Assert.That(accessoriesFolderLocked.IsLocked, Is.True);

            Assert.That(accessoriesFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(accessoriesFolderLocked.AttachRestrictions, Has.Count.EqualTo(1));
            Assert.That(accessoriesFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(accessoriesFolderLocked.DetachRestrictions, Is.Empty);
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer attachable
            Assert.That(await _rlv.ProcessMessage("@attachthis:tattoo=n", _sender.Id, _sender.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage("@attachthis:tattoo=y", _sender.Id, _sender.Name), Is.True);

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
