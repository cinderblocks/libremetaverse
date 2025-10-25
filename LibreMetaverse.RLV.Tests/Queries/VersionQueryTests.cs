using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class VersionQueryTests : RestrictionsBase
    {
        #region @version Manual
        [Fact]
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
        [Theory]
        [InlineData("@version", 1234, RlvService.RLVVersion)]
        [InlineData("@versionnew", 1234, RlvService.RLVVersion)]
        [InlineData("@versionnum", 1234, RlvService.RLVVersionNum)]
        public async Task CheckVersions(string command, int channel, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage($"{command}={channel}", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, expectedResponse),
            };

            Assert.Equal(expected, actual);
        }
        #endregion
    }
}
