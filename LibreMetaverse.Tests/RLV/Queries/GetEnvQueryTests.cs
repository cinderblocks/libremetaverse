using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Queries
{
    [TestFixture]
    public class GetEnvQueryTests : RlvTestBase
    {
        #region @getenv_<setting>=<channel_number>

        [TestCase("Daytime", "Daytime Success")]
        [TestCase("Unknown Setting", "Unknown Setting Success")]
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

            Assert.That(await _rlv.ProcessMessage($"@getenv_{settingName}=1234", _sender.Id, _sender.Name), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        #endregion

    }
}
