using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class SitCommandTests : RestrictionsBase
    {
        #region @sit:<uuid>=force
        private void SetObjectExists(Guid objectId)
        {
            _queryCallbacks.Setup(e =>
                e.ObjectExistsAsync(objectId, default)
            ).ReturnsAsync(true);
        }

        private void SetIsSitting(bool isCurrentlySitting)
        {
            _queryCallbacks.Setup(e =>
                e.IsSittingAsync(default)
            ).ReturnsAsync(isCurrentlySitting);
        }


        [Fact]
        public async Task ForceSit_Default()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetObjectExists(objectId1);
            SetIsSitting(false);

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SitAsync(objectId1, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForceSit_RestrictedUnsit_WhileStanding()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetObjectExists(objectId1);
            SetIsSitting(false);

            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SitAsync(objectId1, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForceSit_RestrictedUnsit_WhileSeated()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetObjectExists(objectId1);
            SetIsSitting(true);

            await _rlv.ProcessMessage("@unsit=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.False(await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name));

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task ForceSit_RestrictedSit()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetObjectExists(objectId1);
            SetIsSitting(true);

            await _rlv.ProcessMessage("@sit=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.False(await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name));

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForceSit_RestrictedStandTp()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            SetObjectExists(objectId1);
            SetIsSitting(true);

            await _rlv.ProcessMessage("@standtp=n", _sender.Id, _sender.Name);

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.False(await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name));

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForceSit_InvalidObject()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");
            // SetupSitTarget(objectId1, true); <-- Don't setup sit target for this test

            _actionCallbacks
                .Setup(e => e.SitAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            Assert.False(await _rlv.ProcessMessage($"@sit:{objectId1}=force", _sender.Id, _sender.Name));

            // Assert
            _actionCallbacks.VerifyNoOtherCalls();
        }
        #endregion
    }
}
