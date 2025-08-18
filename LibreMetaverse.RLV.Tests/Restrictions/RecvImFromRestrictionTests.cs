namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class RecvImFromRestrictionTests : RestrictionsBase
    {
        #region @recvimfrom:<UUID_or_group_name>=<y/n>
        [Fact]
        public async Task CanReceiveIMFrom()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:{userId1}=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanReceiveIM("Hello world", userId1));
            Assert.True(_rlv.Permissions.CanReceiveIM("Hello world", userId2));
        }

        [Fact]
        public async Task CanReceiveIMFrom_Group()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:First Group=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "First Group"));
            Assert.True(_rlv.Permissions.CanReceiveIM("Hello world", groupId2, "Second Group"));
        }

        [Fact]
        public async Task CanReceiveIMTo_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@recvimfrom:allgroups=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "First Group"));
            Assert.False(_rlv.Permissions.CanReceiveIM("Hello world", groupId2, "Second Group"));
        }
        #endregion
    }
}
