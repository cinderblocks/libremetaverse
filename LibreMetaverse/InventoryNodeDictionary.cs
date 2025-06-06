/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2024, Sjofn LLC.
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

namespace OpenMetaverse
{
    public partial class InventoryNodeDictionary: IComparer<UUID>
    {
        protected readonly SortedDictionary<UUID, InventoryNode> SDictionary;
        protected readonly Dictionary<UUID, InventoryNode> Dictionary = new Dictionary<UUID, InventoryNode>();
        protected InventoryNode parent;

        protected readonly object syncRoot = new object();
        public int Compare(UUID x, UUID y)
        {
            InventoryNode n1 = Get(x);
            InventoryNode n2 = Get(y);
            int diff = NullCompare(n1, n2);
            if (diff != 0) return diff;
            if (n1 == null) return x.CompareTo(y);
            DateTime t1 = n1.ModifyTime;
            DateTime t2 = n2.ModifyTime;
            diff = t1.CompareTo(t2);
            if (diff != 0) return diff;
            var d1 = n1.Data;
            var d2 = n2.Data;
            diff = NullCompare(d1, d2);
            if (diff != 0) return diff;
            if (d1 != null)
            {
                diff = NullCompare(d1.Name, d2.Name);
                if (diff != 0) return diff;
                if (d1.Name != null)
                {
                    // both are not null.. due to NullCompare code
                    diff = string.Compare(d1.Name, d2.Name, StringComparison.Ordinal);
                    if (diff != 0) return diff;
                }
            }
            return x.CompareTo(y);
        }

        private InventoryNode Get(UUID uuid)
        {
            InventoryNode val;
            return Dictionary.TryGetValue(uuid, out val) ? val : null;
        }

        static int NullCompare(object o1, object o2)
        {
            return ReferenceEquals(o1, null).CompareTo(ReferenceEquals(o2, null));
        }

        public InventoryNode Parent
        {
            get => parent;
            set => parent = value;
        }

        public object SyncRoot => syncRoot;

        public int Count => Dictionary.Count;

        public InventoryNodeDictionary(InventoryNode parent)
        {
            if (Settings.SORT_INVENTORY) SDictionary = new SortedDictionary<UUID, InventoryNode>(this);
            this.parent = parent;
        }

        public InventoryNode this[UUID key]
        {
            get => (InventoryNode)Dictionary[key];
            set
            {
                value.Parent = parent;
                lock (syncRoot)
                {
                    Dictionary[key] = value;
                    if (Settings.SORT_INVENTORY) SDictionary[key] = value;
                }
            }
        }

        public ICollection<UUID> Keys
        {
            get
            {
                if (Settings.SORT_INVENTORY) return SDictionary.Keys;
                return Dictionary.Keys;
            }
        }
        public ICollection<InventoryNode> Values
        {
            get
            {
                if (Settings.SORT_INVENTORY) return SDictionary.Values;
                return Dictionary.Values;
            }
        }

        public void Add(UUID key, InventoryNode value)
        {
            value.Parent = parent;
            lock (syncRoot)
            {
                Dictionary[key] = value;
                if (Settings.SORT_INVENTORY) SDictionary.Add(key, value);
            } 
        }

        public void Remove(UUID key)
        {
            lock (syncRoot)
            {
                Dictionary.Remove(key);
                if (Settings.SORT_INVENTORY) SDictionary.Remove(key);
            }
        }

        public bool Contains(UUID key)
        {
            return Dictionary.ContainsKey(key);
        }

        internal void Sort()
        {
            if (Settings.SORT_INVENTORY)
            {
                // TODO resort SDictionary now that more data has come?  
            } 
        }
    }
}
