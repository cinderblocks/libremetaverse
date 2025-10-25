namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class DefaultWearRestrictionTests : RestrictionsBase
    {
        #region @defaultwear=<y/n>

        [Fact]
        public async Task CanDefaultWear()
        {
            await CheckSimpleCommand("defaultWear", m => m.CanDefaultWear());
        }

        #endregion
    }
}
