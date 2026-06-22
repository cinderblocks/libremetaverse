using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class TpToCommandTests : RlvTestBase
    {
        #region @tpto:<region_name>/<X_local>/<Y_local>/<Z_local>[;lookat]=force

        [Test]
        public async Task TpTo_Default()
        {
            _actionCallbacks
                .Setup(e => e.TpToAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<float?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessageAsync("@tpto:1.5/2.5/3.5=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.TpToAsync(1.5f, 2.5f, 3.5f, null, null, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task TpTo_WithRegion()
        {
            _actionCallbacks
                .Setup(e => e.TpToAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<float?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessageAsync("@tpto:Region Name/1.5/2.5/3.5=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.TpToAsync(1.5f, 2.5f, 3.5f, "Region Name", null, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task TpTo_WithRegionAndLookAt()
        {
            _actionCallbacks
                .Setup(e => e.TpToAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<float?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessageAsync("@tpto:Region Name/1.5/2.5/3.5;3.1415=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.TpToAsync(1.5f, 2.5f, 3.5f, "Region Name", 3.1415f, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task TpTo_RestrictedUnsit()
        {
            await _rlv.ProcessMessageAsync("@unsit=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.TpToAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<float?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.That(await _rlv.ProcessMessageAsync("@tpto:1.5/2.5/3.5=force", _sender.Id, _sender.Name), Is.False);

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Test]
        public async Task TpTo_RestrictedTpLoc()
        {
            await _rlv.ProcessMessageAsync("@tploc=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.TpToAsync(It.IsAny<float>(), It.IsAny<float>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<float?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.That(await _rlv.ProcessMessageAsync("@tpto:1.5/2.5/3.5=force", _sender.Id, _sender.Name), Is.False);

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }

        #endregion
    }
}
