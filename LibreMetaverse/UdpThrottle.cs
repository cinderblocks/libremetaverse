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
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// Outgoing UDP packet throttle categories, mirroring the AgentThrottle categories
    /// used to negotiate download bandwidth with the simulator.
    /// </summary>
    internal enum UdpThrottleCategory
    {
        /// <summary>Control and handshake packets; bypass throttling entirely.</summary>
        Unthrottled = 0,
        /// <summary>General agent and object packets (AgentUpdate, object manipulation, etc.).</summary>
        Task,
        /// <summary>Texture request and acknowledgement packets.</summary>
        Texture,
        /// <summary>Asset transfer request packets.</summary>
        Asset,
    }

    /// <summary>
    /// Per-category token-bucket rate limiter for outgoing UDP packets.
    /// Replaces the previous global 10 ms/packet delay with bandwidth-proportional
    /// throttling derived from the current AgentThrottle settings.
    /// </summary>
    internal sealed class UdpThrottle : IDisposable
    {
        // Replenishment window; smaller = smoother send rate, larger = more burst headroom.
        private static readonly TimeSpan ReplenishPeriod = TimeSpan.FromMilliseconds(100);

        // Minimum bytes per replenishment period so no category is completely starved.
        private const int MinBytesPerPeriod = 200;

        // Allow bursting up to this many replenishment periods' worth of tokens.
        private const int BurstPeriods = 4;

        // (limiter, tokenLimit) pairs — tokenLimit cached to clamp large-packet requests.
        private (TokenBucketRateLimiter Limiter, int TokenLimit) _task;
        private (TokenBucketRateLimiter Limiter, int TokenLimit) _texture;
        private (TokenBucketRateLimiter Limiter, int TokenLimit) _asset;

        private bool _disposed;

        public UdpThrottle(AgentThrottle throttle)
        {
            _task    = CreateBucket(throttle.Task);
            _texture = CreateBucket(throttle.Texture);
            _asset   = CreateBucket(throttle.Asset);
        }

        private static (TokenBucketRateLimiter, int) CreateBucket(float bitsPerSecond)
        {
            int bytesPerPeriod = Math.Max(MinBytesPerPeriod,
                (int)(bitsPerSecond / 8f * ReplenishPeriod.TotalSeconds));
            int tokenLimit = bytesPerPeriod * BurstPeriods;

            var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit           = tokenLimit,
                TokensPerPeriod      = bytesPerPeriod,
                ReplenishmentPeriod  = ReplenishPeriod,
                QueueLimit           = int.MaxValue,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment    = true,
            });

            return (limiter, tokenLimit);
        }

        /// <summary>
        /// Recreate the per-category buckets with rates from the updated AgentThrottle.
        /// Old limiters are disposed after the swap.
        /// </summary>
        public void Update(AgentThrottle throttle)
        {
            var oldTask    = _task;
            var oldTexture = _texture;
            var oldAsset   = _asset;

            _task    = CreateBucket(throttle.Task);
            _texture = CreateBucket(throttle.Texture);
            _asset   = CreateBucket(throttle.Asset);

            oldTask.Limiter.Dispose();
            oldTexture.Limiter.Dispose();
            oldAsset.Limiter.Dispose();
        }

        /// <summary>
        /// Acquire tokens for a packet before it is sent.
        /// Unthrottled packets return immediately. For throttled categories the
        /// call awaits until the token bucket has enough capacity.
        /// </summary>
        public ValueTask AcquireAsync(UdpThrottleCategory category, int dataLength, CancellationToken ct)
        {
            if (_disposed) return default;

            var (limiter, tokenLimit) = category switch
            {
                UdpThrottleCategory.Task    => _task,
                UdpThrottleCategory.Texture => _texture,
                UdpThrottleCategory.Asset   => _asset,
                _                           => (null!, 0),
            };

            if (limiter == null) return default;

            // Clamp to tokenLimit so large packets never exceed the burst ceiling.
            int tokens = Math.Max(1, Math.Min(dataLength, tokenLimit));

            var leaseTask = limiter.AcquireAsync(tokens, ct);
            if (leaseTask.IsCompletedSuccessfully)
            {
                leaseTask.Result.Dispose();
                return default;
            }

            return AwaitAndDispose(leaseTask);
        }

        private static async ValueTask AwaitAndDispose(ValueTask<RateLimitLease> leaseTask)
        {
            using var _ = await leaseTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Classify an outgoing packet type into a throttle category.
        /// Control and handshake packets are unthrottled to ensure they are never delayed.
        /// </summary>
        public static UdpThrottleCategory Classify(PacketType type) => type switch
        {
            // Control / handshake — must never be delayed
            PacketType.UseCircuitCode        => UdpThrottleCategory.Unthrottled,
            PacketType.CompleteAgentMovement => UdpThrottleCategory.Unthrottled,
            PacketType.AgentThrottle         => UdpThrottleCategory.Unthrottled,
            PacketType.LogoutRequest         => UdpThrottleCategory.Unthrottled,
            PacketType.PacketAck             => UdpThrottleCategory.Unthrottled,
            PacketType.StartPingCheck        => UdpThrottleCategory.Unthrottled,
            PacketType.CompletePingCheck     => UdpThrottleCategory.Unthrottled,
            PacketType.CloseCircuit          => UdpThrottleCategory.Unthrottled,

            // Texture requests
            PacketType.RequestImage          => UdpThrottleCategory.Texture,

            // Asset transfer requests
            PacketType.TransferRequest       => UdpThrottleCategory.Asset,
            PacketType.AbortXfer             => UdpThrottleCategory.Asset,

            // Everything else is general task traffic
            _                                => UdpThrottleCategory.Task,
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _task.Limiter.Dispose();
            _texture.Limiter.Dispose();
            _asset.Limiter.Dispose();
        }
    }
}
