namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TempRunRestrictionTests : RestrictionsBase
    {
        #region @temprun=<y/n>
        [Fact]
        public async Task CanTempRun()
        {
            await CheckSimpleCommand("tempRun", m => m.CanTempRun());
        }
        #endregion
    }
}
