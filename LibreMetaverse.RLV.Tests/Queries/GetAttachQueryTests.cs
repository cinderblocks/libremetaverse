using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetAttachQueryTests : RestrictionsBase
    {
        #region @getattach[:attachpt]=<channel_number>
        [Fact]
        public async Task GetAttach_WearingNothing()
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
                (1234, "00000000000000000000000000000000000000000000000000000000"),
            };

            Assert.True(await _rlv.ProcessMessage("@getattach=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAttach_ExternalItems()
        {
            var actual = _actionCallbacks.RecordReplies();

            var currentOutfit = new List<RlvInventoryItem>();
            var externalWearable = new RlvInventoryItem(
                new Guid("12312312-0001-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Tattoo",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                null,
                null,
                RlvWearableType.Tattoo,
                null);
            var externalAttachable = new RlvInventoryItem(
                new Guid("12312312-0002-4aaa-8aaa-aaaaaaaaaaaa"),
                "External Jaw Thing",
                false,
                new Guid("12312312-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                RlvAttachmentPoint.Jaw,
                new Guid("12312312-0002-4aaa-8aaa-ffffffffffff"),
                null,
                null);

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, [externalWearable, externalAttachable]);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "00000000000000000000000000000000000000000000000100000000"),
            };

            Assert.True(await _rlv.ProcessMessage("@getattach=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAttach_WearingSomeItems()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(new Guid($"c0000000-cccc-4ccc-8ccc-cccccccccccc"), "My Socks", false, new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), RlvAttachmentPoint.LeftFoot, new Guid($"c0000000-cccc-4ccc-8ccc-ffffffffffff"), null, null),
                new(new Guid($"c0000001-cccc-4ccc-8ccc-cccccccccccc"), "My Hair", false, new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"), RlvAttachmentPoint.Skull, new Guid($"c0000001-cccc-4ccc-8ccc-ffffffffffff"), null, null)
            };

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "00100001000000000000000000000000000000000000000000000000"),
            };

            Assert.True(await _rlv.ProcessMessage("@getattach=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAttach_WearingEverything()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>();
            foreach (var item in Enum.GetValues<RlvAttachmentPoint>())
            {
                externalItems.Add(new RlvInventoryItem(
                    new Guid($"c{(int)item:D7}-cccc-4ccc-8ccc-cccccccccccc"),
                    $"My {item}",
                    false,
                    new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                    item,
                    new Guid($"c{(int)item:D7}-cccc-4ccc-8ccc-ffffffffffff"),
                    null,
                    null
                ));
            }

            var sampleTree = SampleInventoryTree.BuildInventoryTree();
            var sharedFolder = sampleTree.Root;

            var inventoryMap = new InventoryMap(sharedFolder, externalItems);
            _queryCallbacks.Setup(e =>
                e.TryGetInventoryMapAsync(default)
            ).ReturnsAsync((true, inventoryMap));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "11111111111111111111111111111111111111111111111111111111"),
            };

            Assert.True(await _rlv.ProcessMessage("@getattach=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAttach_Specific_Exists()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(new Guid($"c0000000-cccc-4ccc-8ccc-cccccccccccc"),
                    "My Socks",
                    false,
                    new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                    RlvAttachmentPoint.LeftFoot,
                    new Guid($"c0000000-cccc-4ccc-8ccc-ffffffffffff"),
                    null,
                    null
                ),
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

            Assert.True(await _rlv.ProcessMessage("@getattach:left foot=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetAttach_Specific_NotExists()
        {
            var actual = _actionCallbacks.RecordReplies();
            var externalItems = new List<RlvInventoryItem>()
            {
                new(
                    new Guid($"c0000001-cccc-4ccc-8ccc-cccccccccccc"),
                    "My Hair",
                    false,
                    new Guid("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                    RlvAttachmentPoint.Skull,
                    new Guid($"c0000001-cccc-4ccc-8ccc-ffffffffffff"),
                    null,
                    null
                )
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

            Assert.True(await _rlv.ProcessMessage("@getattach:left foot=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }
        #endregion

    }
}
