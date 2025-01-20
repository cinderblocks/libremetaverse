/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024, Sjofn LLC
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace OpenMetaverse
{
    /// <summary>
    /// The LockingDictionary class is used through the library for storing key/value pairs.
    /// It is intended to be a replacement for the generic Dictionary class and should
    /// be used in its place. It contains several methods for allowing access to the data from
    /// outside the library that are read only and thread safe.
    /// </summary>
    /// <typeparam name="TKey">Key <see langword="Tkey"/></typeparam>
    /// <typeparam name="TValue">Value <see langword="TValue"/></typeparam>
    public class LockingDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// Internal dictionary that this class wraps around. Do not
        /// modify or enumerate the contents of this dictionary without locking
        /// on this member
        /// </summary>
        internal Dictionary<TKey, TValue> Dictionary;

        public Dictionary<TKey,TValue> Copy()
        {
            lock (Dictionary)
                return new Dictionary<TKey, TValue>(Dictionary);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            lock (Dictionary)
                Dictionary.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            lock (Dictionary)
                Dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue v;
            lock (Dictionary)
                return (Dictionary.TryGetValue(item.Key, out v) && v.Equals(item.Key));
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (Dictionary)
                ((ICollection<KeyValuePair<TKey, TValue>>)Dictionary).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!Contains(item)) { return false; }
            lock (Dictionary)
                Dictionary.Remove(item.Key);
            return true;

        }

        [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            lock (Dictionary)
                Dictionary.GetObjectData(info, context);
        }

        public void OnDeserialization(object sender)
        {
            lock( Dictionary)
                Dictionary.OnDeserialization(sender);
        }

        /// <inheritdoc/>
        public IEqualityComparer<TKey> Comparer
        {
            get { lock (Dictionary) return Dictionary.Comparer; }
        }

        /// <summary>
        /// Gets the number of Key/Value pairs contained in <see cref="LockingDictionary{TKey,TValue}"/>
        /// </summary>
        public int Count { get { lock (Dictionary) return Dictionary.Count; } }

        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Initializes a new instance of <see cref="LockingDictionary{TKey,TValue}"/> Class
        /// with the specified key/value, has the default initial capacity.
        /// </summary>
        public LockingDictionary()
        {
            Dictionary = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LockingDictionary{TKey,TValue}"/> Class
        /// with the specified key/value, has its initial values copied from the specified
        /// <seealso cref="T:System.Collections.Generic.Dictionary"/>
        /// </summary>
        /// <param name="dictionary"><seealso cref="T:System.Collections.Generic.Dictionary"/>
        /// to copy initial values from</param>
        /// <example>
        /// <code>
        /// // initialize a new LockingDictionary named testAvName with a UUID as the key and an string as the value.
        /// // populates with copied values from example KeyNameCache Dictionary.
        ///
        /// // create source dictionary
        /// Dictionary&lt;UUID, string&gt; KeyNameCache = new Dictionary&lt;UUID, string&gt;();
        /// KeyNameCache.Add("8300f94a-7970-7810-cf2c-fc9aa6cdda24", "Jack Avatar");
        /// KeyNameCache.Add("27ba1e40-13f7-0708-3e98-5819d780bd62", "Jill Avatar");
        ///
        /// // Initialize new dictionary.
        /// public LockingDictionary&lt;UUID, string&gt; testAvName = new LockingDictionary&lt;UUID, string&gt;(KeyNameCache);
        /// </code>
        /// </example>
        public LockingDictionary(IDictionary<TKey, TValue> dictionary)
        {
            Dictionary = new Dictionary<TKey, TValue>(dictionary);
        }

        /// <summary>
        /// Initializes a new instance of <seealso cref="LockingDictionary{TKey,TValue}"/>
        /// with the specified key/value, With its initial capacity specified.
        /// </summary>
        /// <param name="capacity">Initial size of dictionary</param>
        /// <example>
        /// <code>
        /// // initialize a new LockingDictionary named testDict with a string as the key and an int as the value,
        /// // initially allocated room for 10 entries.
        /// public LockingDictionary&lt;string, int&gt; testDict = new LockingDictionary&lt;string, int&gt;(10);
        /// </code>
        /// </example>
        public LockingDictionary(int capacity)
        {
            Dictionary = new Dictionary<TKey, TValue>(capacity);
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            return Remove(key);
        }

        /// <summary>
        /// Try to get entry from <see cref="LockingDictionary{TKey,TValue}"/> with specified key
        /// </summary>
        /// <param name="key">Key to use for lookup</param>
        /// <param name="value">Value returned</param>
        /// <returns><see langword="true"/> if specified key exists,  <see langword="false"/> if not found</returns>
        /// <example>
        /// <code>
        /// // find your avatar using the Simulator.ObjectsAvatars LockingDictionary:
        ///    Avatar av;
        ///    if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.AgentID, out av))
        ///        Console.WriteLine("Found Avatar {0}", av.Name);
        /// </code>
        /// <seealso cref="Simulator.ObjectsAvatars"/>
        /// </example>
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (Dictionary)
            {
                return Dictionary.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Finds the specified match.
        /// </summary>
        /// <param name="match">The match.</param>
        /// <returns>Matched value</returns>
        /// <example>
        /// <code>
        /// // use a delegate to find a prim in the ObjectsPrimitives LockingDictionary
        /// // with the ID 95683496
        /// uint findID = 95683496;
        /// Primitive findPrim = sim.ObjectsPrimitives.Find(
        ///             delegate(Primitive prim) { return prim.ID == findID; });
        /// </code>
        /// </example>
        public TValue Find(Predicate<TValue> match)
        {
            lock (Dictionary)
            {
                foreach (var value in Dictionary.Values.Where(value => match(value)))
                {
                    return value;
                }
            }
            return default(TValue);
        }
        
        /// <summary>Find All items in <see cref="LockingDictionary{TKey,TValue}"/></summary>
        /// <param name="match">return matching items.</param>
        /// <returns>a <see cref="T:System.Collections.Generic.List"/> containing found items.</returns>
        /// <example>
        /// Find All prims within 20 meters and store them in a List
        /// <code>
        /// int radius = 20;
        /// List&lt;Primitive&gt; prims = Client.Network.CurrentSim.ObjectsPrimitives.FindAll(
        ///         delegate(Primitive prim) {
        ///             Vector3 pos = prim.Position;
        ///             return ((prim.ParentID == 0) &amp;&amp; (pos != Vector3.Zero) &amp;&amp; (Vector3.Distance(pos, location) &lt; radius));
        ///         }
        ///    );
        ///</code>
        ///</example>
        public List<TValue> FindAll(Predicate<TValue> match)
        {
            var found = new List<TValue>();
            lock (Dictionary)
            {
                found.AddRange(from kvp in Dictionary where match(kvp.Value) select kvp.Value);
            }
            return found;
        }

        /// <summary>Find All items in <see cref="LockingDictionary{TKey,TValue}"/></summary>
        /// <param name="match">return matching keys.</param>
        /// <returns>a <see cref="T:System.Collections.Generic.List"/> containing found keys.</returns>
        /// <example>
        /// Find All keys which also exist in another dictionary
        /// <code>
        /// List&lt;UUID&gt; matches = myDict.FindAll(
        ///         delegate(UUID id) {
        ///             return myOtherDict.ContainsKey(id);
        ///         }
        ///    );
        ///</code>
        ///</example>
        public List<TKey> FindAll(Predicate<TKey> match)
        {
            var found = new List<TKey>();
            lock (Dictionary)
            {
                found.AddRange(from kvp in Dictionary where match(kvp.Key) select kvp.Key);
            }
            return found;
        }

        /// <summary>Perform an <seealso cref="T:System.Action"/> on each entry in <see cref="LockingDictionary{TKey,TValue}"/></summary>
        /// <param name="action"><seealso cref="T:System.Action"/> to perform</param>
        /// <example>
        /// <code>
        /// // Iterates over the ObjectsPrimitives LockingDictionary and prints out some information.
        /// Client.Network.CurrentSim.ObjectsPrimitives.ForEach(
        ///     delegate(Primitive prim)
        ///     {
        ///         if (prim.Text != null)
        ///         {
        ///             Console.WriteLine("NAME={0} ID = {1} TEXT = '{2}'",
        ///                 prim.PropertiesFamily.Name, prim.ID, prim.Text);
        ///         }
        ///     });
        ///</code>
        ///</example>
        public void ForEach(Action<TValue> action)
        {
            lock (Dictionary)
            {
                foreach (var value in Dictionary.Values)
                {
                    action(value);
                }
            }
        }

        /// <summary>Perform an <seealso cref="T:System.Action"/> on each key of an <see cref="LockingDictionary{TKey,TValue}/></summary>
        /// <param name="action"><seealso cref="T:System.Action"/> to perform</param>
        public void ForEach(Action<TKey> action)
        {
            lock (Dictionary)
            {
                foreach (var key in Dictionary.Keys)
                {
                    action(key);
                }
            }
        }

        /// <summary>
        /// Perform an <seealso cref="T:System.Action"/> on each KeyValuePair of <see cref="LockingDictionary{TKey,TValue}"/>
        /// </summary>
        /// <param name="action"><seealso cref="T:System.Action"/> to perform</param>
        public void ForEach(Action<KeyValuePair<TKey, TValue>> action)
        {
            lock (Dictionary)
            {
                foreach (var entry in Dictionary)
                {
                    action(entry);
                }
            }
        }

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            Add(key, value);
        }

        /// <summary>Check if Key exists in Dictionary</summary>
        /// <param name="key">Key to check for</param>
        /// <returns><see langword="true"/> if found, <see langword="false"/> otherwise</returns>
        public bool ContainsKey(TKey key)
        {
            lock (Dictionary) return Dictionary.ContainsKey(key);
        }

        /// <summary>Check if Value exists in Dictionary</summary>
        /// <param name="value">Value to check for</param>
        /// <returns><see langword="true"/> if found, <see langword="false"/> otherwise</returns>
        public bool ContainsValue(TValue value)
        {
            lock (Dictionary) return Dictionary.ContainsValue(value);
        }

        /// <summary>
        /// Adds the specified key to the dictionary, dictionary locking is not performed,
        /// <see cref="SafeAdd"/>
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        internal void Add(TKey key, TValue value)
        {
            lock (Dictionary)
				Dictionary[key] = value;
        }

        /// <summary>
        /// Removes the specified key, dictionary locking is not performed
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> if successful, <see langword="false"/> otherwise</returns>
        internal bool Remove(TKey key)
        {
            lock (Dictionary)
                return Dictionary.Remove(key);
        }

        /// <summary>
        /// Indexer for the dictionary
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The value</returns>
        public TValue this[TKey key]
        {
            get
            {
                lock (Dictionary)
                {
                    return Dictionary[key];
                }
            }
            set
            {
                lock (Dictionary)
                {
                    Dictionary[key] = value;
                }
            }
        }
        
        public object this[object key]
        {
            get
            {
                lock (Dictionary)
                {
                    return Dictionary[(TKey) key];
                }
            }
            set
            {
                lock (Dictionary)
                {
                    Dictionary[(TKey) key] = (TValue) value;
                }
            }
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys
        {
            get
            {
                lock (Dictionary)
                {
                    return Dictionary.Keys;
                }
            }
        }
        
        /// <inheritdoc />
        public ICollection<TValue> Values
        {
            get
            {
                lock (Dictionary)
                {
                    return Dictionary.Values;
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (Dictionary)
            {
                return Dictionary.GetEnumerator();
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
