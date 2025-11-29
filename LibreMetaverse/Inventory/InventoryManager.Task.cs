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
using System.Threading;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>
        /// Copy or move an <see cref="InventoryItem"/> from agent inventory to a task (primitive) inventory
        /// </summary>
        /// <param name="objectLocalID">The target object</param>
        /// <param name="item">The item to copy or move from inventory</param>
        /// <returns>Returns transaction id</returns>
        /// <remarks>For items with copy permissions a copy of the item is placed in the tasks inventory,
        /// for no-copy items the object is moved to the tasks inventory</remarks>
        public UUID UpdateTaskInventory(uint objectLocalID, InventoryItem item)
        {
            var transactionID = UUID.Random();

            var update = new UpdateTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                UpdateData =
                {
                    Key = 0,
                    LocalID = objectLocalID
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
                    TransactionID = transactionID,
                    Type = (sbyte) item.AssetType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate),
                    CRC = ItemCRC(item)
                }
            };


            Client.Network.SendPacket(update);

            return transactionID;
        }

        /// <summary>
        /// Update an existing notecard in a task (primitive) inventory (synchronous wrapper)
        /// </summary>
        /// <param name="data">Notecard asset bytes</param>
        /// <param name="notecardID">UUID of the notecard item</param>
        /// <param name="taskID">UUID of the task (primitive)</param>
        /// <param name="callback">Callback invoked when upload completes</param>
        [Obsolete("Use RequestUpdateNotecardTaskAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public void RequestUpdateNotecardTask(byte[] data, UUID notecardID, UUID taskID, InventoryUploadedAssetCallback callback)
        {
            try
            {
                RequestUpdateNotecardTaskAsync(data, notecardID, taskID, callback, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn($"RequestUpdateNotecardTask failed: {ex.Message}", ex, Client);
            }
        }

        /// <summary>
        /// Update an existing script in a task Inventory (synchronous wrapper)
        /// </summary>
        /// <param name="data">A byte[] array containing the encoded scripts contents</param>
        /// <param name="itemID">the itemID of the script</param>
        /// <param name="taskID">UUID of the prim containing the script</param>
        /// <param name="mono">if true, sets the script content to run on the mono interpreter</param>
        /// <param name="running">if true, sets the script to running</param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        [Obsolete("Use RequestUpdateScriptTaskAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public void RequestUpdateScriptTask(byte[] data, UUID itemID, UUID taskID, bool mono, bool running, ScriptUpdatedCallback callback, CancellationToken cancellationToken = default)
        {
            try
            {
                RequestUpdateScriptTaskAsync(data, itemID, taskID, mono, running, callback, cancellationToken).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Warn($"RequestUpdateScriptTask failed: {ex.Message}", ex, Client);
            }
        }

        /// <summary>
        /// Retrieve a listing of the items contained in a task (Primitive)
        /// </summary>
        /// <param name="objectID">The tasks <see cref="UUID"/></param>
        /// <param name="objectLocalID">The tasks simulator local ID</param>
        /// <param name="timeout">time to wait for reply from simulator</param>
        /// <returns>A list containing the inventory items inside the task or null
        /// if a timeout occurs</returns>
        /// <remarks>This request blocks until the response from the simulator arrives 
        /// before timeout is exceeded</remarks>
        [Obsolete("Use GetTaskInventoryAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public List<InventoryBase> GetTaskInventory(UUID objectID, uint objectLocalID, TimeSpan timeout)
        {
            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (timeout != TimeSpan.Zero)
                        cts.CancelAfter(timeout);

                    var task = GetTaskInventoryAsync(objectID, objectLocalID, cts.Token);
                    return task.GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                return new List<InventoryBase>();
            }
            catch (Exception)
            {
                return new List<InventoryBase>();
            }
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory from the 
        /// current simulator
        /// </summary>
        /// <param name="objectLocalID">The LocalID of the object</param>
        /// <see cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID)
        {
            RequestTaskInventory(objectLocalID, Client.Network.CurrentSim);
        }

        /// <summary>
        /// Request the contents of a tasks (primitives) inventory
        /// </summary>
        /// <param name="objectLocalID">The simulator Local ID of the object</param>
        /// <param name="simulator">A reference to the simulator object that contains the object</param>
        /// <see cref="TaskInventoryReply"/>
        public void RequestTaskInventory(uint objectLocalID, Simulator simulator)
        {
            var request = new RequestTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData = { LocalID = objectLocalID }
            };

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Move an item from a tasks (Primitive) inventory to the specified folder in the avatars inventory
        /// </summary>
        /// <param name="objectLocalID">LocalID of the object in the simulator</param>
        /// <param name="taskItemID">UUID of the task item to move</param>
        /// <param name="inventoryFolderID">The ID of the destination folder in this agents inventory</param>
        /// <param name="simulator">Simulator Object</param>
        /// <remarks>Raises the <see cref="OnTaskItemReceived"/> event</remarks>
        public void MoveTaskInventory(uint objectLocalID, UUID taskItemID, UUID inventoryFolderID, Simulator simulator)
        {
            var request = new MoveTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    FolderID = inventoryFolderID
                },
                InventoryData =
                {
                    ItemID = taskItemID,
                    LocalID = objectLocalID
                }
            };

            Client.Network.SendPacket(request, simulator);
        }

        /// <summary>
        /// Remove an item from an objects (Prim) Inventory
        /// </summary>
        /// <param name="objectLocalID">LocalID of the object in the simulator</param>
        /// <param name="taskItemID">UUID of the task item to remove</param>
        /// <param name="simulator">Simulator Object</param>
        /// <remarks>You can confirm the removal by comparing the tasks inventory serial before and after the 
        /// request with the <see cref="RequestTaskInventory"/> request combined with
        /// the <see cref="TaskInventoryReply"/> event</remarks>
        public void RemoveTaskInventory(uint objectLocalID, UUID taskItemID, Simulator simulator)
        {
            var remove = new RemoveTaskInventoryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                InventoryData =
                {
                    ItemID = taskItemID,
                    LocalID = objectLocalID
                }
            };

            Client.Network.SendPacket(remove, simulator);
        }

        /// <summary>
        /// Copy an InventoryScript item from the Agents Inventory into a primitives task inventory
        /// </summary>
        /// <param name="objectLocalID">An unsigned integer representing a primitive being simulated</param>
        /// <param name="item">An <see cref="InventoryItem"/> which represents a script object from the agents inventory</param>
        /// <param name="enableScript">true to set the scripts running state to enabled</param>
        /// <returns>A Unique Transaction ID</returns>
        /// <example>
        /// The following example shows the basic steps necessary to copy a script from the agents inventory into a tasks inventory
        /// and assumes the script exists in the agents inventory.
        /// <code>
        ///    uint primID = 95899503; // Fake prim ID
        ///    UUID scriptID = UUID.Parse("92a7fe8a-e949-dd39-a8d8-1681d8673232"); // Fake Script UUID in Inventory
        ///
        ///    Client.Inventory.FolderContents(Client.Inventory.FindFolderForType(AssetType.LSLText), Client.Self.AgentID, 
        ///        false, true, InventorySortOrder.ByName, 10000);
        ///
        ///    Client.Inventory.RezScript(primID, (InventoryItem)Client.Inventory.Store[scriptID]);
        /// </code>
        /// </example>
        public UUID CopyScriptToTask(uint objectLocalID, InventoryItem item, bool enableScript)
        {
            var transactionID = UUID.Random();

            var ScriptPacket = new RezScriptPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                UpdateBlock =
                {
                    ObjectLocalID = objectLocalID,
                    Enabled = enableScript
                },
                InventoryBlock =
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
                    TransactionID = transactionID,
                    Type = (sbyte) item.AssetType,
                    InvType = (sbyte) item.InventoryType,
                    Flags = (uint) item.Flags,
                    SaleType = (byte) item.SaleType,
                    SalePrice = item.SalePrice,
                    Name = Utils.StringToBytes(item.Name),
                    Description = Utils.StringToBytes(item.Description),
                    CreationDate = (int) Utils.DateTimeToUnixTime(item.CreationDate),
                    CRC = ItemCRC(item)
                }
            };

            Client.Network.SendPacket(ScriptPacket);

            return transactionID;
        }


        /// <summary>
        /// Request the running status of a script contained in a task (primitive) inventory
        /// </summary>
        /// <param name="objectID">The ID of the primitive containing the script</param>
        /// <param name="scriptID">The ID of the script</param>
        /// <remarks>The <see cref="ScriptRunningReply"/> event can be used to obtain the results of the 
        /// request</remarks>
        /// <see cref="ScriptRunningReply"/>
        public void RequestGetScriptRunning(UUID objectID, UUID scriptID)
        {
            var request = new GetScriptRunningPacket
            {
                Script =
                {
                    ObjectID = objectID,
                    ItemID = scriptID
                }
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Send a request to set the running state of a script contained in a task (primitive) inventory
        /// </summary>
        /// <param name="objectID">The ID of the primitive containing the script</param>
        /// <param name="scriptID">The ID of the script</param>
        /// <param name="running">true to set the script running, false to stop a running script</param>
        /// <remarks>To verify the change you can use the <see cref="RequestGetScriptRunning"/> method combined
        /// with the <see cref="ScriptRunningReply"/> event</remarks>
        public void RequestSetScriptRunning(UUID objectID, UUID scriptID, bool running)
        {
            var request = new SetScriptRunningPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Script =
                {
                    Running = running,
                    ItemID = scriptID,
                    ObjectID = objectID
                }
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Parse the results of a RequestTaskInventory() response
        /// </summary>
        /// <param name="taskData">A string which contains the data from the task reply</param>
        /// <returns>A List containing the items contained within the tasks inventory</returns>
        public static List<InventoryBase> ParseTaskInventory(string taskData)
        {
            var items = new List<InventoryBase>();
            var lineNum = 0;
            var lines = taskData.Replace("\r\n", "\n").Split('\n');

            while (lineNum < lines.Length)
            {
                if (ParseLine(lines[lineNum++], out var key, out var value))
                {
                    if (key == "inv_object")
                    {
                        #region inv_object

                        // In practice this appears to only be used for folders
                        var itemID = UUID.Zero;
                        var parentID = UUID.Zero;
                        var name = string.Empty;
                        var assetType = AssetType.Unknown;

                        while (lineNum < lines.Length)
                        {
                            if (ParseLine(lines[lineNum++], out key, out value))
                            {
                                if (key == "{")
                                {
                                    continue;
                                }
                                else
                                {
                                    if (key == "}")
                                    {
                                        break;
                                    }

                                    switch (key)
                                    {
                                        case "obj_id":
                                            UUID.TryParse(value, out itemID);
                                            break;
                                        case "parent_id":
                                            UUID.TryParse(value, out parentID);
                                            break;
                                        case "type":
                                            assetType = Utils.StringToAssetType(value);
                                            break;
                                        case "name":
                                            name = value.Substring(0, value.IndexOf('|'));
                                            break;
                                    }
                                }
                            }
                        }

                        if (assetType == AssetType.Folder)
                        {
                            var folder = new InventoryFolder(itemID)
                            {
                                Name = name,
                                ParentUUID = parentID
                            };

                            items.Add(folder);
                        }
                        else
                        {
                            var item = new InventoryItem(itemID)
                            {
                                Name = name,
                                ParentUUID = parentID,
                                AssetType = assetType
                            };

                            items.Add(item);
                        }

                        #endregion inv_object
                    }
                    else if (key == "inv_item")
                    {
                        #region inv_item

                        // Any inventory item that links to an assetID, has permissions, etc
                        var itemID = UUID.Zero;
                        var assetID = UUID.Zero;
                        var parentID = UUID.Zero;
                        var creatorID = UUID.Zero;
                        var ownerID = UUID.Zero;
                        var lastOwnerID = UUID.Zero;
                        var groupID = UUID.Zero;
                        var groupOwned = false;
                        var name = string.Empty;
                        var desc = string.Empty;
                        var assetType = AssetType.Unknown;
                        var inventoryType = InventoryType.Unknown;
                        var creationDate = Utils.Epoch;
                        uint flags = 0;
                        var perms = Permissions.NoPermissions;
                        var saleType = SaleType.Not;
                        var salePrice = 0;

                        while (lineNum < lines.Length)
                        {
                            if (ParseLine(lines[lineNum++], out key, out value))
                            {
                                if (key == "{")
                                {
                                    continue;
                                }

                                if (key == "}")
                                {
                                    break;
                                }
                                switch (key)
                                {
                                    case "item_id":
                                        UUID.TryParse(value, out itemID);
                                        break;
                                    case "parent_id":
                                        UUID.TryParse(value, out parentID);
                                        break;
                                    case "permissions":
                                        {
                                            #region permissions

                                            while (lineNum < lines.Length)
                                            {
                                                if (ParseLine(lines[lineNum++], out key, out value))
                                                {
                                                    if (key == "{")
                                                    {
                                                        continue;
                                                    }

                                                    if (key == "}")
                                                    {
                                                        break;
                                                    }

                                                    switch (key)
                                                    {
                                                        case "creator_mask":
                                                            {
                                                                // Deprecated
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.BaseMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "base_mask":
                                                            {
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.BaseMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "owner_mask":
                                                            {
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.OwnerMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "group_mask":
                                                            {
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.GroupMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "everyone_mask":
                                                            {
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.EveryoneMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "next_owner_mask":
                                                            {
                                                                if (Utils.TryParseHex(value, out var val))
                                                                    perms.NextOwnerMask = (PermissionMask)val;
                                                                break;
                                                            }
                                                        case "creator_id":
                                                            UUID.TryParse(value, out creatorID);
                                                            break;
                                                        case "owner_id":
                                                            UUID.TryParse(value, out ownerID);
                                                            break;
                                                        case "last_owner_id":
                                                            UUID.TryParse(value, out lastOwnerID);
                                                            break;
                                                        case "group_id":
                                                            UUID.TryParse(value, out groupID);
                                                            break;
                                                        case "group_owned":
                                                            {
                                                                if (uint.TryParse(value, out var val))
                                                                    groupOwned = (val != 0);
                                                                break;
                                                            }
                                                    }
                                                }
                                            }

                                            #endregion permissions

                                            break;
                                        }
                                    case "sale_info":
                                        {
                                            #region sale_info

                                            while (lineNum < lines.Length)
                                            {
                                                if (ParseLine(lines[lineNum++], out key, out value))
                                                {
                                                    if (key == "{")
                                                    {
                                                        continue;
                                                    }

                                                    if (key == "}")
                                                    {
                                                        break;
                                                    }

                                                    switch (key)
                                                    {
                                                        case "sale_type":
                                                            saleType = Utils.StringToSaleType(value);
                                                            break;
                                                        case "sale_price":
                                                            int.TryParse(value, out salePrice);
                                                            break;
                                                    }
                                                }
                                            }

                                            #endregion sale_info

                                            break;
                                        }
                                    case "shadow_id":
                                        {
                                            if (UUID.TryParse(value, out var shadowID))
                                                assetID = DecryptShadowID(shadowID);
                                            break;
                                        }
                                    case "asset_id":
                                        UUID.TryParse(value, out assetID);
                                        break;
                                    case "type":
                                        assetType = Utils.StringToAssetType(value);
                                        break;
                                    case "inv_type":
                                        inventoryType = Utils.StringToInventoryType(value);
                                        break;
                                    case "flags":
                                        uint.TryParse(value, out flags);
                                        break;
                                    case "name":
                                        name = value.Substring(0, value.IndexOf('|'));
                                        break;
                                    case "desc":
                                        desc = value.Substring(0, value.IndexOf('|'));
                                        break;
                                    case "creation_date":
                                        {
                                            if (uint.TryParse(value, out var timestamp))
                                                creationDate = Utils.UnixTimeToDateTime(timestamp);
                                            else
                                                Logger.Warn($"Failed to parse creation_date: {value}");
                                            break;
                                        }
                                }
                            }
                        }

                        var item = CreateInventoryItem(inventoryType, itemID);
                        item.AssetUUID = assetID;
                        item.AssetType = assetType;
                        item.CreationDate = creationDate;
                        item.CreatorID = creatorID;
                        item.Description = desc;
                        item.Flags = flags;
                        item.GroupID = groupID;
                        item.GroupOwned = groupOwned;
                        item.Name = name;
                        item.OwnerID = ownerID;
                        item.LastOwnerID = lastOwnerID;
                        item.ParentUUID = parentID;
                        item.Permissions = perms;
                        item.SalePrice = salePrice;
                        item.SaleType = saleType;

                        items.Add(item);

                        #endregion inv_item
                    }
                    else
                    {
                        Logger.Error($"Unrecognized token {key} in: " + Environment.NewLine + taskData);
                    }
                }
            }

            return items;
        }
    }
}

