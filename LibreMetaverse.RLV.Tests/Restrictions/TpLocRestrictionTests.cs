namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TpLocRestrictionTests : RestrictionsBase
    {
        #region @tploc=<y/n>
        [Fact]
        public async Task CanTpLoc()
        {
            await CheckSimpleCommand("tpLoc", m => m.CanTpLoc());
        }
        #endregion
    }
}
