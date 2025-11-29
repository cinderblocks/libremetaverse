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

    public enum LoginStatus
    {
        Failed = -1,
        None = 0,
        ConnectingToLogin,
        ReadingResponse,
        ConnectingToSim,
        Redirecting,
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

    /// <summary>Login Request Parameters</summary>
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
        /// <summary>If true, this agent agrees to the Terms of Service of the grid it is connecting to</summary>
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
        /// <remarks>Use this constructor if Application supports multifactor authentication</remarks>
        /// <param name="client">Instance of <see cref="GridClient"/></param>
        /// <param name="credential">Instance of <see cref="LoginCredential"/></param>
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
        /// <remarks>Use this constructor if Application supports multifactor authentication</remarks>
        /// <param name="client">Instance of <see cref="GridClient"/></param>
        /// <param name="credential">Instance of <see cref="LoginCredential"/></param>
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

    public class HomeInfo
    {
        public ulong RegionHandle;
        public Vector3 Position;
        public Vector3 LookAt;
    }

    public class AccountLevelBenefits
    {
        public int MeshUploadCost { get; }
        public int OneTimeEventAllowed { get; }
        public int RepeatingEventsCost { get; }
        public string EstateAccessToken { get; }
        public int LastnameChangeRate { get; }
        public int MarketplaceConciergeSupport { get; }
        public int PhoneSupport { get; }
        public double LindenBuyFee { get; }
        public int PartnerFee { get; }
        public int CreateRepeatingEvents { get; }
        public int TextureUploadCost { get; }
        public int PriorityEntry { get; }
        public int ObjectAccountLevel { get; }
        public int UseAnimesh { get; }
        public double LastnameChangeCost { get; }
        public int BetaGridLand { get; }
        public int CreateGroupCost { get; }
        public int LiveChat { get; }
        public int LandAuctionsAllowed { get; }
        public int MainlandTier { get; }
        public int LocalExperiences { get; }
        public int OneTimeEventCost { get; }
        public int MarketplacePleLimit { get; }
        public int UnpartnerFee { get; }
        public int GroupMembershipLimit { get; }
        public int AnimationUploadCost { get; }
        public int ScriptLimit { get; }
        public int TransactionHistoryLimit { get; }
        public List<int> LargeTextureUploadCost { get; }
        public int AnimatedObjectLimit { get; }
        public int GridwideExperienceLimit { get; }
        public int LastnameChangeAllowed { get; }
        public int SoundUploadCost { get; }
        public int PremiumAlts { get; }
        public int PicksLimit { get; }
        public int PlacePages { get; }
        public int PremiumAccess { get; }
        public int MarketplaceListingLimit { get; }
        public int PremiumGifts { get; }
        public int VoiceMorphing { get; }
        public int StoredImLimit { get; }
        public int Stipend { get; }
        public int SignupBonus { get; }
        public int AttachmentLimit { get; }

        public AccountLevelBenefits(OSDMap reply)
        {
            MeshUploadCost = GetIntFromMap(reply, "mesh_upload_cost", -1);
            OneTimeEventAllowed = GetIntFromMap(reply, "one_time_event_allowed", -1);
            RepeatingEventsCost = GetIntFromMap(reply, "repeating_events_cost", -1);
            EstateAccessToken = GetStringFromMap(reply, "estate_access_token", string.Empty);
            LastnameChangeRate = GetIntFromMap(reply, "lastname_change_rate", -1);
            MarketplaceConciergeSupport = GetIntFromMap(reply, "marketplace_concierge_support", -1);
            PhoneSupport = GetIntFromMap(reply, "phone_support", -1);
            LindenBuyFee = GetRealFromMap(reply, "linden_buy_fee", -1.0);
            PartnerFee = GetIntFromMap(reply, "partner_fee", -1);
            CreateRepeatingEvents = GetIntFromMap(reply, "create_repeating_events", -1);
            TextureUploadCost = GetIntFromMap(reply, "texture_upload_cost", -1);
            PriorityEntry = GetIntFromMap(reply, "priority_entry", -1);
            ObjectAccountLevel = GetIntFromMap(reply, "object_account_level", -1);
            UseAnimesh = GetIntFromMap(reply, "use_animesh", -1);
            LastnameChangeCost = GetRealFromMap(reply, "lastname_change_cost", -1.0);
            BetaGridLand = GetIntFromMap(reply, "beta_grid_land", -1);
            CreateGroupCost = GetIntFromMap(reply, "create_group_cost", -1);
            LiveChat = GetIntFromMap(reply, "live_chat", -1);
            LandAuctionsAllowed = GetIntFromMap(reply, "land_auctions_allowed", -1);
            MainlandTier = GetIntFromMap(reply, "mainland_tier", -1);
            LocalExperiences = GetIntFromMap(reply, "local_experiences", -1);
            OneTimeEventCost = GetIntFromMap(reply, "one_time_event_cost", -1);
            MarketplacePleLimit = GetIntFromMap(reply, "marketplace_ple_limit", -1);
            UnpartnerFee = GetIntFromMap(reply, "unpartner_fee", -1);
            GroupMembershipLimit = GetIntFromMap(reply, "group_membership_limit", -1);
            AnimationUploadCost = GetIntFromMap(reply, "animation_upload_cost", -1);
            ScriptLimit = GetIntFromMap(reply, "script_limit", -1);
            TransactionHistoryLimit = GetIntFromMap(reply, "transaction_history_limit", -1);
            LargeTextureUploadCost = GetListFromMap<int>(reply, "large_texture_upload_cost");
            AnimatedObjectLimit = GetIntFromMap(reply, "animated_object_limit", -1);
            GridwideExperienceLimit = GetIntFromMap(reply, "gridwide_experience_limit", -1);
            LastnameChangeAllowed = GetIntFromMap(reply, "lastname_change_allowed", -1);
            SoundUploadCost = GetIntFromMap(reply, "sound_upload_cost", -1);
            PremiumAlts = GetIntFromMap(reply, "premium_alts", -1);
            PicksLimit = GetIntFromMap(reply, "picks_limit", -1);
            PlacePages = GetIntFromMap(reply, "place_pages", -1);
            PremiumAccess = GetIntFromMap(reply, "premium_access", -1);
            MarketplaceListingLimit = GetIntFromMap(reply, "marketplace_listing_limit", -1);
            PremiumGifts = GetIntFromMap(reply, "premium_gifts", -1);
            VoiceMorphing = GetIntFromMap(reply, "voice_morphing", -1);
            StoredImLimit = GetIntFromMap(reply, "stored_im_limit", -1);
            Stipend = GetIntFromMap(reply, "stipend", -1);
            SignupBonus = GetIntFromMap(reply, "signup_bonus", -1);
            AttachmentLimit = GetIntFromMap(reply, "attachment_limit", 38);

            if (LargeTextureUploadCost.Count == 0)
            {
                LargeTextureUploadCost = new List<int>()
                {
                    TextureUploadCost
                };
            }
            else
            {
                LargeTextureUploadCost = LargeTextureUploadCost
                    .OrderBy(n => n)
                    .ToList();
            }
        }

        public AccountLevelBenefits(Hashtable reply)
        {
            MeshUploadCost = GetValueFromMap(reply, "mesh_upload_cost", -1);
            OneTimeEventAllowed = GetValueFromMap(reply, "one_time_event_allowed", -1);
            RepeatingEventsCost = GetValueFromMap(reply, "repeating_events_cost", -1);
            EstateAccessToken = GetValueFromMap(reply, "estate_access_token", string.Empty);
            LastnameChangeRate = GetValueFromMap(reply, "lastname_change_rate", -1);
            MarketplaceConciergeSupport = GetValueFromMap(reply, "marketplace_concierge_support", -1);
            PhoneSupport = GetValueFromMap(reply, "phone_support", -1);
            LindenBuyFee = GetValueFromMap(reply, "linden_buy_fee", -1.0);
            PartnerFee = GetValueFromMap(reply, "partner_fee", -1);
            CreateRepeatingEvents = GetValueFromMap(reply, "create_repeating_events", -1);
            TextureUploadCost = GetValueFromMap(reply, "texture_upload_cost", -1);
            PriorityEntry = GetValueFromMap(reply, "priority_entry", -1);
            ObjectAccountLevel = GetValueFromMap(reply, "object_account_level", -1);
            UseAnimesh = GetValueFromMap(reply, "use_animesh", -1);
            LastnameChangeCost = GetValueFromMap(reply, "lastname_change_cost", -1.0);
            BetaGridLand = GetValueFromMap(reply, "beta_grid_land", -1);
            CreateGroupCost = GetValueFromMap(reply, "create_group_cost", -1);
            LiveChat = GetValueFromMap(reply, "live_chat", -1);
            LandAuctionsAllowed = GetValueFromMap(reply, "land_auctions_allowed", -1);
            MainlandTier = GetValueFromMap(reply, "mainland_tier", -1);
            LocalExperiences = GetValueFromMap(reply, "local_experiences", -1);
            OneTimeEventCost = GetValueFromMap(reply, "one_time_event_cost", -1);
            MarketplacePleLimit = GetValueFromMap(reply, "marketplace_ple_limit", -1);
            UnpartnerFee = GetValueFromMap(reply, "unpartner_fee", -1);
            GroupMembershipLimit = GetValueFromMap(reply, "group_membership_limit", -1);
            AnimationUploadCost = GetValueFromMap(reply, "animation_upload_cost", -1);
            ScriptLimit = GetValueFromMap(reply, "script_limit", -1);
            TransactionHistoryLimit = GetValueFromMap(reply, "transaction_history_limit", -1);
            LargeTextureUploadCost = GetListFromMap<int>(reply, "large_texture_upload_cost");
            AnimatedObjectLimit = GetValueFromMap(reply, "animated_object_limit", -1);
            GridwideExperienceLimit = GetValueFromMap(reply, "gridwide_experience_limit", -1);
            LastnameChangeAllowed = GetValueFromMap(reply, "lastname_change_allowed", -1);
            SoundUploadCost = GetValueFromMap(reply, "sound_upload_cost", -1);
            PremiumAlts = GetValueFromMap(reply, "premium_alts", -1);
            PicksLimit = GetValueFromMap(reply, "picks_limit", -1);
            PlacePages = GetValueFromMap(reply, "place_pages", -1);
            PremiumAccess = GetValueFromMap(reply, "premium_access", -1);
            MarketplaceListingLimit = GetValueFromMap(reply, "marketplace_listing_limit", -1);
            PremiumGifts = GetValueFromMap(reply, "premium_gifts", -1);
            VoiceMorphing = GetValueFromMap(reply, "voice_morphing", -1);
            StoredImLimit = GetValueFromMap(reply, "stored_im_limit", -1);
            Stipend = GetValueFromMap(reply, "stipend", -1);
            SignupBonus = GetValueFromMap(reply, "signup_bonus", -1);
            AttachmentLimit = GetValueFromMap(reply, "attachment_limit", 38);

            if (LargeTextureUploadCost.Count == 0)
            {
                LargeTextureUploadCost = new List<int>()
                {
                    TextureUploadCost
                };
            }
            else
            {
                LargeTextureUploadCost = LargeTextureUploadCost
                    .OrderBy(n => n)
                    .ToList();
            }
        }

        private static T GetValueFromMap<T>(Hashtable reply, string key, T defaultValue)
        {
            if (reply.ContainsKey(key))
            {
                if (reply[key] is T result)
                {
                    return result;
                }
            }

            return defaultValue;
        }

        private static List<T> GetListFromMap<T>(Hashtable reply, string key)
        {
            if (!reply.ContainsKey(key) || !(reply[key] is ArrayList valArray))
            {
                return new List<T>();
            }

            return valArray.OfType<T>().ToList();
        }

        private static int GetIntFromMap(OSDMap reply, string key, int defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsInteger() : defaultValue;
        }

        private static double GetRealFromMap(OSDMap reply, string key, double defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsReal() : defaultValue;
        }

        private static string GetStringFromMap(OSDMap reply, string key, string defaultValue)
        {
            return reply.TryGetValue(key, out var osd) ? osd.AsString() : defaultValue;
        }

        private static List<T> GetListFromMap<T>(OSDMap reply, string key)
        {
            if(!reply.TryGetValue(key, out var osd) || !(osd is OSDArray osdArray))
            {
                return new List<T>();
            }

            return new List<T>(osdArray);
        }
    }

    #endregion Structs

    public class LoginException : Exception
    {
        public LoginException(string message) 
            : base(message)
        {}

        public LoginException(string message, Exception innerException)
            : base (message, innerException)
        {}
    }
    
    /// <summary>
    /// Provides login routines and network-related state used during the
    /// login sequence. This partial class contains methods and events used to
    /// perform credential exchange with the grid login service, handle
    /// redirects and multifactor challenges, and establish a connection to
    /// the simulator returned by the login reply.
    /// </summary>
    public partial class NetworkManager
    {
        #region Delegates

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

        /// <summary>Raised when the simulator sends us data containing</summary>
        public event EventHandler<LoginProgressEventArgs> LoginProgress
        {
            add { lock (m_LoginProgressLock) { m_LoginProgress += value; } }
            remove { lock (m_LoginProgressLock) { m_LoginProgress -= value; } }
        }

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

        /// <summary>During login this contains a descriptive version of 
        /// LoginStatusCode. After a successful login this will contain the 
        /// message of the day, and after a failed login a descriptive error 
        /// message will be returned</summary>
        public string LoginMessage { get; private set; } = string.Empty;

        /// <summary>Parsed login response data</summary>
        public LoginResponseData LoginResponseData;

        /// <summary>Maximum number of groups an agent can belong to, -1 for unlimited</summary>
        public int MaxAgentGroups => LoginResponseData?.MaxAgentGroups ?? -1;
        /// <summary>Server side baking service URL</summary>
        public string AgentAppearanceServiceURL => LoginResponseData?.AgentAppearanceServiceURL;

        #endregion Public Members

        #region Private Members
        
        private LoginParams CurrentContext = null;
        // Cancellation token source for the active login request
        private CancellationTokenSource loginCts;
        // TaskCompletionSource used for async login waiting
        private TaskCompletionSource<bool> loginTcs;
        // Carries the parsed LoginResponseData result for the async API
        private TaskCompletionSource<LoginResponseData> loginResultTcs;

        private readonly Dictionary<LoginResponseCallback, string[]> CallbackOptions = new Dictionary<LoginResponseCallback, string[]>();

        /// <summary>A list of packets obtained during the login process which 
        /// NetworkManager will log but not process</summary>
        private readonly List<PacketType> UDPBlacklist = new List<PacketType>();

        // MAC Caching
        private static readonly object s_macLock = new object();
        private static string s_cachedMac = null;
        private static DateTime s_cachedMacTimestamp = DateTime.MinValue;
        /// <summary>Cache TTL in seconds</summary>
        private const int MAC_CACHE_TTL_SECONDS = 300;

        #endregion Private Members

        #region Public Methods

        /// <summary>
        /// Generate sane default values for a login request
        /// </summary>
        /// <param name="firstName">Account first name</param>
        /// <param name="lastName">Account last name</param>
        /// <param name="password">Account password</param>
        /// <param name="channel">Client application name (channel)</param>
        /// <param name="version">Version string (typically x.x.x)</param>
        /// <returns>A populated <see cref="LoginParams"/> object containing sane defaults</returns>
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
        /// <param name="version">Version string (typically x.x.x)</param>
        /// <returns>Whether the login was successful or not. On failure the
        /// LoginErrorKey string will contain the error code and LoginMessage
        /// will contain a description of the error</returns>
        [Obsolete("Use LoginAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
        public bool Login(string firstName, string lastName, string password, string channel, string version)
        {
            return Login(firstName, lastName, password, channel, "last", version);
        }

        /// <summary>
        /// Login that works via an existing login response
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
        [Obsolete("Use LoginWithResponseAsync or LoginAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
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
                    SimPort = (ushort)port, Login = LoginState.True
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
                catch (Exception ex) { Logger.Error(ex.Message, ex, Client); }
            }

            // These parameters are stored in NetworkManager, so instead of registering
            // another callback for them, we set the values here
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
        /// <param name="version">Version string (typically x.x.x)</param>
        /// <returns>Whether the login was successful or not. On failure the
        /// LoginErrorKey string will contain the error code and LoginMessage
        /// will contain a description of the error</returns>
        [Obsolete("Use LoginAsync instead (async-first). This synchronous wrapper will block the calling thread.")]
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
        [Obsolete("Use LoginAsync(LoginParams) or LoginWithResponseAsync(LoginParams) instead (async-first). This synchronous wrapper will block the calling thread.")]
        public bool Login(LoginParams loginParams)
        {
            try
            {
                return LoginAsync(loginParams).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        public void BeginLogin(LoginParams loginParams)
        {
            // FIXME: Now that we're using CAPS we could cancel the current login and start a new one
            if (CurrentContext != null) {throw new LoginException("Login already in progress");}

            // initialize async wait primitives
            loginTcs?.TrySetResult(false);
            loginTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            loginResultTcs?.TrySetCanceled();
            loginResultTcs = new TaskCompletionSource<LoginResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);

            CurrentContext = loginParams;

            BeginLogin();
        }

        /// <summary>
        /// Async version of Login that returns a Task and supports cancellation.
        /// </summary>
        public async Task<bool> LoginAsync(LoginParams loginParams, CancellationToken cancellationToken = default)
        {
            // Create or replace the per-login cancellation source, linking caller token
            loginCts?.Dispose();
            loginCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Configure timeout via CancelAfter so Http client cancellations are honored
            try { loginCts.CancelAfter(loginParams.Timeout); } catch { }

            BeginLogin(loginParams);

            try
            {
                var parsed = await loginResultTcs.Task.ConfigureAwait(false);
                return parsed != null && parsed.Success;
            }
            catch (OperationCanceledException)
            {
                UpdateLoginStatus(LoginStatus.Failed, "Canceled");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("LoginAsync failed", ex, Client);
                UpdateLoginStatus(LoginStatus.Failed, "Login failed");
                return false;
            }
        }

        /// <summary>
        /// Async convenience overload that returns the parsed LoginResponseData.
        /// </summary>
        public async Task<LoginResponseData> LoginWithResponseAsync(LoginParams loginParams, CancellationToken cancellationToken = default)
        {
            // Create or replace the per-login cancellation source, linking caller token
            loginCts?.Dispose();
            loginCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try { loginCts.CancelAfter(loginParams.Timeout); } catch { }

            BeginLogin(loginParams);

            try
            {
                return await loginResultTcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                UpdateLoginStatus(LoginStatus.Failed, "Canceled");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("LoginWithResponseAsync failed", ex, Client);
                return null;
            }
        }

        /// <summary>
        /// Async version of Login that returns a Task and supports cancellation.
        /// </summary>
        public Task<bool> LoginAsync(string firstName, string lastName, string password, string channel, string start, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = DefaultLoginParams(firstName, lastName, password, channel, version);
            loginParams.Start = start;
            return LoginAsync(loginParams, cancellationToken);
        }

        /// <summary>
        /// Async convenience overload that accepts a pre-built LoginCredential instance.
        /// </summary>
        public Task<bool> LoginAsync(LoginCredential credential, string channel, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = new LoginParams(Client, credential, channel, version);
            return LoginAsync(loginParams, cancellationToken);
        }

        /// <summary>
        /// Async convenience overload that accepts a pre-built LoginCredential instance and explicit start location.
        /// </summary>
        public Task<bool> LoginAsync(LoginCredential credential, string channel, string start, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = new LoginParams(Client, credential, channel, version);
            loginParams.Start = start;
            return LoginAsync(loginParams, cancellationToken);
        }

        /// <summary>
        /// Async convenience overload with explicit start location that returns the parsed LoginResponseData.
        /// </summary>
        public Task<LoginResponseData> LoginWithResponseAsync(string firstName, string lastName, string password, string channel, string start, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = DefaultLoginParams(firstName, lastName, password, channel, version);
            loginParams.Start = start;
            return LoginWithResponseAsync(loginParams, cancellationToken);
        }

        /// <summary>
        /// Async convenience overload that accepts a pre-built LoginCredential instance and returns parsed LoginResponseData.
        /// </summary>
        public Task<LoginResponseData> LoginWithResponseAsync(LoginCredential credential, string channel, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = new LoginParams(Client, credential, channel, version);
            return LoginWithResponseAsync(loginParams, cancellationToken);
        }

        /// <summary>
        /// Async convenience overload that accepts a pre-built LoginCredential instance with explicit start location and returns parsed LoginResponseData.
        /// </summary>
        public Task<LoginResponseData> LoginWithResponseAsync(LoginCredential credential, string channel, string start, string version, CancellationToken cancellationToken = default)
        {
            var loginParams = new LoginParams(Client, credential, channel, version);
            loginParams.Start = start;
            return LoginWithResponseAsync(loginParams, cancellationToken);
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
        /// Build a start location URI for passing to the Login function.
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
        /// <summary>
        /// Abort the in-progress login attempt, if any. This will cancel outstanding
        /// network requests and mark the login as failed. Any registered login
        /// progress callbacks will receive a failure notification.
        /// </summary>
        public void AbortLogin()
        {
            var loginParams = CurrentContext;
            CurrentContext = null; // Will force any pending callbacks to bail out early
            // Cancel any active login request
            try { loginCts?.Cancel(); } catch { }
            try { loginCts?.Dispose(); loginCts = null; } catch { }
            try { loginResultTcs?.TrySetCanceled(); } catch { }
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
            {
                loginParams.Options = new List<string>();
            }
            if (loginParams.Password == null)
            {
                loginParams.Password = string.Empty;
            }
            if (loginParams.ViewerDigest == null)
            {
                loginParams.ViewerDigest = string.Empty;
            }
            if (loginParams.UserAgent == null)
            {
                loginParams.UserAgent = Settings.USER_AGENT;
            }
            if (loginParams.Platform == null)
            {
                loginParams.Platform = string.Empty;
            }
            if (loginParams.PlatformVersion == null)
            {
                loginParams.PlatformVersion = string.Empty;
            }
            if (loginParams.MAC == null)
            {
                loginParams.MAC = string.Empty;
            }
            if (loginParams.Author == null)
            {
                loginParams.Author = string.Empty;
            }

            // *HACK: Convert the password to MD5 if it isn't already
            if (loginParams.Password.Length != 35 && !loginParams.Password.StartsWith("$1$"))
                loginParams.Password = Utils.MD5(loginParams.Password);
            
            if (string.IsNullOrEmpty(loginParams.Channel))
            {
                Logger.Warn("Viewer channel not set.");
                loginParams.Channel = $"{Settings.USER_AGENT}";
            }

            if (string.IsNullOrEmpty((loginParams.Version)))
            {
                Logger.Warn("Viewer version not set.");
                loginParams.Version = "?.?.?";
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
            
            #endregion

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
                Logger.Error($"Failed to parse login URI {loginParams.URI}, {ex.Message}", Client);
                return;
            }

            // prepare cancellation for this login if not already provided by caller
            if (loginCts == null)
            {
                loginCts = new CancellationTokenSource();
            }

            UpdateLoginStatus(LoginStatus.ConnectingToLogin,
                $"Logging in as {loginParams.FirstName} {loginParams.LastName}...");

            // Start an asynchronous login flow using a proper async method (no Task.Run fire-and-forget)
            var loginTask = PerformLoginAsync(loginUri, loginLLSD, loginCts.Token);

            // Observe exceptions to avoid unobserved task exceptions and log them
            loginTask.ContinueWith(t =>
            {
                if (t.Exception != null)
                    Logger.Error("Login task faulted", t.Exception.Flatten(), Client);
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
         }

        // Perform the HTTP POST and handle the LLSD reply asynchronously.
        // Separated into its own async method to avoid Task.Run and allow the Task to be observed.
        private async Task PerformLoginAsync(Uri loginUri, OSDMap loginLLSD, CancellationToken token)
        {
            try
            {
                var (resp, data) = await Client.HttpCapsClient.PostAsync(loginUri, OSDFormat.Xml, loginLLSD, token).ConfigureAwait(false);
                await LoginReplyLLSDHandler(resp, data, null).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // login canceled
                UpdateLoginStatus(LoginStatus.Failed, "Login canceled");
            }
            catch (Exception ex)
            {
                // Forward the exception into the reply handler so existing error handling is used
                try
                {
                    await LoginReplyLLSDHandler(null, null, ex).ConfigureAwait(false);
                }
                catch (Exception inner)
                {
                    Logger.Error("Unhandled exception in PerformLoginAsync", inner, Client);
                }
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
            }

            // Fire the login status callback
            if (m_LoginProgress != null)
            {
                OnLoginProgress(new LoginProgressEventArgs(status, message, LoginErrorKey));
            }

            // set the TaskCompletionSource result for async waits
            if (loginTcs != null)
            {
                if (status == LoginStatus.Success)
                    loginTcs.TrySetResult(true);
                else
                    loginTcs.TrySetResult(false);
            }

            if (loginResultTcs != null && (status == LoginStatus.Success || status == LoginStatus.Failed))
            {
                if (status == LoginStatus.Success && LoginResponseData != null)
                {
                    loginResultTcs.TrySetResult(LoginResponseData);
                }
                else
                {
                    loginResultTcs.TrySetCanceled();
                }
            }
        }

        /// <summary>
        /// Handle response from LLSD login replies
        /// </summary>
        /// <param name="response">Server response as <see cref="HttpResponseMessage"/></param>
        /// <param name="responseData">Payload response data</param>
        /// <param name="error">Any <see cref="Exception"/> returned from the request</param>
        private async Task LoginReplyLLSDHandler(HttpResponseMessage response, byte[] responseData, Exception error)
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

                // Store parsed login response on the NetworkManager so other
                // properties (e.g. AgentAppearanceServiceURL) can read it after login
                LoginResponseData = data;

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
                        Logger.Info($"Delaying for {seconds} seconds during a login redirect");
                        try
                        {
                            // honor cancellation
                            await Task.Delay(TimeSpan.FromSeconds(seconds), loginCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            UpdateLoginStatus(LoginStatus.Failed, "Login canceled");
                            return;
                        }

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
                            catch (Exception ex) { Logger.Error(ex.Message, ex, Client); }
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

            // set the parsed result for LoginResponseData
            if (loginResultTcs != null)
            {
                loginResultTcs.TrySetResult(LoginStatusCode == LoginStatus.Success ? LoginResponseData : null);
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
            var now = DateTime.UtcNow;
            if (s_cachedMac != null && (now - s_cachedMacTimestamp).TotalSeconds < MAC_CACHE_TTL_SECONDS)
            {
                return s_cachedMac;
            }

            lock (s_macLock)
            {
                now = DateTime.UtcNow;
                if (s_cachedMac != null && (now - s_cachedMacTimestamp).TotalSeconds < MAC_CACHE_TTL_SECONDS)
                {
                    return s_cachedMac;
                }

                var mac = string.Empty;

                try
                {
                    var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

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

                var formatted =
                    $"{mac.Substring(0, 2)}:{mac.Substring(2, 2)}:{mac.Substring(4, 2)}:{mac.Substring(6, 2)}:{mac.Substring(8, 2)}:{mac.Substring(10, 2)}";

                s_cachedMac = formatted;
                s_cachedMacTimestamp = DateTime.UtcNow;
                return s_cachedMac;
            }
        }

        /// <summary>
        /// Force a refresh of the cached MAC address.
        /// </summary>
        /// <remarks>
        /// This method is thread-safe. It clears the cached value and then triggers
        /// an immediate synchronous re-evaluation of the system network interfaces
        /// so that subsequent calls to <see cref="GetMAC"/> will return the up-to-date value.
        /// Call this after network interface changes if you require the MAC to be refreshed immediately.
        /// </remarks>
        public static void RefreshMac()
        {
            // Clear the cached value under lock
            lock (s_macLock)
            {
                s_cachedMac = null;
                s_cachedMacTimestamp = DateTime.MinValue;
            }

            try { GetMAC(); } catch { /*Swallow any exceptions; GetMAC already handles errors internally*/ }
        }

        /// <summary>
        /// MD5 hash of string
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>Hashed string</returns>
        private static string HashString(string str)
        {
            using (var md5 = MD5.Create())
            {
                var buf = Encoding.UTF8.GetBytes(str ?? string.Empty);
                return GetHexString(md5.ComputeHash(buf));
            }
        }

        private static string GetHexString(byte[] buf)
        {
            var sb = new StringBuilder(buf.Length * 2);
            foreach (var b in buf)
            {
                int hi = (b >> 4) & 0xF;
                int lo = b & 0xF;
                sb.Append(hi < 10 ? (char)('0' + hi) : (char)('A' + hi - 10));
                sb.Append(lo < 10 ? (char)('0' + lo) : (char)('A' + lo - 10));
            }

            return sb.ToString();
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

