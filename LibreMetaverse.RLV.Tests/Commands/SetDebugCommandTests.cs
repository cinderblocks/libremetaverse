using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class SetDebugCommandTests : RestrictionsBase
    {

        #region @setdebug_<setting>:<value>=force
        [Theory]
        [InlineData("RenderResolutionDivisor", "RenderResolutionDivisor Success")]
        [InlineData("Unknown Setting", "Unknown Setting Success")]
        public async Task SetDebug_Default(string settingName, string settingValue)
        {
            _actionCallbacks
                .Setup(e => e.SetDebugAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@setdebug_{settingName}:{settingValue}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetDebugAsync(settingName.ToLower(), settingValue, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetDebug_Invalid()
        {
            _actionCallbacks
                .Setup(e => e.SetDebugAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.False(await _rlv.ProcessMessage($"@setdebug_:42=force", _sender.Id, _sender.Name));

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
