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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Assets.Gltf
{
    /// <summary>
    /// A parsed glTF 2.0 document. Supports loading from both JSON (.gltf) and binary GLB (.glb)
    /// formats as well as serializing back to either format.
    /// Geometry data is decoded into typed arrays for consumption by renderers or tools downstream;
    /// no GPU resources are allocated here.
    /// </summary>
    public sealed class GltfDocument
    {
        private const uint GLB_MAGIC       = 0x46546C67u; // "glTF" LE
        private const uint GLB_VERSION     = 2u;
        private const uint CHUNK_JSON      = 0x4E4F534Au; // "JSON" LE
        private const uint CHUNK_BIN       = 0x004E4942u; // "BIN\0" LE
        private const string KHR_TRANSFORM = "KHR_texture_transform";

        // ── Asset metadata ────────────────────────────────────────────────────
        public string Version   { get; set; } = "2.0";
        public string? Generator  { get; set; }
        public string? Copyright  { get; set; }
        public string? MinVersion { get; set; }

        // ── Document arrays (match glTF top-level keys) ───────────────────────
        public int DefaultScene { get; set; } = -1;

        public List<GltfScene>             Scenes      { get; } = new List<GltfScene>();
        public List<GltfNode>              Nodes       { get; } = new List<GltfNode>();
        public List<GltfMesh>              Meshes      { get; } = new List<GltfMesh>();
        public List<GltfSkin>              Skins       { get; } = new List<GltfSkin>();
        public List<GltfAnimation>         Animations  { get; } = new List<GltfAnimation>();
        public List<GltfDocumentMaterial>  Materials   { get; } = new List<GltfDocumentMaterial>();
        public List<GltfTexture>           Textures    { get; } = new List<GltfTexture>();
        public List<GltfSampler>           Samplers    { get; } = new List<GltfSampler>();
        public List<GltfImage>             Images      { get; } = new List<GltfImage>();
        public List<GltfAccessor>          Accessors   { get; } = new List<GltfAccessor>();
        public List<GltfBufferView>        BufferViews { get; } = new List<GltfBufferView>();
        public List<GltfBuffer>            Buffers     { get; } = new List<GltfBuffer>();

        public List<string> ExtensionsUsed     { get; } = new List<string>();
        public List<string> ExtensionsRequired { get; } = new List<string>();

        // ── Loading ───────────────────────────────────────────────────────────

        /// <summary>
        /// Load a glTF document from raw bytes. Auto-detects GLB (binary) versus JSON text.
        /// For JSON files that reference external buffer files, supply a <paramref name="bufferLoader"/>
        /// callback that resolves a relative URI to its raw bytes.
        /// </summary>
        public static GltfDocument Load(byte[] data, Func<string, byte[]>? bufferLoader = null)
        {
            if (data.Length >= 4 &&
                data[0] == 0x67 && data[1] == 0x6C &&
                data[2] == 0x54 && data[3] == 0x46)
            {
                return LoadGlb(data);
            }
            return LoadGltf(Encoding.UTF8.GetString(data), bufferLoader);
        }

        /// <summary>Load a GLB (binary glTF) file from raw bytes.</summary>
        public static GltfDocument LoadGlb(byte[] data)
        {
            using var ms = new MemoryStream(data, writable: false);
            using var br = new BinaryReader(ms);

            uint magic   = br.ReadUInt32();
            uint version = br.ReadUInt32();
            uint length  = br.ReadUInt32();

            if (magic != GLB_MAGIC)
                throw new InvalidDataException("Not a valid GLB file (bad magic).");
            if (version != GLB_VERSION)
                throw new InvalidDataException($"Unsupported GLB version {version}. Only version 2 is supported.");

            string? jsonText = null;
            byte[]? binChunk = null;

            while (ms.Position < length)
            {
                uint chunkLen  = br.ReadUInt32();
                uint chunkType = br.ReadUInt32();
                byte[] chunkData = br.ReadBytes((int)chunkLen);

                if (chunkType == CHUNK_JSON && jsonText == null)
                    jsonText = Encoding.UTF8.GetString(chunkData);
                else if (chunkType == CHUNK_BIN && binChunk == null)
                    binChunk = chunkData;
            }

            if (jsonText == null)
                throw new InvalidDataException("GLB file contains no JSON chunk.");

            var doc = ParseJson(jsonText);

            if (binChunk != null && doc.Buffers.Count > 0)
                doc.Buffers[0].Data = binChunk;

            doc.ResolveBuffers(null);
            return doc;
        }

        /// <summary>
        /// Load a JSON glTF document from a string.
        /// Supply <paramref name="bufferLoader"/> to resolve external buffer URIs.
        /// </summary>
        public static GltfDocument LoadGltf(string json, Func<string, byte[]>? bufferLoader = null)
        {
            var doc = ParseJson(json);
            doc.ResolveBuffers(bufferLoader);
            return doc;
        }

        // ── Serialization ─────────────────────────────────────────────────────

        /// <summary>Serialize this document to a glTF JSON string.</summary>
        public string ToJson(bool pretty = true)
        {
            var root = BuildOsdTree(embedBuffer: false);
            return OSDParser.SerializeJsonString(root, pretty);
        }

        /// <summary>
        /// Serialize this document to a GLB byte array.
        /// The first buffer's <see cref="GltfBuffer.Data"/> (if present) is embedded as the BIN chunk.
        /// </summary>
        public byte[] ToGlb()
        {
            var root = BuildOsdTree(embedBuffer: true);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(root, true));

            byte[]? binData = Buffers.Count > 0 ? Buffers[0].Data : null;

            int jsonPadded = Align4(jsonBytes.Length);
            int binPadded  = binData != null ? Align4(binData.Length) : 0;

            int totalLength = 12                                       // GLB header
                            + 8 + jsonPadded                           // JSON chunk header + data
                            + (binData != null ? 8 + binPadded : 0);  // BIN chunk (optional)

            using var ms = new MemoryStream(totalLength);
            using var bw = new BinaryWriter(ms);

            // GLB header
            bw.Write(GLB_MAGIC);
            bw.Write(GLB_VERSION);
            bw.Write((uint)totalLength);

            // JSON chunk
            bw.Write((uint)jsonPadded);
            bw.Write(CHUNK_JSON);
            bw.Write(jsonBytes);
            for (int i = jsonBytes.Length; i < jsonPadded; i++) bw.Write((byte)0x20); // space padding

            // BIN chunk
            if (binData != null)
            {
                bw.Write((uint)binPadded);
                bw.Write(CHUNK_BIN);
                bw.Write(binData);
                for (int i = binData.Length; i < binPadded; i++) bw.Write((byte)0x00);
            }

            return ms.ToArray();
        }

        // ── Geometry helpers ──────────────────────────────────────────────────

        /// <summary>Decode POSITION (VEC3 FLOAT) for a primitive.</summary>
        public Vector3[] GetPositions(GltfPrimitive primitive)
        {
            return primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_POSITION, out int idx)
                ? DecodeVec3Float(idx)
                : Array.Empty<Vector3>();
        }

        /// <summary>Decode NORMAL (VEC3 FLOAT) for a primitive.</summary>
        public Vector3[] GetNormals(GltfPrimitive primitive)
        {
            return primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_NORMAL, out int idx)
                ? DecodeVec3Float(idx)
                : Array.Empty<Vector3>();
        }

        /// <summary>Decode TANGENT (VEC4 FLOAT, W = bitangent sign) for a primitive.</summary>
        public Vector4[] GetTangents(GltfPrimitive primitive)
        {
            return primitive.Attributes.TryGetValue(GltfPrimitive.ATTR_TANGENT, out int idx)
                ? DecodeVec4Float(idx)
                : Array.Empty<Vector4>();
        }

        /// <summary>Decode TEXCOORD_<paramref name="channel"/> (VEC2, any supported component type) for a primitive.</summary>
        public Vector2[] GetTexCoords(GltfPrimitive primitive, int channel = 0)
        {
            var key = channel == 0 ? GltfPrimitive.ATTR_TEXCOORD_0
                    : channel == 1 ? GltfPrimitive.ATTR_TEXCOORD_1
                    : $"TEXCOORD_{channel}";
            return primitive.Attributes.TryGetValue(key, out int idx)
                ? DecodeVec2(idx)
                : Array.Empty<Vector2>();
        }

        /// <summary>Decode COLOR_<paramref name="set"/> as RGBA floats. VEC3 inputs have alpha set to 1.0.</summary>
        public Color4[] GetColors(GltfPrimitive primitive, int set = 0)
        {
            var key = set == 0 ? GltfPrimitive.ATTR_COLOR_0 : $"COLOR_{set}";
            return primitive.Attributes.TryGetValue(key, out int idx)
                ? DecodeColor(idx)
                : Array.Empty<Color4>();
        }

        /// <summary>Decode the index buffer as uint32 values (handles UNSIGNED_BYTE/SHORT/INT).</summary>
        public uint[] GetIndices(GltfPrimitive primitive)
        {
            return primitive.Indices >= 0
                ? DecodeIndices(primitive.Indices)
                : Array.Empty<uint>();
        }

        /// <summary>Decode JOINTS_<paramref name="set"/> as tuples of four joint indices.</summary>
        public (ushort j0, ushort j1, ushort j2, ushort j3)[] GetJoints(GltfPrimitive primitive, int set = 0)
        {
            var key = set == 0 ? GltfPrimitive.ATTR_JOINTS_0 : $"JOINTS_{set}";
            return primitive.Attributes.TryGetValue(key, out int idx)
                ? DecodeJoints(idx)
                : Array.Empty<(ushort, ushort, ushort, ushort)>();
        }

        /// <summary>Decode WEIGHTS_<paramref name="set"/> as VEC4 floats (handles normalized component types).</summary>
        public Vector4[] GetWeights(GltfPrimitive primitive, int set = 0)
        {
            var key = set == 0 ? GltfPrimitive.ATTR_WEIGHTS_0 : $"WEIGHTS_{set}";
            return primitive.Attributes.TryGetValue(key, out int idx)
                ? DecodeVec4Normalized(idx)
                : Array.Empty<Vector4>();
        }

        /// <summary>Decode the inverse bind matrices for a skin (MAT4 FLOAT).</summary>
        public Matrix4[] GetInverseBindMatrices(GltfSkin skin)
        {
            return skin.InverseBindMatrices >= 0
                ? DecodeMat4Float(skin.InverseBindMatrices)
                : Array.Empty<Matrix4>();
        }

        /// <summary>Decode the keyframe timestamps for an animation sampler (SCALAR FLOAT).</summary>
        public float[] GetSamplerTimestamps(GltfAnimationSampler sampler)
        {
            return sampler.Input >= 0
                ? DecodeScalarFloat(sampler.Input)
                : Array.Empty<float>();
        }

        /// <summary>Decode translation or scale keyframe values (VEC3 FLOAT) from an animation sampler output.</summary>
        public Vector3[] GetSamplerVec3(GltfAnimationSampler sampler)
        {
            return sampler.Output >= 0
                ? DecodeVec3Float(sampler.Output)
                : Array.Empty<Vector3>();
        }

        /// <summary>Decode rotation keyframe values (VEC4 FLOAT as XYZW quaternion) from an animation sampler output.</summary>
        public Quaternion[] GetSamplerQuaternion(GltfAnimationSampler sampler)
        {
            return sampler.Output >= 0
                ? DecodeQuaternionFloat(sampler.Output)
                : Array.Empty<Quaternion>();
        }

        /// <summary>Decode morph-weight keyframe values (SCALAR FLOAT, N values per frame) from a sampler output.</summary>
        public float[] GetSamplerWeights(GltfAnimationSampler sampler)
        {
            return sampler.Output >= 0
                ? DecodeScalarFloat(sampler.Output)
                : Array.Empty<float>();
        }

        // ── Private: buffer resolution ────────────────────────────────────────

        private void ResolveBuffers(Func<string, byte[]>? loader)
        {
            foreach (var buf in Buffers)
            {
                if (buf.Data != null || buf.Uri == null) continue;

                if (buf.Uri.StartsWith("data:", StringComparison.Ordinal))
                {
                    int comma = buf.Uri.IndexOf(',');
                    if (comma >= 0)
                        buf.Data = Convert.FromBase64String(buf.Uri.Substring(comma + 1));
                }
                else
                {
                    buf.Data = loader?.Invoke(buf.Uri);
                }
            }
        }

        // ── Private: accessor data decoding ───────────────────────────────────

        private bool TryGetAccessorBytes(int accessorIndex,
            out GltfAccessor acc, out byte[] data, out int baseOffset, out int stride)
        {
            acc = null!;
            data = Array.Empty<byte>();
            baseOffset = 0;
            stride = 0;

            if (accessorIndex < 0 || accessorIndex >= Accessors.Count) return false;
            acc = Accessors[accessorIndex];
            if (acc.BufferView < 0 || acc.BufferView >= BufferViews.Count) return false;

            var view = BufferViews[acc.BufferView];
            if (view.Buffer < 0 || view.Buffer >= Buffers.Count) return false;

            var buf = Buffers[view.Buffer];
            if (buf.Data == null) return false;

            data       = buf.Data;
            baseOffset = view.ByteOffset + acc.ByteOffset;
            stride     = view.ByteStride > 0 ? view.ByteStride : acc.DefaultStride;
            return true;
        }

        private float[] DecodeScalarFloat(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<float>();

            var result = new float[acc.Count];
            for (int i = 0; i < acc.Count; i++)
                result[i] = BitConverter.ToSingle(data, base0 + i * stride);
            return result;
        }

        private Vector2[] DecodeVec2(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Vector2>();

            var result = new Vector2[acc.Count];
            switch (acc.ComponentType)
            {
                case GltfComponentType.Float:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector2(BitConverter.ToSingle(data, o),
                                                BitConverter.ToSingle(data, o + 4));
                    }
                    break;
                case GltfComponentType.UnsignedByte:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector2(data[o] / 255f, data[o + 1] / 255f);
                    }
                    break;
                case GltfComponentType.UnsignedShort:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector2(BitConverter.ToUInt16(data, o) / 65535f,
                                                BitConverter.ToUInt16(data, o + 2) / 65535f);
                    }
                    break;
            }
            return result;
        }

        private Vector3[] DecodeVec3Float(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Vector3>();

            var result = new Vector3[acc.Count];
            for (int i = 0; i < acc.Count; i++)
            {
                int o = base0 + i * stride;
                result[i] = new Vector3(BitConverter.ToSingle(data, o),
                                        BitConverter.ToSingle(data, o + 4),
                                        BitConverter.ToSingle(data, o + 8));
            }
            return result;
        }

        private Vector4[] DecodeVec4Float(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Vector4>();

            var result = new Vector4[acc.Count];
            for (int i = 0; i < acc.Count; i++)
            {
                int o = base0 + i * stride;
                result[i] = new Vector4(BitConverter.ToSingle(data, o),
                                        BitConverter.ToSingle(data, o + 4),
                                        BitConverter.ToSingle(data, o + 8),
                                        BitConverter.ToSingle(data, o + 12));
            }
            return result;
        }

        private Vector4[] DecodeVec4Normalized(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Vector4>();

            var result = new Vector4[acc.Count];
            switch (acc.ComponentType)
            {
                case GltfComponentType.Float:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector4(BitConverter.ToSingle(data, o),
                                                BitConverter.ToSingle(data, o + 4),
                                                BitConverter.ToSingle(data, o + 8),
                                                BitConverter.ToSingle(data, o + 12));
                    }
                    break;
                case GltfComponentType.UnsignedByte:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector4(data[o] / 255f, data[o + 1] / 255f,
                                                data[o + 2] / 255f, data[o + 3] / 255f);
                    }
                    break;
                case GltfComponentType.UnsignedShort:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = new Vector4(BitConverter.ToUInt16(data, o)     / 65535f,
                                                BitConverter.ToUInt16(data, o + 2) / 65535f,
                                                BitConverter.ToUInt16(data, o + 4) / 65535f,
                                                BitConverter.ToUInt16(data, o + 6) / 65535f);
                    }
                    break;
            }
            return result;
        }

        private Quaternion[] DecodeQuaternionFloat(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Quaternion>();

            var result = new Quaternion[acc.Count];
            for (int i = 0; i < acc.Count; i++)
            {
                int o = base0 + i * stride;
                result[i] = new Quaternion(BitConverter.ToSingle(data, o),
                                           BitConverter.ToSingle(data, o + 4),
                                           BitConverter.ToSingle(data, o + 8),
                                           BitConverter.ToSingle(data, o + 12));
            }
            return result;
        }

        private Color4[] DecodeColor(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Color4>();

            var result = new Color4[acc.Count];
            bool isVec3 = acc.Type == GltfAccessorType.Vec3;

            switch (acc.ComponentType)
            {
                case GltfComponentType.Float:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = isVec3
                            ? new Color4(BitConverter.ToSingle(data, o),
                                         BitConverter.ToSingle(data, o + 4),
                                         BitConverter.ToSingle(data, o + 8), 1f)
                            : new Color4(BitConverter.ToSingle(data, o),
                                         BitConverter.ToSingle(data, o + 4),
                                         BitConverter.ToSingle(data, o + 8),
                                         BitConverter.ToSingle(data, o + 12));
                    }
                    break;
                case GltfComponentType.UnsignedByte:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = isVec3
                            ? new Color4(data[o] / 255f, data[o + 1] / 255f, data[o + 2] / 255f, 1f)
                            : new Color4(data[o] / 255f, data[o + 1] / 255f, data[o + 2] / 255f, data[o + 3] / 255f);
                    }
                    break;
                case GltfComponentType.UnsignedShort:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = isVec3
                            ? new Color4(BitConverter.ToUInt16(data, o)     / 65535f,
                                         BitConverter.ToUInt16(data, o + 2) / 65535f,
                                         BitConverter.ToUInt16(data, o + 4) / 65535f, 1f)
                            : new Color4(BitConverter.ToUInt16(data, o)     / 65535f,
                                         BitConverter.ToUInt16(data, o + 2) / 65535f,
                                         BitConverter.ToUInt16(data, o + 4) / 65535f,
                                         BitConverter.ToUInt16(data, o + 6) / 65535f);
                    }
                    break;
            }
            return result;
        }

        private uint[] DecodeIndices(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<uint>();

            var result = new uint[acc.Count];
            switch (acc.ComponentType)
            {
                case GltfComponentType.UnsignedByte:
                    for (int i = 0; i < acc.Count; i++)
                        result[i] = data[base0 + i * stride];
                    break;
                case GltfComponentType.UnsignedShort:
                    for (int i = 0; i < acc.Count; i++)
                        result[i] = BitConverter.ToUInt16(data, base0 + i * stride);
                    break;
                case GltfComponentType.UnsignedInt:
                    for (int i = 0; i < acc.Count; i++)
                        result[i] = BitConverter.ToUInt32(data, base0 + i * stride);
                    break;
            }
            return result;
        }

        private (ushort, ushort, ushort, ushort)[] DecodeJoints(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<(ushort, ushort, ushort, ushort)>();

            var result = new (ushort, ushort, ushort, ushort)[acc.Count];
            switch (acc.ComponentType)
            {
                case GltfComponentType.UnsignedByte:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = (data[o], data[o + 1], data[o + 2], data[o + 3]);
                    }
                    break;
                case GltfComponentType.UnsignedShort:
                    for (int i = 0; i < acc.Count; i++)
                    {
                        int o = base0 + i * stride;
                        result[i] = (BitConverter.ToUInt16(data, o),
                                     BitConverter.ToUInt16(data, o + 2),
                                     BitConverter.ToUInt16(data, o + 4),
                                     BitConverter.ToUInt16(data, o + 6));
                    }
                    break;
            }
            return result;
        }

        private Matrix4[] DecodeMat4Float(int accIdx)
        {
            if (!TryGetAccessorBytes(accIdx, out var acc, out var data, out int base0, out int stride))
                return Array.Empty<Matrix4>();

            var result = new Matrix4[acc.Count];
            for (int i = 0; i < acc.Count; i++)
            {
                int o = base0 + i * stride;
                // glTF matrices are column-major; Matrix4 constructor is row-major (M11..M44)
                result[i] = new Matrix4(
                    BitConverter.ToSingle(data, o),      BitConverter.ToSingle(data, o + 16),
                    BitConverter.ToSingle(data, o + 32), BitConverter.ToSingle(data, o + 48),
                    BitConverter.ToSingle(data, o + 4),  BitConverter.ToSingle(data, o + 20),
                    BitConverter.ToSingle(data, o + 36), BitConverter.ToSingle(data, o + 52),
                    BitConverter.ToSingle(data, o + 8),  BitConverter.ToSingle(data, o + 24),
                    BitConverter.ToSingle(data, o + 40), BitConverter.ToSingle(data, o + 56),
                    BitConverter.ToSingle(data, o + 12), BitConverter.ToSingle(data, o + 28),
                    BitConverter.ToSingle(data, o + 44), BitConverter.ToSingle(data, o + 60));
            }
            return result;
        }

        // ── Private: JSON parsing ─────────────────────────────────────────────

        private static GltfDocument ParseJson(string json)
        {
            var doc = new GltfDocument();
            if (OSDParser.DeserializeJson(json) is not OSDMap root) return doc;

            if (root["asset"] is OSDMap assetMap)
            {
                doc.Version    = assetMap["version"].AsString();
                doc.Generator  = OrNull(assetMap, "generator");
                doc.Copyright  = OrNull(assetMap, "copyright");
                doc.MinVersion = OrNull(assetMap, "minVersion");
            }

            if (root.ContainsKey("scene")) doc.DefaultScene = root["scene"].AsInteger();

            if (root["extensionsUsed"] is OSDArray extUsed)
                foreach (var e in extUsed) doc.ExtensionsUsed.Add(e.AsString());
            if (root["extensionsRequired"] is OSDArray extReq)
                foreach (var e in extReq) doc.ExtensionsRequired.Add(e.AsString());

            if (root["buffers"] is OSDArray bufArr)
                foreach (var b in bufArr) if (b is OSDMap bm) doc.Buffers.Add(ParseBuffer(bm));

            if (root["bufferViews"] is OSDArray bvArr)
                foreach (var bv in bvArr) if (bv is OSDMap bvm) doc.BufferViews.Add(ParseBufferView(bvm));

            if (root["accessors"] is OSDArray acArr)
                foreach (var ac in acArr) if (ac is OSDMap acm) doc.Accessors.Add(ParseAccessor(acm));

            if (root["images"] is OSDArray imgArr)
                foreach (var img in imgArr) if (img is OSDMap imgm) doc.Images.Add(ParseImage(imgm));

            if (root["samplers"] is OSDArray sampArr)
                foreach (var s in sampArr) if (s is OSDMap sm) doc.Samplers.Add(ParseSampler(sm));

            if (root["textures"] is OSDArray texArr)
                foreach (var t in texArr) if (t is OSDMap tm) doc.Textures.Add(ParseTexture(tm));

            if (root["materials"] is OSDArray matArr)
                foreach (var m in matArr) if (m is OSDMap mm) doc.Materials.Add(ParseMaterial(mm));

            if (root["meshes"] is OSDArray meshArr)
                foreach (var m in meshArr) if (m is OSDMap mm) doc.Meshes.Add(ParseMesh(mm));

            if (root["skins"] is OSDArray skinArr)
                foreach (var sk in skinArr) if (sk is OSDMap skm) doc.Skins.Add(ParseSkin(skm));

            if (root["nodes"] is OSDArray nodeArr)
                foreach (var n in nodeArr) if (n is OSDMap nm) doc.Nodes.Add(ParseNode(nm));

            if (root["scenes"] is OSDArray sceneArr)
                foreach (var sc in sceneArr) if (sc is OSDMap scm) doc.Scenes.Add(ParseScene(scm));

            if (root["animations"] is OSDArray animArr)
                foreach (var a in animArr) if (a is OSDMap am) doc.Animations.Add(ParseAnimation(am));

            return doc;
        }

        private static GltfBuffer ParseBuffer(OSDMap m) => new GltfBuffer
        {
            Uri        = OrNull(m, "uri"),
            ByteLength = m["byteLength"].AsInteger(),
            Name       = OrNull(m, "name")
        };

        private static GltfBufferView ParseBufferView(OSDMap m) => new GltfBufferView
        {
            Buffer     = m["buffer"].AsInteger(),
            ByteOffset = m.ContainsKey("byteOffset") ? m["byteOffset"].AsInteger() : 0,
            ByteLength = m["byteLength"].AsInteger(),
            ByteStride = m.ContainsKey("byteStride") ? m["byteStride"].AsInteger() : 0,
            Target     = m.ContainsKey("target")     ? m["target"].AsInteger()     : -1,
            Name       = OrNull(m, "name")
        };

        private static GltfAccessor ParseAccessor(OSDMap m)
        {
            var acc = new GltfAccessor
            {
                BufferView    = m.ContainsKey("bufferView") ? m["bufferView"].AsInteger() : -1,
                ByteOffset    = m.ContainsKey("byteOffset") ? m["byteOffset"].AsInteger() : 0,
                ComponentType = (GltfComponentType)m["componentType"].AsInteger(),
                Normalized    = m.ContainsKey("normalized") && m["normalized"].AsBoolean(),
                Count         = m["count"].AsInteger(),
                Type          = ParseAccessorType(m["type"].AsString()),
                Name          = OrNull(m, "name")
            };
            if (m["max"] is OSDArray maxArr)
            {
                acc.Max = new double[maxArr.Count];
                for (int i = 0; i < maxArr.Count; i++) acc.Max[i] = maxArr[i].AsReal();
            }
            if (m["min"] is OSDArray minArr)
            {
                acc.Min = new double[minArr.Count];
                for (int i = 0; i < minArr.Count; i++) acc.Min[i] = minArr[i].AsReal();
            }
            return acc;
        }

        private static GltfImage ParseImage(OSDMap m) => new GltfImage
        {
            Name       = OrNull(m, "name"),
            Uri        = OrNull(m, "uri"),
            MimeType   = OrNull(m, "mimeType"),
            BufferView = m.ContainsKey("bufferView") ? m["bufferView"].AsInteger() : -1
        };

        private static GltfSampler ParseSampler(OSDMap m) => new GltfSampler
        {
            MagFilter = m.ContainsKey("magFilter") ? (GltfSamplerFilter)m["magFilter"].AsInteger() : GltfSamplerFilter.Linear,
            MinFilter = m.ContainsKey("minFilter") ? (GltfSamplerFilter)m["minFilter"].AsInteger() : GltfSamplerFilter.LinearMipmapLinear,
            WrapS     = m.ContainsKey("wrapS")     ? (GltfSamplerWrap)m["wrapS"].AsInteger()       : GltfSamplerWrap.Repeat,
            WrapT     = m.ContainsKey("wrapT")     ? (GltfSamplerWrap)m["wrapT"].AsInteger()       : GltfSamplerWrap.Repeat,
            Name      = OrNull(m, "name")
        };

        private static GltfTexture ParseTexture(OSDMap m) => new GltfTexture
        {
            Sampler = m.ContainsKey("sampler") ? m["sampler"].AsInteger() : -1,
            Source  = m.ContainsKey("source")  ? m["source"].AsInteger()  : -1,
            Name    = OrNull(m, "name")
        };

        private static GltfDocumentMaterial ParseMaterial(OSDMap m)
        {
            var mat = new GltfDocumentMaterial { Name = OrNull(m, "name") };

            if (m["pbrMetallicRoughness"] is OSDMap pbr)
            {
                if (pbr["baseColorFactor"] is OSDArray bcf) mat.BaseColorFactor = bcf.AsColor4();
                mat.BaseColorTexture         = ParseTexRef(pbr["baseColorTexture"] as OSDMap);
                mat.MetallicFactor           = pbr.ContainsKey("metallicFactor")  ? (float)pbr["metallicFactor"].AsReal()  : 1f;
                mat.RoughnessFactor          = pbr.ContainsKey("roughnessFactor") ? (float)pbr["roughnessFactor"].AsReal() : 1f;
                mat.MetallicRoughnessTexture = ParseTexRef(pbr["metallicRoughnessTexture"] as OSDMap);
            }

            if (m["normalTexture"] is OSDMap nt)
            {
                var nref = new GltfNormalTextureRef { Scale = nt.ContainsKey("scale") ? (float)nt["scale"].AsReal() : 1f };
                FillTexRef(nref, nt);
                mat.NormalTexture = nref;
            }
            if (m["occlusionTexture"] is OSDMap ot)
            {
                var oref = new GltfOcclusionTextureRef { Strength = ot.ContainsKey("strength") ? (float)ot["strength"].AsReal() : 1f };
                FillTexRef(oref, ot);
                mat.OcclusionTexture = oref;
            }
            mat.EmissiveTexture = ParseTexRef(m["emissiveTexture"] as OSDMap);

            if (m["emissiveFactor"] is OSDArray ef) mat.EmissiveFactor = ef.AsVector3();
            mat.AlphaMode  = StringToAlphaMode(m["alphaMode"].AsString());
            mat.AlphaCutoff = m.ContainsKey("alphaCutoff") ? (float)m["alphaCutoff"].AsReal() : 0.5f;
            mat.DoubleSided = m.ContainsKey("doubleSided") && m["doubleSided"].AsBoolean();

            return mat;
        }

        private static GltfTextureRef? ParseTexRef(OSDMap? m)
        {
            if (m == null) return null;
            var r = new GltfTextureRef();
            FillTexRef(r, m);
            return r;
        }

        private static void FillTexRef(GltfTextureRef r, OSDMap m)
        {
            r.Index    = m.ContainsKey("index")    ? m["index"].AsInteger()    : -1;
            r.TexCoord = m.ContainsKey("texCoord") ? m["texCoord"].AsInteger() : 0;
            if (m["extensions"] is OSDMap exts && exts[KHR_TRANSFORM] is OSDMap khr)
            {
                var t = GltfTextureTransform.Default;
                if (khr["offset"] is OSDArray off)   t.Offset   = off.AsVector2();
                if (khr["scale"]  is OSDArray sc)    t.Scale    = sc.AsVector2();
                if (khr.ContainsKey("rotation"))     t.Rotation = (float)khr["rotation"].AsReal();
                r.Transform = t;
            }
        }

        private static GltfMesh ParseMesh(OSDMap m)
        {
            var mesh = new GltfMesh { Name = OrNull(m, "name") };
            if (m["primitives"] is OSDArray prims)
                foreach (var p in prims) if (p is OSDMap pm) mesh.Primitives.Add(ParsePrimitive(pm));
            if (m["weights"] is OSDArray wArr)
            {
                mesh.Weights = new double[wArr.Count];
                for (int i = 0; i < wArr.Count; i++) mesh.Weights[i] = wArr[i].AsReal();
            }
            return mesh;
        }

        private static GltfPrimitive ParsePrimitive(OSDMap m)
        {
            var prim = new GltfPrimitive
            {
                Indices  = m.ContainsKey("indices")  ? m["indices"].AsInteger()                    : -1,
                Material = m.ContainsKey("material") ? m["material"].AsInteger()                   : -1,
                Mode     = m.ContainsKey("mode")     ? (GltfPrimitiveMode)m["mode"].AsInteger()    : GltfPrimitiveMode.Triangles
            };
            if (m["attributes"] is OSDMap attrs)
                foreach (var key in attrs.Keys) prim.Attributes[key] = attrs[key].AsInteger();
            return prim;
        }

        private static GltfSkin ParseSkin(OSDMap m)
        {
            var skin = new GltfSkin
            {
                Name                 = OrNull(m, "name"),
                InverseBindMatrices  = m.ContainsKey("inverseBindMatrices") ? m["inverseBindMatrices"].AsInteger() : -1,
                Skeleton             = m.ContainsKey("skeleton")            ? m["skeleton"].AsInteger()            : -1
            };
            if (m["joints"] is OSDArray jArr)
                foreach (var j in jArr) skin.Joints.Add(j.AsInteger());
            return skin;
        }

        private static GltfNode ParseNode(OSDMap m)
        {
            var node = new GltfNode
            {
                Name = OrNull(m, "name"),
                Mesh = m.ContainsKey("mesh") ? m["mesh"].AsInteger() : -1,
                Skin = m.ContainsKey("skin") ? m["skin"].AsInteger() : -1
            };
            if (m["children"] is OSDArray ch)
                foreach (var c in ch) node.Children.Add(c.AsInteger());
            if (m["matrix"] is OSDArray mat && mat.Count == 16)
            {
                // column-major → row-major Matrix4
                node.Matrix = new Matrix4(
                    (float)mat[0].AsReal(),  (float)mat[4].AsReal(),  (float)mat[8].AsReal(),  (float)mat[12].AsReal(),
                    (float)mat[1].AsReal(),  (float)mat[5].AsReal(),  (float)mat[9].AsReal(),  (float)mat[13].AsReal(),
                    (float)mat[2].AsReal(),  (float)mat[6].AsReal(),  (float)mat[10].AsReal(), (float)mat[14].AsReal(),
                    (float)mat[3].AsReal(),  (float)mat[7].AsReal(),  (float)mat[11].AsReal(), (float)mat[15].AsReal());
            }
            else
            {
                if (m["translation"] is OSDArray tr) node.Translation = tr.AsVector3();
                if (m["rotation"]    is OSDArray ro) node.Rotation    = ro.AsQuaternion();
                if (m["scale"]       is OSDArray sc) node.Scale       = sc.AsVector3();
            }
            if (m["weights"] is OSDArray wArr)
            {
                node.Weights = new double[wArr.Count];
                for (int i = 0; i < wArr.Count; i++) node.Weights[i] = wArr[i].AsReal();
            }
            return node;
        }

        private static GltfScene ParseScene(OSDMap m)
        {
            var scene = new GltfScene { Name = OrNull(m, "name") };
            if (m["nodes"] is OSDArray nArr)
                foreach (var n in nArr) scene.Nodes.Add(n.AsInteger());
            return scene;
        }

        private static GltfAnimation ParseAnimation(OSDMap m)
        {
            var anim = new GltfAnimation { Name = OrNull(m, "name") };
            if (m["samplers"] is OSDArray sampArr)
                foreach (var s in sampArr) if (s is OSDMap sm)
                    anim.Samplers.Add(new GltfAnimationSampler
                    {
                        Input         = sm["input"].AsInteger(),
                        Output        = sm["output"].AsInteger(),
                        Interpolation = ParseInterpolation(sm["interpolation"].AsString())
                    });
            if (m["channels"] is OSDArray chArr)
                foreach (var c in chArr) if (c is OSDMap cm)
                {
                    var ch = new GltfAnimationChannel { Sampler = cm["sampler"].AsInteger() };
                    if (cm["target"] is OSDMap tgt)
                    {
                        ch.Target.Node = tgt.ContainsKey("node") ? tgt["node"].AsInteger() : -1;
                        ch.Target.Path = ParseTargetPath(tgt["path"].AsString());
                    }
                    anim.Channels.Add(ch);
                }
            return anim;
        }

        // ── Private: JSON serialization ───────────────────────────────────────

        private OSDMap BuildOsdTree(bool embedBuffer)
        {
            var root = new OSDMap();

            var assetMeta = new OSDMap();
            assetMeta["version"] = OSD.FromString(Version);
            if (!string.IsNullOrEmpty(Generator))  assetMeta["generator"]  = OSD.FromString(Generator!);
            if (!string.IsNullOrEmpty(Copyright))  assetMeta["copyright"]  = OSD.FromString(Copyright!);
            if (!string.IsNullOrEmpty(MinVersion)) assetMeta["minVersion"] = OSD.FromString(MinVersion!);
            root["asset"] = assetMeta;

            if (DefaultScene >= 0) root["scene"] = OSD.FromInteger(DefaultScene);

            if (ExtensionsUsed.Count > 0)
            {
                var arr = new OSDArray(ExtensionsUsed.Count);
                foreach (var e in ExtensionsUsed) arr.Add(OSD.FromString(e));
                root["extensionsUsed"] = arr;
            }
            if (ExtensionsRequired.Count > 0)
            {
                var arr = new OSDArray(ExtensionsRequired.Count);
                foreach (var e in ExtensionsRequired) arr.Add(OSD.FromString(e));
                root["extensionsRequired"] = arr;
            }

            SerializeList(root, "buffers",     Buffers,     b => SerializeBuffer(b, embedBuffer));
            SerializeList(root, "bufferViews", BufferViews, SerializeBufferView);
            SerializeList(root, "accessors",   Accessors,   SerializeAccessor);
            SerializeList(root, "images",      Images,      SerializeImage);
            SerializeList(root, "samplers",    Samplers,    SerializeSampler);
            SerializeList(root, "textures",    Textures,    SerializeTexture);
            SerializeList(root, "materials",   Materials,   SerializeMaterial);
            SerializeList(root, "meshes",      Meshes,      SerializeMesh);
            SerializeList(root, "skins",       Skins,       SerializeSkin);
            SerializeList(root, "nodes",       Nodes,       SerializeNode);
            SerializeList(root, "scenes",      Scenes,      SerializeScene);
            SerializeList(root, "animations",  Animations,  SerializeAnimation);

            return root;
        }

        private static void SerializeList<T>(OSDMap root, string key, List<T> list, Func<T, OSDMap> serializer)
        {
            if (list.Count == 0) return;
            var arr = new OSDArray(list.Count);
            foreach (var item in list) arr.Add(serializer(item));
            root[key] = arr;
        }

        private static OSDMap SerializeBuffer(GltfBuffer b, bool embed)
        {
            var m = new OSDMap();
            m["byteLength"] = OSD.FromInteger(b.ByteLength);
            if (!embed && !string.IsNullOrEmpty(b.Uri))
                m["uri"] = OSD.FromString(b.Uri!);
            else if (!embed && b.Data != null)
                m["uri"] = OSD.FromString("data:application/octet-stream;base64," + Convert.ToBase64String(b.Data));
            if (!string.IsNullOrEmpty(b.Name)) m["name"] = OSD.FromString(b.Name!);
            return m;
        }

        private static OSDMap SerializeBufferView(GltfBufferView bv)
        {
            var m = new OSDMap();
            m["buffer"]     = OSD.FromInteger(bv.Buffer);
            m["byteOffset"] = OSD.FromInteger(bv.ByteOffset);
            m["byteLength"] = OSD.FromInteger(bv.ByteLength);
            if (bv.ByteStride > 0) m["byteStride"] = OSD.FromInteger(bv.ByteStride);
            if (bv.Target >= 0)    m["target"]     = OSD.FromInteger(bv.Target);
            if (!string.IsNullOrEmpty(bv.Name)) m["name"] = OSD.FromString(bv.Name!);
            return m;
        }

        private static OSDMap SerializeAccessor(GltfAccessor ac)
        {
            var m = new OSDMap();
            if (ac.BufferView >= 0) m["bufferView"] = OSD.FromInteger(ac.BufferView);
            if (ac.ByteOffset != 0) m["byteOffset"] = OSD.FromInteger(ac.ByteOffset);
            m["componentType"] = OSD.FromInteger((int)ac.ComponentType);
            if (ac.Normalized)  m["normalized"]  = OSD.FromBoolean(true);
            m["count"] = OSD.FromInteger(ac.Count);
            m["type"]  = OSD.FromString(AccessorTypeToString(ac.Type));
            if (ac.Max != null) { var arr = new OSDArray(ac.Max.Length); foreach (var v in ac.Max) arr.Add(OSD.FromReal(v)); m["max"] = arr; }
            if (ac.Min != null) { var arr = new OSDArray(ac.Min.Length); foreach (var v in ac.Min) arr.Add(OSD.FromReal(v)); m["min"] = arr; }
            if (!string.IsNullOrEmpty(ac.Name)) m["name"] = OSD.FromString(ac.Name!);
            return m;
        }

        private static OSDMap SerializeImage(GltfImage img)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(img.Name))     m["name"]       = OSD.FromString(img.Name!);
            if (!string.IsNullOrEmpty(img.Uri))      m["uri"]        = OSD.FromString(img.Uri!);
            if (!string.IsNullOrEmpty(img.MimeType)) m["mimeType"]   = OSD.FromString(img.MimeType!);
            if (img.BufferView >= 0)                 m["bufferView"] = OSD.FromInteger(img.BufferView);
            return m;
        }

        private static OSDMap SerializeSampler(GltfSampler s)
        {
            var m = new OSDMap();
            if (s.MagFilter != GltfSamplerFilter.None) m["magFilter"] = OSD.FromInteger((int)s.MagFilter);
            if (s.MinFilter != GltfSamplerFilter.None) m["minFilter"] = OSD.FromInteger((int)s.MinFilter);
            m["wrapS"] = OSD.FromInteger((int)s.WrapS);
            m["wrapT"] = OSD.FromInteger((int)s.WrapT);
            if (!string.IsNullOrEmpty(s.Name)) m["name"] = OSD.FromString(s.Name!);
            return m;
        }

        private static OSDMap SerializeTexture(GltfTexture t)
        {
            var m = new OSDMap();
            if (t.Sampler >= 0) m["sampler"] = OSD.FromInteger(t.Sampler);
            if (t.Source  >= 0) m["source"]  = OSD.FromInteger(t.Source);
            if (!string.IsNullOrEmpty(t.Name)) m["name"] = OSD.FromString(t.Name!);
            return m;
        }

        private static OSDMap SerializeMaterial(GltfDocumentMaterial mat)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(mat.Name)) m["name"] = OSD.FromString(mat.Name!);

            var pbr = new OSDMap();
            pbr["baseColorFactor"]  = OSD.FromColor4(mat.BaseColorFactor);
            pbr["metallicFactor"]   = OSD.FromReal(mat.MetallicFactor);
            pbr["roughnessFactor"]  = OSD.FromReal(mat.RoughnessFactor);
            if (mat.BaseColorTexture        != null) pbr["baseColorTexture"]        = SerializeTexRef(mat.BaseColorTexture);
            if (mat.MetallicRoughnessTexture != null) pbr["metallicRoughnessTexture"] = SerializeTexRef(mat.MetallicRoughnessTexture);
            m["pbrMetallicRoughness"] = pbr;

            if (mat.NormalTexture != null)
            {
                var nt = SerializeTexRef(mat.NormalTexture);
                if (Math.Abs(mat.NormalTexture.Scale - 1f) > 1e-6f) nt["scale"] = OSD.FromReal(mat.NormalTexture.Scale);
                m["normalTexture"] = nt;
            }
            if (mat.OcclusionTexture != null)
            {
                var ot = SerializeTexRef(mat.OcclusionTexture);
                if (Math.Abs(mat.OcclusionTexture.Strength - 1f) > 1e-6f) ot["strength"] = OSD.FromReal(mat.OcclusionTexture.Strength);
                m["occlusionTexture"] = ot;
            }
            if (mat.EmissiveTexture != null) m["emissiveTexture"] = SerializeTexRef(mat.EmissiveTexture);

            m["emissiveFactor"] = OSD.FromVector3(mat.EmissiveFactor);
            m["alphaMode"]      = OSD.FromString(AlphaModeToString(mat.AlphaMode));
            m["alphaCutoff"]    = OSD.FromReal(mat.AlphaCutoff);
            m["doubleSided"]    = OSD.FromBoolean(mat.DoubleSided);
            return m;
        }

        private static OSDMap SerializeTexRef(GltfTextureRef r)
        {
            var m = new OSDMap();
            m["index"] = OSD.FromInteger(r.Index);
            if (r.TexCoord != 0) m["texCoord"] = OSD.FromInteger(r.TexCoord);
            if (!r.Transform.IsDefault)
            {
                var khr = new OSDMap();
                khr["offset"]   = OSD.FromVector2(r.Transform.Offset);
                khr["scale"]    = OSD.FromVector2(r.Transform.Scale);
                khr["rotation"] = OSD.FromReal(r.Transform.Rotation);
                var exts = new OSDMap();
                exts[KHR_TRANSFORM] = khr;
                m["extensions"] = exts;
            }
            return m;
        }

        private static OSDMap SerializeMesh(GltfMesh mesh)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(mesh.Name)) m["name"] = OSD.FromString(mesh.Name!);
            var prims = new OSDArray(mesh.Primitives.Count);
            foreach (var p in mesh.Primitives)
            {
                var pm = new OSDMap();
                var attrs = new OSDMap();
                foreach (var kvp in p.Attributes) attrs[kvp.Key] = OSD.FromInteger(kvp.Value);
                pm["attributes"] = attrs;
                if (p.Indices  >= 0)                   pm["indices"]  = OSD.FromInteger(p.Indices);
                if (p.Material >= 0)                   pm["material"] = OSD.FromInteger(p.Material);
                if (p.Mode != GltfPrimitiveMode.Triangles) pm["mode"] = OSD.FromInteger((int)p.Mode);
                prims.Add(pm);
            }
            m["primitives"] = prims;
            if (mesh.Weights != null)
            {
                var wArr = new OSDArray(mesh.Weights.Length);
                foreach (var w in mesh.Weights) wArr.Add(OSD.FromReal(w));
                m["weights"] = wArr;
            }
            return m;
        }

        private static OSDMap SerializeSkin(GltfSkin skin)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(skin.Name)) m["name"] = OSD.FromString(skin.Name!);
            if (skin.InverseBindMatrices >= 0) m["inverseBindMatrices"] = OSD.FromInteger(skin.InverseBindMatrices);
            if (skin.Skeleton           >= 0) m["skeleton"]            = OSD.FromInteger(skin.Skeleton);
            var jArr = new OSDArray(skin.Joints.Count);
            foreach (var j in skin.Joints) jArr.Add(OSD.FromInteger(j));
            m["joints"] = jArr;
            return m;
        }

        private static OSDMap SerializeNode(GltfNode node)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(node.Name)) m["name"] = OSD.FromString(node.Name!);
            if (node.Mesh >= 0) m["mesh"] = OSD.FromInteger(node.Mesh);
            if (node.Skin >= 0) m["skin"] = OSD.FromInteger(node.Skin);
            if (node.Children.Count > 0)
            {
                var ch = new OSDArray(node.Children.Count);
                foreach (var c in node.Children) ch.Add(OSD.FromInteger(c));
                m["children"] = ch;
            }
            if (node.Matrix.HasValue)
            {
                var mat = node.Matrix.Value;
                // serialize as column-major
                var arr = new OSDArray(16);
                arr.Add(OSD.FromReal(mat.M11)); arr.Add(OSD.FromReal(mat.M21)); arr.Add(OSD.FromReal(mat.M31)); arr.Add(OSD.FromReal(mat.M41));
                arr.Add(OSD.FromReal(mat.M12)); arr.Add(OSD.FromReal(mat.M22)); arr.Add(OSD.FromReal(mat.M32)); arr.Add(OSD.FromReal(mat.M42));
                arr.Add(OSD.FromReal(mat.M13)); arr.Add(OSD.FromReal(mat.M23)); arr.Add(OSD.FromReal(mat.M33)); arr.Add(OSD.FromReal(mat.M43));
                arr.Add(OSD.FromReal(mat.M14)); arr.Add(OSD.FromReal(mat.M24)); arr.Add(OSD.FromReal(mat.M34)); arr.Add(OSD.FromReal(mat.M44));
                m["matrix"] = arr;
            }
            else
            {
                if (node.Translation != Vector3.Zero)     m["translation"] = OSD.FromVector3(node.Translation);
                if (node.Rotation    != Quaternion.Identity) m["rotation"] = OSD.FromQuaternion(node.Rotation);
                if (node.Scale       != Vector3.One)      m["scale"]       = OSD.FromVector3(node.Scale);
            }
            if (node.Weights != null)
            {
                var wArr = new OSDArray(node.Weights.Length);
                foreach (var w in node.Weights) wArr.Add(OSD.FromReal(w));
                m["weights"] = wArr;
            }
            return m;
        }

        private static OSDMap SerializeScene(GltfScene scene)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(scene.Name)) m["name"] = OSD.FromString(scene.Name!);
            var nArr = new OSDArray(scene.Nodes.Count);
            foreach (var n in scene.Nodes) nArr.Add(OSD.FromInteger(n));
            m["nodes"] = nArr;
            return m;
        }

        private static OSDMap SerializeAnimation(GltfAnimation anim)
        {
            var m = new OSDMap();
            if (!string.IsNullOrEmpty(anim.Name)) m["name"] = OSD.FromString(anim.Name!);

            var sampArr = new OSDArray(anim.Samplers.Count);
            foreach (var s in anim.Samplers)
            {
                var sm = new OSDMap();
                sm["input"]         = OSD.FromInteger(s.Input);
                sm["output"]        = OSD.FromInteger(s.Output);
                sm["interpolation"] = OSD.FromString(InterpolationToString(s.Interpolation));
                sampArr.Add(sm);
            }
            m["samplers"] = sampArr;

            var chArr = new OSDArray(anim.Channels.Count);
            foreach (var ch in anim.Channels)
            {
                var cm = new OSDMap();
                cm["sampler"] = OSD.FromInteger(ch.Sampler);
                var tgt = new OSDMap();
                if (ch.Target.Node >= 0) tgt["node"] = OSD.FromInteger(ch.Target.Node);
                tgt["path"] = OSD.FromString(TargetPathToString(ch.Target.Path));
                cm["target"] = tgt;
                chArr.Add(cm);
            }
            m["channels"] = chArr;
            return m;
        }

        // ── Private: enum ↔ string helpers ────────────────────────────────────

        private static GltfAccessorType ParseAccessorType(string s) => s switch
        {
            "SCALAR" => GltfAccessorType.Scalar,
            "VEC2"   => GltfAccessorType.Vec2,
            "VEC3"   => GltfAccessorType.Vec3,
            "VEC4"   => GltfAccessorType.Vec4,
            "MAT2"   => GltfAccessorType.Mat2,
            "MAT3"   => GltfAccessorType.Mat3,
            "MAT4"   => GltfAccessorType.Mat4,
            _        => GltfAccessorType.Scalar
        };

        private static string AccessorTypeToString(GltfAccessorType t) => t switch
        {
            GltfAccessorType.Scalar => "SCALAR",
            GltfAccessorType.Vec2   => "VEC2",
            GltfAccessorType.Vec3   => "VEC3",
            GltfAccessorType.Vec4   => "VEC4",
            GltfAccessorType.Mat2   => "MAT2",
            GltfAccessorType.Mat3   => "MAT3",
            GltfAccessorType.Mat4   => "MAT4",
            _                       => "SCALAR"
        };

        private static GltfAlphaMode StringToAlphaMode(string s) => s switch
        {
            "BLEND" => GltfAlphaMode.Blend,
            "MASK"  => GltfAlphaMode.Mask,
            _       => GltfAlphaMode.Opaque
        };

        private static string AlphaModeToString(GltfAlphaMode m) => m switch
        {
            GltfAlphaMode.Blend => "BLEND",
            GltfAlphaMode.Mask  => "MASK",
            _                   => "OPAQUE"
        };

        private static GltfAnimationInterpolation ParseInterpolation(string s) => s switch
        {
            "STEP"        => GltfAnimationInterpolation.Step,
            "CUBICSPLINE" => GltfAnimationInterpolation.CubicSpline,
            _             => GltfAnimationInterpolation.Linear
        };

        private static string InterpolationToString(GltfAnimationInterpolation i) => i switch
        {
            GltfAnimationInterpolation.Step        => "STEP",
            GltfAnimationInterpolation.CubicSpline => "CUBICSPLINE",
            _                                      => "LINEAR"
        };

        private static GltfAnimationTargetPath ParseTargetPath(string s) => s switch
        {
            "rotation"    => GltfAnimationTargetPath.Rotation,
            "scale"       => GltfAnimationTargetPath.Scale,
            "weights"     => GltfAnimationTargetPath.Weights,
            _             => GltfAnimationTargetPath.Translation
        };

        private static string TargetPathToString(GltfAnimationTargetPath p) => p switch
        {
            GltfAnimationTargetPath.Rotation    => "rotation",
            GltfAnimationTargetPath.Scale       => "scale",
            GltfAnimationTargetPath.Weights     => "weights",
            _                                   => "translation"
        };

        private static string? OrNull(OSDMap m, string key) =>
            m.ContainsKey(key) ? m[key].AsString() : null;

        private static int Align4(int n) => (n + 3) & ~3;
    }
}
