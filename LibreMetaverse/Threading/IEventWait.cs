/*
 * Copyright (c) 2024, Sjofn LLC
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

namespace LibreMetaverse.Threading
{
    /// <summary>
    /// Interface implemented by PooledEventWait. And, thanks to StructuralCaster, can be used to
    /// access custom EventWait objects.
    /// </summary>
    public interface IEventWait:
        IDisposable
    {
        /// <summary>
        /// Waits for this event to be signalled.
        /// </summary>
        void WaitOne();

        /// <summary>
        /// Waits for this event to be signalled or times-out.
        /// Returns if the object was signalled.
        /// </summary>
        bool WaitOne(int millisecondsTimeout);

        /// <summary>
        /// Waits for this event to be signalled or times-out.
        /// Returns if the object was signalled.
        /// </summary>
        bool WaitOne(TimeSpan timeout);

        /// <summary>
        /// Resets (unsignals) this wait event.
        /// </summary>
        void Reset();

        /// <summary>
        /// Sets (signals) this wait event.
        /// </summary>
        void Set();
    }
}
