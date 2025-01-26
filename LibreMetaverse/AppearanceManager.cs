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
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.Imaging;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using LibreMetaverse;

namespace OpenMetaverse
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

    [Serializable]
    public class AppearanceManagerException : Exception
    {
        public AppearanceManagerException(string message)
        : base(message) { }
    }

    public class AppearanceManager
    {
        #region Constants
        /// <summary>Mask for multiple attachments</summary>
        public static readonly byte ATTACHMENT_ADD = 0x80;
        /// <summary>Mapping between BakeType and AvatarTextureIndex</summary>
        public static readonly byte[] BakeIndexToTextureIndex = new byte[BAKED_TEXTURE_COUNT] { 8, 9, 10, 11, 19, 20 };
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
        /// <summary>Total number of baked textures on each avatar</summary>
        public const int BAKED_TEXTURE_COUNT = 6;
        /// <summary>Total number of wearables per bake layer</summary>
        public const int WEARABLES_PER_LAYER = 9;
        /// <summary>Map of what wearables are included in each bake</summary>
        public static readonly WearableType[][] WEARABLE_BAKE_MAP = {
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Hair,    WearableType.Alpha,   WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Shirt,   WearableType.Jacket,  WearableType.Gloves,  WearableType.Undershirt, WearableType.Alpha,        WearableType.Invalid },
            new[] { WearableType.Shape, WearableType.Skin,    WearableType.Tattoo,  WearableType.Pants,   WearableType.Shoes,   WearableType.Socks,   WearableType.Jacket,     WearableType.Underpants,   WearableType.Alpha   },
            new[] { WearableType.Eyes,  WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Skirt, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid },
            new[] { WearableType.Hair,  WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid, WearableType.Invalid,    WearableType.Invalid,      WearableType.Invalid }
        };
        /// <summary>Magic values to finalize the cache check hashes for each
        /// bake</summary>
        public static readonly UUID[] BAKED_TEXTURE_HASH = {
            new UUID("18ded8d6-bcfc-e415-8539-944c0f5ea7a6"),
            new UUID("338c29e3-3024-4dbb-998d-7c04cf4fa88f"),
            new UUID("91b4a2c7-1b1a-ba16-9a16-1f8f8dcc1c3f"),
            new UUID("b2cf28af-b840-1071-3c6a-78085d8128b5"),
            new UUID("ea800387-ea1a-14e0-56cb-24f2022f969a"),
            new UUID("0af1ef7c-ad24-11dd-8790-001f5bf833e8")
        };
        /// <summary>Default avatar texture, used to detect when a custom
        /// texture is not set for a face</summary>
        public static readonly UUID DEFAULT_AVATAR_TEXTURE = new UUID("c228d1cf-4b5d-4ba8-84f4-899a0796aa97");

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
            public AssetWearable Asset;

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
        public struct TextureData
        {
            /// <summary>A texture AssetID</summary>
            public UUID TextureID;
            /// <summary>Asset data for the texture</summary>
            public AssetTexture Texture;
            /// <summary>Collection of alpha masks that needs applying</summary>
            public Dictionary<VisualAlphaParam, float> AlphaMasks;
            /// <summary>Tint that should be applied to the texture</summary>
            public Color4 Color;
            /// <summary>Where on avatar does this texture belong</summary>
            public AvatarTextureIndex TextureIndex;

            public override string ToString()
            {
                return String.Format("TextureID: {0}, Texture: {1}",
                    TextureID, Texture != null ? Texture.AssetData.Length + " bytes" : "(null)");
            }
        }

        #endregion Structs / Classes

        #region Event delegates, Raise Events

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AgentWearablesReplyEventArgs> m_AgentWearablesReply;

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
        private EventHandler<AgentCachedBakesReplyEventArgs> m_AgentCachedBakesReply;

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
        private EventHandler<AppearanceSetEventArgs> m_AppearanceSet;

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
        private EventHandler<RebakeAvatarTexturesEventArgs> m_RebakeAvatarReply;

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
        /// Returns true if AppearanceManager is busy and trying to set or change appearance will fail
        /// </summary>
        public bool ManagerBusy => AppearanceThreadRunning != 0;

        /// <summary>Visual parameters last sent to the sim</summary>
        public byte[] MyVisualParameters;

        /// <summary>Textures about this client sent to the sim</summary>
        public Primitive.TextureEntry MyTextures;

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
        /// <summary>Indicates whether the appearance thread is currently
        /// running, to prevent multiple appearance threads from running
        /// simultaneously</summary>
        private int AppearanceThreadRunning = 0;
        /// <summary>Reference to our agent</summary>
        private readonly GridClient Client;
        /// <summary>
        /// Timer used for delaying rebake on changing outfit
        /// </summary>
        private Timer RebakeScheduleTimer;
        /// <summary>
        /// Main appearance thread
        /// </summary>
        private Thread AppearanceThread;
        /// <summary>
        /// Main appearance cancellation token source
        /// </summary>
        private CancellationTokenSource AppearanceCts;
        /// <summary>
        /// Is server baking complete. It needs doing only once
        /// </summary>
        private bool ServerBakingDone = false;

        private static readonly ParallelOptions _parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = MAX_CONCURRENT_DOWNLOADS
        };

        #endregion Private Members

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to our agent</param>
        public AppearanceManager(GridClient client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.AgentWearablesUpdate, AgentWearablesUpdateHandler);
            Client.Network.RegisterCallback(PacketType.AgentCachedTextureResponse, AgentCachedTextureResponseHandler);
            Client.Network.RegisterCallback(PacketType.RebakeAvatarTextures, RebakeAvatarTexturesHandler);

            Client.Network.Disconnected += Network_OnDisconnected;
            Client.Network.SimChanged += Network_OnSimChanged;
        }

