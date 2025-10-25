namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowNamesRestrictionTests : RestrictionsBase
    {
        #region @shownames=<y/n>
        [Fact]
        public async Task CanShowNames()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            await _rlv.ProcessMessage("@shownames=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNames(null));
            Assert.False(_rlv.Permissions.CanShowNames(userId1));
        }
        #endregion
    }
}
