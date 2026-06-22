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
using LibreMetaverse.StructuredData;

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

        public async Task<(HttpResponseMessage response, byte[] data)> GetAsync(Uri uri,
            CancellationToken cancellationToken, IProgress<ProgressReport>? progress = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task GetRequestAsync(Uri uri, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        #endregion GET requests

        #region POST requests

        public Task PostRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PostAsync(uri, contentType, payload, cancellationToken, progress);

        public Task PostRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PostAsync(uri, format, payload, cancellationToken, progress);

        #endregion POST requests

        #region PUT requests

        public Task PutRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PutAsync(uri, contentType, payload, cancellationToken, progress);

        public Task PutRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PutAsync(uri, format, payload, cancellationToken, progress);

        #endregion PUT requests

        #region PATCH requests

        public Task PatchRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PatchAsync(uri, contentType, payload, cancellationToken, progress);

        public Task PatchRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => PatchAsync(uri, format, payload, cancellationToken, progress);

        #endregion PATCH requests

        #region DELETE requests

        public Task DeleteRequestAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => DeleteAsync(uri, contentType, payload, cancellationToken, progress);

        public Task DeleteRequestAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
            => DeleteAsync(uri, format, payload, cancellationToken, progress);

        #endregion DELETE requests

        #region Task-based helpers

        private async Task<(HttpResponseMessage response, byte[] data)> SendRequestTaskAsync(HttpRequestMessage request, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            using (var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                var totalBytes = response.Content.Headers.ContentLength;
#if NET5_0_OR_GREATER
                Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                var length = (int)(totalBytes ?? 8192);
                MemoryStream? ms = totalBytes.HasValue ? null : new MemoryStream();
                var buffer = new byte[length];
                var bytesRead = 0;
                var offset = 0;
                var totalBytesRead = 0;

                while (contentStream != null
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
                        ms!.Write(buffer, 0, bytesRead);
                    }

                    if (progress != null && totalBytes.HasValue)
                    {
                        var progressPercent = Math.Round((double)totalBytesRead / totalBytes.Value * 100, 2);
                        progress.Report(new ProgressReport(totalBytes, totalBytesRead, progressPercent));
                    }
                    else
                    {
                        progress?.Report(new ProgressReport(totalBytes, totalBytesRead, null));
                    }
                }

                byte[] responseData;
                if (totalBytes.HasValue)
                {
                    responseData = buffer;
                }
                else
                {
                    responseData = ms!.ToArray();
                    ms.Close();
                    ms.Dispose();
                }

                return (response, responseData);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PostAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(payload, contentType ?? string.Empty, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(payload);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PostAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType?.MediaType ?? string.Empty, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PutAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
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

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PutAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType?.MediaType ?? string.Empty, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PatchAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
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

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> PatchAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
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
                    request.Content = new ProgressableStreamContent(serialized, contentType?.MediaType ?? string.Empty, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> DeleteAsync(Uri uri, string contentType, byte[] payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
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

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        public async Task<(HttpResponseMessage response, byte[] data)> DeleteAsync(Uri uri, OSDFormat format, OSD payload, CancellationToken cancellationToken,
            IProgress<ProgressReport>? progress = null)
        {
            SerializeData(format, payload, out var serialized, out var contentType);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                if (progress != null)
                {
                    request.Content = new ProgressableStreamContent(serialized, contentType?.MediaType ?? string.Empty, progress);
                }
                else
                {
                    request.Content = new ByteArrayContent(serialized);
                    request.Content.Headers.ContentType = contentType;
                }

                return await SendRequestTaskAsync(request, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        #endregion Task-based helpers

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

    }
}

