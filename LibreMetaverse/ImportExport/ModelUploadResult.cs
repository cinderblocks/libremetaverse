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
    /// <summary>The outcome of <see cref="InventoryManager.UploadModelAsync"/></summary>
    public class ModelUploadResult : InventoryManager.CreateItemFromAssetResult
    {
        /// <summary>
        /// What was wrong with the model, or worth knowing about it. A model with an error is not sent:
        /// <see cref="InventoryManager.CreateItemFromAssetResult.Status"/> is <c>invalid_model</c>.
        /// </summary>
        public IReadOnlyList<ModelDiagnostic> Diagnostics { get; set; } = Array.Empty<ModelDiagnostic>();

        /// <summary>True if the model was refused because of an error in <see cref="Diagnostics"/></summary>
        public bool HasErrors
        {
            get
            {
                foreach (var d in Diagnostics)
                {
                    if (d.Severity == ModelDiagnosticSeverity.Error) return true;
                }
                return false;
            }
        }
    }
}
