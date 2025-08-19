using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class AdjustHeightCommandTests : RestrictionsBase
    {
        #region @adjustheight:<distance_pelvis_to_foot_in_meters>;<factor>[;delta_in_meters]=force
        [Fact]
        public async Task AdjustHeight()
        {
            _actionCallbacks
                .Setup(e => e.AdjustHeightAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage("@adjustheight:4.3;1.25=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AdjustHeightAsync(4.3f, 1.25f, 0.0f, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AdjustHeight_WithDelta()
        {
            _actionCallbacks
                .Setup(e => e.AdjustHeightAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage("@adjustheight:4.3;1.25;12.34=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.AdjustHeightAsync(4.3f, 1.25f, 12.34f, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion

    }
}
