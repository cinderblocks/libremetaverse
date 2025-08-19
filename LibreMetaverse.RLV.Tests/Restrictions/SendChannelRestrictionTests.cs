namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendChannelRestrictionTests : RestrictionsBase
    {
        #region @sendchannel[:<channel>]=<y/n>
        [Fact]
        public async Task CanSendChannel()
        {
            await _rlv.ProcessMessage("@sendchannel=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanChat(123, "Hello world"));
        }
        #endregion
    }
}
