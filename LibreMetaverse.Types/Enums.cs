﻿/*
 * Copyright (c) 2006-2016, openmetaverse.co
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
    /// <summary>
    /// Attribute class that allows extra attributes to be attached to ENUMs
    /// </summary>
    public class EnumInfoAttribute : Attribute
    {
        /// <summary>Text used when presenting ENUM to user</summary>
        public string Text = string.Empty;

        /// <summary>Default initializer</summary>
        public EnumInfoAttribute() { }

        /// <summary>Text used when presenting ENUM to user</summary>
        public EnumInfoAttribute(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// The different types of grid assets
    /// </summary>
    public enum AssetType : sbyte
    {
        /// <summary>Unknown asset type</summary>
        [EnumInfo(Text = "invalid")]
        Unknown = -1,
        /// <summary>Texture asset, stores in JPEG2000 J2C stream format</summary>
        [EnumInfo (Text = "texture")]
        Texture = 0,
        /// <summary>Sound asset</summary>
        [EnumInfo(Text = "sound")]
        Sound = 1,
        /// <summary>Calling card for another avatar</summary>
        [EnumInfo(Text = "callcard")]
        CallingCard = 2,
        /// <summary>Link to a location in world</summary>
        [EnumInfo(Text = "landmark")]
        Landmark = 3,
        // <summary>Legacy script asset, you should never see one of these</summary>
        [Obsolete("No longer used")]
        [EnumInfo(Text = "script")]
        Script = 4,
        /// <summary>Collection of textures and parameters that can be worn by an avatar</summary>
        [EnumInfo(Text = "clothing")]
        Clothing = 5,
        /// <summary>Primitive that can contain textures, sounds, scripts and more</summary>
        [EnumInfo(Text = "object")]
        Object = 6,
        /// <summary>Notecard asset</summary>
        [EnumInfo(Text = "notecard")]
        Notecard = 7,
        /// <summary>Holds a collection of inventory items. "Category" in the Linden viewer</summary>
        [EnumInfo(Text = "category")]
        Folder = 8,
        /// <summary>Linden scripting language script</summary>
        [EnumInfo(Text = "lsltext")]
        LSLText = 10,
        /// <summary>LSO bytecode for a script</summary>
        [EnumInfo(Text = "lslbyte")]
        LSLBytecode = 11,
        /// <summary>Uncompressed TGA texture</summary>
        [EnumInfo(Text = "txtr_tga")]
        TextureTGA = 12,
        /// <summary>Collection of textures and shape parameters that can be worn</summary>
        [EnumInfo(Text = "bodypart")]
        Bodypart = 13,
        /// <summary>Uncompressed sound</summary>
        [EnumInfo(Text = "snd_wav")]
        SoundWAV = 17,
        /// <summary>Uncompressed TGA non-square image, not to be used as a texture</summary>
        [EnumInfo(Text = "img_tga")]
        ImageTGA = 18,
        /// <summary>Compressed JPEG non-square image, not to be used as a texture</summary>
        [EnumInfo(Text = "jpeg")]
        ImageJPEG = 19,
        /// <summary>Animation</summary>
        [EnumInfo(Text = "animatn")]
        Animation = 20,
        /// <summary>Sequence of animations, sounds, chat, and pauses</summary>
        [EnumInfo(Text = "gesture")]
        Gesture = 21,
        /// <summary>Simstate file</summary>
        [EnumInfo(Text = "simstate")]
        Simstate = 22,
        /// <summary>Asset is a link to another inventory item</summary>
        [EnumInfo(Text = "link")]
        Link = 24,
        /// <summary>Asset is a link to another inventory folder</summary>
        [EnumInfo(Text = "link_f")]
        LinkFolder = 25,
        /// <summary>Linden mesh format</summary>
        [EnumInfo(Text = "mesh")]
        Mesh = 49,
        /// <summary>widget?</summary>
        [EnumInfo(Text = "widget")]
        Widget = 40,
        /// <summary>Person?</summary>
        [EnumInfo(Text = "person")]
        Person = 45,
        /// <summary> Settings blob </summary>
        [EnumInfo(Text = "settings")]
        Settings,
        /// <summary> Render material </summary>
        [EnumInfo(Text = "material")]
        Material,
    }

    /// <summary>
    /// The different types of folder.
    /// </summary>
    public enum FolderType : sbyte
    {
        /// <summary>None folder type</summary>
        None = -1,
        /// <summary>Texture folder type</summary>
        Texture = 0,
        /// <summary>Sound folder type</summary>
        Sound = 1,
        /// <summary>Calling card folder type</summary>
        CallingCard = 2,
        /// <summary>Landmark folder type</summary>
        Landmark = 3,
        /// <summary>Clothing folder type</summary>
        Clothing = 5,
        /// <summary>Object folder type</summary>
        Object = 6,
        /// <summary>Notecard folder type</summary>
        Notecard = 7,
        /// <summary>The root folder type</summary>
        Root = 8,
        /// <summary>Non-conformant OpenSim root folder type</summary>
        [Obsolete("No longer used, please use FolderType.Root")]
        OldRoot = 9,
        /// <summary>LSLText folder</summary>
        LSLText = 10,
        /// <summary>Bodyparts folder</summary>
        BodyPart = 13,
        /// <summary>Trash folder</summary>
        Trash = 14,
        /// <summary>Snapshot folder</summary>
        Snapshot = 15,
        /// <summary>Lost And Found folder</summary>
        LostAndFound = 16,
        /// <summary>Animation folder</summary>
        Animation = 20,
        /// <summary>Gesture folder</summary>
        Gesture = 21,
        /// <summary>Favorites folder</summary>
        Favorites = 23,
        /// <summary>Ensemble beginning range</summary>
        EnsembleStart = 26,
        /// <summary>Ensemble ending range</summary>
        EnsembleEnd= 45,
        /// <summary>Current outfit folder</summary>
        CurrentOutfit = 46,
        /// <summary>Outfit folder</summary>
        Outfit = 47,
        /// <summary>My outfits folder</summary>
        MyOutfits = 48,
        /// <summary>Mesh folder</summary>
        Mesh = 49,
        /// <summary>Marketplace direct delivery inbox ("Received Items")</summary>
        Inbox = 50,
        /// <summary>Marketplace direct delivery outbox</summary>
        Outbox = 51,
        /// <summary>Basic root folder</summary>
        BasicRoot = 52,
        /// <summary>Marketplace listings folder</summary>
        MarketplaceListings = 53,
        /// <summary>Marketplace stock folder</summary>
        MarkplaceStock = 54,
        /// <summary>Marketplace version. We *never* actually create folder of this type</summary>
        MarketplaceVersion = 55,
        /// <summary>Hypergrid Suitcase folder</summary>
        Suitcase = 100
    }

    /// <summary>
    /// Inventory Item Types, eg Script, Notecard, Folder, etc
    /// </summary>
    public enum InventoryType : sbyte
    {
        /// <summary>Unknown</summary>
        Unknown = -1,
        /// <summary>Texture</summary>
        Texture = 0,
        /// <summary>Sound</summary>
        Sound = 1,
        /// <summary>Calling Card</summary>
        CallingCard = 2,
        /// <summary>Landmark</summary>
        Landmark = 3,
        /*
        /// <summary>Script</summary>
        [Obsolete("See LSL")] Script = 4,
        /// <summary>Clothing</summary>
        [Obsolete("See Wearable")] Clothing = 5,
        */
        /// <summary>Object, both single and coalesced</summary>
        Object = 6,
        /// <summary>Notecard</summary>
        Notecard = 7,
        /// <summary></summary>
        Category = 8,
        /// <summary>Folder</summary>
        Folder = 8,
        /// <summary></summary>
        RootCategory = 9,
        /// <summary>an LSL Script</summary>
        LSL = 10,
        /*
        /// <summary></summary>
        //[Obsolete("See LSL")] LSLBytecode = 11,
        /// <summary></summary>
        //[Obsolete("See Texture")] TextureTGA = 12,
        /// <summary></summary>
        //[Obsolete] Bodypart = 13,
        /// <summary></summary>
        //[Obsolete] Trash = 14,
         */
        /// <summary></summary>
        Snapshot = 15,
        /*
        /// <summary></summary>
        //[Obsolete] LostAndFound = 16,
         */
        /// <summary></summary>
        Attachment = 17,
        /// <summary></summary>
        Wearable = 18,
        /// <summary></summary>
        Animation = 19,
        /// <summary></summary>
        Gesture = 20,
        /// <summary></summary>
        Mesh = 22,
        /// <summary></summary>
        Settings = 23,
        /// <summary></summary>
        Material = 24,
    }

    /// <summary>
    /// Item Sale Status
    /// </summary>
    public enum SaleType : byte
    {
        /// <summary>Not for sale</summary>
        Not = 0,
        /// <summary>The original is for sale</summary>
        Original = 1,
        /// <summary>Copies are for sale</summary>
        Copy = 2,
        /// <summary>The contents of the object are for sale</summary>
        Contents = 3
    }

    /// <summary>
    /// Types of wearable assets
    /// </summary>
    public enum WearableType : byte
    {
        /// <summary>Body shape</summary>
        [EnumInfo(Text = "Shape")]
        Shape = 0,
        /// <summary>Skin textures and attributes</summary>
        [EnumInfo(Text = "Skin")]
        Skin,
        /// <summary>Hair</summary>
        [EnumInfo(Text = "Hair")]
        Hair,
        /// <summary>Eyes</summary>
        [EnumInfo(Text = "Eyes")]
        Eyes,
        /// <summary>Shirt</summary>
        [EnumInfo(Text = "Shirt")]
        Shirt,
        /// <summary>Pants</summary>
        [EnumInfo(Text = "Pants")]
        Pants,
        /// <summary>Shoes</summary>
        [EnumInfo(Text = "Shoes")]
        Shoes,
        /// <summary>Socks</summary>
        [EnumInfo(Text = "Socks")]
        Socks,
        /// <summary>Jacket</summary>
        [EnumInfo(Text = "Jacket")]
        Jacket,
        /// <summary>Gloves</summary>
        [EnumInfo(Text = "Gloves")]
        Gloves,
        /// <summary>Undershirt</summary>
        [EnumInfo(Text = "Undershirt")]
        Undershirt,
        /// <summary>Underpants</summary>
        [EnumInfo(Text = "Underpants")]
        Underpants,
        /// <summary>Skirt</summary>
        [EnumInfo(Text = "Skirt")]
        Skirt,
        /// <summary>Alpha mask to hide parts of the avatar</summary>
        [EnumInfo(Text = "Alpha")]
        Alpha,
        /// <summary>Tattoo</summary>
        [EnumInfo(Text = "Tattoo")]
        Tattoo,
        /// <summary>Physics</summary>
        [EnumInfo(Text = "Physics")]
        Physics,
        /// <summary>Universal</summary>
	    [EnumInfo(Text = "Universal")]
        Universal,
        /// <summary>Invalid wearable asset</summary>
        [EnumInfo(Text = "Invalid")]
        Invalid = 255
    }
}
