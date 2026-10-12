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
    /// Type of return to use when returning objects from a parcel
    /// </summary>
    public enum ObjectReturnType : uint
    {
        /// <summary></summary>
        None = 0,
        /// <summary>Return objects owned by parcel owner</summary>
        Owner = 1 << 1,
        /// <summary>Return objects set to group</summary>
        Group = 1 << 2,
        /// <summary>Return objects not owned by parcel owner or set to group</summary>
        Other = 1 << 3,
        /// <summary>Return a specific list of objects on parcel</summary>
        List = 1 << 4,
        /// <summary>Return objects that are marked for-sale</summary>
        Sell = 1 << 5
    }

    /// <summary>
    /// Blacklist/Whitelist flags used in parcels Access List
    /// </summary>
    public enum ParcelAccessFlags : uint
    {
        /// <summary>Agent is denied access</summary>
        NoAccess = 0,
        /// <summary>Agent is granted access</summary>
        Access = 1
    }

    /// <summary>
    /// Sequence ID in ParcelPropertiesReply packets (sent when avatar
    /// tries to cross a parcel border)
    /// </summary>
    public enum ParcelPropertiesStatus : int
    {
        /// <summary>Parcel is currently selected</summary>
        ParcelSelected = -10000,
        /// <summary>Parcel restricted to a group the avatar is not a
        /// member of</summary>
        CollisionNotInGroup = -20000,
        /// <summary>Avatar is banned from the parcel</summary>
        CollisionBanned = -30000,
        /// <summary>Parcel is restricted to an access list that the
        /// avatar is not on</summary>
        CollisionNotOnAccessList = -40000,
        /// <summary>Response to hovering over a parcel</summary>
        HoveredOverParcel = -50000
    }

    /// <summary>
    /// The tool to use when modifying terrain levels
    /// </summary>
    public enum TerraformAction : byte
    {
        /// <summary>Level the terrain</summary>
        Level = 0,
        /// <summary>Raise the terrain</summary>
        Raise = 1,
        /// <summary>Lower the terrain</summary>
        Lower = 2,
        /// <summary>Smooth the terrain</summary>
        Smooth = 3,
        /// <summary>Add random noise to the terrain</summary>
        Noise = 4,
        /// <summary>Revert terrain to simulator default</summary>
        Revert = 5
    }

    /// <summary>
    /// The tool size to use when changing terrain levels
    /// </summary>
    public enum TerraformBrushSize : byte
    {
        /// <summary>Small</summary>
        Small = 1,
        /// <summary>Medium</summary>
        Medium = 2,
        /// <summary>Large</summary>
        Large = 4
    }

    /// <summary>
    /// Reasons agent is denied access to a parcel on the simulator
    /// </summary>
    public enum AccessDeniedReason : byte
    {
        /// <summary>Agent is not denied, access is granted</summary>
        NotDenied = 0,
        /// <summary>Agent is not a member of the group set for the parcel, or which owns the parcel</summary>
        NotInGroup = 1,
        /// <summary>Agent is not on the parcels specific allow list</summary>
        NotOnAllowList = 2,
        /// <summary>Agent is on the parcels ban list</summary>
        BannedFromParcel = 3,
        /// <summary>Unknown</summary>
        NoAccess = 4,
        /// <summary>Agent is not age verified and parcel settings deny access to non age verified avatars</summary>
        NotAgeVerified = 5
    }

    /// <summary>
    /// Parcel overlay type. This is used primarily for highlighting and
    /// coloring which is why it is a single integer instead of a set of
    /// flags
    /// </summary>
    /// <remarks>These values seem to be poorly thought out. The first three
    /// bits represent a single value, not flags. For example Auction (0x05) is
    /// not a combination of OwnedByOther (0x01) and ForSale(0x04). However,
    /// the BorderWest and BorderSouth values are bit flags that get attached
    /// to the value stored in the first three bits. Bits four, five, and six
    /// are unused</remarks>
    [Flags]
    public enum ParcelOverlayType : byte
    {
        /// <summary>Public land</summary>
        Public = 0,
        /// <summary>Land is owned by another avatar</summary>
        OwnedByOther = 1,
        /// <summary>Land is owned by a group</summary>
        OwnedByGroup = 2,
        /// <summary>Land is owned by the current avatar</summary>
        OwnedBySelf = 3,
        /// <summary>Land is for sale</summary>
        ForSale = 4,
        /// <summary>Land is being auctioned</summary>
        Auction = 5,
        /// <summary>Land is private</summary>
        Private = 32,
        /// <summary>To the west of this area is a parcel border</summary>
        BorderWest = 64,
        /// <summary>To the south of this area is a parcel border</summary>
        BorderSouth = 128
    }

    /// <summary>
    /// Parcel Media Command used in ParcelMediaCommandMessage
    /// </summary>
    public enum ParcelMediaCommand : uint
    {
        /// <summary>Stop the media stream and go back to the first frame</summary>
        Stop = 0,
        /// <summary>Pause the media stream (stop playing but stay on current frame)</summary>
        Pause,
        /// <summary>Start the current media stream playing and stop when the end is reached</summary>
        Play,
        /// <summary>Start the current media stream playing, 
        /// loop to the beginning when the end is reached and continue to play</summary>
        Loop,
        /// <summary>Specifies the texture to replace with video</summary>
        /// <remarks>If passing the key of a texture, it must be explicitly typecast as a key, 
        /// not just passed within double quotes.</remarks>
        Texture,
        /// <summary>Specifies the movie URL (254 characters max)</summary>
        URL,
        /// <summary>Specifies the time index at which to begin playing</summary>
        Time,
        /// <summary>Specifies a single agent to apply the media command to</summary>
        Agent,
        /// <summary>Unloads the stream. While the stop command sets the texture to the first frame of the movie, 
        /// unload resets it to the real texture that the movie was replacing.</summary>
        Unload,
        /// <summary>Turn on/off the auto align feature, similar to the auto align checkbox in the parcel media properties 
        /// (NOT to be confused with the "align" function in the textures view of the editor!) Takes TRUE or FALSE as parameter.</summary>
        AutoAlign,
        /// <summary>Allows a Web page or image to be placed on a prim (1.19.1 RC0 and later only). 
        /// Use "text/html" for HTML.</summary>
        Type,
        /// <summary>Resizes a Web page to fit on x, y pixels (1.19.1 RC0 and later only).</summary>
        /// <remarks>This might still not be working</remarks>
        Size,
        /// <summary>Sets a description for the media being displayed (1.19.1 RC0 and later only).</summary>
        Desc
    }

    /// <summary>
    /// Some information about a parcel of land returned from a DirectoryManager search
    /// </summary>
    public struct ParcelInfo
    {
        /// <summary>Global Key of record</summary>
        public UUID ID;
        /// <summary>Parcel Owners <see cref="UUID"/></summary>
        public UUID OwnerID;
        /// <summary>Name field of parcel, limited to 128 characters</summary>
        public string Name;
        /// <summary>Description field of parcel, limited to 256 characters</summary>
        public string Description;
        /// <summary>Total Square meters of parcel</summary>
        public int ActualArea;
        /// <summary>Total area billable as Tier, for group owned land this will be 10% less than ActualArea</summary>
        public int BillableArea;
        /// <summary>True of parcel is in Mature simulator</summary>
        public bool Mature;
        /// <summary>Grid global X position of parcel</summary>
        public float GlobalX;
        /// <summary>Grid global Y position of parcel</summary>
        public float GlobalY;
        /// <summary>Grid global Z position of parcel (not used)</summary>
        public float GlobalZ;
        /// <summary>Name of simulator parcel is located in</summary>
        public string SimName;
        /// <summary>Texture <see cref="T:LibreMetaverse.UUID"/> of parcels display picture</summary>
        public UUID SnapshotID;
        /// <summary>Float representing calculated traffic based on time spent on parcel by avatars</summary>
        public float Dwell;
        /// <summary>Sale price of parcel (not used)</summary>
        public int SalePrice;
        /// <summary>Auction ID of parcel</summary>
        public int AuctionID;
    }

    /// <summary>
    /// Parcel Media Information
    /// </summary>
    public struct ParcelMedia
    {
        /// <summary>A byte, if 0x1 viewer should auto scale media to fit object</summary>
        public bool MediaAutoScale;
        /// <summary>A boolean, if true the viewer should loop the media</summary>
        public bool MediaLoop;
        /// <summary>The Asset UUID of the Texture which when applied to a 
        /// primitive will display the media</summary>
        public UUID MediaID;
        /// <summary>A URL which points to any Quicktime supported media type</summary>
        public string MediaURL;
        /// <summary>A description of the media</summary>
        public string MediaDesc;
        /// <summary>An Integer which represents the height of the media</summary>
        public int MediaHeight;
        /// <summary>An integer which represents the width of the media</summary>
        public int MediaWidth;
        /// <summary>A string which contains the mime type of the media</summary>
        public string MediaType;
    }

    /// <summary>
    /// Parcel Access list
    /// </summary>
    public struct ParcelAccessEntry
    {
        /// <summary>Agents <see cref="T:LibreMetaverse.UUID"/></summary>
        public UUID AgentID;
        /// <summary></summary>
        public DateTime Time;
        /// <summary>Flags for specific entry in white/black lists</summary>
        public AccessList Flags;
    }

    /// <summary>
    /// Owners of primitives on parcel
    /// </summary>
    public struct ParcelPrimOwners
    {
        /// <summary>Prim Owners <see cref="T:LibreMetaverse.UUID"/></summary>
        public UUID OwnerID;
        /// <summary>True if group owned parcel</summary>
        public bool IsGroupOwned;
        /// <summary>Total count of prims owned by OwnerID</summary>
        public int Count;
        /// <summary>true of OwnerID is currently online and is not a group</summary>
        public bool OnlineStatus;
        /// <summary>The date of the most recent prim left by OwnerID</summary>
        public DateTime NewestPrim;
    }
}
