using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class GetBlacklistQueryTests : RlvTestBase
    {
        #region @getblacklist[:filter]=<channel_number>
        [TestCase("@getblacklist", 1234, "sendim,recvim", "recvim,sendim")]
        [TestCase("@getblacklist:im", 1234, "sendim,recvim", "recvim,sendim")]
        [TestCase("@getblacklist:send", 1234, "sendim,recvim", "sendim")]
        [TestCase("@getblacklist:tpto", 1234, "sendim,recvim", "")]
        [TestCase("@getblacklist", 1234, "", "")]
        public async Task GetBlacklist(string command, int channel, string seed, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();
            SeedBlacklist(seed);

            await _rlv.ProcessMessageAsync($"{command}={channel}", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (channel, expectedResponse),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

        #region @getblacklist Manual
        [TestCase("@getblacklist", "sendim,recvim", "recvim,sendim")]
        [TestCase("@getblacklist", "", "")]
        public async Task ManualBlacklist(string command, string seed, string expected)
        {
            _rlv.EnableInstantMessageProcessing = true;

            SeedBlacklist(seed);

            await _rlv.ProcessInstantMessageAsync(command, _sender.Id);

            _actionCallbacks.Verify(c =>
                c.SendInstantMessageAsync(_sender.Id, expected, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
