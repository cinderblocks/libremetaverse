namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class StandTpRestrictionTests : RestrictionsBase
    {
        #region @standtp=<y/n>
        [Fact]
        public async Task CanStandTp()
        {
            await CheckSimpleCommand("standTp", m => m.CanStandTp());
        }
        #endregion
    }
}
