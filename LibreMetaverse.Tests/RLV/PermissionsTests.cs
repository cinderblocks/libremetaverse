using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV
{
    [TestFixture]
    public class PermissionsTests : RlvTestBase
    {

        [Test]
        public void CamZoomMin_Default()
        {
            var cameraRestrictions = _rlv.Permissions.GetCameraRestrictions();

            Assert.That(cameraRestrictions.ZoomMin, Is.Null);
        }

        [Test]
        public void CanShowHoverTextWorld_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.True);
        }

        [Test]
        public void CanShowHoverTextHud_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.True);
        }

        [Test]
        public void CanShowHoverText_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.True);
        }


        [Test]
        public void CanShowHoverTextAll_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.World, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanShowHoverText(RlvPermissionsService.HoverTextLocation.Hud, objectId1), Is.True);
        }


        [Test]
        public void CanShowNameTags_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanShowNameTags(null), Is.True);
            Assert.That(_rlv.Permissions.CanShowNameTags(userId1), Is.True);
        }

        [Test]
        public void CanShare_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanShare(null), Is.True);
            Assert.That(_rlv.Permissions.CanShare(userId1), Is.True);
        }

        [Test]
        public void CanShowNames_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanShowNames(null), Is.True);
            Assert.That(_rlv.Permissions.CanShowNames(userId1), Is.True);
        }

        [Test]
        public void CanEdit_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, null), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, null), Is.True);

            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Hud, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.Attached, objectId1), Is.True);
            Assert.That(_rlv.Permissions.CanEdit(RlvPermissionsService.ObjectLocation.RezzedInWorld, objectId1), Is.True);
        }

        [Test]
        public void CanRecvChat_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var userId2 = new Guid("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

            Assert.That(_rlv.Permissions.CanReceiveChat("Hello world", userId1), Is.True);
            Assert.That(_rlv.Permissions.CanReceiveChat("/me says Hello world", userId2), Is.True);
        }

        [Test]
        public void CanAutoAcceptTpRequest_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            Assert.That(_rlv.Permissions.IsAutoAcceptTpRequest(userId1), Is.False);
            Assert.That(_rlv.Permissions.IsAutoAcceptTpRequest(userId2), Is.False);
            Assert.That(_rlv.Permissions.IsAutoAcceptTpRequest(), Is.False);
        }

        [Test]
        public void CanTpRequest_Default()
        {
            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.That(_rlv.Permissions.CanTpRequest(null), Is.True);
            Assert.That(_rlv.Permissions.CanTpRequest(userId1), Is.True);
        }

        [Test]
        public void CanAutoAcceptTp_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId1), Is.False);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(userId2), Is.False);
            Assert.That(_rlv.Permissions.IsAutoAcceptTp(), Is.False);
        }

        [Test]
        public void CanChat_Default()
        {
            Assert.That(_rlv.Permissions.CanChat(0, "Hello"), Is.True);
            Assert.That(_rlv.Permissions.CanChat(0, "/me says Hello"), Is.True);
            Assert.That(_rlv.Permissions.CanChat(5, "Hello"), Is.True);
        }

        [Test]
        public void CanSendIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanSendIM("Hello", userId1), Is.True);
            Assert.That(_rlv.Permissions.CanSendIM("Hello", userId1, "Group Name"), Is.True);
        }

        [Test]
        public void CanStartIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanStartIM(userId1), Is.True);
        }

        [Test]
        public void CanReceiveIM_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            Assert.That(_rlv.Permissions.CanReceiveIM("Hello", userId1), Is.True);
            Assert.That(_rlv.Permissions.CanReceiveIM("Hello", userId1, "Group Name"), Is.True);
        }
    }
}
