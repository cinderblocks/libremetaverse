using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class RegionCrossingFaultLoggingTests
    {
        private GridClient _client;

        [SetUp]
        public void SetUp() => _client = new GridClient();

        [TearDown]
        public void TearDown() => _client.Dispose();

        private static void LogCrossingFaults(AgentManager agent, Task task, string stage) =>
            typeof(AgentManager).GetMethod("LogCrossingFaults", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(agent, new object[] { task, stage });

        [Test]
        public void RegionCrossing_FaultedDetachedTask_IsLogged()
        {
            var stage = "test-" + Guid.NewGuid().ToString("N");
            var messages = new BlockingCollection<string>();
            void OnLog(object message, LogLevel level) => messages.Add(message.ToString());
            Logger.OnLogMessage += OnLog;
            try
            {
                LogCrossingFaults(_client.Self, Task.FromException(new InvalidOperationException("boom")), stage);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                string found = null;
                while (found == null && DateTime.UtcNow < deadline)
                {
                    if (messages.TryTake(out var message, 100) && message.Contains(stage)) found = message;
                }

                Assert.That(found, Is.Not.Null, "Faulted crossing task was not logged");
                Assert.That(found, Does.Contain("boom"));
            }
            finally
            {
                Logger.OnLogMessage -= OnLog;
            }
        }

        [Test]
        public void RegionCrossing_CancelledDetachedTask_IsNotLogged()
        {
            var stage = "test-" + Guid.NewGuid().ToString("N");
            var messages = new BlockingCollection<string>();
            void OnLog(object message, LogLevel level) => messages.Add(message.ToString());
            Logger.OnLogMessage += OnLog;
            try
            {
                LogCrossingFaults(_client.Self, Task.FromCanceled(new CancellationToken(true)), stage);

                var deadline = DateTime.UtcNow.AddMilliseconds(500);
                while (DateTime.UtcNow < deadline)
                {
                    if (messages.TryTake(out var message, 50))
                        Assert.That(message, Does.Not.Contain(stage), "Cancellation should not be logged as an error");
                }
            }
            finally
            {
                Logger.OnLogMessage -= OnLog;
            }
        }
    }
}
