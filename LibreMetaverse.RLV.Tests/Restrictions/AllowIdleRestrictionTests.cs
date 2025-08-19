namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class AllowIdleRestrictionTests : RestrictionsBase
    {
        #region @allowidle=<y/n>
        [Fact]
        public async Task CanAllowIdle()
        {
            await CheckSimpleCommand("allowIdle", m => m.CanAllowIdle());
        }
        #endregion
    }
}
