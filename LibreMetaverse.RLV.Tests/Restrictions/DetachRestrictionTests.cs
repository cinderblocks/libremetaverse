namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class DetachRestrictionTests : RestrictionsBase
    {
        #region @detach=<y/n> |  @detach:<attach_point_name>=<y/n>

        [Fact]
        public void Detach_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");


            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt));

            Assert.True(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId2, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, RlvWearableType.Shirt));
        }

        [Fact]
        public async Task Detach()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");

            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.True(await _rlv.ProcessMessage("@detach=n", objectPrimId2, "objectPrimId2"));

            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt));

            Assert.True(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, null));
            Assert.False(_rlv.Permissions.CanDetach(objectId2, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId2, null, folderId1, true, null, RlvWearableType.Shirt));
        }

        [Fact]
        public async Task Detach_AttachPoint()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var objectPrimId1 = new Guid("00000000-0000-4000-8000-ffffffffffff");

            var objectId2 = new Guid("11111111-1111-4111-8111-111111111111");
            var objectPrimId2 = new Guid("11111111-1111-4111-8111-ffffffffffff");

            var folderId1 = new Guid("99999999-9999-4999-8999-999999999999");

            Assert.True(await _rlv.ProcessMessage("@detach:skull=n", _sender.Id, _sender.Name));

            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Chest, null));
            Assert.False(_rlv.Permissions.CanDetach(objectId1, objectPrimId1, folderId1, false, RlvAttachmentPoint.Skull, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, false, null, RlvWearableType.Shirt));

            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, true, null, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, objectPrimId2, folderId1, true, RlvAttachmentPoint.Chest, null));
            Assert.False(_rlv.Permissions.CanDetach(objectId1, objectPrimId2, folderId1, true, RlvAttachmentPoint.Skull, null));
            Assert.True(_rlv.Permissions.CanDetach(objectId1, null, folderId1, true, null, RlvWearableType.Shirt));
        }

        #endregion

    }
}
