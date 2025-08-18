namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendChannelSecRestrictionTests : RestrictionsBase
    {
        #region @sendchannel_sec[:<channel>]=<y/n>
        [Fact]
        public async Task CanSendChannel_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@sendchannel_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendchannel:123=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendchannel:456=n", sender2.Id, sender2.Name);

            Assert.True(_rlv.Permissions.CanChat(123, "Hello world"));
            Assert.False(_rlv.Permissions.CanChat(456, "Hello world"));
        }

        [Fact]
        public async Task CanSendChannel_Secure_Exception()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@sendchannel_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@sendchannel_sec:123=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanChat(123, "Hello world"));
            Assert.False(_rlv.Permissions.CanChat(456, "Hello world"));
        }
        #endregion
    }
}
