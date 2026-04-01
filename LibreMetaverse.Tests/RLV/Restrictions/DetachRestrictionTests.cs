using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Restrictions
{
    [TestFixture]
    public class DetachRestrictionTests : RlvTestBase
    {
        #region @detach=<y/n> |  @detach:<attach_point_name>=<y/n>

        [Test]
        public void Detach_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");


            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId2, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, RlvWearableType.Shirt), Is.True);
        }

        [Test]
        public async Task Detach()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");

            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.That(await _rlv.ProcessMessage("@detach=n", objectPrimId2, "objectPrimId2"), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId2, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, RlvWearableType.Shirt), Is.True);
        }

        [Test]
        public async Task Detach_AttachPoint()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");

            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.That(await _rlv.ProcessMessage("@detach:skull=n", _sender.Id, _sender.Name), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Skull, null), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt), Is.True);

            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, true, null, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null), Is.True);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, objectPrimId2, folderId1, true, RlvAttachmentPoint.Skull, null), Is.False);
            Assert.That(_rlv.Permissions.CanDetach(objectId1, null, folderId1, true, null, RlvWearableType.Shirt), Is.True);
        }

        #endregion

    }
}
