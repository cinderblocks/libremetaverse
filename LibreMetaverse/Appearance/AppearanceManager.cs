/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2024-2025, Sjofn LLC.
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
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using LibreMetaverse.Assets;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    #region Enums

    /// <summary>
    /// Index of TextureEntry slots for avatar appearances
    /// </summary>
    public enum AvatarTextureIndex
    {
        Unknown = -1,
        HeadBodypaint = 0,
        UpperShirt,
        LowerPants,
        EyesIris,
        Hair,
        UpperBodypaint,
        LowerBodypaint,
        LowerShoes,
        HeadBaked,  // pre-composited
        UpperBaked, // pre-composited
        LowerBaked, // pre-composited
        EyesBaked,  // pre-composited
        LowerSocks,
        UpperJacket,
        LowerJacket,
        UpperGloves,
        UpperUndershirt,
        LowerUnderpants,
        Skirt,
        SkirtBaked, // pre-composited
        HairBaked,  // pre-composited
        LowerAlpha,
        UpperAlpha,
        HeadAlpha,
        EyesAlpha,
        HairAlpha,
        HeadTattoo,
        UpperTattoo,
        LowerTattoo,
        HeadUniversalTattoo,
        UpperUniversalTattoo,
        LowerUniversalTattoo,
        SkirtTattoo,
        HairTattoo,
        EyesTattoo,
        LeftArmTattoo,
        LeftLegTattoo,
        Aux1Tattoo,
        Aux2Tattoo,
        Aux3Tattoo,
        LeftArmBaked,   // pre-composited
        LegLegBaked,    // pre-composited
        Aux1Baked,  // pre-composited
        Aux2Baked,  // pre-composited
        Aux3Baked,  // pre-composited
        NumberOfEntries
    }

    /// <summary>
    /// Bake layers for avatar appearance
    /// </summary>
    public enum BakeType
    {
        Unknown = -1,
        Head = 0,
        UpperBody = 1,
        LowerBody = 2,
        Eyes = 3,
        Skirt = 4,
        Hair = 5,
        BakedLeftArm,
        BakedLeftLeg,
        BakedAux1,
        BakedAux2,
        BakedAux3
    }

    /// <summary>
    /// Appearance Flags, introduced with server side baking, currently unused
    /// </summary>
    [Flags]
    public enum AppearanceFlags : uint
    {
        None = 0
    }


    #endregion Enums

    public class AppearanceManagerException : Exception
    {
        public AppearanceManagerException(string message)
            : base(message) { }
    }

    public partial class AppearanceManager : IDisposable
    {
        #region Constants
        /// <summary>Mask for multiple attachments</summary>
        public static readonly byte ATTACHMENT_ADD = 0x80;
        /// <summary>Mapping between BakeType (index) and AvatarTextureIndex (value) for all 11 bake layers</summary>
        public static readonly byte[] BakeIndexToTextureIndex = new byte[BAKED_TEXTURE_COUNT]
            { 8, 9, 10, 11, 19, 20, 40, 41, 42, 43, 44 };
            // Head=8  Upper=9  Lower=10  Eyes=11  Skirt=19  Hair=20
            // LeftArm=40  LeftLeg=41  Aux1=42  Aux2=43  Aux3=44
        /// <summary>Maximum number of concurrent downloads for wearable assets and textures</summary>
        private const int MAX_CONCURRENT_DOWNLOADS = 5;
        /// <summary>Maximum number of concurrent uploads for baked textures</summary>
        private const int MAX_CONCURRENT_UPLOADS = 6;
        /// <summary>Timeout for fetching inventory listings</summary>
        private readonly TimeSpan INVENTORY_TIMEOUT = TimeSpan.FromSeconds(30);
        /// <summary>Timeout for fetching a single wearable, or receiving a single packet response</summary>
        private readonly TimeSpan WEARABLE_TIMEOUT = TimeSpan.FromSeconds(30);
        /// <summary>Timeout for fetching a single texture</summary>
        private readonly TimeSpan TEXTURE_TIMEOUT = TimeSpan.FromSeconds(120);
        /// <summary>Timeout for uploading a single baked texture</summary>
        private readonly TimeSpan UPLOAD_TIMEOUT = TimeSpan.FromSeconds(90);
        /// <summary>Number of times to retry bake upload</summary>
        private const int UPLOAD_RETRIES = 2;
        /// <summary>When changing outfit, kick off rebake after REBAKE_DELAY has passed since the last change</summary>
        private const int REBAKE_DELAY = 1000 * 5;
        /// <summary>Total number of wearables allowed for each avatar</summary>
        public const int WEARABLE_COUNT_MAX = 60;
        /// <summary>Total number of wearables for each avatar</summary>
        public const int WEARABLE_COUNT = 16;
        /// <summary>Total number of baked textures on each avatar (6 classic + 5 extended)</summary>
        public const int BAKED_TEXTURE_COUNT = 11;
        /// <summary>Total number of wearables per bake layer</summary>
        public const int WEARABLES_PER_LAYER = 9;
        /// <summary>Map of what wearables are included in each bake</summary>
        public static readonly WearableType[][] WEARABLE_BAKE_MAP = {
            // Classic bakes (0-5)
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Hair,    WearableType.Alpha,   WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Shirt,   WearableType.Jacket,  WearableType.Gloves,  WearableType.Undershirt, WearableType.Alpha,        WearableType.Invalid },
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Pants,   WearableType.Shoes,   WearableType.Socks,   WearableType.Jacket,     WearableType.Underpants,   WearableType.Alpha   },
            new[] { WearableType.Eyes,  WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Skirt, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Hair,  WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            // Extended bakes (6-10): LeftArm, LeftLeg, Aux1, Aux2, Aux3
            // These bakes have no fixed wearable-type inputs — cache checks always miss for them.
            new[] { WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid },
            new[] { WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid },
            new[] { WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid },
            new[] { WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid },
            new[] { WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid }
        };
        /// <summary>Magic values to finalize the cache check hashes for each bake.
        /// Classic bake hashes are from the SL viewer source; extended bake hashes
        /// are from indra/llappearance/llwearabledata.cpp.</summary>
        public static readonly UUID[] BAKED_TEXTURE_HASH = {
            // Classic bakes 0-5
            new UUID("18ded8d6-bcfc-e415-8539-944c0f5ea7a6"), // Head
            new UUID("338c29e3-3024-4dbb-998d-7c04cf4fa88f"), // UpperBody
            new UUID("91b4a2c7-1b1a-ba16-9a16-1f8f8dcc1c3f"), // LowerBody
            new UUID("b2cf28af-b840-1071-3c6a-78085d8128b5"), // Eyes
            new UUID("ea800387-ea1a-14e0-56cb-24f2022f969a"), // Skirt
            new UUID("0af1ef7c-ad24-11dd-8790-001f5bf833e8"), // Hair
            // Extended bakes 6-10
            new UUID("9d762b57-ffe3-2e34-d897-0c44c8e07c72"), // BakedLeftArm
            new UUID("e12f6f01-8b0e-e00a-03c7-bc7e56a6cbdc"), // BakedLeftLeg
            new UUID("3e2984a2-f03c-71d5-3e97-75fb8c7e1e2f"), // BakedAux1
            new UUID("29bbb16c-4b0c-4809-8de5-4b4df7cca8ef"), // BakedAux2
            new UUID("e0f8b768-e68d-a0cc-d1d8-c2e7f3b51b6e")  // BakedAux3
        };
        /// <summary>Default avatar texture, used to detect when a custom
        /// texture is not set for a face</summary>
        public static readonly UUID DEFAULT_AVATAR_TEXTURE = new UUID("c228d1cf-4b5d-4ba8-84f4-899a0796aa97");

        /// <summary>
        /// Magic sentinel UUIDs that an attachment face's TextureEntry can carry to
        /// request that the viewer substitute a specific avatar bake layer instead of
        /// fetching a standalone texture asset.  Equivalent to the IMG_USE_BAKED_*
        /// constants in indra/llcommon/indra_constants.cpp of the SL viewer.
        /// </summary>
        /// <remarks>
        /// When a prim face's texture ID equals one of these values the renderer should
        /// resolve it to the avatar's current baked texture for that layer via
        /// <see cref="BakeTypeToAgentTextureIndex"/> and the avatar's TextureEntry.
        /// </remarks>
        public static readonly UUID IMG_USE_BAKED_HEAD     = new UUID("5a9f4a74-30f2-821c-b88d-70499d3e7183");
        public static readonly UUID IMG_USE_BAKED_UPPER    = new UUID("ae2de45c-d252-50b8-5c6e-19f39ce79317");
        public static readonly UUID IMG_USE_BAKED_LOWER    = new UUID("24daea5f-0539-cfcf-047f-fbc40b2786ba");
        public static readonly UUID IMG_USE_BAKED_EYES     = new UUID("52cc6bb6-2ee5-e632-d3ad-50197b1dcb8a");
        public static readonly UUID IMG_USE_BAKED_SKIRT    = new UUID("43529ce8-7faa-ad92-165a-bc4078371687");
        public static readonly UUID IMG_USE_BAKED_HAIR     = new UUID("09aac1fb-6bce-0bee-7d44-caac6dbb6c63");
        public static readonly UUID IMG_USE_BAKED_LEFTARM  = new UUID("ff62763f-d60a-9855-890b-0c96f8f8cd98");
        public static readonly UUID IMG_USE_BAKED_LEFTLEG  = new UUID("8e915e25-31d1-cc95-ae08-d58a47488251");
        public static readonly UUID IMG_USE_BAKED_AUX1     = new UUID("9742065b-19b5-297c-858a-29711d539043");
        public static readonly UUID IMG_USE_BAKED_AUX2     = new UUID("03642e83-2bd1-4eb9-34b4-4c47ed586d2d");
        public static readonly UUID IMG_USE_BAKED_AUX3     = new UUID("edd51b77-fc10-ce7a-4b3d-011dfc349e4f");

        /// <summary>
        /// Maps each IMG_USE_BAKED_* sentinel UUID to the corresponding
        /// <see cref="AvatarTextureIndex"/> baked slot on the avatar's TextureEntry.
        /// </summary>
        public static readonly FrozenDictionary<UUID, AvatarTextureIndex> IMG_USE_BAKED_INDICES =
            new Dictionary<UUID, AvatarTextureIndex>
            {
                { IMG_USE_BAKED_HEAD,    AvatarTextureIndex.HeadBaked    },
                { IMG_USE_BAKED_UPPER,   AvatarTextureIndex.UpperBaked   },
                { IMG_USE_BAKED_LOWER,   AvatarTextureIndex.LowerBaked   },
                { IMG_USE_BAKED_EYES,    AvatarTextureIndex.EyesBaked    },
                { IMG_USE_BAKED_SKIRT,   AvatarTextureIndex.SkirtBaked   },
                { IMG_USE_BAKED_HAIR,    AvatarTextureIndex.HairBaked    },
                { IMG_USE_BAKED_LEFTARM, AvatarTextureIndex.LeftArmBaked },
                { IMG_USE_BAKED_LEFTLEG, AvatarTextureIndex.LegLegBaked  },
                { IMG_USE_BAKED_AUX1,    AvatarTextureIndex.Aux1Baked    },
                { IMG_USE_BAKED_AUX2,    AvatarTextureIndex.Aux2Baked    },
                { IMG_USE_BAKED_AUX3,    AvatarTextureIndex.Aux3Baked    },
            }.ToFrozenDictionary();

        #endregion Constants

        #region Structs / Classes

        /// <summary>
        /// Contains information about a wearable inventory item
        /// </summary>
        public class WearableData
        {
            /// <summary>Inventory ItemID of the wearable</summary>
            public UUID ItemID;
            /// <summary>AssetID of the wearable asset</summary>
            public UUID AssetID;
            /// <summary>WearableType of the wearable</summary>
            public WearableType WearableType;
            /// <summary>AssetType of the wearable</summary>
            public AssetType AssetType;
            /// <summary>Asset data for the wearable</summary>
            public AssetWearable? Asset;

            public override string ToString()
            {
                return String.Format("ItemID: {0}, AssetID: {1}, WearableType: {2}, AssetType: {3}, Asset: {4}",
                    ItemID, AssetID, WearableType, AssetType, Asset != null ? Asset.Name : "(null)");
            }
        }

        /// <summary>
        /// Data collected from visual params for each wearable
        /// needed for the calculation of the color
        /// </summary>
        public struct ColorParamInfo
        {
            public VisualParam VisualParam;
            public VisualColorParam VisualColorParam;
            public float Value;
            public WearableType WearableType;
        }

        /// <summary>
        /// Holds a texture assetID and the data needed to bake this layer into
        /// an outfit texture. Used to keep track of currently worn textures
        /// and baking data
        /// </summary>
        public class TextureData
        {
            /// <summary>A texture AssetID</summary>
            public UUID TextureID = UUID.Zero;
            /// <summary>Asset data for the texture</summary>
            public AssetTexture? Texture = null;
            /// <summary>Collection of alpha masks that needs applying</summary>
            public Dictionary<VisualAlphaParam, float> AlphaMasks = new Dictionary<VisualAlphaParam, float>();
            /// <summary>Tint that should be applied to the texture</summary>
            public Color4 Color = Color4.White;
            /// <summary>Where on avatar does this texture belong</summary>
            public AvatarTextureIndex TextureIndex = AvatarTextureIndex.Unknown;

            public override string ToString()
            {
                return String.Format("TextureID: {0}, Texture: {1}",
                    TextureID, Texture != null ? Texture.AssetData.Length + " bytes" : "(null)");
            }
        }

        #endregion Structs / Classes

        #region Event delegates, Raise Events

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AgentWearablesReplyEventArgs>? m_AgentWearablesReply;

        /// <summary>Raises the AgentWearablesReply event</summary>
        /// <param name="e">An AgentWearablesReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAgentWearables(AgentWearablesReplyEventArgs e)
        {
            m_AgentWearablesReply?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AgentWearablesLock = new object();

        /// <summary>Triggered when a legacy AgentWearablesUpdate packet is received
        /// or a request has been made for COF contents has populated <see cref="Wearables"/>
        /// telling us what the avatar is currently wearing</summary>
        /// <seealso cref="RequestAgentWorn" />
        /// <seealso cref="RequestAgentWearablesLLUDP"/>
        public event EventHandler<AgentWearablesReplyEventArgs> AgentWearablesReply
        {
            add { lock (m_AgentWearablesLock) { m_AgentWearablesReply += value; } }
            remove { lock (m_AgentWearablesLock) { m_AgentWearablesReply -= value; } }
        }


        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AgentCachedBakesReplyEventArgs>? m_AgentCachedBakesReply;

        /// <summary>Raises the CachedBakesReply event</summary>
        /// <param name="e">An AgentCachedBakesReplyEventArgs object containing the
        /// data returned from the data server AgentCachedTextureResponse</param>
        protected virtual void OnAgentCachedBakes(AgentCachedBakesReplyEventArgs e)
        {
            m_AgentCachedBakesReply?.Invoke(this, e);
        }


        /// <summary>Thread sync lock object</summary>
        private readonly object m_AgentCachedBakesLock = new object();

        /// <summary>Raised when an AgentCachedTextureResponse packet is
        /// received, giving a list of cached bakes that were found on the
        /// simulator
        /// <seealso cref="RequestCachedBakes"/> request.</summary>
        public event EventHandler<AgentCachedBakesReplyEventArgs> CachedBakesReply
        {
            add { lock (m_AgentCachedBakesLock) { m_AgentCachedBakesReply += value; } }
            remove { lock (m_AgentCachedBakesLock) { m_AgentCachedBakesReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AppearanceSetEventArgs>? m_AppearanceSet;

        /// <summary>Raises the AppearanceSet event</summary>
        /// <param name="e">An AppearanceSetEventArgs object indicating if the operation was successful</param>
        protected virtual void OnAppearanceSet(AppearanceSetEventArgs e)
        {
            m_AppearanceSet?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AppearanceSetLock = new object();

        /// <summary>
        /// Raised when appearance data is sent to the simulator, also indicates
        /// the main appearance thread is finished.
        /// </summary>
        /// <seealso cref="RequestAgentSetAppearance"/> request.
        public event EventHandler<AppearanceSetEventArgs> AppearanceSet
        {
            add { lock (m_AppearanceSetLock) { m_AppearanceSet += value; } }
            remove { lock (m_AppearanceSetLock) { m_AppearanceSet -= value; } }
        }


        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RebakeAvatarTexturesEventArgs>? m_RebakeAvatarReply;

        /// <summary>Raises the RebakeAvatarRequested event</summary>
        /// <param name="e">An RebakeAvatarTexturesEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnRebakeAvatar(RebakeAvatarTexturesEventArgs e)
        {
            m_RebakeAvatarReply?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RebakeAvatarLock = new object();

        /// <summary>
        /// Triggered when the simulator requests the agent rebake its appearance.
        /// </summary>
        /// <seealso cref="RebakeAvatarRequest"/>
        public event EventHandler<RebakeAvatarTexturesEventArgs> RebakeAvatarRequested
        {
            add { lock (m_RebakeAvatarLock) { m_RebakeAvatarReply += value; } }
            remove { lock (m_RebakeAvatarLock) { m_RebakeAvatarReply -= value; } }
        }

        #endregion

        #region Properties and public fields

        /// <summary>
        /// Texture provider used by the baking pipeline to download avatar textures.
        /// Swap this out with a custom <see cref="IBakingTextureProvider"/> implementation
        /// to supply textures from a non-standard source (e.g. an OpenSimulator
        /// server-side baking service that reads from a local asset database).
        /// </summary>
        public IBakingTextureProvider TextureProvider { get; set; }

        /// <summary>
        /// Returns true if an appearance workflow task is currently running
        /// </summary>
        public bool ManagerBusy
        {
            get
            {
                lock (_appearanceLock)
                {
                    return AppearanceTask != null && !AppearanceTask.IsCompleted;
                }
            }
        }

        /// <summary>Visual parameters last sent to the sim</summary>
        public byte[] MyVisualParameters = Array.Empty<byte>();
        
        /// <summary>Textures about this client sent to the sim</summary>
        public Primitive.TextureEntry MyTextures = new Primitive.TextureEntry(Primitive.TextureEntry.WHITE_TEXTURE);

        /// <summary>
        /// Collects the current float value for every visual parameter by merging all
        /// downloaded wearable parameters, falling back to each parameter's default value.
        /// The returned dictionary can be passed directly to
        /// <see cref="LibreMetaverse.Rendering.LindenAvatarDefinition.ComputeBoneTransforms"/>.
        /// </summary>
        /// <returns>
        /// A dictionary mapping every visual parameter ID to its current value.
        /// </returns>
        public Dictionary<int, float> GetCurrentParamValues()
        {
            var result = new Dictionary<int, float>(VisualParams.Params.Count);
            lock (Wearables)
            {
                foreach (var kvp in VisualParams.Params)
                {
                    var vp = kvp.Value;
                    var paramValue = 0f;
                    if (!Wearables.Any(wearableList => wearableList.Value.Any(
                            wearable => wearable.Asset != null &&
                                        wearable.Asset.Params.TryGetValue(vp.ParamID, out paramValue))))
                    {
                        paramValue = vp.DefaultValue;
                    }
                    result[vp.ParamID] = paramValue;
                }
            }

            // Derive driven (group-1) params from their group-0 driver values.
            // Mirrors LLDriverParam::setDrivenWeight in the SL viewer and
            // Avatar.DecodeVisualParams() for remote avatars.
            foreach (var kv in VisualParams.Params)
            {
                var driverVp = kv.Value;
                if (driverVp.DrivenParams == null || driverVp.DrivenParams.Length == 0) continue;
                if (!result.TryGetValue(driverVp.ParamID, out var driverVal)) continue;

                foreach (var driven in driverVp.DrivenParams)
                {
                    if (!VisualParams.Params.TryGetValue(driven.ParamID, out var drivenVp)) continue;

                    float drivenNorm;
                    if (!driven.HasRange)
                    {
                        float range = driverVp.MaxValue - driverVp.MinValue;
                        drivenNorm = range > 1e-6f
                            ? (driverVal - driverVp.MinValue) / range
                            : 0f;
                    }
                    else
                    {
                        if (driverVal < driven.Min1)
                            drivenNorm = 0f;
                        else if (driverVal < driven.Max1)
                            drivenNorm = (driverVal - driven.Min1) / (driven.Max1 - driven.Min1);
                        else if (driverVal <= driven.Max2)
                            drivenNorm = 1f;
                        else if (driverVal < driven.Min2)
                            drivenNorm = (driven.Min2 - driverVal) / (driven.Min2 - driven.Max2);
                        else
                            drivenNorm = 0f;
                    }

                    drivenNorm = Math.Max(0f, Math.Min(1f, drivenNorm));
                    result[driven.ParamID] =
                        drivenVp.MinValue + drivenNorm * (drivenVp.MaxValue - drivenVp.MinValue);
                }
            }

            return result;
        }

        /// <summary>
        /// Applies a genepool archetype to the currently loaded wearables by writing each
        /// archetype param into whichever wearable asset owns that param ID, then triggers
        /// a full rebake. Wearable assets must already be downloaded; call
        /// <see cref="RequestSetAppearance"/> after wearing a new outfit before calling this.
        /// </summary>
        public Task ApplyArchetype(GenepoolArchetype archetype)
        {
            lock (Wearables)
            {
                foreach (var wearableList in Wearables.Values)
                {
                    foreach (var wearable in wearableList)
                    {
                        if (wearable.Asset == null) continue;
                        foreach (var param in archetype.Params)
                        {
                            if (wearable.Asset.Params.ContainsKey(param.Id))
                                wearable.Asset.Params[param.Id] = param.Value;
                        }
                    }
                }
            }
            return RequestSetAppearance(true);
        }

        /// <summary>
        /// Blends the currently loaded wearable visual params toward those of <paramref name="archetype"/>
        /// by weight <paramref name="t"/> (0 = no change, 1 = full archetype values), then triggers a rebake.
        /// This mirrors <c>LLVOAvatar::randomizeEverything()</c> in the SL viewer, which interpolates
        /// between two archetypes rather than snapping to one.
        /// </summary>
        public Task BlendToArchetype(GenepoolArchetype archetype, float t)
        {
            if (t <= 0f) return Task.CompletedTask;
            t = Math.Min(t, 1f);

            lock (Wearables)
            {
                foreach (var wearableList in Wearables.Values)
                {
                    foreach (var wearable in wearableList)
                    {
                        if (wearable.Asset == null) continue;
                        foreach (var param in archetype.Params)
                        {
                            if (!wearable.Asset.Params.TryGetValue(param.Id, out var current)) continue;
                            wearable.Asset.Params[param.Id] = current + t * (param.Value - current);
                        }
                    }
                }
            }
            return RequestSetAppearance(true);
        }

        /// <summary>
        /// Picks a random archetype from <see cref="Genepool.Archetypes"/> and applies it.
        /// </summary>
        public Task RandomizeAppearance(Random? rng = null)
        {
            rng ??= new Random();
            return ApplyArchetype(Genepool.Archetypes[rng.Next(Genepool.Archetypes.Length)]);
        }

        #endregion Properties

        #region Private Members

        /// <summary>A cache of wearables currently being worn</summary>
        private MultiValueDictionary<WearableType, WearableData> Wearables = new MultiValueDictionary<WearableType, WearableData>();
        /// <summary>A cache of attachments currently being worn</summary>
        private readonly ConcurrentDictionary<UUID, AttachmentPoint> Attachments = new ConcurrentDictionary<UUID, AttachmentPoint>();
        /// <summary>A cache of textures currently being worn</summary>
        private TextureData[] Textures = new TextureData[(int)AvatarTextureIndex.NumberOfEntries];
        /// <summary>Incrementing serial number for AgentCachedTexture packets</summary>
        private int CacheCheckSerialNum = -1;
        /// <summary>Incrementing serial number for AgentSetAppearance packets</summary>
        private int SetAppearanceSerialNum = 0;
        /// <summary>Reference to our agent</summary>
        private readonly GridClient Client;
        /// <summary>
        /// Timer used for delaying rebake on changing outfit
        /// </summary>
        private CancellationTokenSource? RebakeScheduleCts;
        private Task? RebakeScheduleTask;
        /// <summary>
        /// Task tracking the async appearance workflow started by RequestSetAppearance
        /// </summary>
        private Task? AppearanceTask;
        /// <summary>
        /// Main appearance cancellation token source
        /// </summary>
        private CancellationTokenSource? AppearanceCts;
        /// <summary>
        /// Is server baking complete. It needs doing only once
        /// </summary>
        private bool ServerBakingDone = false;
        /// <summary>
        /// Cached UUID of the Current Outfit Folder. Stored as a boxed UUID so that reads and
        /// writes from different threads (appearance task vs Network_OnSimChanged) are atomic —
        /// object-reference reads/writes are guaranteed atomic by the CLI spec.
        /// null means no valid cache entry.
        /// </summary>
        private volatile object? _cachedCofUUID;
        /// <summary>
        /// COF version that was sent in the last UpdateAvatarAppearance request.
        /// Mirrors SL viewer's mLastUpdateRequestCOFVersion. -1 = never requested.
        /// </summary>
        private int _lastUpdateRequestCOFVersion = -1;
        /// <summary>
        /// COF version of the last successfully received server-bake result.
        /// Mirrors SL viewer's mLastUpdateReceivedCOFVersion. -1 = never received.
        /// </summary>
        private int _lastUpdateReceivedCOFVersion = -1;
        /// <summary>
        /// COF version for which we last called SendOutfitToCurrentSimulatorAsync.
        /// Reset to -1 on every sim change so the first send to a new region always proceeds.
        /// Prevents racing appearance passes from re-rezzing attachments redundantly.
        /// </summary>
        private int _lastSentOutfitCOFVersion = -1;
        /// <summary>
        /// Public read access for <see cref="_lastUpdateReceivedCOFVersion"/>.
        /// Used by AvatarManager to apply the stale-version guard for self-appearance packets.
        /// </summary>
        public int LastUpdateReceivedCOFVersion => _lastUpdateReceivedCOFVersion;

        /// <summary>
        /// Called by AvatarManager when a UDP AvatarAppearance for our own avatar passes the
        /// stale-version guard. Updates <see cref="_lastUpdateReceivedCOFVersion"/> so that the
        /// SSB retry loop's guard reflects the version the sim has acknowledged, matching SL
        /// viewer's <c>mLastUpdateReceivedCOFVersion = thisAppearanceVersion</c> assignment in
        /// <c>processAvatarAppearance</c>.
        /// </summary>
        public void UpdateLastReceivedCOFVersion(int cofVersion)
        {
            if (cofVersion > _lastUpdateReceivedCOFVersion)
                _lastUpdateReceivedCOFVersion = cofVersion;
        }

        // Lock to guard creation/cancellation/cleanup of appearance workflow state
        private readonly object _appearanceLock = new object();

        private bool _disposed = false;

        private static readonly ParallelOptions _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = MAX_CONCURRENT_DOWNLOADS
        };

        // Centralized helper to cancel a CancellationTokenSource, wait for a Task to complete
        // (best-effort) and dispose the CTS. Keeps cancellation handling consistent.
        private void CancelAndAwaitTask(ref CancellationTokenSource? cts, ref Task? task, int timeoutMs = 5000)
        {
            if (cts == null && task == null) return;

            try
            {
                try { cts?.Cancel(); } catch (Exception ex) { Logger.Debug($"Cancel CTS failed: {ex}", Client); }

                if (task != null)
                {
                    try
                    {
                        // Best-effort wait for task to finish so we don't leak work in background
                        if (!task.Wait(timeoutMs))
                        {
                            Logger.Debug($"Task did not complete within {timeoutMs}ms", Client);
                        }
                    }
                    catch (AggregateException ae)
                    {
                        Logger.Debug($"Waiting for task threw AggregateException: {ae}", Client);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"Waiting for task threw: {ex}", Client);
                    }
                }
            }
            finally
            {
                try { cts?.Dispose(); } catch (Exception ex) { Logger.Debug($"Disposing CTS failed: {ex}", Client); }
                cts = null;
                task = null;
            }
        }

        /// <summary>
        /// Dispose resources, unregister callbacks and event handlers
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) { return; }
            _disposed = true;

            if (!disposing) { return; }

            // Unregister packet callbacks safely
            DisposalHelper.SafeAction(() => Client?.Network?.UnregisterCallback(PacketType.AgentWearablesUpdate, AgentWearablesUpdateHandler), "Unregister AgentWearablesUpdate", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });
            DisposalHelper.SafeAction(() => Client?.Network?.UnregisterCallback(PacketType.AgentCachedTextureResponse, AgentCachedTextureResponseHandler), "Unregister AgentCachedTextureResponse", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });
            DisposalHelper.SafeAction(() => Client?.Network?.UnregisterCallback(PacketType.RebakeAvatarTextures, RebakeAvatarTexturesHandler), "Unregister RebakeAvatarTextures", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });

            // Unsubscribe from events
            DisposalHelper.SafeAction(() => { if (Client?.Objects != null) Client.Objects.ObjectUpdate -= Objects_AttachmentUpdate; }, "Unsubscribe Objects.ObjectUpdate", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });
            DisposalHelper.SafeAction(() => { if (Client?.Network != null) Client.Network.Disconnected -= Network_OnDisconnected; }, "Unsubscribe Network.Disconnected", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });
            DisposalHelper.SafeAction(() => { if (Client?.Network != null) Client.Network.SimChanged -= Network_OnSimChanged; }, "Unsubscribe Network.SimChanged", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });
            DisposalHelper.SafeAction(() => { if (Client?.Network?.CurrentSim?.Caps != null) Client.Network.CurrentSim.Caps.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived; }, "Unsubscribe CapabilitiesReceived", (m,e) => { if (e != null) Logger.Warn(m + ": " + e.Message, e, Client); else Logger.Warn(m, Client); });

            // Cancel and dispose running appearance workflow and rebake scheduler reliably
            CancelAndAwaitTask(ref AppearanceCts, ref AppearanceTask);
            CancelAndAwaitTask(ref RebakeScheduleCts, ref RebakeScheduleTask);

            // Clear thread references/state
            //            DisposalHelper.SafeAction(() => AppearanceThread = null, "Clear AppearanceThread", (m,e) => Logger.Debug(m, e));
        }
        #endregion Private Members

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to our agent</param>
        public AppearanceManager(GridClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));

            Client.Network?.RegisterCallback(PacketType.AgentWearablesUpdate, AgentWearablesUpdateHandler);
            Client.Network?.RegisterCallback(PacketType.AgentCachedTextureResponse, AgentCachedTextureResponseHandler);
            Client.Network?.RegisterCallback(PacketType.RebakeAvatarTextures, RebakeAvatarTexturesHandler);

            if (Client.Objects != null) Client.Objects.ObjectUpdate += Objects_AttachmentUpdate;
            Client.Network?.Disconnected += Network_OnDisconnected;
            Client.Network?.SimChanged += Network_OnSimChanged;

            // Initialize TextureData instances for the Textures array now that TextureData is a class
            for (var i = 0; i < Textures.Length; i++)
            {
                Textures[i] = new TextureData();
            }

            TextureProvider = new GridClientBakingTextureProvider(client);
        }

