namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class StartImRestrictionTests : RestrictionsBase
    {
        #region @startim=<y/n>
        [Fact]
        public async Task CanStartIM()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@startim=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanStartIM(userId1));
        }
        #endregion

    }
}
