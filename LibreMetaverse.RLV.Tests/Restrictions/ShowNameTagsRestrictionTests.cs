namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowNameTagsRestrictionTests : RestrictionsBase
    {
        #region @shownametags=<y/n>
        [Fact]
        public async Task CanShowNameTags()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@shownametags=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNameTags(null));
            Assert.False(_rlv.Permissions.CanShowNameTags(userId1));
        }
        #endregion
    }
}
