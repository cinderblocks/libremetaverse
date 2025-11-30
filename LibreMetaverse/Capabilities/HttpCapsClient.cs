/*
 * Copyright (c) 2022-2025, Sjofn, LLC.
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

#pragma warning disable CS0618 // Type or member is obsolete (DownloadCompleteHandler)

namespace LibreMetaverse
{
    public class HttpCapsClient : HttpClient
    {
        public const string LLSD_XML = "application/llsd+xml";
        public const string LLSD_BINARY = "application/llsd+binary";
        public const string LLSD_JSON = "application/llsd+json";
        public static readonly MediaTypeHeaderValue HDR_LLSD_XML = new MediaTypeHeaderValue(LLSD_XML);
        public static readonly MediaTypeHeaderValue HDR_LLSD_BINARY = new MediaTypeHeaderValue(LLSD_BINARY);
        public static readonly MediaTypeHeaderValue HDR_LLSD_JSON = new MediaTypeHeaderValue(LLSD_JSON);

        public HttpCapsClient(HttpMessageHandler handler) : base(handler)
        {
        }

        #region GET requests

        /// <summary>
        /// Obsolete callback-based GET request.
        /// Use the Task-based `GetRequestAsync` overloads (without a DownloadCompleteHandler) or
        /// `SendRequestTaskAsync(HttpRequestMessage, CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` instead.
        /// </summary>
        [Obsolete("Use Task-based GetRequestAsync overloads or the Task-based SendRequestTaskAsync and IProgress<ProgressReport> instead.")]
        public async Task GetRequestAsync(Uri uri, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task GetRequestAsync(Uri uri, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await GetRequestAsync(uri, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task GetRequestAsync(Uri uri, CancellationToken cancellationToken)
        {
            await GetRequestAsync(uri, cancellationToken, null, null, null);
        }

        #endregion GET requests

        #region POST requests

        /// <summary>
        /// Obsolete callback-based POST request (raw bytes).
        /// Use the Task-based `PostAsync(Uri, string, byte[], CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PostAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PostRequestAsync(uri, contentType, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken)
        {
            await PostRequestAsync(uri, contentType, payload, cancellationToken, null, null, null);
        }

        /// <summary>
        /// Obsolete callback-based POST request (OSD payload).
        /// Use the Task-based `PostAsync(Uri, OSDFormat, OSD, CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PostAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                try
                {
                    if (progress != null)
                    {
                        request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                    }
                    else
                    {
                        request.Content = new ByteArrayContent(serialized);
                        request.Content.Headers.ContentType = contentType;
                    }

                    await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error("Error in HTTP request", e);
                }
            }
        }

        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PostRequestAsync(uri, format, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken)
        {
            await PostRequestAsync(uri, format, payload, cancellationToken, null, null, null);
        }

        #endregion POST requests

        #region PUT requests

        /// <summary>
        /// Obsolete callback-based PUT request (raw bytes).
        /// Use the Task-based `PutAsync(Uri, string, byte[], CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PutAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PutRequestAsync(uri, contentType, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload,
            CancellationToken cancellationToken)
        {
            await PutRequestAsync(uri, contentType, payload, cancellationToken, null, null, null);
        }

        /// <summary>
        /// Obsolete callback-based PUT request (OSD payload).
        /// Use the Task-based `PutAsync(Uri, OSDFormat, OSD, CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PutAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PutRequestAsync(uri, format, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken)
        {
            await PutRequestAsync(uri, format, payload, cancellationToken, null, null, null);
        }

        #endregion PUT requests

        #region PATCH requests

        /// <summary>
        /// Obsolete callback-based PATCH request (raw bytes).
        /// Use the Task-based `PatchAsync(Uri, string, byte[], CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PatchAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
#if (NETSTANDARD2_1_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PatchRequestAsync(uri, contentType, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload,
            CancellationToken cancellationToken)
        {
            await PatchRequestAsync(uri, contentType, payload, cancellationToken, null, null, null);
        }

        /// <summary>
        /// Obsolete callback-based PATCH request (OSD payload).
        /// Use the Task-based `PatchAsync(Uri, OSDFormat, OSD, CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based PatchAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
#if (NETSTANDARD2_1_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await PatchRequestAsync(uri, format, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken)
        {
            await PatchRequestAsync(uri, format, payload, cancellationToken, null, null, null);
        }

#endregion PATCH requests

        #region DELETE requests

        /// <summary>
        /// Obsolete callback-based DELETE request (raw bytes).
        /// Use the Task-based `DeleteAsync(Uri, string, byte[], CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based DeleteAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await DeleteRequestAsync(uri, contentType, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload,
            CancellationToken cancellationToken)
        {
            await DeleteRequestAsync(uri, contentType, payload, cancellationToken, null, null, null);
        }

        /// <summary>
        /// Obsolete callback-based DELETE request (OSD payload).
        /// Use the Task-based `DeleteAsync(Uri, OSDFormat, OSD, CancellationToken, IProgress&lt;ProgressReport&gt;, ConnectedHandler)` overload instead.
        /// </summary>
        [Obsolete("Use Task-based DeleteAsync overloads and IProgress<ProgressReport> instead.")]
        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                await SendRequestAsync(request, cancellationToken, progress, completeHandler, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            await DeleteRequestAsync(uri, format, payload, cancellationToken, null, completeHandler, connectedHandler);
        }

        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken)
        {
            await DeleteRequestAsync(uri, format, payload, cancellationToken, null, null, null);
        }

        #endregion DELETE requests

        #region Task-based helpers

        private async Task<(HttpResponseMessage response, byte[] data)> SendRequestTaskAsync(HttpRequestMessage request, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            var tcs = new TaskCompletionSource<(HttpResponseMessage, byte[])>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using (var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    connectedHandler?.Invoke(response);

                    await ProcessResponseAsync(response, cancellationToken, progress, (r, data, processError) =>
                    {
                        if (processError != null)
                        {
                            tcs.TrySetException(processError);
                        }
                        else
                        {
                            tcs.TrySetResult((r, data));
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { tcs.TrySetCanceled(); }
            catch (Exception ex) { tcs.TrySetException(ex); }

            return await tcs.Task.ConfigureAwait(false);
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PostAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PostAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PutAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PutAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PatchAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
#if (NET5_0_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PatchAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
#if (NET5_0_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> DeleteAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> DeleteAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, ConnectedHandler connectedHandler = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType.MediaType, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
            }
        }

        #endregion Task-based helpers

        /// /// /// /// /// /// /// /// /// /// /// /// 

        private async Task SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null, ConnectedHandler connectedHandler = null)
        {
            // Forward to the Task-based helper and invoke legacy callbacks for compatibility
            try
            {
                var (response, data) = await SendRequestTaskAsync(request, cancellationToken, progress, connectedHandler).ConfigureAwait(false);
                completeHandler?.Invoke(response, data, null);
            }
            catch (OperationCanceledException oce)
            {
                // Invoke legacy callback with explicit cancellation error to inform callers
                completeHandler?.Invoke(null, null, oce);
            }
            catch (HttpRequestException httpReqEx)
            {
                completeHandler?.Invoke(null, null, httpReqEx);
            }
            catch (IOException ioex)
            {
                completeHandler?.Invoke(null, null, ioex);
            }
            catch (Exception ex)
            {
                // Surface other errors to the legacy callback if present
                completeHandler?.Invoke(null, null, ex);
            }
        }

        private static async Task ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken,
            IProgress<ProgressReport> progress = null, DownloadCompleteHandler completeHandler = null)
        {
            var totalBytes = response.Content.Headers.ContentLength;
#if NET5_0_OR_GREATER
            Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
            Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
            var length = (int)(totalBytes ?? 8192);
            var totalSize = totalBytes ?? 0;
            var ms = totalBytes.HasValue ? null : new MemoryStream();
            var buffer = new byte[length];
            byte[] responseData = null;
            Exception error = null;
            var bytesRead = 0;
            var offset = 0;
            var totalBytesRead = 0;

            try
            {
                while (contentStream != null /* (╯°□°)╯︵ ┻━┻ */
                       && (bytesRead = await contentStream.ReadAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytes.HasValue)
                    {
                        offset += bytesRead;
                        length -= bytesRead;
                    }
                    else
                    {
                        totalSize += (length - bytesRead);
                        ms.Write(buffer, 0, bytesRead);
                    }

                    double? progressPercent = null;
                    if (totalBytes.HasValue)
                    {
                        progressPercent = Math.Round((double)totalBytesRead / totalBytes.Value * 100, 2);
                    }

                    // Report IProgress-style progress
                    progress?.Report(new ProgressReport(totalBytes, totalBytesRead, progressPercent));

                }

                if (totalBytes.HasValue)
                {
                    responseData = buffer;
                }
                else
                {
                    responseData = ms.ToArray();
                    ms.Close();
                    ms.Dispose();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            completeHandler?.Invoke(response, responseData, error);
        }

        private static void SerializeData(OSDFormat format, OSD data,
            out byte[] serializedData, out MediaTypeHeaderValue contentType)
        {
            switch (format)
            {
                case OSDFormat.Xml:
                    serializedData = OSDParser.SerializeLLSDXmlBytes(data);
                    contentType = HDR_LLSD_XML;
                    break;
                case OSDFormat.Binary:
                    serializedData = OSDParser.SerializeLLSDBinary(data);
                    contentType = HDR_LLSD_BINARY;
                    break;
                case OSDFormat.Json:
                default:
                    serializedData = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data));
                    contentType = HDR_LLSD_JSON;
                    break;
            }
        }

        // Typed progress report for upload/download progress
        public struct ProgressReport
        {
            public long? TotalBytes { get; }
            public long BytesTransferred { get; }
            public double? Percent { get; }

            public ProgressReport(long? totalBytes, long bytesTransferred, double? percent)
            {
                TotalBytes = totalBytes;
                BytesTransferred = bytesTransferred;
                Percent = percent;
            }
        }

        // HttpContent wrapper that reports upload progress via IProgress<ProgressReport>
        private sealed class ProgressableStreamContent : HttpContent
        {
            private readonly byte[] _content;
            private readonly int _bufferSize = 81920;
            private readonly IProgress<ProgressReport> _progress;

            public ProgressableStreamContent(byte[] content, string contentType, IProgress<ProgressReport> progress)
            {
                _content = content ?? throw new ArgumentNullException(nameof(content));
                _progress = progress;
                if (!string.IsNullOrEmpty(contentType))
                    Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _content.Length;
                return true;
            }

#if NET5_0_OR_GREATER
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
#else
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
#endif
            {
                using (var ms = new MemoryStream(_content))
                {
                    var buffer = new byte[_bufferSize];
                    long total = _content.Length;
                    long uploaded = 0;
                    int read;
                    while ((read = await ms.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        uploaded += read;
                        double? percent = total > 0 ? Math.Round((double)uploaded / total * 100, 2) : (double?)null;
                        _progress?.Report(new ProgressReport(total, uploaded, percent));
                    }
                }
            }
        }

        /// <summary>
        /// Delegate for progress callbacks (legacy). Prefer using <see cref="IProgress{ProgressReport}"/> and the typed
        /// <see cref="ProgressReport"/>.
        /// </summary>
        [Obsolete("Use IProgress<ProgressReport> and the typed ProgressReport instead.")]
        public delegate void DownloadProgressHandler(long? totalBytes, long totalBytesRead, double? progressPercentage);

        /// <summary>
        /// Delegate invoked when an HTTP request completes (legacy). Consider using Task-based wrappers instead.
        /// </summary>
        [Obsolete("Use Task-based APIs and IProgress<ProgressReport> overloads instead.")]
        public delegate void DownloadCompleteHandler(HttpResponseMessage response, byte[] responseData, Exception error);

        /// <summary>
        /// Delegate invoked when a connection is established (legacy). Prefer stronger typed async APIs.
        /// </summary>
        [Obsolete("Prefer Task-based APIs and IProgress<ProgressReport> overloads instead.")]
        public delegate void ConnectedHandler(HttpResponseMessage response);
    }
}

