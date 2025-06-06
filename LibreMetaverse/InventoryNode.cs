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
using MessagePack;

namespace OpenMetaverse
{
    [MessagePackObject]
    public partial class InventoryNode
    {
        private InventoryNodeDictionary nodes;

        [Key("Data")]
        public InventoryBase Data { get; set; }

        /// <summary>User data</summary>
        [IgnoreMember]
        public object Tag { get; set; }

        [IgnoreMember]
        public InventoryNode Parent { get; set; }

        [IgnoreMember]
        public InventoryNodeDictionary Nodes
        {
            get => nodes ?? (nodes = new InventoryNodeDictionary(this));
            set => nodes = value;
        }

        /// <summary>
        /// For inventory folder nodes specifies weather the folder needs to be
        /// refreshed from the server
        /// </summary>
        [IgnoreMember]
        public bool NeedsUpdate { get; set; } = true;

        [IgnoreMember]
        public DateTime ModifyTime
        {
            get
            {
                if (Data is InventoryItem item)
                {
                    return item.CreationDate;
                }
                DateTime newest = default(DateTime); //.MinValue;
                if (Data is InventoryFolder)
                {
                    foreach (var node in Nodes.Values)
                    {
                        var t = node.ModifyTime;
                        if (t > newest) newest = t;
                    }
                }
                return newest;
            }
        }

        public void Sort()
        {
            Nodes.Sort();
        }

        public InventoryNode()
        {
        }

        /// <param name="data"></param>
        public InventoryNode(InventoryBase data)
        {
            this.Data = data;
        }

        /// <summary>
        /// De-serialization constructor for the InventoryNode Class
        /// </summary>
        public InventoryNode(InventoryBase data, InventoryNode parent)
        {
            this.Data = data;
            this.Parent = parent;

            if (parent != null)
            {
                // Add this node to the collection of parent nodes
                lock (parent.Nodes.SyncRoot) parent.Nodes.Add(data.UUID, this);
            }
        }

        public override string ToString()
        {
            return this.Data == null ? "[Empty Node]" : this.Data.ToString();
        }
    }
}
