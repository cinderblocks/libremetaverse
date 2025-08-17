namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class SendChannelExceptionTests : RestrictionsBase
    {
        #region @sendchannel:<channel>=<rem/add>
        [Fact]
        public async Task CanSendChannel_Exception()
        {
            await _rlv.ProcessMessage("@sendchannel=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendchannel:123=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanChat(123, "Hello world"));
        }
        #endregion
    }
}
