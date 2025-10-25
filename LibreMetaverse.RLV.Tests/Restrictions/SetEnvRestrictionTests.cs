namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SetEnvRestrictionTests : RestrictionsBase
    {
        #region @setenv=<y/n>
        [Fact]
        public async Task CanSetEnv()
        {
            await CheckSimpleCommand("setEnv", m => m.CanSetEnv());
        }
        #endregion
    }
}
