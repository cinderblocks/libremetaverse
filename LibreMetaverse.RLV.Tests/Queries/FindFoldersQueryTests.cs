using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class FindFoldersQueryTests : RestrictionsBase
    {
        #region @findfolders:part1[&&...&&partN][;output_separator]=<channel_number>
        [Fact]
        public async Task FindFolders()
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

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Clothing/Hats,Clothing/Hats/Sub Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolders:at=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FindFolders_Separator()
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

            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Clothing/Hats AND Clothing/Hats/Sub Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolders:at; AND =1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        #endregion

    }
}