#region Publics Methods

        /// <summary>
        /// Starts the appearance setting thread
        /// </summary>
        /// <param name="forceRebake">True to force rebaking, otherwise false</param>
        public void RequestSetAppearance(bool forceRebake = false)
        {
            if (Interlocked.CompareExchange(ref AppearanceThreadRunning, 1, 0) != 0)
            {
                Logger.Log("Appearance thread is already running, skipping", Helpers.LogLevel.Warning);
                return;
            }

            // If we have an active delayed scheduled appearance bake, we dispose of it
            if (RebakeScheduleTimer != null)
            {
                RebakeScheduleTimer.Dispose();
                RebakeScheduleTimer = null;
            }

            AppearanceCts = new CancellationTokenSource();

            // This is the first time setting appearance, run through the entire sequence
            AppearanceThread = new Thread(
                () =>
                {
                    var cancellationToken = AppearanceCts.Token;
                    var success = true;
                    try
                    {
                        if (forceRebake)
                        {
                            // Set all the baked textures to UUID.Zero to force rebaking
                            for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
                                Textures[(int)BakeTypeToAgentTextureIndex((BakeType)bakedIndex)].TextureID = UUID.Zero;
                        }

                        // FIXME: we really need to make this better...
                        cancellationToken.ThrowIfCancellationRequested();

                        // Retrieve the worn attachments.
                        if (!GatherAgentAttachments())
                        {
                            Logger.Log(
                                "Failed to retrieve a list of current agent attachments, appearance cannot be set",
                                Helpers.LogLevel.Warning,
                                Client);

                            throw new AppearanceManagerException(
                                "Failed to retrieve a list of current agent attachments, appearance cannot be set");
                        }

                        // Is this server side baking enabled sim
                        if (ServerBakingRegion())
                        {
                            if (!Wearables.Any())
                            {
                                // Fetch a list of the current agent wearables
                                GatherAgentWearables();
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            if (!ServerBakingDone || forceRebake)
                            {
                                if (UpdateAvatarAppearanceAsync(cancellationToken).Result)
                                {
                                    ServerBakingDone = true;
                                }
                                else
                                {
                                    success = false;
                                }
                            }
                        }
                        else // Classic client side baking
                        {
                            if (!Wearables.Any())
                            {
                                // Fetch a list of the current agent wearables
                                if (!GatherAgentWearables())
                                {
                                    Logger.Log("Failed to retrieve a list of current agent wearables, appearance cannot be set",
                                        Helpers.LogLevel.Error, Client);
                                    throw new AppearanceManagerException(
                                        "Failed to retrieve a list of current agent wearables, appearance cannot be set");
                                }
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            // If we get back to server side baking region re-request server bake
                            ServerBakingDone = false;

                            // Download and parse all agent wearables
                            if (!DownloadWearables())
                            {
                                success = false;
                                Logger.Log(
                                    "One or more agent wearables failed to download, appearance will be incomplete",
                                    Helpers.LogLevel.Warning, Client);
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            // If this is the first time setting appearance, and we're not forcing a rebake, check the server
                            // for cached bakes
                            if (SetAppearanceSerialNum == 0 && !forceRebake)
                            {
                                // Compute hashes for each bake layer and compare against what the simulator currently has
                                if (!GetCachedBakes())
                                {
                                    Logger.Log(
                                        "Failed to get a list of cached bakes from the simulator, appearance will be rebaked",
                                        Helpers.LogLevel.Warning, Client);
                                }
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            // Download textures, compute bakes, and upload for any cache misses
                            if (!CreateBakes())
                            {
                                success = false;
                                Logger.Log(
                                    "Failed to create or upload one or more bakes, appearance will be incomplete",
                                    Helpers.LogLevel.Warning, Client);
                            }

                            cancellationToken.ThrowIfCancellationRequested();

                            // Send the appearance packet
                            RequestAgentSetAppearance();
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is OperationCanceledException)
                        {
                            Logger.Log("Setting appearance cancelled.", Helpers.LogLevel.Debug, Client);
                        }
                        else
                        {
                            Logger.Log($"Failed to set appearance with exception {e}", Helpers.LogLevel.Warning, Client);
                        }

                        success = false;
                    }
                    finally
                    {
                        AppearanceThreadRunning = 0;

                        OnAppearanceSet(new AppearanceSetEventArgs(success));
                    }
                }
            )
            {
                Name = "Appearance",
                IsBackground = true
            };

            AppearanceThread.Start();
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
        /// Returns a List of worn items in COF. Also populates <see cref="Wearables"/>
        /// and <see cref="Attachments"/> before firing <see cref="OnAgentWearables"/>
        /// </summary>
        /// <returns><see cref="List{T}"/> of <see cref="InventoryBase"/> in COF</returns>
        public List<InventoryBase> RequestAgentWorn()
        {
            var cof = GetCurrentOutfitFolder();
            if (cof == null)
            {
                Logger.Log("Could not retrieve Current Outfit folder", Helpers.LogLevel.Warning, Client);
                return null;
            }
            // Fetch from cache...
            var contents = Client.Inventory.Store.GetContents(cof.UUID);

            // If that fails, fetch from server...
            if (contents == null || contents.Count == 0)
            {
                contents = Client.Inventory.FolderContents(cof.UUID, cof.OwnerID, true, true,
                    InventorySortOrder.ByDate, TimeSpan.FromMinutes(1), true);
            }

            var wearables = new MultiValueDictionary<WearableType, WearableData>();
            foreach (var item in contents)
            {
                switch (item)
                {
                    case InventoryWearable wearable:
                    {
                        var w = wearable;
                        if (wearable.IsLink() && Client.Inventory.Store.Contains(wearable.ActualUUID))
                        {
                            w = Client.Inventory.Store[wearable.ActualUUID] as InventoryWearable;
                        }
                        wearables.Add(w.WearableType, new WearableData()
                        {
                            ItemID = w.UUID,
                            AssetID = w.ActualUUID,
                            AssetType = w.AssetType,
                            WearableType = w.WearableType
                        });
                        break;
                    }
                    case InventoryAttachment attachment:
                    {
                        var a = attachment;
                        if (attachment.IsLink() && Client.Inventory.Store.Contains(attachment.ActualUUID))
                        {
                            a = Client.Inventory.Store[attachment.ActualUUID] as InventoryAttachment;
                        }
                        Attachments.AddOrUpdate(a.ActualUUID, a.AttachmentPoint, (id, point) => a.AttachmentPoint);
                        break;
                    }
                    case InventoryObject attachedObject:
                    {
                        var a = attachedObject;
                        if (attachedObject.IsLink() && Client.Inventory.Store.Contains(attachedObject.ActualUUID))
                        {
                            a = Client.Inventory.Store[attachedObject.ActualUUID] as InventoryObject;
                        }
                        Attachments.AddOrUpdate(a.ActualUUID, a.AttachPoint, (id, point) => a.AttachPoint);
                        break;
                    }
                }
            }
            lock (Wearables) { Wearables = wearables; }

            OnAgentWearables(new AgentWearablesReplyEventArgs());
            return contents;
        }

        /// <summary>
        /// Ask the server what textures our agent is currently wearing
        /// </summary>
        [Obsolete("Second Life sends dummy information back post SSB. Use RequestAgentWorn")]
        public void RequestAgentWearablesLLUDP()
        {
            var request = new AgentWearablesRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };

            Client.Network.SendPacket(request);
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

        /// <summary>
        /// OBSOLETE! Returns the AssetID of the first asset that is currently 
        /// being worn in a given WearableType slot
        /// </summary>
        /// <param name="type">WearableType slot to get the AssetID for</param>
        /// <returns>The UUID of the asset being worn in the given slot, or
        /// UUID.Zero if no wearable is attached to the given slot or wearables
        /// have not been downloaded yet</returns>
        [Obsolete("Returns the first asset currently being worn, prefer GetWearableAssets")]
        public UUID GetWearableAsset(WearableType type)
        {
            return Wearables.TryGetValue(type, out var wearableList)
                ? wearableList.First().AssetID
                : UUID.Zero;
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
                    if (replace || wearableItem.AssetType == AssetType.Bodypart)
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
                Detach(attachment.UUID);
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
        public void ReplaceOutfit(List<InventoryItem> wearableItems)
        {
            ReplaceOutfit(wearableItems, true);
        }

        /// <summary>
        /// Replace the current outfit with a list of wearables and set appearance
        /// </summary>
        /// <param name="wearableItems">List of wearable inventory items that
        /// define a new outfit</param>
        /// <param name="safe">Check if we have all body parts, set this to false only
        /// if you know what you're doing</param>
        public void ReplaceOutfit(List<InventoryItem> wearableItems, bool safe)
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

                if (needsCurrentWearables && !GatherAgentWearables())
                {
                    Logger.Log("Failed to fetch the current agent wearables, cannot safely replace outfit",
                        Helpers.LogLevel.Error);
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
                Logger.Log(e.Message, Helpers.LogLevel.Error, Client);
            }
        }

        /// <summary>
        /// Checks if an inventory item is currently being worn
        /// </summary>
        /// <param name="item">The inventory item to check against the agent
        /// wearables</param>
        /// <returns>The WearableType slot that the item is being worn in,
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
        public void WearOutfit(List<InventoryBase> wearables, bool replaceItems)
        {
            var wearableItems = wearables.OfType<InventoryItem>().ToList();

            if (replaceItems)
                ReplaceOutfit(wearableItems);
            else
                AddToOutfit(wearableItems);
        }

        #endregion Publics Methods

        #region Attachments

        /// <summary>
        /// Adds a list of attachments to our agent
        /// </summary>
        /// <param name="attachments">A List containing the attachments to add</param>
        /// <param name="removeExistingFirst">If true, tells simulator to remove existing attachment
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        /// first</param>
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
                        ItemID = attachment.ActualUUID,
                        Name = Utils.StringToBytes(attachment.Name),
                        Description = Utils.StringToBytes(attachment.Description),
                        NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                        OwnerID = attachment.OwnerID
                    };

                    Attachments.AddOrUpdate(attachments[i].UUID,
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
                        ItemID = attachment.ActualUUID,
                        Name = Utils.StringToBytes(attachment.Name),
                        Description = Utils.StringToBytes(attachment.Description),
                        NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                        OwnerID = attachment.OwnerID
                    };

                    Attachments.AddOrUpdate(attachments[i].UUID,
                        attachment.AttachPoint,
                        (id, point) => attachment.AttachPoint);
                }
                else
                {
                    Logger.Log($"Cannot attach inventory item {attachments[i].Name}", Helpers.LogLevel.Warning, Client);
                }
            }

            Client.Network.SendPacket(attachmentsPacket);
        }

        /// <summary>
        /// Attach an item to our agent at a specific attach point
        /// </summary>
        /// <param name="item">A <see cref="OpenMetaverse.InventoryItem"/> to attach</param>
        /// <param name="attachPoint">the <see cref="OpenMetaverse.AttachmentPoint"/> on the avatar 
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        /// to attach the item to</param>
        public void Attach(InventoryItem item, AttachmentPoint attachPoint, bool replace = true)
        {
            Attach(item.ActualUUID, item.OwnerID, item.Name, item.Description, item.Permissions, item.Flags,
                attachPoint, replace);
        }

        /// <summary>
        /// Attach an item to our agent specifying attachment details
        /// </summary>
        /// <param name="itemID">The <see cref="OpenMetaverse.UUID"/> of the item to attach</param>
        /// <param name="ownerID">The <see cref="OpenMetaverse.UUID"/> attachments owner</param>
        /// <param name="name">The name of the attachment</param>
        /// <param name="description">The description of the attachment</param>
        /// <param name="perms">The <see cref="OpenMetaverse.Permissions"/> to apply when attached</param>
        /// <param name="itemFlags">The <see cref="OpenMetaverse.InventoryItemFlags"/> of the attachment</param>
        /// <param name="attachPoint">The <see cref="OpenMetaverse.AttachmentPoint"/> on the agent
        /// <param name="replace">If true replace existing attachment on this attachment point, otherwise add to it (multi-attachments)</param>
        /// to attach the item to</param>
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
        /// Detach an item from our agent by <see cref="OpenMetaverse.InventoryItem"/>
        /// </summary>
        /// <param name="item"><see cref="OpenMetaverse.InventoryItem"/> to detach</param>
        public void Detach(InventoryItem item)
        {
            Detach(item.UUID);
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
            var objectsPrimitives = Client.Network.CurrentSim.ObjectsPrimitives;

            // No primitives found.
            if (objectsPrimitives.Count == 0)
            {
                return true;
            }

            // Build a list of objects that are attached to the avatar.
            var primitives = objectsPrimitives.Where(primitive => primitive.Value.ParentID == Client.Self.LocalID)
                                              .Select(primitive => primitive.Value);

            var enumerable = primitives as Primitive[] ?? primitives.ToArray();

            if (enumerable.Length == 0)
            {
                return true;
            }

            foreach (var primitive in enumerable)
            {
                // Find the inventory UUID from the primitive name-value collection.
                if (primitive == null) { continue; }
                if (primitive.NameValues == null || primitive.NameValues.Length == 0) { continue; }

                var nameValue = primitive.NameValues.SingleOrDefault(item => item.Name.Equals("AttachItemID"));

                if (nameValue.Equals(default(NameValue))) { continue; }

                // Retrieve the inventory item UUID from the name values.
                var inventoryItemId = (string)nameValue.Value;

                if (string.IsNullOrEmpty(inventoryItemId) ||
                    !UUID.TryParse(inventoryItemId, out var itemID))
                {
                    return false;
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
            return isItemAttached(item.ActualUUID);
        }

        public bool isItemAttached(UUID key)
        {
            return Attachments.ContainsKey(key);
        }

        /// <summary>
        /// Returns a collection of the agents currently worn attachments
        /// </summary>
        /// <returns>A copy of the agents currently worn attachments</returns>
        /// <remarks>Avoid calling this function multiple times as it will make
        /// a copy of all wearable data each time</remarks>
        public IEnumerable<InventoryItem> GetAttachments()
        {
            return Attachments.Select(item => Client.Inventory.Store[item.Key] as InventoryItem);
        }

        public Dictionary<UUID, AttachmentPoint> GetAttachmentsByItemId()
        {
            return Attachments.ToDictionary(k => k.Key, v => v.Value);
        }

        public MultiValueDictionary<AttachmentPoint, InventoryItem> GetAttachmentsByAttachmentPoint()
        {
            var attachmentsByPoint = new MultiValueDictionary<AttachmentPoint, InventoryItem>();

            foreach (var item in Attachments)
            {
                // If the item is already retrieved then speed this up.
                if (Client.Inventory.Store.Contains(item.Key))
                {
                    attachmentsByPoint.Add(item.Value, Client.Inventory.Store[item.Key] as InventoryItem);

                    continue;
                }

                // Otherwise, retrieve the item off the asset server.
                var inventoryItem = Client.Inventory.FetchItem(item.Key, Client.Self.AgentID, TimeSpan.FromSeconds(10));

                attachmentsByPoint.Add(item.Value, inventoryItem);
            }

            return attachmentsByPoint;
        }

        public Dictionary<InventoryItem, AttachmentPoint> GetAttachmentsByInventoryItem()
        {
            var attachmentsByInventoryItem = new Dictionary<InventoryItem, AttachmentPoint>();

            foreach (var item in Attachments)
            {
                // If the item is already retrieved then speed this up.
                if (Client.Inventory.Store.Contains(item.Key))
                {
                    attachmentsByInventoryItem.Add(Client.Inventory.Store[item.Key] as InventoryItem, item.Value);

                    continue;
                }

                // Otherwise, retrieve the item off the asset server.
                var inventoryItem = Client.Inventory.FetchItem(item.Key, Client.Self.AgentID, TimeSpan.FromSeconds(10));
                if (inventoryItem != null)
                {
                    attachmentsByInventoryItem.Add(inventoryItem, item.Value);
                }
            }

            return attachmentsByInventoryItem;
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
            // *TODO: This could use some love. We need to sanitize wearable layers, and this may not be
            //        the most efficient way of doing that.
            var newWearables = new MultiValueDictionary<WearableType, WearableData>();
            var bodyparts = new Dictionary<WearableType, WearableData>();

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
                            bodyparts[wearableType.Key] = entry;
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
                    // Body cannot be layered. Overwrite when multiple are selected.
                    if (wearableItem.AssetType == AssetType.Bodypart)
                    {
                        bodyparts[wearableItem.WearableType] = wd;
                    }
                    else
                    {
                        newWearables.Add(wearableItem.WearableType, wd);
                    }
                }

                // merge bodyparts into new wearable list
                foreach (var bodypart in bodyparts)
                {
                    newWearables.Add(bodypart.Key, bodypart.Value);
                }

                // heavy-handed body part sanity check
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
                    var distance = p.Value - indexa * step;

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
        /// Blocking method to populate the Wearables dictionary
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private bool GatherAgentWearables()
        {
            var wearablesEvent = new AutoResetEvent(false);
            EventHandler<AgentWearablesReplyEventArgs> WearablesCallback = ((s, e) => wearablesEvent.Set());

            AgentWearablesReply += WearablesCallback;

            RequestAgentWorn();

            var success = wearablesEvent.WaitOne(WEARABLE_TIMEOUT, false);

            AgentWearablesReply -= WearablesCallback;

            return success;
        }

        /// <summary>
        /// Blocking method to populate the Textures array with cached bakes
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private bool GetCachedBakes()
        {
            var cacheCheckEvent = new AutoResetEvent(false);
            EventHandler<AgentCachedBakesReplyEventArgs> CacheCallback = (sender, e) => cacheCheckEvent.Set();

            CachedBakesReply += CacheCallback;

            RequestCachedBakes();

            var success = cacheCheckEvent.WaitOne(WEARABLE_TIMEOUT, false);

            CachedBakesReply -= CacheCallback;

            return success;
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
            foreach (var kvp in wearable.Asset.Params)
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

            var wearableColor = Color4.White; // Never actually used
            if (colorParams.Count > 0)
            {
                wearableColor = GetColorFromParams(colorParams);
                Logger.DebugLog($"Setting tint {wearableColor} for {wearable.WearableType}");
            }

            // Loop through all the texture IDs in this decoded asset and put them in our cache of worn textures
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
                    textures[i].TextureID = entry.Value != AppearanceManager.DEFAULT_AVATAR_TEXTURE ? entry.Value : UUID.Zero;
                    textures[i].Texture = null;
                }
            }
        }

        /// <summary>
        /// Blocking method to download and parse currently worn wearable assets
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private bool DownloadWearables()
        {
            var success = true;
            // Make a copy of the wearables dictionary to enumerate over
            var wearables = new List<WearableData>(GetWearables());

            // We will refresh the textures (zero out all non bake textures)
            for (var i = 0; i < Textures.Length; i++)
            {
                var isBake = BakeIndexToTextureIndex.Any(t => t == i);
                if (!isBake)
                    Textures[i] = new TextureData();
            }

            var pendingWearables = wearables.Count;

            foreach (var wearable in wearables.Where(wearable => wearable.Asset != null))
            {
                DecodeWearableParams(wearable, ref Textures);
                --pendingWearables;
            }

            if (pendingWearables == 0)
                return true;

            Logger.DebugLog($"Downloading {pendingWearables} wearable assets");

            Parallel.ForEach(wearables, _parallelOptions,
                wearable =>
                {
                    if (wearable.Asset != null) return;
                    var downloadEvent = new AutoResetEvent(false);

                    // Fetch this wearable asset
                    Client.Assets.RequestAsset(wearable.AssetID, wearable.AssetType, true,
                        delegate (AssetDownload transfer, Asset asset)
                        {
                            if (transfer.Success && asset is AssetWearable assetWearable)
                            {
                                // Update this wearable with the freshly downloaded asset 
                                wearable.Asset = assetWearable;

                                if (wearable.Asset.Decode())
                                {
                                    DecodeWearableParams(wearable, ref Textures);
                                    Logger.DebugLog("Downloaded wearable asset " + wearable.WearableType + " with " + wearable.Asset.Params.Count +
                                                    " visual params and " + wearable.Asset.Textures.Count + " textures", Client);

                                }
                                else
                                {
                                    wearable.Asset = null;
                                    Logger.Log("Failed to decode asset:" + Environment.NewLine +
                                               Utils.BytesToString(assetWearable.AssetData), Helpers.LogLevel.Error, Client);
                                }
                            }
                            else
                            {
                                Logger.Log("Wearable " + wearable.AssetID + "(" + wearable.WearableType + ") failed to download, " +
                                           transfer.Status, Helpers.LogLevel.Warning, Client);
                            }

                            downloadEvent.Set();
                        }
                    );

                    if (!downloadEvent.WaitOne(WEARABLE_TIMEOUT, false))
                    {
                        Logger.Log("Timed out downloading wearable asset " + wearable.AssetID + " (" + wearable.WearableType + ")",
                            Helpers.LogLevel.Error, Client);
                        success = false;
                    }

                    --pendingWearables;
                }
            );

            return success;
        }

        /// <summary>
        /// Get a list of all textures that need to be downloaded for a single bake layer
        /// </summary>
        /// <param name="bakeType">Bake layer to get texture AssetIDs for</param>
        /// <returns>A list of texture AssetIDs to download</returns>
        private IEnumerable<UUID> GetTextureDownloadList(BakeType bakeType)
        {
            var indices = BakeTypeToTextures(bakeType);
            var textures = new List<UUID>();

            foreach (var textureData in from index in indices 
                     where index != AvatarTextureIndex.Skirt || Wearables.ContainsKey(WearableType.Skirt) 
                     select Textures[(int)index] into textureData 
                     where textureData.TextureID != UUID.Zero && textureData.Texture == null
                                                              && !textures.Contains(textureData.TextureID) select textureData)
            {
                textures.Add(textureData.TextureID);
            }

            return textures;
        }

        /// <summary>
        /// Blocking method to download all textures needed for baking 
        /// the given bake layers
        /// </summary>
        /// <param name="bakeLayers">A list of layers that need baking</param>
        /// <remarks>No return value is given because the baking will happen
        /// whether all textures are successfully downloaded</remarks>
        private void DownloadTextures(List<BakeType> bakeLayers)
        {
            var textureIDs = new List<UUID>();

            foreach (var uuid in from t in bakeLayers 
                     select GetTextureDownloadList(t) into layerTextureIDs 
                     from uuid in layerTextureIDs where !textureIDs.Contains(uuid) select uuid)
            {
                textureIDs.Add(uuid);
            }

            Logger.DebugLog("Downloading " + textureIDs.Count + " textures for baking");

            Parallel.ForEach(textureIDs, _parallelOptions,
                textureId =>
                {
                    try
                    {
                        var downloadEvent = new AutoResetEvent(false);

                        Client.Assets.RequestImage(textureId,
                            delegate (TextureRequestState state, AssetTexture assetTexture)
                            {
                                if (state == TextureRequestState.Finished)
                                {
                                    assetTexture.Decode();

                                    for (var i = 0; i < Textures.Length; i++)
                                    {
                                        if (Textures[i].TextureID == textureId)
                                            Textures[i].Texture = assetTexture;
                                    }
                                }
                                else
                                {
                                    Logger.Log("Texture " + textureId + " failed to download, one or more bakes will be incomplete",
                                        Helpers.LogLevel.Warning);
                                }

                                downloadEvent.Set();
                            }
                        );

                        downloadEvent.WaitOne(TEXTURE_TIMEOUT, false);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(
                            $"Download of texture {textureId} failed with exception {e}",
                            Helpers.LogLevel.Warning, Client);
                    }
                }
            );
        }

        /// <summary>
        /// Blocking method to create and upload baked textures for all missing bakes
        /// </summary>
        /// <returns>True on success, otherwise false</returns>
        private bool CreateBakes()
        {
            var success = true;
            var pendingBakes = new List<BakeType>();

            // Check each bake layer in the Textures array for missing bakes
            for (var bakedIndex = 0; bakedIndex < BAKED_TEXTURE_COUNT; bakedIndex++)
            {
                var textureIndex = BakeTypeToAgentTextureIndex((BakeType)bakedIndex);


                if (Textures[(int)textureIndex].TextureID == UUID.Zero)
                {

                    // If this is the skirt layer and we're not wearing a skirt then skip it
                    if (bakedIndex == (int)BakeType.Skirt && !Wearables.ContainsKey(WearableType.Skirt))
                    {
                        Logger.DebugLog($"texture: {textureIndex} skipping not attached");
                        continue;
                    }
                    Logger.DebugLog($"texture: {textureIndex} is needed adding to pending Bakes");
                    pendingBakes.Add((BakeType)bakedIndex);
                }
                else
                {
                    Logger.DebugLog($"texture: {textureIndex} is ready");
                }
            }

            if (pendingBakes.Any())
            {
                DownloadTextures(pendingBakes);

                Parallel.ForEach(pendingBakes, _parallelOptions,
                    bakeType =>
                    {
                        if (!CreateBake(bakeType))
                            success = false;
                    }
                );
            }

            // Free up all the textures we're holding on to
            for (var i = 0; i < Textures.Length; i++)
            {
                Textures[i].Texture = null;
            }

            return success;
        }

        /// <summary>
        /// Blocking method to create and upload a baked texture for a single 
        /// bake layer
        /// </summary>
        /// <param name="bakeType">Layer to bake</param>
        /// <returns>True on success, otherwise false</returns>
        private bool CreateBake(BakeType bakeType)
        {
            var textureIndices = BakeTypeToTextures(bakeType);
            var oven = new Baker(bakeType);

            foreach (var textureIndex in textureIndices)
            {
                var texture = Textures[(int)textureIndex];
                texture.TextureIndex = textureIndex;

                oven.AddTexture(texture);
            }

            var start = Environment.TickCount;
            oven.Bake();
            Logger.DebugLog($"Baking {bakeType} took {Environment.TickCount - start}ms");

            var newAssetID = UUID.Zero;
            var retries = UPLOAD_RETRIES;

            while (newAssetID == UUID.Zero && retries > 0)
            {
                newAssetID = UploadBake(oven.BakedTexture.AssetData);
                --retries;
            }

            var bakeIndex = (int)BakeTypeToAgentTextureIndex(bakeType);
            Logger.DebugLog($"Saving back to {(AvatarTextureIndex)bakeIndex}");

            Textures[bakeIndex].TextureID = newAssetID;

            if (newAssetID == UUID.Zero)
            {
                Logger.Log($"Failed uploading bake {bakeType}", Helpers.LogLevel.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Blocking method to upload a baked texture
        /// </summary>
        /// <param name="textureData">Five channel JPEG2000 texture data to upload</param>
        /// <returns>UUID of the newly created asset on success, otherwise UUID.Zero</returns>
        private UUID UploadBake(byte[] textureData)
        {
            var bakeID = UUID.Zero;
            var uploadEvent = new AutoResetEvent(false);

            Client.Assets.RequestUploadBakedTexture(textureData,
                delegate (UUID newAssetID)
                {
                    bakeID = newAssetID;
                    uploadEvent.Set();
                }
            );

            // FIXME: evaluate the need for timeout here, RequestUploadBakedTexture() will
            // timeout either on Client.Settings.TRANSFER_TIMEOUT or Client.Settings.CAPS_TIMEOUT
            // depending on which upload method is used.
            uploadEvent.WaitOne(UPLOAD_TIMEOUT, false);

            return bakeID;
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
                            float paramValue;
                            if (data.Asset != null && data.Asset.Params.TryGetValue(vp.ParamID, out paramValue))
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

        private Avatar GetOwnAvatar()
        {
            Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out var av);

            return av;
        }

        /// <summary>
        /// Initiate server baking process
        /// </summary>
        /// <returns>True if the server baking was successful</returns>
        private async Task<bool> UpdateAvatarAppearanceAsync(CancellationToken cancellationToken, int totalRetries = 3)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) { return false; }

                if (totalRetries < 0)
                {
                    return false;
                }

                var cap = Client.Network.CurrentSim.Caps?.CapabilityURI("UpdateAvatarAppearance");
                if (cap == null)
                {
                    Logger.Log("Could not retrieve UpdateAvatarAppearance region capability", 
                        Helpers.LogLevel.Warning, Client);
                    return false;
                }

                var currentOutfitFolder = GetCurrentOutfitFolder();
                if (currentOutfitFolder == null)
                {
                    Logger.Log("Could not retrieve Current Outfit folder",
                        Helpers.LogLevel.Warning, Client);
                    return false;
                }
                else
                {
                    // TODO: create Current Outfit Folder
                }

                Logger.Log($"Requesting bake for COF version {currentOutfitFolder.Version}", 
                    Helpers.LogLevel.Info, Client);

                var request = new OSDMap(1) { ["cof_version"] = currentOutfitFolder.Version };

                OSD res = null;

                var maxRetries = 1000; // About a minute. (50,000ms)

                while (maxRetries-- > 0)
                {
                    if (!Client.Network.Connected)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                    else
                    {
                        if (GetOwnAvatar() == null)
                        {
                            await Task.Delay(50, cancellationToken);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, request, cancellationToken, (response, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log($"UpdateAvatarAppearance failed. Server responded with: {error.Message}", 
                            Helpers.LogLevel.Warning, Client);
                    }
                    else if (data != null)
                    {
                        res = OSDParser.Deserialize(data);
                    }
                });

                if (!(res is OSDMap result)) { return false; }

                if (result.ContainsKey("success") && result["success"].AsBoolean())
                {

                    var visualParams = result["visual_params"].AsBinary();
                    var textures = (result["textures"] as OSDArray)?.Select(arrayEntry => arrayEntry.AsUUID()).ToArray();
                    var cofVersion = result["cof_version"].AsInteger();

                    MyVisualParameters = visualParams;

                    if (textures != null && textures.Length > 20)
                    {
                        if ((textures[8] == UUID.Zero || textures[9] == UUID.Zero || textures[10] == UUID.Zero || textures[11] == UUID.Zero) || (textures[8] == DEFAULT_AVATAR_TEXTURE || textures[9] == DEFAULT_AVATAR_TEXTURE || textures[10] == DEFAULT_AVATAR_TEXTURE || textures[11] == DEFAULT_AVATAR_TEXTURE))
                        {
                            // This hasn't actually baked. Retry after a delay.
                            await Task.Delay(REBAKE_DELAY, cancellationToken);
                            totalRetries = totalRetries - 1;
                            continue;
                        }
                    }

                    try
                    {
                        var selfPrim = GetOwnAvatar();

                        if (selfPrim == null)
                        {
                            Logger.Log("Unable to find avatar to set appearance information", Helpers.LogLevel.Error, Client);
                        }
                        else
                        {
                            var selfAvatarTextures = new Primitive.TextureEntry(UUID.Zero);

                            if (textures != null)
                            {
                                for (var i = 0; i < textures.Length; i++)
                                {
                                    selfAvatarTextures.FaceTextures[i] = new Primitive.TextureEntryFace(null) { TextureID = textures[i] };
                                }

                                selfPrim.Textures = selfAvatarTextures;
                                MyTextures = selfAvatarTextures;
                            }

                            selfPrim.VisualParameters = visualParams;
                            selfPrim.AppearanceVersion = 1;
                            selfPrim.COFVersion = cofVersion;
                            selfPrim.AppearanceFlags = 0;

                            var appearance = new AvatarAppearanceEventArgs(Client.Network.CurrentSim, 
                                Client.Self.AgentID,
                                false,
                                selfPrim.Textures.DefaultTexture,
                                selfPrim.Textures.FaceTextures,
                                selfPrim.VisualParameters.ToList(), 1,
                                cofVersion,
                                AppearanceFlags.None,
                                selfPrim.ChildCount);

                            Client.Avatars.TriggerAvatarAppearanceMessage(appearance);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Error applying textures to avatar object: {e.Message}", Helpers.LogLevel.Error, Client);
                        throw;
                    }

                    Logger.Log("Returning appearance information from server-side bake request.", Helpers.LogLevel.Info, Client);
                    return true;
                }
                if (result.ContainsKey("expected"))
                {
                    Logger.Log($"Server expected {result["expected"].AsInteger()} as COF version. Version {currentOutfitFolder.Version} was sent.", 
                        Helpers.LogLevel.Warning, Client);
                }
                if (result.ContainsKey("error"))
                {
                    var er = result["error"].AsString();
                    if (string.IsNullOrEmpty(er))
                    {
                        Logger.Log($"UpdateAvatarAppearance failed. Server responded with: '{result["error"].AsString()}'",
                            Helpers.LogLevel.Warning, Client);
                    }
                }

                Logger.Log($"Avatar appearance update failed on {totalRetries} attempt.", Helpers.LogLevel.Info, Client);
                await Task.Delay(REBAKE_DELAY, cancellationToken);
                --totalRetries;
                continue;
            }
        }

        /// <summary>
        /// Get the latest version of COF
        /// </summary>
        /// <returns>Current Outfit Folder (or null if getting the data failed)</returns>
        public InventoryFolder GetCurrentOutfitFolder()
        {
            List<InventoryBase> root = null;
            var folderReceived = new AutoResetEvent(false);

            EventHandler<FolderUpdatedEventArgs> UpdatedCallback = (sender, e) =>
            {
                if (e.FolderID != Client.Inventory.Store.RootFolder.UUID) return;
                if (e.Success)
                {
                    root = Client.Inventory.Store.GetContents(Client.Inventory.Store.RootFolder.UUID);
                }
                folderReceived.Set();
            };

            Client.Inventory.FolderUpdated += UpdatedCallback;
            Task task = Client.Inventory.RequestFolderContents(Client.Inventory.Store.RootFolder.UUID, 
                Client.Self.AgentID, true, false, InventorySortOrder.ByDate);
            folderReceived.WaitOne(Client.Settings.CAPS_TIMEOUT);
            Client.Inventory.FolderUpdated -= UpdatedCallback;

            InventoryFolder currentOutfitFolder = null;

            // COF should be in the root folder. Request update to get the latest version number
            if (root == null) { return null; }
            foreach (var baseItem in root)
            {
                if (baseItem is InventoryFolder folder && folder.PreferredType == FolderType.CurrentOutfit)
                {
                    currentOutfitFolder = folder;
                    break;
                }
            }
            return currentOutfitFolder;
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
                    var vp = kvp.Value;
                    var paramValue = 0f;

                    var found = Wearables.Any(wearableList => wearableList.Value.Any(wearable => wearable.Asset != null && wearable.Asset.Params.TryGetValue(vp.ParamID, out paramValue)));


                    // Try and find this value in our collection of downloaded wearables

                    // Use a default value if we don't have one set for it
                    if (!found)
                        paramValue = vp.DefaultValue;

                    // Only Group-0 parameters are sent in AgentSetAppearance packets
                    if (kvp.Value.Group == 0)
                    {
                        set.VisualParam[vpIndex] = new AgentSetAppearancePacket.VisualParamBlock
                        {
                            ParamValue = Utils.FloatToByte(paramValue, vp.MinValue, vp.MaxValue)
                        };
                        ++vpIndex;
                    }

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

                    if (vpIndex >= nrParams) break;
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

                if (Client.Settings.AVATAR_TRACKING)
                {
                    Avatar me;
                    if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out me))
                    {
                        me.Textures = MyTextures;
                        me.VisualParameters = MyVisualParameters;
                    }
                }
            }
            return set;
        }

        public void SendOutfitToCurrentSimulator()
        {
            var blocks = new List<RezMultipleAttachmentsFromInvPacket.ObjectDataBlock>();

            var worn = RequestAgentWorn();

            Logger.Log($"{worn.Count} inventory items in 'Current Outfit' folder", Helpers.LogLevel.Info, Client);

            foreach (var inventoryBase in worn.Where(inventoryBase => inventoryBase != null))
            {
                Logger.Log($"'{inventoryBase.Name}' found in 'Current Outfit' folder ({inventoryBase.GetType().Name})", Helpers.LogLevel.Info, Client);

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
                            ItemID = attachment.ActualUUID,
                            Name = Utils.StringToBytes(attachment.Name),
                            Description = Utils.StringToBytes(attachment.Description),
                            NextOwnerMask = (uint)attachment.Permissions.NextOwnerMask,
                            OwnerID = attachment.OwnerID
                        };

                        Logger.Log($"Wearing attachment {attachment.UUID} ({attachment.Name})", Helpers.LogLevel.Debug, Client);

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
                            ItemID = attachmentIO.ActualUUID,
                            Name = Utils.StringToBytes(attachmentIO.Name),
                            Description = Utils.StringToBytes(attachmentIO.Description),
                            NextOwnerMask = (uint)attachmentIO.Permissions.NextOwnerMask,
                            OwnerID = attachmentIO.OwnerID
                        };

                        Logger.Log($"Wearing object {attachmentIO.UUID} ({attachmentIO.Name})", Helpers.LogLevel.Debug, Client);

                        blocks.Add(block);
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

        private void DelayedRequestSetAppearance()
        {
            if (RebakeScheduleTimer == null)
            {
                RebakeScheduleTimer = new Timer(RebakeScheduleTimerTick);
            }
            try { RebakeScheduleTimer.Change(REBAKE_DELAY, Timeout.Infinite); }
            catch { }
        }

        private void RebakeScheduleTimerTick(object state)
        {
            RequestSetAppearance(true);
        }
        #endregion Appearance Helpers

        #region Inventory Helpers

        private bool GetFolderWearables(string[] folderPath, out List<InventoryWearable> wearables, out List<InventoryItem> attachments)
        {
            var folder = Client.Inventory.FindObjectByPath(
                Client.Inventory.Store.RootFolder.UUID, Client.Self.AgentID, string.Join("/", folderPath), INVENTORY_TIMEOUT);

            if (folder != UUID.Zero)
            {
                return GetFolderWearables(folder, out wearables, out attachments);
            }
            Logger.Log($"Failed to resolve outfit folder path {folderPath}", Helpers.LogLevel.Error, Client);
            wearables = null;
            attachments = null;
            return false;
        }

        private bool GetFolderWearables(UUID folder, out List<InventoryWearable> wearables, out List<InventoryItem> attachments)
        {
            wearables = new List<InventoryWearable>();
            attachments = new List<InventoryItem>();
            var objects = Client.Inventory.FolderContents(folder, Client.Self.AgentID, false, true,
                InventorySortOrder.ByName, INVENTORY_TIMEOUT);

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
            }
            else
            {
                Logger.Log($"Failed to download folder contents of {folder}", Helpers.LogLevel.Error, Client);
                return false;
            }

            return true;
        }

        #endregion Inventory Helpers

        #region Callbacks

        protected void AgentWearablesUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            var update = (AgentWearablesUpdatePacket)e.Packet;

            Logger.DebugLog("Received AgentWearablesUpdate");

            // At one point this was necessary, but Second Life now sends dummy items back...
            // So let's just ignore the dumdum, k?

            // Fire the callback
            OnAgentWearables(new AgentWearablesReplyEventArgs());
        }

        protected void RebakeAvatarTexturesHandler(object sender, PacketReceivedEventArgs e)
        {
            var rebake = (RebakeAvatarTexturesPacket)e.Packet;

            // allow the library to do the rebake
            if (Client.Settings.SEND_AGENT_APPEARANCE)
            {
                RequestSetAppearance(true);
            }

            OnRebakeAvatar(new RebakeAvatarTexturesEventArgs(rebake.TextureData.TextureID));
        }

        protected void AgentCachedTextureResponseHandler(object sender, PacketReceivedEventArgs e)
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

        private void Network_OnDisconnected(object sender, DisconnectedEventArgs e)
        {
            if (RebakeScheduleTimer != null)
            {
                RebakeScheduleTimer.Dispose();
                RebakeScheduleTimer = null;
            }

            if (AppearanceCts != null)
            {
                AppearanceCts.Cancel();
                AppearanceCts.Dispose();
                AppearanceCts = null;
            }

            if (AppearanceThread != null)
            {
                AppearanceThread = null;
                AppearanceThreadRunning = 0;
            }
        }

        private void Network_OnSimChanged(object sender, SimChangedEventArgs e)
        {
            Client.Network.CurrentSim.Caps.CapabilitiesReceived += Simulator_OnCapabilitiesReceived;
        }

        private void Simulator_OnCapabilitiesReceived(object sender, CapabilitiesReceivedEventArgs e)
        {
            e.Simulator.Caps.CapabilitiesReceived -= Simulator_OnCapabilitiesReceived;

            if (e.Simulator == Client.Network.CurrentSim && Client.Settings.SEND_AGENT_APPEARANCE)
            {
                bool updateSucceeded = UpdateAvatarAppearanceAsync(CancellationToken.None).Result;
                if (updateSucceeded)
                {
                    ThreadPool.QueueUserWorkItem((o) => { SendOutfitToCurrentSimulator(); });
                }
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

        /// <summary>
        /// Converts a BakeType to the corresponding baked texture slot in AvatarTextureIndex
        /// </summary>
        /// <param name="index">A BakeType</param>
        /// <returns>The AvatarTextureIndex slot that holds the given BakeType</returns>
        public static AvatarTextureIndex BakeTypeToAgentTextureIndex(BakeType index)
        {
            switch (index)
            {
                case BakeType.Head:
                    return AvatarTextureIndex.HeadBaked;
                case BakeType.UpperBody:
                    return AvatarTextureIndex.UpperBaked;
                case BakeType.LowerBody:
                    return AvatarTextureIndex.LowerBaked;
                case BakeType.Eyes:
                    return AvatarTextureIndex.EyesBaked;
                case BakeType.Skirt:
                    return AvatarTextureIndex.SkirtBaked;
                case BakeType.Hair:
                    return AvatarTextureIndex.HairBaked;
                case BakeType.BakedLeftArm:
                    return AvatarTextureIndex.LeftArmBaked;
                case BakeType.BakedLeftLeg:
                    return AvatarTextureIndex.LegLegBaked;
                case BakeType.BakedAux1:
                    return AvatarTextureIndex.Aux1Baked;
                case BakeType.BakedAux2:
                    return AvatarTextureIndex.Aux2Baked;
                case BakeType.BakedAux3:
                    return AvatarTextureIndex.Aux3Baked;
                default:
                    return AvatarTextureIndex.Unknown;
            }
        }

        /// <summary>
        /// Gives the layer number that is used for morph mask
        /// </summary>
        /// <param name="bakeType">>A BakeType</param>
        /// <returns>Which layer number as defined in BakeTypeToTextures is used for morph mask</returns>
        public static AvatarTextureIndex MorphLayerForBakeType(BakeType bakeType)
        {
            // Indexes return here correspond to those returned
            // in BakeTypeToTextures(), those two need to be in sync.
            // Which wearable layer is used for morph is defined in avatar_lad.xml
            // by looking for <layer> that has <morph_mask> defined in it, and
            // looking up which wearable is defined in that layer. Morph mask
            // is never combined, it's always a straight copy of one single clothing
            // item's alpha channel per bake.
            switch (bakeType)
            {
                case BakeType.Head:
                    return AvatarTextureIndex.Hair; // hair
                case BakeType.UpperBody:
                    return AvatarTextureIndex.UpperShirt; // shirt
                case BakeType.LowerBody:
                    return AvatarTextureIndex.LowerPants; // lower pants
                case BakeType.Skirt:
                    return AvatarTextureIndex.Skirt; // skirt
                case BakeType.Hair:
                    return AvatarTextureIndex.Hair; // hair
                case BakeType.BakedLeftArm:
                    return AvatarTextureIndex.LeftArmTattoo;
                case BakeType.BakedLeftLeg:
                    return AvatarTextureIndex.LeftLegTattoo;
                case BakeType.BakedAux1:
                    return AvatarTextureIndex.Aux1Tattoo;
                case BakeType.BakedAux2:
                    return AvatarTextureIndex.Aux2Tattoo;
                case BakeType.BakedAux3:
                    return AvatarTextureIndex.Aux3Tattoo;
                default:
                    return AvatarTextureIndex.Unknown;
            }
        }

        /// <summary>
        /// Converts a BakeType to a list of the texture slots that make up that bake
        /// </summary>
        /// <param name="bakeType">A BakeType</param>
        /// <returns>A list of texture slots that are inputs for the given bake</returns>
        public static List<AvatarTextureIndex> BakeTypeToTextures(BakeType bakeType)
        {
            var textures = new List<AvatarTextureIndex>();

            switch (bakeType)
            {
                case BakeType.Head:
                    textures.Add(AvatarTextureIndex.HeadBodypaint);
                    textures.Add(AvatarTextureIndex.HeadTattoo);
                    textures.Add(AvatarTextureIndex.Hair);
                    textures.Add(AvatarTextureIndex.HeadAlpha);
                    break;
                case BakeType.UpperBody:
                    textures.Add(AvatarTextureIndex.UpperBodypaint);
                    textures.Add(AvatarTextureIndex.UpperTattoo);
                    textures.Add(AvatarTextureIndex.UpperGloves);
                    textures.Add(AvatarTextureIndex.UpperUndershirt);
                    textures.Add(AvatarTextureIndex.UpperShirt);
                    textures.Add(AvatarTextureIndex.UpperJacket);
                    textures.Add(AvatarTextureIndex.UpperAlpha);
                    break;
                case BakeType.LowerBody:
                    textures.Add(AvatarTextureIndex.LowerBodypaint);
                    textures.Add(AvatarTextureIndex.LowerTattoo);
                    textures.Add(AvatarTextureIndex.LowerUnderpants);
                    textures.Add(AvatarTextureIndex.LowerSocks);
                    textures.Add(AvatarTextureIndex.LowerShoes);
                    textures.Add(AvatarTextureIndex.LowerPants);
                    textures.Add(AvatarTextureIndex.LowerJacket);
                    textures.Add(AvatarTextureIndex.LowerAlpha);
                    break;
                case BakeType.Eyes:
                    textures.Add(AvatarTextureIndex.EyesIris);
                    textures.Add(AvatarTextureIndex.EyesAlpha);
                    break;
                case BakeType.Skirt:
                    textures.Add(AvatarTextureIndex.Skirt);
                    break;
                case BakeType.Hair:
                    textures.Add(AvatarTextureIndex.Hair);
                    textures.Add(AvatarTextureIndex.HairAlpha);
                    break;
            }

            return textures;
        }

        #endregion Static Helpers
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
        /// Triggered when the simulator sends a request for this agent to rebake
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