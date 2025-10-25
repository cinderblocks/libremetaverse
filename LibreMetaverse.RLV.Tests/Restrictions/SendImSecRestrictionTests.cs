namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class SendImSecRestrictionTests : RestrictionsBase
    {

        #region @sendim_sec=<y/n>
        [Fact]
        public async Task CanSendIM_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@sendim_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:{userId2}=add", sender2.Id, sender2.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", userId1));
            Assert.False(_rlv.Permissions.CanSendIM("Hello world", userId2));
        }

        [Fact]
        public async Task CanSendIM_Secure_Group()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));

            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:Group Name=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:allgroups=add", sender2.Id, sender2.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group Name"));
            Assert.False(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Another Group"));
        }

        [Fact]
        public async Task CanSendIM_Secure_AllGroups()
        {
            var groupId1 = new Guid("00000000-0000-4000-8000-000000000000");

            await _rlv.ProcessMessage("@sendim_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@sendim:allgroups=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Group Name"));
            Assert.True(_rlv.Permissions.CanSendIM("Hello world", groupId1, "Another Group"));
        }

        #endregion
    }
}
