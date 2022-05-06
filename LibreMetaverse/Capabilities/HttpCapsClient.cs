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
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    public delegate void DownloadProgressHandler(long? totalBytes, long totalBytesRead, double? progressPercentage);
    public delegate void DownloadCompleteHandler(HttpResponseMessage response, byte[] responseData, Exception error);

    public class HttpCapsClient : HttpClient
    {
        public HttpCapsClient(HttpMessageHandler handler) : base(handler)
        {
        }

        #region GET requests

        public async Task GetRequestAsync(Uri uri, DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler)
        {

            using var response = await GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                completeHandler?.Invoke(response, null, 
                    new HttpRequestException(response.StatusCode + " " + response.ReasonPhrase));
            }

            await ProcessResponseAsync(response, completeHandler, progressHandler);
        }

        public async Task GetRequestAsync(Uri uri, DownloadCompleteHandler completeHandler, 
            DownloadProgressHandler progressHandler, CancellationToken cancellationToken)
        {
            
            using var response = await GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                completeHandler?.Invoke(response, null,
                    new HttpRequestException(response.StatusCode + " " + response.ReasonPhrase));
            }

            await ProcessResponseAsync(response, completeHandler, progressHandler, cancellationToken);
        }

        #endregion GET requests


        /// /// /// /// /// /// /// /// /// /// /// /// 

        private static async Task ProcessResponseAsync(HttpResponseMessage response, 
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler)
        {
            var totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();

            var length = (int)(totalBytes ?? 8192);
            var totalSize = totalBytes ?? 0;
            var ms = totalBytes.HasValue ? null : new MemoryStream();
            byte[] buffer = new byte[length];
            byte[] responseData = null;
            Exception error = null;
            var bytesRead = 0;
            var offset = 0;
            var totalBytesRead = 0;

            try
            {
                while (contentStream != null
                       && (bytesRead = await contentStream.ReadAsync(buffer, offset, buffer.Length)) != 0)
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
                    await ms.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            completeHandler?.Invoke(response, responseData, error);
        }

        private static async Task ProcessResponseAsync(HttpResponseMessage response,
            DownloadCompleteHandler completeHandler, DownloadProgressHandler progressHandler, CancellationToken cancellationToken)
        {
            var totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync();

            var length = (int)(totalBytes ?? 8192);
            var totalSize = totalBytes ?? 0;
            var ms = totalBytes.HasValue ? null : new MemoryStream();
            byte[] buffer = new byte[length];
            byte[] responseData = null;
            Exception error = null;
            var bytesRead = 0;
            var offset = 0;
            var totalBytesRead = 0;

            try
            {
                while (contentStream != null
                       && (bytesRead = await contentStream.ReadAsync(buffer, offset, buffer.Length, cancellationToken)) != 0)
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
                    await ms.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            completeHandler?.Invoke(response, responseData, error);
        }
    }
}
