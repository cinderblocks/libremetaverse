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

        // AgentMovementComplete from the new simulator can be handled while Network.Connect is
        // still running, before the state machine reaches WaitingForComplete. The crossing must
        // still complete instead of waiting for a packet that has already come and gone.
        [Test]
        public async Task RegionCrossing_MovementCompleteWhileConnecting_CompletesCrossing()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var agent = _client.Self;
            var agentType = typeof(AgentManager);
            var infoType = agentType.GetNestedType("CrossingInfo", BindingFlags.NonPublic)!;
            var stateType = agentType.GetNestedType("CrossingState", BindingFlags.Public)!;
            var stateField = infoType.GetField("State", flags)!;

            var sim = new Simulator(_client, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 13000), 1UL);
            var crossing = Activator.CreateInstance(infoType, nonPublic: true)!;
            infoType.GetField("NewSimulator", flags)!.SetValue(crossing, sim);
            infoType.GetField("StartTime", flags)!.SetValue(crossing, DateTime.UtcNow);
            stateField.SetValue(crossing, Enum.Parse(stateType, "Connecting"));
            agentType.GetField("_currentCrossing", flags)!.SetValue(agent, crossing);

            agentType.GetMethod("NotifyMovementComplete", flags)!.Invoke(agent, new object[] { sim });
            Assert.That(stateField.GetValue(crossing)!.ToString(), Is.EqualTo("Connecting"),
                "An early MovementComplete must not change state by itself");

            var entered = (Task<bool>)agentType.GetMethod("EnterWaitingForCompleteAsync", flags)!
                .Invoke(agent, new[] { crossing, sim, (object)CancellationToken.None })!;
            Assert.That(await entered, Is.True);

            // A completed crossing is reset to Idle by OnCrossingCompleted.
            Assert.That(stateField.GetValue(crossing)!.ToString(), Is.EqualTo("Idle"));
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
