/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025-2026, Sjofn LLC.
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
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
#if NET8_0_OR_GREATER
using System.Threading.Tasks;
#endif

namespace LibreMetaverse
{
    /// <summary>
    /// Main class to expose grid functionality to clients. All managers needed
    /// for sending and receiving data are accessible through this client.
    /// </summary>
    /// <example>
    /// <code>
    /// // Example minimum code required to instantiate class and
    /// // connect to a simulator.
    /// using System;
    /// using System.Collections.Generic;
    /// using System.Text;
    /// using LibreMetaverse;
    ///
    /// namespace FirstBot
    /// {
    ///     class Bot
    ///     {
    ///         public static GridClient Client;
    ///         static async Task Main(string[] args)
    ///         {
    ///             Client = new GridClient(); // instantiates the GridClient class
    ///                                        // to the global Client object
    ///             // Async login to Simulator using the new API
    ///             var loginParams = Client.Network.DefaultLoginParams("FirstName", "LastName", "Password", "FirstBot", "1.0");
    ///             bool success = await Client.Network.LoginAsync(loginParams);
    ///             if (success)
    ///             {
    ///                 Console.WriteLine("Login successful");
    ///             }
    ///
    ///             // Wait for a Keypress
    ///             Console.ReadLine();
    ///             // Logout of simulator
    ///             Client.Network.Logout();
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public partial class GridClient : IGridClient
#if NET8_0_OR_GREATER
        , IAsyncDisposable
#endif
    {
        /// <summary>Networking subsystem</summary>
        public NetworkManager Network { get; private set; }
        /// <summary>Settings class including constant values and changeable
        /// parameters for everything</summary>
        public Settings Settings { get; private set; }
        /// <summary>Parcel (subdivided simulator lots) subsystem</summary>
        public ParcelManager Parcels { get; private set; }
        /// <summary>Our own avatars subsystem</summary>
        public AgentManager Self { get; private set; }
        /// <summary>Other avatars subsystem</summary>
        public AvatarManager Avatars { get; private set; }
        /// <summary>Estate subsystem</summary>
        public EstateTools Estate { get; private set; }
        /// <summary>Friends list subsystem</summary>
        public FriendsManager Friends { get; private set; }
        /// <summary>Grid (aka simulator group) subsystem</summary>
        public GridManager Grid { get; private set; }
        /// <summary>Object subsystem</summary>
        public ObjectManager Objects { get; private set; }
        /// <summary>Group subsystem</summary>
        public GroupManager Groups { get; private set; }
        /// <summary>Asset subsystem</summary>
        public AssetManager Assets { get; private set; }
        /// <summary>Inventory AIS client</summary>
        public InventoryAISClient AisClient { get; private set; }
        /// <summary>Appearance subsystem</summary>
        public AppearanceManager Appearance { get; private set; }
        /// <summary>Inventory subsystem</summary>
        public InventoryManager Inventory { get; private set; }
        /// <summary>Directory searches including classifieds, people, land sales, etc</summary>
        public DirectoryManager Directory { get; private set; }
        /// <summary>Handles land, wind, and cloud height maps</summary>
        public TerrainManager Terrain { get; private set; }
        /// <summary>Handles sound-related networking</summary>
        public SoundManager Sound { get; private set; }
        /// <summary>Throttling total bandwidth usage, or allocating bandwidth
        /// for specific data stream types</summary>
        public AgentThrottle Throttle { get; private set; }
        /// <summary>Utilization statistics, obviously</summary>
        public Stats.UtilizationStatistics Stats { get; private set; }
        /// <summary>HttpClient chiefly used for Caps</summary>
        public HttpCapsClient HttpCapsClient { get; set; }
        /// <summary>Per-category rate limiter applied to all caps HTTP requests</summary>
        public CapsRateLimiter CapsRateLimiter { get; private set; }
        /// <summary>Second Life Marketplace subsystem</summary>
        public Marketplace.MarketplaceManager Marketplace { get; private set; }
        /// <summary>EEP (Extended Environment Protocol) and legacy WindLight environment subsystem</summary>
        public EnvironmentManager Environment { get; private set; }
        /// <summary>Interest list mode subsystem (controls simulator object update culling)</summary>
        public InterestListManager InterestList { get; private set; }
        /// <summary>Animesh animation runtime (tracks BVH animations playing on rigged mesh objects)</summary>
        public Animesh.AnimeshManager Animesh { get; private set; }

        /// <summary>
        /// Time provider used for all time-dependent operations (throttling, timeouts, retry back-off).
        /// Override with a fake implementation in tests to control time deterministically.
        /// </summary>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>Current UTC time from the configured <see cref="TimeProvider"/>.</summary>
        internal DateTime UtcNow => TimeProvider.GetUtcNow().UtcDateTime;

        /// <summary>
        /// Default constructor
        /// </summary>
        public GridClient()
        {
            // These are order-dependent
            Network = new NetworkManager(this);
            Settings = new Settings(this);
            Parcels = new ParcelManager(this);
            Self = new AgentManager(this);
            Avatars = new AvatarManager(this);
            Estate = new EstateTools(this);
            Friends = new FriendsManager(this);
            Grid = new GridManager(this);
            Objects = new ObjectManager(this);
            Groups = new GroupManager(this);
            Assets = new AssetManager(this);
            Appearance = new AppearanceManager(this);
            CapsRateLimiter = new CapsRateLimiter();
            HttpCapsClient = SetupHttpCapsClient();
            AisClient = new InventoryAISClient(this);
            Inventory = new InventoryManager(this);
            Directory = new DirectoryManager(this);
            Terrain = new TerrainManager(this);
            Sound = new SoundManager(this);
            Throttle = new AgentThrottle(this);
            Stats = new Stats.UtilizationStatistics();
            Marketplace = new Marketplace.MarketplaceManager(this);
            Environment = new EnvironmentManager(this);
            InterestList = new InterestListManager(this);
            Animesh = new Animesh.AnimeshManager(this);
        }

        private static bool ValidateServerCertificate(HttpRequestMessage message, X509Certificate2? cert,
            X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            // *HACK:
            return true;
        }

        private HttpCapsClient SetupHttpCapsClient()
        {
            HttpMessageHandler handler;
#if NETFRAMEWORK
            // .NET Framework's HttpClientHandler wraps HttpWebRequest, which throws while building
            // the response if a non-http(s) Location/Content-Location header is present (e.g. AISv3's
            // "slcaps://" scheme on SlamFolder responses). WinHttpHandler doesn't have this bug.
            // See https://github.com/cinderblocks/libremetaverse/issues/113.
            var winHttpHandler = new WinHttpHandler
            {
                AutomaticRedirection = true,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                ServerCertificateValidationCallback = ValidateServerCertificate
            };

            if (!RuntimeInformation.FrameworkDescription.StartsWith("mono", StringComparison.OrdinalIgnoreCase))
            {
                winHttpHandler.MaxConnectionsPerServer = Settings.MaxHttpConnections;
            }
            handler = winHttpHandler;
#else
            var httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                ServerCertificateCustomValidationCallback = ValidateServerCertificate
            };

            if (!RuntimeInformation.FrameworkDescription.StartsWith("mono", StringComparison.OrdinalIgnoreCase))
            {
                httpClientHandler.MaxConnectionsPerServer = Settings.MaxHttpConnections;
            }
            handler = httpClientHandler;
#endif

            var rateLimitingHandler = new RateLimitingCapsHandler(CapsRateLimiter, handler);
            HttpCapsClient client = new HttpCapsClient(rateLimitingHandler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", $"{Settings.UserAgent}");
            client.Timeout = TimeSpan.FromMilliseconds(Settings.Timing.CapsTimeout);
            return client;
        }

        /// <summary>
        /// Return the full name of this instance
        /// </summary>
        /// <returns>Client avatars full name</returns>
        public override string ToString()
        {
            return Self.Name;
        }

        #region IDisposable
        private bool _disposed;

        /// <summary>
        /// Dispose managed resources and subsystems
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Shut down networking FIRST to cancel all in-flight HTTP requests before
                // HttpCapsClient is disposed, preventing ObjectDisposedException on SslStream.
                try
                {
                    Network?.Shutdown(NetworkManager.DisconnectType.ClientInitiated);
                }
                catch { }

                // Dispose subsystems that implement IDisposable (these may use HttpCapsClient,
                // so they must be disposed before HttpCapsClient is torn down)
                if (Sound is IDisposable soundDisposable) DisposalHelper.SafeDispose(soundDisposable);
                if (Terrain is IDisposable terrainDisposable) DisposalHelper.SafeDispose(terrainDisposable);
                if (Appearance is IDisposable appearanceDisposable) DisposalHelper.SafeDispose(appearanceDisposable);
                if (Inventory is IDisposable inventoryDisposable) DisposalHelper.SafeDispose(inventoryDisposable);
                if (Assets is IDisposable assetsDisposable) DisposalHelper.SafeDispose(assetsDisposable);
                if (Marketplace is IDisposable marketplaceDisposable) DisposalHelper.SafeDispose(marketplaceDisposable);
                if (Parcels is IDisposable parcelsDisposable) DisposalHelper.SafeDispose(parcelsDisposable);
                if (Objects is IDisposable objectsDisposable) DisposalHelper.SafeDispose(objectsDisposable);

                // Now safe to dispose HttpCapsClient — network is shut down and all subsystems
                // that used it have been disposed, so no more requests are in flight.
                if (HttpCapsClient is IDisposable httpDisposable) DisposalHelper.SafeDispose(httpDisposable);
                CapsRateLimiter?.Dispose();

                // Attempt to shutdown logging synchronously to flush providers when possible
                try { Logger.Shutdown(); } catch { }
            }

            _disposed = true;
        }

        ~GridClient()
        {
            Dispose(false);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// Async dispose that ensures logger providers are flushed.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            // Dispose managed resources
            Dispose(true);

            // Suppress finalizer
            GC.SuppressFinalize(this);

            // Await logger shutdown to allow providers to flush
            try { await Logger.ShutdownAsync().ConfigureAwait(false); } catch { }
        }
#endif
        #endregion

    }
}
