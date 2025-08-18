namespace LibreMetaverse.RLV.Tests.Queries
{
    public class VersionNumBlQueryTests : RestrictionsBase
    {

        #region @versionnumbl=<channel_number>

        [Theory]
        [InlineData("", RlvService.RLVVersionNum)]
        [InlineData("sendim,recvim", RlvService.RLVVersionNum + ",recvim,sendim")]
        public async Task VersionNumBL(string seed, string expectedResponse)
        {
            var actual = _actionCallbacks.RecordReplies();
            SeedBlacklist(seed);

            await _rlv.ProcessMessage("@versionnumbl=1234", _sender.Id, _sender.Name);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, expectedResponse),
            };

            Assert.Equal(expected, actual);
        }
        #endregion
    }
}
