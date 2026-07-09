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
using LibreMetaverse.StructuredData;

namespace LibreMetaverse.Assets
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

        public override bool Equals(object? obj) =>
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

        /// <summary>Matches C/C++ FLT_EPSILON (the smallest e such that 1.0+e != 1.0), used to nudge
        /// override values off of the GLTF-spec default so they stay distinguishable from an
        /// untouched field. NOT the same as .NET's float.Epsilon, which is the smallest
        /// representable positive float and is far too small to survive arithmetic near 1.0.</summary>
        private const float FLT_EPSILON = 1.1920929E-07f;

        /// <summary>UUID used in GLTF override layers to explicitly null a texture slot</summary>
        public static readonly UUID GLTF_OVERRIDE_NULL_UUID = new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff");

        public override AssetType AssetType => AssetType.Material;

        public string Name { get; set; } = string.Empty;

        /// <summary>Texture UUIDs indexed by TEXTURE_BASE_COLOR, TEXTURE_NORMAL, TEXTURE_METALLIC_ROUGHNESS, TEXTURE_EMISSIVE</summary>
        public UUID[] TextureIds { get; set; } = null!;

        /// <summary>UV transforms indexed by TEXTURE_BASE_COLOR, TEXTURE_NORMAL, TEXTURE_METALLIC_ROUGHNESS, TEXTURE_EMISSIVE</summary>
        public GltfTextureTransform[] TextureTransforms { get; set; } = null!;

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

        /// <summary>
        /// Serializes this material to its minified GLTF JSON string form (the same encoding
        /// produced by <see cref="Encode"/>, exposed directly for embedding in wire payloads that
        /// carry GLTF as a JSON string rather than as asset bytes -- e.g. the "gltf_json" field of
        /// the ModifyMaterialParams capability).
        /// </summary>
        public string ToJson()
        {
            Encode();
            return Encoding.UTF8.GetString(AssetData);
        }

        /// <summary>Material with all properties at their GLTF-spec default values. Used as the
        /// comparison baseline when encoding/decoding override data (mirrors
        /// LLGLTFMaterial::sDefault in the reference viewer).</summary>
        public static readonly AssetMaterial Default = new AssetMaterial();

        // ---- Override-aware setters -----------------------------------------------------------
        // Mirror LLGLTFMaterial's for_override setters (llgltfmaterial.cpp): when forOverride is
        // true and the value being set is bit-identical to the GLTF-spec default, the value is
        // nudged just off of default so that, after a round trip, it can still be told apart from a
        // field that was never touched (which is represented by leaving it at exactly the default).

        /// <summary>Sets a texture slot (see TEXTURE_* constants). When forOverride is true, setting
        /// UUID.Zero means "override to no texture at all" (encoded via
        /// <see cref="GLTF_OVERRIDE_NULL_UUID"/>) rather than "no override" (which UUID.Zero means
        /// outside of an override context).</summary>
        public void SetTextureId(int slot, UUID id, bool forOverride = false)
        {
            TextureIds[slot] = forOverride && id == UUID.Zero ? GLTF_OVERRIDE_NULL_UUID : id;
        }

        public void SetBaseColorFactor(Color4 color, bool forOverride = false)
        {
            BaseColorFactor = color;
            if (forOverride && BaseColorFactor == Default.BaseColorFactor)
            {
                BaseColorFactor = new Color4(BaseColorFactor.R, BaseColorFactor.G, BaseColorFactor.B,
                    BaseColorFactor.A - FLT_EPSILON);
            }
        }

        public void SetEmissiveFactor(Vector3 color, bool forOverride = false)
        {
            EmissiveFactor = color;
            if (forOverride && EmissiveFactor == Default.EmissiveFactor)
            {
                EmissiveFactor = new Vector3(EmissiveFactor.X + FLT_EPSILON, EmissiveFactor.Y, EmissiveFactor.Z);
            }
        }

        public void SetMetallicFactor(float metallic, bool forOverride = false)
        {
            MetallicFactor = Utils.Clamp(metallic, 0f, forOverride ? 1f - FLT_EPSILON : 1f);
        }

        public void SetRoughnessFactor(float roughness, bool forOverride = false)
        {
            RoughnessFactor = Utils.Clamp(roughness, 0f, forOverride ? 1f - FLT_EPSILON : 1f);
        }

        public void SetAlphaCutoff(float cutoff, bool forOverride = false)
        {
            AlphaCutoff = Utils.Clamp(cutoff, 0f, 1f);
            if (forOverride && AlphaCutoff == Default.AlphaCutoff)
            {
                AlphaCutoff -= FLT_EPSILON;
            }
        }

        public void SetAlphaMode(GltfAlphaMode mode, bool forOverride = false)
        {
            AlphaMode = mode;
            OverrideAlphaMode = forOverride && AlphaMode == Default.AlphaMode;
        }

        public void SetDoubleSided(bool doubleSided, bool forOverride = false)
        {
            DoubleSided = doubleSided;
            OverrideDoubleSided = forOverride && DoubleSided == Default.DoubleSided;
        }

        // *NOTE: texture offsets only exist in overrides, so no forOverride parameter is needed.
        public void SetTextureOffset(int slot, Vector2 offset) => TextureTransforms[slot].Offset = offset;
        public void SetTextureScale(int slot, Vector2 scale) => TextureTransforms[slot].Scale = scale;
        public void SetTextureRotation(int slot, float rotation) => TextureTransforms[slot].Rotation = rotation;

        /// <summary>
        /// Clears this material back to an "empty override" (all fields at fallthrough/default)
        /// except texture transforms, which are preserved. Used when clearing a material override
        /// while keeping any UV adjustments already applied. Mirrors
        /// LLGLTFMaterial::setBaseMaterial in the reference viewer.
        /// </summary>
        public void SetBaseMaterial()
        {
            var transforms = TextureTransforms;
            Name = string.Empty;
            TextureIds = new UUID[TEXTURE_COUNT];
            BaseColorFactor = Default.BaseColorFactor;
            EmissiveFactor = Default.EmissiveFactor;
            MetallicFactor = Default.MetallicFactor;
            RoughnessFactor = Default.RoughnessFactor;
            AlphaCutoff = Default.AlphaCutoff;
            AlphaMode = Default.AlphaMode;
            DoubleSided = Default.DoubleSided;
            OverrideAlphaMode = false;
            OverrideDoubleSided = false;
            TextureTransforms = transforms;
        }

        /// <summary>
        /// Merges the given override material into this base material, applying only the fields
        /// that differ from <see cref="Default"/> (or are explicitly flagged as an override-to-
        /// default via <see cref="OverrideAlphaMode"/>/<see cref="OverrideDoubleSided"/>). Mirrors
        /// LLGLTFMaterial::applyOverride in the reference viewer.
        /// </summary>
        public void ApplyOverride(AssetMaterial overrideMat)
        {
            if (overrideMat == null) { throw new ArgumentNullException(nameof(overrideMat)); }

            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                var overrideId = overrideMat.TextureIds[i];
                if (overrideId == GLTF_OVERRIDE_NULL_UUID)
                {
                    TextureIds[i] = UUID.Zero;
                }
                else if (overrideId != UUID.Zero)
                {
                    TextureIds[i] = overrideId;
                }
            }

            if (overrideMat.BaseColorFactor != Default.BaseColorFactor) { BaseColorFactor = overrideMat.BaseColorFactor; }
            if (overrideMat.EmissiveFactor != Default.EmissiveFactor) { EmissiveFactor = overrideMat.EmissiveFactor; }
            if (overrideMat.MetallicFactor != Default.MetallicFactor) { MetallicFactor = overrideMat.MetallicFactor; }
            if (overrideMat.RoughnessFactor != Default.RoughnessFactor) { RoughnessFactor = overrideMat.RoughnessFactor; }
            if (overrideMat.AlphaMode != Default.AlphaMode || overrideMat.OverrideAlphaMode) { AlphaMode = overrideMat.AlphaMode; }
            if (overrideMat.AlphaCutoff != Default.AlphaCutoff) { AlphaCutoff = overrideMat.AlphaCutoff; }
            if (overrideMat.DoubleSided != Default.DoubleSided || overrideMat.OverrideDoubleSided) { DoubleSided = overrideMat.DoubleSided; }

            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                if (overrideMat.TextureTransforms[i].Offset != Default.TextureTransforms[i].Offset)
                {
                    TextureTransforms[i].Offset = overrideMat.TextureTransforms[i].Offset;
                }
                if (overrideMat.TextureTransforms[i].Scale != Default.TextureTransforms[i].Scale)
                {
                    TextureTransforms[i].Scale = overrideMat.TextureTransforms[i].Scale;
                }
                if (overrideMat.TextureTransforms[i].Rotation != Default.TextureTransforms[i].Rotation)
                {
                    TextureTransforms[i].Rotation = overrideMat.TextureTransforms[i].Rotation;
                }
            }
        }

        /// <summary>
        /// Encodes this material as a compact LLSD override (only fields that differ from
        /// <see cref="Default"/>, using short keys). This is the wire shape used specifically by
        /// the ModifyRegion capability's terrain material overrides -- NOT the same as
        /// <see cref="ToJson"/>, which ModifyMaterialParams uses for per-face overrides instead.
        /// Mirrors LLGLTFMaterial::getOverrideLLSD in the reference viewer.
        /// </summary>
        public OSDMap ToOverrideOsd()
        {
            var data = new OSDMap();

            OSDArray? tex = null;
            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                var id = TextureIds[i];
                if (id != UUID.Zero && id != Default.TextureIds[i])
                {
                    tex ??= NewSparseArray(TEXTURE_COUNT);
                    tex[i] = OSD.FromUUID(id);
                }
            }
            if (tex != null) { data["tex"] = tex; }

            if (BaseColorFactor != Default.BaseColorFactor) { data["bc"] = OSD.FromColor4(BaseColorFactor); }
            if (EmissiveFactor != Default.EmissiveFactor) { data["ec"] = OSD.FromVector3(EmissiveFactor); }
            if (MetallicFactor != Default.MetallicFactor) { data["mf"] = OSD.FromReal(MetallicFactor); }
            if (RoughnessFactor != Default.RoughnessFactor) { data["rf"] = OSD.FromReal(RoughnessFactor); }
            if (AlphaMode != Default.AlphaMode || OverrideAlphaMode) { data["am"] = OSD.FromInteger((int)AlphaMode); }
            if (AlphaCutoff != Default.AlphaCutoff) { data["ac"] = OSD.FromReal(AlphaCutoff); }
            if (DoubleSided != Default.DoubleSided || OverrideDoubleSided) { data["ds"] = OSD.FromBoolean(DoubleSided); }

            OSDArray? ti = null;
            for (var i = 0; i < TEXTURE_COUNT; i++)
            {
                var t = TextureTransforms[i];
                var d = Default.TextureTransforms[i];
                OSDMap? entry = null;
                if (t.Offset != d.Offset)
                {
                    entry ??= new OSDMap();
                    var o = new OSDArray(2);
                    o.Add(OSD.FromReal(t.Offset.X));
                    o.Add(OSD.FromReal(t.Offset.Y));
                    entry["o"] = o;
                }
                if (t.Scale != d.Scale)
                {
                    entry ??= new OSDMap();
                    var s = new OSDArray(2);
                    s.Add(OSD.FromReal(t.Scale.X));
                    s.Add(OSD.FromReal(t.Scale.Y));
                    entry["s"] = s;
                }
                if (t.Rotation != d.Rotation)
                {
                    entry ??= new OSDMap();
                    entry["r"] = OSD.FromReal(t.Rotation);
                }
                if (entry != null)
                {
                    ti ??= NewSparseArray(TEXTURE_COUNT);
                    ti[i] = entry;
                }
            }
            if (ti != null) { data["ti"] = ti; }

            return data;
        }

        /// <summary>
        /// Decodes a compact LLSD override (as produced by <see cref="ToOverrideOsd"/>) back into
        /// an <see cref="AssetMaterial"/>. Any field present with a value bit-identical to
        /// <see cref="Default"/> is nudged just off of default, matching the encoder's own
        /// override-vs-untouched disambiguation. Mirrors LLGLTFMaterial::applyOverrideLLSD.
        /// </summary>
        public static AssetMaterial FromOverrideOsd(OSDMap data)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            var mat = new AssetMaterial();

            if (data["tex"] is OSDArray tex)
            {
                for (var i = 0; i < tex.Count && i < TEXTURE_COUNT; i++)
                {
                    mat.TextureIds[i] = tex[i].AsUUID();
                }
            }

            if (data["bc"] is OSDArray bc)
            {
                mat.BaseColorFactor = bc.AsColor4();
                if (mat.BaseColorFactor == Default.BaseColorFactor)
                {
                    mat.BaseColorFactor = new Color4(mat.BaseColorFactor.R, mat.BaseColorFactor.G,
                        mat.BaseColorFactor.B, mat.BaseColorFactor.A - FLT_EPSILON);
                }
            }

            if (data["ec"] is OSDArray ec)
            {
                mat.EmissiveFactor = ec.AsVector3();
                if (mat.EmissiveFactor == Default.EmissiveFactor)
                {
                    mat.EmissiveFactor = new Vector3(mat.EmissiveFactor.X + FLT_EPSILON, mat.EmissiveFactor.Y, mat.EmissiveFactor.Z);
                }
            }

            if (data.ContainsKey("mf"))
            {
                mat.MetallicFactor = (float)data["mf"].AsReal();
                if (mat.MetallicFactor == Default.MetallicFactor) { mat.MetallicFactor -= FLT_EPSILON; }
            }

            if (data.ContainsKey("rf"))
            {
                mat.RoughnessFactor = (float)data["rf"].AsReal();
                if (mat.RoughnessFactor == Default.RoughnessFactor) { mat.RoughnessFactor -= FLT_EPSILON; }
            }

            if (data.ContainsKey("am"))
            {
                mat.AlphaMode = (GltfAlphaMode)data["am"].AsInteger();
                mat.OverrideAlphaMode = true;
            }

            if (data.ContainsKey("ac"))
            {
                mat.AlphaCutoff = (float)data["ac"].AsReal();
                if (mat.AlphaCutoff == Default.AlphaCutoff) { mat.AlphaCutoff -= FLT_EPSILON; }
            }

            if (data.ContainsKey("ds"))
            {
                mat.DoubleSided = data["ds"].AsBoolean();
                mat.OverrideDoubleSided = true;
            }

            if (data["ti"] is OSDArray ti)
            {
                for (var i = 0; i < ti.Count && i < TEXTURE_COUNT; i++)
                {
                    if (!(ti[i] is OSDMap entry)) { continue; }
                    if (entry["o"] is OSDArray o) { mat.TextureTransforms[i].Offset = o.AsVector2(); }
                    if (entry["s"] is OSDArray s) { mat.TextureTransforms[i].Scale = s.AsVector2(); }
                    if (entry.ContainsKey("r")) { mat.TextureTransforms[i].Rotation = (float)entry["r"].AsReal(); }
                }
            }

            return mat;
        }

        private static OSDArray NewSparseArray(int count)
        {
            var arr = new OSDArray(count);
            for (var i = 0; i < count; i++) { arr.Add(new OSD()); }
            return arr;
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
            OSDMap? texInfo, UUID[] imageUuids, int[] texSources)
        {
            if (texInfo == null) { return (UUID.Zero, GltfTextureTransform.Default); }

            var idx = texInfo["index"].AsInteger();
            if (idx < 0 || idx >= texSources.Length) { return (UUID.Zero, GltfTextureTransform.Default); }

            var srcIdx = texSources[idx];
            if (srcIdx < 0 || srcIdx >= imageUuids.Length) { return (UUID.Zero, GltfTextureTransform.Default); }

            return (imageUuids[srcIdx], ReadTransform(texInfo));
        }

        private static GltfTextureTransform ReadTransform(OSDMap? texInfo)
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