using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class RedirEmoteExceptionTests : RlvTestBase
    {
        #region @rediremote:<channel_number>=<rem/add>
        [Test]
        public async Task IsRedirEmote()
        {
            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels), Is.True);

            var expected = new List<int>
            {
                1234,
            };

            Assert.That(channels, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirEmote_Removed()
        {
            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@rediremote:1234=rem", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels), Is.False);
        }

        [Test]
        public async Task IsRedirEmote_MultipleChannels()
        {
            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@rediremote:12345=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels), Is.True);

            var expected = new List<int>
            {
                1234,
                12345,
            };

            Assert.That(channels, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirEmote_RedirectEmote()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessageAsync("/me says Hello World");

            Assert.That(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels), Is.True);
            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/me says Hello World"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirEmote_RedirectEmoteMultiple()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessageAsync("@rediremote:5678=n", _sender.Id, _sender.Name);

            await _rlv.ReportSendPublicMessageAsync("/me says Hello World");
            _rlv.Permissions.TryGetRedirEmoteChannels(out var channels);

            var expected = new List<(int Channel, string Text)>
            {
                (1234, "/me says Hello World"),
                (5678, "/me says Hello World"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirEmote_RedirectEmoteChat()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessageAsync("@rediremote:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessageAsync("Hello World");

            Assert.That(_rlv.Permissions.TryGetRedirEmoteChannels(out var channels), Is.True);
            Assert.That(actual, Is.Empty);
        }

        #endregion

    }
}
