/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2024, Sjofn LLC.
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
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.Assets;
using OpenMetaverse.Http;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;

namespace OpenMetaverse
{
    #region Enums

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

    /// <summary>
    /// 
    /// </summary>
    public enum ChannelType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary>Unknown</summary>
        Misc = 1,
        /// <summary>Virtually all asset transfers use this channel</summary>
        Asset = 2
    }

    /// <summary>
    /// 
    /// </summary>
    public enum SourceType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary>Asset from the asset server</summary>
        Asset = 2,
        /// <summary>Inventory item</summary>
        SimInventoryItem = 3,
        /// <summary>Estate asset, such as an estate covenant</summary>
        SimEstate = 4
    }

    /// <summary>
    /// 
    /// </summary>
    public enum TargetType : int
    {
        /// <summary></summary>
        Unknown = 0,
        /// <summary></summary>
        File = 1,
        /// <summary></summary>
        VFile = 2
    }

    /// <summary>
    /// When requesting image download, type of the image requested
    /// </summary>
    public enum ImageType : byte
    {
        /// <summary>Normal in-world object texture</summary>
        Normal = 0,
        /// <summary>Avatar texture</summary>
        Baked = 1,
        /// <summary>Server baked avatar texture</summary>
        ServerBaked = 2
    }

    /// <summary>
    /// Image file format
    /// </summary>
    public enum ImageCodec : byte
    {
        Invalid = 0,
        RGB = 1,
        J2C = 2,
        BMP = 3,
        TGA = 4,
        JPEG = 5,
        DXT = 6,
        PNG = 7
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

    #endregion Enums

    #region Transfer Classes

    /// <summary>
    /// 
    /// </summary>
    public class Transfer
    {
        public UUID ID;
        public int Size;
        public byte[] AssetData;
        public int Transferred;
        public bool Success;
        public AssetType AssetType;

        private int transferStart;

        /// <summary>Number of milliseconds passed since the last transfer
        /// packet was received</summary>
        public int TimeSinceLastPacket
        {
            get => Environment.TickCount - transferStart;
            internal set => transferStart = Environment.TickCount + value;
        }

        public Transfer()
        {
            AssetData = Utils.EmptyBytes;
            transferStart = Environment.TickCount;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetDownload : Transfer
    {
        public UUID AssetID;
        public ChannelType Channel;
        public SourceType Source;
        public TargetType Target;
        public StatusCode Status;
        public float Priority;
        public Simulator Simulator;
        public AssetManager.AssetReceivedCallback Callback;

        public int nextPacket;
        public LockingDictionary<int, byte[]> outOfOrderPackets;
        internal ManualResetEvent HeaderReceivedEvent = new ManualResetEvent(false);

        public AssetDownload()
        {
            nextPacket = 0;
            outOfOrderPackets = new LockingDictionary<int, byte[]>();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class XferDownload : Transfer
    {
        public ulong XferID;
        public UUID VFileID;
        public uint PacketNum;
        public string Filename = string.Empty;
        public TransferError Error = TransferError.None;
    }

    /// <summary>
    /// 
    /// </summary>
    public class ImageDownload : Transfer
    {
        public ushort PacketCount;
        public ImageCodec Codec;
        public Simulator Simulator;
        public SortedList<ushort, ushort> PacketsSeen;
        public ImageType ImageType;
        public int DiscardLevel;
        public float Priority;
        internal int InitialDataSize;
        internal ManualResetEvent HeaderReceivedEvent = new ManualResetEvent(false);
    }

    /// <summary>
    /// 
    /// </summary>
    public class AssetUpload : Transfer
    {
        public UUID AssetID;
        public AssetType Type;
        public ulong XferID;
        public uint PacketNum;
    }

    /// <summary>
    /// 
    /// </summary>
    public class ImageRequest
    {
        public UUID ImageID;
        public ImageType Type;
        public float Priority;
        public int DiscardLevel;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageid"></param>
        /// <param name="type"></param>
        /// <param name="priority"></param>
        /// <param name="discardLevel"></param>
        public ImageRequest(UUID imageid, ImageType type, float priority, int discardLevel)
        {
            ImageID = imageid;
            Type = type;
            Priority = priority;
            DiscardLevel = discardLevel;
        }

    }
    #endregion Transfer Classes

    /// <summary>
    /// 
    /// </summary>
    public class AssetManager
    {
        /// <summary>Number of milliseconds to wait for a transfer header packet if out of order data was received</summary>
        private const int TRANSFER_HEADER_TIMEOUT = 1000 * 15;

        #region Delegates
        /// <summary>
        /// Callback used for various asset download requests
        /// </summary>
        /// <param name="transfer">Transfer information</param>
        /// <param name="asset">Downloaded asset, null on fail</param>
        public delegate void AssetReceivedCallback(AssetDownload transfer, Asset asset);
        /// <summary>
        /// Callback used upon competition of baked texture upload
        /// </summary>
        /// <param name="newAssetID">Asset UUID of the newly uploaded baked texture</param>
        public delegate void BakedTextureUploadedCallback(UUID newAssetID);
        /// <summary>
        /// A callback that fires upon the completion of the RequestMesh call
        /// </summary>
        /// <param name="success">Was the download successful</param>
        /// <param name="assetMesh">Resulting mesh or null on problems</param>
        public delegate void MeshDownloadCallback(bool success, AssetMesh assetMesh);

        #endregion Delegates

        #region Events

        #region XferReceived
        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<XferReceivedEventArgs> m_XferReceivedEvent;

        /// <summary>Raises the XferReceived event</summary>
        /// <param name="e">A XferReceivedEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnXferReceived(XferReceivedEventArgs e)
        {
            EventHandler<XferReceivedEventArgs> handler = m_XferReceivedEvent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_XferReceivedLock = new object();

        /// <summary>Raised when the simulator responds sends </summary>
        public event EventHandler<XferReceivedEventArgs> XferReceived
        {
            add { lock (m_XferReceivedLock) { m_XferReceivedEvent += value; } }
            remove { lock (m_XferReceivedLock) { m_XferReceivedEvent -= value; } }
        }
        #endregion

        #region AssetUploaded
        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AssetUploadEventArgs> m_AssetUploadedEvent;

        /// <summary>Raises the AssetUploaded event</summary>
        /// <param name="e">A AssetUploadedEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnAssetUploaded(AssetUploadEventArgs e)
        {
            EventHandler<AssetUploadEventArgs> handler = m_AssetUploadedEvent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AssetUploadedLock = new object();

        /// <summary>Raised during upload completes</summary>
        public event EventHandler<AssetUploadEventArgs> AssetUploaded
        {
            add { lock (m_AssetUploadedLock) { m_AssetUploadedEvent += value; } }
            remove { lock (m_AssetUploadedLock) { m_AssetUploadedEvent -= value; } }
        }
        #endregion

        #region UploadProgress
        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AssetUploadEventArgs> m_UploadProgressEvent;

        /// <summary>Raises the UploadProgress event</summary>
        /// <param name="e">A UploadProgressEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnUploadProgress(AssetUploadEventArgs e)
        {
            EventHandler<AssetUploadEventArgs> handler = m_UploadProgressEvent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_UploadProgressLock = new object();

        /// <summary>Raised during upload with progres update</summary>
        public event EventHandler<AssetUploadEventArgs> UploadProgress
        {
            add { lock (m_UploadProgressLock) { m_UploadProgressEvent += value; } }
            remove { lock (m_UploadProgressLock) { m_UploadProgressEvent -= value; } }
        }
        #endregion UploadProgress

        #region InitiateDownload
        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<InitiateDownloadEventArgs> m_InitiateDownloadEvent;

        /// <summary>Raises the InitiateDownload event</summary>
        /// <param name="e">A InitiateDownloadEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnInitiateDownload(InitiateDownloadEventArgs e)
        {
            EventHandler<InitiateDownloadEventArgs> handler = m_InitiateDownloadEvent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InitiateDownloadLock = new object();

        /// <summary>Fired when the simulator sends an InitiateDownloadPacket, used to download terrain .raw files</summary>
        public event EventHandler<InitiateDownloadEventArgs> InitiateDownload
        {
            add { lock (m_InitiateDownloadLock) { m_InitiateDownloadEvent += value; } }
            remove { lock (m_InitiateDownloadLock) { m_InitiateDownloadEvent -= value; } }
        }
        #endregion InitiateDownload

        #region ImageReceiveProgress
        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ImageReceiveProgressEventArgs> m_ImageReceiveProgressEvent;

        /// <summary>Raises the ImageReceiveProgress event</summary>
        /// <param name="e">A ImageReceiveProgressEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnImageReceiveProgress(ImageReceiveProgressEventArgs e)
        {
            EventHandler<ImageReceiveProgressEventArgs> handler = m_ImageReceiveProgressEvent;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ImageReceiveProgressLock = new object();

        /// <summary>Fired when a texture is in the process of being downloaded by the TexturePipeline class</summary>
        public event EventHandler<ImageReceiveProgressEventArgs> ImageReceiveProgress
        {
            add { lock (m_ImageReceiveProgressLock) { m_ImageReceiveProgressEvent += value; } }
            remove { lock (m_ImageReceiveProgressLock) { m_ImageReceiveProgressEvent -= value; } }
        }
        #endregion ImageReceiveProgress

        #endregion Events

        /// <summary>Texture download cache</summary>
        public AssetCache Cache;

        private readonly TexturePipeline Texture;

        private readonly DownloadManager HttpDownloads;

        private readonly GridClient Client;

        private readonly Dictionary<UUID, Transfer> Transfers = new Dictionary<UUID, Transfer>();

        private AssetUpload PendingUpload;
        private readonly object PendingUploadLock = new object();
        private volatile bool WaitingForUploadConfirm = false;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="client">A reference to the GridClient object</param>
        public AssetManager(GridClient client)
        {
            Client = client;
            Cache = new AssetCache(client);
            Texture = new TexturePipeline(client);
            HttpDownloads = new DownloadManager(client);

            // Transfer packets for downloading large assets
            Client.Network.RegisterCallback(PacketType.TransferInfo, TransferInfoHandler);
            Client.Network.RegisterCallback(PacketType.TransferPacket, TransferPacketHandler);

            // Xfer packets for uploading large assets
            Client.Network.RegisterCallback(PacketType.RequestXfer, RequestXferHandler);
            Client.Network.RegisterCallback(PacketType.ConfirmXferPacket, ConfirmXferPacketHandler);
            Client.Network.RegisterCallback(PacketType.AssetUploadComplete, AssetUploadCompleteHandler);

            // Xfer packets for downloading misc assets
            Client.Network.RegisterCallback(PacketType.SendXferPacket, SendXferPacketHandler);
            Client.Network.RegisterCallback(PacketType.AbortXfer, AbortXferHandler);

            // Simulator is responding to a request to download a file
            Client.Network.RegisterCallback(PacketType.InitiateDownload, InitiateDownloadPacketHandler);

        }

        // TODO: Probably somewhere else is a more useful place to keep this.
        /// <summary>
        /// Returns type name as string for a given AssetType
        /// </summary>
        /// <param name="assetType"></param>
        /// <returns>Type name</returns>
        public string AssetTypeToString(AssetType assetType)
        {
            switch(assetType) {
                case AssetType.Texture:
                    return "texture";
                case AssetType.Sound:
                    return "sound";
                case AssetType.CallingCard:
                    return "callcard";
                case AssetType.Landmark:
                    return "landmark";
#pragma warning disable 618
                case AssetType.Script:
                    return "script";
#pragma warning restore 618
                case AssetType.Clothing:
                    return "clothing";
                case AssetType.Object:
                    return "object";
                case AssetType.Notecard:
                    return "notecard";
                case AssetType.Folder:
                    return "category";
                case AssetType.LSLText:
                    return "lsltext";
                case AssetType.LSLBytecode:
                    return "lslbyte";
                case AssetType.TextureTGA:
                    return "txtr_tga";
                case AssetType.Bodypart:
                    return "bodypart";
                case AssetType.SoundWAV:
                    return "snd_wav";
                case AssetType.ImageTGA:
                    return "img_tga";
                case AssetType.ImageJPEG:
                    return "jpeg";
                case AssetType.Animation:
                    return "animatn";
                case AssetType.Gesture:
                    return "gesture";
                case AssetType.Simstate:
                    return "simstate";
                case AssetType.Link:
                    return "link";
                case AssetType.LinkFolder:
                    return "link_f";
                case AssetType.Mesh:
                    return "mesh";
                case AssetType.Widget:
                    return "widget";
                case AssetType.Person:
                    return "person";
                case AssetType.Unknown:
                default:
                    return "invalid";
            }
        }

        /// <summary>
        /// Build uri for requesting an asset from ViewerAsset capability
        /// </summary>
        /// <param name="assetType"></param>
        /// <param name="assetId"></param>
        /// <returns>Request URI for an asset</returns>
        private Uri BuildFetchRequestUri(AssetType assetType, UUID assetId)
        {
            return new Uri($"{Client.Network.CurrentSim.Caps.CapabilityURI("ViewerAsset")}?{AssetTypeToString(assetType)}_id={assetId}");
        }

        /// <summary>
        /// Request an asset download
        /// </summary>
        /// <param name="assetID">Asset UUID</param>
        /// <param name="type">Asset type, must be correct for the transfer to succeed</param>
        /// <param name="priority">Whether to give this transfer an elevated priority</param>
        /// <param name="callback">The callback to fire when the simulator responds with the asset data</param>
        public void RequestAsset(UUID assetID, AssetType type, bool priority, AssetReceivedCallback callback)
        {
            RequestAsset(assetID, type, priority, SourceType.Asset, UUID.Random(), callback);
        }

        /// <summary>
        /// Request an asset download
        /// </summary>
        /// <param name="assetID">Asset UUID</param>
        /// <param name="type">Asset type, must be correct for the transfer to succeed</param>
        /// <param name="priority">Whether to give this transfer an elevated priority</param>
        /// <param name="sourceType">Source location of the requested asset</param>
        /// <param name="callback">The callback to fire when the simulator responds with the asset data</param>
        public void RequestAsset(UUID assetID, AssetType type, bool priority, SourceType sourceType, AssetReceivedCallback callback)
        {
            RequestAsset(assetID, type, priority, sourceType, UUID.Random(), callback);
        }

        /// <summary>
        /// Request an asset download
        /// </summary>
        /// <param name="assetID">Asset UUID</param>
        /// <param name="type">Asset type, must be correct for the transfer to succeed</param>
        /// <param name="priority">Whether to give this transfer an elevated priority</param>
        /// <param name="sourceType">Source location of the requested asset</param>
        /// <param name="transactionID">UUID of the transaction</param>
        /// <param name="callback">The callback to fire when the simulator responds with the asset data</param>
        public void RequestAsset(UUID assetID, AssetType type, bool priority, SourceType sourceType, UUID transactionID, AssetReceivedCallback callback)
        {
            RequestAsset(assetID, UUID.Zero, UUID.Zero, type, priority, sourceType, transactionID, callback);
        }

        /// <summary>
        /// Request an asset download
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="itemID"></param>
        /// <param name="taskID"></param>
        /// <param name="assetType"></param>
        /// <param name="priority"></param>
        /// <param name="sourceType"></param>
        /// <param name="transactionID"></param>
        /// <param name="callback"></param>
        public void RequestAsset(UUID assetID, UUID itemID, UUID taskID, AssetType assetType, bool priority,
            SourceType sourceType, UUID transactionID, AssetReceivedCallback callback)
        {
            AssetDownload transfer = new AssetDownload
            {
                ID = transactionID,
                AssetID = assetID,
                AssetType = assetType,
                Priority = 100.0f + (priority ? 1.0f : 0.0f),
                Channel = ChannelType.Asset,
                Source = sourceType,
                Simulator = Client.Network.CurrentSim,
                Callback = callback
            };

            // Check asset cache first
            if (callback != null && Cache.HasAsset(assetID))
            {
                byte[] data = Cache.GetCachedAssetBytes(assetID);
                transfer.AssetData = data;
                transfer.Success = true;
                transfer.Status = StatusCode.OK;

                Asset asset = CreateAssetWrapper(assetType);
                asset.AssetData = data;
                asset.AssetID = assetID;

                try { callback(transfer, asset); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }

                return;
            }

            // If ViewerAsset capability exists and asset is directly fetchable, use that,
            // if not, fallback to UDP (which is obsoleted on Second Life.)
            if (CanFetchAsset(assetType) && Client.Network.CurrentSim?.Caps?.CapabilityURI("ViewerAsset") != null)
            {
                RequestAssetHTTP(assetID, transfer, callback);
            }
            else
            {
                RequestAssetUDP(assetID, itemID, taskID, transfer, callback);
            }

        }

        /// <summary>
        /// Request an asset download via HTTP
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="transfer"></param>
        /// <param name="callback"></param>
        private void RequestAssetHTTP(UUID assetID, AssetDownload transfer, AssetReceivedCallback callback)
        {
            DownloadRequest req = new DownloadRequest(
                BuildFetchRequestUri(transfer.AssetType, assetID),
                null,
                null,
                (response, responseData, error) =>
                {
                    if (error == null && responseData != null && response.IsSuccessStatusCode) // success
                    {
                        Client.Assets.Cache.SaveAssetToCache(assetID, responseData);

                        if (callback != null)
                        {
                            transfer.AssetData = responseData;
                            transfer.Success = true;
                            transfer.Status = StatusCode.OK;

                            Asset asset = CreateAssetWrapper(transfer.AssetType);
                            asset.AssetData = responseData;
                            asset.AssetID = assetID;
                            callback(transfer, asset);
                        }
                    }
                    else // download failed
                    {
                        Logger.Log($"Failed to fetch asset {assetID}: {(error == null ? "" : error.Message)}",
                            Helpers.LogLevel.Warning, Client);
                        if (callback != null)
                        {
                            transfer.Success = false;
                            transfer.Status = StatusCode.Error;
                            callback(transfer, null);
                        }

                    }
                }
            );
            HttpDownloads.QueueDownload(req);
        }

        /// <summary>
        /// Request an asset download via LLUDP
        /// </summary>
        /// <param name="assetID">Asset UUID</param>
        /// <param name="taskID">task ID</param>
        /// <param name="transfer"></param>
        /// <param name="callback">The callback to fire when the simulator responds with the asset data</param>
        /// <param name="itemID">Item ID</param>
        private void RequestAssetUDP(UUID assetID, UUID itemID, UUID taskID, 
            AssetDownload transfer, AssetReceivedCallback callback)
        {

            // Add this transfer to the dictionary
            lock (Transfers) Transfers[transfer.ID] = transfer;

            // Build the request packet and send it
            TransferRequestPacket request = new TransferRequestPacket
            {
                TransferInfo =
                {
                    ChannelType = (int) transfer.Channel,
                    Priority = transfer.Priority,
                    SourceType = (int) transfer.Source,
                    TransferID = transfer.ID
                }
            };

            byte[] paramField = taskID == UUID.Zero ? new byte[20] : new byte[96];
            Buffer.BlockCopy(assetID.GetBytes(), 0, paramField, 0, 16);
            Buffer.BlockCopy(Utils.IntToBytes((int)transfer.AssetType), 0, paramField, 16, 4);

            if (taskID != UUID.Zero)
            {
                Buffer.BlockCopy(taskID.GetBytes(), 0, paramField, 48, 16);
                Buffer.BlockCopy(itemID.GetBytes(), 0, paramField, 64, 16);
                Buffer.BlockCopy(assetID.GetBytes(), 0, paramField, 80, 16);
            }
            request.TransferInfo.Params = paramField;

            Client.Network.SendPacket(request, transfer.Simulator);
        }

        /// <summary>
        /// Request an asset download through the almost deprecated Xfer system
        /// </summary>
        /// <param name="filename">Filename of the asset to request</param>
        /// <param name="deleteOnCompletion">Delete the asset
        /// off the server after it is retrieved</param>
        /// <param name="useBigPackets">Use large transfer packets or not</param>
        /// <param name="vFileID">UUID of the file to request, if filename is
        /// left empty</param>
        /// <param name="vFileType">Asset type of <paramref name="vFileID"/>, or
        /// <see cref="AssetType.Unknown" /> if filename is not empty</param>
        /// <param name="fromCache">Sets the FilePath in the request to Cache
        /// (4) if true, otherwise Unknown (0) is used</param>
        /// <returns></returns>
        public ulong RequestAssetXfer(string filename, bool deleteOnCompletion, bool useBigPackets, UUID vFileID, AssetType vFileType,
            bool fromCache)
        {
            UUID uuid = UUID.Random();
            ulong id = uuid.GetULong();

            XferDownload transfer = new XferDownload
            {
                XferID = id,
                ID = new UUID(id),
                Filename = filename,
                VFileID = vFileID,
                AssetType = vFileType
            };
            // Our dictionary tracks transfers with UUIDs, so convert the ulong back

            // Add this transfer to the dictionary
            lock (Transfers) Transfers[transfer.ID] = transfer;

            RequestXferPacket request = new RequestXferPacket
            {
                XferID =
                {
                    ID = id,
                    Filename = Utils.StringToBytes(filename),
                    FilePath = fromCache ? (byte) 4 : (byte) 0,
                    DeleteOnCompletion = deleteOnCompletion,
                    UseBigPackets = useBigPackets,
                    VFileID = vFileID,
                    VFileType = (short) vFileType
                }
            };

            Client.Network.SendPacket(request);

            return id;
        }

        public void RequestInventoryAsset(UUID assetID, UUID itemID, UUID taskID, UUID ownerID, AssetType assetType,
            bool priority, UUID transferID, AssetReceivedCallback callback)
        {
            AssetDownload transfer = new AssetDownload
            {
                ID = transferID,
                AssetID = assetID,
                AssetType = assetType,
                Priority = 100.0f + (priority ? 1.0f : 0.0f),
                Channel = ChannelType.Asset,
                Source = SourceType.SimInventoryItem,
                Simulator = Client.Network.CurrentSim,
                Callback = callback
            };

            // Check asset cache first
            if (callback != null && Cache.HasAsset(assetID))
            {
                byte[] data = Cache.GetCachedAssetBytes(assetID);
                transfer.AssetData = data;
                transfer.Success = true;
                transfer.Status = StatusCode.OK;

                Asset asset = CreateAssetWrapper(assetType);
                asset.AssetData = data;
                asset.AssetID = assetID;

                try { callback(transfer, asset); }
                catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }

                return;
            }
            
            // If ViewerAsset capability exists, use that, if not, fallback to UDP
            if (CanFetchAsset(assetType) && Client.Network.CurrentSim?.Caps?.CapabilityURI("ViewerAsset") != null)
            {
                RequestInventoryAssetHTTP(assetID, transfer, callback);
            }
            else
            {
                RequestInventoryAssetUDP(assetID, itemID, taskID, ownerID, transfer, callback);
            }

            
        }

        /// <summary>
        /// Request Inventory Asset via HTTP
        /// </summary>
        /// <param name="assetID">Use UUID.Zero if you do not have the 
        /// asset ID but have all the necessary permissions</param>
        /// <param name="transfer"></param>
        /// <param name="callback"></param>
        private void RequestInventoryAssetHTTP(UUID assetID, AssetDownload transfer, AssetReceivedCallback callback)
        {
            DownloadRequest req = new DownloadRequest(
                BuildFetchRequestUri(transfer.AssetType, assetID),
                null,
                null,
                (response, responseData, error) =>
                {
                    if (error == null && responseData != null && response.IsSuccessStatusCode) // success
                    {
                        Client.Assets.Cache.SaveAssetToCache(assetID, responseData);

                        if (callback != null)
                        {
                            transfer.AssetData = responseData;
                            transfer.Success = true;
                            transfer.Status = StatusCode.OK;

                            Asset asset = CreateAssetWrapper(transfer.AssetType);
                            asset.AssetData = responseData;
                            asset.AssetID = assetID;
                            callback(transfer, asset);
                        }
                    }
                    else // download failed
                    {
                        Logger.Log($"Failed to fetch asset {assetID}: {((error == null) ? "" : error.Message)}",
                            Helpers.LogLevel.Warning, Client);
                        if (callback != null)
                        {
                            transfer.Success = false;
                            transfer.Status = StatusCode.Error;
                        }

                    }
                }
            );
            HttpDownloads.QueueDownload(req);
        }

        /// <summary>
        /// Request Inventory Asset from UDP
        /// </summary>
        /// <param name="assetID">Use UUID.Zero if you do not have the 
        /// asset ID but have all the necessary permissions</param>
        /// <param name="itemID">The item ID of this asset in the inventory</param>
        /// <param name="taskID">Use UUID.Zero if you are not requesting an 
        /// asset from an object inventory</param>
        /// <param name="ownerID">The owner of this asset</param>
        /// <param name="transfer"></param>
        /// <param name="callback"></param>
        private void RequestInventoryAssetUDP(UUID assetID, UUID itemID, UUID taskID, UUID ownerID, AssetDownload transfer, AssetReceivedCallback callback)
        {
            // Add this transfer to the dictionary
            lock (Transfers) Transfers[transfer.ID] = transfer;

            // Build the request packet and send it
            TransferRequestPacket request = new TransferRequestPacket
            {
                TransferInfo =
                {
                    ChannelType = (int) transfer.Channel,
                    Priority = transfer.Priority,
                    SourceType = (int) transfer.Source,
                    TransferID = transfer.ID
                }
            };

            byte[] paramField = new byte[100];
            Buffer.BlockCopy(Client.Self.AgentID.GetBytes(), 0, paramField, 0, 16);
            Buffer.BlockCopy(Client.Self.SessionID.GetBytes(), 0, paramField, 16, 16);
            Buffer.BlockCopy(ownerID.GetBytes(), 0, paramField, 32, 16);
            Buffer.BlockCopy(taskID.GetBytes(), 0, paramField, 48, 16);
            Buffer.BlockCopy(itemID.GetBytes(), 0, paramField, 64, 16);
            Buffer.BlockCopy(assetID.GetBytes(), 0, paramField, 80, 16);
            Buffer.BlockCopy(Utils.IntToBytes((int)transfer.AssetType), 0, paramField, 96, 4);
            request.TransferInfo.Params = paramField;

            Client.Network.SendPacket(request, transfer.Simulator);
        }

        public void RequestInventoryAsset(InventoryItem item, bool priority, UUID transferID, AssetReceivedCallback callback)
        {
            RequestInventoryAsset(item.AssetUUID, item.UUID, UUID.Zero, item.OwnerID, item.AssetType, priority, transferID, callback);
        }

        public void RequestEstateAsset()
        {
            throw new Exception("This function is not implemented yet!");
        }

#region Uploads
        /// <summary>
        /// Used to force asset data into the PendingUpload property, ie: for raw terrain uploads
        /// </summary>
        /// <param name="assetData">An AssetUpload object containing the data to upload to the simulator</param>
        internal void SetPendingAssetUploadData(AssetUpload assetData)
        {
            lock (PendingUploadLock)
                PendingUpload = assetData;
        }

        /// <summary>
        /// Request an asset be uploaded to the simulator
        /// </summary>
        /// <param name="asset">The <see cref="Asset"/> Object containing the asset data</param>
        /// <param name="storeLocal">If True, the asset once uploaded will be stored on the simulator
        /// in which the client was connected in addition to being stored on the asset server</param>
        /// <returns>The <see cref="UUID"/> of the transfer, can be used to correlate the upload with
        /// events being fired</returns>
        public UUID RequestUpload(Asset asset, bool storeLocal)
        {
            if (asset.AssetData == null)
                throw new ArgumentException("Can't upload an asset with no data (did you forget to call Encode?)");

            UUID assetID;
            UUID transferID = RequestUpload(out assetID, asset.AssetType, asset.AssetData, storeLocal);
            asset.AssetID = assetID;
            return transferID;
        }

        /// <summary>
        /// Request an asset be uploaded to the simulator
        /// </summary>
        /// <param name="type">The <see cref="AssetType"/> of the asset being uploaded</param>
        /// <param name="data">A byte array containing the encoded asset data</param>
        /// <param name="storeLocal">If True, the asset once uploaded will be stored on the simulator
        /// in which the client was connected in addition to being stored on the asset server</param>
        /// <returns>The <see cref="UUID"/> of the transfer, can be used to correlate the upload with
        /// events being fired</returns>
        public UUID RequestUpload(AssetType type, byte[] data, bool storeLocal)
        {
            UUID assetID;
            return RequestUpload(out assetID, type, data, storeLocal);
        }

        /// <summary>
        /// Request an asset be uploaded to the simulator
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="type">Asset type to upload this data as</param>
        /// <param name="data">A byte array containing the encoded asset data</param>
        /// <param name="storeLocal">If True, the asset once uploaded will be stored on the simulator
        /// in which the client was connected in addition to being stored on the asset server</param>
        /// <returns>The <see cref="UUID"/> of the transfer, can be used to correlate the upload with
        /// events being fired</returns>
        public UUID RequestUpload(out UUID assetID, AssetType type, byte[] data, bool storeLocal)
        {
            return RequestUpload(out assetID, type, data, storeLocal, UUID.Random());
        }

        /// <summary>
        /// Initiate an asset upload
        /// </summary>
        /// <param name="assetID">The ID this asset will have if the
        /// upload succeeds</param>
        /// <param name="type">Asset type to upload this data as</param>
        /// <param name="data">Raw asset data to upload</param>
        /// <param name="storeLocal">Whether to store this asset on the local
        /// simulator or the grid-wide asset server</param>
        /// <param name="transactionID">The tranaction id for the upload <see cref="RequestCreateItem"/></param>
        /// <returns>The transaction ID of this transfer</returns>
        public UUID RequestUpload(out UUID assetID, AssetType type, byte[] data, bool storeLocal, UUID transactionID)
        {
            AssetUpload upload = new AssetUpload
            {
                AssetData = data,
                AssetType = type
            };
            assetID = UUID.Combine(transactionID, Client.Self.SecureSessionID);
            upload.AssetID = assetID;
            upload.Size = data.Length;
            upload.XferID = 0;
            upload.ID = transactionID;

            // Build and send the upload packet
            AssetUploadRequestPacket request = new AssetUploadRequestPacket
            {
                AssetBlock =
                {
                    StoreLocal = storeLocal,
                    Tempfile = false, // This field is deprecated
                    TransactionID = transactionID,
                    Type = (sbyte)type
                }
            };

            bool isMultiPacketUpload;
            if (data.Length + 100 < Settings.MAX_PACKET_SIZE)
            {
                isMultiPacketUpload = false;
                Logger.Log(
                    String.Format("Beginning asset upload [Single Packet], ID: {0}, AssetID: {1}, Size: {2}",
                    upload.ID.ToString(), upload.AssetID.ToString(), upload.Size), Helpers.LogLevel.Info, Client);

                lock (Transfers) Transfers[upload.ID] = upload;

                // The whole asset will fit in this packet, makes things easy
                request.AssetBlock.AssetData = data;
                upload.Transferred = data.Length;
            }
            else
            {
                isMultiPacketUpload = true;
                Logger.Log(
                    String.Format("Beginning asset upload [Multiple Packets], ID: {0}, AssetID: {1}, Size: {2}",
                    upload.ID.ToString(), upload.AssetID.ToString(), upload.Size), Helpers.LogLevel.Info, Client);

                // Asset is too big, send in multiple packets
                request.AssetBlock.AssetData = Utils.EmptyBytes;
            }

            // Wait for the previous upload to receive a RequestXferPacket
            lock (PendingUploadLock)
            {
                const int UPLOAD_CONFIRM_TIMEOUT = 20 * 1000;
                const int SLEEP_INTERVAL = 50;
                int t = 0;
                while (WaitingForUploadConfirm && t < UPLOAD_CONFIRM_TIMEOUT)
                {
                    System.Threading.Thread.Sleep(SLEEP_INTERVAL);
                    t += SLEEP_INTERVAL;
                }

                if (t < UPLOAD_CONFIRM_TIMEOUT)
                {
                    if (isMultiPacketUpload)
                    {
                        WaitingForUploadConfirm = true;
                    }
                    PendingUpload = upload;
                    Client.Network.SendPacket(request);

                    return upload.ID;
                }
                else
                {
                    throw new Exception("Timeout waiting for previous asset upload to begin");
                }
            }
        }

        public void RequestUploadBakedTexture(byte[] textureData, BakedTextureUploadedCallback callback)
        {
            Uri cap = null;
            if(Client.Network.CurrentSim.Caps != null) {
                cap = Client.Network.CurrentSim.Caps.CapabilityURI("UploadBakedTexture");
            }
            if (cap != null)
            {
                Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, new OSD(), CancellationToken.None,
                    (response, data, error) =>
                {
                    if (error == null)
                    {
                        Logger.Log("Bake upload failed during uploader retrieval", Helpers.LogLevel.Warning, Client, error);
                        callback(UUID.Zero);
                        return;
                    }

                    OSD result = OSDParser.Deserialize(data);
                    if (result is OSDMap resultMap)
                    {
                        UploadBakedTextureMessage message = new UploadBakedTextureMessage();
                        message.Deserialize(resultMap);

                        if (message.Request.State == "upload")
                        {
                            Uri uploadUrl = ((UploaderRequestUpload)message.Request).Url;

                            if (uploadUrl != null)
                            {
                                // POST the asset data
                                Task postReq = Client.HttpCapsClient.PostRequestAsync(uploadUrl, "application/octet-stream", textureData, 
                                    CancellationToken.None, (responseMessage, responseData, except) =>
                                    {
                                        if (except != null)
                                        {
                                            Logger.Log("Bake upload failed during asset upload", Helpers.LogLevel.Warning, Client, except);
                                            callback(UUID.Zero);
                                            return;
                                        }

                                        OSD d = OSDParser.Deserialize(responseData);
                                        if (d is OSDMap map)
                                        {
                                            UploadBakedTextureMessage message2 = new UploadBakedTextureMessage();
                                            message2.Deserialize(map);

                                            if (message2.Request.State == "complete")
                                            {
                                                callback(((UploaderRequestComplete)message2.Request).AssetID);
                                                return;
                                            }
                                        }

                                        Logger.Log("Bake upload failed during asset upload", Helpers.LogLevel.Warning, Client);
                                        callback(UUID.Zero);
                                    });
                                return;
                            }
                        }
                    }

                    Logger.Log("Bake upload failed during uploader retrieval", Helpers.LogLevel.Warning, Client);
                    callback(UUID.Zero);
                });
            }
            else
            {
                Logger.Log("UploadBakedTexture not available, falling back to UDP method", Helpers.LogLevel.Info, Client);

                ThreadPool.QueueUserWorkItem(
                    delegate(object o)
                    {
                        UUID transactionID = UUID.Random();
                        BakedTextureUploadedCallback uploadCallback = (BakedTextureUploadedCallback)o;
                        AutoResetEvent uploadEvent = new AutoResetEvent(false);

                        void UdpCallback(object sender, AssetUploadEventArgs e)
                        {
                            if (e.Upload.ID == transactionID)
                            {
                                uploadEvent.Set();
                                uploadCallback(e.Upload.Success ? e.Upload.AssetID : UUID.Zero);
                            }
                        }

                        AssetUploaded += UdpCallback;

                        UUID assetID;
                        bool success;

                        try
                        {
                            RequestUpload(out assetID, AssetType.Texture, textureData, true, transactionID);
                            success = uploadEvent.WaitOne(Client.Settings.TRANSFER_TIMEOUT, false);
                        }
                        catch (Exception)
                        {
                            success = false;
                        }

                        AssetUploaded -= UdpCallback;

                        if (!success)
                        {
                            uploadCallback(UUID.Zero);
                        }
                    }, callback
                );
            }
        }

#endregion Uploads

        /// <summary>
        /// Requests download of a mesh asset
        /// </summary>
        /// <param name="meshID">UUID of the mesh asset</param>
        /// <param name="callback">Callback when the request completes</param>
        public void RequestMesh(UUID meshID, MeshDownloadCallback callback)
        {
            if (meshID == UUID.Zero || callback == null)
                return;

            if (Client.Network.CurrentSim?.Caps?.GetMeshCapURI() != null)
            {
                // Do we have this mesh asset in the cache?
                if (Client.Assets.Cache.HasAsset(meshID))
                {
                    callback(true, new AssetMesh(meshID, Client.Assets.Cache.GetCachedAssetBytes(meshID)));
                    return;
                }

                DownloadRequest req = new DownloadRequest(
                    new Uri($"{Client.Network.CurrentSim.Caps.GetMeshCapURI()}" +
                            $"?mesh_id={meshID}"),
                    null,
                    null,
                    (response, responseData, error) =>
                    {
                        if (error == null && responseData != null && response.IsSuccessStatusCode) // success
                        {
                            callback(true, new AssetMesh(meshID, responseData));
                            Client.Assets.Cache.SaveAssetToCache(meshID, responseData);
                        }
                        else // download failed
                        {
                            Logger.Log(
                                $"Failed to fetch mesh asset {meshID}: {((error == null) ? "" : error.Message)}",
                                Helpers.LogLevel.Warning, Client);
                        }
                    }
                );

                HttpDownloads.QueueDownload(req);
            }
            else
            {
                Logger.Log("Mesh fetch capabilities not available", Helpers.LogLevel.Error, Client);
                callback(false, null);
            }
        }

#region Texture Downloads

        /// <summary>
        /// Request a texture asset from the simulator using the <see cref="TexturePipeline"/> system to 
        /// manage the requests and re-assemble the image from the packets received from the simulator
        /// </summary>
        /// <param name="textureID">The <see cref="UUID"/> of the texture asset to download</param>
        /// <param name="imageType">The <see cref="ImageType"/> of the texture asset. 
        /// Use <see cref="ImageType.Normal"/> for most textures, or <see cref="ImageType.Baked"/> for baked layer texture assets</param>
        /// <param name="priority">A float indicating the requested priority for the transfer. Higher priority values tell the simulator
        /// to prioritize the request before lower valued requests. An image already being transferred using the <see cref="TexturePipeline"/> can have
        /// its priority changed by resending the request with the new priority value</param>
        /// <param name="discardLevel">Number of quality layers to discard.
        /// This controls the end marker of the data sent. Sending with value -1 combined with priority of 0 cancels an in-progress
        /// transfer.</param>
        /// <remarks>A bug exists in the Linden Simulator where a -1 will occasionally be sent with a non-zero priority
        /// indicating an off-by-one error.</remarks>
        /// <param name="packetStart">The packet number to begin the request at. A value of 0 begins the request
        /// from the start of the asset texture</param>
        /// <param name="callback">The <see cref="TextureDownloadCallback"/> callback to fire when the image is retrieved. The callback
        /// will contain the result of the request and the texture asset data</param>
        /// <param name="progress">If true, the callback will be fired for each chunk of the downloaded image. 
        /// The callback asset parameter will contain all previously received chunks of the texture asset starting 
        /// from the beginning of the request</param>
        /// <example>
        /// Request an image and fire a callback when the request is complete
        /// <code>
        /// Client.Assets.RequestImage(UUID.Parse("c307629f-e3a1-4487-5e88-0d96ac9d4965"), ImageType.Normal, TextureDownloader_OnDownloadFinished);
        /// 
        /// private void TextureDownloader_OnDownloadFinished(TextureRequestState state, AssetTexture asset)
        /// {
        ///     if(state == TextureRequestState.Finished)
        ///     {
        ///       Console.WriteLine("Texture {0} ({1} bytes) has been successfully downloaded", 
        ///         asset.AssetID,
        ///         asset.AssetData.Length); 
        ///     }
        /// }
        /// </code>
        /// Request an image and use an inline anonymous method to handle the downloaded texture data
        /// <code>
        /// Client.Assets.RequestImage(UUID.Parse("c307629f-e3a1-4487-5e88-0d96ac9d4965"), ImageType.Normal, delegate(TextureRequestState state, AssetTexture asset) 
        ///                                         {
        ///                                             if(state == TextureRequestState.Finished)
        ///                                             {
        ///                                                 Console.WriteLine("Texture {0} ({1} bytes) has been successfully downloaded", 
        ///                                                 asset.AssetID,
        ///                                                 asset.AssetData.Length); 
        ///                                             }
        ///                                         }
        /// );
        /// </code>
        /// Request a texture, decode the texture to a bitmap image and apply it to a imagebox 
        /// <code>
        /// Client.Assets.RequestImage(UUID.Parse("c307629f-e3a1-4487-5e88-0d96ac9d4965"), ImageType.Normal, TextureDownloader_OnDownloadFinished);
        /// 
        /// private void TextureDownloader_OnDownloadFinished(TextureRequestState state, AssetTexture asset)
        /// {
        ///     if(state == TextureRequestState.Finished)
        ///     {
        ///         ManagedImage imgData;
        ///         Image bitmap;
        ///
        ///         if (state == TextureRequestState.Finished)
        ///         {
        ///             OpenJPEG.DecodeToImage(assetTexture.AssetData, out imgData, out bitmap);
        ///             picInsignia.Image = bitmap;
        ///         }               
        ///     }
        /// }
        /// </code>
        /// </example>
        public void RequestImage(UUID textureID, ImageType imageType, float priority, int discardLevel,
            uint packetStart, TextureDownloadCallback callback, bool progress)
        {
            if (Client.Settings.USE_HTTP_TEXTURES
                && Client.Network.CurrentSim?.Caps?.GetTextureCapURI() != null)
            {
                HttpRequestTexture(textureID, imageType, priority, discardLevel, packetStart, callback, progress);
            }
            else
            {
                Texture.RequestTexture(textureID, imageType, priority, discardLevel, packetStart, callback, progress);
            }
        }

        /// <summary>
        /// Overload: Request a texture asset from the simulator using the <see cref="TexturePipeline"/> system to 
        /// manage the requests and re-assemble the image from the packets received from the simulator
        /// </summary>
        /// <param name="textureID">The <see cref="UUID"/> of the texture asset to download</param>
        /// <param name="callback">The <see cref="TextureDownloadCallback"/> callback to fire when the image is retrieved. The callback
        /// will contain the result of the request and the texture asset data</param>
        public void RequestImage(UUID textureID, TextureDownloadCallback callback)
        {
            RequestImage(textureID, ImageType.Normal, 101300.0f, 0, 0, callback, false);
        }

        /// <summary>
        /// Overload: Request a texture asset from the simulator using the <see cref="TexturePipeline"/> system to 
        /// manage the requests and re-assemble the image from the packets received from the simulator
        /// </summary>
        /// <param name="textureID">The <see cref="UUID"/> of the texture asset to download</param>
        /// <param name="imageType">The <see cref="ImageType"/> of the texture asset. 
        /// Use <see cref="ImageType.Normal"/> for most textures, or <see cref="ImageType.Baked"/> for baked layer texture assets</param>
        /// <param name="callback">The <see cref="TextureDownloadCallback"/> callback to fire when the image is retrieved. The callback
        /// will contain the result of the request and the texture asset data</param>
        public void RequestImage(UUID textureID, ImageType imageType, TextureDownloadCallback callback)
        {
            RequestImage(textureID, imageType, 101300.0f, 0, 0, callback, false);
        }

        /// <summary>
        /// Overload: Request a texture asset from the simulator using the <see cref="TexturePipeline"/> system to 
        /// manage the requests and re-assemble the image from the packets received from the simulator
        /// </summary>
        /// <param name="textureID">The <see cref="UUID"/> of the texture asset to download</param>
        /// <param name="imageType">The <see cref="ImageType"/> of the texture asset. 
        /// Use <see cref="ImageType.Normal"/> for most textures, or <see cref="ImageType.Baked"/> for baked layer texture assets</param>
        /// <param name="callback">The <see cref="TextureDownloadCallback"/> callback to fire when the image is retrieved. The callback
        /// will contain the result of the request and the texture asset data</param>
        /// <param name="progress">If true, the callback will be fired for each chunk of the downloaded image. 
        /// The callback asset parameter will contain all previously received chunks of the texture asset starting 
        /// from the beginning of the request</param>
        public void RequestImage(UUID textureID, ImageType imageType, TextureDownloadCallback callback, bool progress)
        {
            RequestImage(textureID, imageType, 101300.0f, 0, 0, callback, progress);
        }

        /// <summary>
        /// Cancel a texture request
        /// </summary>
        /// <param name="textureID">The texture assets <see cref="UUID"/></param>
        public void RequestImageCancel(UUID textureID)
        {
            Texture.AbortTextureRequest(textureID);
        }

        /// <summary>
        /// Fetch avatar texture on a grid capable of server side baking
        /// </summary>
        /// <param name="avatarID">ID of the avatar</param>
        /// <param name="textureID">ID of the texture</param>
        /// <param name="bakeName">Name of the part of the avatar texture applies to</param>
        /// <param name="callback">Callback invoked on operation completion</param>
        public void RequestServerBakedImage(UUID avatarID, UUID textureID, string bakeName, TextureDownloadCallback callback)
        {
            if (avatarID == UUID.Zero || textureID == UUID.Zero || callback == null)
                return;

            if (string.IsNullOrEmpty(Client.Network.AgentAppearanceServiceURL))
            {
                callback(TextureRequestState.NotFound, null);
                return;
            }

            byte[] assetData;
            // Do we have this image in the cache?
            if (Client.Assets.Cache.HasAsset(textureID)
                && (assetData = Client.Assets.Cache.GetCachedAssetBytes(textureID)) != null)
            {
                ImageDownload image = new ImageDownload {ID = textureID, AssetData = assetData};
                image.Size = image.AssetData.Length;
                image.Transferred = image.AssetData.Length;
                image.ImageType = ImageType.ServerBaked;
                image.AssetType = AssetType.Texture;
                image.Success = true;

                callback(TextureRequestState.Finished, new AssetTexture(image.ID, image.AssetData));
                FireImageProgressEvent(image.ID, image.Transferred, image.Size);
                return;
            }

            DownloadProgressHandler progressHandler = null;

            Uri url = new Uri($"{Client.Network.AgentAppearanceServiceURL}texture/{avatarID}/{bakeName}/{textureID}");

            DownloadRequest req = new DownloadRequest(
                url,
                "image/x-j2c",
                progressHandler,
                (response, responseData, error) =>
                {
                    if (error == null && responseData != null && response.IsSuccessStatusCode) // success
                    {
                        ImageDownload image = new ImageDownload {ID = textureID, AssetData = responseData};
                        image.Size = image.AssetData.Length;
                        image.Transferred = image.AssetData.Length;
                        image.ImageType = ImageType.ServerBaked;
                        image.AssetType = AssetType.Texture;
                        image.Success = true;

                        callback(TextureRequestState.Finished, new AssetTexture(image.ID, image.AssetData));

                        Client.Assets.Cache.SaveAssetToCache(textureID, responseData);
                    }
                    else // download failed
                    {
                        Logger.Log(
                            $"Failed to fetch server bake {textureID}: {((error == null) ? "" : error.Message)}",
                            Helpers.LogLevel.Warning, Client);

                        callback(TextureRequestState.Timeout, null);
                    }
                }
            );

            HttpDownloads.QueueDownload(req);

        }

        /// <summary>
        /// Lets TexturePipeline class fire the progress event
        /// </summary>
        /// <param name="textureID">The texture ID currently being downloaded</param>
        /// <param name="transferredBytes">the number of bytes transferred</param>
        /// <param name="totalBytes">the total number of bytes expected</param>
        internal void FireImageProgressEvent(UUID textureID, int transferredBytes, int totalBytes)
        {
            try { OnImageReceiveProgress(new ImageReceiveProgressEventArgs(textureID, transferredBytes, totalBytes)); }
            catch (Exception e) { Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e); }
        }

        // Helper method for downloading textures via GetTexture cap
        // Same signature as the UDP variant since we need all the params to
        // pass to the UDP TexturePipeline in case we need to fall back to it
        // (Linden servers currently (1.42) don't support bakes downloads via HTTP)
        private void HttpRequestTexture(UUID textureID, ImageType imageType, float priority, int discardLevel,
    uint packetStart, TextureDownloadCallback callback, bool progress)
        {
            if (textureID == UUID.Zero || callback == null)
                return;

            byte[] assetData;
            // Do we have this image in the cache?
            if (Client.Assets.Cache.HasAsset(textureID)
                && (assetData = Client.Assets.Cache.GetCachedAssetBytes(textureID)) != null)
            {
                ImageDownload image = new ImageDownload {ID = textureID, AssetData = assetData};
                image.Size = image.AssetData.Length;
                image.Transferred = image.AssetData.Length;
                image.ImageType = imageType;
                image.AssetType = AssetType.Texture;
                image.Success = true;

                callback(TextureRequestState.Finished, new AssetTexture(image.ID, image.AssetData));
                FireImageProgressEvent(image.ID, image.Transferred, image.Size);
                return;
            }

            DownloadProgressHandler progressHandler = null;

            if (progress)
            {
                progressHandler = (totalBytesToReceive, bytesReceived, progresPercent) =>
                    {
                        FireImageProgressEvent(textureID, (int)bytesReceived, (int)totalBytesToReceive);
                    };
            }

            DownloadRequest req = new DownloadRequest(
                new Uri($"{Client.Network.CurrentSim.Caps.GetTextureCapURI()}?texture_id={textureID}"),
                "image/x-j2c",
                progressHandler,
                (response, responseData, error) =>
                {
                    if (error == null && responseData != null && response.IsSuccessStatusCode) // success
                    {
                        ImageDownload image = new ImageDownload {ID = textureID, AssetData = responseData};
                        image.Size = image.AssetData.Length;
                        image.Transferred = image.AssetData.Length;
                        image.ImageType = imageType;
                        image.AssetType = AssetType.Texture;
                        image.Success = true;

                        callback(TextureRequestState.Finished, new AssetTexture(image.ID, image.AssetData));
                        FireImageProgressEvent(image.ID, image.Transferred, image.Size);

                        Client.Assets.Cache.SaveAssetToCache(textureID, responseData);
                    }
                    else // download failed
                    {
                        Logger.Log(
                            $"Failed to fetch texture {textureID} over HTTP, falling back to UDP: " +
                            $"{((error == null) ? "" : error.Message)}",
                            Helpers.LogLevel.Warning, Client);

                        Texture.RequestTexture(textureID, imageType, priority, discardLevel, packetStart, callback, progress);
                    }
                }
            );

            HttpDownloads.QueueDownload(req);
        }

#endregion Texture Downloads

#region Helpers

        public Asset CreateAssetWrapper(AssetType type)
        {
            Asset asset;

            switch (type)
            {
                case AssetType.Notecard:
                    asset = new AssetNotecard();
                    break;
                case AssetType.LSLText:
                    asset = new AssetScriptText();
                    break;
                case AssetType.LSLBytecode:
                    asset = new AssetScriptBinary();
                    break;
                case AssetType.Texture:
                    asset = new AssetTexture();
                    break;
                case AssetType.Object:
                    asset = new AssetPrim();
                    break;
                case AssetType.Clothing:
                    asset = new AssetClothing();
                    break;
                case AssetType.Bodypart:
                    asset = new AssetBodypart();
                    break;
                case AssetType.Animation:
                    asset = new AssetAnimation();
                    break;
                case AssetType.Sound:
                    asset = new AssetSound();
                    break;
                case AssetType.Landmark:
                    asset = new AssetLandmark();
                    break;
                case AssetType.Gesture:
                    asset = new AssetGesture();
                    break;
                case AssetType.CallingCard:
                    asset = new AssetCallingCard();
                    break;
                case AssetType.Settings:
                    asset = new AssetSettings();
                    break;
                default:
                    asset = new AssetMutable(type);
                    Logger.Log("Unimplemented asset type: " + type, Helpers.LogLevel.Error);
                    break;
            }

            return asset;
        }

        private Asset WrapAsset(AssetDownload download)
        {
            Asset asset = CreateAssetWrapper(download.AssetType);
            if (asset != null)
            {
                asset.AssetID = download.AssetID;
                asset.AssetData = download.AssetData;
                return asset;
            }
            else
            {
                return null;
            }
        }

        private void SendNextUploadPacket(AssetUpload upload)
        {
            SendXferPacketPacket send = new SendXferPacketPacket
            {
                XferID =
                {
                    ID = upload.XferID,
                    Packet = upload.PacketNum++
                }
            };

            if (send.XferID.Packet == 0)
            {
                // The first packet reserves the first four bytes of the data for the
                // total length of the asset and appends 1000 bytes of data after that
                send.DataPacket.Data = new byte[1004];
                Buffer.BlockCopy(Utils.IntToBytes(upload.Size), 0, send.DataPacket.Data, 0, 4);
                Buffer.BlockCopy(upload.AssetData, 0, send.DataPacket.Data, 4, 1000);
                upload.Transferred += 1000;

                lock (Transfers)
                {
                    Transfers.Remove(upload.AssetID);
                    Transfers[upload.ID] = upload;
                }
            }
            else if ((send.XferID.Packet + 1) * 1000 < upload.Size)
            {
                // This packet is somewhere in the middle of the transfer, or a perfectly
                // aligned packet at the end of the transfer
                send.DataPacket.Data = new byte[1000];
                Buffer.BlockCopy(upload.AssetData, upload.Transferred, send.DataPacket.Data, 0, 1000);
                upload.Transferred += 1000;
            }
            else
            {
                // Special handler for the last packet which will be less than 1000 bytes
                int lastlen = upload.Size - ((int)send.XferID.Packet * 1000);
                send.DataPacket.Data = new byte[lastlen];
                Buffer.BlockCopy(upload.AssetData, (int)send.XferID.Packet * 1000, send.DataPacket.Data, 0, lastlen);
                send.XferID.Packet |= (uint)0x80000000; // This signals the final packet
                upload.Transferred += lastlen;
            }

            Client.Network.SendPacket(send);
        }

        private void SendConfirmXferPacket(ulong xferID, uint packetNum)
        {
            ConfirmXferPacketPacket confirm = new ConfirmXferPacketPacket
            {
                XferID =
                {
                    ID = xferID,
                    Packet = packetNum
                }
            };

            Client.Network.SendPacket(confirm);
        }

        /// <summary>
        /// Returns whether asset type can be fetched directly from the asset server endpoint
        /// </summary>
        /// <param name="assetType">The asset's type</param>
        /// <returns>Whether this type can be fetched directly</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static bool CanFetchAsset(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.Texture:
                case AssetType.Sound:
                case AssetType.Landmark:
                case AssetType.Clothing:
                case AssetType.Bodypart:
                case AssetType.Animation:
                case AssetType.Gesture:
                case AssetType.Settings: 
                case AssetType.Material:
                    return true;
                
                case AssetType.Unknown:
                case AssetType.CallingCard:
#pragma warning disable CS0618 // Type or member is obsolete
                case AssetType.Script:
#pragma warning restore CS0618 // Type or member is obsolete
                case AssetType.Object:
                case AssetType.Notecard:
                case AssetType.Folder:
                case AssetType.LSLText:
                case AssetType.LSLBytecode:
                case AssetType.TextureTGA:
                case AssetType.SoundWAV:
                case AssetType.ImageTGA:
                case AssetType.ImageJPEG:
                case AssetType.Simstate:
                case AssetType.Link:
                case AssetType.LinkFolder:
                case AssetType.Mesh:
                case AssetType.Widget:
                case AssetType.Person:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
            }
        }
        
#endregion Helpers

#region Transfer Callbacks

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void TransferInfoHandler(object sender, PacketReceivedEventArgs e)
        {
            var info = (TransferInfoPacket)e.Packet;
            Transfer transfer;

            bool success;
            lock (Transfers) success = Transfers.TryGetValue(info.TransferInfo.TransferID, out transfer);

            if (success)
            {
                var download = (AssetDownload)transfer;

                if (download.Callback == null) return;

                download.Channel = (ChannelType)info.TransferInfo.ChannelType;
                download.Status = (StatusCode)info.TransferInfo.Status;
                download.Target = (TargetType)info.TransferInfo.TargetType;
                download.Size = info.TransferInfo.Size;

                // TODO: Once we support mid-transfer status checking and aborting this
                // will need to become smarter
                if (download.Status != StatusCode.OK)
                {
                    Logger.Log("Transfer failed with status code " + download.Status, Helpers.LogLevel.Warning, Client);

                    lock (Transfers) Transfers.Remove(download.ID);

                    // No data could have been received before the TransferInfo packet
                    download.AssetData = null;

                    // Fire the event with our transfer that contains Success = false;
                    try { download.Callback(download, null); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                }
                else
                {
                    download.AssetData = new byte[download.Size];

                    if (download.Source == SourceType.Asset && info.TransferInfo.Params.Length == 20)
                    {
                        download.AssetID = new UUID(info.TransferInfo.Params, 0);
                        download.AssetType = (AssetType)(sbyte)info.TransferInfo.Params[16];

                        //Client.DebugLog(String.Format("TransferInfo packet received. AssetID: {0} Type: {1}",
                        //    transfer.AssetID, type));
                    }
                    else if (download.Source == SourceType.SimInventoryItem && info.TransferInfo.Params.Length == 100)
                    {
                        // TODO: Can we use these?
                        //UUID agentID = new UUID(info.TransferInfo.Params, 0);
                        //UUID sessionID = new UUID(info.TransferInfo.Params, 16);
                        //UUID ownerID = new UUID(info.TransferInfo.Params, 32);
                        //UUID taskID = new UUID(info.TransferInfo.Params, 48);
                        //UUID itemID = new UUID(info.TransferInfo.Params, 64);
                        download.AssetID = new UUID(info.TransferInfo.Params, 80);
                        download.AssetType = (AssetType)(sbyte)info.TransferInfo.Params[96];

                        //Client.DebugLog(String.Format("TransferInfo packet received. AgentID: {0} SessionID: {1} " + 
                        //    "OwnerID: {2} TaskID: {3} ItemID: {4} AssetID: {5} Type: {6}", agentID, sessionID, 
                        //    ownerID, taskID, itemID, transfer.AssetID, type));
                    }
                    else
                    {
                        Logger.Log("Received a TransferInfo packet with a SourceType of " + download.Source +
                            " and a Params field length of " + info.TransferInfo.Params.Length,
                            Helpers.LogLevel.Warning, Client);
                    }
                }
                download.HeaderReceivedEvent.Set();
            }
            else
            {
                Logger.Log("Received a TransferInfo packet for an asset we didn't request, TransferID: " +
                    info.TransferInfo.TransferID, Helpers.LogLevel.Warning, Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void TransferPacketHandler(object sender, PacketReceivedEventArgs e)
        {
            TransferPacketPacket asset = (TransferPacketPacket)e.Packet;
            Transfer transfer;

            bool success;
            lock (Transfers) success = Transfers.TryGetValue(asset.TransferData.TransferID, out transfer);

            // skip if we couldn't find the transfer
            if (!success) return;
            
            var download = (AssetDownload)transfer;
            if (download.Size == 0)
            {
                Logger.DebugLog("TransferPacket received ahead of the transfer header, blocking...", Client);

                // We haven't received the header yet, block until it's received or times out
                download.HeaderReceivedEvent.WaitOne(TRANSFER_HEADER_TIMEOUT, false);

                if (download.Size == 0)
                {
                    Logger.Log("Timed out while waiting for the asset header to download for " +
                               download.ID, Helpers.LogLevel.Warning, Client);

                    // Abort the transfer
                    TransferAbortPacket abort = new TransferAbortPacket
                    {
                        TransferInfo =
                        {
                            ChannelType = (int)download.Channel,
                            TransferID = download.ID
                        }
                    };
                    Client.Network.SendPacket(abort, download.Simulator);

                    download.Success = false;
                    lock (Transfers) Transfers.Remove(download.ID);

                    // Fire the event with our transfer that contains Success = false
                    if (download.Callback != null)
                    {
                        try { download.Callback(download, null); }
                        catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                    }

                    return;
                }
            }

            // If packets arrive out of order, we add them to the out of order packet directory
            // until all previous packets have arrived
            try
            {
                if (download.nextPacket == asset.TransferData.Packet)
                {
                    byte[] data = asset.TransferData.Data;
                    do
                    {
                        Buffer.BlockCopy(data, 0, download.AssetData, download.Transferred, data.Length);
                        download.Transferred += data.Length;
                        download.nextPacket++;
                    } while (download.outOfOrderPackets.TryGetValue(download.nextPacket, out data));
                }
                else
                {
                    //Logger.Log(string.Format("Fixing out of order packet {0} when expecting {1}!", asset.TransferData.Packet, download.nextPacket), Helpers.LogLevel.Debug);
                    download.outOfOrderPackets.Add(asset.TransferData.Packet, asset.TransferData.Data);
                }
            }
            catch (ArgumentException)
            {
                Logger.Log(String.Format("TransferPacket handling failed. TransferData.Data.Length={0}, AssetData.Length={1}, TransferData.Packet={2}",
                    asset.TransferData.Data.Length, download.AssetData.Length, asset.TransferData.Packet), Helpers.LogLevel.Error);
                return;
            }

            //Client.DebugLog(String.Format("Transfer packet {0}, received {1}/{2}/{3} bytes for asset {4}",
            //    asset.TransferData.Packet, asset.TransferData.Data.Length, transfer.Transferred, transfer.Size,
            //    transfer.AssetID.ToString()));

            // Check if we downloaded the full asset
            if (download.Transferred >= download.Size)
            {
                Logger.DebugLog($"Transfer for asset {download.AssetID} completed", Client);

                download.Success = true;
                lock (Transfers) Transfers.Remove(download.ID);

                // Cache successful asset download
                Cache.SaveAssetToCache(download.AssetID, download.AssetData);

                if (download.Callback != null)
                {
                    try { download.Callback(download, WrapAsset(download)); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                }
            }
        }

#endregion Transfer Callbacks

#region Xfer Callbacks

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void InitiateDownloadPacketHandler(object sender, PacketReceivedEventArgs e)
        {
            InitiateDownloadPacket request = (InitiateDownloadPacket)e.Packet;
            try
            {
                OnInitiateDownload(new InitiateDownloadEventArgs(Utils.BytesToString(request.FileData.SimFilename),
                    Utils.BytesToString(request.FileData.ViewerFilename)));
            }
            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void RequestXferHandler(object sender, PacketReceivedEventArgs e)
        {
            if (PendingUpload == null)
                Logger.Log("Received a RequestXferPacket for an unknown asset upload", Helpers.LogLevel.Warning, Client);
            else
            {
                AssetUpload upload = PendingUpload;
                PendingUpload = null;
                WaitingForUploadConfirm = false;
                RequestXferPacket request = (RequestXferPacket)e.Packet;

                upload.XferID = request.XferID.ID;
                upload.Type = (AssetType)request.XferID.VFileType;

                UUID transferID = new UUID(upload.XferID);
                lock (Transfers) Transfers[transferID] = upload;

                // Send the first packet containing actual asset data
                SendNextUploadPacket(upload);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ConfirmXferPacketHandler(object sender, PacketReceivedEventArgs e)
        {
            var confirm = (ConfirmXferPacketPacket)e.Packet;

            // Building a new UUID every time an ACK is received for an upload is a horrible
            // thing, but this whole Xfer system is horrible
            UUID transferID = new UUID(confirm.XferID.ID);
            Transfer transfer;

            bool success;
            lock (Transfers) success = Transfers.TryGetValue(transferID, out transfer);

            // skip if we couldn't find the transfer
            if (!success) return;
            var upload = (AssetUpload)transfer;

            //Client.DebugLog(String.Format("ACK for upload {0} of asset type {1} ({2}/{3})",
            //    upload.AssetID.ToString(), upload.Type, upload.Transferred, upload.Size));

            try { OnUploadProgress(new AssetUploadEventArgs(upload)); }
            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }

            if (upload.Transferred < upload.Size)
                SendNextUploadPacket(upload);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AssetUploadCompleteHandler(object sender, PacketReceivedEventArgs e)
        {
            AssetUploadCompletePacket complete = (AssetUploadCompletePacket)e.Packet;

            // If we uploaded an asset in a single packet, RequestXferHandler()
            // will never be called so we need to set this here as well
            WaitingForUploadConfirm = false;

            if (m_AssetUploadedEvent != null)
            {
                var found = false;
                var foundTransfer = new KeyValuePair<UUID, Transfer>();

                // Xfer system sucks really really bad. Where is the damn XferID?
                lock (Transfers)
                {
                    foreach (var transfer in Transfers)
                    {
                        if (transfer.Value.GetType() == typeof(AssetUpload))
                        {
                            AssetUpload upload = (AssetUpload)transfer.Value;

                            if (upload.AssetID == complete.AssetBlock.UUID)
                            {
                                found = true;
                                foundTransfer = transfer;
                                upload.Success = complete.AssetBlock.Success;
                                upload.Type = (AssetType)complete.AssetBlock.Type;
                                break;
                            }
                        }
                    }
                }

                if (found)
                {
                    lock (Transfers) Transfers.Remove(foundTransfer.Key);

                    try { OnAssetUploaded(new AssetUploadEventArgs((AssetUpload)foundTransfer.Value)); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                }
                else
                {
                    Logger.Log(String.Format(
                        "Got an AssetUploadComplete on an unrecognized asset, AssetID: {0}, Type: {1}, Success: {2}",
                        complete.AssetBlock.UUID, (AssetType)complete.AssetBlock.Type, complete.AssetBlock.Success),
                        Helpers.LogLevel.Warning);
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void SendXferPacketHandler(object sender, PacketReceivedEventArgs e)
        {
            var xfer = (SendXferPacketPacket)e.Packet;

            // Lame ulong to UUID conversion, please go away Xfer system
            UUID transferID = new UUID(xfer.XferID.ID);
            Transfer transfer;

            bool success;
            lock (Transfers) success = Transfers.TryGetValue(transferID, out transfer);

            // skip if we couldn't find the transfer
            if (!success) return;
            
            var download = (XferDownload)transfer;

            // Apply a mask to get rid of the "end of transfer" bit
            uint packetNum = xfer.XferID.Packet & 0x0FFFFFFF;

            // Check for out of order packets, possibly indicating a resend
            if (packetNum != download.PacketNum)
            {
                if (packetNum == download.PacketNum - 1)
                {
                    Logger.DebugLog("Resending Xfer download confirmation for packet " + packetNum, Client);
                    SendConfirmXferPacket(download.XferID, packetNum);
                }
                else
                {
                    Logger.Log("Out of order Xfer packet in a download, got " + packetNum + " expecting " + download.PacketNum,
                        Helpers.LogLevel.Warning, Client);
                    // Re-confirm the last packet we actually received
                    SendConfirmXferPacket(download.XferID, download.PacketNum - 1);
                }

                return;
            }

            if (packetNum == 0)
            {
                // This is the first packet received in the download, the first four bytes are a size integer
                // in little endian ordering
                byte[] bytes = xfer.DataPacket.Data;
                download.Size = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24));
                download.AssetData = new byte[download.Size];

                Logger.DebugLog("Received first packet in an Xfer download of size " + download.Size);

                Buffer.BlockCopy(xfer.DataPacket.Data, 4, download.AssetData, 0, xfer.DataPacket.Data.Length - 4);
                download.Transferred += xfer.DataPacket.Data.Length - 4;
            }
            else
            {
                Buffer.BlockCopy(xfer.DataPacket.Data, 0, download.AssetData, 1000 * (int)packetNum, xfer.DataPacket.Data.Length);
                download.Transferred += xfer.DataPacket.Data.Length;
            }

            // Increment the packet number to the packet we are expecting next
            download.PacketNum++;

            // Confirm receiving this packet
            SendConfirmXferPacket(download.XferID, packetNum);

            if ((xfer.XferID.Packet & 0x80000000) != 0)
            {
                // This is the last packet in the transfer
                Logger.DebugLog($"Xfer download for asset " +
                                $"{(string.IsNullOrEmpty(download.Filename) ? download.VFileID.ToString() : download.Filename)} completed",
                                Client);

                download.Success = true;
                lock (Transfers) Transfers.Remove(download.ID);

                try { OnXferReceived(new XferReceivedEventArgs(download)); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AbortXferHandler(object sender, PacketReceivedEventArgs e)
        {
            AbortXferPacket abort = (AbortXferPacket)e.Packet;
            XferDownload download = null;

            // Lame ulong to UUID conversion, please go away Xfer system
            UUID transferID = new UUID(abort.XferID.ID);

            lock (Transfers)
            {
                if (Transfers.TryGetValue(transferID, out var transfer))
                {
                    download = (XferDownload)transfer;
                    Transfers.Remove(transferID);
                }
            }

            if (download != null && m_XferReceivedEvent != null)
            {
                download.Success = false;
                download.Error = (TransferError)abort.XferID.Result;

                try { OnXferReceived(new XferReceivedEventArgs(download)); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
            }
        }

#endregion Xfer Callbacks
    }
#region EventArg classes
    // <summary>Provides data for XferReceived event</summary>
    public class XferReceivedEventArgs : EventArgs
    {
        /// <summary>Xfer data</summary>
        public XferDownload Xfer { get; }

        public XferReceivedEventArgs(XferDownload xfer)
        {
            this.Xfer = xfer;
        }
    }

    // <summary>Provides data for AssetUploaded event</summary>
    public class AssetUploadEventArgs : EventArgs
    {
        /// <summary>Upload data</summary>
        public AssetUpload Upload { get; }

        public AssetUploadEventArgs(AssetUpload upload)
        {
            this.Upload = upload;
        }
    }

    // <summary>Provides data for InitiateDownloaded event</summary>
    public class InitiateDownloadEventArgs : EventArgs
    {
        /// <summary>Filename used on the simulator</summary>
        public string SimFileName { get; }

        /// <summary>Filename used by the client</summary>
        public string ViewerFileName { get; }

        public InitiateDownloadEventArgs(string simFilename, string viewerFilename)
        {
            this.SimFileName = simFilename;
            this.ViewerFileName = viewerFilename;
        }
    }

    // <summary>Provides data for ImageReceiveProgress event</summary>
    public class ImageReceiveProgressEventArgs : EventArgs
    {
        /// <summary>UUID of the image that is in progress</summary>
        public UUID ImageID { get; }

        /// <summary>Number of bytes received so far</summary>
        public int Received { get; }

        /// <summary>Image size in bytes</summary>
        public int Total { get; }

        public ImageReceiveProgressEventArgs(UUID imageID, int received, int total)
        {
            this.ImageID = imageID;
            this.Received = received;
            this.Total = total;
        }
    }
#endregion
}
