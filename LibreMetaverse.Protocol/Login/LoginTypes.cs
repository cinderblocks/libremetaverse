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

namespace LibreMetaverse
{
    /// <summary>
    /// Status of the last application run.
    /// Used for error reporting to the grid login service for statistical purposes.
    /// </summary>
    public enum LastExecStatus
    {
        /// <summary> Application exited normally </summary>
        Normal = 0,
        /// <summary> Application froze </summary>
        Froze,
        /// <summary> Application detected error and exited abnormally </summary>
        ForcedCrash,
        /// <summary> Other crash </summary>
        OtherCrash,
        /// <summary> Application froze during logout </summary>
        LogoutFroze,
        /// <summary> Application crashed during logout </summary>
        LogoutCrash
    }

    public struct BuddyListEntry
    {
        public int BuddyRightsGiven;
        public string BuddyId;
        public int BuddyRightsHas;
    }

    public class HomeInfo
    {
        public ulong RegionHandle;
        public Vector3 Position;
        public Vector3 LookAt;
    }

    public class LoginException : Exception
    {
        public LoginException(string message) 
            : base(message)
        {}

        public LoginException(string message, Exception innerException)
            : base (message, innerException)
        {}
    }
}
