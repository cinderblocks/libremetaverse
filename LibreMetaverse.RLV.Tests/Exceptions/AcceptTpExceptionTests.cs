namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class AcceptTpExceptionTests : RestrictionsBase
    {
        #region @accepttp[:<UUID>]=<rem/add>

        [Fact]
        public async Task CanAutoAcceptTp_User()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttp:{userId1}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.IsAutoAcceptTp(userId1));
            Assert.False(_rlv.Permissions.IsAutoAcceptTp(userId2));
            Assert.False(_rlv.Permissions.IsAutoAcceptTp());
        }

        [Fact]
        public async Task CanAutoAcceptTp_All()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttp=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.IsAutoAcceptTp(userId1));
            Assert.True(_rlv.Permissions.IsAutoAcceptTp(userId2));
            Assert.True(_rlv.Permissions.IsAutoAcceptTp());
        }

        #endregion
    }
}
