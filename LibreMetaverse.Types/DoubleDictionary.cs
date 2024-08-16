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
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse
{
    public class DoubleDictionary<TKey1, TKey2, TValue>
    {
        private readonly Dictionary<TKey1, TValue> _dictionary1;
        private readonly Dictionary<TKey2, TValue> _dictionary2;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public DoubleDictionary()
        {
            _dictionary1 = new Dictionary<TKey1,TValue>();
            _dictionary2 = new Dictionary<TKey2,TValue>();
        }

        public DoubleDictionary(int capacity)
        {
            _dictionary1 = new Dictionary<TKey1, TValue>(capacity);
            _dictionary2 = new Dictionary<TKey2, TValue>(capacity);
        }

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            _rwLock.EnterWriteLock();

            try
            {
                if (_dictionary1.ContainsKey(key1))
                {
                    if (!_dictionary2.ContainsKey(key2))
                        throw new ArgumentException("key1 exists in the dictionary but not key2");
                }
                else if (_dictionary2.ContainsKey(key2))
                {
                    if (!_dictionary1.ContainsKey(key1))
                        throw new ArgumentException("key2 exists in the dictionary but not key1");
                }

                _dictionary1[key1] = value;
                _dictionary2[key2] = value;
            }
            finally { _rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key1, TKey2 key2)
        {
            bool success;
            _rwLock.EnterWriteLock();

            try
            {
                _dictionary1.Remove(key1);
                success = _dictionary2.Remove(key2);
            }
            finally { _rwLock.ExitWriteLock(); }

            return success;
        }

        public bool Remove(TKey1 key1)
        {
            bool found = false;
            _rwLock.EnterWriteLock();

            try
            {
                // This is an O(n) operation!
                TValue value;
                if (_dictionary1.TryGetValue(key1, out value))
                {
                    foreach (var kvp in _dictionary2)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            _dictionary1.Remove(key1);
                            _dictionary2.Remove(kvp.Key);
                            found = true;
                            break;
                        }
                    }
                }
            }
            finally { _rwLock.ExitWriteLock(); }

            return found;
        }

        public bool Remove(TKey2 key2)
        {
            bool found = false;
            _rwLock.EnterWriteLock();

            try
            {
                // This is an O(n) operation!
                TValue value;
                if (_dictionary2.TryGetValue(key2, out value))
                {
                    foreach (var kvp in _dictionary1)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            _dictionary2.Remove(key2);
                            _dictionary1.Remove(kvp.Key);
                            found = true;
                            break;
                        }
                    }
                }
            }
            finally { _rwLock.ExitWriteLock(); }

            return found;
        }

        public void Clear()
        {
            _rwLock.EnterWriteLock();

            try
            {
                _dictionary1.Clear();
                _dictionary2.Clear();
            }
            finally { _rwLock.ExitWriteLock(); }
        }

        public int Count => _dictionary1.Count;

        public bool ContainsKey(TKey1 key)
        {
            return _dictionary1.ContainsKey(key);
        }

        public bool ContainsKey(TKey2 key)
        {
            return _dictionary2.ContainsKey(key);
        }

        public bool TryGetValue(TKey1 key, out TValue value)
        {
            bool success;
            _rwLock.EnterReadLock();

            try { success = _dictionary1.TryGetValue(key, out value); }
            finally { _rwLock.ExitReadLock(); }

            return success;
        }

        public bool TryGetValue(TKey2 key, out TValue value)
        {
            bool success;
            _rwLock.EnterReadLock();

            try { success = _dictionary2.TryGetValue(key, out value); }
            finally { _rwLock.ExitReadLock(); }

            return success;
        }

        public void ForEach(Action<TValue> action)
        {
            _rwLock.EnterReadLock();

            try
            {
                foreach (var value in _dictionary1.Values)
                    action(value);
            }
            finally { _rwLock.ExitReadLock(); }
        }

        public void ForEach(Action<KeyValuePair<TKey1, TValue>> action)
        {
            _rwLock.EnterReadLock();

            try
            {
                foreach (var entry in _dictionary1)
                    action(entry);
            }
            finally { _rwLock.ExitReadLock(); }
        }

        public void ForEach(Action<KeyValuePair<TKey2, TValue>> action)
        {
            _rwLock.EnterReadLock();

            try
            {
                foreach (var entry in _dictionary2)
                    action(entry);
            }
            finally { _rwLock.ExitReadLock(); }
        }

        public TValue FindValue(Predicate<TValue> predicate)
        {
            _rwLock.EnterReadLock();
            try
            {
                foreach (var value in _dictionary1.Values)
                {
                    if (predicate(value))
                        return value;
                }
            }
            finally { _rwLock.ExitReadLock(); }

            return default(TValue);
        }

        public IList<TValue> FindAll(Predicate<TValue> predicate)
        {
            var list = new List<TValue>();
            _rwLock.EnterReadLock();

            try
            {
                foreach (var value in _dictionary1.Values)
                {
                    if (predicate(value))
                        list.Add(value);
                }
            }
            finally { _rwLock.ExitReadLock(); }

            return list;
        }

        public int RemoveAll(Predicate<TValue> predicate)
        {
            var list = new List<TKey1>();

            _rwLock.EnterUpgradeableReadLock();

            try
            {
                foreach (var kvp in _dictionary1)
                {
                    if (predicate(kvp.Value))
                        list.Add(kvp.Key);
                }

                var list2 = new List<TKey2>(list.Count);
                list2.AddRange(from kvp in _dictionary2 where predicate(kvp.Value) select kvp.Key);

                _rwLock.EnterWriteLock();

                try
                {
                    foreach (var t in list)
                        _dictionary1.Remove(t);

                    foreach (var t in list2)
                        _dictionary2.Remove(t);
                }
                finally { _rwLock.ExitWriteLock(); }
            }
            finally { _rwLock.ExitUpgradeableReadLock(); }

            return list.Count;
        }
    }
}
