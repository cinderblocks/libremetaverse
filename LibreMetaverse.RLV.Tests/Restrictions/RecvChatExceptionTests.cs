namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvChatExceptionTests : RestrictionsBase
    {
        [Fact]
        public async Task CanRecvChat()
        {
            await _rlv.ProcessMessage("@recvchat=n", _sender.Id, _sender.Name);
            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.False(_rlv.Permissions.CanReceiveChat("Hello world", userId));
            Assert.True(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId));
        }
    }
}
