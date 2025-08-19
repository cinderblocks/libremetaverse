using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetBlacklistQueryTests : RestrictionsBase
    {
        #region @getblacklist[:filter]=<channel_number>
        [Theory]
        [InlineData("@getblacklist", 1234, "sendim,recvim", "recvim,sendim")]
        [InlineData("@getblacklist:im", 1234, "sendim,recvim", "recvim,sendim")]
        [InlineData("@getblacklist:send", 1234, "sendim,recvim", "sendim")]
        [InlineData("@getblacklist:tpto", 1234, "sendim,recvim", "")]
        [InlineData("@getblacklist", 1234, "", "")]
        public async Task GetBlacklist(string command, int channel, string seed, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();
            SeedBlacklist(seed);

            await _rlv.ProcessMessage($"{command}={channel}", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (channel, expectedResponse),
            };

            Assert.Equal(expected, actual);
        }
        #endregion

        #region @getblacklist Manual
        [Theory]
        [InlineData("@getblacklist", "sendim,recvim", "recvim,sendim")]
        [InlineData("@getblacklist", "", "")]
        public async Task ManualBlacklist(string command, string seed, string expected)
        {
            _rlv.EnableInstantMessageProcessing = true;

            SeedBlacklist(seed);

            await _rlv.ProcessInstantMessage(command, _sender.Id);

            _actionCallbacks.Verify(c =>
                c.SendInstantMessageAsync(_sender.Id, expected, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
