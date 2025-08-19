namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class AcceptTpRequestExceptionTests : RestrictionsBase
    {
        #region @accepttprequest[:<UUID>]=<rem/add>

        [Fact]
        public async Task CanAutoAcceptTpRequest_User()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttprequest:{userId1}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.IsAutoAcceptTpRequest(userId1));
            Assert.False(_rlv.Permissions.IsAutoAcceptTpRequest(userId2));
            Assert.False(_rlv.Permissions.IsAutoAcceptTpRequest());
        }

        [Fact]
        public async Task CanAutoAcceptTpRequest_All()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage($"@accepttprequest=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.IsAutoAcceptTpRequest(userId1));
            Assert.True(_rlv.Permissions.IsAutoAcceptTpRequest(userId2));
            Assert.True(_rlv.Permissions.IsAutoAcceptTpRequest());
        }

        #endregion
    }
}
