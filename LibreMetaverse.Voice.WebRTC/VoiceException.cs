/*
 * Copyright (c) 2025, Sjofn LLC
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

namespace LibreMetaverse.Voice.WebRTC
{
    public class VoiceException : Exception
    {
        /// <summary>
        /// True when this represents a definitive, immediate rejection from the server (e.g. an
        /// HTTP status code the server returned deliberately) rather than a transient condition
        /// (timeout, connection error) that merely exhausted its retry budget. Callers one layer
        /// up (e.g. ConnectPrimaryRegionAsync's own retry-with-backoff) should not blindly retry
        /// a definitive rejection the same way they'd retry a transient one — doing so just
        /// repeats an outcome that's already known to be deterministic, and against a real server
        /// can look like abuse/rate-limit-worthy behavior for no benefit.
        /// </summary>
        public bool IsDefinitiveRejection { get; }

        public VoiceException(string msg) : base(msg) { }

        public VoiceException(string msg, bool isDefinitiveRejection) : base(msg)
        {
            IsDefinitiveRejection = isDefinitiveRejection;
        }
    }
}
