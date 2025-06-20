﻿/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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

//#define DEBUG_TIMING

using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse.Packets;
using OpenMetaverse.Assets;

namespace OpenMetaverse
{
    /// <summary>
    /// The current status of a texture request as it moves through the pipeline or final result of a texture request. 
    /// </summary>
    public enum TextureRequestState
    {
        /// <summary>The initial state given to a request. Requests in this state
        /// are waiting for an available slot in the pipeline</summary>
        Pending,
        /// <summary>A request that has been added to the pipeline and the request packet
        /// has been sent to the simulator</summary>
        Started,
        /// <summary>A request that has received one or more packets back from the simulator</summary>
        Progress,
        /// <summary>A request that has received all packets back from the simulator</summary>
        Finished,
        /// <summary>A request that has taken longer than <see cref="Settings.PIPELINE_REQUEST_TIMEOUT"/>
        /// to download OR the initial packet containing the packet information was never received</summary>
        Timeout,
        /// <summary>The texture request was aborted by request of the agent</summary>
        Aborted,
        /// <summary>The simulator replied to the request that it was not able to find the requested texture</summary>
        NotFound
    }
    /// <summary>
    /// A callback fired to indicate the status or final state of the requested texture. For progressive 
    /// downloads this will fire each time new asset data is returned from the simulator.
    /// </summary>
    /// <param name="state">The <see cref="TextureRequestState"/> indicating either Progress for textures not fully downloaded,
    /// or the final result of the request after it has been processed through the TexturePipeline</param>
    /// <param name="assetTexture">The <see cref="AssetTexture"/> object containing the Assets ID, raw data
    /// and other information. For progressive rendering the <see cref="Asset.AssetData"/> will contain
    /// the data from the beginning of the file. For failed, aborted and timed out requests it will contain
    /// an empty byte array.</param>
    public delegate void TextureDownloadCallback(TextureRequestState state, AssetTexture assetTexture);

    /// <summary>
    /// Texture request download handler, allows a configurable number of download slots which manage multiple
    /// concurrent texture downloads from the <see cref="Simulator"/>
    /// </summary>
    /// <remarks>This class makes full use of the internal <see cref="TextureCache"/> 
    /// system for full texture downloads.</remarks>
    public class TexturePipeline
    {
#if DEBUG_TIMING // Timing globals
        /// <summary>The combined time it has taken for all textures requested sofar. This includes the amount of time the 
        /// texture spent waiting for a download slot, and the time spent retrieving the actual texture from the Grid</summary>
        public static TimeSpan TotalTime;
        /// <summary>The amount of time the request spent in the <see cref="TextureRequestState.Progress"/> state</summary>
        public static TimeSpan NetworkTime;
        /// <summary>The total number of bytes transferred since the TexturePipeline was started</summary>
        public static int TotalBytes;
#endif
        /// <summary>
        /// A request task containing information and status of a request as it is processed through the <see cref="TexturePipeline"/>
        /// </summary>
        private class TaskInfo
        {
            /// <summary>The current <see cref="TextureRequestState"/> which identifies the current status of the request</summary>
            public TextureRequestState State;
            /// <summary>The Unique Request ID, This is also the Asset ID of the texture being requested</summary>
            public UUID RequestID;
            /// <summary>The cancellation token for the request.</summary>
            public CancellationTokenSource TokenSource;
            /// <summary>The ImageType of the request.</summary>
            public ImageType Type;

            /// <summary>The callback to fire when the request is complete, will include 
            /// the <see cref="TextureRequestState"/> and the <see cref="AssetTexture"/> 
            /// object containing the result data</summary>
            public List<TextureDownloadCallback> Callbacks;
            /// <summary>If true, indicates the callback will be fired whenever new data is returned from the simulator.
            /// This is used to progressively render textures as portions of the texture are received.</summary>
            public bool ReportProgress;
#if DEBUG_TIMING
            /// <summary>The time the request was added to the the PipeLine</summary>
            public DateTime StartTime;
            /// <summary>The time the request was sent to the simulator</summary>
            public DateTime NetworkTime;
#endif
            /// <summary>An object that maintains the data of a request that is in-process.</summary>
            public ImageDownload Transfer;
        }

