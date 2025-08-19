namespace LibreMetaverse.RLV.Tests.Restrictions
{
    public class FarTouchRestrictionTests : RestrictionsBase
    {
        #region  @touchfar @fartouch[:max_distance]=<y/n>

        [Theory]
        [InlineData("fartouch")]
        [InlineData("touchfar")]
        public async Task CanFarTouch(string command)
        {
            await _rlv.ProcessMessage($"@{command}:0.9=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance));
            Assert.Equal(0.9f, distance);
        }

        [Theory]
        [InlineData("fartouch")]
        [InlineData("touchfar")]
        public async Task CanFarTouch_Synonym(string command)
        {
            await _rlv.ProcessMessage($"@{command}:0.9=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance));
            Assert.Equal(0.9f, distance);
        }

        [Theory]
        [InlineData("fartouch")]
        [InlineData("touchfar")]
        public async Task CanFarTouch_Default(string command)
        {
            await _rlv.ProcessMessage($"@{command}=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetMaxFarTouchDistance(out var distance));
            Assert.Equal(1.5f, distance);
        }

        [Theory]
        [InlineData("fartouch", "fartouch")]
        [InlineData("fartouch", "touchfar")]
        [InlineData("touchfar", "touchfar")]
        [InlineData("touchfar", "fartouch")]
        public async Task CanFarTouch_Multiple_Synonyms(string command1, string command2)
        {
            await _rlv.ProcessMessage($"@{command1}:12.34=n", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage($"@{command2}:6.78=n", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetMaxFarTouchDistance(out var actualDistance2));

            await _rlv.ProcessMessage($"@{command1}:6.78=y", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetMaxFarTouchDistance(out var actualDistance1));

            Assert.Equal(12.34f, actualDistance1, FloatTolerance);
            Assert.Equal(6.78f, actualDistance2, FloatTolerance);
        }

        #endregion

    }
}
