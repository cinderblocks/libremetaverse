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
using System.Collections.Generic;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary>
    /// What a client asks a login server for, as the LLSD map it posts. A client fills it in and calls <see cref="ToOSD"/>;
    /// a login server reads it with <see cref="FromOSD"/>.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>The Agents First name</summary>
        public string FirstName { get; set; } = string.Empty;
        /// <summary>The Agents Last name</summary>
        public string LastName { get; set; } = string.Empty;
        /// <summary>The password as it is sent: the MD5 hash, in the form <c>$1$</c> and 32 hex digits (<see cref="HashPassword"/>)</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>Where the agent starts: <c>last</c>, <c>home</c>, or a URI such as <c>uri:hooper&amp;128&amp;152&amp;17</c></summary>
        public string Start { get; set; } = string.Empty;
        /// <summary>The client software channel, for example <c>Second Life Release</c></summary>
        public string Channel { get; set; } = string.Empty;
        /// <summary>The client software version</summary>
        public string Version { get; set; } = string.Empty;
        /// <summary>The platform the client runs on</summary>
        public string Platform { get; set; } = string.Empty;
        /// <summary>The version of the platform the client runs on</summary>
        public string PlatformVersion { get; set; } = string.Empty;
        /// <summary>A hash of the network card's MAC address</summary>
        public string Mac { get; set; } = string.Empty;
        /// <summary>Unknown or deprecated</summary>
        public string ViewerDigest { get; set; } = string.Empty;
        /// <summary>A hash of the first disk drive's ID, to tell this client from others</summary>
        public string Id0 { get; set; } = string.Empty;
        /// <summary>If true, this agent agrees to the Terms of Service of the grid</summary>
        public bool AgreeToTos { get; set; }
        /// <summary>Unknown</summary>
        public bool ReadCritical { get; set; }
        /// <summary>Status of the last application run, sent for statistics</summary>
        public LastExecStatus LastExecEvent { get; set; }

        /// <summary>
        /// Whether the request carries <see cref="Token"/> and <see cref="MfaHash"/>, the fields of multi-factor
        /// authentication. A client that does not do it leaves them out.
        /// </summary>
        public bool HasMfa { get; set; }
        /// <summary>The time based one time password the user entered, in answer to a challenge; otherwise empty</summary>
        public string Token { get; set; } = string.Empty;
        /// <summary>The hash and timestamp saved from a multi-factor challenge answered before; otherwise empty</summary>
        public string MfaHash { get; set; } = string.Empty;

        /// <summary>What the client asks to be sent back, such as <c>inventory-root</c> and <c>buddy-list</c></summary>
        public List<string> Options { get; set; } = new List<string>();

        /// <summary>
        /// The password as it goes to a login server: the MD5 hash of it, as <c>$1$</c> and 32 hex digits, unless it is that already.
        /// Only the first <see cref="MaxPasswordLength"/> characters are hashed, as the login server only reads that many.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (password == null) return string.Empty;

            // As a client has always decided it: 35 characters, or the $1$ prefix, means it is hashed already
            return password.Length == 35 || password.StartsWith("$1$", StringComparison.Ordinal)
                ? password
                : Utils.MD5(password.Length > MaxPasswordLength ? password.Substring(0, MaxPasswordLength) : password);
        }

        /// <summary>How many characters of a password a login server takes into account, as a client has always hashed no more of it</summary>
        public const int MaxPasswordLength = 16;

        /// <summary>The request as the LLSD map a client posts</summary>
        public OSDMap ToOSD()
        {
            var map = new OSDMap
            {
                ["first"] = OSD.FromString(FirstName),
                ["last"] = OSD.FromString(LastName),
                ["passwd"] = OSD.FromString(Password),
                ["start"] = OSD.FromString(Start),
                ["channel"] = OSD.FromString(Channel),
                ["version"] = OSD.FromString(Version),
                ["platform"] = OSD.FromString(Platform),
                ["platform_version"] = OSD.FromString(PlatformVersion),
                ["mac"] = OSD.FromString(Mac),
                ["agree_to_tos"] = OSD.FromBoolean(AgreeToTos),
                ["read_critical"] = OSD.FromBoolean(ReadCritical),
                ["viewer_digest"] = OSD.FromString(ViewerDigest),
                ["id0"] = OSD.FromString(Id0),
                ["last_exec_event"] = OSD.FromInteger((int)LastExecEvent)
            };

            if (HasMfa)
            {
                map["token"] = OSD.FromString(Token);
                map["mfa_hash"] = OSD.FromString(MfaHash);
            }

            var options = new OSDArray();
            foreach (string option in Options)
                options.Add(OSD.FromString(option));
            map["options"] = options;

            return map;
        }

        /// <summary>
        /// Reads a request. What is missing is left empty or false, so a client that sends less than this library does is
        /// still understood.
        /// </summary>
        public static LoginRequest FromOSD(OSDMap map)
        {
            var request = new LoginRequest
            {
                FirstName = String(map, "first"),
                LastName = String(map, "last"),
                Password = String(map, "passwd"),
                Start = String(map, "start"),
                Channel = String(map, "channel"),
                Version = String(map, "version"),
                Platform = String(map, "platform"),
                PlatformVersion = String(map, "platform_version"),
                Mac = String(map, "mac"),
                ViewerDigest = String(map, "viewer_digest"),
                Id0 = String(map, "id0"),
                AgreeToTos = map.TryGetValue("agree_to_tos", out var tos) && tos.AsBoolean(),
                ReadCritical = map.TryGetValue("read_critical", out var critical) && critical.AsBoolean(),
                LastExecEvent = map.TryGetValue("last_exec_event", out var exec) ? (LastExecStatus)exec.AsInteger() : LastExecStatus.Normal,
                HasMfa = map.ContainsKey("token") || map.ContainsKey("mfa_hash"),
                Token = String(map, "token"),
                MfaHash = String(map, "mfa_hash")
            };

            if (map.TryGetValue("options", out var options) && options is OSDArray array)
            {
                foreach (OSD option in array)
                    request.Options.Add(option.AsString());
            }

            return request;
        }

        private static string String(OSDMap map, string key) => map.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }
}
