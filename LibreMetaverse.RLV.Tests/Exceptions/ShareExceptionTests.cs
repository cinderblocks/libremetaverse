namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class ShareExceptionTests : RestrictionsBase
    {

        #region @share:<UUID>=<rem/add>
        [Fact]
        public async Task CanShare_Except()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@share=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@share:{userId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShare(null));
            Assert.True(_rlv.Permissions.CanShare(userId1));
            Assert.False(_rlv.Permissions.CanShare(userId2));
        }
        #endregion
    }
}
