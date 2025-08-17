namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendImToRestrictionTests : RestrictionsBase
    {
        #region @sendimto:<UUID_or_group_name>=<y/n>

        [Fact]
        public async Task CanSendIMTo()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@sendimto:{userId1}=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanSendIM("Hello world", userId1));
            Assert.True(_rlv.Permissions.CanSendIM("Hello world", userId2));
        }

        [Fact]
        public async Task CanSendIMTo_Group()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@sendimto:First Group=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanSendIM("Hello world", groupId1, "First Group"));
            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId2, "Second Group"));
        }

        [Fact]
        public async Task CanSendIMTo_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var groupId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@sendimto:allgroups=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanSendIM("Hello world", groupId1, "First Group"));
            Assert.False(_rlv.Permissions.CanSendIM("Hello world", groupId2, "Second Group"));
        }

        #endregion
    }
}
