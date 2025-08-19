namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowHoverTextWorldRestrictionTests : RestrictionsBase
    {
        #region @showhovertextworld=<y/n>

        [Fact]
        public async Task CanShowHoverTextWorld()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@showhovertextworld=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));

            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId2));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId2));
        }

        #endregion
    }
}
