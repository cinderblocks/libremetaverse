namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RezRestrictionTests : RestrictionsBase
    {
        #region @rez=<y/n>
        [Fact]
        public async Task CanRez()
        {
            await CheckSimpleCommand("rez", m => m.CanRez());
        }

        #endregion
    }
}
