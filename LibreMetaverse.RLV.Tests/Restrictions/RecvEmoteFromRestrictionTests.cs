namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvEmoteFromRestrictionTests : RestrictionsBase
    {
        #region @recvemotefrom:<UUID>=<y/n>
        [Fact]
        public async Task CanRecvChat_RecvEmoteFrom()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvemotefrom:{userId1}=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId1));
            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId2));
            Assert.False(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId1));
            Assert.True(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId2));
        }

        #endregion
    }
}
