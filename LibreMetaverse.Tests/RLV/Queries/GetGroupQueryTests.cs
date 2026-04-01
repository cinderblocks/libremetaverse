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
    public class GetGroupQueryTests : RlvTestBase
    {
        #region @getgroup=<channel_number>

        [Test]
        public async Task GetGroup_Default()
        {
            var actual = _actionCallbacks.RecordReplies();
            var actualGroupName = "Group Name";

            _queryCallbacks.Setup(e =>
                e.TryGetActiveGroupNameAsync(default)
            ).ReturnsAsync((true, actualGroupName));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, actualGroupName),
            };

            Assert.That(await _rlv.ProcessMessage("@getgroup=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetGroup_NoGroup()
        {
            var actual = _actionCallbacks.RecordReplies();
            var actualGroupName = "";

            _queryCallbacks.Setup(e =>
                e.TryGetActiveGroupNameAsync(default)
            ).ReturnsAsync((false, actualGroupName));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "none"),
            };

            Assert.That(await _rlv.ProcessMessage("@getgroup=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion
    }
}
