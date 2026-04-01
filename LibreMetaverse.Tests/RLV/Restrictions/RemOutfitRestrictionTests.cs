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
    public class RemOutfitRestrictionTests : RlvTestBase
    {

        #region @remoutfit[:<part>]=<y/n>
        [Test]
        public async Task RemOutfit()
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
            //  |        |= Fancy Hat (Attached to Chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@remoutfit=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.False);
        }

        [Test]
        public async Task RemOutfit_part()
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
            //  |        |= Fancy Hat (Attached to Chin)
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn as Tattoo)
            //        \= Glasses
            //

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@remoutfit:pants=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_RetroPants), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);
        }
        #endregion

    }
}
