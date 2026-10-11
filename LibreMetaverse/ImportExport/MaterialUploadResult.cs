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

namespace LibreMetaverse.ImportExport
{
    /// <summary>The outcome of <see cref="InventoryManager.CreateMaterialAsync(ModelPbrMaterial, Permissions, string, string, UUID?, UUID?, IDictionary{ModelTexture, UUID}, Func{int, bool}, System.Threading.CancellationToken, IProgress{HttpCapsClient.ProgressReport})"/></summary>
    public class MaterialUploadResult : InventoryManager.CreateItemFromAssetResult
    {
        /// <summary>
        /// The asset ID of each texture that has been uploaded for the material, including those uploaded before something failed
        /// (they stay in inventory, and have been paid for). Empty for a material that was given with its textures already uploaded.
        /// </summary>
        public IReadOnlyDictionary<ModelTexture, UUID> Textures { get; set; } = new Dictionary<ModelTexture, UUID>();

        /// <summary>Textures the material has that were not uploaded because they were never loaded</summary>
        public IReadOnlyList<ModelDiagnostic> Diagnostics { get; set; } = Array.Empty<ModelDiagnostic>();
    }
}
