namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class TouchWorldExceptionTests : RestrictionsBase
    {

        #region @touchworld:<Guid>=<rem/add>
        [Fact]
        public async Task TouchWorld_Exception()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var userId1 = new Guid("55555555-5555-4555-8555-555555555555");

            await _rlv.ProcessMessage("@touchworld=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@touchworld:{objectId2}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId1, null, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId1, userId1, null));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId1, null, 5.0f));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId1, null, null));

            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectId2, null, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectId2, userId1, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectId2, null, 5.0f));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectId2, null, null));
        }

        #endregion
    }
}