        /// <summary>A dictionary containing all pending and in-process transfer requests where the Key is both the RequestID
        /// and also the Asset Texture ID, and the value is an object containing the current state of the request and also
        /// the asset data as it is being re-assembled</summary>
        private readonly ConcurrentDictionary<UUID, TaskInfo> _Transfers;
        /// <summary>Holds the reference to the <see cref="GridClient"/> client object</summary>
        private readonly GridClient _Client;
        /// <summary>Maximum concurrent texture requests allowed at a time</summary>
        private readonly int maxTextureRequests;
        /// <summary>The primary thread which manages the requests.</summary>
        private Thread downloadMaster;
        /// <summary>The cancellation token for the TexturePipeline and all child tasks.</summary>
        private readonly CancellationTokenSource downloadTokenSource;
        /// <summary>true if the TexturePipeline is currently running</summary>
        private bool _Running;
        /// <summary>A refresh timer used to increase the priority of stalled requests</summary>
        private System.Timers.Timer RefreshDownloadsTimer;

        /// <summary>Current number of pending and in-process transfers</summary>
        public int TransferCount => _Transfers.Count;

        /// <summary>
        /// Default constructor, Instantiates a new copy of the TexturePipeline class
        /// </summary>
        /// <param name="client">Reference to the instantiated <see cref="GridClient"/> object</param>
        public TexturePipeline(GridClient client)
        {
            _Client = client;

            maxTextureRequests = client.Settings.MAX_CONCURRENT_TEXTURE_DOWNLOADS;

            downloadTokenSource = new CancellationTokenSource();

            _Transfers = new ConcurrentDictionary<UUID, TaskInfo>();

            // Handle client connected and disconnected events
            client.Network.LoginProgress += delegate(object sender, LoginProgressEventArgs e) {
                if (e.Status == LoginStatus.Success)
                {
                    Startup(); 
                }
            };

            client.Network.Disconnected += delegate { Shutdown(); };
        }

        /// <summary>
        /// Initialize callbacks required for the TexturePipeline to operate
        /// </summary>
        public void Startup()
        {
            if (_Running)
                return;

            if (!_Client.Settings.USE_TEXTURE_PIPELINE)
                return;

            if (downloadMaster == null)
            {
                // Instantiate master thread that manages the request pool
                downloadMaster = new Thread(DownloadThread)
                {
                    Name = "TexturePipeline",
                    IsBackground = true
                };
            }

            _Running = true;

            _Client.Network.RegisterCallback(PacketType.ImageData, ImageDataHandler);
            _Client.Network.RegisterCallback(PacketType.ImagePacket, ImagePacketHandler);
            _Client.Network.RegisterCallback(PacketType.ImageNotInDatabase, ImageNotInDatabaseHandler);
            downloadMaster.Start();
            if (RefreshDownloadsTimer == null)
            {
                RefreshDownloadsTimer = new System.Timers.Timer(Settings.PIPELINE_REFRESH_INTERVAL);
                RefreshDownloadsTimer.Elapsed += RefreshDownloadsTimer_Elapsed;
                RefreshDownloadsTimer.Start();
            }
        }

