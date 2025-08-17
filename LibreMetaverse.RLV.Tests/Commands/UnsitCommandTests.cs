namespace LibreMetaverse.RLV.Tests.Commands
{
    public class UnsitCommandTests : RestrictionsBase
    {
        #region @unsit=force

        [Fact]
        public async Task ForceUnSit()
        {
            Assert.True(await _rlv.ProcessMessage("@unsit=force", _sender.Id, _sender.Name));
        }

        [Fact]
        public async Task ForceUnSit_RestrictedUnsit()
        {
            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);

            Assert.False(await _rlv.ProcessMessage("@unsit=force", _sender.Id, _sender.Name));
        }

        #endregion
    }
}
