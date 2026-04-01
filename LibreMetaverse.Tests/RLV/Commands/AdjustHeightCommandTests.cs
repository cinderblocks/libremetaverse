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
    public class AdjustHeightCommandTests : RlvTestBase
    {
        #region @adjustheight:<distance_pelvis_to_foot_in_meters>;<factor>[;delta_in_meters]=force
        [Test]
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

        [Test]
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
