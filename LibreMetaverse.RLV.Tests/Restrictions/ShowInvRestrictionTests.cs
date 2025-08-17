namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowInvRestrictionTests : RestrictionsBase
    {
        #region @showinv=<y/n>
        [Fact]
        public async Task CanShowInv()
        {
            await CheckSimpleCommand("showInv", m => m.CanShowInv());
        }

        #endregion
    }
}
