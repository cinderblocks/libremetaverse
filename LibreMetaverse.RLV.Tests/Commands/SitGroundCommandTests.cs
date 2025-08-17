namespace LibreMetaverse.RLV.Tests.Commands
{
    public class SitGroundCommandTests : RestrictionsBase
    {

        #region @sitground=force

        [Fact]
        public async Task ForceSitGround()
        {
            Assert.True(await _rlv.ProcessMessage("@sitground=force", _sender.Id, _sender.Name));
        }

        [Fact]
        public async Task ForceSitGround_RestrictedSit()
        {
            await _rlv.ProcessMessage("@sit=n", _sender.Id, _sender.Name);

            Assert.False(await _rlv.ProcessMessage("@sitground=force", _sender.Id, _sender.Name));
        }

        #endregion
    }
}
