using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class DetachAllThisExceptExceptionTests : RlvTestBase
    {
        #region @detachallthis_except:<folder>=<rem/add>

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage($"@detachallthis_except:Clothing/Hats=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat () - Parent folder locked recursively, but has exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat () - Parent folder locked recursively, but has exception
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
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            // #RLV/Clothing/Hats/Party Hat () - Parent folder locked recursively, but parent has recursive exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat () - Parent folder locked recursively, but parent has recursive exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);

            // #RLV/Clothing/Business Pants ()  - Locked, but folder has exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.True);

            // #RLV/Clothing/Happy Shirt () - Locked, but folder has exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.True);

            // #RLV/Clothing/Retro Pants () - Locked, but folder has exception
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.True);

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
            Assert.That(clothingFolderLocked.CanDetach, Is.True);
            Assert.That(clothingFolderLocked.CanAttach, Is.True);
            Assert.That(clothingFolderLocked.IsLocked, Is.True);

            Assert.That(hatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Name));
            Assert.That(hatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_Folder.Id));
            Assert.That(hatsFolderLocked.CanDetach, Is.True);
            Assert.That(hatsFolderLocked.CanAttach, Is.True);
            Assert.That(hatsFolderLocked.IsLocked, Is.True);

            Assert.That(subhatsFolderLocked.Name, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Name));
            Assert.That(subhatsFolderLocked.Id, Is.EqualTo(sampleTree.Clothing_Hats_SubHats_Folder.Id));
            Assert.That(subhatsFolderLocked.CanDetach, Is.True);
            Assert.That(subhatsFolderLocked.CanAttach, Is.True);
            Assert.That(subhatsFolderLocked.IsLocked, Is.True);

            Assert.That(clothingFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(clothingFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(clothingFolderLocked.DetachExceptions, Has.Count.EqualTo(1));
            Assert.That(clothingFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(hatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(hatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(hatsFolderLocked.DetachExceptions, Has.Count.EqualTo(1));
            Assert.That(hatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));

            Assert.That(subhatsFolderLocked.AttachExceptions, Is.Empty);
            Assert.That(subhatsFolderLocked.AttachRestrictions, Is.Empty);
            Assert.That(subhatsFolderLocked.DetachExceptions, Has.Count.EqualTo(1));
            Assert.That(subhatsFolderLocked.DetachRestrictions, Has.Count.EqualTo(1));
        }

        [Test]
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

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@detachallthis=n", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=add", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);
            Assert.That(await _rlv.ProcessMessage($"@detachallthis_except:Clothing=rem", sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId!.Value, sampleTree.Root_Clothing_BusinessPants_Pelvis.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_BusinessPants_Pelvis), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_HappyShirt), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Glasses), Is.True);
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

        #endregion
    }
}
