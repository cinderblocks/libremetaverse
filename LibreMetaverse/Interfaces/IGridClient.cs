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

namespace LibreMetaverse
{
    /// <summary>
    /// Abstraction over <see cref="GridClient"/> that enables dependency injection and simplifies testing.
    /// Register via <c>services.AddGridClient()</c> and inject <see cref="IGridClient"/> where needed.
    /// </summary>
    public interface IGridClient : IDisposable
    {
        /// <summary>Networking subsystem</summary>
        NetworkManager Network { get; }
        /// <summary>Settings and configuration</summary>
        Settings Settings { get; }
        /// <summary>Parcel (subdivided simulator lots) subsystem</summary>
        ParcelManager Parcels { get; }
        /// <summary>Our own avatar subsystem</summary>
        AgentManager Self { get; }
        /// <summary>Other avatars subsystem</summary>
        AvatarManager Avatars { get; }
        /// <summary>Estate subsystem</summary>
        EstateTools Estate { get; }
        /// <summary>Friends list subsystem</summary>
        FriendsManager Friends { get; }
        /// <summary>Grid (aka simulator group) subsystem</summary>
        GridManager Grid { get; }
        /// <summary>Object subsystem</summary>
        ObjectManager Objects { get; }
        /// <summary>Group subsystem</summary>
        GroupManager Groups { get; }
        /// <summary>Asset subsystem</summary>
        AssetManager Assets { get; }
        /// <summary>Inventory AIS client</summary>
        InventoryAISClient AisClient { get; }
        /// <summary>Appearance subsystem</summary>
        AppearanceManager Appearance { get; }
        /// <summary>Inventory subsystem</summary>
        InventoryManager Inventory { get; }
        /// <summary>Directory searches including classifieds, people, land sales, etc.</summary>
        DirectoryManager Directory { get; }
        /// <summary>Handles land, wind, and cloud height maps</summary>
        TerrainManager Terrain { get; }
        /// <summary>Handles sound-related networking</summary>
        SoundManager Sound { get; }
        /// <summary>Throttling total bandwidth usage, or allocating bandwidth for specific data stream types</summary>
        AgentThrottle Throttle { get; }
        /// <summary>Utilization statistics</summary>
        Stats.UtilizationStatistics Stats { get; }
        /// <summary>HttpClient chiefly used for Caps</summary>
        HttpCapsClient HttpCapsClient { get; }
        /// <summary>Second Life Marketplace subsystem</summary>
        Marketplace.MarketplaceManager Marketplace { get; }
        /// <summary>EEP (Extended Environment Protocol) and legacy WindLight environment subsystem</summary>
        EnvironmentManager Environment { get; }
        /// <summary>Interest list mode subsystem (controls simulator object update culling)</summary>
        InterestListManager InterestList { get; }
        /// <summary>
        /// Time provider used for all time-dependent operations.
        /// Override with a fake implementation in tests to control time deterministically.
        /// </summary>
        TimeProvider TimeProvider { get; set; }
    }
}
