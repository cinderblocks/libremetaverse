namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SetDebugRestrictionTests : RestrictionsBase
    {

        #region @setdebug=<y/n>
        [Fact]
        public async Task CanSetDebug()
        {
            await CheckSimpleCommand("setDebug", m => m.CanSetDebug());
        }
        #endregion
    }
}
