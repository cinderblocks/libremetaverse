namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class DenyPermissionExceptionTests : RestrictionsBase
    {
        #region @denypermission=<rem/add>
        [Fact]
        public async Task DenyPermission()
        {
            Assert.True(await _rlv.ProcessMessage($"@denypermission=add", _sender.Id, _sender.Name));
            Assert.True(_rlv.Permissions.IsAutoDenyPermissions());

            Assert.True(await _rlv.ProcessMessage($"@denypermission=rem", _sender.Id, _sender.Name));
            Assert.False(_rlv.Permissions.IsAutoDenyPermissions());
        }
        #endregion
    }
}