#region Publics Methods

        /// <summary>
        /// Starts the appearance setting thread
        /// </summary>
        /// <param name="forceRebake">True to force rebaking, otherwise false</param>
        public Task RequestSetAppearance(bool forceRebake = false)
        {
            Task? previousTask = null;
            CancellationTokenSource? previousCts = null;

            lock (_appearanceLock)
            {
                previousTask = AppearanceTask;
                previousCts = AppearanceCts;
            }

            if (previousTask != null && !previousTask.IsCompleted)
            {
                try { previousCts?.Cancel(); } catch (Exception ex) { Logger.Debug($"Cancel previous appearance CTS failed: {ex}", Client); }

                // Chain start after previous task completes
                var chained = previousTask.ContinueWith(_ =>
                {
                    try { previousCts?.Dispose(); } catch (Exception ex) { Logger.Debug($"Disposing previous CTS failed: {ex}", Client); }

                    // Cancel and dispose any pending rebake timer inside the lock so that a
                    // concurrent DelayedRequestSetAppearance cannot race and leave an escaped
                    // timer that would cancel the new pipeline 5 seconds later.
                    CancellationTokenSource? rebakeCts;
                    lock (_appearanceLock)
                    {
                        rebakeCts = RebakeScheduleCts;
                        RebakeScheduleTask = null!;
                        RebakeScheduleCts = null!;
                    }
                    if (rebakeCts != null)
                    {
                        try { rebakeCts.Cancel(); } catch (Exception ex) { Logger.Debug($"RebakeScheduleCts.Cancel failed: {ex}", Client); }
                        try { rebakeCts.Dispose(); } catch (Exception ex) { Logger.Debug($"RebakeScheduleCts.Dispose failed: {ex}", Client); }
                    }

                    return StartAppearanceImmediate(forceRebake);
                }, TaskScheduler.Default).Unwrap();

                return chained;
            }

            // No previous running task - start immediately.
            // Capture and own the rebake-schedule reference inside the lock so that a
            // concurrent AgentWearablesUpdateHandler → DelayedRequestSetAppearance cannot
            // write a new RebakeScheduleCts between our read and our null-out, leaving an
            // escaped timer that would cancel the pipeline 5 seconds later.
            CancellationTokenSource? pendingRebakeCts;
            lock (_appearanceLock)
            {
                pendingRebakeCts = RebakeScheduleCts;
                RebakeScheduleTask = null!;
                RebakeScheduleCts = null!;
            }
            if (pendingRebakeCts != null)
            {
                try { pendingRebakeCts.Cancel(); } catch (Exception ex) { Logger.Debug($"RebakeScheduleCts.Cancel failed: {ex}", Client); }
                try { pendingRebakeCts.Dispose(); } catch (Exception ex) { Logger.Debug($"RebakeScheduleCts.Dispose failed: {ex}", Client); }
            }

            return StartAppearanceImmediate(forceRebake);
        }

        // Starts the appearance workflow immediately and returns the appearance Task
        private Task StartAppearanceImmediate(bool forceRebake)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            lock (_appearanceLock)
            {
                AppearanceCts = cts;
                AppearanceTask = Task.Run(() => RequestSetAppearanceAsync(forceRebake, ct), ct);
                // Observe exceptions from the background appearance task to avoid unobserved exceptions
                AppearanceTask.ContinueWith(t =>
                {
                    var ex = t.Exception; // Observe
                    Logger.Warn($"AppearanceTask faulted: {ex}", Client);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }

            return AppearanceTask;
        }

        /// <summary>
        /// Check if current region supports server side baking
        /// </summary>
        /// <returns>True if server side baking support is detected</returns>
        public bool ServerBakingRegion()
        {
            return Client.Network.CurrentSim != null &&
                ((Client.Network.CurrentSim.Protocols & RegionProtocols.AgentAppearanceService) != 0);
        }

        /// <summary>
        /// Async-first variant returning the worn items in COF. Also populates <see cref="Wearables"/>
        /// and <see cref="Attachments"/> before firing <see cref="OnAgentWearables"/>
        /// </summary>
        public async Task<List<InventoryBase>> RequestAgentWornAsync(CancellationToken cancellationToken = default)
        {
            var cof = await GetCurrentOutfitFolderAsync(cancellationToken).ConfigureAwait(false);
            if (cof == null)
            {
                Logger.Warn("Could not retrieve 'Current Outfit' folder", Client);
                return new List<InventoryBase>();
            }

            List<InventoryBase> contents;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(1));
                contents = await Client.Inventory.FolderContentsAsync(cof.UUID, cof.OwnerID, true, true,
                    InventorySortOrder.ByDate, cts.Token, true).ConfigureAwait(false);
            }

            if (contents == null)
            {
                Logger.Warn("Could not retrieve 'Current Outfit' folder contents", Client);
                return new List<InventoryBase>();
            }

            var wearables = new MultiValueDictionary<WearableType, WearableData>();
            // Clear stale attachment entries from any previous outfit before rebuilding.
            // SL viewer always rebuilds from scratch on re-dress.
            Attachments.Clear();
            foreach (var item in contents)
            {
                switch (item)
                {
                    case InventoryWearable wearable:
                    {
                        var w = wearable;
                        if (wearable.IsLink() && Client.Inventory?.Store != null && Client.Inventory.Store.Contains(wearable.ResolvedItemID))
                        {
                            w = Client.Inventory.Store[wearable.ResolvedItemID] as InventoryWearable;
                        }
                        if (w != null && !w.IsLink())
                        {
                            wearables.Add(w.WearableType, new WearableData()
                            {
                                ItemID = w.UUID,
                                AssetID = w.AssetUUID,
                                AssetType = w.AssetType,
                                WearableType = w.WearableType
                            });
                        }
                        break;
                    }
                    case InventoryAttachment attachment:
                    {
                        var a = attachment;
                        if (attachment.IsLink() && Client.Inventory?.Store != null && Client.Inventory.Store.Contains(attachment.ResolvedItemID))
                        {
                            a = Client.Inventory.Store[attachment.ResolvedItemID] as InventoryAttachment;
                        }
                        if (a != null)
                        {
                            Attachments.AddOrUpdate(a.ResolvedItemID, a.AttachmentPoint, (id, point) => a.AttachmentPoint);
                        }
                        break;
                    }
                    case InventoryObject attachedObject:
                    {
                        var a = attachedObject;
                        if (attachedObject.IsLink() && Client.Inventory?.Store != null && Client.Inventory.Store.Contains(attachedObject.ResolvedItemID))
                        {
                            a = Client.Inventory.Store[attachedObject.ResolvedItemID] as InventoryObject;
                        }
                        if (a != null)
                        {
                            Attachments.AddOrUpdate(a.ResolvedItemID, a.AttachPoint, (id, point) => a.AttachPoint);
                        }
                        break;
                    }
                    case InventoryItem misclassifiedLink when misclassifiedLink.IsLink()
                                                             && Client.Inventory?.Store != null
                                                             && Client.Inventory.Store.Contains(misclassifiedLink.ResolvedItemID):
                    {
                        // Old in-world attachments are stored with inv_type=0 (Texture). The link
                        // inherits this wrong type so FromOSD emits InventoryTexture, bypassing the
                        // typed cases above. Resolve the target and handle it by its real type.
                        var resolved = Client.Inventory.Store[misclassifiedLink.ResolvedItemID];
                        switch (resolved)
                        {
                            case InventoryWearable w when !w.IsLink():
                                wearables.Add(w.WearableType, new WearableData
                                {
                                    ItemID = w.UUID,
                                    AssetID = w.AssetUUID,
                                    AssetType = w.AssetType,
                                    WearableType = w.WearableType
                                });
                                break;
                            case InventoryAttachment ra:
                                Attachments.AddOrUpdate(ra.ResolvedItemID, ra.AttachmentPoint, (_, _) => ra.AttachmentPoint);
                                break;
                            case InventoryObject ro:
                                Attachments.AddOrUpdate(ro.ResolvedItemID, ro.AttachPoint, (_, _) => ro.AttachPoint);
                                break;
                        }
                        break;
                    }
                }
            }
            // Only replace if COF yielded at least one wearable — an empty result means the
            // fetch failed (missing COF, LLSD parse error, etc.) and we must not discard
            // any wearables that were already populated (e.g., from LLUDP AgentWearablesUpdate).
            if (wearables.Any())
                lock (Wearables) { Wearables = wearables; }

            OnAgentWearables(new AgentWearablesReplyEventArgs());
            return contents;
        }

        /// <summary>
        /// Build hashes out of the texture assetIDs for each baking layer to
        /// ask the simulator whether it has cached copies of each baked texture
        /// </summary>
        public void RequestCachedBakes()
        {
            var hashes = new List<AgentCachedTexturePacket.WearableDataBlock>();

            // Build hashes for each of the bake layers from the individual components
            lock (Wearables)
            {
                for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
                {
                    // Don't do a cache request for a skirt bake if we're not wearing a skirt
                    if (bakedIndex == (int)BakeType.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                        continue;

                    // Build a hash of all the texture asset IDs in this baking layer
                    var hash = UUID.Zero;
                    for (var wearableIndex = 0; wearableIndex < WEARABLES_PER_LAYER; wearableIndex++)
                    {
                        var type = WEARABLE_BAKE_MAP[bakedIndex][wearableIndex];

                        if (type == WearableType.Invalid) continue;
                        hash = Wearables.Where(e => e.Key == type)
                            .SelectMany(e => e.Value).Aggregate(hash, (current, worn) => current ^ worn.AssetID);
                    }

                    if (hash != UUID.Zero)
                    {
                        // Hash with our secret value for this baked layer
                        hash ^= BAKED_TEXTURE_HASH[bakedIndex];

                        // Add this to the list of hashes to send out
                        var block =
                            new AgentCachedTexturePacket.WearableDataBlock
                            {
                                ID = hash,
                                TextureIndex = (byte)bakedIndex
                            };
                        hashes.Add(block);

                        Logger.DebugLog($"Checking cache for {(BakeType)block.TextureIndex}, hash={block.ID}", Client);
                    }
                }
            }

            // Only send the packet out if there's something to check
            if (hashes.Count <= 0) return;
            var cache = new AgentCachedTexturePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    SerialNum = Interlocked.Increment(ref CacheCheckSerialNum)
                },
                WearableData = hashes.ToArray()
            };


            Client.Network.SendPacket(cache);
        }

        public IEnumerable<UUID> GetWearableAssets(WearableType type)
        {
            return Wearables.Where(e => e.Key == type)
                .SelectMany(e => e.Value)
                .Select(wearable => wearable.AssetID);
        }

        /// <summary>
        /// Add a wearable to the current outfit and set appearance
        /// </summary>
        /// <param name="wearableItem">Wearable to be added to the outfit</param>
        /// <param name="replace">Should existing item on the same point or of the same type be replaced</param>
        public void AddToOutfit(InventoryItem wearableItem, bool replace = true)
        {
            var wearableItems = new List<InventoryItem> { wearableItem };
            AddToOutfit(wearableItems, replace);
        }

        /// <summary>
        /// Add a list of wearables to the current outfit and set appearance
        /// </summary>
        /// <param name="wearableItems">List of wearable inventory items to
        /// be added to the outfit</param>
        /// <param name="replace">Should existing item on the same point or of the same type be replaced</param>
        public void AddToOutfit(List<InventoryItem> wearableItems, bool replace = true)
        {
            var wearables = wearableItems.OfType<InventoryWearable>().ToList();
            var attachments = wearableItems.Where(item => item is InventoryAttachment || item is InventoryObject).ToList();

            lock (Wearables)
            {
                // Add the given wearables to the wearables collection
                foreach (var wearableItem in wearables)
                {
                    var wd = new WearableData
                    {
                        AssetID = wearableItem.AssetUUID,
                        AssetType = wearableItem.AssetType,
                        ItemID = wearableItem.UUID,
                        WearableType = wearableItem.WearableType
                    };
                    
                    // Bodyparts (Shape, Skin, Eyes, Hair) and Physics must always replace - they cannot be layered
                    if (replace || wearableItem.AssetType == AssetType.Bodypart || wearableItem.WearableType == WearableType.Physics)
                    {
                        // Dump everything from the key
                        Wearables.Remove(wearableItem.WearableType);
                    }
                    Wearables.Add(wearableItem.WearableType, wd);
                }
            }

            if (attachments.Any())
            {
                AddAttachments(attachments.ToList(), false, replace);
            }

            if (wearables.Any())
            {
                SendAgentIsNowWearing();
                DelayedRequestSetAppearance();
            }
        }

        /// <summary>
        /// Remove a wearable from the current outfit and set appearance
        /// </summary>
        /// <param name="wearableItem">Wearable to be removed from the outfit</param>
        public void RemoveFromOutfit(InventoryItem wearableItem)
        {
            var wearableItems = new List<InventoryItem> { wearableItem };
            RemoveFromOutfit(wearableItems);
        }


        /// <summary>
        /// Removes a list of wearables from the current outfit and set appearance
        /// </summary>
        /// <param name="wearableItems">List of wearable inventory items to
        /// be removed from the outfit</param>
        public void RemoveFromOutfit(List<InventoryItem> wearableItems)
        {
            var wearables = wearableItems.OfType<InventoryWearable>()
                .ToList();
            var attachments = wearableItems.Where(item => item is InventoryAttachment || item is InventoryObject)
                .ToList();

            var needSetAppearance = false;
            lock (Wearables)
            {
                // Remove the given wearables from the wearables collection
                foreach (var wearable in wearables)
                {
                    if (wearable.AssetType != AssetType.Bodypart        // Remove if it's not a body part
                        && Wearables.ContainsKey(wearable.WearableType)) // And we have that wearable type
                    {
                        var worn = Wearables.Where(e => e.Key == wearable.WearableType)
                            .SelectMany(e => e.Value);
                        var wearableData = worn.FirstOrDefault(item => item.ItemID == wearable.UUID);
                        if (wearableData == null) continue;

                        Wearables.Remove(wearable.WearableType, wearableData);
                        needSetAppearance = true;
                    }
                }
            }

            foreach (var attachment in attachments)
            {
                Detach(attachment.ResolvedItemID);
            }

            if (needSetAppearance)
            {
                SendAgentIsNowWearing();
                DelayedRequestSetAppearance();
            }
        }

        /// <summary>
        /// Replace the current outfit with a list of wearables and set appearance
        /// </summary>
        /// <param name="wearableItems">List of wearable inventory items that
        /// define a new outfit</param>
        public Task ReplaceOutfitAsync(List<InventoryItem> wearableItems)
        {
            return ReplaceOutfitAsync(wearableItems, true);
        }

        /// <summary>
        /// Replace the current outfit with a list of wearables and set appearance
        /// </summary>
        /// <param name="wearableItems">List of wearable inventory items that
        /// define a new outfit</param>
        /// <param name="safe">Check if we have all body parts, set this to false only
        /// if you know what you're doing</param>
        public async Task ReplaceOutfitAsync(List<InventoryItem> wearableItems, bool safe)
        {
            var wearables = wearableItems.OfType<InventoryWearable>().ToList();
            var attachments = wearableItems.Where(item => item is InventoryAttachment || item is InventoryObject).ToList();

            if (safe)
            {
                // If we don't already have the current agent wearables downloaded, updating to a
                // new set of wearables that doesn't have all bodyparts can leave the avatar
                // in an inconsistent state. If any bodypart entries are empty, we need to fetch the
                // current wearables first
                var needsCurrentWearables = false;
                lock (Wearables)
                {
                    for (var i = 0; i < WEARABLE_COUNT; i++)
                    {
                        var wearableType = (WearableType)i;
                        if (WearableTypeToAssetType(wearableType) == AssetType.Bodypart
                            && !Wearables.ContainsKey(wearableType))
                        {
                            needsCurrentWearables = true;
                            break;
                        }
                    }
                }

                if (needsCurrentWearables && !await GatherAgentWearablesAsync().ConfigureAwait(false))
                {
                    Logger.Error("Failed to fetch the current agent wearables, cannot safely replace outfit");
                    return;
                }
            }

            // Replace our local Wearables collection, send the packet(s) to update our
            // attachments, tell sim what we are wearing now, and start the baking process
            if (!safe)
            {
                SetAppearanceSerialNum++;
            }

            try
            {
                ReplaceOutfit(wearables);
                AddAttachments(attachments, true, false);
                SendAgentIsNowWearing();
                DelayedRequestSetAppearance();
            }
            catch (AppearanceManagerException e)
            {
                Logger.Error(e.Message, Client);
            }
        }

        /// <summary>
        /// Checks if an inventory item is currently being worn
        /// </summary>
        /// <param name="item">The <see cref="InventoryItem"/> to check against the agent wearables</param>
        /// <returns>The <see cref="WearableType"/> slot that the item is being worn in,
        /// or <see cref="WearableType.Invalid"/> if it is not currently being worn</returns>
        public WearableType IsItemWorn(InventoryItem item)
        {
            lock (Wearables)
            {
                foreach (var wearableType in Wearables)
                {
                    if (wearableType.Value.Any(wearable => wearable.ItemID == item.UUID))
                    {
                        return wearableType.Key;
                    }
                }
            }
            return WearableType.Invalid;
        }

        /// <summary>
        /// Checks if an inventory item is currently being worn
        /// </summary>
        /// <param name="itemId"><see cref="UUID"/> if <see cref="InventoryItem"/> to check</param>
        /// <returns>True if worn</returns>
        public bool IsItemWorn(UUID itemId)
        {
            lock (Wearables)
            {
                if (Wearables.Any(wearableType => wearableType.Value
                        .Any(wearable => wearable.ItemID == itemId)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a collection of the agents currently worn wearables
        /// </summary>
        /// <returns>A copy of the agents currently worn wearables</returns>
        /// <remarks>Avoid calling this function multiple times as it will make
        /// a copy of all wearable data each time</remarks>
        public IEnumerable<WearableData> GetWearables()
        {
            lock (Wearables)
            {
                // ToList will copy the IEnumerable
                return Wearables.SelectMany(e => e.Value).ToList();
            }
        }

        public MultiValueDictionary<WearableType, WearableData> GetWearablesByType()
        {
            lock (Wearables)
            {
                return new MultiValueDictionary<WearableType, WearableData>(Wearables);
            }
        }

        /// <summary>
        /// Calls either <see cref="AppearanceManager.ReplaceOutfit"/> or
        /// <see cref="AppearanceManager.AddToOutfit"/> depending on the value of
        /// replaceItems
        /// </summary>
        /// <param name="wearables">List of wearable inventory items to add
        /// to the outfit or become a new outfit</param>
        /// <param name="replaceItems">True to replace existing items with the
        /// new list of items, false to add these items to the existing outfit</param>
        public async Task WearOutfitAsync(List<InventoryBase> wearables, bool replaceItems)
        {
            var wearableItems = wearables.OfType<InventoryItem>().ToList();

            if (replaceItems)
                await ReplaceOutfitAsync(wearableItems).ConfigureAwait(false);
            else
                AddToOutfit(wearableItems);
        }

        #endregion Publics Methods

        #region Attachments

        /// <summary>
        /// Adds a list of attachments to our agent
        /// </summary>
        /// <param name="attachments">A List containing the attachments to add</param>
        /// <param name="removeExistingFirst">If true, tells simulator to remove existing attachment first</param>
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        public void AddAttachments(List<InventoryItem> attachments, bool removeExistingFirst, bool replace = true)
        {
            // Use RezMultipleAttachmentsFromInv  to clear out current attachments, and attach new ones
            var attachmentsPacket = new RezMultipleAttachmentsFromInvPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                HeaderData =
                {
                    CompoundMsgID = UUID.Random(),
                    FirstDetachAll = removeExistingFirst,
                    TotalObjects = (byte) attachments.Count
                },
                ObjectData = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock[attachments.Count]
            };

            if (removeExistingFirst)
            {
                Attachments.Clear();
            }

            for (var i = 0; i < attachments.Count; i++)
            {
                if (attachments[i] is InventoryAttachment)
                {
                    var attachment = (InventoryAttachment)attachments[i];
                    attachmentsPacket.ObjectData[i] = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                    {
                        AttachmentPt =
                            replace
                                ? (byte)attachment.AttachmentPoint
                                : (byte)(ATTACHMENT_ADD | (byte)attachment.AttachmentPoint),
                        EveryoneMask = (uint)attachment.Permissions.EveryoneMask,
                        GroupMask = (uint)attachment.Permissions.GroupMask,
                        ItemFlags = attachment.Flags,
                        ItemID = attachment.ResolvedItemID,
                        Name = Utils.StringToBytes(attachment.Name),
                        Description = Utils.StringToBytes(attachment.Description),
                        NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                        OwnerID = attachment.OwnerID
                    };

                    Attachments.AddOrUpdate(attachments[i].ResolvedItemID,
                        attachment.AttachmentPoint,
                        (id, point) => attachment.AttachmentPoint);
                }
                else if (attachments[i] is InventoryObject)
                {
                    var attachment = (InventoryObject)attachments[i];
                    attachmentsPacket.ObjectData[i] = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                    {
                        AttachmentPt = replace ? (byte)0 : ATTACHMENT_ADD,
                        EveryoneMask = (uint)attachment.Permissions.EveryoneMask,
                        GroupMask = (uint)attachment.Permissions.GroupMask,
                        ItemFlags = attachment.Flags,
                        ItemID = attachment.ResolvedItemID,
                        Name = Utils.StringToBytes(attachment.Name),
                        Description = Utils.StringToBytes(attachment.Description),
                        NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                        OwnerID = attachment.OwnerID
                    };

                    Attachments.AddOrUpdate(attachments[i].ResolvedItemID,
                        attachment.AttachPoint,
                        (id, point) => attachment.AttachPoint);
                }
                else
                {
                    Logger.Warn($"Cannot attach inventory item {attachments[i].Name}", Client);
                }
            }

            Client.Network.SendPacket(attachmentsPacket);
        }

        /// <summary>
        /// Attach an item to our agent at a specific attach point
        /// </summary>
        /// <param name="item">A <see cref="LibreMetaverse.InventoryItem"/> to attach</param>
        /// <param name="attachPoint">the <see cref="LibreMetaverse.AttachmentPoint"/> on the avatar to attach the item to</param>
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        public void Attach(InventoryItem item, AttachmentPoint attachPoint, bool replace = true)
        {
            Attach(item.ResolvedItemID, item.OwnerID, item.Name, item.Description, item.Permissions, item.Flags,
                attachPoint, replace);
        }

        /// <summary>
        /// Attach an item to our agent specifying attachment details
        /// </summary>
        /// <param name="itemID">The <see cref="LibreMetaverse.UUID"/> of the item to attach</param>
        /// <param name="ownerID">The <see cref="LibreMetaverse.UUID"/> attachments owner</param>
        /// <param name="name">The name of the attachment</param>
        /// <param name="description">The description of the attachment</param>
        /// <param name="perms">The <see cref="LibreMetaverse.Permissions"/> to apply when attached</param>
        /// <param name="itemFlags">The <see cref="LibreMetaverse.InventoryItemFlags"/> of the attachment</param>
        /// <param name="attachPoint">The <see cref="LibreMetaverse.AttachmentPoint"/> on the agent to attach the item to</param>
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        public void Attach(UUID itemID, UUID ownerID, string name, string description,
            Permissions perms, uint itemFlags, AttachmentPoint attachPoint, bool replace = true)
        {
            var attach = new RezSingleAttachmentFromInvPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    AttachmentPt = replace ? (byte) attachPoint : (byte) (ATTACHMENT_ADD | (byte) attachPoint),
                    Description = Utils.StringToBytes(description),
                    EveryoneMask = (uint) perms.EveryoneMask,
                    GroupMask = (uint) perms.GroupMask,
                    ItemFlags = itemFlags,
                    ItemID = itemID,
                    Name = Utils.StringToBytes(name),
                    NextOwnerMask = (uint) perms.NextOwnerMask,
                    OwnerID = ownerID
                }
            };

            Attachments.AddOrUpdate(itemID, attachPoint, (id, point) => attachPoint);

            Client.Network.SendPacket(attach);
        }

        /// <summary>
        /// Detach an item from our agent by <see cref="LibreMetaverse.InventoryItem"/>
        /// </summary>
        /// <param name="item"><see cref="LibreMetaverse.InventoryItem"/> to detach</param>
        public void Detach(InventoryItem item)
        {
            Detach(item.ResolvedItemID);
        }

        /// <summary>
        /// Detach an item from our agent
        /// </summary>
        /// <param name="itemID">The inventory itemID of the item to detach</param>
        public void Detach(UUID itemID)
        {
            var detach = new DetachAttachmentIntoInvPacket
            {
                ObjectData =
                {
                    AgentID = Client.Self.AgentID,
                    ItemID = itemID
                }
            };

            Attachments.TryRemove(itemID, out _);

            Client.Network.SendPacket(detach);
        }

        /// <summary>
        /// Populates currently worn attachments.
        /// </summary>
        /// <returns>True on success retrieving attachments</returns>
        private bool GatherAgentAttachments()
        {
            var sim = Client.Network.CurrentSim;
            if (sim == null) return true;

            var objectsPrimitives = sim.ObjectsPrimitives;

            // Clear stale entries from the previous sim/outfit before rebuilding from
            // live prim data.  Any concurrent Objects_AttachmentUpdate fires will
            // re-add entries via AddOrUpdate, and the scan below will also capture them.
            Attachments.Clear();

            // No primitives found — cleared above, nothing to add.
            if (objectsPrimitives.IsEmpty)
            {
                return true;
            }

            // Build a list of objects that are attached to the avatar.
            var primitives = objectsPrimitives.Where(primitive => primitive.Value.ParentID == Client.Self.LocalID)
                                              .Select(primitive => primitive.Value);

            var enumerable = primitives as Primitive[] ?? primitives.ToArray();

                foreach (var primitive in enumerable)
                {
                    // Find the inventory UUID from the primitive name-value collection.
                    if (primitive is null || primitive.NameValues is null || !primitive.NameValues.Any()) { continue; }

                var nameValue = primitive.NameValues.SingleOrDefault(item => item.Name.Equals("AttachItemID"));

                if (nameValue.Equals(default(NameValue))) { continue; }

                // Retrieve the inventory item UUID from the name values.
                var inventoryItemId = nameValue.Value?.ToString();

                // If the AttachItemID is missing or invalid, skip this primitive instead of failing the whole gather
                if (string.IsNullOrEmpty(inventoryItemId) || !UUID.TryParse(inventoryItemId!, out var itemID))
                {
                    Logger.Debug($"GatherAgentAttachments: Invalid AttachItemID '{inventoryItemId}' on primitive {primitive.LocalID}", Client);
                    continue;
                }

                // Determine the attachment point from the primitive.
                var attachmentPoint = primitive.PrimData.AttachmentPoint;

                // Add or update the attachment list.
                Attachments.AddOrUpdate(itemID, attachmentPoint, (id, point) => attachmentPoint);
            }
            return true;
        }

        public bool isItemAttached(InventoryItem item)
        {
            return isItemAttached(item.ResolvedItemID);
        }

        public bool isItemAttached(UUID key)
        {
            return Attachments.ContainsKey(key);
        }

        /// <summary>
        /// Returns a collection of the agents currently worn attachments from the cached inventory
        /// </summary>
        /// <returns>A copy of the agents currently worn attachments</returns>
        /// <remarks>Avoid calling this function multiple times as it will make
        /// a copy of all wearable data each time</remarks>
        public IEnumerable<InventoryItem> GetAttachments()
        {
            var store = Client?.Inventory?.Store;
            if (store == null) return Enumerable.Empty<InventoryItem>();

            return Attachments.Select(item => store.Contains(item.Key) ? store[item.Key] as InventoryItem : null)
                              .OfType<InventoryItem>();
        }

        public Dictionary<UUID, AttachmentPoint> GetAttachmentsByItemId()
        {
            return Attachments.ToDictionary(k => k.Key, v => v.Value);
        }

        public async Task<MultiValueDictionary<AttachmentPoint, InventoryItem>> GetAttachmentsByAttachmentPointAsync(CancellationToken cancellationToken = default)
        {
            var attachmentsByPoint = new MultiValueDictionary<AttachmentPoint, InventoryItem>();

            foreach (var item in Attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // If the item is already retrieved then speed this up.
                var store = Client?.Inventory?.Store;
                if (store != null && store.Contains(item.Key))
                {
                    var invItem = store[item.Key] as InventoryItem;
                    if (invItem != null)
                    {
                        attachmentsByPoint.Add(item.Value, invItem);
                    }
                    continue;
                }

                // Otherwise, retrieve the item off the asset server asynchronously.
                try
                {
                    var clientLocal = Client;
                    if (clientLocal?.Inventory == null) continue;

                    var inventoryItem = await clientLocal.Inventory.FetchItemHttpAsync(item.Key, clientLocal.Self.AgentID, cancellationToken).ConfigureAwait(false);
                    if (inventoryItem != null)
                    {
                        attachmentsByPoint.Add(item.Value, inventoryItem);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Propagate cancellation
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to fetch attachment {item.Key}: {ex}", Client);
                }
            }

            return attachmentsByPoint;
        }

        public Dictionary<InventoryItem, AttachmentPoint> GetAttachmentsByInventoryItem()
        {
            var result = new Dictionary<InventoryItem, AttachmentPoint>();

            foreach (var kvp in Attachments)
            {
                try
                {
                    if (Client?.Inventory?.Store != null && Client.Inventory.Store.TryGetValue(kvp.Key, out var item) && item is InventoryItem invItem)
                    {
                        result[invItem] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed to map attachment {kvp.Key} to inventory item: {ex.Message}", Client);
                }
            }

            return result;
        }

        #endregion Attachments

        #region Appearance Helpers

        /// <summary>
        /// Inform the sim which wearables are part of our current outfit
        /// </summary>
        private void SendAgentIsNowWearing()
        {
            var wearing = new AgentIsNowWearingPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                WearableData = new AgentIsNowWearingPacket.WearableDataBlock[WEARABLE_COUNT]
            };

            lock (Wearables)
            {
                for (var i = 0; i < WEARABLE_COUNT; i++)
                {
                    var type = (WearableType)i;
                    wearing.WearableData[i] = new AgentIsNowWearingPacket.WearableDataBlock
                    {
                        WearableType = (byte)i,
                        // This appears to be hacked on SL server side to support multi-layers
                        ItemID = Wearables.TryGetValue(type, out var wearable) ?
                            (wearable.First()?.ItemID ?? UUID.Zero)
                            : UUID.Zero
                    };
                }
            }

            Client.Network.SendPacket(wearing);
        }

        /// <summary>
        /// Replaces the Wearables collection with a list of new wearable items
        /// </summary>
        /// <param name="wearableItems">Wearable items to replace the Wearables collection with</param>
        private void ReplaceOutfit(List<InventoryWearable> wearableItems)
        {
            // Sanitize wearable layers - bodyparts and physics must be single-layer
            var newWearables = new MultiValueDictionary<WearableType, WearableData>();
            var singleLayerWearables = new Dictionary<WearableType, WearableData>();

            lock (Wearables)
            {
                // Preserve body parts from the previous set of wearables. They may be overwritten,
                // but cannot be missing in the new set
                foreach (var wearableType in Wearables)
                {
                    foreach (var entry in wearableType.Value)
                    {
                        if (entry.AssetType == AssetType.Bodypart)
                        {
                            singleLayerWearables[wearableType.Key] = entry;
                        }
                    }
                }

                // Add the given wearables to the new wearables collection
                foreach (var wearableItem in wearableItems)
                {
                    var wd = new WearableData
                    {
                        AssetID = wearableItem.AssetUUID,
                        AssetType = wearableItem.AssetType,
                        ItemID = wearableItem.UUID,
                        WearableType = wearableItem.WearableType
                    };
                    
                    // Bodyparts and Physics cannot be layered. Overwrite when multiple are selected.
                    if (wearableItem.AssetType == AssetType.Bodypart || wearableItem.WearableType == WearableType.Physics)
                    {
                        singleLayerWearables[wearableItem.WearableType] = wd;
                    }
                    else
                    {
                        newWearables.Add(wearableItem.WearableType, wd);
                    }
                }

                // Merge single-layer wearables (bodyparts and physics) into new wearable list
                foreach (var singleLayer in singleLayerWearables)
                {
                    newWearables.Add(singleLayer.Key, singleLayer.Value);
                }

                // Validate required body parts are present (exactly one of each)
                if (newWearables.ContainsKey(WearableType.Shape) &&
                    newWearables.ContainsKey(WearableType.Skin) &&
                    newWearables.ContainsKey(WearableType.Eyes) &&
                    newWearables.ContainsKey(WearableType.Hair))
                {
                    // Replace the Wearables collection
                    Wearables = newWearables;
                }
                else
                {
                    throw new AppearanceManagerException(
                        "Wearables collection does not contain all required body parts; appearance cannot be set");
                }
            }
        }

        /// <summary>
        /// Calculates base color/tint for a specific wearable based on its params
        /// </summary>
        /// <param name="param">All the color info gathered from wearable VisualParams
        /// passed as list of ColorParamInfo tuples</param>
        /// <returns>Base color/tint for the wearable</returns>
        public static Color4 GetColorFromParams(List<ColorParamInfo> param)
        {
            // Start off with a blank slate, black, fully transparent
            var res = new Color4(0, 0, 0, 0);

            // Apply color modification from each color parameter
            foreach (var p in param)
            {
                var n = p.VisualColorParam.Colors.Length;

                var paramColor = new Color4(0, 0, 0, 0);

                if (n == 1)
                {
                    // We got only one color in this param, use it for application
                    // to the final color
                    paramColor = p.VisualColorParam.Colors[0];
                }
                else if (n > 1)
                {
                    // We have an array of colors in this parameter
                    // First, we need to find out, based on param value
                    // between which two elements of the array our value lands

                    // Size of the step using which we iterate from Min to Max
                    var step = (p.VisualParam.MaxValue - p.VisualParam.MinValue) / ((float)n - 1);

                    // Our color should land in between colors in the array with index a and b
                    var indexa = 0;
                    var indexb = 0;

                    var i = 0;

                    for (var a = p.VisualParam.MinValue; a <= p.VisualParam.MaxValue; a += step)
                    {
                        if (a <= p.Value)
                        {
                            indexa = i;
                        }
                        else
                        {
                            break;
                        }

                        i++;
                    }

                    // Sanity check that we don't go outside bounds of the array
                    if (indexa > n - 1)
                        indexa = n - 1;

                    indexb = (indexa == n - 1) ? indexa : indexa + 1;

                    // How far is our value from Index A on the 
                    // line from Index A to Index B
                    var distance = p.Value - (p.VisualParam.MinValue + indexa * step);

                    // We are at Index A (allowing for some floating point math fuzz),
                    // use the color on that index
                    if (distance < 0.00001f || indexa == indexb)
                    {
                        paramColor = p.VisualColorParam.Colors[indexa];
                    }
                    else
                    {
                        // Not so simple as being precisely on the index eh? No problem.
                        // We take the two colors that our param value places us between
                        // and then find the value for each ARGB element that is
                        // somewhere on the line between color1 and color2 at some
                        // distance from the first color
                        var c1 = p.VisualColorParam.Colors[indexa];
                        var c2 = p.VisualColorParam.Colors[indexb];

                        // Distance is some fraction of the step, use that fraction
                        // to find the value in the range from color1 to color2
                        paramColor = Color4.Lerp(c1, c2, distance / step);
                    }

                    // Please leave this fragment even if its commented out
                    // might prove useful should ($deity forbid) there be bugs in this code
                    //string carray = "";
                    //foreach (Color c in p.VisualColorParam.Colors)
                    //{
                    //    carray += c.ToString() + " - ";
                    //}
                    //Logger.DebugLog("Calculating color for " + p.WearableType + " from " + p.VisualParam.Name + ", value is " + p.Value + " in range " + p.VisualParam.MinValue + " - " + p.VisualParam.MaxValue + " step " + step + " with " + n + " elements " + carray + " A: " + indexa + " B: " + indexb + " at distance " + distance);
                }

                // Now that we have calculated color from the scale of colors
                // that visual params provided, lets apply it to the result
                switch (p.VisualColorParam.Operation)
                {
                    case VisualColorOperation.Add:
                        res += paramColor;
                        break;
                    case VisualColorOperation.Multiply:
                        res *= paramColor;
                        break;
                    case VisualColorOperation.Blend:
                        res = Color4.Lerp(res, paramColor, p.Value);
                        break;
                }
            }

            return res;
        }

        /// <summary>
        /// Async-first method to populate the Wearables dictionary
        /// </summary>
        private async Task<bool> GatherAgentWearablesAsync(CancellationToken cancellationToken = default)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(WEARABLE_TIMEOUT);
                try
                {
                    await RequestAgentWornAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Internal 30-second timeout fired (not a caller cancellation).
                    // Return false so the caller can fall through to the LLUDP fallback
                    // instead of propagating an OCE that looks like a pipeline cancellation.
                    Logger.Info("COF wearables fetch timed out; will fall back to LLUDP AgentWearablesRequest", Client);
                    return false;
                }
                return Wearables.Any();
            }
        }

        /// <summary>
        /// Fallback: requests wearables via legacy LLUDP (AgentWearablesRequest/AgentWearablesUpdate).
        /// Used when COF-based loading returns empty — common on OpenSim grids where
        /// FetchInventoryDescendents2 returns invalid LLSD or the COF is not populated.
        /// Only called from the client-side baking path (never on SL's SSB path).
        /// </summary>
        private async Task<bool> GatherAgentWearablesViaLLUDPAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<AgentWearablesReplyEventArgs> handler = (_, _) => tcs.TrySetResult(Wearables.Any());

            AgentWearablesReply += handler;

            try
            {
                var request = new AgentWearablesRequestPacket
                {
                    AgentData = { AgentID = Client.Self.AgentID, SessionID = Client.Self.SessionID }
                };
                Client.Network.SendPacket(request);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(WEARABLE_TIMEOUT, cancellationToken))
                    .ConfigureAwait(false);

                if (completed == tcs.Task)
                    return await tcs.Task.ConfigureAwait(false);

                tcs.TrySetCanceled();
                Logger.Warn("Timed out waiting for LLUDP AgentWearablesUpdate", Client);
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                AgentWearablesReply -= handler;
            }
        }

        /// <summary>
        /// Populates textures and visual params from a decoded asset
        /// </summary>
        /// <param name="wearable">Wearable to decode</param>
        /// <param name="textures">Texture data</param>
        public static void DecodeWearableParams(WearableData wearable, ref TextureData[] textures)
        {
            var alphaMasks = new Dictionary<VisualAlphaParam, float>();
            var colorParams = new List<ColorParamInfo>();

            // Populate collection of alpha masks from visual params
            // also add color tinting information
            var assetParams = wearable.Asset?.Params;
            if (assetParams != null)
            {
                foreach (var kvp in assetParams)
                {
                if (!VisualParams.Params.TryGetValue(kvp.Key, out var p)) continue;

                var colorInfo = new ColorParamInfo
                {
                    WearableType = wearable.WearableType,
                    VisualParam = p,
                    Value = kvp.Value
                };

                // Color params
                if (p.ColorParams.HasValue)
                {
                    colorInfo.VisualColorParam = p.ColorParams.Value;

                    switch (wearable.WearableType)
                    {
                        case WearableType.Tattoo:
                            if (kvp.Key == 1062 || kvp.Key == 1063 || kvp.Key == 1064)
                            {
                                colorParams.Add(colorInfo);
                            }
                            break;
                        case WearableType.Jacket:
                            if (kvp.Key == 809 || kvp.Key == 810 || kvp.Key == 811)
                            {
                                colorParams.Add(colorInfo);
                            }
                            break;
                        case WearableType.Hair:
                            // Param 112 - Rainbow
                            // Param 113 - Red
                            // Param 114 - Blonde
                            // Param 115 - White
                            if (kvp.Key == 112 || kvp.Key == 113 || kvp.Key == 114 || kvp.Key == 115)
                            {
                                colorParams.Add(colorInfo);
                            }
                            break;
                        case WearableType.Skin:
                            // For skin we skip makeup params for now and use only the 3
                            // that are used to determine base skin tone
                            // Param 108 - Rainbow Color
                            // Param 110 - Red Skin (Ruddiness)
                            // Param 111 - Pigment
                            if (kvp.Key == 108 || kvp.Key == 110 || kvp.Key == 111)
                            {
                                colorParams.Add(colorInfo);
                            }
                            break;
                        default:
                            colorParams.Add(colorInfo);
                            break;
                    }
                }

                // Add alpha mask
                if (p.AlphaParams.HasValue && p.AlphaParams.Value.TGAFile != string.Empty && !p.IsBumpAttribute && !alphaMasks.ContainsKey(p.AlphaParams.Value))
                {
                    alphaMasks.Add(p.AlphaParams.Value, kvp.Value == 0 ? 0.01f : kvp.Value);
                }

                // Alpha masks can also be specified in sub "driver" params
                if (p.Drivers != null)
                {
                    foreach (var t in p.Drivers)
                    {
                        if (VisualParams.Params.TryGetValue(t, out var driver))
                        {
                            if (driver.AlphaParams.HasValue && driver.AlphaParams.Value.TGAFile != string.Empty && !driver.IsBumpAttribute && !alphaMasks.ContainsKey(driver.AlphaParams.Value))
                            {
                                alphaMasks.Add(driver.AlphaParams.Value, Math.Abs(kvp.Value) < float.Epsilon ? 0.01f : kvp.Value);
                            }
                        }
                    }
                }
                }
            }

            var wearableColor = Color4.White; // Never actually used
            if (colorParams.Count > 0)
            {
                wearableColor = GetColorFromParams(colorParams);
                Logger.DebugLog($"Setting tint {wearableColor} for {wearable.WearableType}");
            }

            // Loop through all the texture IDs in this decoded asset and put them in our cache of worn textures
            if (wearable.Asset != null)
            {
                foreach (var entry in wearable.Asset.Textures)
                {
                var i = (int)entry.Key;

                // Update information about color and alpha masks for this texture
                textures[i].AlphaMasks = alphaMasks;
                textures[i].Color = wearableColor;

                // If this texture changed, update the TextureID and clear out the old cached texture asset
                if (textures[i].TextureID != entry.Value)
                {
                    // Treat DEFAULT_AVATAR_TEXTURE as null
                    textures[i].TextureID = entry.Value != DEFAULT_AVATAR_TEXTURE ? entry.Value : UUID.Zero;
                    textures[i].Texture = null;
                }
            }
            }
        }
        /// <summary>
        /// Creates a dictionary of visual param values from the downloaded wearables
        /// </summary>
        /// <returns>A dictionary of visual param indices mapping to visual param
        /// values for our agent that can be fed to the Baker class</returns>
        private Dictionary<int, float> MakeParamValues()
        {
            var paramValues = new Dictionary<int, float>(VisualParams.Params.Count);

            lock (Wearables)
            {
                foreach (var kvp in VisualParams.Params)
                {
                    // Only Group-0 parameters are sent in AgentSetAppearance packets
                    if (kvp.Value.Group != 0) continue;
                    var found = false;
                    var vp = kvp.Value;

                    // Try and find this value in our collection of downloaded wearables
                    foreach (var wearableType in Wearables)
                    {
                        foreach (var data in wearableType.Value)
                        {
                            if (data.Asset != null && data.Asset.Params.TryGetValue(vp.ParamID, out var paramValue))
                            {
                                paramValues.Add(vp.ParamID, paramValue);
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }

                    // Use a default value if we don't have one set for it
                    if (!found) paramValues.Add(vp.ParamID, vp.DefaultValue);
                }
            }

            return paramValues;
        }

        private Avatar? GetOwnAvatar()
        {
            var client = Client;
            var sim = client?.Network?.CurrentSim;
            if (sim != null && client != null && sim.ObjectsAvatars.TryGetValue(client.Self.LocalID, out var av))
                return av;
            return null;
        }

        /// <summary>
        /// Initiate server baking process
        /// </summary>
        /// <returns>True if the server baking was successful</returns>
        private async Task<bool> UpdateAvatarAppearanceAsync(CancellationToken cancellationToken, int totalRetries = 3)
        {
            var cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("UpdateAvatarAppearance");
            if (cap == null)
            {
                Logger.Warn("Could not retrieve UpdateAvatarAppearance region capability", Client);
                return false;
            }

            // Fetch COF once outside the retry loop — SL viewer does getCOFVersion() which
            // is a single local store lookup; re-fetching on every iteration wastes network.
            var currentOutfitFolder = await GetCurrentOutfitFolderAsync(cancellationToken);
            if (currentOutfitFolder == null)
            {
                Logger.Warn("Could not retrieve Current Outfit folder", Client);
                return false;
            }

            // SL viewer constants for SSB retry backoff.
            // delay = pow(BAKE_RETRY_TIMEOUT=2.0, retryCount) - 1.0 seconds, max BAKE_RETRY_MAX_COUNT=5 retries.
            const int BAKE_RETRY_MAX_COUNT = 5;
            const double BAKE_RETRY_TIMEOUT_BASE = 2.0;
            var retryCount = 0;
            var bRetry = false;
            var bakeSucceeded = false;

            // Track the version we set _lastUpdateRequestCOFVersion to inside this call so we
            // can clear it on any non-success exit path (cancellation, exception, retry-exhausted,
            // plain error). Without this, a stale in-flight guard can permanently block future
            // bake attempts for the same or lower COF version.
            var requestedCofVersion = -1;

            try
            {
                do
                {
                bRetry = false;
                if (cancellationToken.IsCancellationRequested) { return false; }

                if (totalRetries < 0)
                {
                    return false;
                }

                // Re-read the version from the store on every iteration (including retries) in case
                // AIS updated it while we were waiting — mirrors SL viewer's getCOFVersion() call
                // inside the do-loop.
                var cofVersion = currentOutfitFolder.Version;
                if (Client?.Inventory?.Store != null &&
                    Client.Inventory.Store.TryGetNodeFor(currentOutfitFolder.UUID, out var cofNode) &&
                    cofNode!.Data is InventoryFolder updatedFolder)
                {
                    cofVersion = updatedFolder.Version;
                }

                if (cofVersion < 0)
                {
                    Logger.Warn($"COF version is unknown ({cofVersion}), skipping UpdateAvatarAppearance", Client);
                    return false;
                }

                // Guard: if we already received a successful bake for this (or higher) version, exit.
                // Mirrors SL viewer's "cofVersion <= lastRcv → return" guard inside the do-loop.
                if (_lastUpdateReceivedCOFVersion >= cofVersion)
                {
                    Logger.DebugLog($"COF version {cofVersion} already received (lastRcv={_lastUpdateReceivedCOFVersion}), skipping", Client);
                    return true;
                }

                // Guard: if a request for this version is already in-flight, exit.
                // Mirrors SL viewer's "lastReq >= cofVersion → return" guard inside the do-loop.
                // Return false (not true) — the prior request failed; baking is not confirmed.
                if (_lastUpdateRequestCOFVersion >= cofVersion)
                {
                    Logger.DebugLog($"COF version {cofVersion} already in-flight (lastReq={_lastUpdateRequestCOFVersion}), skipping duplicate", Client);
                    return false;
                }

                var maxWait = 1000; // About a minute. (50,000ms)

                while (maxWait-- > 0)
                {
                    if (Client?.Network?.Connected != true)
                    {
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        if (GetOwnAvatar() is null)
                        {
                            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                var http = Client?.HttpCapsClient;
                if (http == null)
                {
                    Logger.Warn("No HttpCapsClient available to UpdateAvatarAppearance", Client);
                    return false;
                }

                Logger.Info($"Requesting bake for COF version {cofVersion} (lastRcv={_lastUpdateReceivedCOFVersion}, lastReq={_lastUpdateRequestCOFVersion})", Client);
                _lastUpdateRequestCOFVersion = cofVersion;
                requestedCofVersion = cofVersion;

                var request = new OSDMap(1) { ["cof_version"] = cofVersion };

                OSD? res = null;
                try
                {
                    var (response, data) = await http.PostAsync(cap, OSDFormat.Xml, request, cancellationToken);
                    if (data != null)
                    {
                        res = OSDParser.Deserialize(data);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"UpdateAvatarAppearance failed. Server responded: {ex.Message}", Client);
                }

                if (!(res is OSDMap result))
                {
                    // Transport-level failure. The finally block clears the in-flight guard.
                    return false;
                }

                if (result.ContainsKey("success") && result["success"].AsBoolean())
                {

                    var visualParams = result["visual_params"].AsBinary();
                    var textures = (result["textures"] as OSDArray)?.Select(arrayEntry => arrayEntry.AsUUID()).ToArray();
                    var receivedCofVersion = result["cof_version"].AsInteger();

                    MyVisualParameters = visualParams;

                    if (textures != null && textures.Length > 20)
                    {
                        if ((textures[8] == UUID.Zero || textures[9] == UUID.Zero || textures[10] == UUID.Zero || textures[11] == UUID.Zero) || (textures[8] == DEFAULT_AVATAR_TEXTURE || textures[9] == DEFAULT_AVATAR_TEXTURE || textures[10] == DEFAULT_AVATAR_TEXTURE || textures[11] == DEFAULT_AVATAR_TEXTURE))
                        {
                            // This hasn't actually baked. Retry after a delay.
                            // Clear in-flight guard so the next iteration can re-send for the same
                            // (or AIS-bumped) cofVersion without tripping the lastReq >= cofVersion check.
                            _lastUpdateRequestCOFVersion = -1;
                            requestedCofVersion = -1;
                            await Task.Delay(REBAKE_DELAY, cancellationToken).ConfigureAwait(false);
                            totalRetries--;
                            bRetry = true;
                            continue;
                        }
                    }

                    try
                    {
                        var selfPrim = GetOwnAvatar();

                        if (selfPrim is null)
                        {
                            Logger.Error("Unable to find avatar to set appearance information", Client);
                        }
                        else
                        {
                            var selfAvatarTextures = new Primitive.TextureEntry(UUID.Zero);

                            if (textures != null)
                            {
                                var faceCount = Math.Min(textures.Length, Primitive.TextureEntry.MAX_FACES);
                                for (var i = 0; i < faceCount; i++)
                                {
                                    selfAvatarTextures.FaceTextures[i] = new Primitive.TextureEntryFace(null) { TextureID = textures[i] };
                                }

                                selfPrim.Textures = selfAvatarTextures;
                                MyTextures = selfAvatarTextures;
                            }

                            selfPrim.VisualParameters = visualParams;
                            selfPrim.AppearanceVersion = 1;
                            selfPrim.COFVersion = receivedCofVersion;
                            selfPrim.AppearanceFlags = 0;

                            var texEntry = selfPrim.Textures ?? new Primitive.TextureEntry(UUID.Zero);
                            var defaultTex = texEntry.DefaultTexture ?? new Primitive.TextureEntryFace(null);
                            var faceTextures = texEntry.FaceTextures.Select(ft => ft ?? defaultTex).ToArray();

                            var sim = Client?.Network?.CurrentSim;
                            if (sim == null)
                            {
                                Logger.Warn("No current simulator available to trigger avatar appearance event", Client);
                            }
                            else
                            {
                                var clientLocal = Client;
                                var appearance = new AvatarAppearanceEventArgs(sim,
                                    clientLocal?.Self.AgentID ?? UUID.Zero,
                                    false,
                                    defaultTex,
                                    faceTextures,
                                    selfPrim.VisualParameters.ToList(), 1,
                                    receivedCofVersion,
                                    AppearanceFlags.None,
                                    selfPrim.ChildCount);

                                clientLocal?.Avatars?.TriggerAvatarAppearanceMessage(appearance);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Error applying textures to avatar object: {e.Message}", Client);
                        throw;
                    }

                    // Use the monotonic update so a higher-version UDP AvatarAppearance that
                    // arrived between our POST and this response is not overwritten with an older
                    // version. SL viewer's processAvatarAppearance does the same `>=` check.
                    UpdateLastReceivedCOFVersion(receivedCofVersion);
                    bakeSucceeded = true;
                    Logger.Info("Returning appearance information from server-side bake request.", Client);
                    return true;
                }

                if (result.ContainsKey("expected"))
                {
                    // Server reports a COF version mismatch. SL viewer does NOT call IncrementCOFVersion here
                    // — it sends "avatartexturesrequest" (forces UDP resend of our own appearance so
                    // processAvatarAppearance can update mLastUpdateReceivedCOFVersion with the canonical
                    // version), then retries with exponential backoff pow(2.0, retryCount) - 1.0 seconds.
                    var serverExpected = result["expected"].AsInteger();
                    Logger.Warn($"Server expected COF version {serverExpected}, we sent {cofVersion}.", Client);

                    // Force sim to push canonical appearance back via UDP.
                    Client?.Avatars?.RequestOwnAvatarTextures();

                    // Reset in-flight guard so the retry iteration can send a fresh request
                    // (possibly with an updated cofVersion if AIS incremented it during backoff).
                    _lastUpdateRequestCOFVersion = -1;
                    requestedCofVersion = -1;

                    if (++retryCount > BAKE_RETRY_MAX_COUNT)
                    {
                        Logger.Warn("Bake retry count exceeded on COF version mismatch.", Client);
                        break;
                    }

                    var backoffSeconds = Math.Pow(BAKE_RETRY_TIMEOUT_BASE, retryCount) - 1.0;
                    Logger.Warn($"Bake retry #{retryCount} in {backoffSeconds:F1}s.", Client);
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken).ConfigureAwait(false);

                    --totalRetries;
                    bRetry = true;
                    continue;
                }

                // Any other error (no "expected" key) — SL viewer logs "No retry attempted." and breaks.
                if (result.ContainsKey("error"))
                {
                    Logger.Warn($"UpdateAvatarAppearance failed with error: '{result["error"].AsString()}'. No retry attempted.", Client);
                }
                else
                {
                    Logger.Warn($"Avatar appearance update failed on attempt {totalRetries}. No retry attempted.", Client);
                }

                break;

                } while (bRetry);

                return false;
            }
            finally
            {
                // If we exit without a confirmed bake (cancellation, exception, retry-exhausted,
                // plain error), clear the in-flight guard for the version we requested so subsequent
                // bake attempts for that version aren't permanently blocked. Only clear if our
                // request hasn't been superseded — if a concurrent caller has already moved
                // _lastUpdateRequestCOFVersion past ours, leave their value alone.
                if (!bakeSucceeded
                    && requestedCofVersion >= 0
                    && _lastUpdateRequestCOFVersion == requestedCofVersion)
                {
                    _lastUpdateRequestCOFVersion = -1;
                }
            }
        }

        /// <summary>
        /// Get the latest version of COF
        /// </summary>
        /// <returns>Current Outfit Folder (or null if getting the data failed)</returns>
        public async Task<InventoryFolder?> GetCurrentOutfitFolderAsync(CancellationToken cancellationToken = default)
        {
            var clientLocal = Client;
            if (clientLocal?.Inventory?.Store?.RootFolder == null) return null;

            // Fast path: if we already know the COF UUID, fetch it directly from the local
            // inventory store without scanning the root folder over HTTP every time.
            // _cachedCofUUID is a volatile object reference (boxed UUID) for thread safety.
            if (_cachedCofUUID is UUID cachedId && clientLocal.Inventory.Store.Contains(cachedId))
            {
                if (clientLocal.Inventory.Store[cachedId] is InventoryFolder cached
                    && cached.PreferredType == FolderType.CurrentOutfit)
                {
                    return cached;
                }
                // UUID stale (folder removed/reassigned) — fall through to full scan.
                _cachedCofUUID = null;
            }

            // Slow path: scan the root folder to locate the COF.
            var rootFolder = clientLocal.Inventory.Store.RootFolder;

            List<InventoryBase>? root = await clientLocal.Inventory.RequestFolderContentsAsync(rootFolder.UUID,
                clientLocal.Self.AgentID, true, false, InventorySortOrder.ByDate,
                cancellationToken).ConfigureAwait(false);

            if (root == null) { return null; }

            foreach (var baseItem in root)
            {
                if (baseItem is InventoryFolder folder && folder.PreferredType == FolderType.CurrentOutfit)
                {
                    _cachedCofUUID = folder.UUID; // box the UUID into the volatile object field
                    return folder;
                }
            }

            // COF does not exist — create it so appearance operations don't silently fail
            // on accounts that have never logged in with a full viewer.
            var newCofID = clientLocal.Inventory.CreateFolder(rootFolder.UUID, "Current Outfit", FolderType.CurrentOutfit);
            if (newCofID != UUID.Zero)
            {
                // Fetch the freshly-created folder from the store so we return the real object.
                if (clientLocal.Inventory.Store[newCofID] is InventoryFolder newCof)
                {
                    _cachedCofUUID = newCofID;
                    return newCof;
                }
            }
            return null;
        }

        public AgentSetAppearancePacket MakeAppearancePacket()
        {
            var set = new AgentSetAppearancePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    SerialNum = (uint) Interlocked.Increment(ref SetAppearanceSerialNum)
                }
            };

            // Visual params used in the agent height calculation
            var agentSizeVPHeight = 0.0f;
            var agentSizeVPHeelHeight = 0.0f;
            var agentSizeVPPlatformHeight = 0.0f;
            var agentSizeVPHeadSize = 0.5f;
            var agentSizeVPLegLength = 0.0f;
            var agentSizeVPNeckLength = 0.0f;
            var agentSizeVPHipLength = 0.0f;

            lock (Wearables)
            {
                #region VisualParam

                var vpIndex = 0;
                var wearingPhysics = Wearables.ContainsKey(WearableType.Physics);

                var nrParams = wearingPhysics ? 251 : 218;
                set.VisualParam = new AgentSetAppearancePacket.VisualParamBlock[nrParams];

                foreach (var kvp in VisualParams.Params)
                {
                    if (vpIndex >= nrParams) break;
                    var vp = kvp.Value;

                    var paramValue = 0f;
                    var found = Wearables.Any(wearableList => wearableList.Value.Any(wearable => wearable.Asset != null && wearable.Asset.Params.TryGetValue(vp.ParamID, out paramValue)));

                    if (!found)
                        paramValue = vp.DefaultValue;

                    set.VisualParam[vpIndex] = new AgentSetAppearancePacket.VisualParamBlock
                    {
                        ParamValue = Utils.FloatToByte(paramValue, vp.MinValue, vp.MaxValue)
                    };
                    ++vpIndex;

                    // Check if this is one of the visual params used in the agent height calculation
                    switch (vp.ParamID)
                    {
                        case 33:
                            agentSizeVPHeight = paramValue;
                            break;
                        case 198:
                            agentSizeVPHeelHeight = paramValue;
                            break;
                        case 503:
                            agentSizeVPPlatformHeight = paramValue;
                            break;
                        case 682:
                            agentSizeVPHeadSize = paramValue;
                            break;
                        case 692:
                            agentSizeVPLegLength = paramValue;
                            break;
                        case 756:
                            agentSizeVPNeckLength = paramValue;
                            break;
                        case 842:
                            agentSizeVPHipLength = paramValue;
                            break;
                    }
                }

                MyVisualParameters = new byte[set.VisualParam.Length];
                for (var i = 0; i < set.VisualParam.Length; i++)
                {
                    if (set.VisualParam[i] != null)
                    {
                        MyVisualParameters[i] = set.VisualParam[i].ParamValue;
                    }
                }

                #endregion VisualParam

                #region TextureEntry

                var te = new Primitive.TextureEntry(DEFAULT_AVATAR_TEXTURE);

                for (uint i = 0; i < Textures.Length; i++)
                {
                    var face = te.CreateFace(i);
                    if (Textures[i].TextureID != UUID.Zero)
                    {
                        face.TextureID = Textures[i].TextureID;
                        Logger.DebugLog("Sending texture entry for " + (AvatarTextureIndex)i + " to " + Textures[i].TextureID, Client);
                    }
                    else
                    {
                        Logger.DebugLog("Skipping texture entry for " + (AvatarTextureIndex)i + " its null", Client);
                    }
                }

                set.ObjectData.TextureEntry = te.GetBytes();
                MyTextures = te;

                #endregion TextureEntry

                #region WearableData

                set.WearableData = new AgentSetAppearancePacket.WearableDataBlock[BAKED_TEXTURE_COUNT];

                // Build hashes for each of the bake layers from the individual components
                for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
                {
                    var hash = UUID.Zero;
                    for (var wearableIndex = 0; wearableIndex < WEARABLES_PER_LAYER; wearableIndex++)
                    {
                        var type = WEARABLE_BAKE_MAP[bakedIndex][wearableIndex];

                        if (type == WearableType.Invalid) continue;
                        hash = Wearables.Where(e => e.Key == type)
                            .SelectMany(e => e.Value).Aggregate(hash, (current, worn) => current ^ worn.AssetID);
                    }

                    if (hash != UUID.Zero)
                    {
                        // Hash with our magic value for this baked layer
                        hash ^= BAKED_TEXTURE_HASH[bakedIndex];
                    }

                    // Tell the server what cached texture assetID to use for each bake layer
                    set.WearableData[bakedIndex] = new AgentSetAppearancePacket.WearableDataBlock
                    {
                        TextureIndex = BakeIndexToTextureIndex[bakedIndex],
                        CacheID = hash
                    };
                    Logger.DebugLog("Sending TextureIndex " + (BakeType)bakedIndex + " with CacheID " + hash, Client);
                }

                #endregion WearableData

                #region Agent Size

                // Takes into account the Shoe Heel/Platform offsets but not the HeadSize offset. Seems to work.
                const double agentSizeBase = 1.706;

                // The calculation for the HeadSize scalar may be incorrect, but it seems to work
                var agentHeight = agentSizeBase + (agentSizeVPLegLength * .1918) + (agentSizeVPHipLength * .0375) +
                                  (agentSizeVPHeight * .12022) + (agentSizeVPHeadSize * .01117) + (agentSizeVPNeckLength * .038) +
                                  (agentSizeVPHeelHeight * .08) + (agentSizeVPPlatformHeight * .07);

                set.AgentData.Size = new Vector3(0.45f, 0.6f, (float)agentHeight);

                #endregion Agent Size

                if (Client.Settings.World.TrackAvatars)
                {
                    var client = Client;
                    if (client != null)
                    {
                        var cur = client.Network.CurrentSim;
                        if (cur != null && cur.ObjectsAvatars.TryGetValue(client.Self.LocalID, out var me))
                        {
                            me.Textures = MyTextures;
                            me.VisualParameters = MyVisualParameters;
                        }
                    }
                }
            }
            return set;
        }

        /// <summary>
        /// Async-first implementation to send the Current Outfit folder to the current simulator.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        public async Task SendOutfitToCurrentSimulatorAsync(CancellationToken cancellationToken = default)
        {
            var blocks = new List<RezMultipleAttachmentsFromInvPacket.ObjectDataBlock>();

            var worn = await RequestAgentWornAsync(cancellationToken).ConfigureAwait(false);
            if (worn == null)
            {
                Logger.Warn("Could not retrieve 'Current Outfit' folder to send to simulator", Client);
                return;
            }

            Logger.Debug($"{worn.Count} inventory items in 'Current Outfit' folder", Client);

            foreach (var inventoryBase in worn.Where(inventoryBase => inventoryBase != null))
            {
                Logger.Trace($"'{inventoryBase.Name}' found in 'Current Outfit' folder ({inventoryBase.GetType().Name})", Client);

                switch (inventoryBase)
                {
                    case InventoryAttachment attachment:
                        {
                            var block = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                            {
                                AttachmentPt = (byte)(ATTACHMENT_ADD | (byte)attachment.AttachmentPoint),
                                EveryoneMask = (uint)attachment.Permissions.EveryoneMask,
                                GroupMask = (uint)attachment.Permissions.GroupMask,
                                ItemFlags = attachment.Flags,
                                ItemID = attachment.ResolvedItemID,
                                Name = Utils.StringToBytes(attachment.Name),
                                Description = Utils.StringToBytes(attachment.Description),
                                NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                                OwnerID = attachment.OwnerID
                            };

                            Logger.Trace($"Wearing attachment {attachment.UUID} ({attachment.Name})", Client);

                            blocks.Add(block);
                            break;
                        }
                    case InventoryObject attachmentIO:
                        {
                            var block = new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                            {
                                AttachmentPt = ATTACHMENT_ADD,
                                EveryoneMask = (uint)attachmentIO.Permissions.EveryoneMask,
                                GroupMask = (uint)attachmentIO.Permissions.GroupMask,
                                ItemFlags = attachmentIO.Flags,
                                ItemID = attachmentIO.ResolvedItemID,
                                Name = Utils.StringToBytes(attachmentIO.Name),
                                Description = Utils.StringToBytes(attachmentIO.Description),
                                NextOwnerMask = (uint)attachmentIO.Permissions.NextOwnerMask,
                                OwnerID = attachmentIO.OwnerID
                            };

                            Logger.Trace($"Wearing object {attachmentIO.UUID} ({attachmentIO.Name})", Client);

                            blocks.Add(block);
                            break;
                        }
                    case InventoryItem misclassifiedLink when misclassifiedLink.IsLink()
                                                             && Client.Inventory?.Store != null
                                                             && Client.Inventory.Store.Contains(misclassifiedLink.ResolvedItemID):
                        {
                            var resolved = Client.Inventory.Store[misclassifiedLink.ResolvedItemID];
                            RezMultipleAttachmentsFromInvPacket.ObjectDataBlock? block = resolved switch
                            {
                                InventoryAttachment ra => new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                                {
                                    AttachmentPt = (byte)(ATTACHMENT_ADD | (byte)ra.AttachmentPoint),
                                    EveryoneMask = (uint)ra.Permissions.EveryoneMask,
                                    GroupMask = (uint)ra.Permissions.GroupMask,
                                    ItemFlags = ra.Flags,
                                    ItemID = ra.ResolvedItemID,
                                    Name = Utils.StringToBytes(ra.Name),
                                    Description = Utils.StringToBytes(ra.Description),
                                    NextOwnerMask = (uint)ra.Permissions.NextOwnerMask,
                                    OwnerID = ra.OwnerID
                                },
                                InventoryObject ro => new RezMultipleAttachmentsFromInvPacket.ObjectDataBlock
                                {
                                    AttachmentPt = ATTACHMENT_ADD,
                                    EveryoneMask = (uint)ro.Permissions.EveryoneMask,
                                    GroupMask = (uint)ro.Permissions.GroupMask,
                                    ItemFlags = ro.Flags,
                                    ItemID = ro.ResolvedItemID,
                                    Name = Utils.StringToBytes(ro.Name),
                                    Description = Utils.StringToBytes(ro.Description),
                                    NextOwnerMask = (uint)ro.Permissions.NextOwnerMask,
                                    OwnerID = ro.OwnerID
                                },
                                _ => null
                            };
                            if (block != null)
                            {
                                Logger.Debug($"Wearing misclassified link target {resolved.Name} ({resolved.UUID})", Client);
                                blocks.Add(block);
                            }
                            break;
                        }
                }
            }

            var attachmentsPacket = new RezMultipleAttachmentsFromInvPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                HeaderData =
                {
                    CompoundMsgID = UUID.Random(),
                    FirstDetachAll = true,
                    TotalObjects = (byte)blocks.Count
                },
                ObjectData = blocks.ToArray()
            };

            Client.Network.SendPacket(attachmentsPacket);
        }

        /// <summary>
        /// Create an AgentSetAppearance packet from Wearables data and the
        /// Textures array and send it
        /// </summary>
        private void RequestAgentSetAppearance()
        {
            var set = MakeAppearancePacket();
            Client.Network.SendPacket(set);
            Logger.DebugLog("Send AgentSetAppearance packet");
        }

        private void DelayedRequestSetAppearance()
        {
            // Cancel any existing scheduled rebake
            try
            {
                var ctsSnapshot = RebakeScheduleCts;
                if (ctsSnapshot != null)
                {
                    DisposalHelper.SafeCancelAndDispose(ctsSnapshot, (m, ex) => { if (ex != null) Logger.Debug(m, ex); else Logger.Debug(m, Client); });
                }
            }
            catch (Exception ex) { if (ex != null) Logger.Debug($"Cancel previous rebake failed: {ex}", ex, Client); else Logger.Debug("Cancel previous rebake failed", Client); }

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            lock (_appearanceLock)
            {
                RebakeScheduleCts = cts;
                RebakeScheduleTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(REBAKE_DELAY, token).ConfigureAwait(false);
                        try
                        {
                            var task = RequestSetAppearance(true);
                            await task.ContinueWith(t =>
                            {
                                var ex = t.Exception; // Observe
                                if (ex != null) Logger.Warn($"Delayed RequestSetAppearance task faulted: {ex}", ex, Client);
                                else Logger.Warn($"Delayed RequestSetAppearance task faulted", Client);
                            }, TaskContinuationOptions.OnlyOnFaulted);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Delayed RequestSetAppearance invocation failed: {ex}", Client);
                        }
                    }
                    catch (OperationCanceledException) { /* cancelled */ }
                    catch (Exception ex) { Logger.Warn($"Rebake scheduler failed: {ex}", Client); }
                    finally
                    {
                        lock (_appearanceLock)
                        {
                            RebakeScheduleTask = null!;
                            try { RebakeScheduleCts?.Dispose(); } catch { }
                            RebakeScheduleCts = null!;
                        }
                    }
                }, token);
            }
        }

        /// <remarks>
        /// This method is no longer called by the appearance pipeline. The IncrementCOFVersion
        /// capability is for AIS COF mutations only; calling it on an SSB mismatch corrupts the
        /// COF version counter. Retained for potential external callers only.
        /// </remarks>
        [Obsolete("Do not call during SSB retry. IncrementCOFVersion is for AIS mutations only.")]
        private async Task SyncCofVersion(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) { return; }

            Uri? capability = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("IncrementCOFVersion");
            if (capability == null)
            {
                Logger.Warn("Region returned no IncrementCOFVersion capability", Client);
                return;
            }
            Logger.Debug("Requesting COF version be incremented by the server", Client);

            using (var request = new HttpRequestMessage(HttpMethod.Get, capability))
            {
                var http = Client?.HttpCapsClient;
                if (http == null)
                {
                    Logger.Warn("No HttpCapsClient available to increment COF version", Client);
                    return;
                }

                using (var reply = await http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!reply.IsSuccessStatusCode)
                    {
                        Logger.Warn($"Failed to increment COF version: {reply.ReasonPhrase}");
                    }
                    else
                    {
                        var data = await reply.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (OSDParser.Deserialize(data) is OSDMap map)
                        {
                            var version = map["version"].AsInteger();
                            Logger.Info($"Slamming {version} version to Current Outfit Folder", Client);
                            var cof = await GetCurrentOutfitFolderAsync(cancellationToken).ConfigureAwait(false);
                            if (cof != null)
                            {
                                cof.Version = version;
                            }
                        }
                    }
                }
            }
        }

    #endregion Appearance Helpers

    #region Inventory Helpers

    /// <summary>
    /// Async variant of GetFolderWearables that returns the results and a success flag.
    /// </summary>
    /// <param name="folder">Folder UUID to enumerate</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
    /// <returns>Tuple (success, wearables, attachments)</returns>
    public async Task<(bool Success, List<InventoryWearable> Wearables, List<InventoryItem> Attachments)> GetFolderWearablesAsync(UUID folder, CancellationToken cancellationToken = default)
    {
        var wearables = new List<InventoryWearable>();
        var attachments = new List<InventoryItem>();

            List<InventoryBase>? objects = null;
        try
        {
            objects = await Client.Inventory.FolderContentsAsync(folder, Client.Self.AgentID, false, true,
                InventorySortOrder.ByName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug($"GetFolderWearablesAsync cancelled while fetching folder {folder}", Client);
            return (false, wearables, attachments);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to download folder contents of {folder}: {ex}", Client);
            return (false, wearables, attachments);
        }

        if (objects != null)
        {
            foreach (var ib in objects)
            {
                switch (ib)
                {
                    case InventoryWearable wearable:
                        Logger.DebugLog($"Adding wearable {wearable.Name}", Client);
                        wearables.Add(wearable);
                        break;
                    case InventoryAttachment attachment:
                        Logger.DebugLog($"Adding attachment (attachment) {attachment.Name}", Client);
                        attachments.Add(attachment);
                        break;
                    case InventoryObject inventoryObject:
                        Logger.DebugLog($"Adding attachment (object) {inventoryObject.Name}", Client);
                        attachments.Add(inventoryObject);
                        break;
                    default:
                        Logger.DebugLog($"Ignoring inventory item {ib.Name}", Client);
                        break;
                }
            }

            return (true, wearables, attachments);
        }

        Logger.Error($"Failed to download folder contents of {folder}", Client);
        return (false, wearables, attachments);
    }

    #endregion Inventory Helpers

    #region Callbacks

    protected void AgentWearablesUpdateHandler(object? sender, PacketReceivedEventArgs e)
    {
        var update = (AgentWearablesUpdatePacket)e.Packet;

        // Second Life sends a dummy payload since the introduction of Server Side Baking,
        // but OpenSimulator still sends real wearable data via this packet.
        // OpenSim never sets the AgentAppearanceService (bit 0) protocol flag, so
        // ServerBakingRegion() is always false on OpenSim — the check is safe and correct.
        if (!ServerBakingRegion() && update.WearableData != null && update.WearableData.Length > 0)
        {
            // On OpenSim (or older SL grids), process the actual wearable data from the packet
            lock (Wearables)
            {
                var wearables = new MultiValueDictionary<WearableType, WearableData>();

                foreach (var block in update.WearableData)
                {
                    var wearableType = (WearableType)block.WearableType;

                    // Skip invalid wearable types or empty slots
                    if (wearableType == WearableType.Invalid || block.ItemID == UUID.Zero)
                        continue;

                    var wearableData = new WearableData
                    {
                        ItemID = block.ItemID,
                        AssetID = block.AssetID,
                        WearableType = wearableType,
                        AssetType = WearableTypeToAssetType(wearableType)
                    };

                    wearables.Add(wearableType, wearableData);

                    Logger.DebugLog($"Processing wearable from packet: {wearableType} ItemID={block.ItemID} AssetID={block.AssetID}", Client);
                }

                // Only update if we got valid data
                if (wearables.Any())
                {
                    Wearables = wearables;
                    Logger.Info($"Updated wearables from AgentWearablesUpdate packet: {wearables.Count} types", Client);
                }
            }

            // Trigger client-side baking now that wearables are populated from the server.
            // This is the primary appearance trigger for non-SSB regions (OpenSim).
            // Skip if a bake is already in-flight (e.g., the LLUDP fallback path already
            // started one) — scheduling a duplicate would cancel the running bake mid-stream.
            if (Client.Settings.Agent.SendAppearance)
            {
                Task? runningTask;
                lock (_appearanceLock) { runningTask = AppearanceTask; }
                if (runningTask == null || runningTask.IsCompleted)
                    DelayedRequestSetAppearance();
            }
        }

        // Fire the callback
        OnAgentWearables(new AgentWearablesReplyEventArgs());
    }

    protected void RebakeAvatarTexturesHandler(object? sender, PacketReceivedEventArgs e)
    {
        var rebake = (RebakeAvatarTexturesPacket)e.Packet;

        // allow the library to do the rebake
        if (Client.Settings.Agent.SendAppearance)
        {
            RequestSetAppearance(true);
        }

        OnRebakeAvatar(new RebakeAvatarTexturesEventArgs(rebake.TextureData.TextureID));
    }

    protected void AgentCachedTextureResponseHandler(object? sender, PacketReceivedEventArgs e)
    {
        var response = (AgentCachedTextureResponsePacket)e.Packet;

        foreach (var block in response.WearableData)
        {
            var bakeType = (BakeType)block.TextureIndex;
            var index = BakeTypeToAgentTextureIndex(bakeType);

            Logger.DebugLog($"Cache response for {bakeType}, TextureID={block.TextureID}", Client);

            if (block.TextureID != UUID.Zero)
            {
                // A simulator has a cache of this bake layer

                // FIXME: Use this. Right now we don't bother to check if this is a foreign host
                var host = Utils.BytesToString(block.HostName);

                Textures[(int)index].TextureID = block.TextureID;
            }
            else
            {
                // The server does not have a cache of this bake layer
                // FIXME:
            }
        }

        OnAgentCachedBakes(new AgentCachedBakesReplyEventArgs());
    }

    private void Network_OnDisconnected(object? sender, DisconnectedEventArgs e)
        {
            var rs = RebakeScheduleCts;
            if (rs != null)
            {
                try { DisposalHelper.SafeCancelAndDispose(rs, (m, ex) => { if (ex != null) Logger.Debug(m, ex); else Logger.Debug(m, Client); }); } catch { }
            }
            RebakeScheduleTask = null;
            RebakeScheduleCts = null;

            var ac = AppearanceCts;
            if (ac != null)
            {
                try { DisposalHelper.SafeCancelAndDispose(ac, (m, ex) => { if (ex != null) Logger.Debug(m, ex); else Logger.Debug(m, Client); }); } catch { }
            }
            AppearanceCts = null;

            // Clear appearance workflow state
        }

    private void Network_OnSimChanged(object? sender, SimChangedEventArgs e)
    {
        // Cancel any appearance workflow that was running for the previous sim.
        // The new sim will trigger a fresh Simulator_OnCapabilitiesReceived and
        // start a new appearance pass once capabilities are ready.
        CancellationTokenSource? oldCts;
        lock (_appearanceLock)
        {
            oldCts = AppearanceCts;
            AppearanceCts = null;
            AppearanceTask = null;

            // Force a full server bake in the new region — the previous bake result
            // belongs to the old region's appearance service.
            ServerBakingDone = false;
            // Reset COF version tracking so the new region's appearance service
            // doesn't skip a request based on a stale version from the old region.
            _lastUpdateRequestCOFVersion = -1;
            _lastUpdateReceivedCOFVersion = -1;
            // Invalidate the COF UUID cache so the next lookup re-confirms the folder
            // against the inventory store (handles unlikely server-side COF reassignment).
            _cachedCofUUID = null;
            // Reset sent-outfit tracking: the new sim has never received our attachments.
            _lastSentOutfitCOFVersion = -1;
        }

        // Cancel and dispose outside the lock so we don't hold the lock during disposal.
        if (oldCts != null)
        {
            try { oldCts.Cancel(); } catch { }
            try { oldCts.Dispose(); } catch { }
        }

        var cur = Client.Network.CurrentSim;
        if (cur?.Caps != null)
        {
            cur.Caps.CapabilitiesReceived += Simulator_OnCapabilitiesReceived;
        }
    }

    private void Objects_AttachmentUpdate(object? sender, PrimEventArgs e)
    {
        Primitive prim = e.Prim;

        if (Client.Self.LocalID == 0
            || prim.ParentID != Client.Self.LocalID
            || prim.NameValues == null
            || !prim.IsAttachment
            || !e.IsNew)
        {
            return;
        }

        // Updates Attachment points as soon as the data arrives
        for (int i = 0; i < prim.NameValues.Length; ++i)
        {
            if (prim.NameValues[i].Name != "AttachItemID")
            {
                continue;
            }

                try
                {
                    var valueStr = prim.NameValues[i].Value?.ToString();
                    if (!string.IsNullOrEmpty(valueStr) && UUID.TryParse(valueStr!, out var inventoryID))
                    {
                        var attachPoint = prim.PrimData.AttachmentPoint;
                        // Always add or update using the attachment point from the primitive
                        Attachments.AddOrUpdate(inventoryID, attachPoint, (id, old) => attachPoint);
                    }
                    else
                    {
                        Logger.Debug($"Objects_AttachmentUpdate: AttachItemID '{valueStr}' could not be parsed to UUID", Client);
                    }
                }
            catch (Exception ex)
            {
                Logger.Debug($"Objects_AttachmentUpdate: failed parsing AttachItemID: {ex}", Client);
            }

            break;
        }
    }

    private async void Simulator_OnCapabilitiesReceived(object? sender, CapabilitiesReceivedEventArgs e)
    {
        try
        {
            if (e.Simulator?.Caps != null)
                e.Simulator.Caps.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived;

            if (e.Simulator == Client.Network.CurrentSim && Client.Settings.Agent.SendAppearance)
            {
                if (ServerBakingRegion())
                {
                    // Second Life (SSB): run the server-bake + outfit-send under the same
                    // cancellable AppearanceTask machinery used by RequestSetAppearance so that
                    // a subsequent teleport can cancel and supersede this work cleanly.
                    await StartAppearanceImmediate(forceRebake: false).ConfigureAwait(false);
                }
                else
                {
                    // Non-SSB region (OpenSim): always start the client-side baking pipeline.
                    // If Wearables is already populated (intra-grid teleport or AgentWearablesUpdate
                    // arrived first), baking uses the cached data immediately.
                    // If Wearables is empty, the pipeline tries COF (fails silently on OpenSim) then
                    // falls back to sending AgentWearablesRequest via LLUDP — more reliable than waiting
                    // for the server to proactively push AgentWearablesUpdate on its own schedule.
                    await RequestSetAppearance(forceRebake: true).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Simulator_OnCapabilitiesReceived cancelled.", Client);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Simulator_OnCapabilitiesReceived failed: {ex}", Client);
        }
    }

    #endregion Callbacks

    #region Static Helpers

    /// <summary>
    /// Converts a WearableType to a bodypart or clothing WearableType
    /// </summary>
    /// <param name="type">A WearableType</param>
    /// <returns>AssetType.Bodypart or AssetType.Clothing or AssetType.Unknown</returns>
    public static AssetType WearableTypeToAssetType(WearableType type)
    {
        switch (type)
        {
            case WearableType.Shape:
            case WearableType.Skin:
            case WearableType.Hair:
            case WearableType.Eyes:
                return AssetType.Bodypart;
            case WearableType.Shirt:
            case WearableType.Pants:
            case WearableType.Shoes:
            case WearableType.Socks:
            case WearableType.Jacket:
            case WearableType.Gloves:
            case WearableType.Undershirt:
            case WearableType.Underpants:
            case WearableType.Skirt:
            case WearableType.Tattoo:
            case WearableType.Alpha:
            case WearableType.Physics:
                return AssetType.Clothing;
            default:
                return AssetType.Unknown;
        }
    }

    #endregion Static Helpers

    /// <summary>
    /// Async implementation of the appearance setting workflow.
    /// </summary>
    private async Task RequestSetAppearanceAsync(bool forceRebake, CancellationToken cancellationToken)
    {
        var success = true;
        var startSim = Client.Network.CurrentSim;
        try
        {
            if (forceRebake)
            {
                for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
                    Textures[(int)BakeTypeToAgentTextureIndex((BakeType)bakedIndex)].TextureID = UUID.Zero;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!GatherAgentAttachments())
            {
                Logger.Warn("Failed to retrieve a list of current agent attachments, appearance cannot be set", Client);
                throw new AppearanceManagerException(
                    "Failed to retrieve a list of current agent attachments, appearance cannot be set");
            }

            var useClientSideBaking = !ServerBakingRegion();

            if (!useClientSideBaking)
            {
                // Check whether the server actually provides the SSB capability.
                // Second Life always does; some OpenSim grids set the SSB protocol
                // flag but never register the capability.
                var hasSsbCap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("UpdateAvatarAppearance") != null;

                if (!hasSsbCap)
                {
                    // Region advertises SSB but doesn't provide the capability
                    // (common on OpenSim). Fall back to client-side baking.
                    Logger.Warn("Region advertises server-side baking but no UpdateAvatarAppearance capability found, falling back to client-side baking", Client);
                    useClientSideBaking = true;
                }
                else
                {
                    if (!Wearables.Any())
                    {
                        await GatherAgentWearablesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (!ServerBakingDone || forceRebake)
                    {
                        if (await UpdateAvatarAppearanceAsync(cancellationToken).ConfigureAwait(false))
                        {
                            ServerBakingDone = true;
                            // Re-rez attachments in the new region after a successful server bake.
                            // The SL viewer sends RezMultipleAttachmentsFromInv with FirstDetachAll=true
                            // after every region crossing once the bake is confirmed.
                            // Guard: two appearance passes (AgentWearablesUpdate handler and
                            // Simulator_OnCapabilitiesReceived) can race here on teleport.
                            // _lastSentOutfitCOFVersion is reset to -1 in Network_OnSimChanged so
                            // the first winner always sends; subsequent duplicates are dropped.
                            cancellationToken.ThrowIfCancellationRequested();
                            bool shouldSend;
                            lock (_appearanceLock)
                            {
                                var v = _lastUpdateReceivedCOFVersion;
                                shouldSend = v > _lastSentOutfitCOFVersion;
                                if (shouldSend) _lastSentOutfitCOFVersion = v;
                            }
                            if (shouldSend)
                                await SendOutfitToCurrentSimulatorAsync(cancellationToken).ConfigureAwait(false);
                            else
                                Logger.Debug($"Skipping duplicate outfit send (already sent for COF v{_lastSentOutfitCOFVersion})", Client);
                        }
                        else
                        {
                            success = false;
                        }
                    }
                }
            }

            if (useClientSideBaking)
            {
                if (!Wearables.Any())
                {
                    await GatherAgentWearablesAsync(cancellationToken).ConfigureAwait(false);
                }

                // COF-based fetch may return empty on OpenSim grids whose FetchInventoryDescendents2
                // cap returns invalid LLSD or whose COF isn't populated. Fall back to the LLUDP path
                // that OpenSimulator has always supported.
                if (!Wearables.Any())
                {
                    Logger.Info("COF wearables fetch returned no results; falling back to LLUDP AgentWearablesRequest", Client);
                    if (!await GatherAgentWearablesViaLLUDPAsync(cancellationToken).ConfigureAwait(false))
                    {
                        Logger.Error("Failed to retrieve wearables via any available method, appearance cannot be set", Client);
                        throw new AppearanceManagerException(
                            "Failed to retrieve a list of current agent wearables, appearance cannot be set");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                Logger.Info($"CSB: starting wearable download with {Wearables.Count} wearable type(s)", Client);
                ServerBakingDone = false;

                if (!await DownloadWearablesAsync(cancellationToken).ConfigureAwait(false))
                {
                    success = false;
                    Logger.Warn("One or more agent wearables failed to download, appearance will be incomplete", Client);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (SetAppearanceSerialNum == 0 && !forceRebake)
                {
                    if (!await GetCachedBakesAsync(cancellationToken).ConfigureAwait(false))
                    {
                        Logger.Warn("Failed to get a list of cached bakes from the simulator, appearance will be rebaked", Client);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!await CreateBakesAsync(cancellationToken).ConfigureAwait(false))
                {
                    success = false;
                    Logger.Warn("Failed to create or upload one or more bakes, appearance will be incomplete", Client);
                }

                // Do NOT check cancellationToken here. Once baking is complete the packet
                // must be sent — a superseding pipeline will re-bake with its own token.
                // Only skip if the sim has changed underneath us (teleport/region crossing),
                // in which case sending to the new sim with old bake data would be wrong.
                if (Client.Network.CurrentSim != startSim)
                {
                    Logger.Info("Sim changed during CSB pipeline; skipping AgentSetAppearance for old sim", Client);
                }
                else
                {
                    Logger.Info("Sending AgentSetAppearance (CSB)", Client);
                    RequestAgentSetAppearance();
                }
            }
        }
        catch (Exception e)
        {
            if (e is OperationCanceledException)
            {
                Logger.Debug("Setting appearance cancelled.", Client);
            }
            else
            {
                Logger.Warn($"Failed to set appearance with exception {e}", Client);
            }

            success = false;
        }
        finally
        {
            OnAppearanceSet(new AppearanceSetEventArgs(success));
        }
    }
    }

    #region AppearanceManager EventArgs Classes

    /// <summary>Contains the Event data returned from the data server from an AgentWearablesRequest</summary>
    public class AgentWearablesReplyEventArgs : EventArgs
    {
    }

    /// <summary>Contains the Event data returned from the data server from an AgentCachedTextureResponse</summary>
    public class AgentCachedBakesReplyEventArgs : EventArgs
    {
    }

    /// <summary>Contains the Event data returned from an AppearanceSetRequest</summary>
    public class AppearanceSetEventArgs : EventArgs
    {
        /// <summary>Indicates whether appearance setting was successful</summary>
        public bool Success { get; }

        /// <summary>
        /// Triggered when appearance data is sent to the sim and
        /// the main appearance thread is done.</summary>
        /// <param name="success">Indicates whether appearance setting was successful</param>
        public AppearanceSetEventArgs(bool success)
        {
            this.Success = success;
        }
    }

    /// <summary>Contains the Event data returned from the data server from an RebakeAvatarTextures</summary>
    public class RebakeAvatarTexturesEventArgs : EventArgs
    {
        /// <summary>The ID of the Texture Layer to bake</summary>
        public UUID TextureID { get; }

        /// <summary>
        /// Triggered when the simulator requests the agent rebake
        /// its appearance
        /// </summary>
        /// <param name="textureID">The ID of the Texture Layer to bake</param>
        public RebakeAvatarTexturesEventArgs(UUID textureID)
        {
            this.TextureID = textureID;
        }

    }
    #endregion
}
