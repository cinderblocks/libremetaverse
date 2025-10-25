namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TpLmRestrictionTests : RestrictionsBase
    {
        #region @tplm=<y/n>
        [Fact]
        public async Task CanTpLm()
        {
            await CheckSimpleCommand("tpLm", m => m.CanTpLm());
        }
        #endregion
    }
}
