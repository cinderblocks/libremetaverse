namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class SendImExceptionTests : RestrictionsBase
    {
        #region @sendim:<UUID_or_group_name>=<rem/add>
        [Fact]
        public async Task CanSendIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", userId1));
        }

        [Fact]
        public async Task CanSendIM_Exception_SingleGroup()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:Group Name=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group Name"));
        }

        [Fact]
        public async Task CanSendIM_Exception_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:allgroups=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group name"));
        }

        #endregion
    }
}
