using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Threading;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class ReaderWriterLockTests
    {
        [Test]
        public void MultipleReadersOneWriter_ReaderConcurrencyAndWriterExclusion()
        {
            var rw = new OptimisticReaderWriterLock();
            const int readerCount = 5;

            // readersReady: all tasks signal once they are actually running
            var readersReady = new CountdownEvent(readerCount);
            // startGate: released by main thread once all tasks are live, so they all
            // compete for the read lock simultaneously rather than one-at-a-time
            var startGate = new ManualResetEventSlim(false);
            var readersEntered = new CountdownEvent(readerCount);
            var readersExited = new CountdownEvent(readerCount);
            var startRelease = new ManualResetEventSlim(false);

            int currentReaders = 0;
            int maxConcurrentReaders = 0;

            var readerTasks = new Task[readerCount];
            for (int i = 0; i < readerCount; i++)
            {
                readerTasks[i] = Task.Run(() =>
                {
                    readersReady.Signal(); // task is alive and scheduled
                    startGate.Wait();      // wait until all tasks are ready to go

                    using (rw.ReadLock())
                    {
                        var cr = Interlocked.Increment(ref currentReaders);
                        int prevMax;
                        do
                        {
                            prevMax = maxConcurrentReaders;
                            if (cr <= prevMax) break;
                        } while (Interlocked.CompareExchange(ref maxConcurrentReaders, cr, prevMax) != prevMax);

                        readersEntered.Signal();

                        startRelease.Wait();

                        Thread.Sleep(20);

                        Interlocked.Decrement(ref currentReaders);
                        readersExited.Signal();
                    }
                });
            }

            // Wait until every task is actually running before measuring anything
            Assert.That(readersReady.Wait(TimeSpan.FromSeconds(30)), Is.True,
                "All reader tasks should be scheduled within 30s");

            // Release all readers at once so they compete for the read lock simultaneously
            startGate.Set();

            // Wait until all readers have entered the read lock
            Assert.That(readersEntered.Wait(TimeSpan.FromSeconds(30)), Is.True,
                "All readers should enter the read lock within 30s");

            // Start writer — all readers are still inside (blocked on startRelease)
            var writerAcquired = new ManualResetEventSlim(false);
            var writerTask = Task.Run(() =>
            {
                using (rw.WriteLock())
                {
                    writerAcquired.Set();
                }
            });

            Assert.That(writerAcquired.Wait(100), Is.False,
                "Writer should not acquire write lock while readers are active");

            startRelease.Set();

            Assert.That(readersExited.Wait(TimeSpan.FromSeconds(30)), Is.True,
                "Readers should exit within 30s after release");

            Assert.That(writerAcquired.Wait(TimeSpan.FromSeconds(30)), Is.True,
                "Writer should acquire write lock after readers exit");

            Assert.That(maxConcurrentReaders, Is.GreaterThanOrEqualTo(2),
                "At least two readers should have been concurrent");

            Task.WaitAll(readerTasks);
            writerTask.Wait(5000);
        }
    }
}
