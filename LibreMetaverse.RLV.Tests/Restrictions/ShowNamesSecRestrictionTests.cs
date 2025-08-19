namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class ShowNamesSecRestrictionTests : RestrictionsBase
    {
        #region  @shownames_sec[:except_uuid]=<y/n>
        [Fact]
        public async Task CanShowNames_Secure_Default()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNames(null));
            Assert.False(_rlv.Permissions.CanShowNames(userId1));
            Assert.False(_rlv.Permissions.CanShowNames(userId2));
        }

        [Fact]
        public async Task CanShowNames_Secure()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("22222222-2222-4222-8222-222222222222"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames:{userId1}=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames:{userId2}=add", sender2.Id, sender2.Name);

            Assert.False(_rlv.Permissions.CanShowNames(null));
            Assert.True(_rlv.Permissions.CanShowNames(userId1));
            Assert.False(_rlv.Permissions.CanShowNames(userId2));
        }

        [Fact]
        public async Task CanShowNames_Secure_Except()
        {
            var sender2 = new RlvObject("Sender 2", new Guid("22222222-2222-4222-8222-222222222222"));
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames_sec=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames_sec:{userId1}=n", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNames(null));
            Assert.True(_rlv.Permissions.CanShowNames(userId1));
            Assert.False(_rlv.Permissions.CanShowNames(userId2));
        }

        #endregion
    }
}
