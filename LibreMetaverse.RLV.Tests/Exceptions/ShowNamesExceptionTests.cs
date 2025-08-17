namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class ShowNamesExceptionTests : RestrictionsBase
    {
        #region @shownames:uuid=<rem/add>
        [Fact]
        public async Task CanShowNames_Except()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownames=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownames:{userId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNames(null));
            Assert.True(_rlv.Permissions.CanShowNames(userId1));
            Assert.False(_rlv.Permissions.CanShowNames(userId2));
        }
        #endregion
    }
}
