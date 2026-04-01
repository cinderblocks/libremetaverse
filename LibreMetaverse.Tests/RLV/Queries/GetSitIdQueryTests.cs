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
    public class GetSitIdQueryTests : RlvTestBase
    {
        #region @getsitid=<channel_number>

        private void SetCurrentSitId(Guid objectId)
        {
            _queryCallbacks.Setup(e =>
                e.TryGetSitIdAsync(default)
            ).ReturnsAsync((objectId != Guid.Empty, objectId));
        }

        [Test]
        public async Task GetSitID()
        {
            var actual = _actionCallbacks.RecordReplies();
            SetCurrentSitId(Guid.Empty);

            await _rlv.ProcessMessage("@getsitid=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "NULL_KEY"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task GetSitID_Default()
        {
            var actual = _actionCallbacks.RecordReplies();
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetCurrentSitId(objectId1);

            await _rlv.ProcessMessage("@getsitid=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, objectId1.ToString()),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion
    }
}
