namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class PermissiveRestrictionTests : RestrictionsBase
    {
        #region @Permissive
        [Fact]
        public async Task Permissive_On()
        {
            await _rlv.ProcessMessage("@permissive=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.IsPermissive());
        }

        [Fact]
        public async Task Permissive_Off()
        {
            await _rlv.ProcessMessage("@permissive=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@permissive=y", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.IsPermissive());
        }
        #endregion
    }
}
