namespace LibreMetaverse.RLV.Tests.Exceptions
{
    public class RedirEmoteExceptionTests : RestrictionsBase
    {
        #region @rediremote:<channel_number>=<rem/add>
        [Fact]
        public async Task IsRedirEmote()
        {
            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels));

            var expected = new List<int>
            {
                1234,
            };

            Assert.Equal(expected, channels);
        }

        [Fact]
        public async Task IsRedirEmote_Removed()
        {
            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@rediremote:1234=rem", _sender.Id, _sender.Name);

            Assert.False(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels));
        }

        [Fact]
        public async Task IsRedirEmote_MultipleChannels()
        {
            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@rediremote:12345=add", _sender.Id, _sender.Name);

            Assert.True(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels));

            var expected = new List<int>
            {
                1234,
                12345,
            };

            Assert.Equal(expected, channels);
        }

        [Fact]
        public async Task IsRedirEmote_RedirectEmote()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessage("/me says Hello World");

            Assert.True(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels));
            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/me says Hello World"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsRedirEmote_RedirectEmoteMultiple()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@rediremote:5678=n", _sender.Id, _sender.Name);

            await _rlv.ReportSendPublicMessage("/me says Hello World");
            _rlv.Permissions.TryGetRedirEmoteChannels(out var channels);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/me says Hello World"),
                (5678, "/me says Hello World"),
            };

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task IsRedirEmote_RedirectEmoteChat()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessage("Hello World");

            Assert.True(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels));
            Assert.Empty(actual);
        }

        #endregion

    }
}
