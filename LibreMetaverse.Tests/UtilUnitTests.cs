using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using LibreMetaverse;
using OpenMetaverse;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Utilities")]
    public class UtilUnitTests
    {
        [Test]
        public void FileHelper_SanitizeAndSafeNames()
        {
            string original = "inva:lid/fi*le?.txt";
            string safe = FileHelper.SafeFileName(original);
            Assert.That(safe, Is.Not.Null.And.Not.Empty);
            Assert.That(safe, Does.Not.Contain(":"));
            Assert.That(FileHelper.SafeDirName("path|with?bad"), Does.Not.Contain("|"));

            char[] invalid = new[] { 'a', 'b' };
            string san = FileHelper.Sanitize("abcxyz", invalid, '-');
            Assert.That(san, Is.EqualTo("--cxyz") .Or.EqualTo("--cxyz"));

            string first, last;
            Assert.That(FileHelper.TryParseTwoNames("First Last", out first, out last), Is.True);
            Assert.That(first, Is.EqualTo("First"));
            Assert.That(last, Is.EqualTo("Last"));

            Assert.That(FileHelper.TryParseTwoNames("  Too   Many  Parts  Extra", out first, out last), Is.False);
        }

        [Test]
        public void ObservableDictionary_Events_FireOnAddRemoveAndClear()
        {
            var dict = new OpenMetaverse.ObservableDictionary<string, int>();
            int adds = 0, removes = 0, changes = 0;

            DictionaryChangeCallback addCb = (action, entry) => { if (action == DictionaryEventAction.Add) adds++; };
            DictionaryChangeCallback removeCb = (action, entry) => { if (action == DictionaryEventAction.Remove) removes++; };
            DictionaryChangeCallback changeCb = (action, entry) => { if (action == DictionaryEventAction.Change) changes++; };

            dict.AddDelegate(DictionaryEventAction.Add, addCb);
            dict.AddDelegate(DictionaryEventAction.Remove, removeCb);
            dict.AddDelegate(DictionaryEventAction.Change, changeCb);

            dict.Add("one", 1);
            Assert.That(adds, Is.EqualTo(1));

            dict["one"] = 2;
            Assert.That(adds, Is.GreaterThanOrEqualTo(1));

            dict.Remove("one");
            Assert.That(removes, Is.EqualTo(1));

            dict.Add("a", 1);
            dict.Add("b", 2);
            dict.Clear();
            Assert.That(removes, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task EventSubscriptionHelper_WaitForEventAndAsync()
        {
            EventHandler<EventArgs> ev = null;
            Action<EventHandler<EventArgs>> subscribe = h => ev += h;
            Action<EventHandler<EventArgs>> unsubscribe = h => ev -= h;

            // fire event after small delay
            Task.Run(async () =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                ev?.Invoke(null, EventArgs.Empty);
            });

            var result = EventSubscriptionHelper.WaitForEvent<EventArgs, int>(
                subscribe, unsubscribe,
                filter: e => true,
                resultSelector: e => 123,
                timeoutMs: 500,
                defaultValue: -1);

            Assert.That(result, Is.EqualTo(123));

            // Async version with cancellation
            var cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                ev?.Invoke(null, EventArgs.Empty);
            });

            var asyncResult = await EventSubscriptionHelper.WaitForEventAsync<EventArgs, Guid>(
                subscribe, unsubscribe,
                filter: e => true,
                resultSelector: e => Guid.Empty,
                timeoutMs: 500,
                cancellationToken: default,
                defaultValue: Guid.NewGuid());

            Assert.That(asyncResult, Is.EqualTo(Guid.Empty));

            // Test EventSubscription disposable
            using (var sub = new EventSubscription<EventArgs>(subscribe, unsubscribe, (s, e) => { }))
            {
                // subscription exists while disposed = false
            }
            // no exception on dispose
            Assert.Pass();
        }

        [Test]
        public void Repeat_Interval_ExecutesAndCancels()
        {
            int count = 0;
            var cts = new CancellationTokenSource();
            var task = Repeat.Interval(TimeSpan.FromMilliseconds(20), () => Interlocked.Increment(ref count), cts.Token, immediately: true);

            // let it run a bit
            Thread.Sleep(120);
            cts.Cancel();
            task.Wait(1000);

            Assert.That(count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void NameValue_ParseAndToString()
        {
            var nv = new OpenMetaverse.NameValue("greeting STRING R S Hello");
            Assert.That(nv.Name, Is.EqualTo("greeting"));
            Assert.That(nv.Value, Is.EqualTo("Hello"));

            var arr = new[] { new OpenMetaverse.NameValue("n1", OpenMetaverse.NameValue.ValueType.String, OpenMetaverse.NameValue.ClassType.ReadOnly, OpenMetaverse.NameValue.SendtoType.Sim, "v1") };
            var s = OpenMetaverse.NameValue.NameValuesToString(arr);
            Assert.That(s, Does.Contain("n1").And.Contains("v1"));
        }

        [Test]
        public void LockingDictionary_BasicOperations()
        {
            var ld = new OpenMetaverse.LockingDictionary<string, int>();
            ((IDictionary<string,int>)ld).Add("x", 10);
            Assert.That(ld.ContainsKey("x"), Is.True);

            Assert.That(ld.TryGetValue("x", out var val), Is.True);
            Assert.That(val, Is.EqualTo(10));

            var found = ld.Find(v => v == 10);
            Assert.That(found, Is.EqualTo(10));

            var keys = ld.FindAll(k => k == "x");
            Assert.That(keys, Is.Not.Empty);

            int seen = 0;
            ld.ForEach((int v) => { seen += v; });
            Assert.That(seen, Is.EqualTo(10));

            var copy = ld.Copy();
            Assert.That(copy.ContainsKey("x"), Is.True);
        }

        [Test]
        public void Helpers_SplitAndFloatToTerseString()
        {
            var chunks = "abcdef".SplitBy(2);
            Assert.That(chunks, Is.EqualTo(new[] { "ab", "cd", "ef" }));

            Assert.That(Helpers.FloatToTerseString(0f), Is.EqualTo(".00"));
            Assert.That(Helpers.FloatToTerseString(1.2300f), Does.Contain("1.23"));
        }

        [Test]
        public void DisposalHelper_SafeDisposeAndUsing()
        {
            bool disposed = false;
            var throwing = new ThrowOnDispose(() => disposed = true);
            string logged = null;
            Action<string, Exception> logger = (m, e) => logged = m;

            DisposalHelper.SafeDispose(throwing, "test", logger);
            Assert.That(disposed, Is.True);
            Assert.That(logged, Is.Not.Null);

            var result = DisposalHelper.Using(() => new DisposableFlag(), d => { d.Flag = 5; return d.Flag; });
            Assert.That(result, Is.EqualTo(5));
        }

        private class ThrowOnDispose : IDisposable
        {
            private readonly Action onDispose;
            public ThrowOnDispose(Action onDispose) { this.onDispose = onDispose; }
            public void Dispose()
            {
                onDispose();
                throw new InvalidOperationException("boom");
            }
        }

        private class DisposableFlag : IDisposable
        {
            public int Flag;
            public void Dispose() { /* no-op */ }
        }
    }
}
