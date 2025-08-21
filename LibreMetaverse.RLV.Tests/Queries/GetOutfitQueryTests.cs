using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetOutfitQueryTests : RestrictionsBase
    {
        #region @getoutfit[:part]=<channel_number>
        [Fact]
        public async Task GetOutfit_WearingNothing()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>();

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "0000000000000000"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetOutfit_ExternalItems()
        {
            var actual = _actionCallbacks.RecordReplies();

            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo);
            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Jaw Thing",
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Jaw,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null);

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable, externalAttachable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "0000000000000010"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetOutfit_WearingSomeItems()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(new Guid($"c0000000-cccc-4ccc-8ccc-cccccccccccc"), "My Socks", new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), null, null, RlvWearableType.Socks),
                new(new Guid($"c0000001-cccc-4ccc-8ccc-cccccccccccc"), "My Hair", new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), null, null, RlvWearableType.Hair)
            };

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "0000001000010000"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetOutfit_WearingEverything()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>();
            foreach (var item in Enum.GetValues<RlvWearableType>())
            {
                if (item == RlvWearableType.Invalid)
                {
                    continue;
                }

                externalItems.Add(new RlvInventoryItem(
                    new Guid($"c{(int)item:D7}-cccc-4ccc-8ccc-cccccccccccc"),
                    $"My {item}",
                    new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                    null,
                    null,
                    item));
            }

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "1111111111111111"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetOutfit_Specific_Exists()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(new Guid($"c0000000-cccc-4ccc-8ccc-cccccccccccc"), "My Socks", new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), null, null, RlvWearableType.Socks)
            };

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "1"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit:socks=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetOutfit_Specific_NotExists()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(new Guid($"c0000001-cccc-4ccc-8ccc-cccccccccccc"), "My Hair", new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), null, null, RlvWearableType.Hair)
            };

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "0"),
            };

            Assert.True(await _rlv.ProcessMessage("@getoutfit:socks=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        #endregion

    }
}
