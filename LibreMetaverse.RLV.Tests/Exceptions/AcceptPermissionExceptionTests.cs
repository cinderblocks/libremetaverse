namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class AcceptPermissionExceptionTests : RestrictionsBase
    {
        #region @acceptpermission=<rem/add>
        [Fact]
        public async Task AcceptPermission()
        {
            Assert.True(await _rlv.ProcessMessage($"@acceptpermission=add", _sender.Id, _sender.Name));
            Assert.True(_rlv.Permissions.IsAutoAcceptPermissions());

            Assert.True(await _rlv.ProcessMessage($"@acceptpermission=rem", _sender.Id, _sender.Name));
            Assert.False(_rlv.Permissions.IsAutoAcceptPermissions());
        }
        #endregion

    }
}
