namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class EditWorldRestrictionTests : RestrictionsBase
    {
        #region @editworld=<y/n>
        [Fact]
        public async Task CanEdit_World()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage($"@editworld=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null));

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1));
        }
        #endregion
    }
}
