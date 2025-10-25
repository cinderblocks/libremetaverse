namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class SendChannelExceptExceptionTests : RestrictionsBase
    {
        #region @sendchannel_except:<channel>=<y/n>
        [Fact]
        public async Task CanSendChannelExcept()
        {
            await _rlv.ProcessMessage("@sendchannel_except:456=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanChat(123, "Hello world"));
            Assert.False(_rlv.Permissions.CanChat(456, "Hello world"));
        }
        #endregion
    }
}
