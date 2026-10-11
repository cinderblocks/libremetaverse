/*
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

using System.Collections.Frozen;
using System.Collections.Generic;

namespace LibreMetaverse.Assets
{
    /// <summary>
    /// Constants for the archiving module
    /// </summary>
    public class ArchiveConstants
    {
        /// <summary>
        /// The location of the archive control file
        /// </summary>
        public const string CONTROL_FILE_PATH = "archive.xml";

        /// <summary>
        /// Path for the assets held in an archive
        /// </summary>
        public const string ASSETS_PATH = "assets/";

        /// <summary>
        /// Path for the prims file
        /// </summary>
        public const string OBJECTS_PATH = "objects/";

        /// <summary>
        /// Path for terrains.  Technically these may be assets, but I think it's quite nice to split them out.
        /// </summary>
        public const string TERRAINS_PATH = "terrains/";

        /// <summary>
        /// Path for region settings.
        /// </summary>
        public const string SETTINGS_PATH = "settings/";

        /// <value>
        ///   Path for region settings.
        /// </value>
        public const string LANDDATA_PATH = "landdata/";

        /// <summary>
        /// The character the separates the uuid from extension information in an archived asset filename
        /// </summary>
        public const string ASSET_EXTENSION_SEPARATOR = "_";

        /// <summary>
        /// Extensions used for asset types in the archive
        /// </summary>
        public static readonly FrozenDictionary<AssetType, string> ASSET_TYPE_TO_EXTENSION;
        public static readonly FrozenDictionary<string, AssetType> EXTENSION_TO_ASSET_TYPE;

        static ArchiveConstants()
        {
            ASSET_TYPE_TO_EXTENSION = new Dictionary<AssetType, string>
            {
                [AssetType.Animation]  = ASSET_EXTENSION_SEPARATOR + "animation.bvh",
                [AssetType.Bodypart]   = ASSET_EXTENSION_SEPARATOR + "bodypart.txt",
                [AssetType.CallingCard] = ASSET_EXTENSION_SEPARATOR + "callingcard.txt",
                [AssetType.Clothing]   = ASSET_EXTENSION_SEPARATOR + "clothing.txt",
                [AssetType.Folder]     = ASSET_EXTENSION_SEPARATOR + "folder.txt",
                [AssetType.Gesture]    = ASSET_EXTENSION_SEPARATOR + "gesture.txt",
                [AssetType.ImageJPEG]  = ASSET_EXTENSION_SEPARATOR + "image.jpg",
                [AssetType.ImageTGA]   = ASSET_EXTENSION_SEPARATOR + "image.tga",
                [AssetType.Landmark]   = ASSET_EXTENSION_SEPARATOR + "landmark.txt",
                [AssetType.LSLBytecode] = ASSET_EXTENSION_SEPARATOR + "bytecode.lso",
                [AssetType.LSLText]    = ASSET_EXTENSION_SEPARATOR + "script.lsl",
                [AssetType.Notecard]   = ASSET_EXTENSION_SEPARATOR + "notecard.txt",
                [AssetType.Object]     = ASSET_EXTENSION_SEPARATOR + "object.xml",
                [AssetType.Simstate]   = ASSET_EXTENSION_SEPARATOR + "simstate.bin",
                [AssetType.Sound]      = ASSET_EXTENSION_SEPARATOR + "sound.ogg",
                [AssetType.SoundWAV]   = ASSET_EXTENSION_SEPARATOR + "sound.wav",
                [AssetType.Texture]    = ASSET_EXTENSION_SEPARATOR + "texture.jp2",
                [AssetType.TextureTGA] = ASSET_EXTENSION_SEPARATOR + "texture.tga",
            }.ToFrozenDictionary();

            EXTENSION_TO_ASSET_TYPE = new Dictionary<string, AssetType>
            {
                [ASSET_EXTENSION_SEPARATOR + "animation.bvh"]   = AssetType.Animation,
                [ASSET_EXTENSION_SEPARATOR + "bodypart.txt"]    = AssetType.Bodypart,
                [ASSET_EXTENSION_SEPARATOR + "callingcard.txt"] = AssetType.CallingCard,
                [ASSET_EXTENSION_SEPARATOR + "clothing.txt"]    = AssetType.Clothing,
                [ASSET_EXTENSION_SEPARATOR + "folder.txt"]      = AssetType.Folder,
                [ASSET_EXTENSION_SEPARATOR + "gesture.txt"]     = AssetType.Gesture,
                [ASSET_EXTENSION_SEPARATOR + "image.jpg"]       = AssetType.ImageJPEG,
                [ASSET_EXTENSION_SEPARATOR + "image.tga"]       = AssetType.ImageTGA,
                [ASSET_EXTENSION_SEPARATOR + "landmark.txt"]    = AssetType.Landmark,
                [ASSET_EXTENSION_SEPARATOR + "bytecode.lso"]    = AssetType.LSLBytecode,
                [ASSET_EXTENSION_SEPARATOR + "script.lsl"]      = AssetType.LSLText,
                [ASSET_EXTENSION_SEPARATOR + "notecard.txt"]    = AssetType.Notecard,
                [ASSET_EXTENSION_SEPARATOR + "object.xml"]      = AssetType.Object,
                [ASSET_EXTENSION_SEPARATOR + "simstate.bin"]    = AssetType.Simstate,
                [ASSET_EXTENSION_SEPARATOR + "sound.ogg"]       = AssetType.Sound,
                [ASSET_EXTENSION_SEPARATOR + "sound.wav"]       = AssetType.SoundWAV,
                [ASSET_EXTENSION_SEPARATOR + "texture.jp2"]     = AssetType.Texture,
                [ASSET_EXTENSION_SEPARATOR + "texture.tga"]     = AssetType.TextureTGA,
            }.ToFrozenDictionary();
        }
    }
}
