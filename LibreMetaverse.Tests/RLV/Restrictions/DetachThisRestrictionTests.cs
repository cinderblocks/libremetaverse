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
    public class DetachThisRestrictionTests : RlvTestBase
    {
        #region @detachthis[:<layer>|<attachpt>|<uuid>]=<y/n>

        [Test]
        public async Task DetachThis()
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

            Assert.That(await _rlv.ProcessMessage("@detachthis=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachThis_NotRecursive()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants (Attached pelvis) <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach
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

            // TryGetRlvInventoryTree
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the #RLV/Clothing folder because the Business Pants are issuing the command, which is in the Clothing folder.
            //   Business Pants cannot be detached, but hats are still detachable.
            Assert.That(await _rlv.ProcessMessage("@detachthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachThis_ByPath()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer detachable
            Assert.That(await _rlv.ProcessMessage("@detachthis:Clothing=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);

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
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachThis_ByRlvAttachmentPoint()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing <-- Expected locked, no-detach
            //  |    |= Business Pants (Attached pelvis) <-- No detach
            //  |    |= Happy Shirt <-- No detach
            //  |    |= Retro Pants <-- No detach
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat <-- No detach
            //  |        \= Party Hat (Attached pelvis) <-- No detach
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer detachable
            Assert.That(await _rlv.ProcessMessage("@detachthis:pelvis=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);

            // #RLV/Clothing/Hats/Fancy Hat (LOCKED) - folder was locked because PartyHat 
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);

            // #RLV/Clothing/Business Pants (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);

            // #RLV/Clothing/Happy Shirt (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);

            // #RLV/Clothing/Retro Pants (LOCKED) - folder was locked because BusinessPants
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);

            // #RLV/Accessories/Glasses ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);

            // #RLV/Accessories/Watch ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders.Count, Is.EqualTo(2));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Folder.Id, out var clothingFolderLocked), Is.True);
            Assert.That(lockedFolders.TryGetValue(sampleTree.Clothing_Hats_Folder.Id, out var hatsFolderLocked), Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachThis_ByWornLayer()
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
            //        |= Watch (Worn as tattoo) <-- no detach
            //        \= Glasses <-- No detach
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            // This should lock the Hats folder, all hats are no longer detachable
            Assert.That(await _rlv.ProcessMessage("@detachthis:tattoo=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);

            // #RLV/Clothing/Business Pants ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.True);

            // #RLV/Clothing/Happy Shirt ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.True);

            // #RLV/Clothing/Retro Pants ()
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.True);

            // #RLV/Accessories/Glasses (LOCKED) - folder was locked from Watch (tattoo)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.False);

            // #RLV/Accessories/Watch (LOCKED)
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.False);

            var lockedFolders = _rlv.Restrictions.GetLockedFolders();
            Assert.That(lockedFolders, Has.Count.EqualTo(1));

            Assert.That(lockedFolders.TryGetValue(sampleTree.Accessories_Folder.Id, out var accessoriesFolderLocked), Is.True);

            Assert.That(accessoriesFolderLocked.Name, Is.EqualTo(sampleTree.Accessories_Folder.Name));
            Assert.That(accessoriesFolderLocked.Id, Is.EqualTo(sampleTree.Accessories_Folder.Id));
            Assert.That(accessoriesFolderLocked.CanDetach, Is.False);
            Assert.That(accessoriesFolderLocked.CanAttach, Is.True);
            Assert.That(accessoriesFolderLocked.IsLocked, Is.True);

            Assert.That(accessoriesFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(accessoriesFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(accessoriesFolderLocked.DetachExceptions, Is.Empty);
            Assert.That(accessoriesFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DetachThis_ByWornLayer_AddRem()
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

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachthis:tattoo=n", _sender.Id, _sender.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage("@detachthis:tattoo=y", _sender.Id, _sender.Name), Is.True);

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
