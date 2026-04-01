using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class GetInvWornQueryTests : RlvTestBase
    {
        #region @getinvworn[:folder1/.../folderN]=<channel_number>
        [Test]
        public async Task GetInvWorn()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants (attached to 'groin')
            //  |    |= Happy Shirt (attached to 'chest')
            //  |    |= Retro Pants (worn on 'pants')
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached to 'chin')
            //  |        \= Party Hat (attached to 'groin')
            //   \-Accessories
            //        |= Watch (worn on 'tattoo')
            //        \= Glasses (attached to 'chin')
            //
            // 0: No item is present in that folder
            // 1: Some items are present in that folder, but none of them is worn
            // 2: Some items are present in that folder, and some of them are worn
            // 3: Some items are present in that folder, and all of them are worn
            //
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_HappyShirt.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_HappyShirt.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0004-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0005-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "|03,Clothing|33,Accessories|33"),
            };

            Assert.That(await _rlv.ProcessMessage("@getinvworn=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetInvWorn_PartialRoot()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants (attached to 'pelvis')
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (worn on 'tattoo')
            //        \= Glasses
            //
            // 0: No item is present in that folder
            // 1: Some items are present in that folder, but none of them is worn
            // 2: Some items are present in that folder, and some of them are worn
            // 3: Some items are present in that folder, and all of them are worn
            //
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Tattoo;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "|02,Clothing|22,Accessories|22"),
            };

            Assert.That(await _rlv.ProcessMessage("@getinvworn=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetInvWorn_Naked()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
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
            // 0: No item is present in that folder
            // 1: Some items are present in that folder, but none of them is worn
            // 2: Some items are present in that folder, and some of them are worn
            // 3: Some items are present in that folder, and all of them are worn
            //
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "|01,Clothing|11,Accessories|11"),
            };

            Assert.That(await _rlv.ProcessMessage("@getinvworn=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetInvWorn_EmptyFolder()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
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
            // 0: No item is present in that folder
            // 1: Some items are present in that folder, but none of them is worn
            // 2: Some items are present in that folder, and some of them are worn
            // 3: Some items are present in that folder, and all of them are worn
            //
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "|00"),
            };

            Assert.That(await _rlv.ProcessMessage("@getinvworn:Clothing/Hats/Sub Hats=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetInvWorn_PartialWorn()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached to 'Chin')
            //  |        \= Party Hat (attached to 'Spine')
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //
            // 0: No item is present in that folder
            // 1: Some items are present in that folder, but none of them is worn
            // 2: Some items are present in that folder, and some of them are worn
            // 3: Some items are present in that folder, and all of them are worn
            //
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Chin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "|33,Sub Hats|00"),
            };

            Assert.That(await _rlv.ProcessMessage("@getinvworn:Clothing/Hats=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

    }
}
