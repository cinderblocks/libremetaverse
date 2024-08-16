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

namespace OpenMetaverse.Assets
{
    /// <summary>
    /// Represents a Landmark with RegionID and Position vector
    /// </summary>
    public class AssetLandmark : Asset
    {
        /// <summary>Override the base classes AssetType</summary>
        public override AssetType AssetType => AssetType.Landmark;

        /// <summary>UUID of the Landmark target region</summary>
        public UUID RegionID = UUID.Zero;
        /// <summary> Local position of the target </summary>
        public Vector3 Position = Vector3.Zero;

        /// <summary>Construct an Asset of type Landmark</summary>
        public AssetLandmark() { }

        /// <summary>
        /// Construct an Asset object of type Landmark
        /// </summary>
        /// <param name="assetID">A unique <see cref="UUID"/> specific to this asset</param>
        /// <param name="assetData">A byte array containing the raw asset data</param>
        public AssetLandmark(UUID assetID, byte[] assetData)
            : base(assetID, assetData)
        {
        }

        /// <summary>
        /// Encode the raw contents of a string with the specific Landmark format
        /// </summary>
        public sealed override void Encode()
        {
            string temp = "Landmark version 2\n";
            temp += "region_id " + RegionID + "\n";
            temp += string.Format(Utils.EnUsCulture, "local_pos {0:0.00} {1:0.00} {2:0.00}\n", Position.X, Position.Y, Position.Z);
            AssetData = Utils.StringToBytes(temp);
        }

        /// <summary>
        /// Decode the raw asset data, populating the RegionID and Position
        /// </summary>
        /// <returns>true if the AssetData was successfully decoded to a UUID and Vector</returns>
        public sealed override bool Decode()
        {
            string text = Utils.BytesToString(AssetData);
            if (text.ToLower().Contains("landmark version 2"))
            {
                RegionID = new UUID(text.Substring(text.IndexOf("region_id", StringComparison.Ordinal) + 10, 36));
                const string vecDelim = " ";
                string[] vecStrings = text.Substring(text.IndexOf("local_pos", StringComparison.Ordinal) + 10).Split(vecDelim.ToCharArray());
                if (vecStrings.Length == 3)
                {
                    Position = new Vector3(float.Parse(vecStrings[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(vecStrings[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(vecStrings[2], System.Globalization.CultureInfo.InvariantCulture));
                    return true;
                }
            }
            return false;
        }
    }
}
