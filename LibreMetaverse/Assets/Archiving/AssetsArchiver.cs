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
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OpenMetaverse.Assets
{
    /// <summary>
    /// Archives assets
    /// </summary>
    public class AssetsArchiver
    {
        ///// <value>
        ///// Post a message to the log every x assets as a progress bar
        ///// </value>
        //static int LOG_ASSET_LOAD_NOTIFICATION_INTERVAL = 50;

        /// <summary>
        /// Archive assets
        /// </summary>
        protected IDictionary<UUID, Asset> m_assets;

        public AssetsArchiver(IDictionary<UUID, Asset> assets)
        {
            m_assets = assets;
        }

        /// <summary>
        /// Archive the assets given to this archiver to the given archive.
        /// </summary>
        /// <param name="archive"></param>
        public void Archive(TarArchiveWriter archive)
        {
            //WriteMetadata(archive);
            WriteData(archive);
        }

        /// <summary>
        /// Write an assets metadata file to the given archive
        /// </summary>
        /// <param name="archive"></param>
        protected void WriteMetadata(TarArchiveWriter archive)
        {
            StringWriter sw = new StringWriter();
            using (XmlTextWriter xtw = new XmlTextWriter(sw))
            {
                xtw.Formatting = Formatting.Indented;
                xtw.WriteStartDocument();

                xtw.WriteStartElement("assets");

                foreach (UUID uuid in m_assets.Keys)
                {
                    Asset asset = m_assets[uuid];

                    if (asset != null)
                    {
                        xtw.WriteStartElement("asset");

                        string extension = string.Empty;

                        if (ArchiveConstants.ASSET_TYPE_TO_EXTENSION.TryGetValue(asset.AssetType, out var value))
                        {
                            extension = value;
                        }

                        xtw.WriteElementString("filename", uuid + extension);

                        xtw.WriteElementString("name", uuid.ToString());
                        xtw.WriteElementString("description", string.Empty);
                        xtw.WriteElementString("asset-type", asset.AssetType.ToString());

                        xtw.WriteEndElement();
                    }
                }

                xtw.WriteEndElement();
                xtw.WriteEndDocument();
                archive.WriteFile("assets.xml", sw.ToString());
            }
        }

        /// <summary>
        /// Write asset data files to the given archive
        /// </summary>
        /// <param name="archive"></param>
        protected void WriteData(TarArchiveWriter archive)
        {
            // It appears that gtar, at least, doesn't need the intermediate directory entries in the tar
            //archive.AddDir("assets");

            int assetsAdded = 0;

            foreach (UUID uuid in m_assets.Keys)
            {
                Asset asset = m_assets[uuid];

                string extension = string.Empty;

                if (ArchiveConstants.ASSET_TYPE_TO_EXTENSION.TryGetValue(asset.AssetType, out var value))
                {
                    extension = ArchiveConstants.ASSET_TYPE_TO_EXTENSION[asset.AssetType];
                }
                else
                {
                    Logger.Log(string.Format(
                        "Unrecognized asset type {0} with uuid {1}.  This asset will be saved but not reloaded",
                        asset.AssetType, asset.AssetID), Helpers.LogLevel.Warning);
                }

                asset.Encode();

                archive.WriteFile(
                    ArchiveConstants.ASSETS_PATH + uuid + extension,
                    asset.AssetData);

                assetsAdded++;
            }
        }
    }
}
