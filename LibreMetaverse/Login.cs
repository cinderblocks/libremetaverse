/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019, Cinderblocks Design Co.
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
using System.Net.NetworkInformation;
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
        /// <summary>The agents starting location once logged in</summary>
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
        public string[] Options;

        /// <summary>A randomly generated ID to distinguish between login attempts. This value is only used
        /// internally in the library and is never sent over the wire</summary>
        internal UUID LoginID;

        /// <summary>
        /// Default constructor, initializes sane default values
        /// </summary>
        public LoginParams()
        {
            var options = new List<string>(16)
            {
                "inventory-root",
                "inventory-skeleton",
                "inventory-lib-root",
                "inventory-lib-owner",
                "inventory-skel-lib",
                "initial-outfit",
                "gestures",
                "event_categories",
                "event_notifications",
                "classified_categories",
                "buddy-list",
                "ui-config",
                "tutorial_settings",
                "login-flags",
                "global-textures",
                "adult_compliant"
            };

            Options = options.ToArray();
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
        /// Instantiates new LoginParams object and fills in the values
        /// </summary>
        /// <param name="client">Instance of GridClient to read settings from</param>
        /// <param name="firstName">Login first name</param>
        /// <param name="lastName">Login last name</param>
        /// <param name="password">Password</param>
        /// <param name="channel">Login channel (application name)</param>
        /// <param name="version">Client version, should be application name + version number</param>
        public LoginParams(GridClient client, string firstName, string lastName, string password, string channel, string version)
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
        /// <param name="channel">Login channnel (application name)</param>
        /// <param name="version">Client version, should be application name + version number</param>
        /// <param name="loginURI">URI of the login server</param>
        public LoginParams(GridClient client, string firstName, string lastName, string password, string channel, string version, string loginURI)
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
        public UUID AgentID;
        public UUID SessionID;
        public UUID SecureSessionID;
        public string FirstName;
        public string LastName;
        public string StartLocation;
        /// <summary>M or PG, also agent_region_access and agent_access_max</summary>
        public string AgentAccess;
        public Vector3 LookAt;
        public ulong HomeRegion;
        public Vector3 HomePosition;
        public Vector3 HomeLookAt;
        public int CircuitCode;
        public uint RegionX;
        public uint RegionY;
        public ushort SimPort;
        public IPAddress SimIP;
        public string SeedCapability;
        public BuddyListEntry[] BuddyList;
        public int SecondsSinceEpoch;
        public string UDPBlacklist;

        #region Inventory

        public UUID InventoryRoot;
        public UUID LibraryRoot;
        public InventoryFolder[] InventorySkeleton;
        public InventoryFolder[] LibrarySkeleton;
        public UUID LibraryOwner;

        #endregion

        #region Redirection

        public string NextMethod;
        public string NextUrl;
        public string[] NextOptions;
        public int NextDuration;

        #endregion

        // These aren't currently being utilized by the library
        public string AgentAccessMax;
        public string AgentRegionAccess;
        public int AOTransition;
        public string InventoryHost;
        public int MaxAgentGroups;
        public string OpenIDUrl;
        public string AgentAppearanceServiceURL;
        public uint COFVersion;
        public string InitialOutfit;
        public bool FirstLogin;
        public Dictionary<UUID, UUID> Gestures;

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
                FirstName = ParseString("first_name", reply).Trim('"');
                LastName = ParseString("last_name", reply).Trim('"');
                StartLocation = ParseString("start_location", reply);
                AgentAccess = ParseString("agent_access", reply);
                LookAt = ParseVector3("look_at", reply);
                Reason = ParseString("reason", reply);
                Message = ParseString("message", reply);

                Login = reply["login"].AsString();
                Success = reply["login"].AsBoolean();
            }
            catch (OSDException e)
            {
                Logger.Log("Login server returned (some) invalid data: " + e.Message, Helpers.LogLevel.Warning);
            }

            // Home
            OSD osdHome = OSDParser.DeserializeLLSDNotation(reply["home"].AsString());

            if (osdHome.Type == OSDType.Map)
            {
                var home = (OSDMap)osdHome;

                OSD homeRegion;
                if (home.TryGetValue("region_handle", out homeRegion) && homeRegion.Type == OSDType.Array)
                {
                    OSDArray homeArray = (OSDArray)homeRegion;
                    HomeRegion = homeArray.Count == 2
                        ? Utils.UIntsToLong((uint)homeArray[0].AsInteger(), (uint)homeArray[1].AsInteger())
                        : 0;
                }

                HomePosition = ParseVector3("position", home);
                HomeLookAt = ParseVector3("look_at", home);
            }
            else
            {
                HomeRegion = 0;
                HomePosition = Vector3.Zero;
                HomeLookAt = Vector3.Zero;
            }

            CircuitCode = (int)ParseUInt("circuit_code", reply);
            RegionX = ParseUInt("region_x", reply);
            RegionY = ParseUInt("region_y", reply);
            SimPort = (ushort)ParseUInt("sim_port", reply);
            string simIP = ParseString("sim_ip", reply);
            IPAddress.TryParse(simIP, out SimIP);
            SeedCapability = ParseString("seed_capability", reply);

            // Buddy list
            OSD buddyLLSD;
            if (reply.TryGetValue("buddy-list", out buddyLLSD) && buddyLLSD.Type == OSDType.Array)
            {
                List<BuddyListEntry> buddys = new List<BuddyListEntry>();
                OSDArray buddyArray = (OSDArray)buddyLLSD;
                foreach (OSD t in buddyArray)
                {
                    if (t.Type == OSDType.Map)
                    {
                        BuddyListEntry bud = new BuddyListEntry();
                        OSDMap buddy = (OSDMap)t;

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
            if (!Success)
                return;

            // Home
            if (reply.ContainsKey("home"))
            {
                OSD osdHome = OSDParser.DeserializeLLSDNotation(reply["home"].ToString());

                if (osdHome.Type == OSDType.Map)
                {
                    var home = (OSDMap)osdHome;

                    OSD homeRegion;
                    if (home.TryGetValue("region_handle", out homeRegion) && homeRegion.Type == OSDType.Array)
                    {
                        OSDArray homeArray = (OSDArray)homeRegion;
                        if (homeArray.Count == 2)
                            HomeRegion = Utils.UIntsToLong((uint)homeArray[0].AsInteger(),
                                                           (uint)homeArray[1].AsInteger());
                        else
                            HomeRegion = 0;
                    }

                    HomePosition = ParseVector3("position", home);
                    HomeLookAt = ParseVector3("look_at", home);
                }
            }
            else
            {
                HomeRegion = 0;
                HomePosition = Vector3.Zero;
                HomeLookAt = Vector3.Zero;
            }

            CircuitCode = (int)ParseUInt("circuit_code", reply);
            RegionX = ParseUInt("region_x", reply);
            RegionY = ParseUInt("region_y", reply);
            SimPort = (ushort)ParseUInt("sim_port", reply);
            string simIP = ParseString("sim_ip", reply);
            IPAddress.TryParse(simIP, out SimIP);
            SeedCapability = ParseString("seed_capability", reply);

            // Buddy list
            if (reply.ContainsKey("buddy-list") && reply["buddy-list"] is ArrayList)
            {
                List<BuddyListEntry> buddys = new List<BuddyListEntry>();

                ArrayList buddyArray = (ArrayList)reply["buddy-list"];
                foreach (object t in buddyArray)
                {
                    if (!(t is Hashtable)) continue;

                    BuddyListEntry bud = new BuddyListEntry();
                    Hashtable buddy = (Hashtable)t;

                    bud.BuddyId = ParseString("buddy_id", buddy);
                    bud.BuddyRightsGiven = (int)ParseUInt("buddy_rights_given", buddy);
                    bud.BuddyRightsHas = (int)ParseUInt("buddy_rights_has", buddy);

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

            // UDP Blacklist
            if (reply.ContainsKey("udp_blacklist"))
            {
                UDPBlacklist = ParseString("udp_blacklist", reply);
            }

            if (reply.ContainsKey("max-agent-groups"))
            {
                MaxAgentGroups = (int)ParseUInt("max-agent-groups", reply);
            }
            else
            {
                MaxAgentGroups = -1;
            }

            if (reply.ContainsKey("openid_url"))
            {
                OpenIDUrl = ParseString("openid_url", reply);
            }

            if (reply.ContainsKey("agent_appearance_service"))
            {
                AgentAppearanceServiceURL = ParseString("agent_appearance_service", reply);
            }

            COFVersion = 0;
            if (reply.ContainsKey("cof_version"))
            {
                COFVersion = ParseUInt("cof_version", reply);
            }

            InitialOutfit = string.Empty;
            if (reply.ContainsKey("initial-outfit") && reply["initial-outfit"] is ArrayList)
            {
                ArrayList array = (ArrayList)reply["initial-outfit"];
                foreach (object t in array)
                {
                    if (!(t is Hashtable)) continue;

                    Hashtable map = (Hashtable)t;
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
                ArrayList array = (ArrayList)reply["login-flags"];
                foreach (object t in array)
                {
                    if (!(t is Hashtable)) continue;

                    Hashtable map = (Hashtable)t;
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
            return reply.TryGetValue(key, out osd) ? osd.AsString() : String.Empty;
        }

        public static string ParseString(string key, Hashtable reply)
        {
            return reply.ContainsKey(key) ? $"{reply[key]}" : String.Empty;
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
                    OSDArray array = (OSDArray)OSDParser.DeserializeLLSDNotation(osd.AsString());
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
                IList list = list1;
                if (list.Count == 3)
                {
                    float x, y, z;
                    Single.TryParse((string)list[0], out x);
                    Single.TryParse((string)list[1], out y);
                    Single.TryParse((string)list[2], out z);

                    return new Vector3(x, y, z);
                }
            }
            else if (value is string str)
            {
                OSDArray array = (OSDArray)OSDParser.DeserializeLLSDNotation(str);
                return array.AsVector3();
            }

            return Vector3.Zero;
        }

        public static UUID ParseMappedUUID(string key, string key2, OSDMap reply)
        {
            OSD folderOSD;
            if (reply.TryGetValue(key, out folderOSD) && folderOSD.Type == OSDType.Array)
            {
                OSDArray array = (OSDArray)folderOSD;
                if (array.Count == 1 && array[0].Type == OSDType.Map)
                {
                    OSDMap map = (OSDMap)array[0];
                    OSD folder;
                    if (map.TryGetValue(key2, out folder))
                        return folder.AsUUID();
                }
            }

            return UUID.Zero;
        }

        public static UUID ParseMappedUUID(string key, string key2, Hashtable reply)
        {
            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList)) return UUID.Zero;

            ArrayList array = (ArrayList)reply[key];
            if (array.Count == 1 && array[0] is Hashtable)
            {
                Hashtable map = (Hashtable)array[0];
                return ParseUUID(key2, map);
            }

            return UUID.Zero;
        }

        public static InventoryFolder[] ParseInventoryFolders(string key, UUID owner, OSDMap reply)
        {
            List<InventoryFolder> folders = new List<InventoryFolder>();

            OSD skeleton;
            if (reply.TryGetValue(key, out skeleton) && skeleton.Type == OSDType.Array)
            {
                OSDArray array = (OSDArray)skeleton;

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
            }

            return folders.ToArray();
        }

        public InventoryFolder[] ParseInventorySkeleton(string key, OSDMap reply)
        {
            List<InventoryFolder> folders = new List<InventoryFolder>();

            OSD skeleton;
            if (reply.TryGetValue(key, out skeleton) && skeleton.Type == OSDType.Array)
            {
                OSDArray array = (OSDArray)skeleton;
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
            }
            return folders.ToArray();
        }

        public InventoryFolder[] ParseInventorySkeleton(string key, Hashtable reply)
        {
            UUID ownerID;
            ownerID = key.Equals("inventory-skel-lib") ? LibraryOwner : AgentID;

            List<InventoryFolder> folders = new List<InventoryFolder>();

            if (reply.ContainsKey(key) && reply[key] is ArrayList)
            {
                ArrayList array = (ArrayList)reply[key];
                foreach (object t in array)
                {
                    if (!(t is Hashtable)) continue;
                    Hashtable map = (Hashtable)t;
                    InventoryFolder folder = new InventoryFolder(ParseUUID("folder_id", map))
                    {
                        Name = ParseString("name", map),
                        ParentUUID = ParseUUID("parent_id", map),
                        PreferredType = (FolderType)ParseUInt("type_default", map),
                        Version = (int)ParseUInt("version", map),
                        OwnerID = ownerID
                    };

                    folders.Add(folder);
                }
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
        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<LoginProgressEventArgs> m_LoginProgress;

        ///<summary>Raises the LoginProgress Event</summary>
        /// <param name="e">A LoginProgressEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnLoginProgress(LoginProgressEventArgs e)
        {
            EventHandler<LoginProgressEventArgs> handler = m_LoginProgress;
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

        ///// <summary>The event subscribers, null of no subscribers</summary>
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
        /// <summary>Seed CAPS URL returned from the login server</summary>
        public string LoginSeedCapability = String.Empty;
        /// <summary>Current state of logging in</summary>
        public LoginStatus LoginStatusCode { get { return InternalStatusCode; } }
        /// <summary>Upon login failure, contains a short string key for the
        /// type of login error that occurred</summary>
        public string LoginErrorKey => InternalErrorKey;

        /// <summary>The raw XML-RPC reply from the login server, exactly as it
        /// was received (minus the HTTP header)</summary>
        public string RawLoginReply => InternalRawLoginReply;

        /// <summary>During login this contains a descriptive version of 
        /// LoginStatusCode. After a successful login this will contain the 
        /// message of the day, and after a failed login a descriptive error 
        /// message will be returned</summary>
        public string LoginMessage => InternalLoginMessage;

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
        private AutoResetEvent LoginEvent = new AutoResetEvent(false);
        private LoginStatus InternalStatusCode = LoginStatus.None;
        private string InternalErrorKey = String.Empty;
        private string InternalLoginMessage = String.Empty;
        private string InternalRawLoginReply = String.Empty;
        private Dictionary<LoginResponseCallback, string[]> CallbackOptions = new Dictionary<LoginResponseCallback, string[]>();

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
        /// <returns>A populated <seealso cref="LoginParams"/> struct containing
        /// sane defaults</returns>
        public LoginParams DefaultLoginParams(string firstName, string lastName, string password,
            string channel, string version)
        {
            return new LoginParams(Client, firstName, lastName, password, channel, version);
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
            LoginParams loginParams = DefaultLoginParams(firstName, lastName, password, channel, version);
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
                InternalStatusCode = LoginStatus.Failed;
                InternalLoginMessage = "Timed out";
                return false;
            }

            return (InternalStatusCode == LoginStatus.Success);
        }

        public void BeginLogin(LoginParams loginParams)
        {
            // FIXME: Now that we're using CAPS we could cancel the current login and start a new one
            if (CurrentContext != null)
                throw new Exception("Login already in progress");

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
        /// <returns>String with a URI that can be used to login to a specified
        /// location</returns>
        public static string StartLocation(string sim, int x, int y, int z)
        {
            return $"uri:{sim}&{x}&{y}&{z}";
        }
        public void AbortLogin()
        {
            LoginParams loginParams = CurrentContext;
            CurrentContext = null; // Will force any pending callbacks to bail out early
            // FIXME: Now that we're using CAPS we could cancel the current login and start a new one
            if (loginParams == null)
            {
                Logger.DebugLog("No Login was in progress: " + CurrentContext, Client);
            }
            else
            {
                InternalStatusCode = LoginStatus.Failed;
                InternalLoginMessage = "Aborted";
            }
            UpdateLoginStatus(LoginStatus.Failed, "Abort Requested");
        }

        #endregion

        #region Private Methods

        private void BeginLogin()
        {
            LoginParams loginParams = CurrentContext;
            // Generate a random ID to identify this login attempt
            loginParams.LoginID = UUID.Random();
            CurrentContext = loginParams;

            #region Sanity Check loginParams

            if (loginParams.Options == null)
                loginParams.Options = new List<string>().ToArray();

            if (loginParams.Password == null)
                loginParams.Password = String.Empty;

            // Convert the password to MD5 if it isn't already
            if (loginParams.Password.Length != 35 && !loginParams.Password.StartsWith("$1$"))
                loginParams.Password = Utils.MD5(loginParams.Password);

            if (loginParams.ViewerDigest == null)
                loginParams.ViewerDigest = String.Empty;

            if (loginParams.Version == null)
                loginParams.Version = String.Empty;

            if (loginParams.UserAgent == null)
                loginParams.UserAgent = String.Empty;

            if (loginParams.Platform == null)
                loginParams.Platform = String.Empty;

            if (loginParams.PlatformVersion == null)
                loginParams.PlatformVersion = String.Empty;

            if (loginParams.MAC == null)
                loginParams.MAC = String.Empty;

            if (string.IsNullOrEmpty(loginParams.Channel))
            {
                Logger.Log("Viewer channel not set. This is a TOS violation on some grids.", Helpers.LogLevel.Warning);
                loginParams.Channel = "libopenmetaverse generic client";
            }

            if (loginParams.Author == null)
                loginParams.Author = String.Empty;

            #endregion

            // TODO: Allow a user callback to be defined for handling the cert
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            if (Client.Settings.USE_LLSD_LOGIN)
            {
                #region LLSD Based Login

                // Create the CAPS login structure
                OSDMap loginLLSD = new OSDMap
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

                // Create the options LLSD array
                OSDArray optionsOSD = new OSDArray();
                foreach (string t in loginParams.Options)
                    optionsOSD.Add(OSD.FromString(t));

                foreach (string[] callbackOpts in CallbackOptions.Values)
                {
                    if (callbackOpts == null) continue;
                    foreach (string t in callbackOpts)
                    {
                        if (!optionsOSD.Contains(t))
                            optionsOSD.Add(t);
                    }
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

                CapsClient loginRequest = new CapsClient(loginUri, "LoginRequest");
                loginRequest.OnComplete += LoginReplyLLSDHandler;
                loginRequest.UserData = CurrentContext;
                UpdateLoginStatus(LoginStatus.ConnectingToLogin,
                    $"Logging in as {loginParams.FirstName} {loginParams.LastName}...");
                loginRequest.BeginGetResponse(loginLLSD, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);

                #endregion
            }
            else
            {
                #region XML-RPC Based Login Code

                // Create the Hashtable for XmlRpcCs
                Hashtable loginXmlRpc = new Hashtable
                {
                    ["first"] = loginParams.FirstName,
                    ["last"] = loginParams.LastName,
                    ["passwd"] = loginParams.Password,
                    ["start"] = loginParams.Start,
                    ["channel"] = loginParams.Channel,
                    ["version"] = loginParams.Version,
                    ["platform"] = loginParams.Platform,
                    ["platform_version"] = loginParams.PlatformVersion,
                    ["mac"] = loginParams.MAC
                };
                if (loginParams.AgreeToTos)
                    loginXmlRpc["agree_to_tos"] = "true";
                if (loginParams.ReadCritical)
                    loginXmlRpc["read_critical"] = "true";
                loginXmlRpc["id0"] = loginParams.ID0;
                loginXmlRpc["last_exec_event"] = (int)loginParams.LastExecEvent;

                // Create the options array
                ArrayList options = new ArrayList();
                foreach (string t in loginParams.Options)
                    options.Add(t);

                foreach (string[] callbackOpts in CallbackOptions.Values)
                {
                    if (callbackOpts == null) continue;
                    foreach (string t in callbackOpts)
                    {
                        if (!options.Contains(t))
                            options.Add(t);
                    }
                }
                loginXmlRpc["options"] = options;

                try
                {
                    ArrayList loginArray = new ArrayList(1) { loginXmlRpc };
                    XmlRpcRequest request = new XmlRpcRequest(CurrentContext.MethodName, loginArray);
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
                                "Error opening the login server connection: " + e.Message);
                        }
                    });
                }
                catch (Exception e)
                {
                    UpdateLoginStatus(LoginStatus.Failed, "Error opening the login server connection: " + e);
                }

                #endregion
            }
        }

        private void UpdateLoginStatus(LoginStatus status, string message)
        {
            InternalStatusCode = status;
            InternalLoginMessage = message;

            Logger.DebugLog("Login status: " + status.ToString() + ": " + message, Client);

            // If we reached a login resolution trigger the event
            if (status == LoginStatus.Success || status == LoginStatus.Failed)
            {
                CurrentContext = null;
                LoginEvent.Set();
            }

            // Fire the login status callback
            if (m_LoginProgress != null)
            {
                OnLoginProgress(new LoginProgressEventArgs(status, message, InternalErrorKey));
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
            LoginResponseData reply = new LoginResponseData();
            // Fetch the login response
            if (!(response?.Value is Hashtable))
            {
                UpdateLoginStatus(LoginStatus.Failed, "Invalid or missing login response from the server");
                Logger.Log("Invalid or missing login response from the server", Helpers.LogLevel.Warning);
                return;
            }

            try
            {
                reply.Parse((Hashtable)response.Value);
                if (context.LoginID != CurrentContext.LoginID)
                {
                    Logger.Log("Login response does not match login request. Only one login can be attempted at a time",
                        Helpers.LogLevel.Error);
                    return;
                }
            }
            catch (Exception e)
            {
                UpdateLoginStatus(LoginStatus.Failed, "Error retrieving the login response from the server: " + e.Message);
                Logger.Log("Login response failure: " + e.Message + " " + e.StackTrace, Helpers.LogLevel.Warning);
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
            string reason = reply.Reason;
            string message = reply.Message;

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
                    LoginSeedCapability = reply.SeedCapability;
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

                // Unhandled:
                // reply.gestures
                // reply.event_categories
                // reply.classified_categories
                // reply.event_notifications
                // reply.ui_config
                // reply.login_flags
                // reply.global_textures
                // reply.inventory_lib_root
                // reply.inventory_lib_owner
                // reply.inventory_skeleton
                // reply.inventory_skel_lib
                // reply.initial_outfit
            }

            bool redirect = (reply.Login == "indeterminate");

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
                LoginParams loginParams = CurrentContext;
                loginParams.URI = reply.NextUrl;
                loginParams.MethodName = reply.NextMethod;
                loginParams.Options = reply.NextOptions;

                // Sleep for some amount of time while the servers work
                int seconds = reply.NextDuration;
                Logger.Log("Sleeping for " + seconds + " seconds during a login redirect",
                    Helpers.LogLevel.Info);
                Thread.Sleep(seconds * 1000);

                CurrentContext = loginParams;
                BeginLogin();
            }
            else if (reply.Success)
            {
                UpdateLoginStatus(LoginStatus.ConnectingToSim, "Connecting to simulator...");

                ulong handle = Utils.UIntsToLong(regionX, regionY);

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

                InternalErrorKey = !string.IsNullOrEmpty(reason) ? reason : "unknown";

                UpdateLoginStatus(LoginStatus.Failed, message);
            }
        }

        /// <summary>
        /// Handle response from LLSD login replies
        /// </summary>
        /// <param name="client"></param>
        /// <param name="result"></param>
        /// <param name="error"></param>
        private void LoginReplyLLSDHandler(CapsClient client, OSD result, Exception error)
        {
            if (error == null)
            {
                if (result != null && result.Type == OSDType.Map)
                {
                    OSDMap map = (OSDMap)result;
                    OSD osd;

                    LoginResponseData data = new LoginResponseData();
                    data.Parse(map);

                    if (map.TryGetValue("login", out osd))
                    {
                        bool loginSuccess = osd.AsBoolean();
                        bool redirect = (osd.AsString() == "indeterminate");

                        if (redirect)
                        {
                            // Login redirected

                            // Make the next login URL jump
                            UpdateLoginStatus(LoginStatus.Redirecting, data.Message);

                            LoginParams loginParams = CurrentContext;
                            loginParams.URI = LoginResponseData.ParseString("next_url", map);
                            //CurrentContext.Params.MethodName = LoginResponseData.ParseString("next_method", map);

                            // Sleep for some amount of time while the servers work
                            int seconds = (int)LoginResponseData.ParseUInt("next_duration", map);
                            Logger.Log("Sleeping for " + seconds + " seconds during a login redirect",
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
                            LoginSeedCapability = data.SeedCapability;

                            UpdateLoginStatus(LoginStatus.ConnectingToSim, "Connecting to simulator...");

                            ulong handle = Utils.UIntsToLong(data.RegionX, data.RegionY);

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
                            InternalErrorKey = data.Reason != String.Empty ? data.Reason : "unknown";

                            UpdateLoginStatus(LoginStatus.Failed, data.Message);
                        }
                    }
                    else
                    {
                        // Got an LLSD map but no login value
                        UpdateLoginStatus(LoginStatus.Failed, "login parameter missing in the response");
                    }
                }
                else
                {
                    // No LLSD response
                    InternalErrorKey = "bad response";
                    UpdateLoginStatus(LoginStatus.Failed, "Empty or unparseable login response");
                }
            }
            else
            {
                // Connection error
                InternalErrorKey = "no connection";
                UpdateLoginStatus(LoginStatus.Failed, error.Message);
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
            string mac = string.Empty;

            try
            {
                System.Net.NetworkInformation.NetworkInterface[] nics =
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                if (nics.Length > 0)
                {
                    foreach (NetworkInterface t in nics)
                    {
                        string adapterMac = t.GetPhysicalAddress().ToString().ToUpper();
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

            return String.Format("{0}:{1}:{2}:{3}:{4}:{5}",
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
            MD5 sec = new MD5CryptoServiceProvider();
            ASCIIEncoding enc = new ASCIIEncoding();
            byte[] buf = enc.GetBytes(str);
            return GetHexString(sec.ComputeHash(buf));
        }

        private static string GetHexString(byte[] buf)
        {
            string str = string.Empty;

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