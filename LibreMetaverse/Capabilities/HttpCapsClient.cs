/*
 * Copyright (c) 2022, Sjofn, LLC.
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
        public HttpCapsClient(HttpMessageHandler handler) : base(handler)
        {
        }

        #region GET requests

        public async Task GetRequestAsync(Uri uri, CancellationToken? cancellationToken, 
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task GetRequestAsync(Uri uri, CancellationToken? cancellationToken, DownloadCompleteHandler completeHandler)
        {
            await GetRequestAsync(uri, cancellationToken, completeHandler, null, null);
        }

        #endregion GET requests

        #region POST requests

        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PostRequestAsync(uri, contentType, payload, cancellationToken, completeHandler, null, null);
        }

        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
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
                    Logger.Log("Error in HTTP request; " + e, Helpers.LogLevel.Error, null, e);
                }
            }
        }

        public async Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PostRequestAsync(uri, format, payload, cancellationToken, completeHandler, null, null);
        }

        #endregion POST requests

        #region PUT requests

        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PutRequestAsync(uri, contentType, payload, cancellationToken, completeHandler, null, null);
        }

        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PutRequestAsync(uri, format, payload, cancellationToken, completeHandler, null, null);
        }

        #endregion PUT requests

        #region PATCH requests

        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            // TODO: 2.1 Standard has built in HttpMethod.Patch. Fix when the time comes we can utilize it.
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PatchRequestAsync(uri, contentType, payload, cancellationToken, completeHandler, null, null);
        }

        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            // TODO: 2.1 Standard has built in HttpMethod.Patch. Fix when the time comes we can utilize it.
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await PatchRequestAsync(uri, format, payload, cancellationToken, completeHandler, null, null);
        }

        #endregion PATCH requests

        #region DELETE requests

        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload,
            CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await DeleteRequestAsync(uri, contentType, payload, cancellationToken, completeHandler, null, null);
        }

        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                request.Content = new ByteArrayContent(serialized);
                request.Content.Headers.ContentType = contentType;
                await SendRequestAsync(request, cancellationToken, completeHandler, progressHandler, connectedHandler);
            }
        }

        public async Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler)
        {
            await DeleteRequestAsync(uri, format, payload, cancellationToken, completeHandler, null, null);
        }

        #endregion DELETE requests

        /// /// /// /// /// /// /// /// /// /// /// /// 

        private async Task SendRequestAsync(HttpRequestMessage request, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, ConnectedHandler connectedHandler)
        {
            try
            {
                using (var response = (cancellationToken.HasValue)
                                          ? await SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                                                            cancellationToken.Value)
                                          : await SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        completeHandler?.Invoke(response, null,
                                                new HttpRequestException(response.StatusCode + " " +
                                                                         response.ReasonPhrase));
                    }

                    connectedHandler?.Invoke(response);

                    await ProcessResponseAsync(response, cancellationToken, completeHandler, progressHandler);
                }
            }
            catch (TaskCanceledException)
            {
                // Eat this.
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

        private static async Task ProcessResponseAsync(HttpResponseMessage response, CancellationToken? cancellationToken,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler)
        {
            var totalBytes = response.Content.Headers.ContentLength;
            Stream contentStream = response.Content.ReadAsStreamAsync().Result;

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
                       && ((!cancellationToken.HasValue && (bytesRead = await contentStream.ReadAsync(buffer, offset, length)) != 0)
                           || (cancellationToken.HasValue && (bytesRead = await contentStream.ReadAsync(buffer, offset, length, cancellationToken.Value)) != 0)))
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
                    contentType = new MediaTypeHeaderValue("application/llsd+xml");
                    break;
                case OSDFormat.Binary:
                    serializedData = OSDParser.SerializeLLSDBinary(data);
                    contentType = new MediaTypeHeaderValue("application/llsd+binary");
                    break;
                case OSDFormat.Json:
                default:
                    serializedData = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data));
                    contentType = new MediaTypeHeaderValue("application/llsd+json");
                    break;
            }
        }
    }
}
