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
    [Flags]
    public enum InventorySortOrder : int
    {
        /// <summary>Sort by name</summary>
        ByName = 0,
        /// <summary>Sort by date</summary>
        ByDate = 1,
        /// <summary>Sort folders by name, regardless of whether items are
        /// sorted by name or date</summary>
        FoldersByName = 2,
        /// <summary>Place system folders at the top</summary>
        SystemFoldersToTop = 4
    }

    /// <summary>
    /// Possible destinations for DeRezObject request
    /// </summary>
    public enum DeRezDestination : byte
    {
        /// <summary></summary>
        AgentInventorySave = 0,
        /// <summary>Copy from in-world to agent inventory</summary>
        AgentInventoryCopy = 1,
        /// <summary>Derez to TaskInventory</summary>
        TaskInventory = 2,
        /// <summary></summary>
        Attachment = 3,
        /// <summary>Take Object</summary>
        AgentInventoryTake = 4,
        /// <summary>God force to inventory</summary>
        ForceToGodInventory = 5,
        /// <summary>Delete Object</summary>
        TrashFolder = 6,
        /// <summary>Put an avatar attachment into agent inventory</summary>
        AttachmentToInventory = 7,
        /// <summary></summary>
        AttachmentExists = 8,
        /// <summary>Return an object back to the owner's inventory</summary>
        ReturnToOwner = 9,
        /// <summary>Return a deeded object back to the last owner's inventory</summary>
        ReturnToLastOwner = 10
    }

    /// <summary>
    /// Upper half of the Flags field for inventory items
    /// </summary>
    [Flags]
    public enum InventoryItemFlags : uint
    {
        None = 0,
        /// <summary>Indicates that the NextOwner permission will be set to the
        /// most restrictive set of permissions found in the object set
        /// (including linkset items and object inventory items) on next rez</summary>
        ObjectSlamPerm = 0x100,
        /// <summary>Indicates that the object sale information has been
        /// changed</summary>
        ObjectSlamSale = 0x1000,
        /// <summary>If set, and a slam bit is set, indicates BaseMask will be overwritten on Rez</summary>
        ObjectOverwriteBase = 0x010000,
        /// <summary>If set, and a slam bit is set, indicates OwnerMask will be overwritten on Rez</summary>
        ObjectOverwriteOwner = 0x020000,
        /// <summary>If set, and a slam bit is set, indicates GroupMask will be overwritten on Rez</summary>
        ObjectOverwriteGroup = 0x040000,
        /// <summary>If set, and a slam bit is set, indicates EveryoneMask will be overwritten on Rez</summary>
        ObjectOverwriteEveryone = 0x080000,
        /// <summary>If set, and a slam bit is set, indicates NextOwnerMask will be overwritten on Rez</summary>
        ObjectOverwriteNextOwner = 0x100000,
        /// <summary>Indicates whether this object is composed of multiple
        /// items or not</summary>
        ObjectHasMultipleItems = 0x200000,
        /// <summary>Indicates that the asset is only referenced by this
        /// inventory item. If this item is deleted or updated to reference a
        /// new assetID, the asset can be deleted</summary>
        SharedSingleReference = 0x40000000,
    }
}
