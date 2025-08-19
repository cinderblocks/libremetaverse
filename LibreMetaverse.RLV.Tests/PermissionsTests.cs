namespace LibreMetaverse.RLV.Tests
{
    public class PermissionsTests : RestrictionsBase
    {

        [Fact]
        public void CamZoomMin_Default()
        {
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.Null(cameraRestrictions.ZoomMin);
        }

        [Fact]
        public void CanShowHoverTextWorld_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));
        }

        [Fact]
        public void CanShowHoverTextHud_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));
        }

        [Fact]
        public void CanShowHoverText_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));
        }


        [Fact]
        public void CanShowHoverTextAll_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1));
            Assert.True(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1));
        }


        [Fact]
        public void CanShowNameTags_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanShowNameTags(null));
            Assert.True(_rlv.Permissions.CanShowNameTags(userId1));
        }

        [Fact]
        public void CanShare_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanShare(null));
            Assert.True(_rlv.Permissions.CanShare(userId1));
        }

        [Fact]
        public void CanShowNames_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanShowNames(null));
            Assert.True(_rlv.Permissions.CanShowNames(userId1));
        }

        [Fact]
        public void CanEdit_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null));

            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1));
            Assert.True(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1));
        }

        [Fact]
        public void CanRecvChat_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var userId2 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

            Assert.True(_rlv.Permissions.CanReceiveChat("Hello world", userId1));
            Assert.True(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId2));
        }

        [Fact]
        public void CanAutoAcceptTpRequest_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            Assert.False(_rlv.Permissions.IsAutoAcceptTpRequest(userId1));
            Assert.False(_rlv.Permissions.IsAutoAcceptTpRequest(userId2));
            Assert.False(_rlv.Permissions.IsAutoAcceptTpRequest());
        }

        [Fact]
        public void CanTpRequest_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.True(_rlv.Permissions.CanTpRequest(null));
            Assert.True(_rlv.Permissions.CanTpRequest(userId1));
        }

        [Fact]
        public void CanAutoAcceptTp_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            Assert.False(_rlv.Permissions.IsAutoAcceptTp(userId1));
            Assert.False(_rlv.Permissions.IsAutoAcceptTp(userId2));
            Assert.False(_rlv.Permissions.IsAutoAcceptTp());
        }

        [Fact]
        public void CanChat_Default()
        {
            Assert.True(_rlv.Permissions.CanChat(0, "Hello"));
            Assert.True(_rlv.Permissions.CanChat(0, "/me says Hello"));
            Assert.True(_rlv.Permissions.CanChat(5, "Hello"));
        }

        [Fact]
        public void CanSendIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanSendIM("Hello", userId1));
            Assert.True(_rlv.Permissions.CanSendIM("Hello", userId1, "Group Name"));
        }

        [Fact]
        public void CanStartIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanStartIM(userId1));
        }

        [Fact]
        public void CanReceiveIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.True(_rlv.Permissions.CanReceiveIM("Hello", userId1));
            Assert.True(_rlv.Permissions.CanReceiveIM("Hello", userId1, "Group Name"));
        }
    }
}
