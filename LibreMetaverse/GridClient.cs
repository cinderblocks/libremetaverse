/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using LibreMetaverse;
#if NET8_0_OR_GREATER
using System.Threading.Tasks;
#endif

namespace OpenMetaverse
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
    /// using OpenMetaverse;
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
    public partial class GridClient : IDisposable
#if NET8_0_OR_GREATER
        , IAsyncDisposable
#endif
    {
        /// <summary>Networking subsystem</summary>
        public NetworkManager Network;
        /// <summary>Settings class including constant values and changeable
        /// parameters for everything</summary>
        public Settings Settings;
        /// <summary>Parcel (subdivided simulator lots) subsystem</summary>
        public ParcelManager Parcels;
        /// <summary>Our own avatars subsystem</summary>
        public AgentManager Self;
        /// <summary>Other avatars subsystem</summary>
        public AvatarManager Avatars;
        /// <summary>Estate subsystem</summary>
        public EstateTools Estate;
        /// <summary>Friends list subsystem</summary>
        public FriendsManager Friends;
        /// <summary>Grid (aka simulator group) subsystem</summary>
        public GridManager Grid;
        /// <summary>Object subsystem</summary>
        public ObjectManager Objects;
        /// <summary>Group subsystem</summary>
        public GroupManager Groups;
        /// <summary>Asset subsystem</summary>
        public AssetManager Assets;
        /// <summary>Inventory AIS client</summary>
        public InventoryAISClient AisClient;
        /// <summary>Appearance subsystem</summary>
        public AppearanceManager Appearance;
        /// <summary>Inventory subsystem</summary>
        public InventoryManager Inventory;
        /// <summary>Directory searches including classifieds, people, land sales, etc</summary>
        public DirectoryManager Directory;
        /// <summary>Handles land, wind, and cloud height maps</summary>
        public TerrainManager Terrain;
        /// <summary>Handles sound-related networking</summary>
        public SoundManager Sound;
        /// <summary>Throttling total bandwidth usage, or allocating bandwidth
        /// for specific data stream types</summary>
        public AgentThrottle Throttle;
        /// <summary>Utilization statistics, obviously</summary>
        public Stats.UtilizationStatistics Stats;
        /// <summary>HttpClient chiefly used for Caps</summary>
        public HttpCapsClient HttpCapsClient;

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
            Inventory = new InventoryManager(this);
            Directory = new DirectoryManager(this);
            Terrain = new TerrainManager(this);
            Sound = new SoundManager(this);
            Throttle = new AgentThrottle(this);
            Stats = new Stats.UtilizationStatistics();

            HttpCapsClient = SetupHttpCapsClient();
            AisClient = new InventoryAISClient(this);
        }

        private HttpCapsClient SetupHttpCapsClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

                    // *HACK:
                    return true;
                }
            };

            if (Utils.GetRunningRuntime() != Utils.Runtime.Mono)
                handler.MaxConnectionsPerServer = Settings.MAX_HTTP_CONNECTIONS;

            HttpCapsClient client = new HttpCapsClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", $"{Settings.USER_AGENT}");
            client.Timeout = System.TimeSpan.FromMilliseconds(Settings.CAPS_TIMEOUT);
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
                // Dispose subsystems that implement IDisposable
                DisposalHelper.SafeDispose(Sound as IDisposable);
                DisposalHelper.SafeDispose(Terrain as IDisposable);
                DisposalHelper.SafeDispose(Appearance as IDisposable);
                DisposalHelper.SafeDispose(Inventory as IDisposable);
                DisposalHelper.SafeDispose(Assets as IDisposable);
                DisposalHelper.SafeDispose(Parcels as IDisposable);
                DisposalHelper.SafeDispose(Objects as IDisposable);

                // Dispose HttpCapsClient which is a HttpClient
                DisposalHelper.SafeDispose(HttpCapsClient);

                // If Network has shutdown needs, attempt a graceful shutdown
                try
                {
                    // Best-effort shutdown of network (if available)
                    Network?.Shutdown( NetworkManager.DisconnectType.ClientInitiated );
                }
                catch { }

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
