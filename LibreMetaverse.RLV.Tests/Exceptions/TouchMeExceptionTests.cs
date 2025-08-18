namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class TouchMeExceptionTests : RestrictionsBase
    {
        #region @touchme=<rem/add>

        [Fact]
        public async Task TouchMe_default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId1 = new Guid("55555555-5555-4555-8555-555555555555");

            await _rlv.ProcessMessage("@touchall=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@touchme=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId1, null, null));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId1, userId1, null));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId1, null, 5.0f));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId1, null, null));

            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, _sender.Id, null, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, _sender.Id, userId1, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, _sender.Id, null, 5.0f));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, _sender.Id, null, null));
        }

        #endregion

    }
}
