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
    /// <summary>An image referenced by a <see cref="GltfTexture"/>.</summary>
    public sealed class GltfImage
    {
        public string? Name { get; set; }
        /// <summary>Relative URI or data URI. Null when data is embedded via a buffer view.</summary>
        public string? Uri { get; set; }
        public string? MimeType { get; set; }
        /// <summary>Index into <see cref="GltfDocument.BufferViews"/> for embedded image data. -1 if URI-based.</summary>
        public int BufferView { get; set; } = -1;
    }

    /// <summary>Texture sampler parameters.</summary>
    public sealed class GltfSampler
    {
        public GltfSamplerFilter MagFilter { get; set; } = GltfSamplerFilter.Linear;
        public GltfSamplerFilter MinFilter { get; set; } = GltfSamplerFilter.LinearMipmapLinear;
        public GltfSamplerWrap WrapS { get; set; } = GltfSamplerWrap.Repeat;
        public GltfSamplerWrap WrapT { get; set; } = GltfSamplerWrap.Repeat;
        public string? Name { get; set; }
    }

    /// <summary>Pairs an image with a sampler.</summary>
    public sealed class GltfTexture
    {
        /// <summary>Index into <see cref="GltfDocument.Samplers"/>. -1 means use default sampler.</summary>
        public int Sampler { get; set; } = -1;
        /// <summary>Index into <see cref="GltfDocument.Images"/>.</summary>
        public int Source { get; set; } = -1;
        public string? Name { get; set; }
    }

    /// <summary>A reference to a texture slot within a material, with optional UV transform.</summary>
    public class GltfTextureRef
    {
        /// <summary>Index into <see cref="GltfDocument.Textures"/>. -1 means unused.</summary>
        public int Index { get; set; } = -1;
        /// <summary>UV channel (TEXCOORD_n) to use. 0 or 1.</summary>
        public int TexCoord { get; set; }
        /// <summary>Optional KHR_texture_transform UV transform.</summary>
        public GltfTextureTransform Transform { get; set; } = GltfTextureTransform.Default;
    }

    /// <summary>Normal map texture reference with an additional scale factor.</summary>
    public sealed class GltfNormalTextureRef : GltfTextureRef
    {
        /// <summary>Scales the normal map XY components. Default 1.0.</summary>
        public float Scale { get; set; } = 1f;
    }

    /// <summary>Occlusion map texture reference with an additional strength factor.</summary>
    public sealed class GltfOcclusionTextureRef : GltfTextureRef
    {
        /// <summary>Occlusion strength blending. 0 = no occlusion, 1 = full. Default 1.0.</summary>
        public float Strength { get; set; } = 1f;
    }

    /// <summary>
    /// A PBR metallic-roughness material as found in a full glTF document.
    /// This is distinct from <see cref="AssetMaterial"/>, which is the SL asset form
    /// where textures are referenced by UUID.
    /// </summary>
    public sealed class GltfDocumentMaterial
    {
        public string? Name { get; set; }

        public Color4 BaseColorFactor { get; set; } = Color4.White;
        public GltfTextureRef? BaseColorTexture { get; set; }

        public float MetallicFactor { get; set; } = 1f;
        public float RoughnessFactor { get; set; } = 1f;
        public GltfTextureRef? MetallicRoughnessTexture { get; set; }

        public GltfNormalTextureRef? NormalTexture { get; set; }
        public GltfOcclusionTextureRef? OcclusionTexture { get; set; }
        public GltfTextureRef? EmissiveTexture { get; set; }

        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public GltfAlphaMode AlphaMode { get; set; } = GltfAlphaMode.Opaque;
        public float AlphaCutoff { get; set; } = 0.5f;
        public bool DoubleSided { get; set; }
    }
}
