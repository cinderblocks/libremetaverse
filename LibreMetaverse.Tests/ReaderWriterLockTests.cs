using NUnit.Framework;
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

            var readersEntered = new CountdownEvent(readerCount);
            var readersExited = new CountdownEvent(readerCount);
            var startRelease = new ManualResetEventSlim(false);

            int currentReaders = 0;
            int maxConcurrentReaders = 0;

            // Start readers
            var readerTasks = new Task[readerCount];
            for (int i = 0; i < readerCount; i++)
            {
                readerTasks[i] = Task.Run(() =>
                {
                    using (rw.ReadLock())
                    {
                        var cr = Interlocked.Increment(ref currentReaders);
                        // update max
                        int prevMax;
                        do
                        {
                            prevMax = maxConcurrentReaders;
                            if (cr <= prevMax) break;
                        } while (Interlocked.CompareExchange(ref maxConcurrentReaders, cr, prevMax) != prevMax);

                        readersEntered.Signal();

                        // wait until main thread allows readers to exit
                        startRelease.Wait();

                        // small work to keep lock held briefly
                        Thread.Sleep(20);

                        Interlocked.Decrement(ref currentReaders);
                        readersExited.Signal();
                    }
                });
            }

            // Wait until all readers have entered
            Assert.That(readersEntered.Wait(1000), Is.True, "All readers should enter the read lock within 1s");

            // Start writer which should block until readers release
            var writerAcquired = new ManualResetEventSlim(false);
            var writerTask = Task.Run(() =>
            {
                using (rw.WriteLock())
                {
                    writerAcquired.Set();
                }
            });

            // Writer should not be able to acquire while readers haven't been released
            Assert.That(writerAcquired.Wait(100), Is.False, "Writer should not acquire write lock while readers are active");

            // Allow readers to exit
            startRelease.Set();

            // Wait for readers to fully exit
            Assert.That(readersExited.Wait(1000), Is.True, "Readers should exit within 1s after release");

            // Now writer should be able to acquire
            Assert.That(writerAcquired.Wait(1000), Is.True, "Writer should acquire write lock after readers exit");

            // Ensure we had concurrent readers
            Assert.That(maxConcurrentReaders, Is.GreaterThanOrEqualTo(2), "At least two readers should have been concurrent");

            // Cleanup
            Task.WaitAll(readerTasks);
            writerTask.Wait(1000);
        }
    }
}
