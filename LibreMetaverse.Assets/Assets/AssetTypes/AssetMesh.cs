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
using System.IO;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Assets
{
    /// <summary>
    /// Represents Mesh asset
    /// </summary>
    public class AssetMesh : Asset
    {
        /// <summary>Override the base classes AssetType</summary>
        public override AssetType AssetType => AssetType.Mesh;

        /// <summary>
        /// Decoded mesh data
        /// </summary>
        public OSDMap MeshData = new OSDMap();

        /// <summary>Maximum number of parts (LODs, physics, skin, etc.) a mesh asset header may declare</summary>
        public static int MaxParts = 64;

        /// <summary>Maximum number of bytes a single mesh part may inflate to</summary>
        public static int MaxInflatedPartBytes = 16 * 1024 * 1024;

        /// <summary>Maximum number of bytes all the parts of one mesh asset may inflate to in total</summary>
        public static long MaxInflatedAssetBytes = 64L * 1024 * 1024;

        /// <summary>Maximum number of values all the parts of one mesh asset may contain in total.
        /// Real meshes keep geometry in binary blobs and have only a few hundred.</summary>
        public static int MaxDecodedElements = 2 * 1024 * 1024;

        /// <summary>Initializes a new instance of an AssetMesh object</summary>
        public AssetMesh() { }

        /// <summary>Initializes a new instance of an AssetMesh object with parameters</summary>
        /// <param name="assetID">A unique <see cref="UUID"/> specific to this asset</param>
        /// <param name="assetData">A byte array containing the raw asset data</param>
        public AssetMesh(UUID assetID, byte[] assetData)
            : base(assetID, assetData)
        {
        }

        /// <summary>
        /// TODO: Encodes Collada file into LLMesh format
        /// </summary>
        public sealed override void Encode() { }

        /// <summary>
        /// Decodes mesh asset. See <see cref="LibreMetaverse.Rendering.FacetedMesh.TryDecodeFromAsset"/>
        /// to furter decode it for rendering</summary>
        /// <returns>true if the asset was decoded, false if it is malformed or exceeds the
        /// size limits (<see cref="MaxParts"/>, <see cref="MaxInflatedPartBytes"/>,
        /// <see cref="MaxInflatedAssetBytes"/>, <see cref="MaxDecodedElements"/>)</returns>
        public sealed override bool Decode()
        {
            try
            {
                MeshData = new OSDMap();

                using (MemoryStream data = new MemoryStream(AssetData))
                {
                    OSDMap header = (OSDMap)OSDParser.DeserializeLLSDBinary(data);
                    MeshData["asset_header"] = header;
                    long start = data.Position;
                    int parts = 0;
                    long remaining = MaxInflatedAssetBytes;
                    int elementBudget = MaxDecodedElements;

                    foreach(string partName in header.Keys)
                    {
                        if (header[partName].Type != OSDType.Map)
                        {
                            MeshData[partName] = header[partName];
                            continue;
                        }

                        if (++parts > MaxParts)
                            throw new InvalidDataException($"Mesh asset has more than {MaxParts} parts");

                        OSDMap partInfo = (OSDMap)header[partName];
                        if (!partInfo.TryGetValue("offset", out OSD offsetOsd) || !partInfo.TryGetValue("size", out OSD sizeOsd)
                            || offsetOsd.Type != OSDType.Integer || sizeOsd.Type != OSDType.Integer)
                        {
                            MeshData[partName] = partInfo;
                            continue;
                        }

                        long offset = offsetOsd.AsInteger();
                        long size = sizeOsd.AsInteger();
                        if (offset < 0 || size == 0)
                        {
                            MeshData[partName] = partInfo;
                            continue;
                        }

                        // The header is untrusted: check the declared range against the bytes we
                        // actually have before allocating a buffer for it.
                        if (size < 0 || start + offset + size > AssetData.Length)
                            throw new InvalidDataException($"Mesh part {partName} extends past the end of the asset");

                        byte[] part = new byte[size];
                        Buffer.BlockCopy(AssetData, (int)(start + offset), part, 0, part.Length);
                        MeshData[partName] = Helpers.DecompressOSD(part, (int)Math.Min(MaxInflatedPartBytes, remaining), out int inflated, ref elementBudget);
                        remaining -= inflated;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to decode mesh asset", ex);
                return false;
            }
        }
    }
}

