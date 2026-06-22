using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class SetDebugCommandTests : RlvTestBase
    {

        #region @setdebug_<setting>:<value>=force
        [TestCase("RenderResolutionDivisor", "RenderResolutionDivisor Success")]
        [TestCase("Unknown Setting", "Unknown Setting Success")]
        public async Task SetDebug_Default(string settingName, string settingValue)
        {
            _actionCallbacks
                .Setup(e => e.SetDebugAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessageAsync($"@setdebug_{settingName}:{settingValue}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetDebugAsync(settingName.ToLower(), settingValue, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task SetDebug_Invalid()
        {
            _actionCallbacks
                .Setup(e => e.SetDebugAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.That(await _rlv.ProcessMessageAsync($"@setdebug_:42=force", _sender.Id, _sender.Name), Is.False);

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
