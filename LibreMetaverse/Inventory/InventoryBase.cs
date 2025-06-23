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
using OpenMetaverse.StructuredData;
using MessagePack;

namespace OpenMetaverse
{
    /// <summary>
    /// Base Class for Inventory Items
    /// </summary>
    [MessagePackObject]
    [Union(0, typeof(InventoryFolder))]
    [Union(1, typeof(InventoryItem))]
    [Union(2, typeof(InventoryAnimation))]
    [Union(3, typeof(InventoryAttachment))]
    [Union(4, typeof(InventoryCallingCard))]
    [Union(5, typeof(InventoryCategory))]
    [Union(6, typeof(InventoryGesture))]
    [Union(7, typeof(InventoryLSL))]
    [Union(8, typeof(InventoryLandmark))]
    [Union(9, typeof(InventoryMaterial))]
    [Union(10, typeof(InventoryNotecard))]
    [Union(11, typeof(InventoryObject))]
    [Union(12, typeof(InventorySettings))]
    [Union(13, typeof(InventorySnapshot))]
    [Union(14, typeof(InventorySound))]
    [Union(15, typeof(InventoryTexture))]
    [Union(16, typeof(InventoryWearable))]
    public abstract partial class InventoryBase
    {
        /// <summary><see cref="OpenMetaverse.UUID"/> of item/folder</summary>
        [Key("UUID")]
        public UUID UUID;
        /// <summary><see cref="OpenMetaverse.UUID"/> of parent folder</summary>
        [Key("ParentUUID")]
        public UUID ParentUUID;
        /// <summary>Name of item/folder</summary>
        [Key("Name")]
        public string Name;
        /// <summary>Item/Folder Owners <see cref="OpenMetaverse.UUID"/></summary>
        [Key("OwnerID")]
        public UUID OwnerID;

