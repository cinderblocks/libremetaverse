namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendImRestrictionTests : RestrictionsBase
    {
        #region @sendim=<y/n>
        [Fact]
        public async Task CanSendIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanSendIM("Hello", userId1));
            Assert.False(_rlv.Permissions.CanSendIM("Hello", userId1, "Group Name"));
        }
        #endregion
    }
}
