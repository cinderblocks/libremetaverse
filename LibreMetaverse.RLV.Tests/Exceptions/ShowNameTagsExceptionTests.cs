namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class ShowNameTagsExceptionTests : RestrictionsBase
    {
        #region @shownametags:uuid=<rem/add>
        [Fact]
        public async Task CanShowNameTags_Except()
        {
            var userId1 = new Guid("00000000-0000-4000-8000-000000000000");
            var userId2 = new Guid("11111111-1111-4111-8111-111111111111");

            await _rlv.ProcessMessage("@shownametags=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@shownametags:{userId1}=add", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.CanShowNameTags(null));
            Assert.True(_rlv.Permissions.CanShowNameTags(userId1));
            Assert.False(_rlv.Permissions.CanShowNameTags(userId2));
        }
        #endregion
    }
}
