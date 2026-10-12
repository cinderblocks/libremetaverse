/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025-2026, Sjofn LLC.
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

namespace LibreMetaverse
{
    /// <summary>
    /// Reason for submitting a request or report to the simulator
    /// </summary>
    public enum ReportType : uint
    {
        /// <summary>No report</summary>
        None = 0,
        /// <summary>Unknown report type</summary>
        Unknown = 1,
        /// <summary>Bug report</summary>
        Bug = 2,
        /// <summary>Complaint report</summary>
        Complaint = 3,
        /// <summary>Customer service report</summary>
        CustomerServiceRequest = 4
    }

    /// <summary>
    /// Specific Flags for MultipleObjectUpdate requests
    /// </summary>
    [Flags]
    public enum UpdateType : uint
    {
        /// <summary>None</summary>
        None = 0x00,
        /// <summary>Change position of prims</summary>
        Position = 0x01,
        /// <summary>Change rotation of prims</summary>
        Rotation = 0x02,
        /// <summary>Change size of prims</summary>
        Scale = 0x04,
        /// <summary>Perform operation on link set</summary>
        Linked = 0x08,
        /// <summary>Scale prims uniformly, same as selecting ctrl+shift in the
        /// viewer. Used in conjunction with Scale</summary>
        Uniform = 0x10
    }

    /// <summary>
    /// Special values in PayPriceReply. If the price is not one of these
    /// literal value of the price should be use
    /// </summary>
    public enum PayPriceType : int
    {
        /// <summary>
        /// Indicates that this pay option should be hidden
        /// </summary>
        Hide = -1,

        /// <summary>
        /// Indicates that this pay option should have the default value
        /// </summary>
        Default = -2
    }
}
