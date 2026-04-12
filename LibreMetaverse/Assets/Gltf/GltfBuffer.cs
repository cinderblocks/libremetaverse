/*
 * Copyright (c) 2024-2026, Sjofn LLC
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

namespace OpenMetaverse.Assets.Gltf
{
    /// <summary>Raw binary data for a glTF buffer. Data may be embedded (loaded) or referenced via URI.</summary>
    public sealed class GltfBuffer
    {
        /// <summary>Decoded binary payload. Null until resolved from URI or GLB BIN chunk.</summary>
        public byte[]? Data { get; set; }
        /// <summary>Relative file path or data URI. Null for the GLB-embedded buffer.</summary>
        public string? Uri { get; set; }
        public int ByteLength { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>A slice of a <see cref="GltfBuffer"/> exposed to accessors.</summary>
    public sealed class GltfBufferView
    {
        /// <summary>Index into <see cref="GltfDocument.Buffers"/>.</summary>
        public int Buffer { get; set; } = -1;
        public int ByteOffset { get; set; }
        public int ByteLength { get; set; }
        /// <summary>Stride between elements in bytes. 0 means tightly packed.</summary>
        public int ByteStride { get; set; }
        /// <summary>Optional GPU target hint: 34962 = ARRAY_BUFFER, 34963 = ELEMENT_ARRAY_BUFFER.</summary>
        public int Target { get; set; } = -1;
        public string? Name { get; set; }
    }

    /// <summary>Typed view into a <see cref="GltfBufferView"/>.</summary>
    public sealed class GltfAccessor
    {
        /// <summary>Index into <see cref="GltfDocument.BufferViews"/>. -1 for a zero-filled sparse accessor.</summary>
        public int BufferView { get; set; } = -1;
        /// <summary>Additional byte offset relative to the buffer view start.</summary>
        public int ByteOffset { get; set; }
        public GltfComponentType ComponentType { get; set; }
        public bool Normalized { get; set; }
        public int Count { get; set; }
        public GltfAccessorType Type { get; set; }
        public double[]? Max { get; set; }
        public double[]? Min { get; set; }
        public string? Name { get; set; }

        /// <summary>Returns the number of scalar components per element (1 for SCALAR, 3 for VEC3, etc.).</summary>
        public int ComponentCount => Type switch
        {
            GltfAccessorType.Scalar => 1,
            GltfAccessorType.Vec2   => 2,
            GltfAccessorType.Vec3   => 3,
            GltfAccessorType.Vec4   => 4,
            GltfAccessorType.Mat2   => 4,
            GltfAccessorType.Mat3   => 9,
            GltfAccessorType.Mat4   => 16,
            _                       => 1
        };

        /// <summary>Returns the byte size of a single scalar component.</summary>
        public int ComponentByteSize => ComponentType switch
        {
            GltfComponentType.Byte          => 1,
            GltfComponentType.UnsignedByte  => 1,
            GltfComponentType.Short         => 2,
            GltfComponentType.UnsignedShort => 2,
            GltfComponentType.UnsignedInt   => 4,
            GltfComponentType.Float         => 4,
            _                               => 4
        };

        /// <summary>Default tightly-packed stride in bytes for this accessor's element type.</summary>
        public int DefaultStride => ComponentCount * ComponentByteSize;
    }
}
