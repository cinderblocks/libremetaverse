/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC
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

namespace LibreMetaverse
{
    /// <summary>
    /// Parcel ownership status
    /// </summary>
    public enum ParcelStatus : sbyte
    {
        /// <summary>Placeholder</summary>
        None = -1,
        /// <summary>Parcel is leased (owned) by an avatar or group</summary>
        Leased = 0,
        /// <summary>Parcel is in process of being leased (purchased) by an avatar or group</summary>
        LeasePending = 1,
        /// <summary>Parcel has been abandoned back to Governor Linden</summary>
        Abandoned = 2
    }

    /// <summary>
    /// The result of a request for parcel properties
    /// </summary>
    public enum ParcelResult : int
    {
        /// <summary>No matches were found for the request</summary>
        NoData = -1,
        /// <summary>Request matched a single parcel</summary>
        Single = 0,
        /// <summary>Request matched multiple parcels</summary>
        Multiple = 1
    }

    /// <summary>
    /// Various parcel properties
    /// </summary>
    [Flags]
    public enum ParcelFlags : uint
    {
        /// <summary>No flags set</summary>
        None = 0,
        /// <summary>Allow avatars to fly (a client-side only restriction)</summary>
        AllowFly = 1 << 0,
        /// <summary>Allow foreign scripts to run</summary>
        AllowOtherScripts = 1 << 1,
        /// <summary>This parcel is for sale</summary>
        ForSale = 1 << 2,
        /// <summary>Allow avatars to create a landmark on this parcel</summary>
        AllowLandmark = 1 << 3,
        /// <summary>Allows all avatars to edit the terrain on this parcel</summary>
        AllowTerraform = 1 << 4,
        /// <summary>Avatars have health and can take damage on this parcel.
        /// If set, avatars can be killed and sent home here</summary>
        AllowDamage = 1 << 5,
        /// <summary>Foreign avatars can create objects here</summary>
        CreateObjects = 1 << 6,
        /// <summary>All objects on this parcel can be purchased</summary>
        ForSaleObjects = 1 << 7,
        /// <summary>Access is restricted to a group</summary>
        UseAccessGroup = 1 << 8,
        /// <summary>Access is restricted to a whitelist</summary>
        UseAccessList = 1 << 9,
        /// <summary>Ban blacklist is enabled</summary>
        UseBanList = 1 << 10,
        /// <summary>Unknown</summary>
        UsePassList = 1 << 11,
        /// <summary>List this parcel in the search directory</summary>
        ShowDirectory = 1 << 12,
        /// <summary>Allow personally owned parcels to be deeded to group</summary>
        AllowDeedToGroup = 1 << 13,
        /// <summary>If Deeded, owner contributes required tier to group parcel is deeded to</summary>
        ContributeWithDeed = 1 << 14,
        /// <summary>Restrict sounds originating on this parcel to the 
        /// parcel boundaries</summary>
        SoundLocal = 1 << 15,
        /// <summary>Objects on this parcel are sold when the land is 
        /// purchsaed</summary>
        SellParcelObjects = 1 << 16,
        /// <summary>Allow this parcel to be published on the web</summary>
        AllowPublish = 1 << 17,
        /// <summary>The information for this parcel is mature content</summary>
        MaturePublish = 1 << 18,
        /// <summary>The media URL is an HTML page</summary>
        UrlWebPage = 1 << 19,
        /// <summary>The media URL is a raw HTML string</summary>
        UrlRawHtml = 1 << 20,
        /// <summary>Restrict foreign object pushes</summary>
        RestrictPushObject = 1 << 21,
        /// <summary>Ban all non identified/transacted avatars</summary>
        DenyAnonymous = 1 << 22,
        // <summary>Ban all identified avatars [OBSOLETE]</summary>
        //[Obsolete]
        // This was obsoleted in 1.19.0 but appears to be recycled and is used on linden homes parcels
        LindenHome = 1 << 23,
        // <summary>Ban all transacted avatars [OBSOLETE]</summary>
        //[Obsolete]
        //DenyTransacted = 1 << 24,
        /// <summary>Allow group-owned scripts to run</summary>
        AllowGroupScripts = 1 << 25,
        /// <summary>Allow object creation by group members or group 
        /// objects</summary>
        CreateGroupObjects = 1 << 26,
        /// <summary>Allow all objects to enter this parcel</summary>
        AllowAPrimitiveEntry = 1 << 27,
        /// <summary>Only allow group and owner objects to enter this parcel</summary>
        AllowGroupObjectEntry = 1 << 28,
        /// <summary>Voice Enabled on this parcel</summary>
        AllowVoiceChat = 1 << 29,
        /// <summary>Use Estate Voice channel for Voice on this parcel</summary>
        UseEstateVoiceChan = 1 << 30,
        /// <summary>Deny Age Unverified Users</summary>
        DenyAgeUnverified = 1U << 31
    }

    /// <summary>
    /// Category parcel is listed in under search
    /// </summary>
    public enum ParcelCategory : sbyte
    {
        /// <summary>No assigned category</summary>
        None = 0,
        /// <summary>Linden Infohub or public area</summary>
        Linden,
        /// <summary>Adult themed area</summary>
        Adult,
        /// <summary>Arts and Culture</summary>
        Arts,
        /// <summary>Business</summary>
        Business,
        /// <summary>Educational</summary>
        Educational,
        /// <summary>Gaming</summary>
        Gaming,
        /// <summary>Hangout or Club</summary>
        Hangout,
        /// <summary>Newcomer friendly</summary>
        Newcomer,
        /// <summary>Parks and Nature</summary>
        Park,
        /// <summary>Residential</summary>
        Residential,
        /// <summary>Shopping</summary>
        Shopping,
        /// <summary>Not Used?</summary>
        Stage,
        /// <summary>Other</summary>
        Other,
        /// <summary>Not an actual category, only used for queries</summary>
        Any = -1
    }

    /// <summary>
    /// Type of teleport landing for a parcel
    /// </summary>
    public enum LandingType : byte
    {
        /// <summary>Unset, simulator default</summary>
        None = 0,
        /// <summary>Specific landing point set for this parcel</summary>
        LandingPoint = 1,
        /// <summary>No landing point set, direct teleports enabled for
        /// this parcel</summary>
        Direct = 2
    }

    /// <summary>
    /// Flags used in the ParcelAccessListRequest packet to specify whether
    /// we want the access list (whitelist), ban list (blacklist), or both
    /// </summary>
    [Flags]
    public enum AccessList : uint
    {
        /// <summary>Request the access list</summary>
        Access = 1 << 0,
        /// <summary>Request the ban list</summary>
        Ban = 1 << 1,
        /// <summary>Request both White and Black lists</summary>
        Both = Access | Ban
    }
}
