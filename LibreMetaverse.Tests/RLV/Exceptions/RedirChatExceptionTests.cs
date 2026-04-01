using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using Moq;
using NUnit.Framework;
namespace LibreMetaverse.Tests.RLV.Exceptions
{
    [TestFixture]
    public class RedirChatExceptionTests : RlvTestBase
    {
        #region @redirchat:<channel_number>=<rem/add>

        [Test]
        public async Task IsRedirChat()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirChatChannels(out var channels), Is.True);

            var expected = new List<int>
            {
                1234,
            };

            Assert.That(channels, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirChat_Removed()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@redirchat:1234=rem", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirChatChannels(out var channels), Is.False);
        }

        [Test]
        public async Task IsRedirChat_MultipleChannels()
        {
            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ProcessMessage("@redirchat:12345=add", _sender.Id, _sender.Name);

            Assert.That(_rlv.Permissions.TryGetRedirChatChannels(out var channels), Is.True);

            var expected = new List<int>
            {
                1234,
                12345,
            };

            Assert.That(channels, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirChat_RedirectChat()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);
            await _rlv.ReportSendPublicMessage("Hello World");

            Assert.That(_rlv.Permissions.TryGetRedirChatChannels(out var channels), Is.True);
            var expected = new List<(int Channel, string Text)>
            {
                (1234, "Hello World"),
            };

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
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

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task IsRedirChat_RedirectChatEmote()
        {
            var actual = _actionCallbacks.RecordReplies();

            await _rlv.ProcessMessage("@redirchat:1234=add", _sender.Id, _sender.Name);

            await _rlv.ReportSendPublicMessage("/me says Hello World");

            Assert.That(_rlv.Permissions.TryGetRedirChatChannels(out var channels), Is.True);
            Assert.That(actual, Is.Empty);
        }

        #endregion

    }
}
