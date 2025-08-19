/*
 * Copyright (c) 2022-2025, Sjofn LLC
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
using System.Linq;

namespace LibreMetaverse
{
    public interface ICacheDictionaryRemovalStrategy<TKey>
    {
        /// <summary>
        /// Initialize the strategy and pass the maximum number of allowed items
        /// </summary>
        /// <param name="maxSize">The maximum number of allowed items</param>
        void Initialize(int maxSize);

        /// <summary>
        /// Notify the strategy that a key was added to the base collection
        /// </summary>
        /// <param name="key">The key that was added</param>
        void KeyAdded(TKey key);

        /// <summary>
        /// Notify the strategy that a key was removed from the base collection
        /// </summary>
        /// <param name="key">The key that was removed</param>
        void KeyRemoved(TKey key);

        /// <summary>
        /// Notify the strategy that a key was accessed (retrieved by the user) in the base collection
        /// </summary>
        /// <param name="key">The key that was retrieved</param>
        void KeyAccessed(TKey key);

        /// <summary>
        /// Notify the strategy that the base collection was cleared
        /// </summary>
        void Clear();

        /// <summary>
        /// Get the most appropriate key to remove, this is called when the base collection runs out of space
        /// </summary>
        /// <returns>The key that the base collection will remove</returns>
        TKey GetKeyToRemove();
    }

    public class CacheDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _data;
        private readonly int _maxSize;
        private readonly ICacheDictionaryRemovalStrategy<TKey> _removalStrategy;

        public CacheDictionary(int maxSize, ICacheDictionaryRemovalStrategy<TKey> removalStrategy)
        {
            if (maxSize == 0)
                throw new ArgumentException("maxSize must be a positive integer value");
            _maxSize = maxSize;
            _removalStrategy = removalStrategy ?? throw new ArgumentNullException(nameof(removalStrategy));
            _data = new Dictionary<TKey, TValue>();

            _removalStrategy.Initialize(maxSize);
        }

        #region IDictionaty Implementation

        public void Add(TKey key, TValue value)
        {
            if (_data.ContainsKey(key))
                _data.Add(key, value); //I want to throw the same exception as the internal dictionary for this case.

            if (_data.Count == _maxSize)
            {
                TKey keyToRemove = _removalStrategy.GetKeyToRemove();
                if (_data.ContainsKey(keyToRemove))
                    _data.Remove(keyToRemove);
                else
                    throw new Exception(
                        $"Could not find a valid key to remove from cache, key = {(key == null ? "null" : key.ToString())}");
            }
            _data.Add(key, value);
            _removalStrategy.KeyAdded(key);
        }

        public bool ContainsKey(TKey key)
        {
            return _data.ContainsKey(key);
        }

        public ICollection<TKey> Keys => _data.Keys;

        public bool Remove(TKey key)
        {
            bool result = _data.Remove(key);
            if (result)
                _removalStrategy.KeyRemoved(key);
            return result;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            bool result = _data.TryGetValue(key, out value);
            if (result)
                _removalStrategy.KeyAccessed(key);
            return result;
        }

        public ICollection<TValue> Values => _data.Values;

        public TValue this[TKey key]
        {
            get
            {
                TValue result = _data[key];
                _removalStrategy.KeyAccessed(key);
                return result;
            }
            set
            {
                _data[key] = value;
                _removalStrategy.KeyAccessed(key);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _data.Clear();
            _removalStrategy.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _data.Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_data).CopyTo(array, arrayIndex);
        }

        public int Count => _data.Count;

        public bool IsReadOnly => ((IDictionary<TKey, TValue>)_data).IsReadOnly;

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            bool result = ((IDictionary<TKey, TValue>)_data).Remove(item);
            if (result)
                _removalStrategy.KeyRemoved(item.Key);
            return result;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new CacheDictionaryEnumerator(_data.GetEnumerator(), _removalStrategy);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new CacheDictionaryEnumerator(_data.GetEnumerator(), _removalStrategy);
        }
        #endregion

        public class CacheDictionaryEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private IEnumerator<KeyValuePair<TKey, TValue>> _innerEnumerator;
            private readonly ICacheDictionaryRemovalStrategy<TKey> _removalStrategy;

            internal CacheDictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> innerEnumerator, ICacheDictionaryRemovalStrategy<TKey> removalStrategy)
            {
                _innerEnumerator = innerEnumerator ?? throw new ArgumentNullException(nameof(innerEnumerator));
                _removalStrategy = removalStrategy ?? throw new ArgumentNullException(nameof(removalStrategy));
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    KeyValuePair<TKey, TValue> result = _innerEnumerator.Current;
                    _removalStrategy.KeyAccessed(result.Key);
                    return result;
                }
            }

            public void Dispose()
            {
                _innerEnumerator.Dispose();
                _innerEnumerator = null;
            }

            object System.Collections.IEnumerator.Current => this.Current;

            public bool MoveNext()
            {
                return _innerEnumerator.MoveNext();
            }

            public void Reset()
            {
                _innerEnumerator.Reset();
            }
        }
    }



    /// <summary>
    /// A removal strategy that removes some item.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class EmptyRemovalStrategy<TKey> : ICacheDictionaryRemovalStrategy<TKey>
    {
        private HashSet<TKey> _currentKeys;

        public void Initialize(int maxSize)
        {
            _currentKeys = new HashSet<TKey>();
        }

        public void KeyAdded(TKey key)
        {
            _currentKeys.Add(key);
        }

        public void KeyRemoved(TKey key)
        {
            if (_currentKeys.Contains(key))
                _currentKeys.Remove(key);
        }

        public void KeyAccessed(TKey key)
        {

        }

        public TKey GetKeyToRemove()
        {
            if (_currentKeys.Count == 0)
                throw new IndexOutOfRangeException("No key to remove because the internal collection is empty");
            TKey key = _currentKeys.First();
            _currentKeys.Remove(key);
            return key;
        }

        public void Clear()
        {
            _currentKeys.Clear();
        }
    }

    /// <summary>
    /// Remove the most recently used (MRU) item from the cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class MruRemovalStrategy<TKey> : ICacheDictionaryRemovalStrategy<TKey>
    {
        private List<TKey> _items;

        public void Initialize(int maxSize)
        {
            _items = new List<TKey>(maxSize);
        }

        public void KeyAdded(TKey key)
        {
            _items.Add(key);
        }

        public void KeyRemoved(TKey key)
        {
            _items.Remove(key);
        }

        public void KeyAccessed(TKey key)
        {
            _items.Remove(key);
            _items.Add(key);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public TKey GetKeyToRemove()
        {
            if (_items.Count == 0)
                throw new IndexOutOfRangeException("No key to remove because the internal collection is empty");
            TKey key = _items.Last();
            _items.Remove(key);
            return key;
        }
    }

    /// <summary>
    /// Removes the least recently used (LRU) item in the cache
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class LruRemovalStrategy<TKey> : ICacheDictionaryRemovalStrategy<TKey>
    {
        private List<TKey> _items;

        public void Initialize(int maxSize)
        {
            _items = new List<TKey>(maxSize);
        }

        public void KeyAdded(TKey key)
        {
            _items.Add(key);
        }

        public void KeyRemoved(TKey key)
        {
            _items.Remove(key);
        }

        public void KeyAccessed(TKey key)
        {
            _items.Remove(key);
            _items.Add(key);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public TKey GetKeyToRemove()
        {
            if (_items.Count == 0)
                throw new IndexOutOfRangeException("No key to remove because the internal collection is empty");
            TKey key = _items.First();
            _items.Remove(key);
            return key;
        }
    }
}
