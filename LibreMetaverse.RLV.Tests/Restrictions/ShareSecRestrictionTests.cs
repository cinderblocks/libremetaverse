namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShareSecRestrictionTests : RestrictionsBase
    {
        #region @share_sec=<y/n>
        [Fact]
        public async Task CanShare_Secure_Default()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@share_sec=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShare(null));
            Assert.False(_rlv.Permissions.CanShare(userId1));
            Assert.False(_rlv.Permissions.CanShare(userId2));
        }

        [Fact]
        public async Task CanShare_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@share_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@share:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@share:{userId2}=add", sender2.Id, sender2.Name);

            Assert.False(_rlv.Permissions.CanShare(null));
            Assert.True(_rlv.Permissions.CanShare(userId1));
            Assert.False(_rlv.Permissions.CanShare(userId2));
        }

        #endregion
    }
}
