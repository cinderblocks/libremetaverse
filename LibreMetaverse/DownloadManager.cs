/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019, Cinderblocks Design Co.
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
using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenMetaverse.Http;

namespace OpenMetaverse
{
    /// <summary>
    /// Represents individual HTTP Download request
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>URI of the item to fetch</summary>
        public Uri Address;
        /// <summary>Timout specified in milliseconds</summary>
        public int MillisecondsTimeout;
        /// <summary>Download progress callback</summary>
        public CapsBase.DownloadProgressEventHandler DownloadProgressCallback;
        /// <summary>Download completed callback</summary>
        public CapsBase.RequestCompletedEventHandler CompletedCallback;
        /// <summary>Accept the following content type</summary>
        public string ContentType;
        /// <summary>How many times will this request be retried</summary>
        public int Retries = 5;
        /// <summary>Current fetch attempt</summary>
        public int Attempt = 0;

        /// <summary>Default constructor</summary>
        public DownloadRequest()
        {
        }

        /// <summary>Constructor</summary>
        public DownloadRequest(Uri address, int millisecondsTimeout,
            string contentType,
            CapsBase.DownloadProgressEventHandler downloadProgressCallback,
            CapsBase.RequestCompletedEventHandler completedCallback)
        {
            this.Address = address;
            this.MillisecondsTimeout = millisecondsTimeout;
            this.DownloadProgressCallback = downloadProgressCallback;
            this.CompletedCallback = completedCallback;
            this.ContentType = contentType;
        }
    }

    internal class ActiveDownload
    {
        public List<CapsBase.DownloadProgressEventHandler> ProgresHandlers = new List<CapsBase.DownloadProgressEventHandler>();
        public List<CapsBase.RequestCompletedEventHandler> CompletedHandlers = new List<CapsBase.RequestCompletedEventHandler>();
        public HttpWebRequest Request;
    }

    /// <summary>
    /// Manages async HTTP downloads with a limit on maximum
    /// concurrent downloads
    /// </summary>
    public class DownloadManager
    {
        Queue<DownloadRequest> queue = new Queue<DownloadRequest>();
        Dictionary<string, ActiveDownload> activeDownloads = new Dictionary<string, ActiveDownload>();

        /// <summary>Maximum number of parallel downloads from a single endpoint</summary>
        public int ParallelDownloads { get; set; }

        /// <summary>Client certificate</summary>
        public X509Certificate2 ClientCert { get; set; }

        /// <summary>Default constructor</summary>
        public DownloadManager()
        {
            ParallelDownloads = 8;
        }

        /// <summary>Cleanup method</summary>
        public virtual void Dispose()
        {
            lock (activeDownloads)
            {
                foreach (ActiveDownload download in activeDownloads.Values)
                {
                    try
                    {
                        download.Request?.Abort();
                    }
                    catch { }
                }
                activeDownloads.Clear();
            }
        }

        /// <summary>Setup http download request</summary>
        protected virtual HttpWebRequest SetupRequest(Uri address, string acceptHeader)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(address);
            request.Method = "GET";

            if (!string.IsNullOrEmpty(acceptHeader))
                request.Accept = acceptHeader;

            // Add the client certificate to the request if one was given
            if (ClientCert != null)
                request.ClientCertificates.Add(ClientCert);

            // Leave idle connections to this endpoint open for up to 60 seconds
            request.ServicePoint.MaxIdleTime = 1000 * 60;
            // Disable stupid Expect-100: Continue header
            request.ServicePoint.Expect100Continue = false;
            // Crank up the max number of connections per endpoint
            if (request.ServicePoint.ConnectionLimit < Settings.MAX_HTTP_CONNECTIONS)
            {
                Logger.Log(
                    "In DownloadManager.SetupRequest() setting conn limit for " +
                    $"{address.Host}:{address.Port} to {Settings.MAX_HTTP_CONNECTIONS}", Helpers.LogLevel.Debug);
                request.ServicePoint.ConnectionLimit = Settings.MAX_HTTP_CONNECTIONS;
            }

            return request;
        }

        /// <summary>Check the queue for pending work</summary>
        private void EnqueuePending()
        {
            lock (queue)
            {
                if (queue.Count <= 0) return;

                int nr = 0;
                lock (activeDownloads)
                {
                    nr = activeDownloads.Count;
                }

                // Logger.DebugLog(nr.ToString() + " active downloads. Queued textures: " + queue.Count.ToString());

                for (int i = nr; i < ParallelDownloads && queue.Count > 0; i++)
                {
                    DownloadRequest item = queue.Dequeue();
                    lock (activeDownloads)
                    {
                        string addr = item.Address.ToString();
                        if (activeDownloads.ContainsKey(addr))
                        {
                            activeDownloads[addr].CompletedHandlers.Add(item.CompletedCallback);
                            if (item.DownloadProgressCallback != null)
                            {
                                activeDownloads[addr].ProgresHandlers.Add(item.DownloadProgressCallback);
                            }
                        }
                        else
                        {
                            ActiveDownload activeDownload = new ActiveDownload();
                            activeDownload.CompletedHandlers.Add(item.CompletedCallback);
                            if (item.DownloadProgressCallback != null)
                            {
                                activeDownload.ProgresHandlers.Add(item.DownloadProgressCallback);
                            }

                            Logger.DebugLog("Requesting " + item.Address);
                            activeDownload.Request = SetupRequest(item.Address, item.ContentType);
                            CapsBase.DownloadDataAsync(
                                activeDownload.Request,
                                item.MillisecondsTimeout,
                                (request, response, bytesReceived, totalBytesToReceive) =>
                                {
                                    foreach (CapsBase.DownloadProgressEventHandler handler in activeDownload.ProgresHandlers)
                                    {
                                        handler(request, response, bytesReceived, totalBytesToReceive);
                                    }
                                },
                                (request, response, responseData, error) =>
                                {
                                    lock (activeDownloads) activeDownloads.Remove(addr);
                                    if (error == null || item.Attempt >= item.Retries || error.Message.Contains("404"))
                                    {
                                        foreach (CapsBase.RequestCompletedEventHandler handler in activeDownload.CompletedHandlers)
                                        {
                                            handler(request, response, responseData, error);
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
                                }
                            );

                            activeDownloads[addr] = activeDownload;
                        }
                    }
                }
            }
        }

        /// <summary>Enqueue a new HTTP download</summary>
        public void QueueDownload(DownloadRequest req)
        {
            lock (activeDownloads)
            {
                string addr = req.Address.ToString();
                if (activeDownloads.ContainsKey(addr))
                {
                    activeDownloads[addr].CompletedHandlers.Add(req.CompletedCallback);
                    if (req.DownloadProgressCallback != null)
                    {
                        activeDownloads[addr].ProgresHandlers.Add(req.DownloadProgressCallback);
                    }
                    return;
                }
            }

            lock (queue)
            {
                queue.Enqueue(req);
            }
            EnqueuePending();
        }
    }
}
