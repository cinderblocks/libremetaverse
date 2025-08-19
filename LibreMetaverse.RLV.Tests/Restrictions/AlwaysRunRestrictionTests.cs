namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class AlwaysRunRestrictionTests : RestrictionsBase
    {
        #region @alwaysrun=<y/n>
        [Fact]
        public async Task CanAlwaysRun()
        {
            await CheckSimpleCommand("alwaysRun", m => m.CanAlwaysRun());
        }
        #endregion
    }
}
