namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class RedirChatExceptionTests : RestrictionsBase
    {
        #region @redirchat:<channel_number>=<rem/add>

        [Fact]
        public async Task IsRedirChat()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetRedirChatChannels(out var channels));

            var expected = new List<int>
            {
                1234,
            };

            Assert.Equal(expected, channels);
        }

        [Fact]
        public async Task IsRedirChat_Removed()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@redirchat:1234=rem", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.TryGetRedirChatChannels(out var channels));
        }

        [Fact]
        public async Task IsRedirChat_MultipleChannels()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@redirchat:12345=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetRedirChatChannels(out var channels));

            var expected = new List<int>
            {
                1234,
                12345,
            };

            Assert.Equal(expected, channels);
        }

        [Fact]
        public async Task IsRedirChat_RedirectChat()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessage("Hello World");

            Assert.True(_rlv.Permissions.TryGetRedirChatChannels(out var channels));
            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Hello World"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsRedirChat_RedirectChatMultiple()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@redirchat:5678=add", _sender.Id, _sender.Name);

            await _rlv.ReportSendPublicMessage("Hello World");
            _rlv.Permissions.TryGetRedirChatChannels(out var channels);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Hello World"),
                (5678, "Hello World"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsRedirChat_RedirectChatEmote()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportSendPublicMessage("/me says Hello World");

            Assert.True(_rlv.Permissions.TryGetRedirChatChannels(out var channels));
            Assert.Empty(actual);
        }

        #endregion

    }
}
