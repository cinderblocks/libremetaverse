namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TpRequestRestrictionTests : RestrictionsBase
    {
        #region @tprequest=<y/n>

        [Fact]
        public async Task CanTpRequest()
        {
            await _rlv.ProcessMessage("@tprequest=n", _sender.Id, _sender.Name);

            var userId1 = new Guid("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

            Assert.False(_rlv.Permissions.CanTpRequest(null));
            Assert.False(_rlv.Permissions.CanTpRequest(userId1));
        }

        [Fact]
        public async Task CanTpRequest_Except()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@tprequest=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@tprequest:{userId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanTpRequest(null));
            Assert.True(_rlv.Permissions.CanTpRequest(userId1));
            Assert.False(_rlv.Permissions.CanTpRequest(userId2));
        }
        #endregion
    }
}
