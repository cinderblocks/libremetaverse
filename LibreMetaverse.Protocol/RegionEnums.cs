/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
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
    /// Simulator (region) properties
    /// </summary>
    [Flags]
    public enum RegionFlags : ulong
    {
        /// <summary>No flags set</summary>
        None = 0,
        /// <summary>Agents can take damage and be killed</summary>
        AllowDamage = 1UL << 0,
        /// <summary>Landmarks can be created here</summary>
        AllowLandmark = 1UL << 1,
        /// <summary>Home position can be set in this sim</summary>
        AllowSetHome = 1UL << 2,
        /// <summary>Home position is reset when an agent teleports away</summary>
        ResetHomeOnTeleport = 1UL << 3,
        /// <summary>Sun does not move</summary>
        SunFixed = 1UL << 4,
        /// <summary>Allows private parcels (ie. banlines)</summary>
        AllowAccessOverride = 1UL << 5,
        /// <summary>Disable heightmap alterations (agents can still plant foliage)</summary>
        BlockTerraform = 1UL << 6,
        /// <summary>Land cannot be released, sold, or purchased</summary>
        BlockLandResell = 1UL << 7,
        /// <summary>All content is wiped nightly</summary>
        Sandbox = 1UL << 8,
        /// <summary>Unknown: Related to the availability of an overview world map tile.(Think mainland images when zoomed out.)</summary>
        NullLayer = 1UL << 9,
        /// <summary>Unknown: Related to region debug flags. Possibly to skip processing of agent interaction with world. </summary>
        SkipAgentAction = 1UL << 10,
        /// <summary>Region does not update agent prim interest lists. Internal debugging option.</summary>
        SkipUpdateInterestList = 1UL << 11,
        /// <summary>No collision detection for non-agent objects</summary>
        SkipCollisions = 1UL << 12,
        /// <summary>No scripts are ran</summary>
        SkipScripts = 1UL << 13,
        /// <summary>All physics processing is turned off</summary>
        SkipPhysics = 1UL << 14,
        /// <summary>Region can be seen from other regions on world map. (Legacy world map option?) </summary>
        ExternallyVisible = 1UL << 15,
        /// <summary>Region can be seen from mainland on world map. (Legacy world map option?) </summary>
        MainlandVisible = 1UL << 16,
        /// <summary>Agents not explicitly on the access list can visit the region. </summary>
        PublicAllowed = 1UL << 17,
        /// <summary>Traffic calculations are not run across entire region, overrides parcel settings. </summary>
        BlockDwell = 1UL << 18,
        /// <summary>Flight is disabled (not currently enforced by the sim)</summary>
        NoFly = 1UL << 19,
        /// <summary>Allow direct (p2p) teleporting</summary>
        AllowDirectTeleport = 1UL << 20,
        /// <summary>Estate owner has temporarily disabled scripting</summary>
        EstateSkipScripts = 1UL << 21,
        /// <summary>Restricts the usage of the LSL llPushObject function, applies to whole region.</summary>
        RestrictPushObject = 1UL << 22,
        /// <summary>Deny agents with no payment info on file</summary>
        DenyAnonymous = 1UL << 23,
        /// <summary>Deny agents with payment info on file</summary>
        DenyIdentified = 1UL << 24,
        /// <summary>Deny agents who have made a monetary transaction</summary>
        DenyTransacted = 1UL << 25,
        /// <summary>Parcels within the region may be joined or divided by anyone, not just estate owners/managers. </summary>
        AllowParcelChanges = 1UL << 26,
        /// <summary>Abuse reports sent from within this region are sent to the estate owner defined email. </summary>
        AbuseEmailToEstateOwner = 1UL << 27,
        /// <summary>Region is Voice Enabled</summary>
        AllowVoice = 1UL << 28,
        /// <summary>Removes the ability from parcel owners to set their parcels to show in search.</summary>
        BlockParcelSearch = 1UL << 29,
        /// <summary>Deny agents who have not been age verified from entering the region.</summary>
        DenyAgeUnverified = 1UL << 30,
        /// <summary>Deny scripted agents from entering the region</summary>
        DenyBots = 1UL << 31
    }

    /// <summary>
    /// Access level for a simulator
    /// </summary>
    [Flags]
    public enum SimAccess : byte
    {
        /// <summary>Unknown or invalid access level</summary>
        Unknown = 0,
        /// <summary>Trial accounts allowed</summary>
        Trial = 7,
        /// <summary>PG rating</summary>
        PG = 13,
        /// <summary>Mature rating</summary>
        Mature = 21,
        /// <summary>Adult rating</summary>
        Adult = 42,
        /// <summary>Simulator is offline</summary>
        Down = 254,
        /// <summary>Simulator does not exist</summary>
        NonExistent = 255
    }
}
