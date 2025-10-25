namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShareRestrictionTests : RestrictionsBase
    {
        #region @share=<y/n>

        [Fact]
        public async Task CanShare()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@share=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShare(null));
            Assert.False(_rlv.Permissions.CanShare(userId1));
        }
        #endregion
    }
}
