namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class RecvChatExceptionTests : RestrictionsBase
    {
        #region @recvchat:<UUID>=<rem/add>
        [Fact]
        public async Task CanRecvChat_Except()
        {
            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@recvchat=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvchat:{userId}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId));
            Assert.True(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId));
        }
        #endregion
    }
}
