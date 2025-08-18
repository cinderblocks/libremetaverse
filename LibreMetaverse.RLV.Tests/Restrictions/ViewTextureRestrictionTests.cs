namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ViewTextureRestrictionTests : RestrictionsBase
    {
        #region @viewtexture=<y/n>
        [Fact]
        public async Task CanViewTexture()
        {
            await CheckSimpleCommand("viewTexture", m => m.CanViewTexture());
        }
        #endregion
    }
}
