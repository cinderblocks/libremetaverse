namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class StartImExceptionTests : RestrictionsBase
    {
        #region @startim:<UUID>=<rem/add>
        [Fact]
        public async Task CanStartIM_Exception()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@startim=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@startim:{userId1}=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanStartIM(userId1));
            Assert.False(_rlv.Permissions.CanStartIM(userId2));
        }
        #endregion
    }
}
