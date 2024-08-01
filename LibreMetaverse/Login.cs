/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2022, Sjofn, LLC
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

using XmlRpcCore;
using OpenMetaverse.Http;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    #region Enums

    /// <summary>
    /// 
    /// </summary>
    public enum LoginStatus
    {
        /// <summary></summary>
        Failed = -1,
        /// <summary></summary>
        None = 0,
        /// <summary></summary>
        ConnectingToLogin,
        /// <summary></summary>
        ReadingResponse,
        /// <summary></summary>
        ConnectingToSim,
        /// <summary></summary>
        Redirecting,
        /// <summary></summary>
        Success
    }

    /// <summary>
    /// Status of the last application run.
    /// Used for error reporting to the grid login service for statistical purposes.
    /// </summary>
    public enum LastExecStatus
    {
        /// <summary> Application exited normally </summary>
        Normal = 0,
        /// <summary> Application froze </summary>
        Froze,
        /// <summary> Application detected error and exited abnormally </summary>
        ForcedCrash,
        /// <summary> Other crash </summary>
        OtherCrash,
        /// <summary> Application froze during logout </summary>
        LogoutFroze,
        /// <summary> Application crashed during logout </summary>
        LogoutCrash
    }

    #endregion Enums

    #region Structs

    public class LoginCredential
    {
        public string FirstName { get; }
        public string LastName { get; }
        public string Password { get; }
        public string Token { get; }
        public string MfaHash { get; }

        public LoginCredential(string first, string last, string passwd, string token, string mfaHash)
        {
            FirstName = first;
            LastName = last;
            Password = passwd;
            Token = token;
            MfaHash = mfaHash;
        }

        public LoginCredential(string first, string last, string passwd)
            : this(first, last, passwd, string.Empty, string.Empty)
        { }
    }

    /// <summary>
    /// Login Request Parameters
    /// </summary>
    public class LoginParams
    {
        /// <summary>The URL of the Login Server</summary>
        public string URI;
        /// <summary>The number of milliseconds to wait before a login is considered
        /// failed due to timeout</summary>
        public int Timeout;
        /// <summary>The request method</summary>
        /// <remarks>login_to_simulator is currently the only supported method</remarks>
        public string MethodName;
        /// <summary>The Agents First name</summary>
        public string FirstName;
        /// <summary>The Agents Last name</summary>
        public string LastName;
        /// <summary>A md5 hashed password</summary>
        /// <remarks>plaintext password will be automatically hashed</remarks>
        public string Password;
        /// <summary>The user's entered Time based One Time Password (TOTP) token.</summary>
        /// <remarks>This should be the empty string for login attempts that are not responding to an MFA challenge.</remarks>
        public string Token;
        /// <summary>The saved hash value and timestamp from a previously successfully answered MFA challenge.</summary>
        /// <remarks>This should be the empty string initially.</remarks>
        public string MfaHash;
        /// <summary>The agents starting location home or last</summary>
        /// <remarks>Either "last", "home", or a string encoded URI 
        /// containing the simulator name and x/y/z coordinates e.g: uri:hooper&amp;128&amp;152&amp;17</remarks>
        public string Start;
        /// <summary>A string containing the client software channel information</summary>
        /// <example>Second Life Release</example>
        public string Channel;
        /// <summary>The client software version information</summary>
        /// <remarks>The official viewer uses: Second Life Release n.n.n.n 
        /// where n is replaced with the current version of the viewer</remarks>
        public string Version;
        /// <summary>A string containing the platform information the agent is running on</summary>
        public string Platform;
        /// <summary>A string containing version number for OS the agent is running on</summary>
        public string PlatformVersion;
        /// <summary>A string hash of the network cards Mac Address</summary>
        public string MAC;
        /// <summary>Unknown or deprecated</summary>
        public string ViewerDigest;
        /// <summary>A string hash of the first disk drives ID used to identify this clients uniqueness</summary>
        public string ID0;
        /// <summary>A string containing the viewers Software, this is not directly sent to the login server but 
        /// instead is used to generate the Version string</summary>
        public string UserAgent;
        /// <summary>A string representing the software creator. This is not directly sent to the login server but
        /// is used by the library to generate the Version information</summary>
        public string Author;
        /// <summary>If true, this agent agrees to the Terms of Service of the grid its connecting to</summary>
        public bool AgreeToTos;
        /// <summary>Unknown</summary>
        public bool ReadCritical;
        /// <summary>Status of the last application run sent to the grid login server for statistical purposes</summary>
        public LastExecStatus LastExecEvent;

        /// <summary>An array of string sent to the login server to enable various options</summary>
        public List<string> Options;

        /// <summary>A randomly generated ID to distinguish between login attempts. This value is only used
        /// internally in the library and is never sent over the wire</summary>
        internal UUID LoginID;

        /// <summary>LoginLocation used to set the starting region and location (overrides Start) example: "Tentacles/128/64/109"</summary>
        /// <remarks>Leave empty to use the starting location</remarks>
        public string LoginLocation;

        /// <summary>
        /// Is the client Multi-Factor Authentication enabled
        /// </summary>
        public bool MfaEnabled;

        /// <summary>
        /// Default constructor, initializes sane default values
        /// </summary>
        public LoginParams()
        {
            var options = new List<string>(19)
            {
                "inventory-root",
                "inventory-skeleton",
                "initial-outfit",
                "gestures",
                "display_names",
                "event_categories",
                "event_notifications",
                "classified_categories",
                "adult_compliant",
                "buddy-list",
                "newuser-config",
                "ui-config",
                "advanced-mode",
                "max-agent-groups",
                "map-server-url",
                "voice-config",
                "tutorial_settings",
                "login-flags",
                "global-textures"
            };

            var library_options = new List<string>(3)
            {
                "inventory-lib-root",
                "inventory-lib-owner",
                "inventory-skel-lib"
            };

            var opensim_options = new List<string>(6)
            {
                "avatar_picker_url",
                "classified_fee",
                "currency",
                "destination_guide_url",
                //"max_groups", unsupported, OpenSim should use max-agent-groups
                "profile-server-url",
                "search"
            };

            Options = options;
            // *TODO: include library and opensim options when we support them
            MethodName = "login_to_simulator";
            Start = "last";
            Platform = NetworkManager.GetPlatform();
            PlatformVersion = NetworkManager.GetPlatformVersion();
            MAC = NetworkManager.GetHashedMAC();
            ViewerDigest = string.Empty;
            ID0 = NetworkManager.GetHashedMAC();
            AgreeToTos = true;
            ReadCritical = true;
            LastExecEvent = LastExecStatus.Normal;
        }

        /// <summary>
        /// Instantiate LoginParams
        /// </summary>
        /// <remarks>Use this constructor if Application supports multi-factor authentication</remarks>
        /// <param name="client">Instance of <seealso cref="GridClient"/></param>
        /// <param name="credential">Instance of <seealso cref="LoginCredential"/></param>
        /// <param name="channel">Login channel (application name)</param>
        /// <param name="version">Client version, as application name + version number</param>
        public LoginParams(GridClient client, LoginCredential credential, string channel, string version)
            : this()
        {
            URI = client.Settings.LOGIN_SERVER;
            Timeout = client.Settings.LOGIN_TIMEOUT;
            MfaEnabled = client.Settings.MFA_ENABLED;
            FirstName = credential.FirstName;
            LastName = credential.LastName;
            Password = credential.Password;
            Token = credential.Token;
            MfaHash = credential.MfaHash;
            Channel = channel;
            Version = version;
        }

        /// <summary>
        /// Instantiate LoginParams
        /// </summary>
        /// <remarks>Use this constructor if Application supports multi-factor authentication</remarks>
        /// <param name="client">Instance of <seealso cref="GridClient"/></param>
        /// <param name="credential">Instance of <seealso cref="LoginCredential"/></param>
        /// <param name="channel">Login channel (application name)</param>
        /// <param name="version">Client version, as application name + version number</param>
        /// <param name="loginUri">Address of login service</param>
        public LoginParams(GridClient client, LoginCredential credential, string channel, string version, string loginUri)
            : this(client, credential, channel, version)
        {
            URI = loginUri;
        }

        /// <summary>
        /// Instantiates new LoginParams object and fills in the values
        /// </summary>
        /// <param name="client">Instance of GridClient to read settings from</param>
        /// <param name="firstName">Login first name</param>
        /// <param name="lastName">Login last name</param>
        /// <param name="password">Password</param>
        /// <param name="channel">Login channel (application name)</param>
        /// <param name="version">Client version, should be application name + version number</param>
        public LoginParams(GridClient client, string firstName, string lastName, string password, 
            string channel, string version)
            : this()
        {
            URI = client.Settings.LOGIN_SERVER;
            Timeout = client.Settings.LOGIN_TIMEOUT;
            FirstName = firstName;
            LastName = lastName;
            Password = password;
            Channel = channel;
            Version = version;
        }

        /// <summary>
        /// Instantiates new LoginParams object and fills in the values
        /// </summary>
        /// <param name="client">Instance of GridClient to read settings from</param>
        /// <param name="firstName">Login first name</param>
        /// <param name="lastName">Login last name</param>
        /// <param name="password">Password</param>
        /// <param name="channel">Login channel (application name)</param>
        /// <param name="version">Client version, should be application name + version number</param>
        /// <param name="loginURI">URI of the login server</param>
        public LoginParams(GridClient client, string firstName, string lastName, string password, 
            string channel, string version, string loginURI)
            : this(client, firstName, lastName, password, channel, version)
        {
            URI = loginURI;
        }
    }

    public struct BuddyListEntry
    {
        public int BuddyRightsGiven;
        public string BuddyId;
        public int BuddyRightsHas;
    }

    public struct HomeInfo
    {
        public ulong RegionHandle;
        public Vector3 Position;
        public Vector3 LookAt;
    }

    /// <summary>
    /// The decoded data returned from the login server after a successful login
    /// </summary>
    public struct LoginResponseData
    {
        /// <summary>true, false, indeterminate</summary>
        //[XmlRpcMember("login")]
        public string Login;
        public bool Success;
        public string Reason;
        /// <summary>Login message of the day</summary>
        public string Message;
        public bool FirstLogin;
        public UUID AgentID;
        public UUID SessionID;
        public UUID SecureSessionID;
        public string MfaHash;
        public string FirstName;
        public string LastName;
        public string StartLocation;
        public string AccountType;
        public string AgentAccess;
        public string AgentAccessMax;
        public string AgentRegionAccess;
        public string InitialOutfit;
        public Vector3 LookAt;
        public HomeInfo Home;
        public int CircuitCode;
        public uint RegionX;
        public uint RegionY;
        public ushort SimPort;
        public IPAddress SimIP;
        public string SeedCapability;
        public BuddyListEntry[] BuddyList;
        public int SecondsSinceEpoch;
        public string UDPBlacklist;
        public int MaxAgentGroups;
        public string OpenIDUrl;
        public string AgentAppearanceServiceURL;
        public string MapServerUrl;
        public string SnapshotConfigUrl;
        public uint COFVersion;
        public Hashtable AccountLevelBenefits;
        public Hashtable PremiumPackages;
        public ArrayList ClassifiedCategories;
        public ArrayList EventCategories;
        public ArrayList GlobalTextures;
        public ArrayList UiConfig;

        #region Inventory

        public UUID InventoryRoot;
        public UUID LibraryRoot;
        public InventoryFolder[] InventorySkeleton;
        public InventoryFolder[] LibrarySkeleton;
        public UUID LibraryOwner;
        public Dictionary<UUID, UUID> Gestures;

        #endregion

        #region Redirection

        public string NextMethod;
        public string NextUrl;
        public string[] NextOptions;
        public int NextDuration;

        #endregion

        /// <summary>
        /// Parse LLSD Login Reply Data
        /// </summary>
        /// <param name="reply">An <seealso cref="OSDMap"/> 
        /// containing the login response data</param>
        /// <remarks>XML-RPC logins do not require this as XML-RPC.NET 
        /// automatically populates the struct properly using attributes</remarks>
        public void Parse(OSDMap reply)
        {
            try
            {
                AgentID = ParseUUID("agent_id", reply);
                SessionID = ParseUUID("session_id", reply);
                SecureSessionID = ParseUUID("secure_session_id", reply);
                FirstName = ParseString("first_name", reply).Trim('"').Trim(); // lol but necessary, unfortunately.
                LastName = ParseString("last_name", reply).Trim('"').Trim();
                StartLocation = ParseString("start_location", reply);
                AgentAccess = ParseString("agent_access", reply);
                AgentAccessMax = ParseString("agent_access_max", reply);
                AgentRegionAccess = ParseString("agent_region_access", reply);
                LookAt = ParseVector3("look_at", reply);
                Reason = ParseString("reason", reply);
                Message = ParseString("message", reply);

                Login = reply["login"].AsString();
                Success = reply["login"].AsBoolean();
            }
            catch (OSDException e)
            {
                Logger.Log("Login server returned (some) invalid data", Helpers.LogLevel.Warning, e);
            }

            // Home
            if (reply.ContainsKey("home_info"))
            {
                if (reply["home_info"].Type == OSDType.Map)
                {
                    var map = (OSDMap)reply["home_info"];
                    Home.Position = ParseVector3("position", map);
                    Home.LookAt = ParseVector3("look_at", map);

                    var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(map["region_handle"].ToString());
                    if (coords.Type == OSDType.Array)
                    {
                        Home.RegionHandle = (coords.Count == 2)
                            ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                    }
                }
            }
            else if (reply.ContainsKey("home"))
            {
                var osdHome = OSDParser.DeserializeLLSDNotation(reply["home"].AsString());

                if (osdHome.Type == OSDType.Map)
                {
                    var home = (OSDMap)osdHome;

                    OSD homeRegion;
                    if (home.TryGetValue("region_handle", out homeRegion) && homeRegion.Type == OSDType.Array)
                    {
                        var homeArray = (OSDArray)homeRegion;
                        Home.RegionHandle = homeArray.Count == 2
                            ? Utils.UIntsToLong((uint)homeArray[0].AsInteger(), (uint)homeArray[1].AsInteger())
                            : 0;
                    }

                    Home.Position = ParseVector3("position", home);
                    Home.LookAt = ParseVector3("look_at", home);
                }
            }
            else
            {
                Home.RegionHandle = 0;
                Home.Position = Vector3.Zero;
                Home.LookAt = Vector3.Zero;
            }

            CircuitCode = (int)ParseUInt("circuit_code", reply);
            RegionX = ParseUInt("region_x", reply);
            RegionY = ParseUInt("region_y", reply);
            SimPort = (ushort)ParseUInt("sim_port", reply);
            var simIP = ParseString("sim_ip", reply);
            IPAddress.TryParse(simIP, out SimIP);
            SeedCapability = ParseString("seed_capability", reply);

            // Buddy list
            OSD buddyLLSD;
            if (reply.TryGetValue("buddy-list", out buddyLLSD) && buddyLLSD.Type == OSDType.Array)
            {
                var buddys = new List<BuddyListEntry>();
                var buddyArray = (OSDArray)buddyLLSD;
                foreach (var t in buddyArray)
                {
                    if (t.Type == OSDType.Map)
                    {
                        var bud = new BuddyListEntry();
                        var buddy = (OSDMap)t;

                        bud.BuddyId = buddy["buddy_id"].AsString();
                        bud.BuddyRightsGiven = (int)ParseUInt("buddy_rights_given", buddy);
                        bud.BuddyRightsHas = (int)ParseUInt("buddy_rights_has", buddy);

                        buddys.Add(bud);
                    }
                    BuddyList = buddys.ToArray();
                }
            }

            SecondsSinceEpoch = (int)ParseUInt("seconds_since_epoch", reply);

            InventoryRoot = ParseMappedUUID("inventory-root", "folder_id", reply);
            InventorySkeleton = ParseInventorySkeleton("inventory-skeleton", reply);

            LibraryOwner = ParseMappedUUID("inventory-lib-owner", "agent_id", reply);
            LibraryRoot = ParseMappedUUID("inventory-lib-root", "folder_id", reply);
            LibrarySkeleton = ParseInventorySkeleton("inventory-skel-lib", reply);

            if (reply.ContainsKey("mfa_hash"))
            {
                MfaHash = ParseString("mfa_hash", reply);
            }

            if (reply.ContainsKey("account_level_benefits"))
            {
                if (reply["account_level_benefits"].Type == OSDType.Map)
                {
                    AccountLevelBenefits = ((OSDMap)reply["account_level_benefits"]).ToHashtable();
                }
            }

            if (reply.ContainsKey("classified_categories"))
            {
                if (reply["classified_categories"].Type == OSDType.Array)
                {
                    ClassifiedCategories = ((OSDArray)reply["classified_categories"]).ToArrayList();
                }
            }

            if (reply.ContainsKey("event_categories"))
            {
                if (reply["event_categories"].Type == OSDType.Array)
                {
                    EventCategories = ((OSDArray)reply["event_categories"]).ToArrayList();
                }
            }

            if (reply.ContainsKey("global-textures"))
            {
                if (reply["global-textures"].Type == OSDType.Array)
                {
                    GlobalTextures = ((OSDArray)reply["global-textures"]).ToArrayList();
                }
            }

            if (reply.ContainsKey("premium_packages"))
            {
                if (reply["premium_packages"].Type == OSDType.Map)
                {
                    PremiumPackages = ((OSDMap)reply["premium_packages"]).ToHashtable();
                }
            }

            if (reply.ContainsKey("ui-config"))
            {
                if (reply["ui-config"].Type == OSDType.Array)
                {
                    UiConfig = ((OSDArray)reply["ui-config"]).ToArrayList();
                }
            }

            if (reply.ContainsKey("max-agent-groups"))
            {
                MaxAgentGroups = (int)ParseUInt("max-agent-groups", reply);
            }
            else
            {
                MaxAgentGroups = -1;
            }
        }

        public void Parse(Hashtable reply)
        {
            try
            {
                AgentID = ParseUUID("agent_id", reply);
                SessionID = ParseUUID("session_id", reply);
                SecureSessionID = ParseUUID("secure_session_id", reply);
                FirstName = ParseString("first_name", reply).Trim('"');
                LastName = ParseString("last_name", reply).Trim('"');
                // "first_login" for brand new accounts
                StartLocation = ParseString("start_location", reply);
                AgentAccess = ParseString("agent_access", reply);
                LookAt = ParseVector3("look_at", reply);
                Reason = ParseString("reason", reply);
                Message = ParseString("message", reply);

                if (reply.ContainsKey("login"))
                {
                    Login = (string)reply["login"];
                    Success = Login == "true";

                    // Parse redirect options
                    if (Login == "indeterminate")
                    {
                        NextUrl = ParseString("next_url", reply);
                        NextDuration = (int)ParseUInt("next_duration", reply);
                        NextMethod = ParseString("next_method", reply);
                        NextOptions = (string[])((ArrayList)reply["next_options"]).ToArray(typeof(string));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log("Login server returned (some) invalid data: " + e.Message, Helpers.LogLevel.Warning);
            }
            if (!Success) { return; }

            // HomeInfo
            try
            {
                if (reply.ContainsKey("home_info"))
                {
                    if (reply?["home_info"] is Hashtable map)
                    {
                        Home.Position = ParseVector3("position", map);
                        Home.LookAt = ParseVector3("look_at", map);

                        var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(map["region_handle"].ToString());
                        if (coords.Type == OSDType.Array)
                        {
                            Home.RegionHandle = (coords.Count == 2)
                                ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                        }
                    }
                }

                // Home
                if (Home.RegionHandle == 0 && reply.ContainsKey("home"))
                {
                    if (reply?["home"] is Hashtable map)
                    {
                        Home.Position = ParseVector3("position", map);
                        Home.LookAt = ParseVector3("look_at", map);

                        var coords = (OSDArray)OSDParser.DeserializeLLSDNotation(map["region_handle"].ToString());
                        if (coords.Type == OSDType.Array)
                        {
                            Home.RegionHandle = (coords.Count == 2)
                                ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;
                        }
                    }
                    else if (reply?["home"] is string osdString)
                    {
                        var osdHome = OSDParser.DeserializeLLSDNotation(reply["home"].ToString());

                        if (osdHome.Type == OSDType.Map)
                        {
                            var home = (OSDMap)osdHome;

                            OSD homeRegion;
                            if (home.TryGetValue("region_handle", out homeRegion) && homeRegion.Type == OSDType.Array)
                            {
                                var coords = (OSDArray)homeRegion;
                                Home.RegionHandle = (coords.Count == 2)
                                    ? Utils.UIntsToLong((uint)coords[0].AsInteger(), (uint)coords[1].AsInteger()) : 0;

                            }
                            Home.Position = ParseVector3("position", home);
                            Home.LookAt = ParseVector3("look_at", home);
                        }
                    }
                    else
                    {
                        throw new Exception("Could not parse 'home' in Login Response");
                    }
                }
            } catch (Exception ex)
            {
                Logger.Log("Could not parse home info from login response. Setting nil", Helpers.LogLevel.Warning, ex);
                Home = new HomeInfo();
            }

            CircuitCode = (int)ParseUInt("circuit_code", reply);
            RegionX = ParseUInt("region_x", reply);
            RegionY = ParseUInt("region_y", reply);
            SimPort = (ushort)ParseUInt("sim_port", reply);
            var simIP = ParseString("sim_ip", reply);
            IPAddress.TryParse(simIP, out SimIP);
            SeedCapability = ParseString("seed_capability", reply);

            // Buddy list
            if (reply.ContainsKey("buddy-list") && reply["buddy-list"] is ArrayList)
            {
                var buddys = new List<BuddyListEntry>();

                var buddyArray = (ArrayList)reply["buddy-list"];
                foreach (var t in buddyArray)
                {
                    if (!(t is Hashtable buddy)) continue;

                    var bud = new BuddyListEntry
                    {
                        BuddyId = ParseString("buddy_id", buddy),
                        BuddyRightsGiven = (int)ParseUInt("buddy_rights_given", buddy),
                        BuddyRightsHas = (int)ParseUInt("buddy_rights_has", buddy)
                    };

                    buddys.Add(bud);
                }

                BuddyList = buddys.ToArray();
            }

            SecondsSinceEpoch = (int)ParseUInt("seconds_since_epoch", reply);

            InventoryRoot = ParseMappedUUID("inventory-root", "folder_id", reply);
            InventorySkeleton = ParseInventorySkeleton("inventory-skeleton", reply);

            LibraryOwner = ParseMappedUUID("inventory-lib-owner", "agent_id", reply);
            LibraryRoot = ParseMappedUUID("inventory-lib-root", "folder_id", reply);
            LibrarySkeleton = ParseInventorySkeleton("inventory-skel-lib", reply);
            
            AccountType = ParseString("account_type", reply);
            AgentAppearanceServiceURL = ParseString("agent_appearance_service", reply);
            COFVersion = ParseUInt("cof_version", reply);
            MapServerUrl = ParseString("map-server-url", reply);
            OpenIDUrl = ParseString("openid_url", reply);
            SnapshotConfigUrl = ParseString("snapshot_config_url", reply);
            UDPBlacklist = ParseString("udp_blacklist", reply);

            if (reply.ContainsKey("mfa_hash"))
            {
                MfaHash = ParseString("mfa_hash", reply);
            }

            if (reply.ContainsKey("account_level_benefits"))
            {
                if (reply?["account_level_benefits"] is Hashtable)
                {
                    AccountLevelBenefits = (Hashtable)reply["account_level_benefits"];
                }
            }

            if (reply.ContainsKey("classified_categories"))
            {
                if (reply?["classified_categories"] is ArrayList)
                {
                    ClassifiedCategories = (ArrayList)reply["classified_categories"];
                }
            }

            if (reply.ContainsKey("event_categories"))
            {
                if (reply?["event_categories"] is ArrayList)
                {
                    EventCategories = (ArrayList)reply["event_categories"];
                }
            }

            if (reply.ContainsKey("global-textures"))
            {
                if (reply?["global-textures"] is ArrayList)
                {
                    GlobalTextures = (ArrayList)reply["global-textures"];
                }
            }

            if (reply.ContainsKey("premium_packages"))
            {
                if (reply?["premium_packages"] is Hashtable)
                {
                    PremiumPackages = (Hashtable)reply["premium_packages"];
                }
            }

            if (reply.ContainsKey("ui-config"))
            {
                if (reply?["ui-config"] is ArrayList)
                {
                    UiConfig = (ArrayList)reply["ui-config"];
                }
            }

            if (reply.ContainsKey("max-agent-groups"))
            {
                MaxAgentGroups = (int)ParseUInt("max-agent-groups", reply);
            }
            else
            {
                MaxAgentGroups = -1;
            }


            InitialOutfit = string.Empty;
            if (reply.ContainsKey("initial-outfit") && reply["initial-outfit"] is ArrayList)
            {
                var array = (ArrayList)reply["initial-outfit"];
                foreach (var t in array)
                {
                    if (!(t is Hashtable map)) continue;

                    InitialOutfit = ParseString("folder_name", map);
                }
            }

            Gestures = new Dictionary<UUID, UUID>();
            if (reply.ContainsKey("gestures") && reply["gestures"] is ArrayList)
            {
                var gestureMaps = (ArrayList)reply["gestures"];
                foreach (var item in gestureMaps)
                {
                    if (!(item is Hashtable gestureMap) || !gestureMap.ContainsKey("item_id") || !gestureMap.ContainsKey("asset_id"))
                    {
                        continue;
                    }

                    UUID itemId;
                    if (!UUID.TryParse(gestureMap["item_id"].ToString(), out itemId))
                    {
                        continue;
                    }

                    UUID assetId;
                    if (!UUID.TryParse(gestureMap["asset_id"].ToString(), out assetId))
                    {
                        continue;
                    }

                    Gestures.Add(itemId, assetId);
                }
            }

            FirstLogin = false;
            if (reply.ContainsKey("login-flags") && reply["login-flags"] is ArrayList)
            {
                var array = (ArrayList)reply["login-flags"];
                foreach (var t in array)
                {
                    if (!(t is Hashtable map)) continue;

                    FirstLogin = ParseString("ever_logged_in", map) == "N";
                }
            }


        }

        #region Parsing Helpers

        public static uint ParseUInt(string key, OSDMap reply)
        {
            OSD osd;
            return reply.TryGetValue(key, out osd) ? osd.AsUInteger() : 0;
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
            OSD osd;
            return reply.TryGetValue(key, out osd) ? osd.AsUUID() : UUID.Zero;
        }

        public static UUID ParseUUID(string key, Hashtable reply)
        {
            if (!reply.ContainsKey(key)) return UUID.Zero;

            UUID value;
            return UUID.TryParse((string)reply[key], out value) ? value : UUID.Zero;
        }

        public static string ParseString(string key, OSDMap reply)
        {
            OSD osd;
            return reply.TryGetValue(key, out osd) ? osd.AsString() : string.Empty;
        }

        public static string ParseString(string key, Hashtable reply)
        {
            return reply.ContainsKey(key) ? $"{reply[key]}" : string.Empty;
        }

        public static Vector3 ParseVector3(string key, OSDMap reply)
        {
            OSD osd;
            if (!reply.TryGetValue(key, out osd)) return Vector3.Zero;

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
                    float x, y, z;
                    float.TryParse((string)list[0], out x);
                    float.TryParse((string)list[1], out y);
                    float.TryParse((string)list[2], out z);

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
            OSD folderOSD;
            if (!reply.TryGetValue(key, out folderOSD) || folderOSD.Type != OSDType.Array) {return UUID.Zero;}

            var array = (OSDArray)folderOSD;
            if (array.Count == 1 && array[0].Type == OSDType.Map)
            {
                var map = (OSDMap)array[0];
                OSD folder;
                if (map.TryGetValue(key2, out folder))
                    return folder.AsUUID();
            }

            return UUID.Zero;
        }

        public static UUID ParseMappedUUID(string key, string key2, Hashtable reply)
        {
            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList)) {return UUID.Zero;}

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

            OSD skeleton;
            if (!reply.TryGetValue(key, out skeleton) || skeleton.Type != OSDType.Array) {return folders.ToArray();}

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

            OSD skeleton;
            if (!reply.TryGetValue(key, out skeleton) || skeleton.Type != OSDType.Array) {return folders.ToArray();}
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

            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList)) {return folders.ToArray();}

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

    #endregion Structs

    /// <summary>
    /// Login Routines
    /// </summary>
    public partial class NetworkManager
    {
        #region Delegates


        //////LoginProgress
        //// LoginProgress
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<LoginProgressEventArgs> m_LoginProgress;

        ///<summary>Raises the LoginProgress Event</summary>
        /// <param name="e">A LoginProgressEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnLoginProgress(LoginProgressEventArgs e)
        {
            var handler = m_LoginProgress;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_LoginProgressLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// ...</summary>
        public event EventHandler<LoginProgressEventArgs> LoginProgress
        {
            add { lock (m_LoginProgressLock) { m_LoginProgress += value; } }
            remove { lock (m_LoginProgressLock) { m_LoginProgress -= value; } }
        }

        ///// <summary>The event subscribers, null if no subscribers</summary>
        //private EventHandler<LoggedInEventArgs> m_LoggedIn;

        /////<summary>Raises the LoggedIn Event</summary>
        ///// <param name="e">A LoggedInEventArgs object containing
        ///// the data sent from the simulator</param>
        //protected virtual void OnLoggedIn(LoggedInEventArgs e)
        //{
        //    EventHandler<LoggedInEventArgs> handler = m_LoggedIn;
        //    if (handler != null)
        //        handler(this, e);
        //}

        ///// <summary>Thread sync lock object</summary>
        //private readonly object m_LoggedInLock = new object();

        ///// <summary>Raised when the simulator sends us data containing
        ///// ...</summary>
        //public event EventHandler<LoggedInEventArgs> LoggedIn
        //{
        //    add { lock (m_LoggedInLock) { m_LoggedIn += value; } }
        //    remove { lock (m_LoggedInLock) { m_LoggedIn -= value; } }
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loginSuccess"></param>
        /// <param name="redirect"></param>
        /// <param name="replyData"></param>
        /// <param name="message"></param>
        /// <param name="reason"></param>
        public delegate void LoginResponseCallback(bool loginSuccess, bool redirect, string message, string reason, LoginResponseData replyData);

        #endregion Delegates

        #region Events

        /// <summary>Called when a reply is received from the login server, the
        /// login sequence will block until this event returns</summary>
        private event LoginResponseCallback OnLoginResponse;

        #endregion Events

        #region Public Members
        /// <summary>Seed CAPS URI returned from the login server</summary>
        public Uri LoginSeedCapability { get; private set; } = null;
        /// <summary>Current state of logging in</summary>
        public LoginStatus LoginStatusCode { get; private set; } = LoginStatus.None;

        /// <summary>Upon login failure, contains a short string key for the
        /// type of login error that occurred</summary>
        public string LoginErrorKey { get; private set; } = string.Empty;

        /// <summary>The raw XML-RPC reply from the login server, exactly as it
        /// was received (minus the HTTP header)</summary>
        public string RawLoginReply { get; } = string.Empty;

        /// <summary>During login this contains a descriptive version of 
        /// LoginStatusCode. After a successful login this will contain the 
        /// message of the day, and after a failed login a descriptive error 
        /// message will be returned</summary>
        public string LoginMessage { get; private set; } = string.Empty;

        /// <summary>Maximum number of groups an agent can belong to, -1 for unlimited</summary>
        public int MaxAgentGroups = -1;
        /// <summary>Server side baking service URL</summary>
        public string AgentAppearanceServiceURL;
        /// <summary>Parsed login response data</summary>
        public LoginResponseData LoginResponseData;
        #endregion

        #region Private Members

        public static readonly HttpClient HTTP_CLIENT = new HttpClient(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = delegate { return true; },
            AllowAutoRedirect = true
        });
        
        private LoginParams CurrentContext = null;
        private readonly AutoResetEvent LoginEvent = new AutoResetEvent(false);
        private readonly Dictionary<LoginResponseCallback, string[]> CallbackOptions = new Dictionary<LoginResponseCallback, string[]>();

        /// <summary>A list of packets obtained during the login process which 
        /// networkmanager will log but not process</summary>
        private readonly List<string> UDPBlacklist = new List<string>();
        #endregion

        #region Public Methods

        /// <summary>
        /// Generate sane default values for a login request
        /// </summary>
        /// <param name="firstName">Account first name</param>
        /// <param name="lastName">Account last name</param>
        /// <param name="password">Account password</param>
        /// <param name="channel">Client application name (channel)</param>
        /// <param name="version">Client application name + version</param>
        /// <returns>A populated <seealso cref="LoginParams"/> object containing sane defaults</returns>
        public LoginParams DefaultLoginParams(string firstName, string lastName, string password,
            string channel, string version)
        {
            return new LoginParams(Client, new LoginCredential(firstName, lastName, password), channel, version);
        }

        /// <summary>
        /// Simplified login that takes the most common and required fields
        /// </summary>
        /// <param name="firstName">Account first name</param>
        /// <param name="lastName">Account last name</param>
        /// <param name="password">Account password</param>
        /// <param name="channel">Client application name (channel)</param>
        /// <param name="version">Client application name + version</param>
        /// <returns>Whether the login was successful or not. On failure the
        /// LoginErrorKey string will contain the error code and LoginMessage
        /// will contain a description of the error</returns>
        public bool Login(string firstName, string lastName, string password, string channel, string version)
        {
            return Login(firstName, lastName, password, channel, "last", version);
        }

        /// <summary>
        /// Login that works via a existing login response
        /// </summary>
        /// <returns>Whether we are able to connect to a simulator using this data</returns>
        public bool Login(LoginResponseData response)
        {
            LoginResponseData = response;

            Client.Network.CircuitCode = (uint)response.CircuitCode;

            LoginSeedCapability = new Uri(response.SeedCapability);

            var handle = Utils.UIntsToLong(response.RegionX, response.RegionY);

            if (Connect(response.SimIP, response.SimPort, handle, true, LoginSeedCapability) != null)
            {
                SendPacket(new EconomyDataRequestPacket());

                UpdateLoginStatus(LoginStatus.Success, "Login success");
                return true;
            }
            else
            {
                UpdateLoginStatus(LoginStatus.Failed, "Unable to connect to simulator");
                return false;
            }
        }

        /// <summary>
        /// Login that works via a SeedCap to allow logins to occur on another host with the details passed in here.
        /// </summary>
        /// <returns>Whether we are able to connect to a simulator using this data</returns>
        public bool Login(string fullLLSD, string seedcap, string username, UUID agentID, UUID sessionID,
                          UUID secureSessionID, string host, uint port, int circuitCode, uint regionX, uint regionY)
        {
            if (string.IsNullOrEmpty(fullLLSD))
            {
                LoginResponseData = new LoginResponseData
                {
                    AgentID = agentID, SessionID = sessionID, SecureSessionID = secureSessionID,
                    CircuitCode = circuitCode,
                    RegionX = regionX, RegionY = regionY, SeedCapability = seedcap, SimIP = IPAddress.Parse(host),
                    SimPort = (ushort)port, Success = true
                };
            }
            else
            {
                LoginResponseData = new LoginResponseData();
                LoginResponseData.Parse(OSDParser.DeserializeLLSDXml(fullLLSD) as OSDMap);
            }

            // Login succeeded

            // Fire the login callback
            if (OnLoginResponse != null)
            {
                try { OnLoginResponse(LoginResponseData.Success, false, "Login Message", LoginResponseData.Reason, LoginResponseData); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
            }

            // These parameters are stored in NetworkManager, so instead of registering
            // another callback for them we just set the values here
            Client.Network.CircuitCode = (uint)circuitCode;
            LoginSeedCapability = new Uri(seedcap);
            
            UpdateLoginStatus(LoginStatus.ConnectingToSim, "Connecting to simulator...");

            var handle = Utils.UIntsToLong(regionX, regionY);

            if (LoginResponseData.SimIP != null && LoginResponseData.SimPort != 0)
            {
                // Connect to the sim given in the login reply
                if (Connect(LoginResponseData.SimIP, LoginResponseData.SimPort, handle, true, LoginSeedCapability) != null)
                {
                    // Request the economy data right after login
                    SendPacket(new EconomyDataRequestPacket());

                    // Update the login message with the MOTD returned from the server
                    UpdateLoginStatus(LoginStatus.Success, "Login Success");
                    return true;
                }
                else
                {
                    UpdateLoginStatus(LoginStatus.Failed,
                                      "Unable to establish a UDP connection to the simulator");
                    return false;
                }
            }
            else
            {
                UpdateLoginStatus(LoginStatus.Failed,
                                  "Login server did not return a simulator address");
                return false;
            }
        }

        /// <summary>
        /// Simplified login that takes the most common fields along with a
        /// starting location URI, and can accept an MD5 string instead of a
        /// plaintext password
        /// </summary>
        /// <param name="firstName">Account first name</param>
        /// <param name="lastName">Account last name</param>
        /// <param name="password">Account password or MD5 hash of the password
        /// such as $1$1682a1e45e9f957dcdf0bb56eb43319c</param>
        /// <param name="channel">Client application name (channel)</param>
        /// <param name="start">Starting location URI that can be built with
        /// StartLocation()</param>
        /// <param name="version">Client application name + version</param>
        /// <returns>Whether the login was successful or not. On failure the
        /// LoginErrorKey string will contain the error code and LoginMessage
        /// will contain a description of the error</returns>
        public bool Login(string firstName, string lastName, string password, string channel, string start,
            string version)
        {
            var loginParams = DefaultLoginParams(firstName, lastName, password, channel, version);
            loginParams.Start = start;

            return Login(loginParams);
        }

        /// <summary>
        /// Login that takes a struct of all the values that will be passed to
        /// the login server
        /// </summary>
        /// <param name="loginParams">The values that will be passed to the login
        /// server, all fields must be set even if they are String.Empty</param>
        /// <returns>Whether the login was successful or not. On failure the
        /// LoginErrorKey string will contain the error code and LoginMessage
        /// will contain a description of the error</returns>
        public bool Login(LoginParams loginParams)
        {
            BeginLogin(loginParams);

            LoginEvent.WaitOne(loginParams.Timeout, false);

            if (CurrentContext != null)
            {
                CurrentContext = null; // Will force any pending callbacks to bail out early
                LoginStatusCode = LoginStatus.Failed;
                LoginMessage = "Timed out";
                return false;
            }

            return (LoginStatusCode == LoginStatus.Success);
        }

        public void BeginLogin(LoginParams loginParams)
        {
            // FIXME: Now that we're using CAPS we could cancel the current login and start a new one
            if (CurrentContext != null) {throw new Exception("Login already in progress");}

            LoginEvent.Reset();
            CurrentContext = loginParams;

            BeginLogin();
        }

        public void RegisterLoginResponseCallback(LoginResponseCallback callback)
        {
            RegisterLoginResponseCallback(callback, null);
        }


        public void RegisterLoginResponseCallback(LoginResponseCallback callback, string[] options)
        {
            CallbackOptions.Add(callback, options);
            OnLoginResponse += callback;
        }

        public void UnregisterLoginResponseCallback(LoginResponseCallback callback)
        {
            CallbackOptions.Remove(callback);
            OnLoginResponse -= callback;
        }

        /// <summary>
        /// Build a start location URI for passing to the Login function
        /// </summary>
        /// <param name="sim">Name of the simulator to start in</param>
        /// <param name="x">X coordinate to start at</param>
        /// <param name="y">Y coordinate to start at</param>
        /// <param name="z">Z coordinate to start at</param>
        /// <returns>String with a URI that can be used to login to a specified location</returns>
        public static string StartLocation(string sim, int x, int y, int z)
        {
            return $"uri:{sim}&{x}&{y}&{z}";
        }
        public void AbortLogin()
        {
            var loginParams = CurrentContext;
            CurrentContext = null; // Will force any pending callbacks to bail out early
            // FIXME: Now that we're using CAPS we could cancel the current login and start a new one
            if (loginParams == null)
            {
                Logger.DebugLog($"No Login was in progress: {CurrentContext}", Client);
            }
            else
            {
                LoginStatusCode = LoginStatus.Failed;
                LoginMessage = "Aborted";
            }
            UpdateLoginStatus(LoginStatus.Failed, "Abort Requested");
        }

        #endregion

        #region Private Methods

        private void BeginLogin()
        {
            var loginParams = CurrentContext;
            // Generate a random ID to identify this login attempt
            loginParams.LoginID = UUID.Random();
            CurrentContext = loginParams;

            #region Sanity Check loginParams

            if (loginParams.Options == null)
                loginParams.Options = new List<string>();

            if (loginParams.Password == null)
                loginParams.Password = string.Empty;

            // *HACK: Convert the password to MD5 if it isn't already
            if (loginParams.Password.Length != 35 && !loginParams.Password.StartsWith("$1$"))
                loginParams.Password = Utils.MD5(loginParams.Password);

            if (loginParams.ViewerDigest == null)
                loginParams.ViewerDigest = string.Empty;

            if (loginParams.Version == null)
                loginParams.Version = string.Empty;

            if (loginParams.UserAgent == null)
                loginParams.UserAgent = Settings.USER_AGENT;

            if (loginParams.Platform == null)
                loginParams.Platform = string.Empty;

            if (loginParams.PlatformVersion == null)
                loginParams.PlatformVersion = string.Empty;

            if (loginParams.MAC == null)
                loginParams.MAC = string.Empty;

            if (string.IsNullOrEmpty(loginParams.Channel))
            {
                Logger.Log("Viewer channel not set.", 
                    Helpers.LogLevel.Warning);
                loginParams.Channel = $"{Settings.USER_AGENT}";
            }

            if (!string.IsNullOrEmpty(loginParams.LoginLocation))
            {
                var startLoc = new LocationParser(loginParams.LoginLocation.Trim());
                loginParams.Start = startLoc.GetStartLocationUri();
            } 
            else
            {
                switch (loginParams.Start)
                {
                    case "home":
                    case "last":
                        break;
                    default:
                        var startLoc = new LocationParser(loginParams.Start.Trim());
                        loginParams.Start = startLoc.GetStartLocationUri();
                        break;
                }
            }

            if (loginParams.Author == null)
            {
                loginParams.Author = string.Empty;
            }
            #endregion

            // TODO: Allow a user callback to be defined for handling the cert
            ServicePointManager.ServerCertificateValidationCallback = 
                (sender, certificate, chain, sslPolicyErrors) => true;

            if (Client.Settings.USE_LLSD_LOGIN)
            {
                #region LLSD Based Login

                // Create the CAPS login structure
                var loginLLSD = new OSDMap
                {
                    ["first"] = OSD.FromString(loginParams.FirstName),
                    ["last"] = OSD.FromString(loginParams.LastName),
                    ["passwd"] = OSD.FromString(loginParams.Password),
                    ["start"] = OSD.FromString(loginParams.Start),
                    ["channel"] = OSD.FromString(loginParams.Channel),
                    ["version"] = OSD.FromString(loginParams.Version),
                    ["platform"] = OSD.FromString(loginParams.Platform),
                    ["platform_version"] = OSD.FromString(loginParams.PlatformVersion),
                    ["mac"] = OSD.FromString(loginParams.MAC),
                    ["agree_to_tos"] = OSD.FromBoolean(loginParams.AgreeToTos),
                    ["read_critical"] = OSD.FromBoolean(loginParams.ReadCritical),
                    ["viewer_digest"] = OSD.FromString(loginParams.ViewerDigest),
                    ["id0"] = OSD.FromString(loginParams.ID0),
                    ["last_exec_event"] = OSD.FromInteger((int)loginParams.LastExecEvent)
                };
                if (loginParams.MfaEnabled)
                {
                    loginLLSD["token"] = OSD.FromString(loginParams.Token);
                    loginLLSD["mfa_hash"] = OSD.FromString(loginParams.MfaHash);
                }

                // Create the options LLSD array
                var optionsOSD = new OSDArray();
                foreach (var option in loginParams.Options)
                {
                    optionsOSD.Add(OSD.FromString(option));
                }
                foreach (var t in from callbackOpts in CallbackOptions.Values where callbackOpts != null
                    from t in callbackOpts where !optionsOSD.Contains(t) select t)
                {
                    optionsOSD.Add(t);
                }
                loginLLSD["options"] = optionsOSD;

                // Make the CAPS POST for login
                Uri loginUri;
                try
                {
                    loginUri = new Uri(loginParams.URI);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to parse login URI {loginParams.URI}, {ex.Message}",
                        Helpers.LogLevel.Error, Client);
                    return;
                }

                UpdateLoginStatus(LoginStatus.ConnectingToLogin,
                    $"Logging in as {loginParams.FirstName} {loginParams.LastName}...");
                Task loginReq = Client.HttpCapsClient.PostRequestAsync(loginUri, OSDFormat.Xml, loginLLSD, 
                    CancellationToken.None, LoginReplyLLSDHandler);

                #endregion
            }
            else
            {
                #region XML-RPC Based Login Code

                // Create the Hashtable for XmlRpcCs
                var loginXmlRpc = new Hashtable
                {
                    ["first"] = loginParams.FirstName,
                    ["last"] = loginParams.LastName,
                    ["passwd"] = loginParams.Password,
                    ["start"] = loginParams.Start,
                    ["channel"] = loginParams.Channel,
                    ["version"] = loginParams.Version,
                    ["platform"] = loginParams.Platform,
                    ["platform_version"] = loginParams.PlatformVersion,
                    ["mac"] = loginParams.MAC,
                    ["id0"] = loginParams.ID0,
                    ["last_exec_event"] = (int)loginParams.LastExecEvent
                };
                if (loginParams.AgreeToTos) { loginXmlRpc["agree_to_tos"] = "true"; }
                if (loginParams.ReadCritical) { loginXmlRpc["read_critical"] = "true"; }
                if (loginParams.MfaEnabled)
                {
                    loginXmlRpc["token"] = loginParams.Token;
                    loginXmlRpc["mfa_hash"] = loginParams.MfaHash;
                }

                // Create the options array
                var options = new ArrayList();
                foreach (var option in loginParams.Options)
                {
                    options.Add(option);
                }
                foreach (var callbackOpts in CallbackOptions.Values)
                {
                    if (callbackOpts == null) continue;
                    foreach (var t in callbackOpts)
                    {
                        if (!options.Contains(t))
                            options.Add(t);
                    }
                }
                loginXmlRpc["options"] = options;

                try
                {
                    var loginArray = new ArrayList(1) { loginXmlRpc };
                    var request = new XmlRpcRequest(CurrentContext.MethodName, loginArray);
                    var cc = CurrentContext;
                    // Start the request
                    Task.Run(async () =>
                    {
                        try
                        {
                            var cts = new CancellationTokenSource();
                            cts.CancelAfter(cc.Timeout);
                            var loginResponse = await HTTP_CLIENT.PostAsXmlRpcAsync(cc.URI, request, cts.Token);
                            cts.Dispose();
                            
                            LoginReplyXmlRpcHandler(loginResponse, loginParams);
                        }
                        catch (Exception e)
                        {
                            UpdateLoginStatus(LoginStatus.Failed,
                                $"Error opening the login server connection: {e.Message}");
                        }
                    });
                }
                catch (Exception e)
                {
                    UpdateLoginStatus(LoginStatus.Failed, $"Error opening the login server connection: {e}");
                }

                #endregion
            }
        }

        private void UpdateLoginStatus(LoginStatus status, string message)
        {
            LoginStatusCode = status;
            LoginMessage = message;

            Logger.DebugLog($"Login status: {status}: {message}", Client);

            // If we reached a login resolution trigger the event
            if (status == LoginStatus.Success || status == LoginStatus.Failed)
            {
                CurrentContext = null;
                LoginEvent.Set();
            }

            // Fire the login status callback
            if (m_LoginProgress != null)
            {
                OnLoginProgress(new LoginProgressEventArgs(status, message, LoginErrorKey));
            }
        }


        /// <summary>
        /// LoginParams and the initial login XmlRpcRequest were made on a remote machine.
        /// This method now initializes libomv with the results.
        /// </summary>
        public void RemoteLoginHandler(LoginResponseData response, LoginParams newContext)
        {
            CurrentContext = newContext;
            LoginReplyXmlRpcHandler(response, newContext);
        }


        /// <summary>
        /// Handles response from XML-RPC login replies
        /// </summary>
        private void LoginReplyXmlRpcHandler(XmlRpcResponse response, LoginParams context)
        {
            var reply = new LoginResponseData();
            // Fetch the login response
            if (!(response?.Value is Hashtable value))
            {
                UpdateLoginStatus(LoginStatus.Failed, "Invalid or missing login response from the server");
                Logger.Log("Invalid or missing login response from the server", Helpers.LogLevel.Warning);
                return;
            }

            try
            {
                reply.Parse(value);
                if (context.LoginID != CurrentContext.LoginID)
                {
                    Logger.Log("Login response does not match login request. " +
                               "Only one login can be attempted at a time",
                        Helpers.LogLevel.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                UpdateLoginStatus(LoginStatus.Failed, $"Error retrieving the login response from the server: {ex.Message}");
                Logger.Log($"Login response failure: {ex.Message} ", Helpers.LogLevel.Warning, ex);
                return;
            }
            LoginReplyXmlRpcHandler(reply, context);
        }


        /// <summary>
        /// Handles response from XML-RPC login replies with already parsed LoginResponseData
        /// </summary>
        private void LoginReplyXmlRpcHandler(LoginResponseData reply, LoginParams context)
        {
            LoginResponseData = reply;
            ushort simPort = 0;
            uint regionX = 0;
            uint regionY = 0;
            var reason = reply.Reason;
            var message = reply.Message;

            if (reply.Login == "true")
            {
                // Remove the quotes around our first name.
                if (reply.FirstName[0] == '"')
                    reply.FirstName = reply.FirstName.Remove(0, 1);
                if (reply.FirstName[reply.FirstName.Length - 1] == '"')
                    reply.FirstName = reply.FirstName.Remove(reply.FirstName.Length - 1);

                #region Critical Information

                try
                {
                    // Networking
                    Client.Network.CircuitCode = (uint)reply.CircuitCode;
                    regionX = reply.RegionX;
                    regionY = reply.RegionY;
                    simPort = reply.SimPort;
                    LoginSeedCapability = new Uri(reply.SeedCapability);
                }
                catch (Exception)
                {
                    UpdateLoginStatus(LoginStatus.Failed, "Login server failed to return critical information");
                    return;
                }

                #endregion Critical Information

                /* Add any blacklisted UDP packets to the blacklist
                 * for exclusion from packet processing */
                if (reply.UDPBlacklist != null)
                {
                    UDPBlacklist.AddRange(reply.UDPBlacklist.Split(','));
                }

                // Misc:
                MaxAgentGroups = reply.MaxAgentGroups;
                AgentAppearanceServiceURL = reply.AgentAppearanceServiceURL;

                //uint timestamp = (uint)reply.seconds_since_epoch;
                //DateTime time = Helpers.UnixTimeToDateTime(timestamp); // TODO: Do something with this?
            }

            var redirect = (reply.Login == "indeterminate");

            try
            {
                if (OnLoginResponse != null)
                {
                    try { OnLoginResponse(reply.Success, redirect, message, reason, reply); }
                    catch (Exception ex) { Logger.Log(ex.ToString(), Helpers.LogLevel.Error); }
                }
            }
            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, ex); }

            // Make the next network jump, if needed
            if (redirect)
            {
                UpdateLoginStatus(LoginStatus.Redirecting, "Redirecting login...");
                var loginParams = CurrentContext;
                loginParams.URI = reply.NextUrl;
                loginParams.MethodName = reply.NextMethod;
                loginParams.Options = reply.NextOptions.ToList();

                // Sleep for some amount of time while the servers work
                var seconds = reply.NextDuration;
                Logger.Log($"Sleeping for {seconds} seconds during a login redirect",
                    Helpers.LogLevel.Info);
                Thread.Sleep(seconds * 1000);

                CurrentContext = loginParams;
                BeginLogin();
            }
            else if (reply.Success)
            {
                UpdateLoginStatus(LoginStatus.ConnectingToSim, "Connecting to simulator...");

                var handle = Utils.UIntsToLong(regionX, regionY);

                // Connect to the sim given in the login reply
                if (Connect(reply.SimIP, simPort, handle, true, LoginSeedCapability) != null)
                {
                    // Request the economy data right after login
                    SendPacket(new EconomyDataRequestPacket());

                    // Update the login message with the MOTD returned from the server
                    UpdateLoginStatus(LoginStatus.Success, message);
                }
                else
                {
                    UpdateLoginStatus(LoginStatus.Failed, "Unable to connect to simulator");
                }
            }
            else
            {
                // Make sure a usable error key is set

                LoginErrorKey = !string.IsNullOrEmpty(reason) ? reason : "unknown";

                UpdateLoginStatus(LoginStatus.Failed, message);
            }
        }

        /// <summary>
        /// Handle response from LLSD login replies
        /// </summary>
        /// <param name="response">Server response as <seealso cref="HttpResponseMessage"/></param>
        /// <param name="responseData">Payload response data</param>
        /// <param name="error">Any <seealso cref="Exception"/> returned from the request</param>
        private void LoginReplyLLSDHandler(HttpResponseMessage response, byte[] responseData, Exception error)
        {
            if (error != null)
            {
                // Connection error
                LoginErrorKey = "no connection";
                UpdateLoginStatus(LoginStatus.Failed, error.Message);
                return;
            }

            OSD result = OSDParser.Deserialize(responseData);
            if (result is OSDMap resMap)
            {
                var data = new LoginResponseData();
                data.Parse(resMap);

                if (resMap.TryGetValue("login", out OSD osd))
                {
                    var loginSuccess = osd.AsBoolean();
                    var redirect = (osd.AsString() == "indeterminate");

                    if (redirect)
                    {
                        // Login redirected

                        // Make the next login URL jump
                        UpdateLoginStatus(LoginStatus.Redirecting, data.Message);

                        var loginParams = CurrentContext;
                        loginParams.URI = LoginResponseData.ParseString("next_url", resMap);

                        // Sleep for some amount of time while the servers work
                        var seconds = (int)LoginResponseData.ParseUInt("next_duration", resMap);
                        Logger.Log($"Sleeping for {seconds} seconds during a login redirect",
                            Helpers.LogLevel.Info);
                        Thread.Sleep(seconds * 1000);

                        // Ignore next_options for now
                        CurrentContext = loginParams;

                        BeginLogin();
                    }
                    else if (loginSuccess)
                    {
                        // Login succeeded

                        // Fire the login callback
                        if (OnLoginResponse != null)
                        {
                            try { OnLoginResponse(loginSuccess, redirect, data.Message, data.Reason, data); }
                            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }
                        }

                        // These parameters are stored in NetworkManager, so instead of registering
                        // another callback for them we just set the values here
                        CircuitCode = (uint)data.CircuitCode;
                        LoginSeedCapability = new Uri(data.SeedCapability);

                        UpdateLoginStatus(LoginStatus.ConnectingToSim, "Connecting to simulator...");

                        var handle = Utils.UIntsToLong(data.RegionX, data.RegionY);

                        if (data.SimIP != null && data.SimPort != 0)
                        {
                            // Connect to the sim given in the login reply
                            if (Connect(data.SimIP, data.SimPort, handle, true, LoginSeedCapability) != null)
                            {
                                // Request the economy data right after login
                                SendPacket(new EconomyDataRequestPacket());

                                // Update the login message with the MOTD returned from the server
                                UpdateLoginStatus(LoginStatus.Success, data.Message);
                            }
                            else
                            {
                                UpdateLoginStatus(LoginStatus.Failed,
                                    "Unable to establish a UDP connection to the simulator");
                            }
                        }
                        else
                        {
                            UpdateLoginStatus(LoginStatus.Failed,
                                "Login server did not return a simulator address");
                        }
                    }
                    else
                    {
                        // Login failed

                        // Make sure a usable error key is set
                        LoginErrorKey = data.Reason != string.Empty ? data.Reason : "unknown";

                        UpdateLoginStatus(LoginStatus.Failed, data.Message);
                    }
                }
                else
                {
                    // Got an LLSD map but no login value
                    UpdateLoginStatus(LoginStatus.Failed, "Login parameter missing in the response");
                }
            }
            else
            {
                // No LLSD response
                LoginErrorKey = "bad response";
                UpdateLoginStatus(LoginStatus.Failed, "Empty or corrupt login response");
            }
        }

        /// <summary>
        /// Get current OS
        /// </summary>
        /// <returns>Either "Win" or "Linux"</returns>
        public static string GetPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    return "mac";
                case PlatformID.Unix:
                    return "lnx";
                default:
                    return "win";
            }
        }

        /// <summary>
        /// Gets the current OS version number
        /// </summary>
        /// <returns>The platform version.</returns>
        public static string GetPlatformVersion()
        {
            return Environment.OSVersion.Version.ToString();
        }

        /// <summary>
        /// Get clients default Mac Address
        /// </summary>
        /// <returns>A string containing the first found Mac Address</returns>
        public static string GetMAC()
        {
            var mac = string.Empty;

            try
            {
                var nics =
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                if (nics.Length > 0)
                {
                    foreach (var t in nics)
                    {
                        var adapterMac = t.GetPhysicalAddress().ToString().ToUpper();
                        if (adapterMac.Length == 12 && adapterMac != "000000000000")
                        {
                            mac = adapterMac;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (mac.Length < 12)
                mac = UUID.Random().ToString().Substring(24, 12);

            return string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                mac.Substring(0, 2),
                mac.Substring(2, 2),
                mac.Substring(4, 2),
                mac.Substring(6, 2),
                mac.Substring(8, 2),
                mac.Substring(10, 2));
        }

        /// <summary>
        /// MD5 hash of string
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>Hashed string</returns>
        private static string HashString(string str)
        {
            MD5 sec = MD5.Create();
            var enc = new ASCIIEncoding();
            var buf = enc.GetBytes(str);
            return GetHexString(sec.ComputeHash(buf));
        }

        private static string GetHexString(byte[] buf)
        {
            var str = string.Empty;

            foreach (var b in buf)
            {
                var n = (int)b;
                var n1 = n & 15;
                var n2 = (n >> 4) & 15;
                if (n2 > 9)
                    str += ((char)(n2 - 10 + 'A')).ToString();
                else
                    str += ((char)(n1 - 10 + 'A')).ToString();
                if (n1 > 9)
                    str += ((char)(n1 - 10 + 'A')).ToString();
                else
                    str += n1.ToString();
            }

            return str;
        }

        public static string GetHashedMAC()
        {
            return HashString(GetMAC());
        }

        #endregion
    }
    #region EventArgs

    public class LoginProgressEventArgs : EventArgs
    {
        public LoginStatus Status { get; }

        public string Message { get; }

        public string FailReason { get; }

        public LoginProgressEventArgs(LoginStatus login, string message, string failReason)
        {
            Status = login;
            Message = message;
            FailReason = failReason;
        }
    }

    #endregion EventArgs
}
