namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class EditExceptionTests : RestrictionsBase
    {
        #region @edit:<UUID>=<rem/add>
        [Fact]
        public async Task CanEdit_Exception()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@edit=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@edit:{objectId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null));

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1));

            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId2));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId2));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId2));
        }

        [Fact]
        public async Task CanEdit_Specific()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@editobj:{objectId1}=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null));

            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1));

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId2));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId2));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId2));
        }
        #endregion
    }
}
