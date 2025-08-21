using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class FindFolderQueryTests : RestrictionsBase
    {
        #region @findfolder:part1[&&...&&partN]=<channel_number>
        [Fact]
        public async Task FindFolder_MultipleTerms()
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
                (1234, "Clothing/Hats/Sub Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolder:at&&ub=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        [Fact]
        public async Task FindFolder_SearchOrder()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Clothing/Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FindFolder_IgnorePrivate()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var clothingFolder = sampleTree.Root.Children.Where(n => n.Name == "Clothing").First();
            var hatsFolder = clothingFolder.Children.Where(n => n.Name == "Hats").First();
            hatsFolder.Name = ".Hats";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, ""),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task FindFolder_IgnoreTildePrefix()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var clothingFolder = sampleTree.Root.Children.Where(n => n.Name == "Clothing").First();
            var hatsFolder = clothingFolder.Children.Where(n => n.Name == "Hats").First();
            hatsFolder.Name = "~Hats";

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, ""),
            };

            Assert.True(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        #endregion

    }
}
