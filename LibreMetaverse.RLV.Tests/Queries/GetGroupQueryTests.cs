using Moq;

namespace LibreMetaverse.RLV.Tests.Queries
{
    public class GetGroupQueryTests : RestrictionsBase
    {
        #region @getgroup=<channel_number>

        [Fact]
        public async Task GetGroup_Default()
        {
            var actual = _actionCallbacks.RecordReplies();
            var actualGroupName = "Group Name";

            _queryCallbacks.Setup(e =>
                e.TryGetActiveGroupNameAsync(default)
            ).ReturnsAsync((true, actualGroupName));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, actualGroupName),
            };

            Assert.True(await _rlv.ProcessMessage("@getgroup=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task GetGroup_NoGroup()
        {
            var actual = _actionCallbacks.RecordReplies();
            var actualGroupName = "";

            _queryCallbacks.Setup(e =>
                e.TryGetActiveGroupNameAsync(default)
            ).ReturnsAsync((false, actualGroupName));

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "none"),
            };

            Assert.True(await _rlv.ProcessMessage("@getgroup=1234", _sender.Id, _sender.Name));
            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
