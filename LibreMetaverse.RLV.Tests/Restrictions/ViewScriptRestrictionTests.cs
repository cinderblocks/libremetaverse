namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ViewScriptRestrictionTests : RestrictionsBase
    {
        #region @viewscript=<y/n>
        [Fact]
        public async Task CanViewScript()
        {
            await CheckSimpleCommand("viewScript", m => m.CanViewScript());
        }
        #endregion
    }
}
