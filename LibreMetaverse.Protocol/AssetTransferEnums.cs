/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn LLC.
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

namespace LibreMetaverse
{
    public enum EstateAssetType : int
    {
        None = -1,
        Covenant = 0
    }

    /// <summary>
    /// 
    /// </summary>
    public enum StatusCode
    {
        /// <summary>OK</summary>
        OK = 0,
        /// <summary>Transfer completed</summary>
        Done = 1,
        /// <summary></summary>
        Skip = 2,
        /// <summary></summary>
        Abort = 3,
        /// <summary>Unknown error occurred</summary>
        Error = -1,
        /// <summary>Equivalent to a 404 error</summary>
        UnknownSource = -2,
        /// <summary>Client does not have permission for that resource</summary>
        InsufficientPermissions = -3,
        /// <summary>Unknown status</summary>
        Unknown = -4
    }

    public enum TransferError : int
    {
        None = 0,
        Failed = -1,
        AssetNotFound = -3,
        AssetNotFoundInDatabase = -4,
        InsufficientPermissions = -5,
        EOF = -39,
        CannotOpenFile = -42,
        FileNotFound = -43,
        FileIsEmpty = -44,
        TCPTimeout = -23016,
        CircuitGone = -23017
    }
}
