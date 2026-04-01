using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class SetEnvCommandTests : RlvTestBase
    {

        #region @setenv_<setting>:<value>=force

        [TestCase("Daytime", "Daytime Success")]
        [TestCase("Unknown Setting", "Unknown Setting Success")]
        public async Task SetEnv_Default(string settingName, string settingValue)
        {
            _actionCallbacks
                .Setup(e => e.SetEnvAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@setenv_{settingName}:{settingValue}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetEnvAsync(settingName.ToLower(), settingValue, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        #endregion
    }
}
