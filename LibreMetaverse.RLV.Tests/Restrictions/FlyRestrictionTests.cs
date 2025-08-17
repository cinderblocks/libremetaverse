namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class FlyRestrictionTests : RestrictionsBase
    {
        #region @fly=<y/n>
        [Fact]
        public async Task CanFly()
        {
            await CheckSimpleCommand("fly", m => m.CanFly());
        }
        #endregion
    }
}
