namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SitRestrictionTests : RestrictionsBase
    {

        #region @sit=<y/n>
        [Fact]
        public async Task CanSit()
        {
            await CheckSimpleCommand("sit", m => m.CanSit());
        }
        #endregion
    }
}
