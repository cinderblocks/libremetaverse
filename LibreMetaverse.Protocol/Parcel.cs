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
using System.Collections.Generic;

namespace LibreMetaverse
{
    /// <summary>
    /// Parcel of land, a portion of virtual real estate in a simulator
    /// </summary>
    public class Parcel
    {
        /// <summary>The total number of contiguous 4x4 meter blocks your agent owns within this parcel</summary>        
        public int SelfCount;
        /// <summary>The total number of contiguous 4x4 meter blocks contained in this parcel owned by a group or agent other than your own</summary>
        public int OtherCount;
        /// <summary>Deprecated, Value appears to always be 0</summary>
        public int PublicCount;
        /// <summary>Simulator-local ID of this parcel</summary>
        public int LocalID;
        /// <summary>UUID of the owner of this parcel</summary>
        public UUID OwnerID;
        /// <summary>Whether the land is deeded to a group or not</summary>
        public bool IsGroupOwned;
        /// <summary></summary>
        public uint AuctionID;
        /// <summary>Date land was claimed</summary>
        public DateTime ClaimDate;
        /// <summary>Appears to always be zero</summary>
        public int ClaimPrice;
        /// <summary>This field is no longer used</summary>
        public int RentPrice;
        /// <summary>Minimum corner of the axis-aligned bounding box for this
        /// parcel</summary>
        public Vector3 AABBMin;
        /// <summary>Maximum corner of the axis-aligned bounding box for this
        /// parcel</summary>
        public Vector3 AABBMax;
        /// <summary>Bitmap describing land layout in 4x4m squares across the 
        /// entire region</summary>
        public byte[] Bitmap;
        /// <summary>Total parcel land area</summary>
        public int Area;
        /// <summary></summary>
        public ParcelStatus Status;
        /// <summary>Maximum primitives across the entire simulator owned by the same agent or group that owns this parcel that can be used</summary>
        public int SimWideMaxPrims;
        /// <summary>Total primitives across the entire simulator calculated by combining the allowed prim counts for each parcel
        /// owned by the agent or group that owns this parcel</summary>
        public int SimWideTotalPrims;
        /// <summary>Maximum number of primitives this parcel supports</summary>
        public int MaxPrims;
        /// <summary>Total number of primitives on this parcel</summary>
        public int TotalPrims;
        /// <summary>For group-owned parcels this indicates the total number of prims deeded to the group,
        /// for parcels owned by an individual this inicates the number of prims owned by the individual</summary>
        public int OwnerPrims;
        /// <summary>Total number of primitives owned by the parcel group on 
        /// this parcel, or for parcels owned by an individual with a group set the
        /// total number of prims set to that group.</summary>
        public int GroupPrims;
        /// <summary>Total number of prims owned by other avatars that are not set to group, or not the parcel owner</summary>
        public int OtherPrims;
        /// <summary>A bonus multiplier which allows parcel prim counts to go over times this amount, this does not affect
        /// the max prims per simulator. e.g: 117 prim parcel limit x 1.5 bonus = 175 allowed</summary>
        public float ParcelPrimBonus;
        /// <summary>Autoreturn value in minutes for others' objects</summary>
        public int OtherCleanTime;
        /// <summary></summary>
        public ParcelFlags Flags;
        /// <summary>Sale price of the parcel, only useful if ForSale is set</summary>
        /// <remarks>The SalePrice will remain the same after an ownership
        /// transfer (sale), so it can be used to see the purchase price after
        /// a sale if the new owner has not changed it</remarks>
        public int SalePrice;
        /// <summary>Parcel Name</summary>
        public string Name;
        /// <summary>Parcel Description</summary>
        public string Desc;
        /// <summary>URL For Music Stream</summary>
        public string MusicURL;
        /// <summary></summary>
        public UUID GroupID;
        /// <summary>Price for a temporary pass</summary>
        public int PassPrice;
        /// <summary>How long is pass valid for</summary>
        public float PassHours;
        /// <summary></summary>
        public ParcelCategory Category;
        /// <summary>Key of authorized buyer</summary>
        public UUID AuthBuyerID;
        /// <summary>Key of parcel snapshot</summary>
        public UUID SnapshotID;
        /// <summary>The landing point location</summary>
        public Vector3 UserLocation;
        /// <summary>The landing point LookAt</summary>
        public Vector3 UserLookAt;
        /// <summary>The type of landing enforced from the <see cref="LandingType"/> enum</summary>
        public LandingType Landing;
        /// <summary>Traffic count</summary>
        public float Dwell;
        /// <summary></summary>
        public bool RegionDenyAnonymous;
        /// <summary></summary>
        public bool RegionPushOverride;
        /// <summary>Access list of who is whitelisted on this
        /// parcel</summary>
        public List<ParcelAccessEntry> AccessWhiteList;
        /// <summary>Access list of who is blacklisted on this
        /// parcel</summary>
        public List<ParcelAccessEntry> AccessBlackList;
        /// <summary>TRUE of region denies access to age unverified users</summary>
        public bool RegionDenyAgeUnverified;
        /// <summary>true to obscure (hide) media url</summary>
        public bool ObscureMedia;
        /// <summary>true to obscure (hide) music url</summary>
        public bool ObscureMusic;
        /// <summary>A struct containing media details</summary>
        public ParcelMedia Media;
        /// <summary>Parcel privacy see avatars outside/inside parcel</summary>
        public bool SeeAVs;
        /// <summary>Parcel privacy play sounds attached to avatars outside/inside parcel</summary>
        public bool AnyAVSounds;
        /// <summary>Parcel privacy play sounds for group members</summary>
        public bool GroupAVSounds;

