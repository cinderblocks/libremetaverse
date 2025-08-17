namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowNearbyRestrictionTests : RestrictionsBase
    {
        #region @shownearby=<y/n>
        [Fact]
        public async Task CanShowNearby()
        {
            await CheckSimpleCommand("showNearby", m => m.CanShowNearby());
        }
        #endregion
    }
}
