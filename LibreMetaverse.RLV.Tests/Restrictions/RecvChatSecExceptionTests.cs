namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvChatSecExceptionTests : RestrictionsBase
    {
        #region @recvchat_sec=<y/n>
        [Fact]
        public async Task CanRecvChat_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@recvchat_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvchat:{userId1}=add", sender2.Id, sender2.Name);
            await _rlv.ProcessMessage($"@recvchat:{userId2}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanReceiveChat("Hello world", userId1));
            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId2));
        }
        #endregion
    }
}
