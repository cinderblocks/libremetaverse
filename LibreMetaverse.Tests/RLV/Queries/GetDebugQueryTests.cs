using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class GetDebugQueryTests : RlvTestBase
    {
        #region @getdebug_<setting>=<channel_number>
        [TestCase("RenderResolutionDivisor", "RenderResolutionDivisor Success")]
        [TestCase("Unknown Setting", "Unknown Setting Success")]
        public async Task GetDebug_Default(string settingName, string settingValue)
        {
            var actual = _actionCallbacks.RecordReplies();

            _queryCallbacks.Setup(e =>
                e.TryGetDebugSettingValueAsync(settingName.ToLower(), default)
            ).ReturnsAsync((true, settingValue));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, settingValue),
            };

            Assert.That(await _rlv.ProcessMessageAsync($"@getdebug_{settingName}=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
        #endregion

    }
}
