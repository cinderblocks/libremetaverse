namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class TouchThisExceptionTests : RestrictionsBase
    {
        #region @touchthis:<Guid>=<rem/add>

        [Fact]
        public async Task TouchThis_default()
        {
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-111111111111");

            var userId1 = new Guid("55555555-5555-4555-8555-555555555555");

            await _rlv.ProcessMessage($"@touchthis:{objectPrimId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectPrimId1, null, null));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectPrimId1, userId1, null));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectPrimId1, null, 5.0f));
            Assert.False(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectPrimId1, null, null));

            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedSelf, objectPrimId2, null, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.AttachedOther, objectPrimId2, userId1, null));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.RezzedInWorld, objectPrimId2, null, 5.0f));
            Assert.True(_rlv.Permissions.CanTouch(RlvPermissionsService.TouchLocation.Hud, objectPrimId2, null, null));
        }

        #endregion

    }
}
