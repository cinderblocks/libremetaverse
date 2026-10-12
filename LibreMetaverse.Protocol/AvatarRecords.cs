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

namespace LibreMetaverse
{
    /// <summary>
    /// Holds group information for Avatars such as those you might find in a profile
    /// </summary>
    public struct AvatarGroup
    {
        /// <summary>true of Avatar accepts group notices</summary>
        public bool AcceptNotices;
        /// <summary>Groups Key</summary>
        public UUID GroupID;
        /// <summary>Texture Key for groups insignia</summary>
        public UUID GroupInsigniaID;
        /// <summary>Name of the group</summary>
        public string GroupName;
        /// <summary>Powers avatar has in the group</summary>
        public GroupPowers GroupPowers;
        /// <summary>Avatars Currently selected title</summary>
        public string GroupTitle;
        /// <summary>true of Avatar has chosen to list this in their profile</summary>
        public bool ListInProfile;
    }

    /// <summary>
    /// Holds group information on an individual profile pick
    /// </summary>
    public struct ProfilePick
    {
        public UUID PickID;
        public UUID CreatorID;
        public bool TopPick;
        public UUID ParcelID;
        public string Name;
        public string Desc;
        public UUID SnapshotID;
        public string User;
        public string OriginalName;
        public string SimName;
        public Vector3d PosGlobal;
        public int SortOrder;
        public bool Enabled;
    }

    public struct ClassifiedAd
    {
        public UUID ClassifiedID;
        public UUID CreatorID;
        public uint CreationDate;
        public uint ExpirationDate;
        public uint Category;
        public UUID ParcelID;
        public uint ParentEstate;
        public UUID SnapShotID;
        public Vector3d Position;
        public string SimName;
        public string ParcelName;
        public byte ClassifiedFlags;
        public int Price;
        public string Name;
        public string Desc;
    }
}
