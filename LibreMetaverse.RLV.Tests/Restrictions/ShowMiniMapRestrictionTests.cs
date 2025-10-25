namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowMiniMapRestrictionTests : RestrictionsBase
    {
        #region @showminimap=<y/n>
        [Fact]
        public async Task CanShowMiniMap()
        {
            await CheckSimpleCommand("showMiniMap", m => m.CanShowMiniMap());
        }
        #endregion
    }
}
