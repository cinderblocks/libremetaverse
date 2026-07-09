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
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary>
    /// One entry of a ModifyMaterialParams batch update, targeting a single face (TextureEntry) of
    /// a single object. Mirrors the per-entry shape validated by the reference viewer's
    /// is_valid_update (llgltfmateriallist.cpp): object_id + side are always required, plus at
    /// least one of AssetId (apply a material asset to this face) or Override (set/replace the
    /// face's material override; leave both null to clear the face's override entirely).
    /// </summary>
    public class GLTFMaterialUpdate
    {
        public UUID ObjectId { get; set; }
        /// <summary>TextureEntry face index this update applies to.</summary>
        public int Side { get; set; }
        /// <summary>Base material asset to apply to this face, or null to leave the current base
        /// material (if any) unchanged.</summary>
        public UUID? AssetId { get; set; }
        /// <summary>Material override to set/replace on this face, or null. When both
        /// <see cref="AssetId"/> and <see cref="Override"/> are null, this update clears the face's
        /// existing override entirely (sends an empty "gltf_json").</summary>
        public AssetMaterial? Override { get; set; }
    }

    public partial class ObjectManager
    {
        /// <summary>
        /// Sends a batch of per-face GLTF material updates via the ModifyMaterialParams capability.
        /// Mirrors LLGLTFMaterialList::flushUpdatesOnce/modifyMaterialCoro in the reference viewer
        /// (llgltfmateriallist.cpp): the request body is a bare LLSD array of update maps (not
        /// wrapped in an outer object), and the response is a single {"success","message"} result
        /// for the whole batch -- not one result per update. The reference viewer batches up to 255
        /// updates per POST and queues the rest; this method sends exactly the batch given to it, so
        /// callers targeting many faces at once should chunk themselves if they care about that limit.
        /// </summary>
        /// <param name="sim">The <see cref="Simulator"/> to send the update to</param>
        /// <param name="updates">One or more per-face update entries</param>
        /// <param name="cancellationToken">Cancellation token for the request</param>
        /// <returns>True if the server accepted the batch</returns>
        public async Task<bool> SendMaterialUpdatesAsync(Simulator sim, IEnumerable<GLTFMaterialUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            if (sim == null) { throw new ArgumentNullException(nameof(sim)); }
            if (updates == null) { throw new ArgumentNullException(nameof(updates)); }

            var uri = sim.Caps?.CapabilityURI("ModifyMaterialParams");
            if (uri == null)
            {
                Logger.Warn("ModifyMaterialParams capability not available.", Client);
                return false;
            }

            var body = new OSDArray();
            foreach (var update in updates)
            {
                var entry = new OSDMap
                {
                    ["object_id"] = OSD.FromUUID(update.ObjectId),
                    ["side"] = OSD.FromInteger(update.Side)
                };

                if (update.AssetId.HasValue)
                {
                    entry["asset_id"] = OSD.FromUUID(update.AssetId.Value);
                }

                if (update.Override != null)
                {
                    entry["gltf_json"] = OSD.FromString(update.Override.ToJson());
                }
                else if (!update.AssetId.HasValue)
                {
                    // Neither an asset to apply nor an override to set -- clear any existing override.
                    entry["gltf_json"] = OSD.FromString(string.Empty);
                }

                body.Add(entry);
            }

            try
            {
                var (response, data) = await Client.HttpCapsClient.PostAsync(uri, OSDFormat.Xml, body, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"ModifyMaterialParams POST non-success status: {response.StatusCode}", Client);
                    return false;
                }
                if (data == null) { return false; }

                if (OSDParser.Deserialize(data) is OSDMap result)
                {
                    if (!result["success"].AsBoolean())
                    {
                        Logger.Warn($"ModifyMaterialParams rejected update: {result["message"].AsString()}", Client);
                    }
                    return result["success"].AsBoolean();
                }
                return false;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed sending ModifyMaterialParams update", ex, Client);
                return false;
            }
        }

        /// <summary>Sets or replaces a face's material override via the ModifyMaterialParams
        /// capability. See <see cref="SendMaterialUpdatesAsync"/>.</summary>
        public Task<bool> SetMaterialOverrideAsync(Simulator sim, UUID objectId, int side, AssetMaterial overrideMaterial,
            CancellationToken cancellationToken = default)
        {
            if (overrideMaterial == null) { throw new ArgumentNullException(nameof(overrideMaterial)); }
            return SendMaterialUpdatesAsync(sim,
                new[] { new GLTFMaterialUpdate { ObjectId = objectId, Side = side, Override = overrideMaterial } },
                cancellationToken);
        }

        /// <summary>Clears a face's material override (reverting to its applied base material, if
        /// any) via the ModifyMaterialParams capability. See <see cref="SendMaterialUpdatesAsync"/>.</summary>
        public Task<bool> ClearMaterialOverrideAsync(Simulator sim, UUID objectId, int side,
            CancellationToken cancellationToken = default)
        {
            return SendMaterialUpdatesAsync(sim,
                new[] { new GLTFMaterialUpdate { ObjectId = objectId, Side = side } },
                cancellationToken);
        }

        /// <summary>Applies a material asset to a face, optionally setting an override at the same
        /// time, via the ModifyMaterialParams capability. See <see cref="SendMaterialUpdatesAsync"/>.</summary>
        public Task<bool> ApplyMaterialAsync(Simulator sim, UUID objectId, int side, UUID materialAssetId,
            AssetMaterial? overrideMaterial = null, CancellationToken cancellationToken = default)
        {
            return SendMaterialUpdatesAsync(sim,
                new[] { new GLTFMaterialUpdate { ObjectId = objectId, Side = side, AssetId = materialAssetId, Override = overrideMaterial } },
                cancellationToken);
        }
    }
}
