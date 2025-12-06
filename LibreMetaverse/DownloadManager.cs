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
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using LibreMetaverse;

#pragma warning disable CS0618 // Type or member is obsolete (DownloadCompleteHandler)

namespace OpenMetaverse.Http
{
    /// <summary>
    /// Represents individual HTTP Download request
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>URI of the item to fetch</summary>
        public Uri Address;
        /// <summary>Download progress reporter</summary>
        public IProgress<HttpCapsClient.ProgressReport> DownloadProgressCallback;
        /// <summary>Download completed callback</summary>
        public HttpCapsClient.DownloadCompleteHandler CompletedCallback;
        /// <summary>Accept the following content type</summary>
        public string ContentType;
        /// <summary>How many times will this request be retried</summary>
        public int Retries = 5;
        /// <summary>Current fetch attempt</summary>
        public int Attempt = 0;
        /// <summary>Optional cancellation token for this request</summary>
        public CancellationToken CancellationToken = CancellationToken.None;

        /// <summary>Optional TaskCompletionSource for task-based completion</summary>
        public TaskCompletionSource<(HttpResponseMessage, byte[])> CompletionTcs;

        /// <summary>Constructor</summary>
        public DownloadRequest(Uri address, string contentType,
            IProgress<HttpCapsClient.ProgressReport> downloadProgressCallback,
            HttpCapsClient.DownloadCompleteHandler completedCallback)
        {
            Address = address;
            DownloadProgressCallback = downloadProgressCallback;
            CompletedCallback = completedCallback;
            ContentType = contentType;
        }
    }

    internal class ActiveDownload
    {
        public ConcurrentBag<IProgress<HttpCapsClient.ProgressReport>> ProgressHandlers = new ConcurrentBag<IProgress<HttpCapsClient.ProgressReport>>();
        public ConcurrentBag<TaskCompletionSource<(HttpResponseMessage, byte[])>> CompletedHandlers = new ConcurrentBag<TaskCompletionSource<(HttpResponseMessage, byte[])>>();
        public CancellationTokenSource CancellationToken = new CancellationTokenSource();
        // 0 = not started, 1 = started
        public int Started;
    }

    /// <summary>
    /// Manages async HTTP downloads with a limit on maximum
    /// concurrent downloads
    /// </summary>
    public class DownloadManager : IDisposable
    {
        private readonly ConcurrentQueue<DownloadRequest> queue = new ConcurrentQueue<DownloadRequest>();
        private readonly ConcurrentDictionary<string, ActiveDownload> activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> hostSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

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
                try
                {
                    download.CancellationToken.Cancel();
                    download.CancellationToken.Dispose();
                }
                catch { }
            }

            // Dispose host semaphores
            foreach (var sem in hostSemaphores.Values)
            {
                try { sem.Dispose(); } catch { }
            }

            activeDownloads.Clear();
            hostSemaphores.Clear();
        }

        /// <summary>Check the queue for pending work</summary>
        private void EnqueuePending()
        {
            if (queue.Count <= 0) { return; }

            var nr = activeDownloads.Count;

            // Logger.DebugLog(nr.ToString() + " active downloads. Queued textures: " + queue.Count.ToString());

            for (var i = nr; i < ParallelDownloads && queue.Count > 0; ++i)
            {
                if (!queue.TryDequeue(out var item)) { return; }

                // Normalize key to absolute uri to avoid subtle duplicates
                var addr = item.Address.AbsoluteUri;

                // determine host key (authority includes host:port)
                var hostKey = item.Address.IsAbsoluteUri ? item.Address.Authority : item.Address.Host;

                // Get or create the active download entry
                var activeDownload = activeDownloads.GetOrAdd(addr, _ => new ActiveDownload());

                // Add completion handler as TaskCompletionSource; prefer existing CompletionTcs
                TaskCompletionSource<(HttpResponseMessage, byte[])> completionTcs = item.CompletionTcs ?? new TaskCompletionSource<(HttpResponseMessage, byte[])>(TaskCreationOptions.RunContinuationsAsynchronously);
                // If caller provided legacy callback, forward TCS completion to it
                if (item.CompletedCallback != null)
                {
                    completionTcs.Task.ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                        {
                            try { item.CompletedCallback(null, null, new OperationCanceledException()); } catch { }
                        }
                        else if (t.IsFaulted)
                        {
                            try { item.CompletedCallback(null, null, t.Exception?.InnerException ?? t.Exception); } catch { }
                        }
                        else
                        {
                            try { item.CompletedCallback(t.Result.Item1, t.Result.Item2, null); } catch { }
                        }
                    }, TaskScheduler.Default);
                }
                activeDownload.CompletedHandlers.Add(completionTcs);

                // Add handlers in a thread-safe manner
                if (item.DownloadProgressCallback != null)
                {
                    activeDownload.ProgressHandlers.Add(item.DownloadProgressCallback);
                }

                // If this request provided a cancellation token, register it to cancel the active download
                if (item.CancellationToken.CanBeCanceled)
                {
                    try { item.CancellationToken.Register(() => activeDownload.CancellationToken.Cancel()); } catch { }
                }

                // Only one thread should start the actual HTTP request
                if (Interlocked.Exchange(ref activeDownload.Started, 1) == 0)
                {
                    // Ensure we have a semaphore for this host
                    var sem = hostSemaphores.GetOrAdd(hostKey, _ => new SemaphoreSlim(ParallelDownloads));

                    // Start the request loop for this active download (fire-and-forget)
                    _ = StartActiveDownloadLoop(item, addr, activeDownload, sem);
                }
            }
        }

        private async Task StartActiveDownloadLoop(DownloadRequest representative, string addr, ActiveDownload activeDownload, SemaphoreSlim hostSemaphore)
        {
            // Wait on per-host semaphore before starting the download
            try
            {
                await hostSemaphore.WaitAsync(activeDownload.CancellationToken.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // If waiting was cancelled, notify handlers and exit
                var handlers = activeDownload.CompletedHandlers.ToArray();
                foreach (var handler in handlers)
                {
                    try { handler.TrySetException(new OperationCanceledException()); } catch { }
                }

                // Remove from active downloads and try to start pending items
                activeDownloads.TryRemove(addr, out _);
                EnqueuePending();
                return;
            }

            try
            {
                while (true)
                {
                    HttpResponseMessage response = null;
                    byte[] responseData = null;
                    Exception finalError = null;

                    try
                    {
                        var sw = Stopwatch.StartNew();

                        using (var request = new HttpRequestMessage(HttpMethod.Get, addr))
                        {
                            // Send request and get headers
                            response = await Client.HttpCapsClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, activeDownload.CancellationToken.Token).ConfigureAwait(false);

                            Exception statusError = null;
                            if (!response.IsSuccessStatusCode)
                            {
                                statusError = new HttpRequestException(response.StatusCode + ": " + response.ReasonPhrase);
                            }

                            // Read response stream with progress
                            var totalBytes = response.Content.Headers.ContentLength;
#if NET5_0_OR_GREATER
                            var contentStream = await response.Content.ReadAsStreamAsync(activeDownload.CancellationToken.Token).ConfigureAwait(false);
#else
                            var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif

                            var length = (int)(totalBytes ?? 8192);
                            var ms = totalBytes.HasValue ? null : new System.IO.MemoryStream();
                            var buffer = new byte[length];
                            int bytesRead;
                            var offset = 0;
                            long totalBytesRead = 0;

                            try
                            {
                                while (contentStream != null && (bytesRead = await contentStream.ReadAsync(buffer, offset, length, activeDownload.CancellationToken.Token).ConfigureAwait(false)) != 0)
                                {
                                    totalBytesRead += bytesRead;

                                    if (totalBytes.HasValue)
                                    {
                                        offset += bytesRead;
                                        length -= bytesRead;
                                    }
                                    else
                                    {
                                        ms.Write(buffer, 0, bytesRead);
                                    }

                                    double? progressPercent = null;
                                    if (totalBytes.HasValue)
                                    {
                                        progressPercent = Math.Round((double)totalBytesRead / totalBytes.Value * 100, 2);
                                    }

                                    // Fire progress handlers using IProgress<ProgressReport>
                                    foreach (var handler in activeDownload.ProgressHandlers)
                                    {
                                        try { handler.Report(new HttpCapsClient.ProgressReport(totalBytes, totalBytesRead, progressPercent)); } catch { }
                                    }
                                }

                                if (totalBytes.HasValue)
                                {
                                    responseData = buffer;
                                }
                                else
                                {
                                    responseData = ms.ToArray();
                                    try { ms.Close(); } catch { }
                                    try { ms.Dispose(); } catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Reading failed
                                finalError = ex;
                            }

                            // Prefer pipeline error if any, otherwise status error
                            if (finalError == null)
                                finalError = statusError;

                            // If there was no error or we've exhausted retries or NotFound, finish
                            if (finalError == null || representative.Attempt >= representative.Retries || response.StatusCode == HttpStatusCode.NotFound)
                            {
                                sw.Stop();
                                try {
                                    Logger.Trace($"Download completed {addr} attempts={representative.Attempt} status={(int)response.StatusCode} bytes={responseData?.Length ?? 0} time={sw.ElapsedMilliseconds}ms");
                                } catch { }

                                var handlers = activeDownload.CompletedHandlers.ToArray();
                                foreach (var handler in handlers)
                                {
                                    try
                                    {
                                        if (finalError != null)
                                        {
                                            handler.TrySetException(finalError);
                                        }
                                        else
                                        {
                                            handler.TrySetResult((response, responseData));
                                        }
                                    }
                                    catch { }
                                }

                                // Dispose response after handlers
                                try { response.Dispose(); } catch { }

                                break; // exit retry loop
                            }
                            else
                            {
                                // Transient error -> retry
                                representative.Attempt++;
                                Logger.Warn($"{representative.Address} HTTP download failed, trying again retry {representative.Attempt}/{representative.Retries}");

                                // Dispose response before retry/backoff
                                try { response.Dispose(); } catch { }

                                var delay = Math.Min(2000, 200 * representative.Attempt);
                                var jitter = new Random().Next(0, 200);
                                try { await Task.Delay(delay + jitter, activeDownload.CancellationToken.Token).ConfigureAwait(false); } catch { }

                                sw.Stop();
                                try { Logger.Debug($"Download failed {addr} attempts={representative.Attempt} error={finalError?.Message ?? "status"} time={sw.ElapsedMilliseconds}ms"); } catch { }

                                // Requeue the representative for another attempt
                                queue.Enqueue(representative);
                                EnqueuePending();

                                // Break here; the requeued item will be processed again by EnqueuePending
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested - invoke handlers with cancellation exception
                        var handlers = activeDownload.CompletedHandlers.ToArray();
                        foreach (var handler in handlers)
                        {
                            try { handler.TrySetException(new OperationCanceledException()); } catch { }
                        }

                        try { response?.Dispose(); } catch { }

                        break;
                    }
                    catch (Exception ex)
                    {
                        // Unexpected exception - consider retrying with representative retry policy
                        if (representative.Attempt < representative.Retries)
                        {
                            representative.Attempt++;
                            Logger.Warn($"{representative.Address} HTTP download exception, retry {representative.Attempt}/{representative.Retries}: {ex}");
                            try { response?.Dispose(); } catch { }
                            var delay = Math.Min(2000, 200 * representative.Attempt);
                            var jitter = new Random().Next(0, 200);
                            try { await Task.Delay(delay + jitter, activeDownload.CancellationToken.Token).ConfigureAwait(false); } catch { }

                            try { Logger.Debug($"Download exception {addr} attempts={representative.Attempt} error={ex.Message}"); } catch { }

                            queue.Enqueue(representative);
                            EnqueuePending();

                            break;
                        }

                        var handlers = activeDownload.CompletedHandlers.ToArray();
                        foreach (var handler in handlers)
                        {
                            try { handler.TrySetException(ex); } catch { }
                        }

                        try { response?.Dispose(); } catch { }

                        break;
                    }
                }
            }
            finally
            {
                // Release the host semaphore permit
                try { hostSemaphore.Release(); } catch { }

                // Remove from active downloads and try to start pending items
                activeDownloads.TryRemove(addr, out _);
                EnqueuePending();
            }
        }

        /// <summary>Enqueue a new HTTP download</summary>
        public void QueueDownload(DownloadRequest req)
        {
            var addr = req.Address.AbsoluteUri;

            // Fast-path: if an active download exists, attach handlers and return
            if (activeDownloads.TryGetValue(addr, out var existing))
            {
                // Attach completion TCS to existing active download
                var tcs = req.CompletionTcs ?? new TaskCompletionSource<(HttpResponseMessage, byte[])>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (req.CompletedCallback != null)
                {
                    tcs.Task.ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                        {
                            try { req.CompletedCallback(null, null, new OperationCanceledException()); } catch { }
                        }
                        else if (t.IsFaulted)
                        {
                            try { req.CompletedCallback(null, null, t.Exception?.InnerException ?? t.Exception); } catch { }
                        }
                        else
                        {
                            try { req.CompletedCallback(t.Result.Item1, t.Result.Item2, null); } catch { }
                        }
                    }, TaskScheduler.Default);
                }
                existing.CompletedHandlers.Add(tcs);
                if (req.DownloadProgressCallback != null)
                {
                    existing.ProgressHandlers.Add(req.DownloadProgressCallback);
                }
                if (req.CancellationToken.CanBeCanceled)
                {
                    try { req.CancellationToken.Register(() => existing.CancellationToken.Cancel()); } catch { }
                }
                return;
            }

            queue.Enqueue(req);
            EnqueuePending();
        }

        /// <summary>Enqueue a new HTTP download with a cancellation token</summary>
        public void QueueDownload(DownloadRequest req, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
                req.CancellationToken = cancellationToken;

            QueueDownload(req);
        }

        /// <summary>
        /// Enqueue a DownloadRequest and return a Task that completes when the download finishes.
        /// </summary>
        public Task<(HttpResponseMessage response, byte[] data)> QueueDownloadAsync(DownloadRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            if (req.CompletionTcs == null)
                req.CompletionTcs = new TaskCompletionSource<(HttpResponseMessage, byte[])>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (req.CancellationToken.CanBeCanceled)
            {
                try { req.CancellationToken.Register(() => req.CompletionTcs.TrySetCanceled()); } catch { }
            }

            QueueDownload(req);
            return req.CompletionTcs.Task;
        }

        /// <summary>
        /// Async-first API: queue a download and await its completion.
        /// Returns a tuple of the HttpResponseMessage and the response bytes.
        /// </summary>
        /// <param name="address">URI to download</param>
        /// <param name="contentType">Optional expected content type</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="retries">Number of retries for transient failures</param>
        /// <returns>Task that completes with (HttpResponseMessage, byte[] data)</returns>
        public Task<(HttpResponseMessage response, byte[] data)> QueueDownloadAsync(Uri address, string contentType = null,
            IProgress<HttpCapsClient.ProgressReport> progressCallback = null, CancellationToken cancellationToken = default, int retries = 5)
        {
            var tcs = new TaskCompletionSource<(HttpResponseMessage, byte[])>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Completion handler bridges callback to TCS
            HttpCapsClient.DownloadCompleteHandler completeHandler = (response, data, error) =>
            {
                if (error != null)
                {
                    try { tcs.TrySetException(error); } catch { }
                }
                else
                {
                    try { tcs.TrySetResult((response, data)); } catch { }
                }
            };

            // Build a DownloadRequest compatible with existing queue
            var req = new DownloadRequest(address, contentType, progressCallback, completeHandler)
            {
                Retries = retries
            };

            if (cancellationToken.CanBeCanceled)
            {
                req.CancellationToken = cancellationToken;

                // If caller cancels, propagate to the TCS as well
                try
                {
                    cancellationToken.Register(() => tcs.TrySetCanceled());
                }
                catch { }
            }

            // Enqueue using existing API which will deduplicate and attach handlers
            QueueDownload(req, cancellationToken);

            return tcs.Task;
        }

        /// <summary>
        /// Convenience overload that queues a download and returns a Task for the response and data.
        /// </summary>
        public Task<(HttpResponseMessage response, byte[] data)> DownloadAsync(Uri address,
            string contentType, IProgress<HttpCapsClient.ProgressReport> progress, CancellationToken cancellationToken)
        {
            return QueueDownloadAsync(address, contentType, progress, cancellationToken);
        }

        /// <summary>
        /// Convenience overload that queues a download and returns a Task for the response and data.
        /// </summary>
        public Task<(HttpResponseMessage response, byte[] data)> DownloadAsync(Uri address,
            IProgress<HttpCapsClient.ProgressReport> progress, CancellationToken cancellationToken)
        {
            return QueueDownloadAsync(address, null, progress, cancellationToken);
        }
    }
}

