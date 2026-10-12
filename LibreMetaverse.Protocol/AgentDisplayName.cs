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

using System;
using System.Diagnostics.CodeAnalysis;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary> Information about agents display name </summary>
    public class AgentDisplayName
    {
        /// <summary> Agent UUID </summary>
        public UUID ID { get; set; }
        /// <summary> Username </summary>
        public string? UserName { get; set; }
        /// <summary> Display name </summary>
        public string? DisplayName { get; set; }
        /// <summary> First name (legacy) </summary>
        public string? LegacyFirstName { get; set; }
        /// <summary> Last name (legacy) </summary>
        public string? LegacyLastName { get; set; }
        /// <summary> Full name (legacy) </summary>
        public string LegacyFullName => $"{LegacyFirstName} {LegacyLastName}";
        /// <summary> Is display name default display name </summary>
        public bool IsDefaultDisplayName { get; set; }
        /// <summary> Cache display name until </summary>
        public DateTime NextUpdate { get; set; }
        /// <summary> Last updated timestamp </summary>
        public DateTime Updated { get; set; }

        /// <summary>
        /// Creates AgentDisplayName object from OSD
        /// </summary>
        /// <param name="data">Incoming OSD data</param>
        /// <returns>AgentDisplayName object</returns>
        public static AgentDisplayName FromOSD(OSD data)
        {
            AgentDisplayName ret = new AgentDisplayName();

            OSDMap map = (OSDMap)data;
            ret.ID = map["id"];
            ret.UserName = map["username"];
            ret.DisplayName = map["display_name"];
            ret.LegacyFirstName = map["legacy_first_name"];
            ret.LegacyLastName = map["legacy_last_name"];
            ret.IsDefaultDisplayName = map["is_display_name_default"];
            ret.NextUpdate = map["display_name_next_update"];
            ret.Updated = map["last_updated"];

            return ret;
        }

        /// <summary>
        /// Return object as OSD map
        /// </summary>
        /// <returns>OSD containing agent's display name data</returns>
        public OSD GetOSD()
        {
            OSDMap map = new OSDMap
            {
                ["id"] = ID,
                ["username"] = OSD.FromString(UserName ?? string.Empty),
                ["display_name"] = OSD.FromString(DisplayName ?? string.Empty),
                ["legacy_first_name"] = OSD.FromString(LegacyFirstName ?? string.Empty),
                ["legacy_last_name"] = OSD.FromString(LegacyLastName ?? string.Empty),
                ["is_display_name_default"] = IsDefaultDisplayName,
                ["display_name_next_update"] = NextUpdate,
                ["last_updated"] = Updated
            };
            return map;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "StructToString is a debug helper; reflection loss is acceptable here.")]
        public override string ToString()
        {
            return Helpers.StructToString(this);
        }
    }
}
