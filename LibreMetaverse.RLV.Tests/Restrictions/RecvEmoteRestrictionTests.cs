namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvEmoteRestrictionTests : RestrictionsBase
    {
        #region @recvemote=<y/n>
        [Fact]
        public async Task CanRecvChat_RecvEmote()
        {
            await _rlv.ProcessMessage("@recvemote=n", _sender.Id, _sender.Name);

            var userId = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId));
            Assert.False(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId));
        }
        #endregion
    }
}
