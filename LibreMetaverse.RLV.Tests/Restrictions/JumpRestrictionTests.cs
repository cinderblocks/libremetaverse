namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class JumpRestrictionTests : RestrictionsBase
    {
        #region @jump=<y/n> (RLVa)
        [Fact]
        public async Task CanJump()
        {
            await CheckSimpleCommand("jump", m => m.CanJump());
        }
        #endregion
    }
}
