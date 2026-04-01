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
    public class FindFolderQueryTests : RlvTestBase
    {
        #region @findfolder:part1[&&...&&partN]=<channel_number>
        [Test]
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

            Assert.That(await _rlv.ProcessMessage("@findfolder:at&&ub=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        [Test]
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

            Assert.That(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
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

            Assert.That(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
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

            Assert.That(await _rlv.ProcessMessage("@findfolder:at=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

    }
}
