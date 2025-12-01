/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Appearance
{
    /// <summary>
    /// Policy interface for controlling attachment and detachment permissions for Current Outfit Folder items.
    /// Implementers can enforce restrictions (e.g., RLV restrictions) on what can be worn or removed.
    /// </summary>
    public interface ICurrentOutfitPolicy
    {
        /// <summary>
        /// Determines if the specified item can be attached/worn
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if the item can be attached, false otherwise</returns>
        bool CanAttach(InventoryItem item);

        /// <summary>
        /// Determines if the specified item can be detached/removed
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if the item can be detached, false otherwise</returns>
        bool CanDetach(InventoryItem item);

        /// <summary>
        /// Reports a change in outfit items (items added or removed)
        /// </summary>
        /// <param name="addedItems">Items that were added to the outfit</param>
        /// <param name="removedItems">Items that were removed from the outfit</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task ReportItemChange(List<InventoryItem> addedItems, List<InventoryItem> removedItems, CancellationToken cancellationToken = default);
    }
}
