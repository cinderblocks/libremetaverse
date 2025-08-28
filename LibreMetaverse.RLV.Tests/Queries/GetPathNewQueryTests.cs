using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetPathNewQueryTests : RestrictionsBase
    {
        #region @getpath @getpathnew[:<attachpt> or <clothing_layer> or <uuid>]=<channel_number>

        [Fact]
        public async Task GetPathNew_BySender()
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
            //  |        \= Party Hat (Worn on spine)
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Clothing/Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@getpathnew=1234", sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId.Value, sampleTree.Root_Clothing_Hats_PartyHat_Spine.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPathNew_ByUUID()
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
            //  |        \= Party Hat (Worn on spine)
            //   \-Accessories
            //        |= Watch
            //        \= Glasses
            //

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = RlvAttachmentPoint.Spine;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Clothing/Hats"),
            };

            Assert.True(await _rlv.ProcessMessage($"@getpathnew:{sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedPrimId.Value}=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPathNew_ByUUID_Unknown()
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

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = null;
            sampleTree.Root_Clothing_Hats_PartyHat_Spine.AttachedTo = null;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = null;
            sampleTree.Root_Clothing_HappyShirt.AttachedTo = null;
            sampleTree.Root_Accessories_Glasses.AttachedTo = null;
            sampleTree.Root_Clothing_RetroPants.WornOn = null;
            sampleTree.Root_Accessories_Watch.WornOn = null;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, ""),
            };

            Assert.True(await _rlv.ProcessMessage($"@getpathnew:BADBADBA-DBAD-4BAD-8BAD-BADBADBADBAD=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPathNew_ByAttach()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants (attached to 'Pelvis')
            //  |    |= Happy Shirt
            //  |    |= Retro Pants
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (attached to 'Groin')
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch
            //        \= Glasses (attached to 'Groin')
            //

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedTo = RlvAttachmentPoint.Pelvis;
            sampleTree.Root_Clothing_BusinessPants_Pelvis.AttachedPrimId = new Guid("11111111-0001-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.AttachedPrimId = new Guid("11111111-0002-4aaa-8aaa-ffffffffffff");

            sampleTree.Root_Accessories_Glasses.AttachedTo = RlvAttachmentPoint.Groin;
            sampleTree.Root_Accessories_Glasses.AttachedPrimId = new Guid("11111111-0003-4aaa-8aaa-ffffffffffff");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Accessories,Clothing/Hats"),
            };

            Assert.True(await _rlv.ProcessMessage($"@getpathnew:groin=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetPathNew_ByWorn()
        {
            // #RLV
            //  |
            //  |- .private
            //  |
            //  |- Clothing
            //  |    |= Business Pants
            //  |    |= Happy Shirt
            //  |    |= Retro Pants (worn on 'Tattoo')
            //  |    \-Hats
            //  |        |
            //  |        |- Sub Hats
            //  |        |    \ (Empty)
            //  |        |
            //  |        |= Fancy Hat (worn on 'Pants')
            //  |        \= Party Hat
            //   \-Accessories
            //        |= Watch (Worn on 'pants')
            //        \= Glasses
            //


            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            sampleTree.Root_Clothing_RetroPants.WornOn = RlvWearableType.Tattoo;
            sampleTree.Root_Clothing_Hats_FancyHat_Chin.WornOn = RlvWearableType.Pants;
            sampleTree.Root_Accessories_Watch.WornOn = RlvWearableType.Pants;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Accessories,Clothing/Hats"),
            };

            Assert.True(await _rlv.ProcessMessage($"@getpathnew:pants=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
