namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class TpLocalRestrictionTests : RestrictionsBase
    {
        #region @tplocal[:max_distance]=<y/n>
        [Fact]
        public async Task CanTpLocal_Default()
        {
            await _rlv.ProcessMessage("@TpLocal=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanTpLocal(out var distance));
            Assert.Equal(0.0f, distance, FloatTolerance);
        }

        [Fact]
        public async Task CanTpLocal()
        {
            await _rlv.ProcessMessage("@TpLocal:0.9=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.CanTpLocal(out var distance));
            Assert.Equal(0.9f, distance, FloatTolerance);
        }
        #endregion
    }
}
