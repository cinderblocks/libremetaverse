using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class SetRotCommandTests : RestrictionsBase
    {

        #region @setrot:<angle_in_radians>=force
        [Fact]
        public async Task SetRot()
        {
            _actionCallbacks
                .Setup(e => e.SetRotAsync(It.IsAny<float>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage("@setrot:1.5=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetRotAsync(1.5f, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
