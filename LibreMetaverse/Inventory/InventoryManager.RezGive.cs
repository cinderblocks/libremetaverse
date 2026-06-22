/*
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Threading.Tasks;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
            InventoryItem item)
        {
            // taskID not applicable here, pass UUID.Zero
            return RequestRezFromInventory(simulator, UUID.Zero, rotation, position, item, Client.Self.ActiveGroup,
                UUID.Random(), true);
        }

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        /// <param name="groupOwner">UUID of group to own the object</param>
        public UUID RequestRezFromInventory(Simulator simulator, Quaternion rotation, Vector3 position,
            InventoryItem item, UUID groupOwner)
        {
            // taskID not applicable here, pass UUID.Zero
            return RequestRezFromInventory(simulator, UUID.Zero, rotation, position, item, groupOwner, UUID.Random(), true);
        }

        /// <summary>
        /// Rez an object from inventory
        /// </summary>
        /// <param name="simulator">Simulator to place object in</param>
        /// <param name="taskID">TaskID (or UUID.Zero if not from a task)</param>
        /// <param name="rotation">Rotation of the object when rezzed</param>
        /// <param name="position">Vector of where to place object</param>
        /// <param name="item">InventoryItem object containing item details</param>
        /// <param name="groupOwner">UUID of group to own the object</param>        
        /// <param name="queryID">User defined queryID to correlate replies</param>
        /// <param name="rezSelected">If set to true, the CreateSelected flag
        /// will be set on the rezzed object</param>        
        public UUID RequestRezFromInventory(Simulator simulator, UUID taskID, Quaternion rotation, Vector3 position,
            InventoryItem item, UUID groupOwner, UUID queryID, bool rezSelected)
        {
            var add = new RezObjectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = groupOwner
                },
                RezData =
                {
                    FromTaskID = taskID,
                    BypassRaycast = 1,
                    RayStart = position,
                    RayEnd = position,
                    RayTargetID = UUID.Zero,
                    RayEndIsIntersection = false,
                    RezSelected = rezSelected,
                    RemoveItem = false,
                    ItemFlags = (uint) item.Flags,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask
                },
                InventoryData =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = queryID,
                    Type = (sbyte) item.InventoryType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate)
                }
            };

            Client.Network.SendPacket(add, simulator);

            // Remove from store if the item is no copy
            if (Store != null && Store.TryGetValue(item.UUID, out var storeItem) && storeItem is InventoryItem invItem)
            {
                if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                {
                    Store.RemoveNodeFor(invItem);
                }
            }

            return queryID;
        }

        /// <summary>
        /// DeRez an object from the simulator to the agents Objects folder in the agents Inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
        public void RequestDeRezToInventory(uint objectLocalID)
        {
            RequestDeRezToInventory(objectLocalID, DeRezDestination.AgentInventoryTake,
                Client.Inventory.FindFolderForType(AssetType.Object), UUID.Random());
        }

        /// <summary>
        /// DeRez an object from the simulator and return to inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <param name="destType">The type of destination from the <see cref="DeRezDestination"/> enum</param>
        /// <param name="destFolder">The destination inventory folders <see cref="UUID"/> -or-
        /// if DeRezzing object to a tasks Inventory, the Tasks <see cref="UUID"/></param>
        /// <param name="transactionID">The transaction ID for this request which
        /// can be used to correlate this request with other packets</param>
        /// <remarks>If objectLocalID is a child primitive in a linkset, the entire linkset will be derezzed</remarks>
        public void RequestDeRezToInventory(uint objectLocalID, DeRezDestination destType, UUID destFolder, UUID transactionID)
        {
            var take = new DeRezObjectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                AgentBlock = new DeRezObjectPacket.AgentBlockBlock
                {
                    GroupID = UUID.Zero,
                    Destination = (byte)destType,
                    DestinationID = destFolder,
                    PacketCount = 1,
                    PacketNumber = 1,
                    TransactionID = transactionID
                },
                ObjectData = new DeRezObjectPacket.ObjectDataBlock[1]
            };


            take.ObjectData[0] = new DeRezObjectPacket.ObjectDataBlock { ObjectLocalID = objectLocalID };

            Client.Network.SendPacket(take);
        }

        /// <summary>
        /// Empty a folder by removing all of its contents (including sub-folders)
        /// </summary>
        /// <param name="folderID">The folder to empty</param>
        /// <remarks> This will only remove the contents of the folder from the inventory. The folder itself will remain.</remarks>
        public void EmptyFolder(UUID folderID)
        {
            if (_Store == null)
            {
                Logger.Warn("Inventory store not initialized, cannot empty folder", Client);
                return;
            }

            if (Client.AisClient.IsAvailable)
            {
                _ = Client.AisClient.PurgeDescendentsAsync(folderID)
                    .ContinueWith(t => RemoveLocalUi(t.Status == TaskStatus.RanToCompletion && t.Result, folderID), TaskScheduler.Default);
            }
            else
            {
                var items = _Store.GetContents(folderID);
                var remItems = new List<UUID>();
                var remFolders = new List<UUID>();
                foreach (var item in items)
                {
                    if (item is InventoryFolder)
                    {
                        remFolders.Add(item.UUID);
                    }
                    else
                    {
                        remItems.Add(item.UUID);
                    }
                }

                var rem = new RemoveInventoryObjectsPacket
                {
                    AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID }
                };
                rem.ItemData = remItems.Select(id => new RemoveInventoryObjectsPacket.ItemDataBlock { ItemID = id }).ToArray();
                rem.FolderData = remFolders.Select(id => new RemoveInventoryObjectsPacket.FolderDataBlock { FolderID = id }).ToArray();
                Client.Network.SendPacket(rem);
            }
        }

        /// <summary>
        /// Rez an item from inventory to its previous simulator location
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="item"></param>
        /// <param name="queryID"></param>
        /// <returns></returns>
        public UUID RequestRestoreRezFromInventory(Simulator simulator, InventoryItem item, UUID queryID)
        {
            var add = new RezRestoreToWorldPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData =
                {
                    ItemID = item.UUID,
                    FolderID = item.ParentUUID,
                    CreatorID = item.CreatorID,
                    OwnerID = item.OwnerID,
                    GroupID = item.GroupID,
                    BaseMask = (uint) item.Permissions.BaseMask,
                    OwnerMask = (uint) item.Permissions.OwnerMask,
                    GroupMask = (uint) item.Permissions.GroupMask,
                    EveryoneMask = (uint) item.Permissions.EveryoneMask,
                    NextOwnerMask = (uint) item.Permissions.NextOwnerMask,
                    GroupOwned = item.GroupOwned,
                    TransactionID = queryID,
                    Type = (sbyte) item.InventoryType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate)
                }
            };

            Client.Network.SendPacket(add, simulator);

            return queryID;
        }

        /// <summary>
        /// Give an inventory item to another avatar
        /// </summary>
        /// <param name="itemID">The <see cref="UUID"/> of the item to give</param>
        /// <param name="itemName">The name of the item</param>
        /// <param name="assetType">The type of the item from the <see cref="AssetType"/> enum</param>
        /// <param name="recipient">The <see cref="UUID"/> of the recipient</param>
        /// <param name="doEffect">true to generate a beam-effect during transfer</param>
        public void GiveItem(UUID itemID, string itemName, AssetType assetType, UUID recipient,
            bool doEffect)
        {
            var bucket = new byte[17];
            bucket[0] = (byte)assetType;
            Buffer.BlockCopy(itemID.GetBytes(), 0, bucket, 1, 16);

            var currentSimId = Client.Network.CurrentSim?.ID ?? UUID.Zero;
            Client.Self.InstantMessage(
                    Client.Self.Name,
                    recipient,
                    itemName,
                    UUID.Random(),
                    InstantMessageDialog.InventoryOffered,
                    InstantMessageOnline.Online,
                    Client.Self.SimPosition,
                    currentSimId,
                    bucket);

            if (doEffect)
            {
                Client.Self.BeamEffect(Client.Self.AgentID, recipient, Vector3d.Zero,
                    Client.Settings.DefaultEffectColor, 1f, UUID.Random());
            }

            // Remove from store if the item is no copy
            if (Store != null && Store.TryGetValue(itemID, out var storeItem) && storeItem is InventoryItem invItem)
            {
                if ((invItem.Permissions.OwnerMask & PermissionMask.Copy) == PermissionMask.None)
                {
                    Store.RemoveNodeFor(invItem);
                }
            }
        }

    }
}

