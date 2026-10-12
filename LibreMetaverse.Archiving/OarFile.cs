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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace LibreMetaverse.Assets
{
    public static class OarFile
    {
        public delegate void AssetLoadedCallback(Asset asset, long bytesRead, long totalBytes);
        public delegate void TerrainLoadedCallback(float[,] terrain, long bytesRead, long totalBytes);
        public delegate void SceneObjectLoadedCallback(AssetPrim linkset, long bytesRead, long totalBytes);
        public delegate void SettingsLoadedCallback(string regionName, RegionSettings settings);
        public delegate void ArchiveInfoLoadedCallback(ArchiveInfo info);

        #region Archive Loading

        public static void UnpackageArchive(string filename, AssetLoadedCallback assetCallback, TerrainLoadedCallback terrainCallback,
            SceneObjectLoadedCallback objectCallback, SettingsLoadedCallback settingsCallback)
        {
            UnpackageArchive(filename, assetCallback, terrainCallback, objectCallback, settingsCallback, null);
        }

        /// <summary>
        /// Reads a region archive and gives its parts to the callbacks as they are met.
        /// </summary>
        /// <param name="filename">The archive, a gzipped tar</param>
        /// <param name="assetCallback">Called with each asset</param>
        /// <param name="terrainCallback">Called with each terrain</param>
        /// <param name="objectCallback">Called with each linkset</param>
        /// <param name="settingsCallback">Called with the region settings</param>
        /// <param name="infoCallback">Called with what the archive's <c>archive.xml</c> says about it (see
        /// <see cref="ArchiveInfo"/>), if the archive has one that can be read. An archive of a newer format than
        /// <see cref="ArchiveInfo.SupportedMajorVersion"/>.<see cref="ArchiveInfo.SupportedMinorVersion"/> is logged
        /// and read as that format, and not refused.</param>
        public static void UnpackageArchive(string filename, AssetLoadedCallback assetCallback, TerrainLoadedCallback terrainCallback,
            SceneObjectLoadedCallback objectCallback, SettingsLoadedCallback settingsCallback, ArchiveInfoLoadedCallback? infoCallback)
        {
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;
            int failedObjectFiles = 0;

            try
            {
                using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream loadStream = new GZipStream(fileStream, CompressionMode.Decompress))
                    {
                        TarArchiveReader archive = new TarArchiveReader(loadStream);

                        string filePath;
                        byte[]? data;
                        TarArchiveReader.TarEntryType entryType;

                        while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
                        {
                            if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                            {
                                LoadControlFile(data!, infoCallback);
                            }
                            else if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                            {
                                // Deserialize the XML bytes
                                if (objectCallback != null)
                                {
                                    // One unreadable object file must not lose the rest of the archive. The
                                    // callback is called outside the try, so an exception from it is not taken
                                    // for an unreadable file.
                                    List<AssetPrim> linksets;
                                    try
                                    {
                                        linksets = ParseObjects(data!, filePath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Warn($"[OarFile] Failed to read {filePath}: {ex.Message}");
                                        failedObjectFiles++;
                                        continue;
                                    }

                                    foreach (AssetPrim linkset in linksets)
                                        objectCallback(linkset, fileStream.Position, fileStream.Length);
                                }
                            }
                            else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                            {
                                if (assetCallback != null)
                                {
                                    if (LoadAsset(filePath, data!, assetCallback, fileStream.Position, fileStream.Length))
                                        successfulAssetRestores++;
                                    else
                                        failedAssetRestores++;
                                }
                            }
                            else if (filePath.StartsWith(ArchiveConstants.TERRAINS_PATH))
                            {
                                if (terrainCallback != null)
                                    LoadTerrain(filePath, data!, terrainCallback, fileStream.Position, fileStream.Length);
                            }
                            else if (filePath.StartsWith(ArchiveConstants.SETTINGS_PATH))
                            {
                                if (settingsCallback != null)
                                    LoadRegionSettings(filePath, data!, settingsCallback);
                            }
                        }

                        archive.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("[OarFile] Error loading OAR file: " + e.Message);
                return;
            }

            if (failedAssetRestores > 0)
                Logger.Warn($"[OarFile]: Failed to load {failedAssetRestores} assets");

            if (failedObjectFiles > 0)
                Logger.Warn($"[OarFile]: Failed to load {failedObjectFiles} object files");
        }

        /// <summary>
        /// Reads the control file of an archive, <c>archive.xml</c>. OpenSim writes it first, so this does not read
        /// the rest of the archive unless the file is somewhere else.
        /// </summary>
        /// <returns>What the archive says about itself, or null if the file cannot be read, has no control file, or
        /// the control file does not say what version it is</returns>
        public static ArchiveInfo? ReadArchiveInfo(string filename)
        {
            try
            {
                using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (var loadStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    var archive = new TarArchiveReader(loadStream);
                    byte[]? data;

                    while ((data = archive.ReadEntry(out string filePath, out TarArchiveReader.TarEntryType _)) != null)
                    {
                        if (filePath == ArchiveConstants.CONTROL_FILE_PATH)
                            return ArchiveInfo.FromBytes(data);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("[OarFile] Error reading OAR file: " + e.Message);
            }

            return null;
        }

        private static void LoadControlFile(byte[] data, ArchiveInfoLoadedCallback? infoCallback)
        {
            ArchiveInfo? info = ArchiveInfo.FromBytes(data);
            if (info == null)
            {
                Logger.Warn("[OarFile] The archive's archive.xml could not be read, or does not say what version it is");
                return;
            }

            if (info.IsNewerThanSupported)
            {
                Logger.Warn($"[OarFile] The archive is version {info.VersionString}, which is newer than the " +
                            $"{ArchiveInfo.SupportedMajorVersion}.{ArchiveInfo.SupportedMinorVersion} this library knows. " +
                            "Reading it as that version; whatever is laid out differently will not be found");
            }

            infoCallback?.Invoke(info);
        }

        private static bool LoadAsset(string assetPath, byte[] data, AssetLoadedCallback assetCallback, long bytesRead, long totalBytes)
        {
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR, StringComparison.Ordinal);

            if (i == -1)
            {
                Logger.Warn($"[OarFile]: Could not find extension information in asset path {assetPath} since it's missing the separator " +
                    $"{ArchiveConstants.ASSET_EXTENSION_SEPARATOR}.  Skipping");
                return false;
            }

            string extension = filename.Substring(i);
            UUID uuid;
            UUID.TryParse(filename.Remove(filename.Length - extension.Length), out uuid);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.TryGetValue(extension, out var assetType))
            {
                Asset? asset = null;

                switch (assetType)
                {
                    case AssetType.Animation:
                        asset = new AssetAnimation(uuid, data);
                        break;
                    case AssetType.Bodypart:
                        asset = new AssetBodypart(uuid, data);
                        break;
                    case AssetType.Clothing:
                        asset = new AssetClothing(uuid, data);
                        break;
                    case AssetType.Gesture:
                        asset = new AssetGesture(uuid, data);
                        break;
                    case AssetType.Landmark:
                        asset = new AssetLandmark(uuid, data);
                        break;
                    case AssetType.LSLBytecode:
                        asset = new AssetScriptBinary(uuid, data);
                        break;
                    case AssetType.LSLText:
                        asset = new AssetScriptText(uuid, data);
                        break;
                    case AssetType.Notecard:
                        asset = new AssetNotecard(uuid, data);
                        break;
                    case AssetType.Object:
                        asset = new AssetPrim(uuid, data);
                        break;
                    case AssetType.Sound:
                        asset = new AssetSound(uuid, data);
                        break;
                    case AssetType.Texture:
                        asset = new AssetTexture(uuid, data);
                        break;
                    default:
                        Logger.Error("[OarFile] Unhandled asset type " + assetType);
                        break;
                }

                if (asset != null)
                {
                    assetCallback(asset, bytesRead, totalBytes);
                    return true;
                }
            }

            Logger.Warn("[OarFile] Failed to load asset");
            return false;
        }

        private static bool LoadRegionSettings(string filePath, byte[] data, SettingsLoadedCallback settingsCallback)
        {
            RegionSettings? settings = null;
            bool loaded = false;

            try
            {
                using (MemoryStream stream = new MemoryStream(data))
                    settings = RegionSettings.FromStream(stream);
                loaded = true;
            }
            catch (Exception ex)
            {
                Logger.Warn("[OarFile] Failed to parse region settings file " + filePath + ": " + ex.Message);
            }

            // Parse the region name out of the filename
            string regionName = Path.GetFileNameWithoutExtension(filePath);

            if (loaded && settings != null)
                settingsCallback(regionName, settings);

            return loaded;
        }

        private static bool LoadTerrain(string filePath, byte[] data, TerrainLoadedCallback terrainCallback, long bytesRead, long totalBytes)
        {
            float[,]? terrain = null;
            bool loaded = false;

            switch (Path.GetExtension(filePath))
            {
                case ".r32":
                case ".f32":
                    // RAW32. Region is square, so the side length is the square root of the
                    // number of posts (varregions/megaregions produce sizes other than 256x256).
                    int floatCount = data.Length / 4;
                    int side = (int)Math.Sqrt(floatCount);

                    if (data.Length % 4 == 0 && side * side == floatCount && side > 0)
                    {
                        terrain = new float[side, side];
                        int pos = 0;
                        for (int y = 0; y < side; y++)
                        {
                            for (int x = 0; x < side; x++)
                            {
                                terrain[y, x] = Utils.Clamp(Utils.BytesToFloat(data, pos), 0.0f, 255.0f);
                                pos += 4;
                            }
                        }

                        loaded = true;
                    }
                    else
                    {
                        Logger.Warn("[OarFile] RAW32 terrain file " + filePath + " has the wrong number of bytes: " + data.Length);
                    }
                    break;
            // commented out - maybe for later
            /*                  
                case ".ter":
                    // Terragen
                case ".raw":
                    // LLRAW
                case ".jpg":
                case ".jpeg":
                    // JPG
                case ".bmp":
                    // BMP
                case ".png":
                    // PNG
                case ".gif":
                    // GIF
                case ".tif":
                case ".tiff":
                    // TIFF
            */
                default:
                    Logger.Warn("[OarFile] Unrecognized terrain format in " + filePath);
                    break;
            }

            if (loaded)
                terrainCallback(terrain!, bytesRead, totalBytes);

            return loaded;
        }

        public static void LoadObjects(byte[] objectData, SceneObjectLoadedCallback objectCallback, long bytesRead, long totalBytes)
        {
            foreach (AssetPrim linkset in ParseObjects(objectData, null))
                objectCallback(linkset, bytesRead, totalBytes);
        }

        /// <summary>
        /// Reads the linksets in one object file: a single <c>SceneObjectGroup</c>, or a <c>scene</c> holding several.
        /// A linkset that cannot be read is logged and left out; XML that cannot be parsed at all throws.
        /// </summary>
        private static List<AssetPrim> ParseObjects(byte[] objectData, string? source)
        {
            XmlDocument doc = new XmlDocument();

            using (XmlTextReader reader = new XmlTextReader(ArchiveXml.OpenText(objectData)))
            {
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
            }

            var linksets = new List<AssetPrim>();

            // DocumentElement, not FirstChild: the first child is the XML declaration when there is one.
            XmlElement? rootNode = doc.DocumentElement;
            if (rootNode == null) return linksets;

            IEnumerable<XmlNode> nodes = rootNode.LocalName.Equals("scene")
                ? rootNode.ChildNodes.Cast<XmlNode>()
                : new XmlNode[] { rootNode };

            foreach (XmlNode node in nodes)
            {
                if (node is not XmlElement) continue;

                try
                {
                    AssetPrim linkset = new AssetPrim(node.OuterXml);
                    if (linkset.Parent == null)
                    {
                        Logger.Warn($"[OarFile] No object found in an entry of {source ?? "an object file"}");
                        continue;
                    }

                    linksets.Add(linkset);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[OarFile] Failed to read an object in {source ?? "an object file"}: {ex.Message}");
                }
            }

            return linksets;
        }

        #endregion Archive Loading

        #region Archive Saving

        public static void PackageArchive(string directoryName, string filename)
        {
            PackageArchive(directoryName, filename, null);
        }

        /// <summary>
        /// Packs the folders of a region (assets, objects, terrains, landdata and settings) into an archive.
        /// </summary>
        /// <param name="directoryName">The folder that holds the region's folders</param>
        /// <param name="filename">The archive to make</param>
        /// <param name="info">What to put in <c>archive.xml</c>. By default, an archive made now in version
        /// <see cref="ArchiveInfo.WrittenMajorVersion"/>.<see cref="ArchiveInfo.WrittenMinorVersion"/>, for a
        /// 256 by 256 region, that includes assets if the assets folder has any.</param>
        public static void PackageArchive(string directoryName, string filename, ArchiveInfo? info)
        {
            string assetsFolder = directoryName + "/" + ArchiveConstants.ASSETS_PATH;
            info ??= ArchiveInfo.CreateNew(assetsIncluded: Directory.GetFiles(assetsFolder).Length > 0);

            TarArchiveWriter archive = new TarArchiveWriter(new GZipStream(new FileStream(filename, FileMode.Create), CompressionMode.Compress));

            // Create the archive.xml file
            archive.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, info.ToXml());

            // Add the assets
            string[] files = Directory.GetFiles(directoryName + "/" + ArchiveConstants.ASSETS_PATH);
            foreach (string file in files)
                archive.WriteFile(ArchiveConstants.ASSETS_PATH + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the objects
            files = Directory.GetFiles(directoryName + "/" + ArchiveConstants.OBJECTS_PATH);
            foreach (string file in files)
                archive.WriteFile(ArchiveConstants.OBJECTS_PATH + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the terrain(s)
            files = Directory.GetFiles(directoryName + "/" + ArchiveConstants.TERRAINS_PATH);
            foreach (string file in files)
                archive.WriteFile(ArchiveConstants.TERRAINS_PATH + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the parcels(s)
            files = Directory.GetFiles(directoryName + "/" + ArchiveConstants.LANDDATA_PATH);
            foreach (string file in files)
                archive.WriteFile(ArchiveConstants.LANDDATA_PATH + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the setting(s)
            files = Directory.GetFiles(directoryName + "/" + ArchiveConstants.SETTINGS_PATH);
            foreach (string file in files)
                archive.WriteFile(ArchiveConstants.SETTINGS_PATH + Path.GetFileName(file), File.ReadAllBytes(file));

            archive.Close();
        }

        public static void SaveTerrain(Simulator sim, string terrainPath)
        {
            if (Directory.Exists(terrainPath))
                Directory.Delete(terrainPath, true);

            Directory.CreateDirectory(terrainPath);

            FileInfo file = new FileInfo(Path.Combine(terrainPath, sim.Name + ".r32"));
            FileStream s = file.Open(FileMode.Create, FileAccess.Write);
            SaveTerrainStream(s, sim);

            s.Close();
        }

        private static void SaveTerrainStream(Stream s, Simulator sim)
        {
            BinaryWriter bs = new BinaryWriter(s);
            
            int y;
            for (y = 0; y < sim.SizeY; y++)
            {
                int x;
                for (x = 0; x < sim.SizeX; x++)
                {
                    float height;
                    sim.TerrainHeightAtPoint(x, y, out height);
                    bs.Write(height);
                }
            }

            bs.Close();
        }

        public static void SaveParcels(Simulator sim, string parcelPath)
        {
            if (Directory.Exists(parcelPath))
                Directory.Delete(parcelPath, true);

            Directory.CreateDirectory(parcelPath);

            foreach (var parcel in sim.Parcels.Values)
            {
                UUID globalID = UUID.Random();
                SerializeParcel(parcel, globalID, Path.Combine(parcelPath, globalID + ".xml"));
            }
        }

        private static void SerializeParcel(Parcel parcel, UUID globalID, string filename)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw) { Formatting = Formatting.Indented };

            xtw.WriteStartDocument();
            xtw.WriteStartElement("LandData");

            xtw.WriteElementString("Area", Convert.ToString(parcel.Area));
            xtw.WriteElementString("AuctionID", Convert.ToString(parcel.AuctionID));
            xtw.WriteElementString("AuthBuyerID", parcel.AuthBuyerID.ToString());
            xtw.WriteElementString("Category", Convert.ToString((sbyte)parcel.Category));
            TimeSpan t = parcel.ClaimDate.ToUniversalTime() - Utils.Epoch;
            xtw.WriteElementString("ClaimDate", Convert.ToString((int)t.TotalSeconds));
            xtw.WriteElementString("ClaimPrice", Convert.ToString(parcel.ClaimPrice));
            xtw.WriteElementString("GlobalID", globalID.ToString());
            xtw.WriteElementString("GroupID", parcel.GroupID.ToString());
            xtw.WriteElementString("IsGroupOwned", Convert.ToString(parcel.IsGroupOwned));
            xtw.WriteElementString("Bitmap", Convert.ToBase64String(parcel.Bitmap));
            xtw.WriteElementString("Description", parcel.Desc);
            xtw.WriteElementString("Flags", Convert.ToString((uint)parcel.Flags));
            xtw.WriteElementString("LandingType", Convert.ToString((byte)parcel.Landing));
            xtw.WriteElementString("Name", parcel.Name);
            xtw.WriteElementString("Status", Convert.ToString((sbyte)parcel.Status));
            xtw.WriteElementString("LocalID", parcel.LocalID.ToString());
            xtw.WriteElementString("MediaAutoScale", Convert.ToString(parcel.Media.MediaAutoScale ? 1 : 0));
            xtw.WriteElementString("MediaID", parcel.Media.MediaID.ToString());
            xtw.WriteElementString("MediaURL", parcel.Media.MediaURL);
            xtw.WriteElementString("MusicURL", parcel.MusicURL);
            xtw.WriteElementString("OwnerID", parcel.OwnerID.ToString());

            xtw.WriteStartElement("ParcelAccessList");
            foreach (ParcelAccessEntry pal in parcel.AccessBlackList)
            {
                xtw.WriteStartElement("ParcelAccessEntry");
                xtw.WriteElementString("AgentID", pal.AgentID.ToString());
                xtw.WriteElementString("Time", pal.Time.ToString("s"));
                xtw.WriteElementString("AccessList", Convert.ToString((uint)pal.Flags));
                xtw.WriteEndElement();
            }
            foreach (ParcelAccessEntry pal in parcel.AccessWhiteList)
            {
                xtw.WriteStartElement("ParcelAccessEntry");
                xtw.WriteElementString("AgentID", pal.AgentID.ToString());
                xtw.WriteElementString("Time", pal.Time.ToString("s"));
                xtw.WriteElementString("AccessList", Convert.ToString((uint)pal.Flags));
                xtw.WriteEndElement();
            }
            xtw.WriteEndElement();

            xtw.WriteElementString("PassHours", Convert.ToString(parcel.PassHours, CultureInfo.InvariantCulture));
            xtw.WriteElementString("PassPrice", Convert.ToString(parcel.PassPrice));
            xtw.WriteElementString("SalePrice", Convert.ToString(parcel.SalePrice));
            xtw.WriteElementString("SnapshotID", parcel.SnapshotID.ToString());
            xtw.WriteElementString("UserLocation", parcel.UserLocation.ToString());
            xtw.WriteElementString("UserLookAt", parcel.UserLookAt.ToString());
            xtw.WriteElementString("Dwell", parcel.Dwell.ToString(CultureInfo.InvariantCulture));
            xtw.WriteElementString("SeeAVs", parcel.SeeAVs.ToString());
            xtw.WriteElementString("AnyAVSounds", parcel.AnyAVSounds.ToString());
            xtw.WriteElementString("GroupAVSounds", parcel.GroupAVSounds.ToString());
            xtw.WriteElementString("OtherCleanTime", Convert.ToString(parcel.OtherCleanTime));

            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();
            File.WriteAllText(filename, sw.ToString());
        }

        public static void SaveRegionSettings(Simulator sim, string settingsPath)
        {
            if (Directory.Exists(settingsPath))
                Directory.Delete(settingsPath, true);

            Directory.CreateDirectory(settingsPath);
            
            RegionSettings settings = new RegionSettings();
            //settings.AgentLimit;
            settings.AllowDamage = (sim.Flags & RegionFlags.AllowDamage) == RegionFlags.AllowDamage;
            //settings.AllowLandJoinDivide;
            settings.AllowLandResell = (sim.Flags & RegionFlags.BlockLandResell) != RegionFlags.BlockLandResell;
            settings.BlockFly = (sim.Flags & RegionFlags.NoFly) == RegionFlags.NoFly;
            settings.BlockLandShowInSearch = (sim.Flags & RegionFlags.BlockParcelSearch) == RegionFlags.BlockParcelSearch;
            settings.BlockTerraform = (sim.Flags & RegionFlags.BlockTerraform) == RegionFlags.BlockTerraform;
            settings.DisableCollisions = (sim.Flags & RegionFlags.SkipCollisions) == RegionFlags.SkipCollisions;
            settings.DisablePhysics = (sim.Flags & RegionFlags.SkipPhysics) == RegionFlags.SkipPhysics;
            settings.DisableScripts = (sim.Flags & RegionFlags.SkipScripts) == RegionFlags.SkipScripts;
            settings.FixedSun = (sim.Flags & RegionFlags.SunFixed) == RegionFlags.SunFixed;
            settings.MaturityRating = (int)(sim.Access & SimAccess.Mature & SimAccess.Adult & SimAccess.PG);
            //settings.ObjectBonus;
            settings.RestrictPushing = (sim.Flags & RegionFlags.RestrictPushObject) == RegionFlags.RestrictPushObject;
            settings.TerrainDetail0 = sim.TerrainDetail0;
            settings.TerrainDetail1 = sim.TerrainDetail1;
            settings.TerrainDetail2 = sim.TerrainDetail2;
            settings.TerrainDetail3 = sim.TerrainDetail3;
            settings.TerrainHeightRange00 = sim.TerrainHeightRange00;
            settings.TerrainHeightRange01 = sim.TerrainHeightRange01;
            settings.TerrainHeightRange10 = sim.TerrainHeightRange10;
            settings.TerrainHeightRange11 = sim.TerrainHeightRange11;
            settings.TerrainStartHeight00 = sim.TerrainStartHeight00;
            settings.TerrainStartHeight01 = sim.TerrainStartHeight01;
            settings.TerrainStartHeight10 = sim.TerrainStartHeight10;
            settings.TerrainStartHeight11 = sim.TerrainStartHeight11;
            //settings.UseEstateSun;
            settings.WaterHeight = sim.WaterHeight;

            settings.ToXML(Path.Combine(settingsPath, sim.Name + ".xml"));
        }

        public static async Task SavePrimsAsync(AssetManager manager, IList<AssetPrim> prims, string primsPath, string assetsPath)
        {
            Dictionary<UUID, UUID> textureList = new Dictionary<UUID, UUID>();

            // Delete all old linkset files
            try { Directory.Delete(primsPath, true); }
            catch (Exception) { }
            
            // Create a new folder for the linkset files
            try { Directory.CreateDirectory(primsPath); }
            catch (Exception ex)
            {
                Logger.Error("Failed saving prims: " + ex.Message);
                return;
            }

            try
            {
                foreach (AssetPrim assetPrim in prims)
                {
                    if (assetPrim.Parent == null) continue;

                    SavePrim(assetPrim, Path.Combine(primsPath, "Primitive_" + assetPrim.Parent.ID + ".xml"));

                    CollectTextures(assetPrim.Parent, textureList);
                    if (assetPrim.Children != null)
                    {
                        foreach (PrimObject child in assetPrim.Children)
                            CollectTextures(child, textureList);
                    }
                }

                await SaveAssetsAsync(manager, AssetType.Texture, new List<UUID>(textureList.Keys), assetsPath).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        static void CollectTextures(PrimObject prim, Dictionary<UUID, UUID> textureList)
        {
            if (prim.Textures != null)
            {
                // Add all textures on this prim to the save list
                if (prim.Textures.DefaultTexture != null)
                    textureList[prim.Textures.DefaultTexture.TextureID] = prim.Textures.DefaultTexture.TextureID;

                if (prim.Textures.FaceTextures != null)
                {
                    foreach (var face in prim.Textures.FaceTextures)
                    {
                        if (face != null)
                            textureList[face.TextureID] = face.TextureID;
                    }
                }
                if(prim.Sculpt != null && prim.Sculpt.Texture != UUID.Zero)
                    textureList[prim.Sculpt.Texture] = prim.Sculpt.Texture;
            }
        }

        public static void ClearAssetFolder(string assetsPath)
        {
            // Delete the assets folder
            try { Directory.Delete(assetsPath, true); }
            catch (Exception) { }

            // Create a new assets folder
            try { Directory.CreateDirectory(assetsPath); }
            catch (Exception ex)
            {
                Logger.Error("Failed saving assets: " + ex.Message);
            }
        }

        public static async Task SaveAssetsAsync(AssetManager assetManager, AssetType assetType, IList<UUID> assets, string assetsPath)
        {
            int count = 0;
            ArchiveConstants.ASSET_TYPE_TO_EXTENSION.TryGetValue(assetType, out var extension);
            extension ??= string.Empty;

            var tasks = assets.Select(async id =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                Assets.Asset? asset = assetType == AssetType.Texture
                    ? await assetManager.RequestImageAsync(id, cancellationToken: cts.Token).ConfigureAwait(false)
                    : await assetManager.RequestAssetAsync(id, assetType, false, cts.Token).ConfigureAwait(false);

                if (asset == null)
                {
                    Console.WriteLine("Missing asset " + id);
                    return;
                }

                File.WriteAllBytes(Path.Combine(assetsPath, id + extension), asset.AssetData);
                Interlocked.Increment(ref count);
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            Logger.Info("Copied " + count + " assets to the asset archive folder");
        }

        public static async Task SaveSimAssetsAsync(AssetManager assetManager, AssetType assetType, UUID assetID, UUID itemID, UUID primID, string assetsPath)
        {
            ArchiveConstants.ASSET_TYPE_TO_EXTENSION.TryGetValue(assetType, out var extension);
            extension ??= string.Empty;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var asset = await assetManager.RequestAssetAsync(assetID, itemID, primID, assetType, false,
                SourceType.SimInventoryItem, UUID.Random(), cts.Token).ConfigureAwait(false);

            if (asset == null) return;

            File.WriteAllBytes(Path.Combine(assetsPath, assetID + extension), asset.AssetData);
            Logger.Info("Copied 1 asset to the asset archive folder");
        }

        static void SavePrim(AssetPrim prim, string filename)
        {
            try
            {
                using (StreamWriter stream = new StreamWriter(filename))
                {
                    XmlTextWriter writer = new XmlTextWriter(stream);
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 4;
                    writer.IndentChar = ' ';
                    SOGToXml2(writer, prim);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed saving linkset: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes a linkset as the <c>SceneObjectGroup</c> XML that region archives hold. This is
        /// <see cref="SceneObjectGroupXmlWriter.Write"/>, which <see cref="AssetPrim.EncodeXml"/> also uses.
        /// </summary>
        public static void SOGToXml2(XmlTextWriter writer, AssetPrim prim)
        {
            SceneObjectGroupXmlWriter.Write(writer, prim);
        }

        #endregion Archive Saving
    }
}

