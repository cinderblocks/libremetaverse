/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC.
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

using Microsoft.Extensions.Logging;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    /// <summary>
    /// Controls settings for a <see cref="GridClient"/> instance. Grouped into typed sub-configs
    /// accessible as properties on this class (e.g. <see cref="Timing"/>, <see cref="Agent"/>).
    /// Process-wide settings (shared across all GridClient instances) are static fields on this class.
    /// </summary>
    public class Settings
    {
        #region Protocol Constants

        /// <summary>Main grid login server</summary>
        public const string AgniLoginServer = "https://login.agni.lindenlab.com/cgi-bin/login.cgi";

        /// <summary>Beta grid login server</summary>
        public const string AditiLoginServer = "https://login.aditi.lindenlab.com/cgi-bin/login.cgi";

        /// <summary>Number of milliseconds between sending pings to each sim</summary>
        public const int PingInterval = 2200;

        /// <summary>Millisecond interval between ticks, where all ACKs are sent out and unACKed packet age is checked</summary>
        public const int NetworkTickInterval = 500;

        /// <summary>Maximum size of a packet that we want to send over the wire</summary>
        public const int MaxPacketSize = 1200;

        /// <summary>The maximum value of a packet sequence number before it rolls over back to one</summary>
        public const int MaxSequence = 0xFFFFFF;

        /// <summary>InventoryManager requests inventory information on login</summary>
        public const bool EnableInventoryStore = true;

        /// <summary>InventoryManager requests library information on login</summary>
        public const bool EnableLibraryStore = true;

        #endregion

        #region Process-Wide Statics

        /// <summary>HTTP User-Agent string reported to servers</summary>
        public static string UserAgent = "LibreMetaverse";

        /// <summary>The relative directory where external resources are kept</summary>
        public static string ResourceDir = "linden";

        /// <summary>IP address the client will bind to</summary>
        public static System.Net.IPAddress BindAddress = System.Net.IPAddress.Any;

        /// <summary>Maximum number of HTTP connections to open to a particular endpoint (used for Caps)</summary>
        public static int MaxHttpConnections = 32;

        /// <summary>The maximum size of the sequence number archive, used to check for resent and/or duplicate packets</summary>
        public static int PacketArchiveSize = 1000;

        /// <summary>Capacity of the incoming UDP receive queue; packets received when the queue is full are dropped and counted in SimStats.DroppedPackets</summary>
        public static int UdpReceiveQueueCapacity = 512;

        /// <summary>Timer interval in milliseconds between checks for stalled texture downloads</summary>
        public static float TexturePipelineRefreshInterval = 500.0f;

        /// <summary>Milliseconds to preserve cached simulator data when no client is connected</summary>
        public static int SimulatorPoolTimeout = 2 * 60 * 1000;

        /// <summary>Minimum log level output to the console</summary>
        public static LogLevel LogLevel = LogLevel.Debug;

        #endregion

        #region Sub-Configs

        /// <summary>Login server and authentication settings</summary>
        public ConnectionSettings Connection { get; } = new ConnectionSettings();

        /// <summary>Timeout and interval values for various operations</summary>
        public TimingSettings Timing { get; } = new TimingSettings();

        /// <summary>UDP/packet-layer behaviour (acks, resends, throttling, stats)</summary>
        public PacketSettings Packets { get; } = new PacketSettings();

        /// <summary>Agent state update and multi-sim behaviour</summary>
        public AgentSettings Agent { get; } = new AgentSettings();

        /// <summary>Object, avatar, and terrain tracking and decoding</summary>
        public WorldSettings World { get; } = new WorldSettings();

        /// <summary>Parcel tracking and automatic request behaviour</summary>
        public ParcelSettings Parcel { get; } = new ParcelSettings();

        /// <summary>Local asset cache settings</summary>
        public AssetCacheSettings AssetCache { get; } = new AssetCacheSettings();

        /// <summary>Texture download pipeline settings</summary>
        public TexturePipelineSettings TexturePipeline { get; } = new TexturePipelineSettings();

        /// <summary>Per-client logging behaviour</summary>
        public LoggingSettings Logging { get; } = new LoggingSettings();

        #endregion

        #region Instance Members

        /// <summary>Default color used for viewer particle effects</summary>
        public Color4 DefaultEffectColor = new Color4(255, 0, 0, 255);

        /// <summary>Cost of uploading an asset. Dynamically updated from EconomyData and ViewerBenefits.</summary>
        public int UploadCost { get; internal set; }

        #endregion

        #region Constructor

        private GridClient Client;

        /// <param name="client">Reference to a GridClient object</param>
        public Settings(GridClient client)
        {
            Client = client;
            Client.Network.RegisterCallback(PacketType.EconomyData, EconomyDataHandler);
        }

        #endregion

        #region Packet Callbacks

        protected void EconomyDataHandler(object? sender, PacketReceivedEventArgs e)
        {
            EconomyDataPacket econ = (EconomyDataPacket)e.Packet;
            UploadCost = econ.Info.PriceUpload;
        }

        #endregion
    }
}
