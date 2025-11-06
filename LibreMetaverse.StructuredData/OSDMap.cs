/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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

namespace OpenMetaverse.StructuredData
{
    /// <summary>
    /// OSD Map Element
    /// </summary>
    public sealed class OSDMap : OSD, IDictionary<string, OSD>
    {
        private readonly Dictionary<string, OSD> _mMap;

        public override OSDType Type => OSDType.Map;

        public OSDMap()
        {
            _mMap = new Dictionary<string, OSD>();
        }

        public OSDMap(int capacity)
        {
            _mMap = new Dictionary<string, OSD>(capacity);
        }

        public OSDMap(Dictionary<string, OSD> value)
        {
            this._mMap = value ?? new Dictionary<string, OSD>();
        }

        public override bool AsBoolean() { return _mMap.Count > 0; }

        public override string ToString()
        {
            return OSDParser.SerializeJsonString(this, true);
        }

        public override OSD Copy()
        {
            return new OSDMap(new Dictionary<string, OSD>(_mMap));
        }

        public Hashtable ToHashtable()
        {
            return new Hashtable(_mMap);
        }

        #region IDictionary Implementation

        public int Count => _mMap.Count;
        public bool IsReadOnly => false;
        public ICollection<string> Keys => _mMap.Keys;
        public ICollection<OSD> Values => _mMap.Values;

        public OSD this[string key]
        {
            get => _mMap.TryGetValue(key, out var llsd) ? llsd : new OSD();
            set => _mMap[key] = value;
        }

        public bool ContainsKey(string key)
        {
            return _mMap.ContainsKey(key);
        }

        public void Add(string key, OSD llsd)
        {
            _mMap.Add(key, llsd);
        }

        public void Add(KeyValuePair<string, OSD> kvp)
        {
            _mMap.Add(kvp.Key, kvp.Value);
        }

        public bool Remove(string key)
        {
            return _mMap.Remove(key);
        }

        public bool TryGetValue(string key, out OSD llsd)
        {
            return _mMap.TryGetValue(key, out llsd);
        }

        public void Clear()
        {
            _mMap.Clear();
        }

        public bool Contains(KeyValuePair<string, OSD> kvp)
        {
            return _mMap.Contains(kvp);
        }

        public void CopyTo(KeyValuePair<string, OSD>[] array, int index)
        {
            if (array == null) { throw new ArgumentNullException(nameof(array)); }
            if (array.Rank != 1) { throw new ArgumentException("Multi-dimensional arrays not supported"); }
            if (index < 0) { throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be a negative number"); }

            if (array.Length - index < _mMap.Count)
                throw new ArgumentException("Destination array is not large enough to hold the items.");

            int i = index;
            foreach (var kvp in _mMap)
            {
                array[i++] = kvp;
            }
        }

        public bool Remove(KeyValuePair<string, OSD> kvp)
        {
            return _mMap.Remove(kvp.Key);
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return _mMap.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, OSD>> IEnumerable<KeyValuePair<string, OSD>>.GetEnumerator()
        {
            return _mMap.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _mMap.GetEnumerator();
        }

        #endregion IDictionary Implementation
    }
}