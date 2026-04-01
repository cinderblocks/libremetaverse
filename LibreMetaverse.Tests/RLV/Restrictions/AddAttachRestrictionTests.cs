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
    public class AddAttachRestrictionTests : RlvTestBase
    {
        #region @addattach[:<attach_point_name>]=<y/n>
        [Test]
        public async Task AddAttach()
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@addattach=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.False);

            // #RLV/Clothing/Happy Shirt
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);
        }

        [Test]
        public async Task AddAttach_Specific()
        {
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            Assert.That(await _rlv.ProcessMessage("@addattach:groin=n", _sender.Id, _sender.Name), Is.True);

            // #RLV/Clothing/Hats/Fancy Hat
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_Hats_FancyHat_Chin, true), Is.True);

            // #RLV/Clothing/Happy Shirt
            Assert.That(_rlv.Permissions.CanAttach(sampleTree.Root_Clothing_HappyShirt, true), Is.False);
        }
        #endregion

    }
}
