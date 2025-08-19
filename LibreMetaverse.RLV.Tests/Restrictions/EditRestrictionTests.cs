namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class EditRestrictionTests : RestrictionsBase
    {
        #region @edit=<y/n>

        [Fact]
        public async Task CanEditFolderNameSpecifiesToAddInsteadOfReplace()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@edit=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null));

            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1));
            Assert.False(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1));
        }
        #endregion
    }
}
