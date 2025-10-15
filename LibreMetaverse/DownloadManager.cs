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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;

namespace OpenMetaverse.Http
{
    /// <summary>
    /// Represents individual HTTP Download request
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>URI of the item to fetch</summary>
        public Uri Address;
        /// <summary>Download progress callback</summary>
        public DownloadProgressHandler DownloadProgressCallback;
        /// <summary>Download completed callback</summary>
        public DownloadCompleteHandler CompletedCallback;
        /// <summary>Accept the following content type</summary>
        public string ContentType;
        /// <summary>How many times will this request be retried</summary>
        public int Retries = 5;
        /// <summary>Current fetch attempt</summary>
        public int Attempt = 0;

        /// <summary>Constructor</summary>
        public DownloadRequest(Uri address, string contentType,
            DownloadProgressHandler downloadProgressCallback,
            DownloadCompleteHandler completedCallback)
        {
            Address = address;
            DownloadProgressCallback = downloadProgressCallback;
            CompletedCallback = completedCallback;
            ContentType = contentType;
        }
    }

    internal class ActiveDownload
    {
        public List<DownloadProgressHandler> ProgressHandlers = new List<DownloadProgressHandler>();
        public List<DownloadCompleteHandler> CompletedHandlers = new List<DownloadCompleteHandler>();
        public CancellationTokenSource CancellationToken = new CancellationTokenSource();
    }

    /// <summary>
    /// Manages async HTTP downloads with a limit on maximum
    /// concurrent downloads
    /// </summary>
    public class DownloadManager : IDisposable
    {
        private readonly ConcurrentQueue<DownloadRequest> queue = new ConcurrentQueue<DownloadRequest>();
        private readonly ConcurrentDictionary<string, ActiveDownload> activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();

        /// <summary>Maximum number of parallel downloads from a single endpoint</summary>
        public int ParallelDownloads { get; set; }

        private readonly GridClient Client;

        /// <summary>Default constructor</summary>
        public DownloadManager(GridClient client)
        {
            Client = client;
            ParallelDownloads = 8;
        }

        /// <summary>Cleanup method</summary>
        public virtual void Dispose()
        {
            foreach (var download in activeDownloads.Values)
            {
                download.CancellationToken.Cancel();
                download.CancellationToken.Dispose();
            }
            activeDownloads.Clear();
        }

        /// <summary>Check the queue for pending work</summary>
        private void EnqueuePending()
        {
            if (queue.Count <= 0) { return; }

            var nr = activeDownloads.Count;

            // Logger.DebugLog(nr.ToString() + " active downloads. Queued textures: " + queue.Count.ToString());

            for (var i = nr; i < ParallelDownloads && queue.IsEmpty; ++i)
            {
                if (!queue.TryDequeue(out var item)) { return; }

                var addr = item.Address.ToString();
                if (activeDownloads.ContainsKey(addr))
                {
                    activeDownloads[addr].CompletedHandlers.Add(item.CompletedCallback);
                    if (item.DownloadProgressCallback != null)
                    {
                        activeDownloads[addr].ProgressHandlers.Add(item.DownloadProgressCallback);
                    }
                }
                else
                {
                    var activeDownload = new ActiveDownload();
                    activeDownload.CompletedHandlers.Add(item.CompletedCallback);
                    if (item.DownloadProgressCallback != null)
                    {
                        activeDownload.ProgressHandlers.Add(item.DownloadProgressCallback);
                    }

                    Logger.DebugLog($"Requesting {item.Address}");
                    
                    Task req = Client.HttpCapsClient.GetRequestAsync(item.Address, activeDownload.CancellationToken.Token,
                            (response, responseData, error)  =>
                            {
                                activeDownloads.TryRemove(addr, out _);
                                if (error == null || item.Attempt >= item.Retries || response.StatusCode == HttpStatusCode.NotFound)
                                {
                                    foreach (var handler in activeDownload.CompletedHandlers)
                                    {
                                        handler(response, responseData, error);
                                    }
                                }
                                else
                                {
                                    item.Attempt++;
                                    Logger.Log($"{item.Address} HTTP download failed, trying again retry {item.Attempt}/{item.Retries}",
                                        Helpers.LogLevel.Warning);
                                    lock (queue) queue.Enqueue(item);
                                }
                                EnqueuePending();
                        }, (totalBytes, totalReceived, progressPercent) =>
                        {
                            foreach (var handler in activeDownload.ProgressHandlers)
                            {
                                handler(totalBytes, totalReceived, progressPercent);
                            }
                        }, null);
                    activeDownloads[addr] = activeDownload;
                }
            }
        }

        /// <summary>Enqueue a new HTTP download</summary>
        public void QueueDownload(DownloadRequest req)
        {
            var addr = req.Address.ToString();
            if (activeDownloads.ContainsKey(addr))
            {
                activeDownloads[addr].CompletedHandlers.Add(req.CompletedCallback);
                if (req.DownloadProgressCallback != null)
                {
                    activeDownloads[addr].ProgressHandlers.Add(req.DownloadProgressCallback);
                }
                return;
            }

            queue.Enqueue(req);
            EnqueuePending();
        }
    }
}
