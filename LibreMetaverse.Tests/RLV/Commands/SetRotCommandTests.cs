using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace LibreMetaverse.Tests.RLV.Commands
{
    [TestFixture]
    public class SetRotCommandTests : RlvTestBase
    {

        #region @setrot:<angle_in_radians>=force
        [Test]
        public async Task SetRot()
        {
            _actionCallbacks
                .Setup(e => e.SetRotAsync(It.IsAny<float>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessageAsync("@setrot:1.5=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetRotAsync(1.5f, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
