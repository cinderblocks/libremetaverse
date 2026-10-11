/*
 * Copyright (c) 2026, Sjofn LLC.
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
using LibreMetaverse.Assets;
using LibreMetaverse.ImportExport;
using static LibreMetaverse.HttpCapsClient;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        /// <summary>How long to wait for the grid to say it made an inventory item</summary>
        internal static TimeSpan ItemCreateTimeout = TimeSpan.FromSeconds(30);

        // Where tests stand in for the grid's replies to the item messages, which travel by UDP
        internal Func<UUID, string, string, PermissionMask, CancellationToken, Task<InventoryItem?>>? CreateMaterialItemHook;
        internal Action<InventoryItem>? UpdateItemHook;

        /// <summary>
        /// Makes a new material in inventory from a material asset whose textures have been uploaded already, the way the
        /// viewer's material editor saves a new one (<c>LLMaterialEditor::saveIfNeeded</c> and <c>LLObjectsMaterialItemCallback</c>
        /// in llmaterialeditor.cpp): an empty material item is created in the Materials folder, its permissions are set (the
        /// create message only takes the next owner's), and the material is uploaded to it through the
        /// <c>UpdateMaterialAgentInventory</c> capability.
        /// </summary>
        /// <remarks>
        /// If something fails after the item was made, the empty item is still in inventory and
        /// <see cref="CreateItemFromAssetResult.ItemID"/> says which it is.
        /// </remarks>
        /// <param name="material">The material, with the asset IDs of its textures in <see cref="AssetMaterial.TextureIds"/></param>
        /// <param name="name">Name of the new item</param>
        /// <param name="description">Description of the new item</param>
        /// <param name="permissions">Permissions of the new item. The owner has all of them, whatever this says.</param>
        /// <param name="folderID">Where to put it. The Materials folder if <c>null</c>.</param>
        public async Task<MaterialUploadResult> CreateMaterialAsync(AssetMaterial material, string name, string description,
            Permissions permissions, UUID? folderID = null, CancellationToken cancellationToken = default,
            IProgress<ProgressReport>? progress = null)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (name == null) throw new ArgumentNullException(nameof(name));

            var result = new MaterialUploadResult { Success = false, Status = "" };

            // Checked first, so nothing is made that cannot be finished
            if (GetCapabilityURI("UpdateMaterialAgentInventory", false) == null)
            {
                result.Status = "capability_missing";
                result.Error = new InvalidOperationException("UpdateMaterialAgentInventory capability is not currently available");
                return result;
            }

            var folder = folderID ?? FindFolderForType(FolderType.Material);
            if (folder == UUID.Zero)
            {
                result.Status = "no_material_folder";
                result.Error = new InvalidOperationException("The inventory has no folder for materials");
                return result;
            }

            InventoryItem? item;
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(ItemCreateTimeout);
                    try
                    {
                        item = CreateMaterialItemHook != null
                            ? await CreateMaterialItemHook(folder, name, description, permissions.NextOwnerMask, timeout.Token).ConfigureAwait(false)
                            : await CreateItemAsync(folder, name, description, AssetType.Material, UUID.Random(), InventoryType.Material,
                                permissions.NextOwnerMask, timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        result.Status = "item_create_timeout";
                        result.Error = new TimeoutException("The grid did not say it had made the material item");
                        return result;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Status = ex.Message;
                result.Error = ex;
                return result;
            }

            if (item == null)
            {
                result.Status = "item_not_created";
                result.Error = new InvalidOperationException("The grid refused to make the material item");
                return result;
            }
            result.ItemID = item.UUID;

            // The message that makes the item sets the next owner's permissions and nothing else
            var wanted = new Permissions((uint)PermissionMask.All, (uint)permissions.EveryoneMask, (uint)permissions.GroupMask,
                (uint)permissions.NextOwnerMask, (uint)PermissionMask.All);
            if (item.Permissions != wanted)
            {
                item.Permissions = wanted;
                if (UpdateItemHook != null) UpdateItemHook(item);
                else RequestUpdateItem(item);
            }

            var upload = await RequestUpdateMaterialAgentInventoryAsync(material, item.UUID, cancellationToken, progress).ConfigureAwait(false);
            result.Success = upload.success;
            result.Status = upload.status;
            result.AssetID = upload.assetID;
            return result;
        }

        /// <summary>
        /// Makes a new material in inventory from a glTF material read by <c>GltfLoader</c>: the textures it has are uploaded
        /// first, as the viewer does (it waits for each upload and then puts the new asset IDs in the material), and then the
        /// material is made with <see cref="CreateMaterialAsync(AssetMaterial, string, string, Permissions, UUID?, CancellationToken, IProgress{ProgressReport})"/>.
        /// </summary>
        /// <remarks>
        /// The textures have to have been loaded (<c>GltfLoader.LoadPbrTextures</c>, with images). One that was not is left
        /// out of the material, and <see cref="MaterialUploadResult.Diagnostics"/> says so. A texture costs the usual upload fee,
        /// and a texture that was uploaded stays in inventory if the material fails; pass the same
        /// <paramref name="uploadedTextures"/> to retry, or to make several materials that share textures, and it is not uploaded again.
        /// </remarks>
        /// <param name="pbr">The material</param>
        /// <param name="permissions">Permissions of the material and of its textures</param>
        /// <param name="name">Name of the material. The name the model gave it if <c>null</c>.</param>
        /// <param name="description">Description of the material</param>
        /// <param name="folderID">Where to put the material. The Materials folder if <c>null</c>.</param>
        /// <param name="textureFolderID">Where to put the textures. The Textures folder if <c>null</c>.</param>
        /// <param name="uploadedTextures">Textures that are already uploaded, and where the new ones are put</param>
        /// <param name="confirmCost">Called with what the textures will cost together, before any is uploaded. Return false to stop.</param>
        public async Task<MaterialUploadResult> CreateMaterialAsync(ModelPbrMaterial pbr, Permissions permissions,
            string? name = null, string? description = null, UUID? folderID = null, UUID? textureFolderID = null,
            IDictionary<ModelTexture, UUID>? uploadedTextures = null, Func<int, bool>? confirmCost = null,
            CancellationToken cancellationToken = default, IProgress<ProgressReport>? progress = null)
        {
            if (pbr == null) throw new ArgumentNullException(nameof(pbr));

            var uploaded = uploadedTextures ?? new Dictionary<ModelTexture, UUID>();
            var diagnostics = new List<ModelDiagnostic>();
            var result = new MaterialUploadResult { Success = false, Status = "", Diagnostics = diagnostics };
            string materialName = !string.IsNullOrEmpty(name) ? name! : !string.IsNullOrEmpty(pbr.Name) ? pbr.Name : "Material";

            var slots = new[]
            {
                (pbr.BaseColorTexture, "Base Color"), (pbr.NormalTexture, "Normal"),
                (pbr.MetallicRoughnessTexture, "Metallic Roughness"), (pbr.EmissiveTexture, "Emissive")
            };

            var pending = new List<(ModelTexture Texture, string Label)>();
            foreach (var (texture, label) in slots)
            {
                if (texture == null || uploaded.ContainsKey(texture) || pending.Any(p => ReferenceEquals(p.Texture, texture))) continue;
                if (texture.Data.Length == 0)
                {
                    diagnostics.Add(new ModelDiagnostic(ModelDiagnosticSeverity.Warning, ModelDiagnosticCode.TextureNotLoaded,
                        $"The {label} texture of {materialName} was not loaded, so the material has none", texture.Name));
                    continue;
                }
                pending.Add((texture, label));
            }

            // Everything that can be found out before paying for a texture
            if (GetCapabilityURI("UpdateMaterialAgentInventory", false) == null || (pending.Count > 0 && GetCapabilityURI("NewFileAgentInventory", false) == null))
            {
                result.Status = "capability_missing";
                result.Error = new InvalidOperationException("The capabilities to upload a material and its textures are not currently available");
                return result;
            }

            if (pending.Count > 0 && confirmCost != null && !confirmCost(pending.Count * GetUploadCostForAssetType(AssetType.Texture)))
            {
                result.Status = "cost_rejected";
                result.Error = new OperationCanceledException("The cost of the textures was rejected by the confirmCost delegate");
                return result;
            }

            var textureFolder = textureFolderID ?? FindFolderForType(FolderType.Texture);
            foreach (var (texture, label) in pending)
            {
                var upload = await CreateItemFromAssetAsync(texture.Data, $"{materialName} ({label})", string.Empty, AssetType.Texture,
                    InventoryType.Texture, textureFolder, permissions, cancellationToken, progress).ConfigureAwait(false);
                if (!upload.Success)
                {
                    result.Status = "texture_upload_failed:" + upload.Status;
                    result.Error = upload.Error;
                    result.Textures = Used(pbr, uploaded);
                    return result;
                }
                uploaded[texture] = upload.AssetID;
            }

            var asset = pbr.ToAssetMaterial(t => uploaded.TryGetValue(t, out var id) ? id : UUID.Zero);
            var made = await CreateMaterialAsync(asset, materialName, description ?? string.Empty, permissions, folderID, cancellationToken, progress).ConfigureAwait(false);

            made.Textures = Used(pbr, uploaded);
            made.Diagnostics = diagnostics;
            return made;
        }

        private static IReadOnlyDictionary<ModelTexture, UUID> Used(ModelPbrMaterial pbr, IDictionary<ModelTexture, UUID> uploaded)
        {
            var used = new Dictionary<ModelTexture, UUID>();
            foreach (var texture in new[] { pbr.BaseColorTexture, pbr.NormalTexture, pbr.MetallicRoughnessTexture, pbr.EmissiveTexture })
            {
                if (texture != null && uploaded.TryGetValue(texture, out var id)) used[texture] = id;
            }
            return used;
        }
    }
}
