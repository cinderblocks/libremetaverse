/*
 * Copyright (c) 2026, Sjofn LLC
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.ImportExport;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>
        /// Uploads one or more model prims (e.g. from <see cref="ColladaLoader"/>) as a single mesh
        /// inventory item, via the two-phase <c>NewFileAgentInventory</c> flow. Mirrors
        /// LLMeshUploadThread::requestWholeModelFee/doWholeModelUpload (llmeshrepository.cpp):
        /// phase 1 POSTs a fee-quote request (name/permissions/asset_resources) to
        /// <c>NewFileAgentInventory</c> and gets back a quoted L$ price plus a one-time
        /// <c>uploader</c> URL; phase 2 POSTs the same <c>asset_resources</c> map to that URL,
        /// which creates the inventory item and charges the fee.
        /// </summary>
        /// <remarks>
        /// Known simplifications versus a full creator-tool uploader:
        /// <list type="bullet">
        /// <item>Only a single LOD (<c>high_lod</c>) is sent per prim -- <see cref="ModelPrim.CreateAsset"/>
        /// does not generate reduced medium/low/lowest LODs. The simulator accepts this; every
        /// viewing distance simply renders the same (full-detail) geometry.</item>
        /// <item>Physics uses a fixed placeholder convex hull (<see cref="ModelPrim.PhysicsStub"/>)
        /// rather than a real decomposition of the uploaded geometry.</item>
        /// <item>The fee-quote request sends only image dimensions for each texture, not the real
        /// J2C bytes (matching the reference viewer). Dimensions come from <see cref="ModelMaterial.Width"/>/
        /// <see cref="ModelMaterial.Height"/>, which <see cref="ColladaLoader"/> fills in for any
        /// texture it decodes and re-encodes itself. A texture file that is already <c>.jp2</c>/<c>.j2c</c>
        /// is passed through unread, so its dimensions are unknown -- in that case (or any other time
        /// dimensions aren't available) the fee quote falls back to sending the real bytes for that
        /// upload, so the server is never quoted against a fabricated width/height.</item>
        /// <item>Each <see cref="ModelPrim"/> becomes its own <c>mesh_list</c> entry -- geometry
        /// instancing (multiple prims sharing one mesh at different transforms) is not detected or
        /// deduplicated.</item>
        /// </list>
        /// </remarks>
        /// <param name="prims">One or more prims to upload as a single linked mesh object. The
        /// first prim becomes the root.</param>
        /// <param name="name">Inventory item / object name.</param>
        /// <param name="description">Inventory item description.</param>
        /// <param name="folderID">Destination inventory folder for both the new item and any
        /// embedded textures.</param>
        /// <param name="permissions">Permissions to apply to the new item.</param>
        /// <param name="confirmCost">Optional delegate called with the server-quoted upload price
        /// in L$. Return <c>true</c> to proceed, <c>false</c> to cancel before any fee is charged.
        /// If <c>null</c>, the upload always proceeds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Optional upload progress reporter.</param>
        public async Task<CreateItemFromAssetResult> UploadModelAsync(
            IReadOnlyList<ModelPrim> prims, string name, string description, UUID folderID,
            Permissions permissions, Func<int, bool>? confirmCost = null,
            CancellationToken cancellationToken = default, IProgress<HttpCapsClient.ProgressReport>? progress = null)
        {
            if (prims == null) { throw new ArgumentNullException(nameof(prims)); }
            if (prims.Count == 0) { throw new ArgumentException("At least one ModelPrim is required", nameof(prims)); }

            var result = new CreateItemFromAssetResult
            {
                Success = false,
                Status = "",
                ItemID = UUID.Zero,
                AssetID = UUID.Zero,
                Error = null,
                RawResult = null
            };

            var cap = GetCapabilityURI("NewFileAgentInventory", false);
            if (cap == null)
            {
                result.Status = "capability_missing";
                result.Error = new InvalidOperationException("NewFileAgentInventory capability is not currently available");
                return result;
            }

            try
            {
                // mesh_list/instance_list are identical for both phases and built once; texture_list
                // is swapped in place below (dimensions-only for the fee quote, real bytes for the
                // final upload) per LLMeshUploadThread::wholeModelToLLSD's include_textures switch.
                // If any texture lacks known dimensions (e.g. a pre-encoded .jp2/.j2c passed straight
                // through by ColladaLoader without ever being decoded), fall back to sending real
                // bytes for the quote too -- the reference viewer's fallback is to measure the actual
                // texture rather than ever quote against width=0/height=0.
                var assetResources = BuildAssetResources(prims, Client.Self.AgentID, out var texturesInOrder);
                var haveAllDimensions = texturesInOrder.All(m => m.Width > 0 && m.Height > 0);
                ApplyTextureList(assetResources, texturesInOrder, includeTextures: !haveAllDimensions);

                var feeRequest = new OSDMap
                {
                    {"folder_id", OSD.FromUUID(folderID)},
                    {"texture_folder_id", OSD.FromUUID(folderID)},
                    {"asset_type", OSD.FromString("mesh")},
                    {"inventory_type", OSD.FromString("object")},
                    {"name", OSD.FromString(name)},
                    {"description", OSD.FromString(description)},
                    {"everyone_mask", OSD.FromInteger((int) permissions.EveryoneMask)},
                    {"group_mask", OSD.FromInteger((int) permissions.GroupMask)},
                    {"next_owner_mask", OSD.FromInteger((int) permissions.NextOwnerMask)},
                    {"asset_resources", assetResources}
                };

                // Phase 1 -- ask the server for the upload price.
                var osd = await PostCapAsync(cap, feeRequest, cancellationToken, progress).ConfigureAwait(false);
                result.RawResult = osd;

                if (osd is not OSDMap feeResponse)
                {
                    result.Status = "invalid_response";
                    return result;
                }

                var state = feeResponse.ContainsKey("state") ? feeResponse["state"].AsString() : string.Empty;
                if (state != "upload")
                {
                    result.Status = $"unexpected_state:{state}";
                    return result;
                }

                var uploaderUri = feeResponse.ContainsKey("uploader") ? feeResponse["uploader"].AsUri() : null;
                if (uploaderUri == null || uploaderUri.ToString() == "about:blank")
                {
                    result.Status = "missing_uploader_url";
                    return result;
                }

                var uploadPrice = feeResponse.ContainsKey("upload_price") ? feeResponse["upload_price"].AsInteger() : 0;

                // Phase 2 -- let the caller approve the quoted price before it's charged.
                if (confirmCost != null && !confirmCost(uploadPrice))
                {
                    result.Status = "cost_rejected";
                    result.Error = new OperationCanceledException(
                        $"Upload cancelled: quoted price {uploadPrice} L$ was rejected by confirmCost delegate");
                    return result;
                }

                // Phase 3 -- swap in the real texture bytes, then POST asset_resources to the
                // uploader URL; the server creates the item and charges the previously-quoted fee.
                ApplyTextureList(assetResources, texturesInOrder, includeTextures: true);
                var uploadOsd = await PostCapAsync(uploaderUri, assetResources, cancellationToken, progress).ConfigureAwait(false);
                result.RawResult = uploadOsd;

                if (uploadOsd is not OSDMap uploadResponse)
                {
                    result.Status = "invalid_upload_response";
                    return result;
                }

                state = uploadResponse.ContainsKey("state") ? uploadResponse["state"].AsString() : string.Empty;
                result.Status = state;

                if (state == "complete" &&
                    uploadResponse.ContainsKey("new_inventory_item") && uploadResponse.ContainsKey("new_asset"))
                {
                    result.ItemID = uploadResponse["new_inventory_item"].AsUUID();
                    result.AssetID = uploadResponse["new_asset"].AsUUID();

                    try { RequestFetchInventory(result.ItemID, Client.Self.AgentID, cancellationToken); }
                    catch { /* best-effort */ }

                    result.Success = true;
                    return result;
                }

                result.Success = false;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                result.Status = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Builds the <c>asset_resources</c> map (<c>mesh_list</c>/<c>instance_list</c>/
        /// <c>source_format</c>/<c>metric</c>) shared by both phases of <see cref="UploadModelAsync"/>,
        /// plus the ordered list of distinct textures referenced by <c>face_list.image</c> indices.
        /// <c>texture_list</c> itself is left empty here -- call <see cref="ApplyTextureList"/> to
        /// fill it in per-phase. Mirrors LLMeshUploadThread::wholeModelToLLSD and packModelIntance
        /// (llmeshrepository.cpp), simplified per the remarks on <see cref="UploadModelAsync"/> (no
        /// mesh dedup, no multi-LOD, no real physics).
        /// </summary>
        private static OSDMap BuildAssetResources(
            IReadOnlyList<ModelPrim> prims, UUID creatorId, out List<ModelMaterial> texturesInOrder)
        {
            var meshList = new OSDArray();
            var instanceList = new OSDArray();
            var textureIndex = new Dictionary<ModelMaterial, int>();
            texturesInOrder = new List<ModelMaterial>();

            for (var i = 0; i < prims.Count; i++)
            {
                var prim = prims[i];
                prim.CreateAsset(creatorId);
                meshList.Add(OSD.FromBinary(prim.Asset));

                var instanceEntry = new OSDMap
                {
                    ["position"] = OSD.FromVector3(prim.Position),
                    ["rotation"] = OSD.FromQuaternion(prim.Rotation),
                    ["scale"] = OSD.FromVector3(prim.Scale),
                    ["material"] = OSD.FromInteger((int) Material.Wood),
                    ["physics_shape_type"] = OSD.FromInteger((int) PhysicsShapeType.ConvexHull),
                    ["mesh"] = OSD.FromInteger(i),
                    ["mesh_name"] = OSD.FromString(string.IsNullOrEmpty(prim.ID) ? $"model_{i}" : prim.ID)
                };

                var faceList = new OSDArray();
                foreach (var face in prim.Faces)
                {
                    var faceEntry = new OSDMap
                    {
                        ["diffuse_color"] = OSD.FromColor4(face.Material.DiffuseColor),
                        ["fullbright"] = OSD.FromBoolean(false)
                    };

                    if (face.Material.TextureData.Length > 0)
                    {
                        if (!textureIndex.TryGetValue(face.Material, out var texIdx))
                        {
                            texIdx = textureIndex.Count;
                            textureIndex[face.Material] = texIdx;
                            texturesInOrder.Add(face.Material);
                        }

                        faceEntry["image"] = OSD.FromInteger(texIdx);
                        faceEntry["scales"] = OSD.FromReal(1.0);
                        faceEntry["scalet"] = OSD.FromReal(1.0);
                        faceEntry["offsets"] = OSD.FromReal(0.0);
                        faceEntry["offsett"] = OSD.FromReal(0.0);
                        faceEntry["imagerot"] = OSD.FromReal(0.0);
                    }

                    faceList.Add(faceEntry);
                }
                instanceEntry["face_list"] = faceList;

                instanceList.Add(instanceEntry);
            }

            return new OSDMap
            {
                ["mesh_list"] = meshList,
                ["texture_list"] = new OSDArray(),
                ["instance_list"] = instanceList,
                ["source_format"] = new OSDMap { ["high"] = OSD.FromInteger(0) },
                ["metric"] = OSD.FromString("MUT_Unspecified")
            };
        }

        /// <summary>
        /// Fills in <paramref name="assetResources"/>'s <c>texture_list</c> (and, for the fee-quote
        /// phase, the parallel <c>texture_info</c> width/height array) to match
        /// LLMeshUploadThread::wholeModelToLLSD's <c>include_textures</c> switch: the fee-quote phase
        /// sends only image dimensions -- so the server doesn't assume a worst-case 1024x1024 and
        /// over-quote the price -- while the final upload phase sends the real J2C bytes.
        /// </summary>
        private static void ApplyTextureList(
            OSDMap assetResources, IReadOnlyList<ModelMaterial> texturesInOrder, bool includeTextures)
        {
            var textureList = new OSDArray();
            var textureInfo = includeTextures ? null : new OSDArray();

            foreach (var material in texturesInOrder)
            {
                if (includeTextures)
                {
                    textureList.Add(OSD.FromBinary(material.TextureData));
                }
                else
                {
                    textureList.Add(OSD.FromBinary(Array.Empty<byte>()));
                    textureInfo!.Add(new OSDMap
                    {
                        ["width"] = OSD.FromInteger(material.Width),
                        ["height"] = OSD.FromInteger(material.Height)
                    });
                }
            }

            assetResources["texture_list"] = textureList;
            if (textureInfo != null)
            {
                assetResources["texture_info"] = textureInfo;
            }
            else
            {
                assetResources.Remove("texture_info");
            }
        }
    }
}
