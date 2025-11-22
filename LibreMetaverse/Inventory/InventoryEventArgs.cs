/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
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

namespace OpenMetaverse
{
    public class InventoryObjectOfferedEventArgs : EventArgs
    {
        /// <summary>Set to true to accept offer, false to decline it</summary>
        public bool Accept { get; set; }
        /// <summary>The folder to accept the inventory into, if null default folder for <see cref="AssetType"/> will be used</summary>
        public UUID FolderID { get; set; }

        public InstantMessage Offer { get; }

        public AssetType AssetType { get; }

        public UUID ObjectID { get; }

        public bool FromTask { get; }

        public InventoryObjectOfferedEventArgs(InstantMessage offerDetails, AssetType type, UUID objectID, bool fromTask, UUID folderID)
        {
            this.Accept = false;
            this.FolderID = folderID;
            this.Offer = offerDetails;
            this.AssetType = type;
            this.ObjectID = objectID;
            this.FromTask = fromTask;
        }
    }

    public class FolderUpdatedEventArgs : EventArgs
    {
        public UUID FolderID { get; }

        public bool Success { get; }

        public FolderUpdatedEventArgs(UUID folderID, bool success)
        {
            this.FolderID = folderID;
            this.Success = success;
        }
    }

    public class ItemReceivedEventArgs : EventArgs
    {
        public InventoryItem Item { get; }

        public ItemReceivedEventArgs(InventoryItem item)
        {
            this.Item = item;
        }
    }

    public class FindObjectByPathReplyEventArgs : EventArgs
    {
        public string Path { get; }

        public UUID InventoryObjectID { get; }

        public FindObjectByPathReplyEventArgs(string path, UUID inventoryObjectID)
        {
            this.Path = path;
            this.InventoryObjectID = inventoryObjectID;
        }
    }

    /// <summary>
    /// Callback when an inventory object is accepted and received from a
    /// task inventory. This is the callback in which you actually get
    /// the ItemID, as in ObjectOfferedCallback it is null when received
    /// from a task.
    /// </summary>
    public class TaskItemReceivedEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public UUID FolderID { get; }

        public UUID CreatorID { get; }

        public UUID AssetID { get; }

        public InventoryType Type { get; }

        public TaskItemReceivedEventArgs(UUID itemID, UUID folderID, UUID creatorID, UUID assetID, InventoryType type)
        {
            this.ItemID = itemID;
            this.FolderID = folderID;
            this.CreatorID = creatorID;
            this.AssetID = assetID;
            this.Type = type;
        }
    }

    public class TaskInventoryReplyEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public short Serial { get; }

        public string AssetFilename { get; }

        public TaskInventoryReplyEventArgs(UUID itemID, short serial, string assetFilename)
        {
            this.ItemID = itemID;
            this.Serial = serial;
            this.AssetFilename = assetFilename;
        }
    }

    public class SaveAssetToInventoryEventArgs : EventArgs
    {
        public UUID ItemID { get; }

        public UUID NewAssetID { get; }

        public SaveAssetToInventoryEventArgs(UUID itemID, UUID newAssetID)
        {
            this.ItemID = itemID;
            this.NewAssetID = newAssetID;
        }
    }

    public class ScriptRunningReplyEventArgs : EventArgs
    {
        public UUID ObjectID { get; }

        public UUID ScriptID { get; }

        public bool IsMono { get; }

        public bool IsRunning { get; }

        public ScriptRunningReplyEventArgs(UUID objectID, UUID sctriptID, bool isMono, bool isRunning)
        {
            this.ObjectID = objectID;
            this.ScriptID = sctriptID;
            this.IsMono = isMono;
            this.IsRunning = isRunning;
        }
    }
}