        /// <summary>
        /// Shutdown the TexturePipeline and cleanup any callbacks or transfers
        /// </summary>
        public void Shutdown()
        {
            if (!_Running)
                return;
#if DEBUG_TIMING
            Logger.Log(String.Format("Combined Execution Time: {0}, Network Execution Time {1}, Network {2}K/sec, Image Size {3}",
                        TotalTime, NetworkTime, Math.Round(TotalBytes / NetworkTime.TotalSeconds / 60, 2), TotalBytes), Helpers.LogLevel.Debug);
#endif
            RefreshDownloadsTimer?.Dispose();
            RefreshDownloadsTimer = null;
            
            if (!downloadTokenSource.IsCancellationRequested)
                downloadTokenSource.Cancel();

            downloadMaster = null;

            _Client.Network.UnregisterCallback(PacketType.ImageNotInDatabase, ImageNotInDatabaseHandler);
            _Client.Network.UnregisterCallback(PacketType.ImageData, ImageDataHandler);
            _Client.Network.UnregisterCallback(PacketType.ImagePacket, ImagePacketHandler);

            _Transfers.Clear();

            _Running = false;
        }

        private void RefreshDownloadsTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var transfers = _Transfers.ToFrozenDictionary();

            foreach (TaskInfo transfer in transfers.Values)
            {
                if (transfer.State != TextureRequestState.Progress) continue;
                ImageDownload download = transfer.Transfer;

                // Find the first missing packet in the download
                ushort packet = 0;
                lock (download)
                {
                    if (download.PacketsSeen != null && download.PacketsSeen.Count > 0)
                        packet = GetFirstMissingPacket(download.PacketsSeen);
                }

                if (download.TimeSinceLastPacket > 5000)
                {
                    // We're not receiving data for this texture fast enough, bump up the priority by 5%
                    download.Priority *= 1.05f;

                    download.TimeSinceLastPacket = 0;
                    RequestImage(download.ID, download.ImageType, download.Priority, download.DiscardLevel, packet);
                }

                if (download.TimeSinceLastPacket > _Client.Settings.PIPELINE_REQUEST_TIMEOUT)
                {
                    transfer.TokenSource.Cancel();
                }
            }
        }

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
        /// This controls the end marker of the data sent</param>
        /// <param name="packetStart">The packet number to begin the request at. A value of 0 begins the request
        /// from the start of the asset texture</param>
        /// <param name="callback">The <see cref="TextureDownloadCallback"/> callback to fire when the image is retrieved. The callback
        /// will contain the result of the request and the texture asset data</param>
        /// <param name="progressive">If true, the callback will be fired for each chunk of the downloaded image. 
        /// The callback asset parameter will contain all previously received chunks of the texture asset starting 
        /// from the beginning of the request</param>
        public void RequestTexture(UUID textureID, ImageType imageType, float priority, int discardLevel, uint packetStart, TextureDownloadCallback callback, bool progressive)
        {
            if (textureID == UUID.Zero)return;
            if (callback == null) return;

            if (_Client.Assets.Cache.HasAsset(textureID))
            {
                ImageDownload image = new ImageDownload
                {
                    ID = textureID,
                    AssetData = _Client.Assets.Cache.GetCachedAssetBytes(textureID)
                };
                image.Size = image.AssetData.Length;
                image.Transferred = image.AssetData.Length;
                image.ImageType = imageType;
                image.AssetType = AssetType.Texture;
                image.Success = true;

                callback(TextureRequestState.Finished, new AssetTexture(image.ID, image.AssetData));

                _Client.Assets.FireImageProgressEvent(image.ID, image.Transferred, image.Size);
            }
            else
            {
                _Transfers.AddOrUpdate(textureID,
                    new TaskInfo
                    {
                        State = TextureRequestState.Pending,
                        RequestID = textureID,
                        ReportProgress = progressive,
                        TokenSource = CancellationTokenSource.CreateLinkedTokenSource(downloadTokenSource.Token),
                        Type = imageType,
                        Callbacks = new List<TextureDownloadCallback> { callback },
                        Transfer = new ImageDownload
                        {
                            ID = textureID,
                            Priority = priority,
                            ImageType = imageType,
                            DiscardLevel = discardLevel
                        },
#if DEBUG_TIMING
                        StartTime = DateTime.UtcNow
#endif
                    }, (uuid, info) =>
                    {
                        info.Callbacks.Add(callback);
                        return info;
                    });
            }
        }

