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
    public class VersionQueryTests : RlvTestBase
    {
        #region @version Manual
        [Test]
        public async Task ManualVersion()
        {
            _rlv.EnableInstantMessageProcessing = true;

            await _rlv.ProcessInstantMessage("@version", _sender.Id);

            _actionCallbacks.Verify(c =>
                c.SendInstantMessageAsync(_sender.Id, RlvService.RLVVersion, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion

        #region @version=<channel_number>
        [TestCase("@version", 1234, RlvService.RLVVersion)]
        [TestCase("@versionnew", 1234, RlvService.RLVVersion)]
        [TestCase("@versionnum", 1234, RlvService.RLVVersionNum)]
        public async Task CheckVersions(string command, int channel, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage($"{command}={channel}", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, expectedResponse),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion
    }
}