        /// <summary>
        /// Displays a parcel object in string format
        /// </summary>
        /// <returns>string containing key=value pairs of a parcel object</returns>
        public override string ToString()
        {
            return $"SelfCount = {SelfCount} OtherCount = {OtherCount} PublicCount = {PublicCount} " +
                   $"LocalID = {LocalID} OwnerID = {OwnerID} IsGroupOwned = {IsGroupOwned} " +
                   $"AuctionID = {AuctionID} ClaimDate = {ClaimDate} ClaimPrice = {ClaimPrice} " +
                   $"RentPrice = {RentPrice} AABBMin = {AABBMin} AABBMax = {AABBMax} " +
                   $"Area = {Area} Status = {Status} SimWideMaxPrims = {SimWideMaxPrims} " +
                   $"SimWideTotalPrims = {SimWideTotalPrims} MaxPrims = {MaxPrims} TotalPrims = {TotalPrims} " +
                   $"OwnerPrims = {OwnerPrims} GroupPrims = {GroupPrims} OtherPrims = {OtherPrims} " +
                   $"ParcelPrimBonus = {ParcelPrimBonus} OtherCleanTime = {OtherCleanTime} Flags = {Flags} " +
                   $"SalePrice = {SalePrice} Name = {Name} Desc = {Desc} MusicURL = {MusicURL} " +
                   $"GroupID = {GroupID} PassPrice = {PassPrice} PassHours = {PassHours} " +
                   $"Category = {Category} AuthBuyerID = {AuthBuyerID} SnapshotID = {SnapshotID} " +
                   $"UserLocation = {UserLocation} UserLookAt = {UserLookAt} Landing = {Landing} " +
                   $"Dwell = {Dwell} RegionDenyAnonymous = {RegionDenyAnonymous} " +
                   $"RegionPushOverride = {RegionPushOverride} RegionDenyAgeUnverified = {RegionDenyAgeUnverified} " +
                   $"ObscureMedia = {ObscureMedia} ObscureMusic = {ObscureMusic} " +
                   $"SeeAVs = {SeeAVs} AnyAVSounds = {AnyAVSounds} GroupAVSounds = {GroupAVSounds} ";
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="localID">Local ID of this parcel</param>
        public Parcel(int localID)
        {
            LocalID = localID;
            ClaimDate = Utils.Epoch;
            Bitmap = Utils.EmptyBytes;
            Name = string.Empty;
            Desc = string.Empty;
            MusicURL = string.Empty;
            AccessWhiteList = new List<ParcelAccessEntry>(0);
            AccessBlackList = new List<ParcelAccessEntry>(0);
            Media = new ParcelMedia();
        }
    }
}
