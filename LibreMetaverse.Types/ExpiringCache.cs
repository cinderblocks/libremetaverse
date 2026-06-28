/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    #region TimedCacheKey Class

    internal class TimedCacheKey<TKey> : IComparable<TKey>
        where TKey : notnull
    {
        public DateTime ExpirationDate { get; private set; }
        public TKey Key { get; }
        public bool SlidingExpiration { get; }
        public TimeSpan SlidingExpirationWindowSize { get; }
        public TimedCacheKey(TKey key, DateTime expirationDate)
        {
            Key = key;
            SlidingExpiration = false;
            ExpirationDate = expirationDate;
        }

        public TimedCacheKey(TKey key, TimeSpan slidingExpirationWindowSize)
        {
            Key = key;
            SlidingExpiration = true;
            SlidingExpirationWindowSize = slidingExpirationWindowSize;
            Accessed();
        }

        public void Accessed()
        {
            if (SlidingExpiration)
                ExpirationDate = DateTime.UtcNow.Add(SlidingExpirationWindowSize);
        }

        public int CompareTo(TKey? other)
        {
            return Key.GetHashCode().CompareTo(other?.GetHashCode() ?? 0);
        }
    }

    #endregion

    public sealed class ExpiringCache<TKey, TValue> : IDisposable
        where TKey : notnull
    {
        const int MAX_LOCK_WAIT = 5000; // milliseconds

        #region Private fields

        /// <summary>For thread safety</summary>
        private readonly object syncRoot = new object();

        private readonly Dictionary<TimedCacheKey<TKey>, TValue> timedStorage = new Dictionary<TimedCacheKey<TKey>, TValue>();
        private readonly Dictionary<TKey, TimedCacheKey<TKey>> timedStorageIndex = new Dictionary<TKey, TimedCacheKey<TKey>>();

#if NET6_0_OR_GREATER
        private PeriodicTimer? _purgeTimer;
        private CancellationTokenSource? _purgeCts;
        private Task? _purgeTask;
#else
        private readonly System.Timers.Timer _timer;
#endif
        private bool _disposed;

        #endregion

        #region Constructor

        public ExpiringCache()
        {
#if NET6_0_OR_GREATER
            _purgeCts = new CancellationTokenSource();
            _purgeTimer = new PeriodicTimer(TimeSpan.FromSeconds(1.0));
            _purgeTask = Task.Run(PurgeLoopAsync);
#else
            _timer = new System.Timers.Timer(1000.0);
            _timer.Elapsed += PurgeCache;
            _timer.Start();
#endif
        }

        #endregion

        #region Public methods

        public bool Add(TKey key, TValue value, double expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    return false;
                }
                else
                {
                    var internalKey = new TimedCacheKey<TKey>(key, DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds));
                    timedStorage.Add(internalKey, value);
                    timedStorageIndex.Add(key, internalKey);
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Add(TKey key, TValue value, TimeSpan slidingExpiration)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    return false;
                }
                else
                {
                    var internalKey = new TimedCacheKey<TKey>(key, slidingExpiration);
                    timedStorage.Add(internalKey, value);
                    timedStorageIndex.Add(key, internalKey);
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool AddOrUpdate(TKey key, TValue value, double expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (Contains(key))
                {
                    Update(key, value, expirationSeconds);
                    return false;
                }
                else
                {
                    Add(key, value, expirationSeconds);
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool AddOrUpdate(TKey key, TValue value, TimeSpan slidingExpiration)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (Contains(key))
                {
                    Update(key, value, slidingExpiration);
                    return false;
                }
                else
                {
                    Add(key, value, slidingExpiration);
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public void Clear()
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                timedStorage.Clear();
                timedStorageIndex.Clear();
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Contains(TKey key)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                return timedStorageIndex.ContainsKey(key);
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public int Count => timedStorage.Count;

        public object? this[TKey key]
        {
            get
            {
                if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                    throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
                try
                {
                    if (timedStorageIndex.TryGetValue(key, out var tkey))
                    {
                        var o = timedStorage[tkey];
                        timedStorage.Remove(tkey);
                        tkey.Accessed();
                        timedStorage.Add(tkey, o);
                        return o;
                    }
                    else
                    {
                        throw new ArgumentException("Key not found in the cache");
                    }
                }
                finally { Monitor.Exit(syncRoot); }
            }
        }

        public bool Remove(TKey key)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    timedStorage.Remove(timedStorageIndex[key]);
                    timedStorageIndex.Remove(key);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool TryGetValue(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TValue value)
        {
            TValue? o;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.TryGetValue(key, out var tkey))
                {
                    o = timedStorage[tkey];
                    timedStorage.Remove(tkey);
                    tkey.Accessed();
                    timedStorage.Add(tkey, o);
                    value = o;
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }

            value = default!;
            return false;
        }

        public bool Update(TKey key, TValue value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    timedStorage.Remove(timedStorageIndex[key]);
                    timedStorageIndex[key].Accessed();
                    timedStorage.Add(timedStorageIndex[key], value);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Update(TKey key, TValue value, double expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    timedStorage.Remove(timedStorageIndex[key]);
                    timedStorageIndex.Remove(key);
                }
                else
                {
                    return false;
                }

                TimedCacheKey<TKey> internalKey = new TimedCacheKey<TKey>(key, DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds));
                timedStorage.Add(internalKey, value);
                timedStorageIndex.Add(key, internalKey);
                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Update(TKey key, TValue value, TimeSpan slidingExpiration)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorageIndex.ContainsKey(key))
                {
                    timedStorage.Remove(timedStorageIndex[key]);
                    timedStorageIndex.Remove(key);
                }
                else
                {
                    return false;
                }

                var internalKey = new TimedCacheKey<TKey>(key, slidingExpiration);
                timedStorage.Add(internalKey, value);
                timedStorageIndex.Add(key, internalKey);
                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public void CopyTo(Array array, int startIndex)
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }
            if (startIndex < 0) { throw new ArgumentOutOfRangeException(nameof(startIndex), "startIndex must be >= 0."); }
            if (array.Rank > 1) { throw new ArgumentException("array must be of Rank 1 (one-dimensional)", nameof(array)); }
            if (startIndex >= array.Length) { throw new ArgumentException("startIndex must be less than the length of the array.", nameof(startIndex)); }
            if (Count > array.Length - startIndex) { throw new ArgumentException("There is not enough space from startIndex to the end of the array to accomodate all items in the cache."); }

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                foreach (var o in timedStorage)
                {
                    array.SetValue(o, startIndex);
                    startIndex++;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

#if NET6_0_OR_GREATER
            _purgeCts?.Cancel();
            _purgeTimer?.Dispose();
            _purgeTimer = null;
            _purgeCts?.Dispose();
            _purgeCts = null;
            // _purgeTask completes on cancellation; don't await here to avoid sync deadlock
            _purgeTask = null;
#else
            _timer.Stop();
            _timer.Dispose();
#endif
        }

        #endregion

        #region Private methods

#if NET6_0_OR_GREATER
        private async Task PurgeLoopAsync()
        {
            try
            {
                while (await _purgeTimer!.WaitForNextTickAsync(_purgeCts!.Token).ConfigureAwait(false))
                {
                    PurgeExpired();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void PurgeExpired()
        {
            var now = DateTime.UtcNow;
            List<TimedCacheKey<TKey>>? toRemove = null;

            lock (syncRoot)
            {
                foreach (var timedKey in timedStorage.Keys)
                {
                    if (timedKey.ExpirationDate < now)
                    {
                        toRemove ??= new List<TimedCacheKey<TKey>>();
                        toRemove.Add(timedKey);
                    }
                }

                if (toRemove != null)
                {
                    foreach (var timedKey in toRemove)
                    {
                        timedStorageIndex.Remove(timedKey.Key);
                        timedStorage.Remove(timedKey);
                    }
                }
            }
        }
#else
        private void PurgeCache(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var now = DateTime.UtcNow;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                return;
            try
            {
                List<TimedCacheKey<TKey>>? toRemove = null;

                foreach (var timedKey in timedStorage.Keys)
                {
                    if (timedKey.ExpirationDate < now)
                    {
                        toRemove ??= new List<TimedCacheKey<TKey>>();
                        toRemove.Add(timedKey);
                    }
                }

                if (toRemove != null)
                {
                    foreach (var timedKey in toRemove)
                    {
                        timedStorageIndex.Remove(timedKey.Key);
                        timedStorage.Remove(timedKey);
                    }
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }
#endif

        #endregion
    }
}
