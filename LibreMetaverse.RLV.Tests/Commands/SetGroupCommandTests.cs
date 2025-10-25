using Moq;

namespace LibreMetaverse.RLV.Tests.Commands
{
    public class SetGroupCommandTests : RestrictionsBase
    {
        #region @setgroup:<uuid|group_name>[;<role>]=force

        [Fact]
        public async Task SetGroup_ByName()
        {
            _actionCallbacks
                .Setup(e => e.SetGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage("@setgroup:Group Name=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetGroupAsync("Group Name", null, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetGroup_ByNameAndRole()
        {
            _actionCallbacks
                .Setup(e => e.SetGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage("@setgroup:Group Name;Admin Role=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetGroupAsync("Group Name", "Admin Role", It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetGroup_ById()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            _actionCallbacks
                .Setup(e => e.SetGroupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@setgroup:{objectId1}=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetGroupAsync(objectId1, null, It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SetGroup_ByIdAndRole()
        {
            var objectId1 = new Guid("00000000-0000-4000-8000-000000000000");

            _actionCallbacks
                .Setup(e => e.SetGroupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _rlv.ProcessMessage($"@setgroup:{objectId1};Admin Role=force", _sender.Id, _sender.Name);

            // Assert
            _actionCallbacks.Verify(e =>
                e.SetGroupAsync(objectId1, "Admin Role", It.IsAny<CancellationToken>()),
                Times.Once);

            _actionCallbacks.VerifyNoOtherCalls();
        }

        #endregion
    }
}
