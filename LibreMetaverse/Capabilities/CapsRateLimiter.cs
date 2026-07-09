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
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    /// <summary>
    /// Broad categories for caps request rate limiting.
    /// Each category maps to an independently tuned token bucket.
    /// Add new categories here as new subsystems are integrated.
    /// </summary>
    /// <remarks>
    /// Default limits are derived from the Second Life viewer's HTTP policy classes
    /// (llappcorehttp.cpp) and observed server-side throttle behaviour:
    /// <list type="bullet">
    ///   <item>AP_MATERIALS  – concurrency 2 default, max 8; PUT throttle 1 s</item>
    ///   <item>AP_AGENT      – concurrency 2 default, max 32 (name/profile requests)</item>
    ///   <item>AP_INVENTORY  – concurrency 4 default, hard max 4</item>
    ///   <item>AP_UPLOADS    – concurrency 2 default, max 8</item>
    ///   <item>AP_TEXTURE    – concurrency 8 default, max 12</item>
    ///   <item>AP_MESH1/2    – concurrency 8–32</item>
    /// </list>
    /// </remarks>
    public enum CapsCategory
    {
        /// <summary>General or uncategorized caps endpoints</summary>
        Default,
        /// <summary>RenderMaterials endpoint — AP_MATERIALS in the SL viewer (concurrency 2, max 8)</summary>
        RenderMaterials,
        /// <summary>Asset download endpoints: GetTexture, ViewerAsset, GetMesh, GetMesh2</summary>
        AssetFetch,
        /// <summary>Asset upload endpoints: NewFileAgentInventory, UploadBakedTexture, UpdateAvatarAppearance</summary>
        AssetUpload,
        /// <summary>Inventory fetch endpoints: FetchInventory2, FetchInventoryDescendents2, InventoryAPIv3, etc.</summary>
        Inventory,
        /// <summary>EventQueueGet long-poll endpoint — already time-gated by the EQ client itself</summary>
        EventQueue,
        /// <summary>
        /// Avatar name and profile lookups: GetDisplayNames, AvatarPickerSearch, AgentProfile.
        /// Maps to AP_AGENT in the SL viewer (concurrency 2 default, max 32).
        /// </summary>
        DisplayName,
    }

    /// <summary>
    /// Token bucket configuration for one caps rate limit category.
    /// </summary>
    public sealed class CapsRateLimiterOptions
    {
        /// <summary>Maximum burst size; the bucket starts full at this value</summary>
        public int TokenLimit { get; set; }
        /// <summary>Tokens added per <see cref="ReplenishmentPeriod"/></summary>
        public int TokensPerPeriod { get; set; }
        /// <summary>How often tokens are replenished</summary>
        public TimeSpan ReplenishmentPeriod { get; set; }
        /// <summary>
        /// Maximum requests that may queue waiting for tokens.
        /// Requests beyond this limit are issued a non-acquired lease immediately.
        /// </summary>
        public int QueueLimit { get; set; }
    }

    /// <summary>
    /// Rate limiter for caps HTTP requests. Maintains per-<see cref="CapsCategory"/> token buckets
    /// and a URI-to-category map populated as capabilities are discovered during the seed request.
    /// Integrated into the <see cref="HttpCapsClient"/> handler pipeline via
    /// <see cref="RateLimitingCapsHandler"/>; callers do not need to interact with this class directly.
    /// </summary>
    public sealed class CapsRateLimiter : IDisposable
    {
        // Maps well-known cap names to their rate limit category.
        // Derived from SL viewer HTTP policy class assignments; extend as needed.
        private static readonly FrozenDictionary<string, CapsCategory> CapNameToCategory =
            new Dictionary<string, CapsCategory>(StringComparer.OrdinalIgnoreCase)
            {
                // AP_MATERIALS
                ["RenderMaterials"]                    = CapsCategory.RenderMaterials,
                ["ModifyMaterialParams"]                = CapsCategory.RenderMaterials,
                ["ModifyRegion"]                        = CapsCategory.RenderMaterials,

                // AP_TEXTURE / AP_MESH
                ["GetTexture"]                         = CapsCategory.AssetFetch,
                ["ViewerAsset"]                        = CapsCategory.AssetFetch,
                ["GetMesh"]                            = CapsCategory.AssetFetch,
                ["GetMesh2"]                           = CapsCategory.AssetFetch,
                ["GetMetadata"]                        = CapsCategory.AssetFetch,
                ["RequestTextureDownload"]             = CapsCategory.AssetFetch,

                // EventQueue long-poll
                ["EventQueueGet"]                      = CapsCategory.EventQueue,

                // AP_INVENTORY
                ["FetchInventory2"]                    = CapsCategory.Inventory,
                ["FetchInventoryDescendents2"]         = CapsCategory.Inventory,
                ["FetchLib2"]                          = CapsCategory.Inventory,
                ["FetchLibDescendents2"]               = CapsCategory.Inventory,
                ["InventoryAPIv3"]                     = CapsCategory.Inventory,
                ["LibraryAPIv3"]                       = CapsCategory.Inventory,
                ["RequestTaskInventory"]               = CapsCategory.Inventory,

                // AP_UPLOADS
                ["NewFileAgentInventory"]              = CapsCategory.AssetUpload,
                ["NewFileAgentInventoryVariablePrice"] = CapsCategory.AssetUpload,
                ["UploadBakedTexture"]                 = CapsCategory.AssetUpload,
                ["UpdateAvatarAppearance"]             = CapsCategory.AssetUpload,
                ["InventoryThumbnailUpload"]           = CapsCategory.AssetUpload,
                ["UpdateMaterialAgentInventory"]       = CapsCategory.AssetUpload,
                ["UpdateMaterialTaskInventory"]        = CapsCategory.AssetUpload,

                // AP_AGENT — name/profile lookups
                ["GetDisplayNames"]                    = CapsCategory.DisplayName,
                ["SetDisplayName"]                     = CapsCategory.DisplayName,
                ["AvatarPickerSearch"]                 = CapsCategory.DisplayName,
                ["AgentProfile"]                       = CapsCategory.DisplayName,
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private readonly IReadOnlyDictionary<CapsCategory, RateLimiter> _limiters;

        // Populated by RegisterCapUri as each simulator's seed response is parsed.
        private readonly ConcurrentDictionary<string, CapsCategory> _uriToCategory = new();

        private bool _disposed;

        /// <summary>
        /// Creates a <see cref="CapsRateLimiter"/> with conservative default limits
        /// tuned for observed Second Life server behaviour.
        /// </summary>
        public CapsRateLimiter() : this(null) { }

        /// <summary>
        /// Creates a <see cref="CapsRateLimiter"/> with custom per-category overrides.
        /// Any category not present in <paramref name="overrides"/> uses the built-in defaults.
        /// </summary>
        public CapsRateLimiter(IReadOnlyDictionary<CapsCategory, CapsRateLimiterOptions>? overrides)
        {
            var opts = BuildDefaults();
            if (overrides != null)
                foreach (var kv in overrides)
                    opts[kv.Key] = kv.Value;

            var limiters = new Dictionary<CapsCategory, RateLimiter>();
            var defaultOpts = opts[CapsCategory.Default];
#if NET5_0_OR_GREATER
            foreach (CapsCategory cat in Enum.GetValues<CapsCategory>())
#else
            foreach (CapsCategory cat in Enum.GetValues(typeof(CapsCategory)))
#endif
                limiters[cat] = CreateLimiter(opts.TryGetValue(cat, out var o) ? o : defaultOpts);

            _limiters = limiters;
        }

        private static TokenBucketRateLimiter CreateLimiter(CapsRateLimiterOptions o) =>
            new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit           = o.TokenLimit,
                TokensPerPeriod      = o.TokensPerPeriod,
                ReplenishmentPeriod  = o.ReplenishmentPeriod,
                QueueLimit           = o.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment    = true,
            });

        private static Dictionary<CapsCategory, CapsRateLimiterOptions> BuildDefaults() =>
            new Dictionary<CapsCategory, CapsRateLimiterOptions>
            {
                [CapsCategory.Default] = new CapsRateLimiterOptions
                {
                    // Uncategorized caps: moderate burst, steady drain
                    TokenLimit = 20, TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 30,
                },
                [CapsCategory.RenderMaterials] = new CapsRateLimiterOptions
                {
                    // AP_MATERIALS: concurrency 2 default, max 8. Server-enforced PUT throttle
                    // of 1 s in llmaterialmgr.cpp; keep both GET and POST equally conservative.
                    TokenLimit = 4, TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 20,
                },
                [CapsCategory.AssetFetch] = new CapsRateLimiterOptions
                {
                    // AP_TEXTURE concurrency 8 (max 12); AP_MESH1 concurrency 32.
                    // ViewerAsset serves both, so allow generous throughput.
                    TokenLimit = 24, TokensPerPeriod = 12,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 60,
                },
                [CapsCategory.AssetUpload] = new CapsRateLimiterOptions
                {
                    // AP_UPLOADS: concurrency 2 default, max 8. Uploads are expensive.
                    TokenLimit = 4, TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 10,
                },
                [CapsCategory.Inventory] = new CapsRateLimiterOptions
                {
                    // AP_INVENTORY: concurrency 4, hard max 4.
                    TokenLimit = 6, TokensPerPeriod = 3,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 20,
                },
                [CapsCategory.EventQueue] = new CapsRateLimiterOptions
                {
                    // EQ long-polls are already time-gated by Repeat.IntervalAsync;
                    // this bucket just prevents accidental multi-region pile-ups.
                    TokenLimit = 3, TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 3,
                },
                [CapsCategory.DisplayName] = new CapsRateLimiterOptions
                {
                    // AP_AGENT: concurrency 2 default, max 32. GetDisplayNames batches up to
                    // 90 IDs per request, so individual request rate matters more than volume.
                    TokenLimit = 5, TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 15,
                },
            };

        /// <summary>
        /// Associates a capability URI with its rate limit category so that requests
        /// to that URI are throttled appropriately. Called automatically when cap URIs
        /// are discovered from a simulator's seed response.
        /// </summary>
        public void RegisterCapUri(string capName, Uri uri)
        {
            var category = CapNameToCategory.TryGetValue(capName, out var cat)
                ? cat : CapsCategory.Default;
            _uriToCategory[uri.AbsoluteUri] = category;
        }

        /// <summary>
        /// Asynchronously acquires a rate limit lease for a request to <paramref name="uri"/>.
        /// Waits if the appropriate bucket is empty and the queue has space.
        /// The caller must dispose the returned lease.
        /// </summary>
        public ValueTask<RateLimitLease> AcquireAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            var category = _uriToCategory.TryGetValue(uri.AbsoluteUri, out var cat)
                ? cat : CapsCategory.Default;
            return _limiters[category].AcquireAsync(1, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var limiter in _limiters.Values)
                limiter.Dispose();
        }
    }
}
