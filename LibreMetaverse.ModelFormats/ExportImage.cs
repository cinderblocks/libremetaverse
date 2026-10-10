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

namespace LibreMetaverse.ImportExport
{
    /// <summary>An encoded image (PNG or JPEG) to embed in, or write beside, an exported model</summary>
    public sealed class ExportImage
    {
        /// <summary>The compressed image file</summary>
        public byte[] Data { get; }

        /// <summary><c>image/png</c> or <c>image/jpeg</c>, the only formats every exported model format allows</summary>
        public string MimeType { get; }

        public ExportImage(byte[] data, string mimeType)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
        }
    }

    /// <summary>A file an exporter wants written: the model itself, or something it refers to</summary>
    public sealed class ExportedFile
    {
        /// <summary>The file's name, without any directory</summary>
        public string Name { get; }

        /// <summary>The file's contents</summary>
        public byte[] Data { get; }

        public ExportedFile(string name, byte[] data)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }
}