        /// <summary>
        /// Sends the actual request packet to the simulator
        /// </summary>
        /// <param name="imageID">The image to download</param>
        /// <param name="type">Type of the image to download, either a baked
        /// avatar texture or a normal texture</param>
        /// <param name="priority">Priority level of the download. Default is
        /// <c>1,013,000.0f</c></param>
        /// <param name="discardLevel">Number of quality layers to discard.
        /// This controls the end marker of the data sent</param>
        /// <param name="packetNum">Packet number to start the download at.
        /// This controls the start marker of the data sent</param>
        /// <remarks>Sending a priority of 0 and a discardlevel of -1 aborts
        /// download</remarks>
        private void RequestImage(UUID imageID, ImageType type, float priority, int discardLevel, uint packetNum)
        {
            // Priority == 0 && DiscardLevel == -1 means cancel the transfer
            if (priority.Equals(0) && discardLevel.Equals(-1))
            {
                AbortTextureRequest(imageID);
            }
            else
            {
                if (_Transfers.TryGetValue(imageID, out var task))
                {
                    if (task.Transfer.Simulator != null)
                    {
                        // Already downloading, just updating the priority
                        float percentComplete = ((float)task.Transfer.Transferred / (float)task.Transfer.Size) * 100f;
                        if (float.IsNaN(percentComplete))
                            percentComplete = 0f;

                        if (percentComplete > 0f)
                        {
                            Logger.DebugLog(
                                $"Updating priority on image transfer {imageID} to {task.Transfer.Priority}, {Math.Round(percentComplete, 2)}% complete");
                        }
                    }
                    else
                    {
                        ImageDownload transfer = task.Transfer;
                        transfer.Simulator = _Client.Network.CurrentSim;
                    }

                    // Build and send the request packet
                    RequestImagePacket request = new RequestImagePacket
                    {
                        AgentData =
                        {
                            AgentID = _Client.Self.AgentID,
                            SessionID = _Client.Self.SessionID
                        },
                        RequestImage = new RequestImagePacket.RequestImageBlock[1]
                    };
                    request.RequestImage[0] = new RequestImagePacket.RequestImageBlock
                    {
                        DiscardLevel = (sbyte) discardLevel,
                        DownloadPriority = priority,
                        Packet = packetNum,
                        Image = imageID,
                        Type = (byte) type
                    };

                    _Client.Network.SendPacket(request, _Client.Network.CurrentSim);
                }
                else
                {
                    Logger.Log($"Received texture download request for a texture that isn't in the download queue: {imageID}", 
                        Helpers.LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// Cancel a pending or in process texture request
        /// </summary>
        /// <param name="textureID">The texture assets unique ID</param>
        public void AbortTextureRequest(UUID textureID)
        {
            if (!_Transfers.TryGetValue(textureID, out var task)) { return; }

            // this means we've actually got the request assigned to the threadpool
            if (task.State == TextureRequestState.Progress)
            {
                RequestImagePacket request = new RequestImagePacket
                {
                    AgentData =
                    {
                        AgentID = _Client.Self.AgentID,
                        SessionID = _Client.Self.SessionID
                    },
                    RequestImage = new RequestImagePacket.RequestImageBlock[1]
                };
                request.RequestImage[0] = new RequestImagePacket.RequestImageBlock
                {
                    DiscardLevel = -1,
                    DownloadPriority = 0,
                    Packet = 0,
                    Image = textureID,
                    Type = (byte) task.Type
                };
                _Client.Network.SendPacket(request);

                foreach (var callback in task.Callbacks)
                    callback(TextureRequestState.Aborted, new AssetTexture(textureID, Utils.EmptyBytes));

                _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred, task.Transfer.Size);

                task.TokenSource.Cancel();

                _Transfers.TryRemove(textureID, out _);
            }
            else
            {
                _Transfers.TryRemove(textureID, out _);

                foreach (var callback in task.Callbacks)
                    callback(TextureRequestState.Aborted, new AssetTexture(textureID, Utils.EmptyBytes));

                _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred, task.Transfer.Size);
            }
        }

        /// <summary>
        /// Master Download Thread, Queues up downloads in the threadpool
        /// </summary>
        private void DownloadThread()
        {
            while (_Running)
            {
                // find free slots
                int active = 0;

                var pendingTasks = new Queue<TaskInfo>();

                foreach (var request in _Transfers)
                {
                    switch (request.Value.State)
                    {
                        case TextureRequestState.Pending:
                            pendingTasks.Enqueue(request.Value);
                            break;
                        case TextureRequestState.Started:
                        case TextureRequestState.Progress:
                            ++active;
                            break;
                    }
                }

                // NOTE: use TryDequeue once we can use .NET Standard 2.1
                while (active <= maxTextureRequests && pendingTasks.Any())
                {
                    var nextTask = pendingTasks.Dequeue();
                    nextTask.State = TextureRequestState.Started;

                    //Logger.DebugLog(String.Format("Sending Worker thread new download request {0}", slot));
                    ThreadPool.QueueUserWorkItem(TextureRequestDoWork, nextTask);
                    ++active;
                }

                // Queue was empty or all download slots are in use, let's give up some CPU time
                Thread.Sleep(500);
            }

            Logger.Log("Texture pipeline shutting down", Helpers.LogLevel.Info);
        }


        /// <summary>
        /// The worker thread that sends the request and handles timeouts
        /// </summary>
        /// <param name="threadContext">A <see cref="TaskInfo"/> object containing the request details</param>
        private void TextureRequestDoWork(object threadContext)
        {
            TaskInfo task = (TaskInfo)threadContext;

            task.State = TextureRequestState.Progress;

#if DEBUG_TIMING
            task.NetworkTime = DateTime.UtcNow;
#endif
            // Find the first missing packet in the download
            ushort packet = 0;
            lock (task.Transfer)
            {
                if (task.Transfer.PacketsSeen != null && task.Transfer.PacketsSeen.Count > 0)
                    packet = GetFirstMissingPacket(task.Transfer.PacketsSeen);
            }

            // Request the texture
            RequestImage(task.RequestID, task.Type, task.Transfer.Priority, task.Transfer.DiscardLevel, packet);

            // Set starting time
            task.Transfer.TimeSinceLastPacket = 0;

            // Don't release this worker slot until texture is downloaded or timeout occurs
            if (task.TokenSource.Token.WaitHandle.WaitOne())
            {
                // Timed out
                Logger.Log($"Worker timeout waiting for texture {task.RequestID} to download got " +
                    $"{task.Transfer.Transferred} of {task.Transfer.Size}", Helpers.LogLevel.Warning);

                AssetTexture texture = new AssetTexture(task.RequestID, task.Transfer.AssetData);
                foreach (TextureDownloadCallback callback in task.Callbacks)
                    callback(TextureRequestState.Timeout, texture);

                _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred, task.Transfer.Size);

                _Transfers.TryRemove(task.RequestID, out _);
            }
        }

        private ushort GetFirstMissingPacket(SortedList<ushort, ushort> packetsSeen)
        {
            ushort packet = 0;

            lock (packetsSeen)
            {
                bool first = true;
                foreach (var packetSeen in packetsSeen)
                {
                    if (first)
                    {
                        // Initially set this to the earliest packet received in the transfer
                        packet = packetSeen.Value;
                        first = false;
                    }
                    else
                    {
                        ++packet;

                        // If there is a missing packet in the list, break and request the download
                        // resume here
                        if (packetSeen.Value != packet)
                        {
                            --packet;
                            break;
                        }
                    }
                }

                ++packet;
            }

            return packet;
        }

        #region Raw Packet Handlers

        /// <summary>
        /// Handle responses from the simulator that tell us a texture we have requested is unable to be located
        /// or no longer exists. This will remove the request from the pipeline and free up a slot if one is in use
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ImageNotInDatabaseHandler(object sender, PacketReceivedEventArgs e)
        {
            ImageNotInDatabasePacket imageNotFoundData = (ImageNotInDatabasePacket)e.Packet;

            if (_Transfers.TryGetValue(imageNotFoundData.ImageID.ID, out var task))
            {
                // cancel active request and free up the threadpool slot
                task.TokenSource.Cancel();

                // fire callback to inform the caller 
                foreach (TextureDownloadCallback callback in task.Callbacks)
                    callback(TextureRequestState.NotFound, new AssetTexture(imageNotFoundData.ImageID.ID, Utils.EmptyBytes));

                _Transfers.TryRemove(imageNotFoundData.ImageID.ID, out _);
            }
            else
            {
                Logger.Log($"Received an ImageNotFound packet for an image we did not request: {imageNotFoundData.ImageID.ID}",
                    Helpers.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Handles the remaining Image data that did not fit in the initial ImageData packet
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ImagePacketHandler(object sender, PacketReceivedEventArgs e)
        {
            ImagePacketPacket image = (ImagePacketPacket)e.Packet;

            if (_Transfers.TryGetValue(image.ImageID.ID, out var task))
            {
                if (task.Transfer.Size == 0)
                {
                    // We haven't received the header yet, block until it's received or times out
                    task.Transfer.HeaderReceivedEvent.WaitOne(TimeSpan.FromSeconds(5), false);

                    if (task.Transfer.Size == 0)
                    {
                        Logger.Log($"Timed out while waiting for the image header to download for {task.Transfer.ID}",
                            Helpers.LogLevel.Warning, _Client);

                        task.TokenSource.Cancel();

                        _Transfers.TryRemove(task.Transfer.ID, out _);

                        foreach (TextureDownloadCallback callback in task.Callbacks)
                            callback(TextureRequestState.Timeout, new AssetTexture(task.RequestID, task.Transfer.AssetData));

                        return;
                    }
                }

                // The header is downloaded, we can insert this data in to the proper position
                // Only insert if we haven't seen this packet before
                lock (task.Transfer)
                {
                    if (!task.Transfer.PacketsSeen.ContainsKey(image.ImageID.Packet))
                    {
                        task.Transfer.PacketsSeen[image.ImageID.Packet] = image.ImageID.Packet;
                        Buffer.BlockCopy(image.ImageData.Data, 0, task.Transfer.AssetData,
                                         task.Transfer.InitialDataSize + (1000 * (image.ImageID.Packet - 1)),
                                         image.ImageData.Data.Length);
                        task.Transfer.Transferred += image.ImageData.Data.Length;
                    }
                }

                task.Transfer.TimeSinceLastPacket = 0;

                if (task.Transfer.Transferred >= task.Transfer.Size)
                {
#if DEBUG_TIMING
                        DateTime stopTime = DateTime.UtcNow;
                        TimeSpan requestDuration = stopTime - task.StartTime;

                        TimeSpan networkDuration = stopTime - task.NetworkTime;

                        TotalTime += requestDuration;
                        NetworkTime += networkDuration;
                        TotalBytes += task.Transfer.Size;

                        Logger.Log(
                            String.Format(
                                "Transfer Complete {0} [{1}] Total Request Time: {2}, Download Time {3}, Network {4}Kb/sec, Image Size {5} bytes",
                                task.RequestID, task.RequestSlot, requestDuration, networkDuration,
                                Math.Round(task.Transfer.Size/networkDuration.TotalSeconds/60, 2), task.Transfer.Size),
                            Helpers.LogLevel.Debug);
#endif

                    task.Transfer.Success = true;
                    task.TokenSource.Cancel();
                    _Transfers.TryRemove(task.Transfer.ID, out _);
                    _Client.Assets.Cache.SaveAssetToCache(task.RequestID, task.Transfer.AssetData);
                    foreach (var callback in task.Callbacks)
                        callback(TextureRequestState.Finished, new AssetTexture(task.RequestID, task.Transfer.AssetData));

                    _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred, task.Transfer.Size);
                }
                else
                {
                    if (task.ReportProgress)
                    {
                        foreach (var callback in task.Callbacks)
                        {
                            callback(TextureRequestState.Progress,
                                     new AssetTexture(task.RequestID, task.Transfer.AssetData));
                        }
                    }
                    _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred,
                                                              task.Transfer.Size);
                }
            }
        }

        /// <summary>
        /// Handle the initial ImageDataPacket sent from the simulator
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ImageDataHandler(object sender, PacketReceivedEventArgs e)
        {
            ImageDataPacket data = (ImageDataPacket)e.Packet;

            if (_Transfers.TryGetValue(data.ImageID.ID, out var task))
            {
                // reset the timeout interval since we got data
                task.Transfer.TimeSinceLastPacket = 0;

                lock (task.Transfer)
                {
                    if (task.Transfer.Size == 0)
                    {
                        task.Transfer.Codec = (ImageCodec)data.ImageID.Codec;
                        task.Transfer.PacketCount = data.ImageID.Packets;
                        task.Transfer.Size = (int)data.ImageID.Size;
                        task.Transfer.AssetData = new byte[task.Transfer.Size];
                        task.Transfer.AssetType = AssetType.Texture;
                        task.Transfer.PacketsSeen = new SortedList<ushort, ushort>();
                        Buffer.BlockCopy(data.ImageData.Data, 0, task.Transfer.AssetData, 0, data.ImageData.Data.Length);
                        task.Transfer.InitialDataSize = data.ImageData.Data.Length;
                        task.Transfer.Transferred += data.ImageData.Data.Length;
                    }
                }

                task.Transfer.HeaderReceivedEvent.Set();

                if (task.Transfer.Transferred >= task.Transfer.Size)
                {
#if DEBUG_TIMING
                    DateTime stopTime = DateTime.UtcNow;
                    TimeSpan requestDuration = stopTime - task.StartTime;

                    TimeSpan networkDuration = stopTime - task.NetworkTime;

                    TotalTime += requestDuration;
                    NetworkTime += networkDuration;
                    TotalBytes += task.Transfer.Size;

                    Logger.Log(
                        String.Format(
                            "Transfer Complete {0} [{1}] Total Request Time: {2}, Download Time {3}, Network {4}Kb/sec, Image Size {5} bytes",
                            task.RequestID, task.RequestSlot, requestDuration, networkDuration,
                            Math.Round(task.Transfer.Size/networkDuration.TotalSeconds/60, 2), task.Transfer.Size),
                        Helpers.LogLevel.Debug);
#endif
                    task.Transfer.Success = true;
                    task.TokenSource.Cancel();
                    _Transfers.TryRemove(task.RequestID, out _);

                    _Client.Assets.Cache.SaveAssetToCache(task.RequestID, task.Transfer.AssetData);

                    foreach (var callback in task.Callbacks)
                        callback(TextureRequestState.Finished, new AssetTexture(task.RequestID, task.Transfer.AssetData));

                    _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred, task.Transfer.Size);
                }
                else
                {
                    if (task.ReportProgress)
                    {
                        foreach (var callback in task.Callbacks)
                        {
                            callback(TextureRequestState.Progress,
                                      new AssetTexture(task.RequestID, task.Transfer.AssetData));
                        }
                    }

                    _Client.Assets.FireImageProgressEvent(task.RequestID, task.Transfer.Transferred,
                                                              task.Transfer.Size);
                }
            }
        }

        #endregion
    }
}
