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
using System.Runtime.Serialization;
using OpenMetaverse.StructuredData;
#if NET7_0_OR_GREATER
using MemoryPack;
#endif

namespace OpenMetaverse
{
    /// <summary>
    /// Base Class for Inventory Items
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(InventoryFolder))]
    [MemoryPackUnion(1, typeof(InventoryItem))]
    [MemoryPackUnion(2, typeof(InventoryAnimation))]
    [MemoryPackUnion(3, typeof(InventoryAttachment))]
    [MemoryPackUnion(4, typeof(InventoryCallingCard))]
    [MemoryPackUnion(5, typeof(InventoryCategory))]
    [MemoryPackUnion(6, typeof(InventoryGesture))]
    [MemoryPackUnion(7, typeof(InventoryLSL))]
    [MemoryPackUnion(8, typeof(InventoryLandmark))]
    [MemoryPackUnion(9, typeof(InventoryMaterial))]
    [MemoryPackUnion(10, typeof(InventoryNotecard))]
    [MemoryPackUnion(11, typeof(InventoryObject))]
    [MemoryPackUnion(12, typeof(InventorySettings))]
    [MemoryPackUnion(13, typeof(InventorySnapshot))]
    [MemoryPackUnion(14, typeof(InventorySound))]
    [MemoryPackUnion(15, typeof(InventoryTexture))]
    [MemoryPackUnion(16, typeof(InventoryWearable))]
