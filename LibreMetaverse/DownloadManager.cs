/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn LLC.
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
using System.Net;
using System.Threading;
using System.Threading.Channels;
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
        public Uri Address { get; }
        /// <summary>Download progress callback</summary>
        public DownloadProgressHandler DownloadProgressCallback { get; }
        /// <summary>Download completed callback</summary>
        public DownloadCompleteHandler CompletedCallback { get; }
        /// <summary>Accept the following content type</summary>
        public string ContentType { get; }
        /// <summary>How many times will this request be retried</summary>
        public const int Retries = 5;
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

    /// <summary>
    /// Manages async HTTP downloads with a limit on maximum
    /// concurrent downloads
    /// </summary>
    public class DownloadManager : IDisposable
    {
        private readonly GridClient Client;
        private readonly Channel<DownloadRequest> channel;
        private readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>Default constructor</summary>
        public DownloadManager(GridClient client)
        {
            Client = client;
            channel = Channel.CreateBounded<DownloadRequest>(8);
            cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartConsumer()
        {
            while (await channel.Reader.WaitToReadAsync())
            {
                while (channel.Reader.TryRead(out var download))
                {
                    if (download.Attempt > DownloadRequest.Retries) { continue; }

                    await Client.HttpCapsClient.GetRequestAsync(download.Address, cancellationTokenSource.Token,
                        (response, responseData, error) =>
                        {
                            if (error == null || download.Attempt >= DownloadRequest.Retries ||
                                response.StatusCode == HttpStatusCode.NotFound)
                            {
                                download.CompletedCallback(response, responseData, error);
                            }
                            else
                            {
                                download.Attempt++;
                                Logger.Log(
                                    $"{download.Address} HTTP download failed, trying again retry {download.Attempt}/{DownloadRequest.Retries}",
                                    Helpers.LogLevel.Warning);
                                QueueDownload(download);
                            }
                        }, download.DownloadProgressCallback);

                }
            }
        }

        /// <summary>Cleanup method</summary>
        public virtual void Dispose()
        {
            cancellationTokenSource.Cancel();
        }

        /// <summary>Enqueue a new HTTP download</summary>
        public void QueueDownload(DownloadRequest request)
        {
            _ = channel.Writer.WriteAsync(request, cancellationTokenSource.Token);
        }
    }
}
