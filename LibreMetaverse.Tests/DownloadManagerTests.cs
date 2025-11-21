using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class DownloadManagerTests
    {
        private class FakeHandler : HttpMessageHandler
        {
            private readonly byte[] _data;
            private readonly int _delayMs;
            public int SendCount;

            public FakeHandler(byte[] data, int delayMs = 0)
            {
                _data = data;
                _delayMs = delayMs;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                System.Threading.Interlocked.Increment(ref SendCount);

                if (_delayMs > 0)
                {
                    try { await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false); } catch { }
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_data)
                };
                response.Content.Headers.ContentLength = _data.Length;

                return response;
            }
        }

        [Test]
        public async Task QueueDownloadAsync_SingleDownload_CompletesSuccessfully()
        {
            var bytes = Encoding.UTF8.GetBytes("hello world");
            var handler = new FakeHandler(bytes);
            var fake = new HttpCapsClient(handler);
            var client = new GridClient();
            client.HttpCapsClient = fake;

            using (var dm = new DownloadManager(client))
            {
                var task = dm.QueueDownloadAsync(new Uri("http://example.test/one"), null, null, CancellationToken.None, retries: 1);

                var completed = await Task.WhenAny(task, Task.Delay(5000)).ConfigureAwait(false);
                Assert.That(completed, Is.EqualTo(task), "Download task timed out");

                var tuple = await task.ConfigureAwait(false);
                Assert.That(tuple.data, Is.Not.Null);
                Assert.That(tuple.data, Is.EqualTo(bytes));
                Assert.That(tuple.response, Is.Not.Null);
                Assert.That(tuple.response.IsSuccessStatusCode, Is.True);
            }
        }

        [Test]
        public async Task QueueDownloadAsync_DeduplicatesRequests_SameUri_OneHttpCall()
        {
            var bytes = Encoding.UTF8.GetBytes("duplicate payload");
            // Add a small delay to ensure the first request is still in-flight when the second is queued
            var handler = new FakeHandler(bytes, delayMs: 200);
            var fake = new HttpCapsClient(handler);
            var client = new GridClient();
            client.HttpCapsClient = fake;

            using (var dm = new DownloadManager(client))
            {
                var uri = new Uri("http://example.test/dup");
                var t1 = dm.QueueDownloadAsync(uri, null, null, CancellationToken.None, retries: 1);
                var t2 = dm.QueueDownloadAsync(uri, null, null, CancellationToken.None, retries: 1);

                var all = Task.WhenAll(t1, t2);
                var completed = await Task.WhenAny(all, Task.Delay(5000)).ConfigureAwait(false);
                Assert.That(completed, Is.EqualTo(all), "Downloads did not complete in time");

                var r1 = await t1.ConfigureAwait(false);
                var r2 = await t2.ConfigureAwait(false);

                Assert.That(r1.data, Is.EqualTo(bytes));
                Assert.That(r2.data, Is.EqualTo(bytes));

                // Ensure the underlying Http handler was only invoked once for the same absolute URI
                Assert.That(handler.SendCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void QueueDownloadAsync_Cancellation_PropagatesToTask()
        {
            var bytes = Encoding.UTF8.GetBytes("will cancel");
            var handler = new FakeHandler(bytes);
            var fake = new HttpCapsClient(handler);
            var client = new GridClient();
            client.HttpCapsClient = fake;

            using (var dm = new DownloadManager(client))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel(); // cancel immediately

                var task = dm.QueueDownloadAsync(new Uri("http://example.test/cancel"), null, null, cts.Token, retries: 1);

                // Task should be canceled (registered cancellation sets the TaskCompletionSource)
                Assert.ThrowsAsync<TaskCanceledException>(async () => await task.ConfigureAwait(false));
            }
        }
    }
}
