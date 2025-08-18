namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowHoverTextRestrictionTests : RestrictionsBase
    {
        #region @showhovertext:<Guid>=<y/n>
        [Fact]
        public async Task CanShowHoverText()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@showhovertext:{objectId1}=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));

            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId2));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId2));
        }
        #endregion
    }
}
