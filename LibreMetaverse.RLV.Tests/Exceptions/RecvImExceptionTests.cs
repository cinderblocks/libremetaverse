namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class RecvImExceptionTests : RestrictionsBase
    {
        #region @recvim:<UUID_or_group_name>=<rem/add>
        [Fact]
        public async Task CanReceiveIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanReceiveIM("Hello world", userId1));
        }


        [Fact]
        public async Task CanReceiveIM_Exception_SingleGroup()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:Group Name=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "Group Name"));
        }

        [Fact]
        public async Task CanReceiveIM_Exception_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@recvim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@recvim:allgroups=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanReceiveIM("Hello world", groupId1, "Group name"));
        }

        #endregion
    }
}
