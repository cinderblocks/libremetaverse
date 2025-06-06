﻿/*
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
using System.Threading;
using System.Collections.Generic;

namespace OpenMetaverse
{
    #region TimedCacheKey Class

    internal class TimedCacheKey<TKey> : IComparable<TKey>
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

        public int CompareTo(TKey other)
        {
            return Key.GetHashCode().CompareTo(other.GetHashCode());
        }
    }

    #endregion

    public sealed class ExpiringCache<TKey, TValue>
    {
        const double CACHE_PURGE_HZ = 1.0;
        const int MAX_LOCK_WAIT = 5000; // milliseconds

        #region Private fields

        /// <summary>For thread safety</summary>
        object syncRoot = new object();
        /// <summary>For thread safety</summary>
        object isPurging = new object();

        readonly Dictionary<TimedCacheKey<TKey>, TValue> timedStorage = new Dictionary<TimedCacheKey<TKey>, TValue>();
        readonly Dictionary<TKey, TimedCacheKey<TKey>> timedStorageIndex = new Dictionary<TKey, TimedCacheKey<TKey>>();
        private System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(CACHE_PURGE_HZ).TotalMilliseconds);

        #endregion

        #region Constructor

        public ExpiringCache()
        {
            timer.Elapsed += PurgeCache;
            timer.Start();
        }

        #endregion

        #region Public methods

        public bool Add(TKey key, TValue value, double expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                // This is the actual adding of the key
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
                // This is the actual adding of the key
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

        public object this[TKey key]
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

        public bool TryGetValue(TKey key, out TValue value)
        {
            TValue o;

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

            value = default(TValue);
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
            // Error checking
            if (array == null) { throw new ArgumentNullException(nameof(array)); }

            if (startIndex < 0) { throw new ArgumentOutOfRangeException(nameof(startIndex), "startIndex must be >= 0."); }

            if (array.Rank > 1) { throw new ArgumentException("array must be of Rank 1 (one-dimensional)", nameof(array)); }
            if (startIndex >= array.Length) { throw new ArgumentException("startIndex must be less than the length of the array.", nameof(startIndex)); }
            if (Count > array.Length - startIndex) { throw new ArgumentException("There is not enough space from startIndex to the end of the array to accomodate all items in the cache."); }

            // Copy the data to the array (in a thread-safe manner)
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

        #region Private methods

        /// <summary>
        /// Purges expired objects from the cache. Called automatically by the purge timer.
        /// </summary>
        private void PurgeCache(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Only let one thread purge at once - a buildup could cause a crash
            // This could cause the purge to be delayed while there are lots of read/write ops 
            // happening on the cache
            if (!Monitor.TryEnter(isPurging))
                return;

            var signalTime = DateTime.UtcNow;

            try
            {
                // If we fail to acquire a lock on the synchronization root after MAX_LOCK_WAIT, skip this purge cycle
                if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                    return;
                try
                {
                    var expiredItems = new Lazy<List<object>>();

                    foreach (var timedKey in timedStorage.Keys)
                    {
                        if (timedKey.ExpirationDate < signalTime)
                        {
                            // Mark the object for purge
                            expiredItems.Value.Add(timedKey.Key);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (expiredItems.IsValueCreated)
                    {
                        foreach (TKey key in expiredItems.Value)
                        {
                            var timedKey = timedStorageIndex[key];
                            timedStorageIndex.Remove(timedKey.Key);
                            timedStorage.Remove(timedKey);
                        }
                    }
                }
                finally { Monitor.Exit(syncRoot); }
            }
            finally { Monitor.Exit(isPurging); }
        }

        #endregion
    }
}
