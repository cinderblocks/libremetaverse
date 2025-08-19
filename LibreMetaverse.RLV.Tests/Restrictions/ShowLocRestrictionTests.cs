namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowLocRestrictionTests : RestrictionsBase
    {
        #region @showloc=<y/n>
        [Fact]
        public async Task CanShowLoc()
        {
            await CheckSimpleCommand("showLoc", m => m.CanShowLoc());
        }
        #endregion

    }
}
