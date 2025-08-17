namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowHoverTextAllRestrictionTests : RestrictionsBase
    {

        #region @showhovertextall=<y/n>
        [Fact]
        public async Task CanShowHoverTextAll()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@showhovertextall=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.False(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));
        }
        #endregion
    }
}
