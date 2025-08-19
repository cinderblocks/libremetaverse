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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    public delegate void DownloadProgressHandler(long? totalBytes, long totalBytesRead, double? progressPercentage);
    public delegate void DownloadCompleteHandler(HttpResponseMessage response, byte[] responseData, Exception error);
    public delegate void ConnectedHandler(HttpResponseMessage response);

    public static class AsyncHelper
    {
        public static void Sync(Func<Task> func) => Task.Run(func).ConfigureAwait(false);

        public static T Sync<T>(Func<Task<T>> func) => Task.Run(func).Result;

    }

    public class HttpCapsClient : HttpClient
    {
        public static readonly MediaTypeHeaderValue LLSD_XML = new MediaTypeHeaderValue("application/llsd+xml");
        public static readonly MediaTypeHeaderValue LLSD_BINARY = new MediaTypeHeaderValue("application/llsd+binary");
        public static readonly MediaTypeHeaderValue LLSD_JSON = new MediaTypeHeaderValue("application/llsd+json");

        public HttpCapsClient(HttpMessageHandler handler) : base(handler)
        {
        }

        #region GET requests

        public async Task GetRequestAsync(Uri uri, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        #endregion GET requests

        #region POST requests

        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                try
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                    await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
                }
                catch (Exception e)
                {
                    Logger.Log("Error in HTTP request: " + e, Helpers.LogLevel.Error, null, e);
                }
            }
        }

        #endregion POST requests

        #region PUT requests

        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        #endregion PUT requests

        #region PATCH requests

        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
#if (NETSTANDARD2_1_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
#if (NETSTANDARD2_1_OR_GREATER || NET)
            using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

#endregion PATCH requests

        #region DELETE requests

        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler = default, ConnectedHandler connectedHandler = default)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        #endregion DELETE requests

        /// /// /// /// /// /// /// /// /// /// /// /// 

        private async Task SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            try
            {
                using (var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        completeHandler?.Invoke(response, null,
                                                new HttpRequestException(response.StatusCode + ": " +
                                                                         response.ReasonPhrase));
                        return;
                    }

                    connectedHandler?.Invoke(response);

                    await ProcessResponseAsync(response, cancellationToken, completeHandler, progressHandler);
                }
            }
            catch (TaskCanceledException)
            {
                /* noop */
            }
            catch (HttpRequestException httpReqEx)
            {
                completeHandler?.Invoke(null, null, httpReqEx);
            }
            catch (IOException ioex)
            {
                completeHandler?.Invoke(null, null, ioex);
            }
        }

        private static async Task ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler)
        {
            var totalBytes = response.Content.Headers.ContentLength;
#if NET5_0_OR_GREATER
            Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
#else
            Stream contentStream = await response.Content.ReadAsStreamAsync();
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
                       && (bytesRead = await contentStream.ReadAsync(buffer, offset, length, cancellationToken)) != 0)
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

                    progressHandler?.Invoke(totalBytes, totalBytesRead, progressPercent);

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
                    contentType = LLSD_XML;
                    break;
                case OSDFormat.Binary:
                    serializedData = OSDParser.SerializeLLSDBinary(data);
                    contentType = LLSD_BINARY;
                    break;
                case OSDFormat.Json:
                default:
                    serializedData = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data));
                    contentType = LLSD_JSON;
                    break;
            }
        }
    }
}
