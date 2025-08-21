using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetInvQueryTests : RestrictionsBase
    {
        #region @getinv[:folder1/.../folderN]=<channel_number>
        [Fact]
        public async Task GetInv()
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
                (1234, "Clothing,Accessories"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Outfits()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var outfitsFolderId = new Guid("12312399-9999-4999-8999-999999999999");
            var outfitSubfolder1Id = new Guid("12312399-0001-4999-8999-999999999999");
            var outfitSubfolder2Id = new Guid("12312399-0002-4999-8999-999999999999");

            var outfitsFolder = sampleTree.Root.AddChild(outfitsFolderId, ".outfits");
            var outfitSubfolder1 = outfitsFolder.AddChild(outfitSubfolder1Id, "First outfit");
            var outfitSubfolder2 = outfitsFolder.AddChild(outfitSubfolder2Id, "Second outfit");

            var item1 = outfitsFolder.AddItem(new Guid("12312399-0001-0001-8999-999999999999"), "First Item", null, null, null);
            var item2 = outfitsFolder.AddItem(new Guid("12312399-0001-0002-8999-999999999999"), "Second Item", null, null, null);

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"{outfitSubfolder1.Name},{outfitSubfolder2.Name}"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:.outfits=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Outfits_IgnoreLeadingSlash()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var outfitsFolder = sampleTree.Root.AddChild(new Guid("12312399-9999-4999-8999-999999999999"), "~MyOutfits");
            var outfitSubfolder1 = outfitsFolder.AddChild(new Guid("12312399-0001-4999-8999-999999999999"), "First outfit");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"{outfitSubfolder1.Name}"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:/~MyOutfits=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Outfits_IgnoreTrailingSlash()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var outfitsFolder = sampleTree.Root.AddChild(new Guid("12312399-9999-4999-8999-999999999999"), "~MyOutfits");
            var outfitSubfolder1 = outfitsFolder.AddChild(new Guid("12312399-0001-4999-8999-999999999999"), "First outfit");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"{outfitSubfolder1.Name}"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:~MyOutfits/=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Outfits_IgnoreLeadingAndTrailingSlash()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var outfitsFolder = sampleTree.Root.AddChild(new Guid("12312399-9999-4999-8999-999999999999"), "~MyOutfits");
            var outfitSubfolder1 = outfitsFolder.AddChild(new Guid("12312399-0001-4999-8999-999999999999"), "First outfit");

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $"{outfitSubfolder1.Name}"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:/~MyOutfits/=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Outfits_Inventory()
        {
            var actual = _actionCallbacks.RecordReplies();
            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var outfitsFolderId = new Guid("12312399-9999-4999-8999-999999999999");
            var outfitSubfolder1Id = new Guid("12312399-0001-4999-8999-999999999999");

            var outfitsFolder = sampleTree.Root.AddChild(outfitsFolderId, ".outfits");
            var outfitSubfolder1 = outfitsFolder.AddChild(outfitSubfolder1Id, "First outfit");
            var item1 = outfitSubfolder1.AddItem(new Guid("12312399-0001-0001-8999-999999999999"), "First Item", null, null, null);
            var item2 = outfitSubfolder1.AddItem(new Guid("12312399-0001-0002-8999-999999999999"), "Second Item", null, null, null);

            var inventoryMap = new InventoryMap(sharedFolder, []);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, $""),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:.outfits/First outfit=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Subfolder()
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
                (1234, "Sub Hats"),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:Clothing/Hats=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Empty()
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
                (1234, ""),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:Clothing/Hats/Sub Hats=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetInv_Invalid()
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
                (1234, ""),
            };

            Assert.True(await _rlv.ProcessMessage("@getinv:Invalid Folder=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        #endregion

    }
}
