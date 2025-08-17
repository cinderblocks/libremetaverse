namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class UnsitRestrictionTests : RestrictionsBase
    {
        #region @unsit=<y/n>
        [Fact]
        public async Task CanUnsit()
        {
            await CheckSimpleCommand("unsit", m => m.CanUnsit());
        }
        #endregion
    }
}
