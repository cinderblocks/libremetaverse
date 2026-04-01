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
    public class RemAttachRestrictionTests : RlvTestBase
    {
        #region @remattach[:<attach_point_name>]=<y/n>
        [Test]
        public async Task RemAttach()
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

            Assert.That(await _rlv.ProcessMessage("@remattach=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);
        }

        [Test]
        public async Task RemAttach_Specific()
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

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@remattach:spine=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_PartyHat_Spine), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Clothing_Hats_FancyHat_Chin), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(sampleTree.Root_Accessories_Watch), Is.True);
        }

        #endregion
    }
}
