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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace LibreMetaverse.Appearance
{
    /// <summary>
    /// Composite policy that allows multiple ICurrentOutfitPolicy implementations to be combined.
    /// All policies must agree for an action to be allowed.
    /// </summary>
    public class CompositeCurrentOutfitPolicy : ICurrentOutfitPolicy
    {
        private readonly object policiesLock = new object();
        private ImmutableHashSet<ICurrentOutfitPolicy> policies = ImmutableHashSet<ICurrentOutfitPolicy>.Empty;

        private ImmutableHashSet<ICurrentOutfitPolicy> GetCurrentPolicies()
        {
            lock (policiesLock)
            {
                return policies;
            }
        }

        /// <summary>
        /// Add a policy to the composite
        /// </summary>
        /// <param name="policyToAdd">Policy to add</param>
        /// <returns>This instance for method chaining</returns>
        public CompositeCurrentOutfitPolicy AddPolicy(ICurrentOutfitPolicy policyToAdd)
        {
            if (policyToAdd == null)
            {
                throw new ArgumentNullException(nameof(policyToAdd));
            }

            lock (policiesLock)
            {
                policies = policies.Add(policyToAdd);
            }

            return this;
        }

        /// <summary>
        /// Remove a policy from the composite
        /// </summary>
        /// <param name="policyToRemove">Policy to remove</param>
        public void RemovePolicy(ICurrentOutfitPolicy policyToRemove)
        {
            lock (policiesLock)
            {
                policies = policies.Remove(policyToRemove);
            }
        }

        /// <summary>
        /// Determines if the specified item can be attached/worn.
        /// All policies must return true for this to return true.
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if all policies allow attachment</returns>
        public bool CanAttach(InventoryItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return GetCurrentPolicies()
                .All(n => n.CanAttach(item));
        }

        /// <summary>
        /// Determines if the specified item can be detached/removed.
        /// All policies must return true for this to return true.
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if all policies allow detachment</returns>
        public bool CanDetach(InventoryItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return GetCurrentPolicies()
                .All(n => n.CanDetach(item));
        }

        /// <summary>
        /// Reports a change in outfit items to all policies
        /// </summary>
        /// <param name="addedItems">Items that were added to the outfit</param>
        /// <param name="removedItems">Items that were removed from the outfit</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ReportItemChange(List<InventoryItem> addedItems, List<InventoryItem> removedItems, CancellationToken cancellationToken = default)
        {
            var currentPolicies = GetCurrentPolicies();

            foreach (var policy in currentPolicies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await policy.ReportItemChange(addedItems, removedItems, cancellationToken);
            }
        }
    }
}