#endif
    public abstract partial class InventoryBase : ISerializable
    {
        /// <summary><see cref="OpenMetaverse.UUID"/> of item/folder</summary>
        public UUID UUID;
        /// <summary><see cref="OpenMetaverse.UUID"/> of parent folder</summary>
        public UUID ParentUUID;
        /// <summary>Name of item/folder</summary>
        public string Name;
        /// <summary>Item/Folder Owners <see cref="OpenMetaverse.UUID"/></summary>
        public UUID OwnerID;

        /// <summary>
        /// Constructor, takes an itemID as a parameter
        /// </summary>
        /// <param name="UUID">The <see cref="OpenMetaverse.UUID"/> of the item</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        protected InventoryBase(UUID UUID)
        {
            if (UUID == UUID.Zero)
                Logger.Log("Initializing an InventoryBase with UUID.Zero", Helpers.LogLevel.Warning);
            this.UUID = UUID;
        }

        /// <summary>
        /// Get object data
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("UUID", UUID);
            info.AddValue("ParentUUID", ParentUUID);
            info.AddValue("Name", Name);
            info.AddValue("OwnerID", OwnerID);
        }

        /// <summary>
        /// Inventory base ctor
        /// </summary>
        /// <param name="info"></param>
        /// <param name="ctxt"></param>
        protected InventoryBase(SerializationInfo info, StreamingContext ctxt)
        {
            UUID = (UUID)info.GetValue("UUID", typeof(UUID));
            ParentUUID = (UUID)info.GetValue("ParentUUID", typeof(UUID));
            Name = (string)info.GetValue("Name", typeof(string));
            OwnerID = (UUID)info.GetValue("OwnerID", typeof(UUID));
        }

        /// <summary>
        /// Generates a number corresponding to the value of the object to support the use of a hash table,
        /// suitable for use in hashing algorithms and data structures such as a hash table
        /// </summary>
        /// <returns>A Hashcode of all the combined InventoryBase fields</returns>
        public override int GetHashCode()
        {
            return UUID.GetHashCode() ^ ParentUUID.GetHashCode() ^ Name.GetHashCode() ^ OwnerID.GetHashCode();
        }

        /// <summary>
        /// Determine whether the specified <see cref="InventoryBase"/> object is equal to the current object
        /// </summary>
        /// <param name="obj">InventoryBase object to compare against</param>
        /// <returns>true if objects are the same</returns>
        public override bool Equals(object obj)
        {
            return obj is InventoryBase inv && Equals(inv);
        }

        /// <summary>
        /// Determine whether the specified <see cref="InventoryBase"/> object is equal to the current object
        /// </summary>
        /// <param name="o">InventoryBase object to compare against</param>
        /// <returns>true if objects are the same</returns>
        public virtual bool Equals(InventoryBase o)
        {
            return o.UUID == UUID
                && o.ParentUUID == ParentUUID
                && o.Name == Name
                && o.OwnerID == OwnerID;
        }

        /// <summary>
        /// Convert inventory to OSD
        /// </summary>
        /// <returns>OSD representation</returns>
        public abstract OSD GetOSD();
    }

    /// <summary>
    /// An Item in Inventory
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryItem : InventoryBase
    {
        public override string ToString()
        {
            return $"{AssetType} {AssetUUID} ({InventoryType} {UUID}) '{Name}'/'{Description}' {Permissions}";
        }
        /// <summary><see cref="UUID"/> of the underlying asset</summary>
        public UUID AssetUUID;
        /// <summary>Combined <see cref="OpenMetaverse.Permissions"/> of the item</summary>
        public Permissions Permissions;
        /// <summary><see cref="OpenMetaverse.AssetType"/> of the underlying asset</summary>
        public AssetType AssetType;
        /// <summary><see cref="OpenMetaverse.InventoryType"/> of the item</summary>
        public InventoryType InventoryType;
        /// <summary><see cref="UUID"/> of the creator of the item</summary>
        public UUID CreatorID;
        /// <summary>Description of the item</summary>
        public string Description;
        /// <summary><see cref="Group"/>s <see cref="UUID"/> the item is owned by</summary>
        public UUID GroupID;
        /// <summary>If true, item is owned by a group</summary>
        public bool GroupOwned;
        /// <summary>Price the item can be purchased for</summary>
        public int SalePrice;
        /// <summary><see cref="OpenMetaverse.SaleType"/> of the item</summary>
        public SaleType SaleType;
        /// <summary>Combined flags from <see cref="InventoryItemFlags"/></summary>
        public uint Flags;
        /// <summary>Time and date the inventory item was created, stored as
        /// UTC (Coordinated Universal Time)</summary>
        public DateTime CreationDate;
        /// <summary>Used to update the AssetID in requests sent to the server</summary>
        public UUID TransactionID;
        /// <summary><see cref="UUID"/> of the previous owner of the item</summary>
        public UUID LastOwnerID;

        /// <summary>
        ///  Construct a new InventoryItem object
        /// </summary>
        /// <param name="UUID">The <see cref="UUID"/> of the item</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryItem(UUID UUID)
            : base(UUID) { }

        /// <summary>
        /// Construct a new InventoryItem object of a specific Type
        /// </summary>
        /// <param name="type">The type of item from <see cref="T:OpenMetaverse.InventoryType" /></param>
        /// <param name="itemID"><see cref="T:OpenMetaverse.UUID" /> of the item</param>
        public InventoryItem(InventoryType type, UUID itemID) : base(itemID) { InventoryType = type; }

        /// <summary>
        /// Indicates inventory item is a link
        /// </summary>
        /// <returns>True if inventory item is a link to another inventory item</returns>
        public bool IsLink()
        {
            return AssetType == AssetType.Link || AssetType == AssetType.LinkFolder;
        }

        /// <inheritdoc />
        /// <summary>
        /// Get object data
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("AssetUUID", AssetUUID, typeof(UUID));
            info.AddValue("Permissions", Permissions, typeof(Permissions));
            info.AddValue("AssetType", AssetType);
            info.AddValue("InventoryType", InventoryType);
            info.AddValue("CreatorID", CreatorID);
            info.AddValue("Description", Description);
            info.AddValue("GroupID", GroupID);
            info.AddValue("GroupOwned", GroupOwned);
            info.AddValue("SalePrice", SalePrice);
            info.AddValue("SaleType", SaleType);
            info.AddValue("Flags", Flags);
            info.AddValue("CreationDate", CreationDate);
            info.AddValue("LastOwnerID", LastOwnerID);
        }

        /// <summary>
        /// Inventory item ctor
        /// </summary>
        /// <param name="info"></param>
        /// <param name="ctxt"></param>
        public InventoryItem(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            AssetUUID = (UUID)info.GetValue("AssetUUID", typeof(UUID));
            Permissions = (Permissions)info.GetValue("Permissions", typeof(Permissions));
            AssetType = (AssetType)info.GetValue("AssetType", typeof(AssetType));
            InventoryType = (InventoryType)info.GetValue("InventoryType", typeof(InventoryType));
            CreatorID = (UUID)info.GetValue("CreatorID", typeof(UUID));
            Description = (string)info.GetValue("Description", typeof(string));
            GroupID = (UUID)info.GetValue("GroupID", typeof(UUID));
            GroupOwned = (bool)info.GetValue("GroupOwned", typeof(bool));
            SalePrice = (int)info.GetValue("SalePrice", typeof(int));
            SaleType = (SaleType)info.GetValue("SaleType", typeof(SaleType));
            Flags = (uint)info.GetValue("Flags", typeof(uint));
            CreationDate = (DateTime)info.GetValue("CreationDate", typeof(DateTime));
            LastOwnerID = (UUID)info.GetValue("LastOwnerID", typeof(UUID));
        }

        /// <summary>
        /// Generates a number corresponding to the value of the object to support the use of a hash table.
        /// Suitable for use in hashing algorithms and data structures such as a hash table
        /// </summary>
        /// <returns>A Hashcode of all the combined InventoryItem fields</returns>
        public override int GetHashCode()
        {
            return AssetUUID.GetHashCode() ^ Permissions.GetHashCode() ^ AssetType.GetHashCode() ^
                InventoryType.GetHashCode() ^ Description.GetHashCode() ^ GroupID.GetHashCode() ^
                GroupOwned.GetHashCode() ^ SalePrice.GetHashCode() ^ SaleType.GetHashCode() ^
                Flags.GetHashCode() ^ CreationDate.GetHashCode() ^ LastOwnerID.GetHashCode();
        }

        /// <inheritdoc />
        /// <summary>
        /// Compares an object
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns>true if comparison object matches</returns>
        public override bool Equals(object obj)
        {
            return obj is InventoryItem item && Equals(item);
        }

        /// <inheritdoc />
        /// <summary>
        /// Determine whether the specified <see cref="T:OpenMetaverse.InventoryBase" /> object is equal to the current object
        /// </summary>
        /// <param name="o">The <see cref="T:OpenMetaverse.InventoryBase" /> object to compare against</param>
        /// <returns>true if objects are the same</returns>
        public override bool Equals(InventoryBase o)
        {
            return o is InventoryItem item && Equals(item);
        }

        /// <summary>
        /// Determine whether the specified <see cref="InventoryItem"/> object is equal to the current object
        /// </summary>
        /// <param name="o">The <see cref="InventoryItem"/> object to compare against</param>
        /// <returns>true if objects are the same</returns>
        public bool Equals(InventoryItem o)
        {
            return base.Equals(o)
                && o.AssetType == AssetType
                && o.AssetUUID == AssetUUID
                && o.CreationDate == CreationDate
                && o.Description == Description
                && o.Flags == Flags
                && o.GroupID == GroupID
                && o.GroupOwned == GroupOwned
                && o.InventoryType == InventoryType
                && o.Permissions.Equals(Permissions)
                && o.SalePrice == SalePrice
                && o.SaleType == SaleType
                && o.LastOwnerID == LastOwnerID;
        }

        /// <summary>
        /// Create InventoryItem from OSD
        /// </summary>
        /// <param name="data">OSD Data that makes up InventoryItem</param>
        /// <returns>Inventory item created</returns>
        public static InventoryItem FromOSD(OSD data)
        {
            OSDMap descItem = (OSDMap)data;

            InventoryType type = (InventoryType)descItem["inv_type"].AsInteger();
            if (type == InventoryType.Texture && (AssetType)descItem["type"].AsInteger() == AssetType.Object)
            {
                type = InventoryType.Attachment;
            }
            InventoryItem item = InventoryManager.CreateInventoryItem(type, descItem["item_id"]);

            item.ParentUUID = descItem["parent_id"];
            item.Name = descItem["name"];
            item.Description = descItem["desc"];
            item.OwnerID = descItem["agent_id"];
            item.ParentUUID = descItem["parent_id"];
            item.AssetUUID = descItem["asset_id"];
            item.AssetType = (AssetType)descItem["type"].AsInteger();
            item.CreationDate = Utils.UnixTimeToDateTime(descItem["created_at"]);
            item.Flags = descItem["flags"];

            OSDMap perms = (OSDMap)descItem["permissions"];
            item.CreatorID = perms["creator_id"];
            item.LastOwnerID = perms["last_owner_id"];
            item.Permissions = new Permissions(perms["base_mask"], perms["everyone_mask"], perms["group_mask"], perms["next_owner_mask"], perms["owner_mask"]);
            item.GroupOwned = perms["is_owner_group"];
            item.GroupID = perms["group_id"];

            OSDMap sale = (OSDMap)descItem["sale_info"];
            item.SalePrice = sale["sale_price"];
            item.SaleType = (SaleType)sale["sale_type"].AsInteger();

            return item;
        }

        /// <summary>
        /// Convert InventoryItem to OSD
        /// </summary>
        /// <returns>OSD representation of InventoryItem</returns>
        public override OSD GetOSD()
        {
            OSDMap map = new OSDMap
            {
                ["item_id"] = UUID,
                ["parent_id"] = ParentUUID,
                ["type"] = (sbyte)AssetType,
                ["inv_type"] = (sbyte)InventoryType,
                ["flags"] = Flags,
                ["name"] = Name,
                ["desc"] = Description,
                ["asset_id"] = AssetUUID,
                ["created_at"] = CreationDate
            };

            OSDMap perms = (OSDMap)Permissions.GetOSD();
            perms["creator_id"] = CreatorID;
            perms["last_owner_id"] = LastOwnerID;
            perms["is_owner_group"] = GroupOwned;
            perms["group_id"] = GroupID;
            map["permissions"] = perms;

            OSDMap sale = new OSDMap
            {
                ["sale_price"] = SalePrice,
                ["sale_type"] = (sbyte)SaleType
            };
            map["sale_info"] = sale;

            return map;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryTexture Class representing a graphical image
    /// </summary>
    /// <seealso cref="T:OpenMetaverse.Imaging.ManagedImage" />
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryTexture : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryTexture object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryTexture(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Texture;
        }

        /// <summary>
        /// Construct an InventoryTexture object from a serialization stream
        /// </summary>
        public InventoryTexture(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Texture;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySound Class representing a playable sound
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventorySound : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySound object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventorySound(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Sound;
        }

        /// <summary>
        /// Construct an InventorySound object from a serialization stream
        /// </summary>
        public InventorySound(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Sound;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryCallingCard Class, contains information on another avatar
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryCallingCard : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryCallingCard object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryCallingCard(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.CallingCard;
        }

        /// <summary>
        /// Construct an InventoryCallingCard object from a serialization stream
        /// </summary>
        public InventoryCallingCard(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.CallingCard;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryLandmark Class, contains details on a specific location
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryLandmark : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryLandmark object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryLandmark(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Landmark;
        }

        /// <summary>
        /// Construct an InventoryLandmark object from a serialization stream
        /// </summary>
        public InventoryLandmark(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Landmark;

        }

        /// <summary>
        /// Landmarks use the InventoryItemFlags struct and will have a flag of 1 set if they have been visited
        /// </summary>
        public bool LandmarkVisited
        {
            get => (Flags & 1) != 0;
            set
            {
                if (value) Flags |= 1;
                else Flags &= ~1u;
            }
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryObject Class contains details on a primitive or coalesced set of primitives
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryObject : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryObject object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryObject(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Object;
        }

        /// <summary>
        /// Construct an InventoryObject object from a serialization stream
        /// </summary>
        public InventoryObject(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Object;
        }

        /// <summary>
        /// Gets or sets the upper byte of the Flags value
        /// </summary>
        public InventoryItemFlags ItemFlags
        {
            get => (InventoryItemFlags)(Flags & ~0xFF);
            set => Flags = (uint)value | (Flags & 0xFF);
        }

        /// <summary>
        /// Gets or sets the object attachment point, the lower byte of the Flags value
        /// </summary>
        public AttachmentPoint AttachPoint
        {
            get => (AttachmentPoint)(Flags & 0xFF);
            set => Flags = (uint)value | (Flags & 0xFFFFFF00);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryNotecard Class, contains details on an encoded text document
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial  class InventoryNotecard : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryNotecard object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryNotecard(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Notecard;
        }

        /// <summary>
        /// Construct an InventoryNotecard object from a serialization stream
        /// </summary>
        public InventoryNotecard(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Notecard;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryCategory Class
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryCategory : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryCategory object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryCategory(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Category;
        }

        /// <summary>
        /// Construct an InventoryCategory object from a serialization stream
        /// </summary>
        public InventoryCategory(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Category;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryLSL Class, represents a Linden Scripting Language object
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryLSL : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryLSL object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryLSL(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.LSL;
        }

        /// <summary>
        /// Construct an InventoryLSL object from a serialization stream
        /// </summary>
        public InventoryLSL(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.LSL;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySnapshot Class, an image taken with the viewer
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventorySnapshot : InventoryItem
    {
        /// <inheritdoc />
        /// <summary>
        /// Construct an InventorySnapshot object
        /// </summary>
        /// <param name="UUID">A <see cref="T:OpenMetaverse.UUID" /> which becomes the 
        /// <seealso cref="T:OpenMetaverse.InventoryItem" /> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventorySnapshot(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Snapshot;
        }

        /// <summary>
        /// Construct an InventorySnapshot object from a serialization stream
        /// </summary>
        public InventorySnapshot(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Snapshot;
        }
    }

    /// <summary>
    /// InventoryAttachment Class, contains details on an attachable object
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryAttachment : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryAttachment object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryAttachment(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Attachment;
        }

        /// <summary>
        /// Construct an InventoryAttachment object from a serialization stream
        /// </summary>
        public InventoryAttachment(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Attachment;
        }

        /// <summary>
        /// Get the last AttachmentPoint this object was attached to
        /// </summary>
        public AttachmentPoint AttachmentPoint
        {
            get => (AttachmentPoint)Flags;
            set => Flags = (uint)value;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryWearable Class, details on a clothing item or body part
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryWearable : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryWearable object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryWearable(UUID UUID) : base(UUID) { InventoryType = InventoryType.Wearable; }

        /// <summary>
        /// Construct an InventoryWearable object from a serialization stream
        /// </summary>
        public InventoryWearable(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Wearable;
        }

        /// <summary>
        /// The <see cref="OpenMetaverse.WearableType"/>, Skin, Shape, Skirt, Etc
        /// </summary>
        public WearableType WearableType
        {
            get => (WearableType)Flags;
            set => Flags = (uint)value;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryAnimation Class, A bvh encoded object which animates an avatar
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryAnimation : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryAnimation object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryAnimation(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Animation;
        }

        /// <summary>
        /// Construct an InventoryAnimation object from a serialization stream
        /// </summary>
        public InventoryAnimation(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Animation;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryGesture Class, details on a series of animations, sounds, and actions
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryGesture : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryGesture object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryGesture(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Gesture;
        }

        /// <summary>
        /// Construct an InventoryGesture object from a serialization stream
        /// </summary>
        public InventoryGesture(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            InventoryType = InventoryType.Gesture;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySettings, LLSD settings blob as an asset
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventorySettings : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySettings object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventorySettings(UUID UUID) : base(UUID)
        {
            InventoryType = InventoryType.Settings;
        }

        public InventorySettings(SerializationInfo info, StreamingContext ctxt) : base(info, ctxt)
        {
            InventoryType = InventoryType.Settings;
        }
    }
    
    /// <inheritdoc />
    /// <summary>
    /// InventoryMaterial, material as an asset
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryMaterial : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySettings object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryMaterial(UUID UUID) : base(UUID)
        {
            InventoryType = InventoryType.Settings;
        }

        public InventoryMaterial(SerializationInfo info, StreamingContext ctxt) : base(info, ctxt)
        {
            InventoryType = InventoryType.Settings;
        }
    }
    
    /// <inheritdoc />
    /// <summary>
    /// A folder contains <see cref="T:OpenMetaverse.InventoryItem" />s and has certain attributes specific 
    /// to itself
    /// </summary>
    [Serializable]
#if NET7_0_OR_GREATER
    [MemoryPackable]
#endif
    public partial class InventoryFolder : InventoryBase
    {
        /// <summary>The Preferred <see cref="T:OpenMetaverse.FolderType"/> for a folder.</summary>
        public FolderType PreferredType;
        /// <summary>The Version of this folder</summary>
        public int Version;
        /// <summary>Number of child items this folder contains.</summary>
        public int DescendentCount;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="UUID">UUID of the folder</param>
#if NET7_0_OR_GREATER
        [MemoryPackConstructor]
#endif
        public InventoryFolder(UUID UUID)
            : base(UUID)
        {
            PreferredType = FolderType.None;
            Version = 1;
            DescendentCount = 0;
        }

        /// <summary>
        /// Returns folder name
        /// </summary>
        /// <returns>Return folder name as string</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Get Serilization data for this InventoryFolder object
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("PreferredType", PreferredType, typeof(FolderType));
            info.AddValue("Version", Version);
            info.AddValue("DescendentCount", DescendentCount);
        }

        /// <summary>
        /// Construct an InventoryFolder object from a serialization stream
        /// </summary>
        public InventoryFolder(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
            PreferredType = (FolderType)info.GetValue("PreferredType", typeof(FolderType));
            Version = (int)info.GetValue("Version", typeof(int));
            DescendentCount = (int)info.GetValue("DescendentCount", typeof(int));
        }

        /// <summary>
        /// Return int hash code
        /// </summary>
        /// <returns>Hash code as integer</returns>
        public override int GetHashCode()
        {
            return PreferredType.GetHashCode() ^ Version.GetHashCode() ^ DescendentCount.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryFolder folder && Equals(folder);
        }

        public override bool Equals(InventoryBase o)
        {
            return o is InventoryFolder folder && Equals(folder);
        }

        public bool Equals(InventoryFolder o)
        {
            return base.Equals(o as InventoryBase)
                && o.DescendentCount == DescendentCount
                && o.PreferredType == PreferredType
                && o.Version == Version;
        }

        /// <summary>
        /// Create InventoryFolder from OSD
        /// </summary>
        /// <param name="data">OSD Data that makes up InventoryFolder</param>
        /// <returns>Inventory folder created</returns>
        public static InventoryFolder FromOSD(OSD data)
        {
            OSDMap res = (OSDMap)data;
            InventoryFolder folder = new InventoryFolder(res["item_id"].AsUUID())
            {
                UUID = res["item_id"].AsUUID(),
                ParentUUID = res["parent_id"].AsUUID(),
                PreferredType = (FolderType)(sbyte)res["type"].AsUInteger(),
                Name = res["name"]
            };
            return folder;
        }

        /// <summary>
        /// Convert InventoryFolder to OSD
        /// </summary>
        /// <returns>OSD representation of InventoryFolder</returns>
        public override OSD GetOSD()
        {
            OSDMap res = new OSDMap(4)
            {
                ["item_id"] = UUID,
                ["parent_id"] = ParentUUID,
                ["type"] = (sbyte)PreferredType,
                ["name"] = Name
            };
            return res;
        }
    }
}
