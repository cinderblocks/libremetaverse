/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn, LLC
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    /// <summary>
    /// Represents the three possible login outcomes previously expressed as strings
    /// </summary>
    public enum LoginState
    {
        False = 0,
        True = 1,
        Indeterminate = 2
    }

    /// <summary>
    /// The decoded data returned from the login server after a successful login
    /// </summary>
    public class LoginResponseData
    {
        public enum ParseMessageLevel { Info, Warning, Error }

        /// <summary>
        /// A single diagnostic message produced while parsing login response data.
        /// </summary>
        public class ParseMessage
        {
            /// <summary>Severity level of the message.</summary>
            public ParseMessageLevel Level { get; }
            /// <summary>Human-readable message describing the issue or info.</summary>
            public string Message { get; }
            /// <summary>An optional exception associated with the diagnostic.</summary>
            public Exception Exception { get; }

            /// <summary>
            /// Create a new parse diagnostic message.
            /// </summary>
            /// <param name="level">Severity level.</param>
            /// <param name="message">Message text.</param>
            /// <param name="ex">Optional exception captured during parsing.</param>
            public ParseMessage(ParseMessageLevel level, string message, Exception ex = null)
            {
                Level = level;
                Message = message;
                Exception = ex;
            }
        }

        /// <summary>
        /// Result container returned by parsing operations. Contains the parsed
        /// data (when available) and any diagnostics/messages produced during parsing.
        /// </summary>
        public class ParseResult
        {
            /// <summary>True when the parsed response indicates a successful login.</summary>
            public bool Success { get; set; }

            private readonly List<ParseMessage> _messages = new List<ParseMessage>();
            /// <summary>Read-only list of diagnostic messages collected during parsing.</summary>
            public IReadOnlyList<ParseMessage> Messages => _messages;

            /// <summary>The LoginResponseData instance produced by the parse operation.
            /// This is a copy and safe to use independently of the instance that performed parsing.</summary>
            public LoginResponseData ParsedData { get; set; }

            /// <summary>Add a diagnostics message to the result.</summary>
            public void Add(ParseMessageLevel level, string message, Exception ex = null) => _messages.Add(new ParseMessage(level, message, ex));
        }

        /// <summary>
        /// The login outcome returned by the login server.
        /// </summary>
        /// <remarks>
        /// This property represents the three possible login states the server may return:
        /// <list type="bullet">
        /// <item>
        /// <term><see cref="LoginState.True"/></term>
        /// <description>Login succeeded.</description>
        /// </item>
        /// <item>
        /// <term><see cref="LoginState.False"/></term>
        /// <description>Login failed.</description>
        /// </item>
        /// <item>
        /// <term><see cref="LoginState.Indeterminate"/></term>
        /// <description>The server returned an indeterminate result (for example a redirect or challenge).</description>
        /// </item>
        /// </list>
        /// Code that needs to know whether the login succeeded can use the <see cref="Success"/> helper property.
        /// </remarks>
        /// <value>The parsed login state.</value>
        public LoginState Login { get; set; }

        /// <summary>True when Login == LoginState.True</summary>
        public bool Success => Login == LoginState.True;

        public string Reason { get; set; }
        /// <summary>Login message of the day</summary>
        public string Message { get; set; }
        public bool FirstLogin { get; set; }
        public UUID AgentID { get; set; }
        public UUID SessionID { get; set; }
        public UUID SecureSessionID { get; set; }
        public string MfaHash { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string StartLocation { get; set; }
        public string AccountType { get; set; }
        public string AgentAccess { get; set; }
        public string AgentAccessMax { get; set; }
        public string AgentRegionAccess { get; set; }
        public string InitialOutfit { get; set; }
        public Vector3 LookAt { get; set; }
        public HomeInfo Home { get; }
        public int CircuitCode { get; set; }
        public uint RegionX { get; set; }
        public uint RegionY { get; set; }
        public uint RegionSizeX { get; set; }
        public uint RegionSizeY { get; set; }
        public ushort SimPort { get; set; }
        public IPAddress SimIP { get; set; }
        public string SeedCapability { get; set; }
        public BuddyListEntry[] BuddyList { get; set; } = Array.Empty<BuddyListEntry>();
        public int SecondsSinceEpoch { get; set; }
        public string UDPBlacklist { get; set; }
        public int MaxAgentGroups { get; set; }
        public string OpenIDUrl { get; set; }
        public string AgentAppearanceServiceURL { get; set; }
        public string MapServerUrl { get; set; }
        public string SnapshotConfigUrl { get; set; }
        public uint COFVersion { get; set; }
        public AccountLevelBenefits AccountLevelBenefits { get; set; }
        public Dictionary<string, object> PremiumPackages { get; set; } = new Dictionary<string, object>();
        public List<object> ClassifiedCategories { get; set; } = new List<object>();
        public List<object> EventCategories { get; set; } = new List<object>();
        public List<object> GlobalTextures { get; set; } = new List<object>();
        public List<object> UiConfig { get; set; } = new List<object>();

        #region Inventory

        public UUID InventoryRoot { get; set; }
        public UUID LibraryRoot { get; set; }
        public InventoryFolder[] InventorySkeleton { get; set; } = Array.Empty<InventoryFolder>();
        public InventoryFolder[] LibrarySkeleton { get; set; } = Array.Empty<InventoryFolder>();
        public UUID LibraryOwner { get; set; }
        public Dictionary<UUID, UUID> Gestures { get; set; } = new Dictionary<UUID, UUID>();

        #endregion

        #region Redirection

        public string NextMethod { get; set; }
        public string NextUrl { get; set; }
        public string[] NextOptions { get; set; }
        public int NextDuration { get; set; }

        #endregion

        // Keys used in login parsing
        private static class Keys
        {
            public const string AgentId = "agent_id";
            public const string SessionId = "session_id";
            public const string SecureSessionId = "secure_session_id";
            public const string FirstName = "first_name";
            public const string LastName = "last_name";
            public const string StartLocation = "start_location";
            public const string AgentAccess = "agent_access";
            public const string AgentAccessMax = "agent_access_max";
            public const string AgentRegionAccess = "agent_region_access";
            public const string LookAt = "look_at";
            public const string Reason = "reason";
            public const string Message = "message";
            public const string Login = "login";
            public const string NextUrl = "next_url";
            public const string NextDuration = "next_duration";
            public const string NextMethod = "next_method";
            public const string NextOptions = "next_options";
            public const string CircuitCode = "circuit_code";
            public const string RegionX = "region_x";
            public const string RegionY = "region_y";
            public const string RegionSizeX = "region_size_x";
            public const string RegionSizeY = "region_size_y";
            public const string SimPort = "sim_port";
            public const string SimIP = "sim_ip";
            public const string SeedCapability = "seed_capability";
            public const string BuddyList = "buddy-list";
            public const string BuddyId = "buddy_id";
            public const string BuddyRightsGiven = "buddy_rights_given";
            public const string BuddyRightsHas = "buddy_rights_has";
            public const string SecondsSinceEpoch = "seconds_since_epoch";
            public const string InventoryRoot = "inventory-root";
            public const string FolderId = "folder_id";
            public const string InventorySkeleton = "inventory-skeleton";
            public const string InventoryLibOwner = "inventory-lib-owner";
            public const string InventoryLibRoot = "inventory-lib-root";
            public const string InventorySkelLib = "inventory-skel-lib";
            public const string AccountType = "account_type";
            public const string AgentAppearanceService = "agent_appearance_service";
            public const string CofVersion = "cof_version";
            public const string MapServerUrl = "map-server-url";
            public const string OpenIdUrl = "openid_url";
            public const string SnapshotConfigUrl = "snapshot_config_url";
            public const string UdpBlacklist = "udp_blacklist";
            public const string MfaHash = "mfa_hash";
            public const string AccountLevelBenefits = "account_level_benefits";
            public const string ClassifiedCategories = "classified_categories";
            public const string EventCategories = "event_categories";
            public const string GlobalTextures = "global-textures";
            public const string PremiumPackages = "premium_packages";
            public const string UiConfig = "ui-config";
            public const string MaxAgentGroups = "max-agent-groups";
            public const string InitialOutfit = "initial-outfit";
            public const string ItemId = "item_id";

            public static class HomeInfo
            {
                public const string Key = "home_info";
                public const string Home = "home";
                public const string Position = "position";
                public const string RegionHandle = "region_handle";
                public const string LookAt = "look_at";
            }
        }

        /// <summary>
        /// Create an empty LoginResponseData instance.
        /// </summary>
        public LoginResponseData()
        {
            Home = new HomeInfo();
        }

        /// <summary>
        /// Create a deep-ish copy of another LoginResponseData instance.
        /// Used by the diagnostics-aware parsing to return an immutable result object.
        /// </summary>
        /// <param name="other">Source to copy from.</param>
        private LoginResponseData(LoginResponseData other)
        {
            // shallow copy of simple properties; collections will be shallow-copied where needed
            Login = other.Login;
            Reason = other.Reason;
            Message = other.Message;
            FirstLogin = other.FirstLogin;
            AgentID = other.AgentID;
            SessionID = other.SessionID;
            SecureSessionID = other.SecureSessionID;
            MfaHash = other.MfaHash;
            FirstName = other.FirstName;
            LastName = other.LastName;
            StartLocation = other.StartLocation;
            AccountType = other.AccountType;
            AgentAccess = other.AgentAccess;
            AgentAccessMax = other.AgentAccessMax;
            AgentRegionAccess = other.AgentRegionAccess;
            InitialOutfit = other.InitialOutfit;
            LookAt = other.LookAt;
            Home = new HomeInfo { RegionHandle = other.Home.RegionHandle, Position = other.Home.Position, LookAt = other.Home.LookAt };
            CircuitCode = other.CircuitCode;
            RegionX = other.RegionX;
            RegionY = other.RegionY;
            RegionSizeX = other.RegionSizeX;
            RegionSizeY = other.RegionSizeY;
            SimPort = other.SimPort;
            SimIP = other.SimIP;
            SeedCapability = other.SeedCapability;
            BuddyList = other.BuddyList?.ToArray() ?? Array.Empty<BuddyListEntry>();
            SecondsSinceEpoch = other.SecondsSinceEpoch;
            UDPBlacklist = other.UDPBlacklist;
            MaxAgentGroups = other.MaxAgentGroups;
            OpenIDUrl = other.OpenIDUrl;
            AgentAppearanceServiceURL = other.AgentAppearanceServiceURL;
            MapServerUrl = other.MapServerUrl;
            SnapshotConfigUrl = other.SnapshotConfigUrl;
            COFVersion = other.COFVersion;
            AccountLevelBenefits = other.AccountLevelBenefits;
            PremiumPackages = other.PremiumPackages != null ? new Dictionary<string, object>(other.PremiumPackages) : new Dictionary<string, object>();
            ClassifiedCategories = other.ClassifiedCategories != null ? new List<object>(other.ClassifiedCategories) : new List<object>();
            EventCategories = other.EventCategories != null ? new List<object>(other.EventCategories) : new List<object>();
            GlobalTextures = other.GlobalTextures != null ? new List<object>(other.GlobalTextures) : new List<object>();
            UiConfig = other.UiConfig != null ? new List<object>(other.UiConfig) : new List<object>();
            InventoryRoot = other.InventoryRoot;
            LibraryRoot = other.LibraryRoot;
            InventorySkeleton = other.InventorySkeleton?.ToArray() ?? Array.Empty<InventoryFolder>();
            LibrarySkeleton = other.LibrarySkeleton?.ToArray() ?? Array.Empty<InventoryFolder>();
            LibraryOwner = other.LibraryOwner;
            Gestures = other.Gestures != null ? new Dictionary<UUID, UUID>(other.Gestures) : new Dictionary<UUID, UUID>();
            NextMethod = other.NextMethod;
            NextUrl = other.NextUrl;
            NextOptions = (string[])other.NextOptions?.Clone();
            NextDuration = other.NextDuration;
        }

        /// <summary>
        /// Parse login response data from an OSDMap and return a diagnostics result containing a parsed copy.
        /// The current instance is left unchanged.
        /// </summary>
        /// <param name="reply">OSDMap containing login response LLSD.</param>
        /// <returns>ParseResult containing parsed data and diagnostics.</returns>
        public ParseResult Parse(OSDMap reply)
        {
            var adapter = new ReplyAdapter(reply);
            var result = new ParseResult();
            ParseCore(adapter, earlyReturnIfNotSuccess: false, result);
            result.Success = Success;
            return result;
        }

        /// <summary>
        /// Parse login response data from a Hashtable and return a diagnostics result containing a parsed copy.
        /// The current instance is left unchanged.
        /// </summary>
        /// <param name="reply">Hashtable containing login response data (legacy XML-RPC style).</param>
        /// <returns>ParseResult containing parsed data and diagnostics.</returns>
        public ParseResult Parse(Hashtable reply)
        {
            var adapter = new ReplyAdapter(reply);
            var result = new ParseResult();
            ParseCore(adapter, earlyReturnIfNotSuccess: true, result);
            result.Success = Success;
            return result;
        }

        // Centralized parsing implementation used by both Parse overloads
        // diagnostics is optional; when provided, parsing issues are added to it in addition to logging
        private void ParseCore(ReplyAdapter adapter, bool earlyReturnIfNotSuccess, ParseResult diagnostics = null)
        {
            // delegate to instance-aware parser to allow parsing into a supplied object
            ParseInto(this, adapter, earlyReturnIfNotSuccess, diagnostics);
        }

        // Parse into the provided target instance. This enables ParseWithDiagnostics to parse
        // into a new immutable instance while leaving the current object unchanged.
        private static void ParseInto(LoginResponseData target, ReplyAdapter adapter, bool earlyReturnIfNotSuccess, ParseResult diagnostics = null)
        {
            try
            {
                target.ParseBasicFields(adapter);
            }
            catch (Exception e)
            {
                Logger.Warn("Login server returned (some) invalid data while parsing basic fields", e);
                diagnostics?.Add(ParseMessageLevel.Warning, "Login server returned (some) invalid data while parsing basic fields", e);
            }

            if (earlyReturnIfNotSuccess && !target.Success) return;

            try
            {
                target.ParseHome(adapter);
            }
            catch (Exception ex)
            {
                Logger.Warn("Could not parse home info from login response. Setting nil", ex);
                diagnostics?.Add(ParseMessageLevel.Warning, "Could not parse home info from login response. Setting nil", ex);
                // reset home fields to defaults
                target.Home.RegionHandle = 0;
                target.Home.Position = Vector3.Zero;
                target.Home.LookAt = Vector3.Zero;
            }

            try
            {
                target.ParseRegionAndCircuit(adapter);
                target.ParseBuddyList(adapter);
                target.ParseInventory(adapter);
                target.ParseOptionalAndLists(adapter);
                target.ParseInitialOutfit(adapter);
            }
            catch (Exception e)
            {
                Logger.Warn("Login server returned (some) invalid data during later parsing", e);
                diagnostics?.Add(ParseMessageLevel.Warning, "Login server returned (some) invalid data during later parsing", e);
            }
        }

        private void ParseBasicFields(ReplyAdapter adapter)
        {
            AgentID = adapter.GetUUID(Keys.AgentId);
            SessionID = adapter.GetUUID(Keys.SessionId);
            SecureSessionID = adapter.GetUUID(Keys.SecureSessionId);

            FirstName = adapter.GetString(Keys.FirstName).Trim('"').Trim();
            LastName = adapter.GetString(Keys.LastName).Trim('"').Trim();

            StartLocation = adapter.GetString(Keys.StartLocation);
            AgentAccess = adapter.GetString(Keys.AgentAccess);
            AgentAccessMax = adapter.GetString(Keys.AgentAccessMax);
            AgentRegionAccess = adapter.GetString(Keys.AgentRegionAccess);
            LookAt = adapter.GetVector3(Keys.LookAt);
            Reason = adapter.GetString(Keys.Reason);
            Message = adapter.GetString(Keys.Message);

            // login state
            if (adapter.ContainsKey(Keys.Login))
            {
                var s = adapter.GetString(Keys.Login);
                if (string.Equals(s, "indeterminate", StringComparison.OrdinalIgnoreCase))
                {
                    Login = LoginState.Indeterminate;
                }
                else if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || (bool.TryParse(s, out var b) && b))
                {
                    Login = LoginState.True;
                }
                else
                {
                    Login = LoginState.False;
                }

                if (Login == LoginState.Indeterminate && !adapter.IsOSD)
                {
                    NextUrl = adapter.GetString(Keys.NextUrl);
                    NextDuration = (int)adapter.GetUInt(Keys.NextDuration);
                    NextMethod = adapter.GetString(Keys.NextMethod);

                    var opts = adapter.GetArrayList(Keys.NextOptions);
                    if (opts != null) NextOptions = (string[])opts.ToArray(typeof(string));
                }
            }
        }

        private void ParseHome(ReplyAdapter adapter)
        {
            // prefer explicit home_info if present
            if (adapter.IsOSD)
            {
                var map = adapter.GetOSDMap(Keys.HomeInfo.Key);
                if (map != null)
                {
                    var hi = Home;
                    hi.Position = ParseVector3(Keys.HomeInfo.Position, map);
                    hi.LookAt = ParseVector3(Keys.HomeInfo.LookAt, map);

                    if (map.TryGetValue(Keys.HomeInfo.RegionHandle, out var value))
                    {
                        var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(value.ToString());
                        if (coords != null && coords.Type == OSDType.Array)
                        {
                            hi.RegionHandle = (coords.Count == 2) ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                        }
                    }

                    return;
                }
            }
            else
            {
                var ht = adapter.GetHashtable(Keys.HomeInfo.Key);
                if (ht != null)
                {
                    var hi = Home;
                    hi.Position = ParseVector3(Keys.HomeInfo.Position, ht);
                    hi.LookAt = ParseVector3(Keys.HomeInfo.LookAt, ht);

                    if (ht.ContainsKey(Keys.HomeInfo.RegionHandle))
                    {
                        var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(ht[Keys.HomeInfo.RegionHandle].ToString());
                        if (coords != null && coords.Type == OSDType.Array)
                        {
                            hi.RegionHandle = (coords.Count == 2) ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                        }
                    }

                    return;
                }
            }

            // fallback to "home" if RegionHandle still unset or home_info not present
            if (Home.RegionHandle == 0 && adapter.ContainsKey(Keys.HomeInfo.Home))
            {
                if (adapter.IsOSD)
                {
                    var osdStr = adapter.GetString(Keys.HomeInfo.Home);
                    var osdHome = OSDParser.DeserializeLLSDNotation(osdStr);
                    if (osdHome.Type == OSDType.Map)
                    {
                        var home = (OSDMap)osdHome;
                        var hi = Home;
                        if (home.TryGetValue(Keys.HomeInfo.RegionHandle, out var homeRegion) && homeRegion.Type == OSDType.Array)
                        {
                            var coords = (OSDArray)homeRegion;
                            hi.RegionHandle = (coords.Count == 2) ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                        }

                        hi.Position = ParseVector3(Keys.HomeInfo.Position, home);
                        hi.LookAt = ParseVector3(Keys.HomeInfo.LookAt, home);
                        return;
                    }
                }
                else
                {
                    var homeObj = adapter.GetObject(Keys.HomeInfo.Home);
                    if (homeObj is Hashtable map)
                    {
                        var hi = Home;
                        hi.Position = ParseVector3(Keys.HomeInfo.Position, map);
                        hi.LookAt = ParseVector3(Keys.HomeInfo.LookAt, map);

                        if (map.ContainsKey(Keys.HomeInfo.RegionHandle))
                        {
                            var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(map[Keys.HomeInfo.RegionHandle].ToString());
                            if (coords != null && coords.Type == OSDType.Array)
                            {
                                hi.RegionHandle = (coords.Count == 2) ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                            }
                        }

                        return;
                    }
                    else if (homeObj is string osdString)
                    {
                        var osdHome = OSDParser.DeserializeLLSDNotation(osdString);
                        if (osdHome.Type == OSDType.Map)
                        {
                            var home = (OSDMap)osdHome;
                            var hi = Home;

                            if (home.TryGetValue(Keys.HomeInfo.RegionHandle, out var homeRegion) && homeRegion.Type == OSDType.Array)
                            {
                                var coords = (OSDArray)homeRegion;
                                hi.RegionHandle = (coords.Count == 2) ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                            }

                            hi.Position = ParseVector3(Keys.HomeInfo.Position, home);
                            hi.LookAt = ParseVector3(Keys.HomeInfo.LookAt, home);
                            return;
                        }
                    }
                    else
                    {
                        throw new LoginException("Could not parse 'home' in Login Response");
                    }
                }
            }

            // ensure default Home values if nothing parsed
            Home.RegionHandle = 0;
            Home.Position = Vector3.Zero;
            Home.LookAt = Vector3.Zero;
        }

        private void ParseRegionAndCircuit(ReplyAdapter adapter)
        {
            CircuitCode = (int)adapter.GetUInt(Keys.CircuitCode);
            RegionX = adapter.GetUInt(Keys.RegionX);
            RegionY = adapter.GetUInt(Keys.RegionY);

            RegionSizeX = adapter.HasKeyAndUInt(Keys.RegionSizeX) ? adapter.GetUInt(Keys.RegionSizeX) : Simulator.DefaultRegionSizeX;
            RegionSizeY = adapter.HasKeyAndUInt(Keys.RegionSizeY) ? adapter.GetUInt(Keys.RegionSizeY) : Simulator.DefaultRegionSizeY;

            SimPort = (ushort)adapter.GetUInt(Keys.SimPort);
            var simIP = adapter.GetString(Keys.SimIP);
            if (IPAddress.TryParse(simIP, out var parsedIp)) SimIP = parsedIp; else SimIP = null;
            SeedCapability = adapter.GetString(Keys.SeedCapability);

            SecondsSinceEpoch = (int)adapter.GetUInt(Keys.SecondsSinceEpoch);
        }

        private void ParseBuddyList(ReplyAdapter adapter)
        {
            if (adapter.IsOSD)
            {
                var buddyArray = adapter.GetOSDArray(Keys.BuddyList);
                if (buddyArray != null)
                {
                    var buddies = new List<BuddyListEntry>();
                    foreach (var t in buddyArray)
                    {
                        if (t.Type != OSDType.Map) continue;
                        var buddy = (OSDMap)t;
                        var bud = new BuddyListEntry
                        {
                            BuddyId = buddy[Keys.BuddyId].AsString(),
                            BuddyRightsGiven = (int)ParseUInt(Keys.BuddyRightsGiven, buddy),
                            BuddyRightsHas = (int)ParseUInt(Keys.BuddyRightsHas, buddy)
                        };
                        buddies.Add(bud);
                    }

                    BuddyList = buddies.ToArray();
                }
            }
            else
            {
                var arr = adapter.GetArrayList(Keys.BuddyList);
                if (arr != null)
                {
                    var buddies = new List<BuddyListEntry>();
                    foreach (var t in arr)
                    {
                        if (!(t is Hashtable buddy)) continue;
                        var bud = new BuddyListEntry
                        {
                            BuddyId = ParseString(Keys.BuddyId, buddy),
                            BuddyRightsGiven = (int)ParseUInt(Keys.BuddyRightsGiven, buddy),
                            BuddyRightsHas = (int)ParseUInt(Keys.BuddyRightsHas, buddy)
                        };
                        buddies.Add(bud);
                    }

                    BuddyList = buddies.ToArray();
                }
            }
        }

        private void ParseInventory(ReplyAdapter adapter)
        {
            InventoryRoot = adapter.GetMappedUUID(Keys.InventoryRoot, Keys.FolderId);
            InventorySkeleton = adapter.IsOSD ? ParseInventorySkeleton(Keys.InventorySkeleton, adapter.OSD) : ParseInventorySkeleton(Keys.InventorySkeleton, adapter.Table);

            LibraryOwner = adapter.GetMappedUUID(Keys.InventoryLibOwner, Keys.AgentId);
            LibraryRoot = adapter.GetMappedUUID(Keys.InventoryLibRoot, Keys.FolderId);
            LibrarySkeleton = adapter.IsOSD ? ParseInventorySkeleton(Keys.InventorySkelLib, adapter.OSD) : ParseInventorySkeleton(Keys.InventorySkelLib, adapter.Table);
        }

        private void ParseOptionalAndLists(ReplyAdapter adapter)
        {
            AccountType = adapter.HasKey(Keys.AccountType) ? adapter.GetString(Keys.AccountType) : AccountType;
            AgentAppearanceServiceURL = adapter.HasKey(Keys.AgentAppearanceService) ? adapter.GetString(Keys.AgentAppearanceService) : AgentAppearanceServiceURL;
            COFVersion = adapter.HasKey(Keys.CofVersion) ? adapter.GetUInt(Keys.CofVersion) : COFVersion;
            MapServerUrl = adapter.HasKey(Keys.MapServerUrl) ? adapter.GetString(Keys.MapServerUrl) : MapServerUrl;
            OpenIDUrl = adapter.HasKey(Keys.OpenIdUrl) ? adapter.GetString(Keys.OpenIdUrl) : OpenIDUrl;
            SnapshotConfigUrl = adapter.HasKey(Keys.SnapshotConfigUrl) ? adapter.GetString(Keys.SnapshotConfigUrl) : SnapshotConfigUrl;
            UDPBlacklist = adapter.HasKey(Keys.UdpBlacklist) ? adapter.GetString(Keys.UdpBlacklist) : UDPBlacklist;

            if (adapter.HasKey(Keys.MfaHash)) MfaHash = adapter.GetString(Keys.MfaHash);

            if (adapter.HasKey(Keys.AccountLevelBenefits))
            {
                if (adapter.IsOSD)
                {
                    var alb = adapter.GetOSDMap(Keys.AccountLevelBenefits);
                    if (alb != null) AccountLevelBenefits = new AccountLevelBenefits(alb);
                }
                else
                {
                    var table = adapter.GetHashtable(Keys.AccountLevelBenefits);
                    if (table != null) AccountLevelBenefits = new AccountLevelBenefits(table);
                }
            }

            // ClassifiedCategories
            if (adapter.IsOSD)
            {
                var al = adapter.GetOSDArray(Keys.ClassifiedCategories);
                if (al != null) ClassifiedCategories = al.ToArrayList().Cast<object>().ToList();
            }
            else
            {
                var al = adapter.GetArrayList(Keys.ClassifiedCategories);
                if (al != null) ClassifiedCategories = al.Cast<object>().ToList();
            }

            // EventCategories
            if (adapter.IsOSD)
            {
                var al = adapter.GetOSDArray(Keys.EventCategories);
                if (al != null) EventCategories = al.ToArrayList().Cast<object>().ToList();
            }
            else
            {
                var al = adapter.GetArrayList(Keys.EventCategories);
                if (al != null) EventCategories = al.Cast<object>().ToList();
            }

            // GlobalTextures
            if (adapter.IsOSD)
            {
                var al = adapter.GetOSDArray(Keys.GlobalTextures);
                if (al != null) GlobalTextures = al.ToArrayList().Cast<object>().ToList();
            }
            else
            {
                var al = adapter.GetArrayList(Keys.GlobalTextures);
                if (al != null) GlobalTextures = al.Cast<object>().ToList();
            }

            // PremiumPackages
            if (adapter.IsOSD)
            {
                var map = adapter.GetOSDMap(Keys.PremiumPackages);
                if (map != null)
                {
                    var ht = map.ToHashtable();
                    var dict = new Dictionary<string, object>(ht.Count);
                    foreach (DictionaryEntry de in ht) dict[(string)de.Key] = de.Value;
                    PremiumPackages = dict;
                }
            }
            else
            {
                var ht = adapter.GetHashtable(Keys.PremiumPackages);
                if (ht != null)
                {
                    var dict = new Dictionary<string, object>(ht.Count);
                    foreach (DictionaryEntry de in ht) dict[(string)de.Key] = de.Value;
                    PremiumPackages = dict;
                }
            }

            // UiConfig
            if (adapter.IsOSD)
            {
                var al = adapter.GetOSDArray(Keys.UiConfig);
                if (al != null) UiConfig = al.ToArrayList().Cast<object>().ToList();
            }
            else
            {
                var al = adapter.GetArrayList(Keys.UiConfig);
                if (al != null) UiConfig = al.Cast<object>().ToList();
            }

            MaxAgentGroups = adapter.HasKey(Keys.MaxAgentGroups) ? (int)adapter.GetUInt(Keys.MaxAgentGroups) : -1;
        }

        private void ParseInitialOutfit(ReplyAdapter adapter)
        {
            InitialOutfit = string.Empty;
            if (adapter.IsOSD)
            {
                var arr = adapter.GetOSDArray(Keys.InitialOutfit);
                if (arr != null) InitialOutfit = string.Join(",", arr.OfType<OSDMap>().Select(map => map[Keys.ItemId].AsString()));
            }
            else
            {
                var arr = adapter.GetArrayList(Keys.InitialOutfit);
                if (arr != null) InitialOutfit = string.Join(",", arr.OfType<Hashtable>().Select(map => ParseString(Keys.ItemId, map)));
            }
        }

        // Adapter class to unify OSDMap and Hashtable access and provide typed getters
        private sealed class ReplyAdapter
        {
            public readonly bool IsOSD;
            public readonly OSDMap OSD;
            public readonly Hashtable Table;

            public ReplyAdapter(OSDMap osd)
            {
                IsOSD = true;
                OSD = osd;
            }

            public ReplyAdapter(Hashtable table)
            {
                IsOSD = false;
                Table = table;
            }

            public bool ContainsKey(string key) => IsOSD ? (OSD?.ContainsKey(key) ?? false) : (Table?.ContainsKey(key) ?? false);
            public bool HasKey(string key) => ContainsKey(key);

            public string GetString(string key) => IsOSD ? ParseString(key, OSD) : ParseString(key, Table);
            public UUID GetUUID(string key) => IsOSD ? ParseUUID(key, OSD) : ParseUUID(key, Table);
            public uint GetUInt(string key) => IsOSD ? ParseUInt(key, OSD) : ParseUInt(key, Table);
            public Vector3 GetVector3(string key) => IsOSD ? ParseVector3(key, OSD) : ParseVector3(key, Table);

            public OSDMap GetOSDMap(string key)
            {
                if (!IsOSD) return null;
                if (!OSD.TryGetValue(key, out var val)) return null;
                return val != null && val.Type == OSDType.Map ? (OSDMap)val : null;
            }

            public OSDArray GetOSDArray(string key)
            {
                if (!IsOSD) return null;
                if (!OSD.TryGetValue(key, out var val)) return null;
                return val != null && val.Type == OSDType.Array ? (OSDArray)val : null;
            }

            public Hashtable GetHashtable(string key)
            {
                if (IsOSD) return null;
                if (!Table.ContainsKey(key)) return null;
                return Table[key] as Hashtable;
            }

            public ArrayList GetArrayList(string key)
            {
                if (IsOSD) return null;
                if (!Table.ContainsKey(key)) return null;
                return Table[key] as ArrayList;
            }

            public object GetObject(string key)
            {
                if (IsOSD) return GetString(key);
                return !Table.ContainsKey(key) ? null : Table[key];
            }

            public bool HasKeyAndUInt(string key)
            {
                if (IsOSD) return OSD.ContainsKey(key);
                if (!Table.ContainsKey(key)) return false;
                return Table[key] is int || Table[key] is uint || Table[key] is long || Table[key] is string;
            }

            public UUID GetMappedUUID(string key, string key2)
            {
                return IsOSD ? ParseMappedUUID(key, key2, OSD) : ParseMappedUUID(key, key2, Table);
            }
        }

        #region Parsing Helpers

        public static uint ParseUInt(string key, OSDMap reply)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsUInteger() : 0;
        }

        public static uint ParseUInt(string key, Hashtable reply)
        {
            if (!reply.ContainsKey(key)) return 0;

            var value = reply[key];
            if (value is int i)
                return (uint)i;

            return 0;
        }

        public static UUID ParseUUID(string key, OSDMap reply)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsUUID() : UUID.Zero;
        }

        public static UUID ParseUUID(string key, Hashtable reply)
        {
            if (!reply.ContainsKey(key)) return UUID.Zero;

            return UUID.TryParse((string)reply[key], out var value) ? value : UUID.Zero;
        }

        public static string ParseString(string key, OSDMap reply)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsString() : string.Empty;
        }

        public static string ParseString(string key, Hashtable reply)
        {
            return reply.ContainsKey(key) ? $"{reply[key]}" : string.Empty;
        }

        public static Vector3 ParseVector3(string key, OSDMap reply)
        {
            if (!reply.TryGetValue(key, out var osd)) return Vector3.Zero;

            switch (osd.Type)
            {
                case OSDType.Array:
                    return ((OSDArray)osd).AsVector3();
                case OSDType.String:
                    var array = (OSDArray)OSDParser.DeserializeLLSDNotation(osd.AsString());
                    return array.AsVector3();
            }

            return Vector3.Zero;
        }

        public static Vector3 ParseVector3(string key, Hashtable reply)
        {
            if (!reply.ContainsKey(key)) return Vector3.Zero;
            var value = reply[key];

            if (value is IList list1)
            {
                var list = list1;
                if (list.Count == 3)
                {
                    float.TryParse((string)list[0], out var x);
                    float.TryParse((string)list[1], out var y);
                    float.TryParse((string)list[2], out var z);

                    return new Vector3(x, y, z);
                }
            }
            else if (value is string str)
            {
                var array = (OSDArray)OSDParser.DeserializeLLSDNotation(str);
                return array.AsVector3();
            }

            return Vector3.Zero;
        }

        public static UUID ParseMappedUUID(string key, string key2, OSDMap reply)
        {
            if (!reply.TryGetValue(key, out var folderOSD) || folderOSD.Type != OSDType.Array) { return UUID.Zero; }

            var array = (OSDArray)folderOSD;
            if (array.Count == 1 && array[0].Type == OSDType.Map)
            {
                var map = (OSDMap)array[0];
                if (map.TryGetValue(key2, out var folder))
                    return folder.AsUUID();
            }

            return UUID.Zero;
        }

        public static UUID ParseMappedUUID(string key, string key2, Hashtable reply)
        {
            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList)) { return UUID.Zero; }

            var array = (ArrayList)reply[key];
            if (array.Count == 1 && array[0] is Hashtable)
            {
                var map = (Hashtable)array[0];
                return ParseUUID(key2, map);
            }

            return UUID.Zero;
        }

        public static InventoryFolder[] ParseInventoryFolders(string key, UUID owner, OSDMap reply)
        {
            var folders = new List<InventoryFolder>();

            if (!reply.TryGetValue(key, out var skeleton) || skeleton.Type != OSDType.Array) { return folders.ToArray(); }

            var array = (OSDArray)skeleton;

            folders.AddRange(from t in array
                where t.Type == OSDType.Map
                select (OSDMap)t
                into map
                select new InventoryFolder(map["folder_id"].AsUUID())
                {
                    PreferredType = (FolderType)map["type_default"].AsInteger(),
                    Version = map["version"].AsInteger(),
                    OwnerID = owner,
                    ParentUUID = map["parent_id"].AsUUID(),
                    Name = map["name"].AsString()
                });

            return folders.ToArray();
        }

        public InventoryFolder[] ParseInventorySkeleton(string key, OSDMap reply)
        {
            var folders = new List<InventoryFolder>();

            if (!reply.TryGetValue(key, out var skeleton) || skeleton.Type != OSDType.Array) { return folders.ToArray(); }
            var array = (OSDArray)skeleton;
            folders.AddRange(from t in array
                where t.Type == OSDType.Map
                select (OSDMap)t
                into map
                select new InventoryFolder(map["folder_id"].AsUUID())
                {
                    Name = map["name"].AsString(),
                    ParentUUID = map["parent_id"].AsUUID(),
                    PreferredType = (FolderType)map["type_default"].AsInteger(),
                    Version = map["version"].AsInteger()
                });
            return folders.ToArray();
        }

        public InventoryFolder[] ParseInventorySkeleton(string key, Hashtable reply)
        {
            UUID ownerID;
            ownerID = key.Equals("inventory-skel-lib") ? LibraryOwner : AgentID;

            var folders = new List<InventoryFolder>();

            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList)) { return folders.ToArray(); }

            var array = (ArrayList)reply[key];
            foreach (var t in array)
            {
                if (!(t is Hashtable map)) continue;
                var folder = new InventoryFolder(ParseUUID("folder_id", map))
                {
                    Name = ParseString("name", map),
                    ParentUUID = ParseUUID("parent_id", map),
                    PreferredType = (FolderType)ParseUInt("type_default", map),
                    Version = (int)ParseUInt("version", map),
                    OwnerID = ownerID
                };

                folders.Add(folder);
            }

            return folders.ToArray();
        }

        #endregion Parsing Helpers
    }
}
