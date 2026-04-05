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
using System.Text;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.Assets
{
    public enum GltfAlphaMode : byte
    {
        Opaque = 0,
        Blend = 1,
        Mask = 2
    }

    public struct GltfTextureTransform : IEquatable<GltfTextureTransform>
    {
        public static readonly GltfTextureTransform Default = new GltfTextureTransform
        {
            Offset = Vector2.Zero,
            Scale = new Vector2(1f, 1f),
            Rotation = 0f
        };

        public Vector2 Offset;
        public Vector2 Scale;
        public float Rotation;

        public readonly bool IsDefault =>
            Math.Abs(Rotation) < 1e-6f &&
            Offset.X == 0f && Offset.Y == 0f &&
            Math.Abs(Scale.X - 1f) < 1e-6f && Math.Abs(Scale.Y - 1f) < 1e-6f;

        public bool Equals(GltfTextureTransform other) =>
            Math.Abs(Rotation - other.Rotation) < 1e-6f &&
            Offset.X == other.Offset.X && Offset.Y == other.Offset.Y &&
            Scale.X == other.Scale.X && Scale.Y == other.Scale.Y;

        public override bool Equals(object obj) =>
            obj is GltfTextureTransform other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + Offset.X.GetHashCode();
                hash = hash * 31 + Offset.Y.GetHashCode();
                hash = hash * 31 + Scale.X.GetHashCode();
                hash = hash * 31 + Scale.Y.GetHashCode();
                hash = hash * 31 + Rotation.GetHashCode();
                return hash;
            }
        }
    }

    public class AssetMaterial : Asset
    {
        public const int TEXTURE_BASE_COLOR = 0;
        public const int TEXTURE_NORMAL = 1;
        public const int TEXTURE_METALLIC_ROUGHNESS = 2;
        public const int TEXTURE_EMISSIVE = 3;
        public const int TEXTURE_COUNT = 4;

        private const string KHR_TEXTURE_TRANSFORM = "KHR_texture_transform";
        private const string GLTF_VERSION = "2.0";

        /// <summary>UUID used in GLTF override layers to explicitly null a texture slot</summary>
        public static readonly UUID GLTF_OVERRIDE_NULL_UUID = new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff");

        public override AssetType AssetType => AssetType.Material;

        public string Name { get; set; } = string.Empty;

        /// <summary>Texture UUIDs indexed by TEXTURE_BASE_COLOR, TEXTURE_NORMAL, TEXTURE_METALLIC_ROUGHNESS, TEXTURE_EMISSIVE</summary>
        public UUID[] TextureIds { get; set; }

        /// <summary>UV transforms indexed by TEXTURE_BASE_COLOR, TEXTURE_NORMAL, TEXTURE_METALLIC_ROUGHNESS, TEXTURE_EMISSIVE</summary>
        public GltfTextureTransform[] TextureTransforms { get; set; }

        public Color4 BaseColorFactor { get; set; } = Color4.White;
        public Vector3 EmissiveFactor { get; set; } = Vector3.Zero;
        public float MetallicFactor { get; set; } = 1f;
        public float RoughnessFactor { get; set; } = 1f;
        public float AlphaCutoff { get; set; } = 0.5f;
        public GltfAlphaMode AlphaMode { get; set; } = GltfAlphaMode.Opaque;
        public bool DoubleSided { get; set; } = false;

        /// <summary>When true, AlphaMode is explicitly set on this layer (used for material layering)</summary>
        public bool OverrideAlphaMode { get; set; } = false;

        /// <summary>When true, DoubleSided is explicitly set on this layer (used for material layering)</summary>
        public bool OverrideDoubleSided { get; set; } = false;

        /// <summary>Initializes a new instance of an AssetMaterial object</summary>
        public AssetMaterial()
        {
            InitDefaults();
        }

        /// <summary>
        /// Construct an Asset object of type Material
        /// </summary>
        /// <param name="assetId">A unique <see cref="UUID"/> specific to this asset</param>
        /// <param name="assetData">A byte array containing the raw asset data</param>
        public AssetMaterial(UUID assetId, byte[] assetData)
            : base(assetId, assetData)
        {
            InitDefaults();
            Decode();
        }

        private void InitDefaults()
        {
            TextureIds = new UUID[TEXTURE_COUNT];
            TextureTransforms = new GltfTextureTransform[TEXTURE_COUNT];
            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                TextureTransforms[i] = GltfTextureTransform.Default;
            }
        }

        public override void Encode()
        {
            // 5 logical GLTF image/texture slots: BC, Normal, MR, Emissive, Occlusion (ORM duplicate of MR)
            const int SLOT_COUNT = 5;
            var slotIds = new UUID[SLOT_COUNT];
            var slotTransforms = new GltfTextureTransform[SLOT_COUNT];
            slotIds[0] = TextureIds[TEXTURE_BASE_COLOR];
            slotIds[1] = TextureIds[TEXTURE_NORMAL];
            slotIds[2] = TextureIds[TEXTURE_METALLIC_ROUGHNESS];
            slotIds[3] = TextureIds[TEXTURE_EMISSIVE];
            slotIds[4] = TextureIds[TEXTURE_METALLIC_ROUGHNESS]; // occlusion shares ORM texture
            slotTransforms[0] = TextureTransforms[TEXTURE_BASE_COLOR];
            slotTransforms[1] = TextureTransforms[TEXTURE_NORMAL];
            slotTransforms[2] = TextureTransforms[TEXTURE_METALLIC_ROUGHNESS];
            slotTransforms[3] = TextureTransforms[TEXTURE_EMISSIVE];
            slotTransforms[4] = TextureTransforms[TEXTURE_METALLIC_ROUGHNESS]; // occlusion = ORM

            var texIndex = new int[SLOT_COUNT];
            var images = new OSDArray();
            var textures = new OSDArray();
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                if (slotIds[i] == UUID.Zero && slotTransforms[i].IsDefault)
                {
                    texIndex[i] = -1;
                    continue;
                }

                var img = new OSDMap(1);
                img["uri"] = OSD.FromString(slotIds[i].ToString());
                images.Add(img);

                var tex = new OSDMap(1);
                tex["source"] = OSD.FromInteger(images.Count - 1);
                textures.Add(tex);

                texIndex[i] = textures.Count - 1;
            }

            var hasTransforms = false;
            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                if (!TextureTransforms[i].IsDefault) { hasTransforms = true; break; }
            }

            var doc = new OSDMap();
            var assetMeta = new OSDMap(1);
            assetMeta["version"] = OSD.FromString(GLTF_VERSION);
            doc["asset"] = assetMeta;

            if (images.Count > 0) { doc["images"] = images; }
            if (textures.Count > 0) { doc["textures"] = textures; }
            if (hasTransforms)
            {
                var extUsed = new OSDArray(1);
                extUsed.Add(OSD.FromString(KHR_TEXTURE_TRANSFORM));
                doc["extensionsUsed"] = extUsed;
            }

            var pbr = new OSDMap();
            pbr["baseColorFactor"] = OSD.FromColor4(BaseColorFactor);
            if (texIndex[0] >= 0)
            {
                pbr["baseColorTexture"] = MakeTextureInfo(texIndex[0], slotTransforms[0]);
            }
            pbr["metallicFactor"] = OSD.FromReal(MetallicFactor);
            pbr["roughnessFactor"] = OSD.FromReal(RoughnessFactor);
            if (texIndex[2] >= 0)
            {
                pbr["metallicRoughnessTexture"] = MakeTextureInfo(texIndex[2], slotTransforms[2]);
            }

            var material = new OSDMap();
            if (!string.IsNullOrEmpty(Name))
            {
                material["name"] = OSD.FromString(Name);
            }
            material["pbrMetallicRoughness"] = pbr;
            if (texIndex[1] >= 0)
            {
                material["normalTexture"] = MakeTextureInfo(texIndex[1], slotTransforms[1]);
            }
            if (texIndex[4] >= 0)
            {
                material["occlusionTexture"] = MakeTextureInfo(texIndex[4], slotTransforms[4]);
            }
            if (texIndex[3] >= 0)
            {
                material["emissiveTexture"] = MakeTextureInfo(texIndex[3], slotTransforms[3]);
            }
            material["emissiveFactor"] = OSD.FromVector3(EmissiveFactor);
            material["alphaMode"] = OSD.FromString(AlphaModeToString(AlphaMode));
            material["alphaCutoff"] = OSD.FromReal(AlphaCutoff);
            material["doubleSided"] = OSD.FromBoolean(DoubleSided);

            var extras = new OSDMap();
            if (OverrideAlphaMode) { extras["override_alpha_mode"] = OSD.FromBoolean(true); }
            if (OverrideDoubleSided) { extras["override_double_sided"] = OSD.FromBoolean(true); }
            if (extras.Count > 0) { material["extras"] = extras; }

            var materials = new OSDArray(1);
            materials.Add(material);
            doc["materials"] = materials;

            AssetData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(doc, true));
        }

        public sealed override bool Decode()
        {
            if (AssetData == null || AssetData.Length == 0) { return false; }

            try
            {
                var json = Encoding.UTF8.GetString(AssetData);
                if (!(OSDParser.DeserializeJson(json) is OSDMap doc)) { return false; }

                // images[] → UUID array
                var imageUuids = Array.Empty<UUID>();
                if (doc["images"] is OSDArray imgArray)
                {
                    imageUuids = new UUID[imgArray.Count];
                    for (var i = 0; i < imgArray.Count; i++)
                    {
                        if (imgArray[i] is OSDMap imgMap)
                        {
                            UUID.TryParse(imgMap["uri"].AsString(), out imageUuids[i]);
                        }
                    }
                }

                // textures[] → source index array
                var texSources = Array.Empty<int>();
                if (doc["textures"] is OSDArray texArray)
                {
                    texSources = new int[texArray.Count];
                    for (var i = 0; i < texArray.Count; i++)
                    {
                        texSources[i] = texArray[i] is OSDMap texMap ? texMap["source"].AsInteger() : -1;
                    }
                }

                if (!(doc["materials"] is OSDArray mats) || mats.Count == 0) { return false; }
                if (!(mats[0] is OSDMap mat)) { return false; }

                Name = mat["name"].AsString();

                if (mat["pbrMetallicRoughness"] is OSDMap pbr)
                {
                    if (pbr["baseColorFactor"] is OSDArray bcArr)
                    {
                        BaseColorFactor = bcArr.AsColor4();
                    }
                    (TextureIds[TEXTURE_BASE_COLOR], TextureTransforms[TEXTURE_BASE_COLOR]) =
                        ReadTextureSlot(pbr["baseColorTexture"] as OSDMap, imageUuids, texSources);
                    MetallicFactor = pbr.ContainsKey("metallicFactor")
                        ? Utils.Clamp((float)pbr["metallicFactor"].AsReal(), 0f, 1f)
                        : 1f;
                    RoughnessFactor = pbr.ContainsKey("roughnessFactor")
                        ? Utils.Clamp((float)pbr["roughnessFactor"].AsReal(), 0f, 1f)
                        : 1f;
                    (TextureIds[TEXTURE_METALLIC_ROUGHNESS], TextureTransforms[TEXTURE_METALLIC_ROUGHNESS]) =
                        ReadTextureSlot(pbr["metallicRoughnessTexture"] as OSDMap, imageUuids, texSources);
                }
                else
                {
                    MetallicFactor = 1f;
                    RoughnessFactor = 1f;
                }

                (TextureIds[TEXTURE_NORMAL], TextureTransforms[TEXTURE_NORMAL]) =
                    ReadTextureSlot(mat["normalTexture"] as OSDMap, imageUuids, texSources);
                (TextureIds[TEXTURE_EMISSIVE], TextureTransforms[TEXTURE_EMISSIVE]) =
                    ReadTextureSlot(mat["emissiveTexture"] as OSDMap, imageUuids, texSources);
                // occlusionTexture always mirrors metallicRoughness in SL (ORM); no separate property needed

                if (mat["emissiveFactor"] is OSDArray emArr)
                {
                    EmissiveFactor = emArr.AsVector3();
                }

                AlphaMode = StringToAlphaMode(mat["alphaMode"].AsString());
                AlphaCutoff = mat.ContainsKey("alphaCutoff")
                    ? Utils.Clamp((float)mat["alphaCutoff"].AsReal(), 0f, 1f)
                    : 0.5f;
                DoubleSided = mat["doubleSided"].AsBoolean();

                if (mat["extras"] is OSDMap extras)
                {
                    OverrideAlphaMode = extras["override_alpha_mode"].AsBoolean();
                    OverrideDoubleSided = extras["override_double_sided"].AsBoolean();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static OSDMap MakeTextureInfo(int textureIndex, GltfTextureTransform transform)
        {
            var info = new OSDMap();
            info["index"] = OSD.FromInteger(textureIndex);
            if (!transform.IsDefault)
            {
                var khr = new OSDMap(3);
                var offset = new OSDArray(2);
                offset.Add(OSD.FromReal(transform.Offset.X));
                offset.Add(OSD.FromReal(transform.Offset.Y));
                khr["offset"] = offset;
                var scale = new OSDArray(2);
                scale.Add(OSD.FromReal(transform.Scale.X));
                scale.Add(OSD.FromReal(transform.Scale.Y));
                khr["scale"] = scale;
                khr["rotation"] = OSD.FromReal(transform.Rotation);
                var extensions = new OSDMap(1);
                extensions[KHR_TEXTURE_TRANSFORM] = khr;
                info["extensions"] = extensions;
            }
            return info;
        }

        private static (UUID id, GltfTextureTransform transform) ReadTextureSlot(
            OSDMap texInfo, UUID[] imageUuids, int[] texSources)
        {
            if (texInfo == null) { return (UUID.Zero, GltfTextureTransform.Default); }

            var idx = texInfo["index"].AsInteger();
            if (idx < 0 || idx >= texSources.Length) { return (UUID.Zero, GltfTextureTransform.Default); }

            var srcIdx = texSources[idx];
            if (srcIdx < 0 || srcIdx >= imageUuids.Length) { return (UUID.Zero, GltfTextureTransform.Default); }

            return (imageUuids[srcIdx], ReadTransform(texInfo));
        }

        private static GltfTextureTransform ReadTransform(OSDMap texInfo)
        {
            var transform = GltfTextureTransform.Default;
            if (texInfo == null || !texInfo.ContainsKey("extensions")) { return transform; }
            if (!(texInfo["extensions"] is OSDMap exts) || !exts.ContainsKey(KHR_TEXTURE_TRANSFORM))
            {
                return transform;
            }
            if (!(exts[KHR_TEXTURE_TRANSFORM] is OSDMap khr)) { return transform; }
            if (khr["offset"] is OSDArray offArr) { transform.Offset = offArr.AsVector2(); }
            if (khr["scale"] is OSDArray scaleArr) { transform.Scale = scaleArr.AsVector2(); }
            if (khr.ContainsKey("rotation")) { transform.Rotation = (float)khr["rotation"].AsReal(); }
            return transform;
        }

        private static string AlphaModeToString(GltfAlphaMode mode) => mode switch
        {
            GltfAlphaMode.Blend => "BLEND",
            GltfAlphaMode.Mask => "MASK",
            _ => "OPAQUE"
        };

        private static GltfAlphaMode StringToAlphaMode(string s) => s switch
        {
            "BLEND" => GltfAlphaMode.Blend,
            "MASK" => GltfAlphaMode.Mask,
            _ => GltfAlphaMode.Opaque
        };
    }
}