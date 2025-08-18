namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowWorldMapRestrictionTests : RestrictionsBase
    {
        #region  @showworldmap=<y/n>
        [Fact]
        public async Task CanShowWorldMap()
        {
            await CheckSimpleCommand("showWorldMap", m => m.CanShowWorldMap());
        }
        #endregion
    }
}