        /// <summary>
        /// Constructor, takes an itemID as a parameter
        /// </summary>
        /// <param name="UUID">The <see cref="OpenMetaverse.UUID"/> of the item</param>
        protected InventoryBase(UUID UUID)
        {
            if (UUID == UUID.Zero)
                Logger.Log("Initializing an InventoryBase with UUID.Zero", Helpers.LogLevel.Warning);
            this.UUID = UUID;
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
    [MessagePackObject]
    public partial class InventoryItem : InventoryBase
    {
        public override string ToString()
        {
            return $"{AssetType} {AssetUUID} ({InventoryType} {UUID}) '{Name}'/'{Description}' {Permissions}";
        }
        /// <summary><see cref="UUID"/> of the underlying asset</summary>
        [Key("AssetUUID")]
        public UUID AssetUUID;
        /// <summary>Combined <see cref="OpenMetaverse.Permissions"/> of the item</summary>
        [Key("Permissions")]
        public Permissions Permissions;
        /// <summary><see cref="OpenMetaverse.AssetType"/> of the underlying asset</summary>
        [Key("AssetType")]
        public AssetType AssetType;
        /// <summary><see cref="OpenMetaverse.InventoryType"/> of the item</summary>
        [Key("InventoryType")]
        public InventoryType InventoryType;
        /// <summary><see cref="UUID"/> of the creator of the item</summary>
        [Key("CreatorID")]
        public UUID CreatorID;
        /// <summary>Description of the item</summary>
        [Key("Description")]
        public string Description;
        /// <summary><see cref="Group"/>s <see cref="UUID"/> the item is owned by</summary>
        [Key("GroupID")]
        public UUID GroupID;
        /// <summary>If true, item is owned by a group</summary>
        [Key("GroupOwned")]
        public bool GroupOwned;
        /// <summary>Price the item can be purchased for</summary>
        [Key("SalePrice")]
        public int SalePrice;
        /// <summary><see cref="OpenMetaverse.SaleType"/> of the item</summary>
        [Key("SaleType")]
        public SaleType SaleType;
        /// <summary>Combined flags from <see cref="InventoryItemFlags"/></summary>
        [Key("Flags")]
        public uint Flags;

        /// <summary>Time and date the inventory item was created, stored as
        /// UTC (Coordinated Universal Time)</summary>
        [Key("CreationDate")]
        public DateTime CreationDate;
        /// <summary>Used to update the AssetID in requests sent to the server</summary>
        [Key("TransactionID")]
        public UUID TransactionID;
        /// <summary><see cref="UUID"/> of the previous owner of the item</summary>
        [Key("LastOwnerID")]
        public UUID LastOwnerID;

        /// <summary>inventoryID that this item points to, else this item's inventoryID</summary>
        [IgnoreMember]
        public UUID ActualUUID => IsLink() ? AssetUUID : UUID;

        /// <summary>
        ///  Construct a new InventoryItem object
        /// </summary>
        /// <param name="UUID">The <see cref="UUID"/> of the item</param>
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
            /*
             * Objects that have been attached in-world prior to being stored on the
             * asset server are stored with the InventoryType of 0 (Texture)
             * instead of 17 (Attachment)
             *
             * This corrects that behavior by forcing Object Asset types that have an
             * invalid InventoryType with the proper InventoryType of Attachment.
             */
            InventoryType type = (InventoryType)descItem["inv_type"].AsInteger();
            if (type == InventoryType.Texture &&
                ((AssetType)descItem["type"].AsInteger() == AssetType.Object
                 || (AssetType)descItem["type"].AsInteger() == AssetType.Mesh))
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
        /// Update InventoryItem from new OSD data
        /// </summary>
        /// <param name="data">Data to update in <see cref="OSDMap"/> format</param>
        public void Update(OSDMap data)
        {
            if (data.ContainsKey("item_id"))
            {
                UUID = data["item_id"].AsUUID();
            }
            if (data.ContainsKey("parent_id"))
            {
                ParentUUID = data["parent_id"].AsUUID();
            }
            if (data.ContainsKey("agent_id"))
            {
                OwnerID = data["agent_id"].AsUUID();
            }
            if (data.ContainsKey("name"))
            {
                Name = data["name"].AsString();
            }
            if (data.ContainsKey("desc"))
            {
                Description = data["desc"].AsString();
            }
            if (data.TryGetValue("permissions", out var permissions))
            {
                Permissions = Permissions.FromOSD(permissions);
            }
            if (data.TryGetValue("sale_info", out var saleInfo))
            {
                OSDMap sale = (OSDMap)saleInfo;
                SalePrice = sale["sale_price"].AsInteger(); 
                SaleType = (SaleType)sale["sale_type"].AsInteger();
            }
            if (data.ContainsKey("shadow_id"))
            {
                    AssetUUID = InventoryManager.DecryptShadowID(data["shadow_id"].AsUUID());
            }
            if (data.ContainsKey("asset_id"))
            {
                AssetUUID = data["asset_id"].AsUUID();
            }
            if (data.ContainsKey("linked_id"))
            {
                AssetUUID = data["linked_id"].AsUUID();
            }
            if (data.ContainsKey("type"))
            {
                AssetType type = AssetType.Unknown;
                switch (data["type"].Type)
                {
                    case OSDType.String:
                        type = Utils.StringToAssetType(data["type"].AsString());
                        break;
                    case OSDType.Integer:
                        type = (AssetType)data["type"].AsInteger();
                        break;
                }
                if (type != AssetType.Unknown)
                {
                    AssetType = type;
                }
            }
            if (data.ContainsKey("inv_type"))
            {
                InventoryType type = InventoryType.Unknown;
                switch (data["inv_type"].Type)
                {
                    case OSDType.String:
                        type = Utils.StringToInventoryType(data["inv_type"].AsString());
                        break;
                    case OSDType.Integer:
                        type = (InventoryType)data["inv_type"].AsInteger();
                        break;
                }
                if (type != InventoryType.Unknown)
                {
                    InventoryType = type;
                }
            }
            if (data.TryGetValue("flags", out var flags))
            {
                Flags = flags;
            }
            if (data.TryGetValue("created_at", out var createdAt))
            {
                CreationDate = Utils.UnixTimeToDateTime(createdAt);
            }
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
    [MessagePackObject]
    public partial class InventoryTexture : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryTexture object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryTexture(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Texture;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySound Class representing a playable sound
    /// </summary>
    [MessagePackObject]
    public partial class InventorySound : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySound object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventorySound(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Sound;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryCallingCard Class, contains information on another avatar
    /// </summary>
    [MessagePackObject]
    public partial class InventoryCallingCard : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryCallingCard object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryCallingCard(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.CallingCard;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryLandmark Class, contains details on a specific location
    /// </summary>
    [MessagePackObject]
    public partial class InventoryLandmark : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryLandmark object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryLandmark(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Landmark;
        }

        /// <summary>
        /// Landmarks use the InventoryItemFlags struct and will have a flag of 1 set if they have been visited
        /// </summary>
        [IgnoreMember]
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
    [MessagePackObject]
    public partial class InventoryObject : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryObject object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryObject(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Object;
        }

        /// <summary>
        /// Gets or sets the upper byte of the Flags value
        /// </summary>
        [IgnoreMember]
        public InventoryItemFlags ItemFlags
        {
            get => (InventoryItemFlags)(Flags & ~0xFF);
            set => Flags = (uint)value | (Flags & 0xFF);
        }

        /// <summary>
        /// Gets or sets the object attachment point, the lower byte of the Flags value
        /// </summary>
        [IgnoreMember]
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
    [MessagePackObject]
    public partial  class InventoryNotecard : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryNotecard object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryNotecard(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Notecard;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryCategory Class
    /// </summary>
    [MessagePackObject]
    public partial class InventoryCategory : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryCategory object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryCategory(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Category;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryLSL Class, represents a Linden Scripting Language object
    /// </summary>
    [MessagePackObject]
    public partial class InventoryLSL : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryLSL object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryLSL(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.LSL;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySnapshot Class, an image taken with the viewer
    /// </summary>
    [MessagePackObject]
    public partial class InventorySnapshot : InventoryItem
    {
        /// <inheritdoc />
        /// <summary>
        /// Construct an InventorySnapshot object
        /// </summary>
        /// <param name="UUID">A <see cref="T:OpenMetaverse.UUID" /> which becomes the 
        /// <seealso cref="T:OpenMetaverse.InventoryItem" /> objects UUID</param>
        public InventorySnapshot(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Snapshot;
        }
    }

    /// <summary>
    /// InventoryAttachment Class, contains details on an attachable object
    /// </summary>
    [MessagePackObject]
    public partial class InventoryAttachment : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryAttachment object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryAttachment(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Attachment;
        }

        /// <summary>
        /// Get the last AttachmentPoint this object was attached to
        /// </summary>
        [IgnoreMember]
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
    [MessagePackObject]
    public partial class InventoryWearable : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryWearable object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryWearable(UUID UUID) : base(UUID) { InventoryType = InventoryType.Wearable; }

        /// <summary>
        /// The <see cref="OpenMetaverse.WearableType"/>, Skin, Shape, Skirt, Etc
        /// </summary>
        [IgnoreMember]
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
    [MessagePackObject]
    public partial class InventoryAnimation : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryAnimation object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryAnimation(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Animation;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryGesture Class, details on a series of animations, sounds, and actions
    /// </summary>
    [MessagePackObject]
    public partial class InventoryGesture : InventoryItem
    {
        /// <summary>
        /// Construct an InventoryGesture object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryGesture(UUID UUID)
            : base(UUID)
        {
            InventoryType = InventoryType.Gesture;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventorySettings, LLSD settings blob as an asset
    /// </summary>
    [MessagePackObject]
    public partial class InventorySettings : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySettings object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventorySettings(UUID UUID) : base(UUID)
        {
            InventoryType = InventoryType.Settings;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// InventoryMaterial, material as an asset
    /// </summary>
    [MessagePackObject]
    public partial class InventoryMaterial : InventoryItem
    {
        /// <summary>
        /// Construct an InventorySettings object
        /// </summary>
        /// <param name="UUID">A <see cref="UUID"/> which becomes the 
        /// <seealso cref="InventoryItem"/> objects UUID</param>
        public InventoryMaterial(UUID UUID) : base(UUID)
        {
            InventoryType = InventoryType.Settings;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// A folder contains <see cref="T:OpenMetaverse.InventoryItem" />s and has certain attributes specific 
    /// to itself
    /// </summary>
    [MessagePackObject]
    public partial class InventoryFolder : InventoryBase
    {
        public const int VERSION_UNKNOWN = -1;

        /// <summary>The Preferred <see cref="T:OpenMetaverse.FolderType"/> for a folder.</summary>
        [Key("PreferredType")]
        public FolderType PreferredType;

        /// <summary>The Version of this folder</summary>
        [Key("Version")]
        public int Version;

        /// <summary>Number of child items this folder contains.</summary>
        [Key("DescendentCount")]
        public int DescendentCount;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="UUID">UUID of the folder</param>
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
            var res = (OSDMap)data;
            UUID folderId = res.TryGetValue("category_id", out var catId) ? catId : res["folder_id"];
            var folder = new InventoryFolder(folderId)
            {
                UUID = res["category_id"].AsUUID(),
                Version = res.ContainsKey("version") ? res["version"].AsInteger() : VERSION_UNKNOWN,
                ParentUUID = res["parent_id"].AsUUID(),
                DescendentCount = res["descendents"],
                OwnerID = res.TryGetValue("agent_id", out var agentId) ? agentId : res["owner_id"],
                PreferredType = (FolderType)(sbyte)res["type_default"].AsUInteger(),
                Name = res["name"]
            };
            return folder;
        }

        public void Update(OSDMap data)
        {
            if (data.ContainsKey("category_id"))
            {
                UUID = data["category_id"].AsUUID();
            }
            if (data.ContainsKey("version"))
            {
                Version = data["version"].AsInteger();
            }
            if (data.ContainsKey("parent_id"))
            {
                ParentUUID = data["parent_id"].AsUUID();
            }
            if (data.ContainsKey("type_default"))
            {
                PreferredType = (FolderType)(sbyte)data["type_default"].AsUInteger();
            }
            if (data.ContainsKey("descendents"))
            {
                DescendentCount = data["descendents"].AsInteger();
            }
            if (data.ContainsKey("owner_id"))
            {
                OwnerID = data["owner_id"].AsUUID();
            }
            if (data.ContainsKey("agent_id"))
            {
                OwnerID = data["agent_id"].AsUUID();
            }
            if (data.ContainsKey("name"))
            {
                Name = data["name"].AsString();
            }
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
                ["version"] = Version,
                ["parent_id"] = ParentUUID,
                ["type"] = (sbyte)PreferredType,
                ["name"] = Name
            };
            return res;
        }
    }
}
