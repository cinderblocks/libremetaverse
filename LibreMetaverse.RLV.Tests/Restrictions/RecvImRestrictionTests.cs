namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvImRestrictionTests : RestrictionsBase
    {
        #region @recvim=<y/n>
        [Fact]
        public async Task CanReceiveIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanReceiveIM("Hello", userId1));
            Assert.False(_rlv.Permissions.CanReceiveIM("Hello", userId1, "Group Name"));
        }

        #endregion
    }
}
