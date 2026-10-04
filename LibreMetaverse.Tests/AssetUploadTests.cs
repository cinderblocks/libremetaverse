using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class AssetUploadTests
    {
        private GridClient _client;

        [SetUp]
        public void SetUp() => _client = new GridClient();

        [TearDown]
        public void TearDown() => _client.Dispose();

        private AssetUpload PendingUpload()
        {
            var field = typeof(AssetManager).GetField("PendingUpload", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (AssetUpload)field.GetValue(_client.Assets);
        }

        private async Task<AssetUpload> WaitForPendingUpload(UUID transactionID)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var pending = PendingUpload();
                if (pending != null && pending.ID == transactionID) return pending;
                await Task.Delay(5);
            }
            Assert.Fail("Upload never became pending");
            return null;
        }

        [Test]
        public async Task AssetUpload_CancelledByCaller_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            var transactionID = UUID.Random();
            var upload = _client.Assets.RequestUploadAsync(AssetType.Notecard, new byte[16], true, transactionID, cts.Token);

            await WaitForPendingUpload(transactionID);
            cts.Cancel();

            // Must be reported as a cancellation rather than as the upload timing out
            Assert.That(async () => await upload, Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public async Task BakedTextureUpload_UdpFallback_DoesNotBlockTheCaller()
        {
            // No simulator or capabilities, so the upload takes the UDP fallback path and
            // would wait for a confirmation that never arrives.
            using var cts = new CancellationTokenSource();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var upload = _client.Assets.RequestUploadBakedTextureAsync(new byte[16], cts.Token);

            // The call used to block here until the upload timed out
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "Call blocked instead of returning a task");
            Assert.That(upload.IsCompleted, Is.False);

            cts.Cancel();

            var completed = await Task.WhenAny(upload, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.That(completed, Is.SameAs(upload), "Cancelling did not end the upload");
            Assert.That(await upload, Is.EqualTo(UUID.Zero));
        }

        [Test]
        public async Task AssetUpload_Confirmed_DoesNotHoldThreadPoolThreads()
        {
            // A token that can be cancelled but never is, as an application-wide token would be.
            using var cts = new CancellationTokenSource();

            // Far more uploads than the pool's minimum thread count, so that if each one parked a
            // pool thread the pool would be starved.
            var uploads = Math.Max(16, Environment.ProcessorCount * 4);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < uploads; i++)
            {
                var transactionID = UUID.Random();
                var upload = _client.Assets.RequestUploadAsync(AssetType.Notecard, new byte[16], true, transactionID, cts.Token);

                var pending = await WaitForPendingUpload(transactionID);
                pending.ConfirmTcs.TrySetResult(true);

                Assert.That(await upload, Is.EqualTo(transactionID));
            }

            // Each upload completes in milliseconds. A parked pool thread per upload starves the
            // pool, and the pool only adds threads slowly, so the loop takes many seconds instead.
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                "Uploads were slowed by thread pool starvation");

            var probe = Task.Run(() => 42);
            Assert.That(await Task.WhenAny(probe, Task.Delay(TimeSpan.FromSeconds(2))), Is.SameAs(probe),
                "Thread pool is starved: confirmed uploads are still holding threads");
        }
    }
}
