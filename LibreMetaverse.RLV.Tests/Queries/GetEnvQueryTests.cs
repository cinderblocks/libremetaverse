using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetEnvQueryTests : RestrictionsBase
    {
        #region @getenv_<setting>=<channel_number>

        [Theory]
        [InlineData("Daytime", "Daytime Success")]
        [InlineData("Unknown Setting", "Unknown Setting Success")]
        public async Task GetEnv_Default(string settingName, string settingValue)
        {
            var actual = _actionCallbacks.RecordReplies();

            _queryCallbacks.Setup(e =>
                e.TryGetEnvironmentSettingValueAsync(settingName.ToLower(), default)
            ).ReturnsAsync((true, settingValue));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, settingValue),
            };

            Assert.True(await _rlv.ProcessMessage($"@getenv_{settingName}=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        #endregion

    }
}
