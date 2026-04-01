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
    public class AddOutfitRestrictionTests : RlvTestBase
    {
        #region @addoutfit[:<part>]=<y/n>
        [Test]
        public async Task AddOutfit()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (Worn as shirt)
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Worn as hair)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Skull;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Hair;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@addoutfit=n", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId!.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name), Is.True);

            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);
        }

        [Test]
        public async Task AddOutfit_part()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt (Worn as shirt)
            //  |    |= Retro Pants
            //  |    \- Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (Worn as hair)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.WornOn = RlvWearableType.Shirt;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Hair;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@addoutfit:shirt=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.True);
        }
        #endregion

    }
}
