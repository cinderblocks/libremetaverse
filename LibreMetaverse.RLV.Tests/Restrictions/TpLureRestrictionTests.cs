namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TpLureRestrictionTests : RestrictionsBase
    {
        #region @tplure=<y/n>

        [Fact]
        public void CanTpLure_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanTPLure(null));
            Assert.True(_rlv.Permissions.CanTPLure(userId1));
        }

        [Fact]
        public async Task CanTpLure()
        {
            await _rlv.ProcessMessage("@tplure=n", _sender.Id, _sender.Name);

            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.False(_rlv.Permissions.CanTPLure(null));
            Assert.False(_rlv.Permissions.CanTPLure(userId1));
        }
        #endregion
    }
}
