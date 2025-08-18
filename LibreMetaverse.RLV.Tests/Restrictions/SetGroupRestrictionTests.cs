namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SetGroupRestrictionTests : RestrictionsBase
    {
        #region @setgroup=<y/n>
        [Fact]
        public async Task CanSetGroup()
        {
            await CheckSimpleCommand("setGroup", m => m.CanSetGroup());
        }
        #endregion
    }
}
