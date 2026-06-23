/*
 * Copyright (c) 2026, Sjofn LLC.
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    /// <summary>
    /// <see cref="DelegatingHandler"/> that acquires a <see cref="CapsRateLimiter"/> lease
    /// before forwarding each HTTP request. Inserted into the <see cref="HttpCapsClient"/>
    /// handler pipeline by <see cref="GridClient"/> at construction time.
    /// </summary>
    internal sealed class RateLimitingCapsHandler : DelegatingHandler
    {
        private readonly CapsRateLimiter _rateLimiter;

        public RateLimitingCapsHandler(CapsRateLimiter rateLimiter, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri == null)
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            using var lease = await _rateLimiter
                .AcquireAsync(request.RequestUri, cancellationToken)
                .ConfigureAwait(false);

            if (!lease.IsAcquired)
            {
                // Queue was full — log and proceed rather than dropping the request
                Logger.Warn($"Caps rate limiter queue full for {request.RequestUri.Host}; proceeding without throttle");
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
