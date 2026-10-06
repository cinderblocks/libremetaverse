#if !NET481
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Objects choose the channels RLV replies, notifications and redirected chat are sent on. Channel 0 is
    /// public chat, so nothing an object asks for may end up there.
    /// </summary>
    [TestFixture]
    public class RlvChannelTests
    {
        private sealed class RecordingActions : RlvActionCallbacksDefault
        {
            public readonly List<(int Channel, string Message)> Replies = new List<(int, string)>();

            public override Task SendReplyAsync(int channel, string message, CancellationToken cancellationToken)
            {
                Replies.Add((channel, message));
                return Task.CompletedTask;
            }
        }

        private static readonly Guid Object = Guid.NewGuid();

        private static (RlvService Service, RecordingActions Actions) NewService()
        {
            var actions = new RecordingActions();
            return (new RlvService(new RlvCallbacksDefault(), actions, enabled: true), actions);
        }

        private static Task<bool> Send(RlvService service, string command) =>
            service.ProcessMessageAsync(command, Object, "Test Object");

        [Test]
        public async Task Notify_OnAnotherChannel_ReportsThere()
        {
            var (service, actions) = NewService();

            Assert.That(await Send(service, "@notify:5=add"), Is.True);
            await service.ReportSitAsync(Guid.NewGuid());

            Assert.That(actions.Replies, Is.Not.Empty);
            Assert.That(actions.Replies.TrueForAll(r => r.Channel == 5));
        }

        [Test]
        public async Task Notify_OnPublicChat_IsRefused()
        {
            var (service, actions) = NewService();

            Assert.That(await Send(service, "@notify:0=add"), Is.False);
            await service.ReportSitAsync(Guid.NewGuid());

            Assert.That(actions.Replies, Is.Empty);
        }

        [TestCase("redirchat")]
        [TestCase("rediremote")]
        public async Task Redirect_ToAnotherChannel_Redirects(string behavior)
        {
            var (service, actions) = NewService();
            var message = behavior == "rediremote" ? "/me waves" : "hello";

            Assert.That(await Send(service, $"@{behavior}:7=add"), Is.True);
            await service.ReportSendPublicMessageAsync(message);

            Assert.That(actions.Replies, Is.EqualTo(new[] { (7, message) }));
        }

        [TestCase("redirchat")]
        [TestCase("rediremote")]
        public async Task Redirect_ToPublicChat_IsRefused(string behavior)
        {
            var (service, actions) = NewService();
            var message = behavior == "rediremote" ? "/me waves" : "hello";

            Assert.That(await Send(service, $"@{behavior}:0=add"), Is.False);
            await service.ReportSendPublicMessageAsync(message);

            Assert.That(actions.Replies, Is.Empty, "the user's own chat must not be sent a second time");
        }

        [Test]
        public async Task GetCommand_OnPublicChat_IsRefused()
        {
            var (service, actions) = NewService();

            Assert.That(await Send(service, "@version=0"), Is.False);

            Assert.That(actions.Replies, Is.Empty);
        }

        [Test]
        public async Task BlacklistedCommand_DoesNotAnswerOnPublicChat()
        {
            var (service, actions) = NewService();
            service.Blacklist.BlacklistBehavior("version");

            Assert.That(await Send(service, "@version=0"), Is.False);
            Assert.That(await Send(service, "@version=9"), Is.False);

            Assert.That(actions.Replies, Is.EqualTo(new[] { (9, "") }));
        }
    }
}
#endif
