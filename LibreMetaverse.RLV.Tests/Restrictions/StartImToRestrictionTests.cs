namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class StartImToRestrictionTests : RestrictionsBase
    {

        #region @startimto:<UUID>=<y/n>
        [Fact]
        public async Task CanStartIMTo()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@startimto:{userId2}=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanStartIM(userId1));
            Assert.False(_rlv.Permissions.CanStartIM(userId2));
        }
        #endregion
    }
}
