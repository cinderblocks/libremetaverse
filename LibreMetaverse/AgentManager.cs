/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2021, Sjofn LLC
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Http;
using OpenMetaverse.Assets;
using OpenMetaverse.Packets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;

namespace OpenMetaverse
{
    #region Enums

    /// <summary>
    /// Permission request flags, asked when a script wants to control an Avatar
    /// </summary>
    [Flags]
    public enum ScriptPermission : int
    {
        /// <summary>Placeholder for empty values, shouldn't ever see this</summary>
        None = 0,
        /// <summary>Script wants ability to take money from you</summary>
        Debit = 1 << 1,
        /// <summary>Script wants to take camera controls for you</summary>
        TakeControls = 1 << 2,
        /// <summary>Script wants to remap avatars controls</summary>
        RemapControls = 1 << 3,
        /// <summary>Script wants to trigger avatar animations</summary>
        /// <remarks>This function is not implemented on the grid</remarks>
        TriggerAnimation = 1 << 4,
        /// <summary>Script wants to attach or detach the prim or primset to your avatar</summary>
        Attach = 1 << 5,
        /// <summary>Script wants permission to release ownership</summary>
        /// <remarks>This function is not implemented on the grid
        /// The concept of "public" objects does not exist anymore.</remarks>
        ReleaseOwnership = 1 << 6,
        /// <summary>Script wants ability to link/delink with other prims</summary>
        ChangeLinks = 1 << 7,
        /// <summary>Script wants permission to change joints</summary>
        /// <remarks>This function is not implemented on the grid</remarks>
        ChangeJoints = 1 << 8,
        /// <summary>Script wants permissions to change permissions</summary>
        /// <remarks>This function is not implemented on the grid</remarks>
        ChangePermissions = 1 << 9,
        /// <summary>Script wants to track avatars camera position and rotation </summary>
        TrackCamera = 1 << 10,
        /// <summary>Script wants to control your camera</summary>
        ControlCamera = 1 << 11,
        /// <summary>Script wants the ability to teleport you</summary>
        Teleport = 1 << 12
    }

    /// <summary>
    /// Special commands used in Instant Messages
    /// </summary>
    public enum InstantMessageDialog : byte
    {
        /// <summary>Indicates a regular IM from another agent</summary>
        MessageFromAgent = 0,
        /// <summary>Simple notification box with an OK button</summary>
        MessageBox = 1,
        // <summary>Used to show a countdown notification with an OK
        // button, deprecated now</summary>
        //[Obsolete]
        //MessageBoxCountdown = 2,
        /// <summary>You've been invited to join a group.</summary>
        GroupInvitation = 3,
        /// <summary>Inventory offer</summary>
        InventoryOffered = 4,
        /// <summary>Accepted inventory offer</summary>
        InventoryAccepted = 5,
        /// <summary>Declined inventory offer</summary>
        InventoryDeclined = 6,
        /// <summary>Group vote</summary>
        GroupVote = 7,
        // <summary>A message to everyone in the agent's group, no longer
        // used</summary>
        //[Obsolete]
        //DeprecatedGroupMessage = 8,
        /// <summary>An object is offering its inventory</summary>
        TaskInventoryOffered = 9,
        /// <summary>Accept an inventory offer from an object</summary>
        TaskInventoryAccepted = 10,
        /// <summary>Decline an inventory offer from an object</summary>
        TaskInventoryDeclined = 11,
        /// <summary>Unknown</summary>
        NewUserDefault = 12,
        /// <summary>Start a session, or add users to a session</summary>
        SessionAdd = 13,
        /// <summary>Start a session, but don't prune offline users</summary>
        SessionOfflineAdd = 14,
        /// <summary>Start a session with your group</summary>
        SessionGroupStart = 15,
        /// <summary>Start a session without a calling card (finder or objects)</summary>
        SessionCardlessStart = 16,
        /// <summary>Send a message to a session</summary>
        SessionSend = 17,
        /// <summary>Leave a session</summary>
        SessionDrop = 18,
        /// <summary>Indicates that the IM is from an object</summary>
        MessageFromObject = 19,
        /// <summary>Sent an IM to a busy user, this is the auto response</summary>
        BusyAutoResponse = 20,
        /// <summary>Shows the message in the console and chat history</summary>
        ConsoleAndChatHistory = 21,
        /// <summary>Send a teleport lure</summary>
        RequestTeleport = 22,
        /// <summary>Response sent to the agent which inititiated a teleport invitation</summary>
        AcceptTeleport = 23,
        /// <summary>Response sent to the agent which inititiated a teleport invitation</summary>
        DenyTeleport = 24,
        /// <summary>Only useful if you have Linden permissions</summary>
        GodLikeRequestTeleport = 25,
        /// <summary>Request a teleport lure</summary>
        RequestLure = 26,
        // <summary>Notification of a new group election, this is 
        // deprecated</summary>
        //[Obsolete]
        //DeprecatedGroupElection = 27,
        /// <summary>IM to tell the user to go to an URL</summary>
        GotoUrl = 28,
        /// <summary>IM for help</summary>
        Session911Start = 29,
        /// <summary>IM sent automatically on call for help, sends a lure 
        /// to each Helper reached</summary>
        Lure911 = 30,
        /// <summary>Like an IM but won't go to email</summary>
        FromTaskAsAlert = 31,
        /// <summary>IM from a group officer to all group members</summary>
        GroupNotice = 32,
        /// <summary>Unknown</summary>
        GroupNoticeInventoryAccepted = 33,
        /// <summary>Unknown</summary>
        GroupNoticeInventoryDeclined = 34,
        /// <summary>Accept a group invitation</summary>
        GroupInvitationAccept = 35,
        /// <summary>Decline a group invitation</summary>
        GroupInvitationDecline = 36,
        /// <summary>Unknown</summary>
        GroupNoticeRequested = 37,
        /// <summary>An avatar is offering you friendship</summary>
        FriendshipOffered = 38,
        /// <summary>An avatar has accepted your friendship offer</summary>
        FriendshipAccepted = 39,
        /// <summary>An avatar has declined your friendship offer</summary>
        FriendshipDeclined = 40,
        /// <summary>Indicates that a user has started typing</summary>
        StartTyping = 41,
        /// <summary>Indicates that a user has stopped typing</summary>
        StopTyping = 42
    }

    /// <summary>
    /// Flag in Instant Messages, whether the IM should be delivered to
    /// offline avatars as well
    /// </summary>
    public enum InstantMessageOnline
    {
        /// <summary>Only deliver to online avatars</summary>
        Online = 0,
        /// <summary>If the avatar is offline the message will be held until
        /// they login next, and possibly forwarded to their e-mail account</summary>
        Offline = 1
    }

    /// <summary>
    /// Conversion type to denote Chat Packet types in an easier-to-understand format
    /// </summary>
    public enum ChatType : byte
    {
        /// <summary>Whisper (5m radius)</summary>
        Whisper = 0,
        /// <summary>Normal chat (10/20m radius), what the official viewer typically sends</summary>
        Normal = 1,
        /// <summary>Shouting! (100m radius)</summary>
        Shout = 2,
        // <summary>Say chat (10/20m radius) - The official viewer will 
        // print "[4:15] You say, hey" instead of "[4:15] You: hey"</summary>
        //[Obsolete]
        //Say = 3,
        /// <summary>Event message when an Avatar has begun to type</summary>
        StartTyping = 4,
        /// <summary>Event message when an Avatar has stopped typing</summary>
        StopTyping = 5,
        /// <summary>Send the message to the debug channel</summary>
        Debug = 6,
        /// <summary>Event message when an object uses llOwnerSay</summary>
        OwnerSay = 8,
        /// <summary>Event message when an object uses llRegionSayTo</summary>
        RegionSayTo = 9,
        /// <summary>Special value to support llRegionSay, never sent to the client</summary>
        RegionSay = byte.MaxValue,
    }

    /// <summary>
    /// Identifies the source of a chat message
    /// </summary>
    public enum ChatSourceType : byte
    {
        /// <summary>Chat from the grid or simulator</summary>
        System = 0,
        /// <summary>Chat from another avatar</summary>
        Agent = 1,
        /// <summary>Chat from an object</summary>
        Object = 2
    }

    /// <summary>
    /// 
    /// </summary>
    public enum ChatAudibleLevel : sbyte
    {
        /// <summary></summary>
        Not = -1,
        /// <summary></summary>
        Barely = 0,
        /// <summary></summary>
        Fully = 1
    }

    /// <summary>
    /// Effect type used in ViewerEffect packets
    /// </summary>
    public enum EffectType : byte
    {
        /// <summary></summary>
        Text = 0,
        /// <summary></summary>
        Icon,
        /// <summary></summary>
        Connector,
        /// <summary></summary>
        FlexibleObject,
        /// <summary></summary>
        AnimalControls,
        /// <summary></summary>
        AnimationObject,
        /// <summary></summary>
        Cloth,
        /// <summary>Project a beam from a source to a destination, such as
        /// the one used when editing an object</summary>
        Beam,
        /// <summary></summary>
        Glow,
        /// <summary></summary>
        Point,
        /// <summary></summary>
        Trail,
        /// <summary>Create a swirl of particles around an object</summary>
        Sphere,
        /// <summary></summary>
        Spiral,
        /// <summary></summary>
        Edit,
        /// <summary>Cause an avatar to look at an object</summary>
        LookAt,
        /// <summary>Cause an avatar to point at an object</summary>
        PointAt
    }

    /// <summary>
    /// The action an avatar is doing when looking at something, used in 
    /// ViewerEffect packets for the LookAt effect
    /// </summary>
    public enum LookAtType : byte
    {
        /// <summary></summary>
        None,
        /// <summary></summary>
        Idle,
        /// <summary></summary>
        AutoListen,
        /// <summary></summary>
        FreeLook,
        /// <summary></summary>
        Respond,
        /// <summary></summary>
        Hover,
        /// <summary>Deprecated</summary>
        [Obsolete]
        Conversation,
        /// <summary></summary>
        Select,
        /// <summary></summary>
        Focus,
        /// <summary></summary>
        Mouselook,
        /// <summary></summary>
        Clear
    }

    /// <summary>
    /// The action an avatar is doing when pointing at something, used in
    /// ViewerEffect packets for the PointAt effect
    /// </summary>
    public enum PointAtType : byte
    {
        /// <summary></summary>
        None,
        /// <summary></summary>
        Select,
        /// <summary></summary>
        Grab,
        /// <summary></summary>
        Clear
    }

    /// <summary>
    /// Money transaction types
    /// </summary>
    public enum MoneyTransactionType : int
    {
        /// <summary></summary>
        None = 0,
        /// <summary></summary>
        FailSimulatorTimeout = 1,
        /// <summary></summary>
        FailDataserverTimeout = 2,
        /// <summary></summary>
        ObjectClaim = 1000,
        /// <summary></summary>
        LandClaim = 1001,
        /// <summary></summary>
        GroupCreate = 1002,
        /// <summary></summary>
        ObjectPublicClaim = 1003,
        /// <summary></summary>
        GroupJoin = 1004,
        /// <summary></summary>
        TeleportCharge = 1100,
        /// <summary></summary>
        UploadCharge = 1101,
        /// <summary></summary>
        LandAuction = 1102,
        /// <summary></summary>
        ClassifiedCharge = 1103,
        /// <summary></summary>
        ObjectTax = 2000,
        /// <summary></summary>
        LandTax = 2001,
        /// <summary></summary>
        LightTax = 2002,
        /// <summary></summary>
        ParcelDirFee = 2003,
        /// <summary></summary>
        GroupTax = 2004,
        /// <summary></summary>
        ClassifiedRenew = 2005,
        /// <summary></summary>
        GiveInventory = 3000,
        /// <summary></summary>
        ObjectSale = 5000,
        /// <summary></summary>
        Gift = 5001,
        /// <summary></summary>
        LandSale = 5002,
        /// <summary></summary>
        ReferBonus = 5003,
        /// <summary></summary>
        InventorySale = 5004,
        /// <summary></summary>
        RefundPurchase = 5005,
        /// <summary></summary>
        LandPassSale = 5006,
        /// <summary></summary>
        DwellBonus = 5007,
        /// <summary></summary>
        PayObject = 5008,
        /// <summary></summary>
        ObjectPays = 5009,
        /// <summary></summary>
        GroupLandDeed = 6001,
        /// <summary></summary>
        GroupObjectDeed = 6002,
        /// <summary></summary>
        GroupLiability = 6003,
        /// <summary></summary>
        GroupDividend = 6004,
        /// <summary></summary>
        GroupMembershipDues = 6005,
        /// <summary></summary>
        ObjectRelease = 8000,
        /// <summary></summary>
        LandRelease = 8001,
        /// <summary></summary>
        ObjectDelete = 8002,
        /// <summary></summary>
        ObjectPublicDecay = 8003,
        /// <summary></summary>
        ObjectPublicDelete = 8004,
        /// <summary></summary>
        LindenAdjustment = 9000,
        /// <summary></summary>
        LindenGrant = 9001,
        /// <summary></summary>
        LindenPenalty = 9002,
        /// <summary></summary>
        EventFee = 9003,
        /// <summary></summary>
        EventPrize = 9004,
        /// <summary></summary>
        StipendBasic = 10000,
        /// <summary></summary>
        StipendDeveloper = 10001,
        /// <summary></summary>
        StipendAlways = 10002,
        /// <summary></summary>
        StipendDaily = 10003,
        /// <summary></summary>
        StipendRating = 10004,
        /// <summary></summary>
        StipendDelta = 10005
    }
    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum TransactionFlags : byte
    {
        /// <summary></summary>
        None = 0,
        /// <summary></summary>
        SourceGroup = 1,
        /// <summary></summary>
        DestGroup = 2,
        /// <summary></summary>
        OwnerGroup = 4,
        /// <summary></summary>
        SimultaneousContribution = 8,
        /// <summary></summary>
        ContributionRemoval = 16
    }
    /// <summary>
    /// 
    /// </summary>
    public enum MeanCollisionType : byte
    {
        /// <summary></summary>
        None,
        /// <summary></summary>
        Bump,
        /// <summary></summary>
        LLPushObject,
        /// <summary></summary>
        SelectedObjectCollide,
        /// <summary></summary>
        ScriptedObjectCollide,
        /// <summary></summary>
        PhysicalObjectCollide
    }

    /// <summary>
    /// Flags sent when a script takes or releases a control
    /// </summary>
    /// <remarks>NOTE: (need to verify) These might be a subset of the ControlFlags enum in Movement,</remarks>
    [Flags]
    public enum ScriptControlChange : uint
    {
        /// <summary>No Flags set</summary>
        None = 0,
        /// <summary>Forward (W or up Arrow)</summary>
        Forward = 1,
        /// <summary>Back (S or down arrow)</summary>
        Back = 2,
        /// <summary>Move left (shift+A or left arrow)</summary>
        Left = 4,
        /// <summary>Move right (shift+D or right arrow)</summary>
        Right = 8,
        /// <summary>Up (E or PgUp)</summary>
        Up = 16,
        /// <summary>Down (C or PgDown)</summary>
        Down = 32,
        /// <summary>Rotate left (A or left arrow)</summary>
        RotateLeft = 256,
        /// <summary>Rotate right (D or right arrow)</summary>
        RotateRight = 512,
        /// <summary>Left Mouse Button</summary>
        LeftButton = 268435456,
        /// <summary>Left Mouse button in MouseLook</summary>
        MouseLookLeftButton = 1073741824
    }

    /// <summary>
    /// Currently only used to hide your group title
    /// </summary>
    [Flags]
    public enum AgentFlags : byte
    {
        /// <summary>No flags set</summary>
        None = 0,
        /// <summary>Hide your group title</summary>
        HideTitle = 0x01,
    }

    /// <summary>
    /// Action state of the avatar, which can currently be typing and
    /// editing
    /// </summary>
    [Flags]
    public enum AgentState : byte
    {
        /// <summary></summary>
        None = 0x00,
        /// <summary></summary>
        Typing = 0x04,
        /// <summary></summary>
        Editing = 0x10
    }

    /// <summary>
    /// Current teleport status
    /// </summary>
    public enum TeleportStatus
    {
        /// <summary>Unknown status</summary>
        None,
        /// <summary>Teleport initialized</summary>
        Start,
        /// <summary>Teleport in progress</summary>
        Progress,
        /// <summary>Teleport failed</summary>
        Failed,
        /// <summary>Teleport completed</summary>
        Finished,
        /// <summary>Teleport cancelled</summary>
        Cancelled
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum TeleportFlags : uint
    {
        /// <summary>No flags set, or teleport failed</summary>
        Default = 0,
        /// <summary>Set when newbie leaves help island for first time</summary>
        SetHomeToTarget = 1 << 0,
        /// <summary></summary>
        SetLastToTarget = 1 << 1,
        /// <summary>Via Lure</summary>
        ViaLure = 1 << 2,
        /// <summary>Via Landmark</summary>
        ViaLandmark = 1 << 3,
        /// <summary>Via Location</summary>
        ViaLocation = 1 << 4,
        /// <summary>Via Home</summary>
        ViaHome = 1 << 5,
        /// <summary>Via Telehub</summary>
        ViaTelehub = 1 << 6,
        /// <summary>Via Login</summary>
        ViaLogin = 1 << 7,
        /// <summary>Linden Summoned</summary>
        ViaGodlikeLure = 1 << 8,
        /// <summary>Linden Forced me</summary>
        Godlike = 1 << 9,
        /// <summary></summary>
        NineOneOne = 1 << 10,
        /// <summary>Agent Teleported Home via Script</summary>
        DisableCancel = 1 << 11,
        /// <summary></summary>
        ViaRegionID = 1 << 12,
        /// <summary></summary>
        IsFlying = 1 << 13,
        /// <summary></summary>
        ResetHome = 1 << 14,
        /// <summary>forced to new location for example when avatar is banned or ejected</summary>
        ForceRedirect = 1 << 15,
        /// <summary>Teleport Finished via a Lure</summary>
        FinishedViaLure = 1 << 26,
        /// <summary>Finished, Sim Changed</summary>
        FinishedViaNewSim = 1 << 28,
        /// <summary>Finished, Same Sim</summary>
        FinishedViaSameSim = 1 << 29
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum TeleportLureFlags
    {
        /// <summary></summary>
        NormalLure = 0,
        /// <summary></summary>
        GodlikeLure = 1,
        /// <summary></summary>
        GodlikePursuit = 2
    }

    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum ScriptSensorTypeFlags
    {
        /// <summary></summary>
        Agent = 1,
        /// <summary></summary>
        Active = 2,
        /// <summary></summary>
        Passive = 4,
        /// <summary></summary>
        Scripted = 8,
    }

    /// <summary>
    /// Type of mute entry
    /// </summary>
    public enum MuteType
    {
        /// <summary>Object muted by name</summary>
        ByName = 0,
        /// <summary>Muted residet</summary>
        Resident = 1,
        /// <summary>Object muted by UUID</summary>
        Object = 2,
        /// <summary>Muted group</summary>
        Group = 3,
        /// <summary>Muted external entry</summary>
        External = 4
    }

    /// <summary>
    /// Flags of mute entry
    /// </summary>
    [Flags]
    public enum MuteFlags : int
    {
        /// <summary>No exceptions</summary>
        Default = 0x0,
        /// <summary>Don't mute text chat</summary>
        TextChat = 0x1,
        /// <summary>Don't mute voice chat</summary>
        VoiceChat = 0x2,
        /// <summary>Don't mute particles</summary>
        Particles = 0x4,
        /// <summary>Don't mute sounds</summary>
        ObjectSounds = 0x8,
        /// <summary>Don't mute</summary>
        All = 0xf
    }
    #endregion Enums

    #region Structs

    /// <summary>
    /// Instant Message
    /// </summary>
    public struct InstantMessage
    {
        /// <summary>Key of sender</summary>
        public UUID FromAgentID;
        /// <summary>Name of sender</summary>
        public string FromAgentName;
        /// <summary>Key of destination avatar</summary>
        public UUID ToAgentID;
        /// <summary>ID of originating estate</summary>
        public uint ParentEstateID;
        /// <summary>Key of originating region</summary>
        public UUID RegionID;
        /// <summary>Coordinates in originating region</summary>
        public Vector3 Position;
        /// <summary>Instant message type</summary>
        public InstantMessageDialog Dialog;
        /// <summary>Group IM session toggle</summary>
        public bool GroupIM;
        /// <summary>Key of IM session, for Group Messages, the groups UUID</summary>
        public UUID IMSessionID;
        /// <summary>Timestamp of the instant message</summary>
        public DateTime Timestamp;
        /// <summary>Instant message text</summary>
        public string Message;
        /// <summary>Whether this message is held for offline avatars</summary>
        public InstantMessageOnline Offline;
        /// <summary>Context specific packed data</summary>
        public byte[] BinaryBucket;

        /// <summary>Print the struct data as a string</summary>
        /// <returns>A string containing the field name, and field value</returns>
        public override string ToString()
        {
            return Helpers.StructToString(this);
        }
    }

    /// <summary>Represents muted object or resident</summary>
    public class MuteEntry
    {
        /// <summary>Type of the mute entry</summary>
        public MuteType Type;
        /// <summary>UUID of the mute etnry</summary>
        public UUID ID;
        /// <summary>Mute entry name</summary>
        public string Name;
        /// <summary>Mute flags</summary>
        public MuteFlags Flags;
    }

    /// <summary>Transaction detail sent with MoneyBalanceReply message</summary>
    public class TransactionInfo
    {
        /// <summary>Type of the transaction</summary>
        public int TransactionType; // FIXME: this should be an enum
        /// <summary>UUID of the transaction source</summary>
        public UUID SourceID;
        /// <summary>Is the transaction source a group</summary>
        public bool IsSourceGroup;
        /// <summary>UUID of the transaction destination</summary>
        public UUID DestID;
        /// <summary>Is transaction destination a group</summary>
        public bool IsDestGroup;
        /// <summary>Transaction amount</summary>
        public int Amount;
        /// <summary>Transaction description</summary>
        public string ItemDescription;
    }
    #endregion Structs

    /// <summary>
    /// Manager class for our own avatar
    /// </summary>
    public partial class AgentManager
    {
        #region Delegates
        /// <summary>
        /// Called once attachment resource usage information has been collected
        /// </summary>
        /// <param name="success">Indicates if operation was successfull</param>
        /// <param name="info">Attachment resource usage information</param>
        public delegate void AttachmentResourcesCallback(bool success, AttachmentResourcesMessage info);
        #endregion Delegates

        #region Event Delegates

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ChatEventArgs> m_Chat;

        /// <summary>Raises the ChatFromSimulator event</summary>
        /// <param name="e">A ChatEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnChat(ChatEventArgs e)
        {
            EventHandler<ChatEventArgs> handler = m_Chat;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ChatLock = new object();

        /// <summary>Raised when a scripted object or agent within range sends a public message</summary>
        public event EventHandler<ChatEventArgs> ChatFromSimulator
        {
            add { lock (m_ChatLock) { m_Chat += value; } }
            remove { lock (m_ChatLock) { m_Chat -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ScriptDialogEventArgs> m_ScriptDialog;

        /// <summary>Raises the ScriptDialog event</summary>
        /// <param name="e">A SctriptDialogEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnScriptDialog(ScriptDialogEventArgs e)
        {
            EventHandler<ScriptDialogEventArgs> handler = m_ScriptDialog;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptDialogLock = new object();
        /// <summary>Raised when a scripted object sends a dialog box containing possible
        /// options an agent can respond to</summary>
        public event EventHandler<ScriptDialogEventArgs> ScriptDialog
        {
            add { lock (m_ScriptDialogLock) { m_ScriptDialog += value; } }
            remove { lock (m_ScriptDialogLock) { m_ScriptDialog -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ScriptQuestionEventArgs> m_ScriptQuestion;

        /// <summary>Raises the ScriptQuestion event</summary>
        /// <param name="e">A ScriptQuestionEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnScriptQuestion(ScriptQuestionEventArgs e)
        {
            EventHandler<ScriptQuestionEventArgs> handler = m_ScriptQuestion;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptQuestionLock = new object();
        /// <summary>Raised when an object requests a change in the permissions an agent has permitted</summary>
        public event EventHandler<ScriptQuestionEventArgs> ScriptQuestion
        {
            add { lock (m_ScriptQuestionLock) { m_ScriptQuestion += value; } }
            remove { lock (m_ScriptQuestionLock) { m_ScriptQuestion -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<LoadUrlEventArgs> m_LoadURL;

        /// <summary>Raises the LoadURL event</summary>
        /// <param name="e">A LoadUrlEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnLoadURL(LoadUrlEventArgs e)
        {
            EventHandler<LoadUrlEventArgs> handler = m_LoadURL;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_LoadUrlLock = new object();
        /// <summary>Raised when a script requests an agent open the specified URL</summary>
        public event EventHandler<LoadUrlEventArgs> LoadURL
        {
            add { lock (m_LoadUrlLock) { m_LoadURL += value; } }
            remove { lock (m_LoadUrlLock) { m_LoadURL -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<BalanceEventArgs> m_Balance;

        /// <summary>Raises the MoneyBalance event</summary>
        /// <param name="e">A BalanceEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnBalance(BalanceEventArgs e)
        {
            EventHandler<BalanceEventArgs> handler = m_Balance;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_BalanceLock = new object();

        /// <summary>Raised when an agents currency balance is updated</summary>
        public event EventHandler<BalanceEventArgs> MoneyBalance
        {
            add { lock (m_BalanceLock) { m_Balance += value; } }
            remove { lock (m_BalanceLock) { m_Balance -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<MoneyBalanceReplyEventArgs> m_MoneyBalance;

        /// <summary>Raises the MoneyBalanceReply event</summary>
        /// <param name="e">A MoneyBalanceReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnMoneyBalanceReply(MoneyBalanceReplyEventArgs e)
        {
            EventHandler<MoneyBalanceReplyEventArgs> handler = m_MoneyBalance;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_MoneyBalanceReplyLock = new object();

        /// <summary>Raised when a transaction occurs involving currency such as a land purchase</summary>
        public event EventHandler<MoneyBalanceReplyEventArgs> MoneyBalanceReply
        {
            add { lock (m_MoneyBalanceReplyLock) { m_MoneyBalance += value; } }
            remove { lock (m_MoneyBalanceReplyLock) { m_MoneyBalance -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<InstantMessageEventArgs> m_InstantMessage;

        /// <summary>Raises the IM event</summary>
        /// <param name="e">A InstantMessageEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnInstantMessage(InstantMessageEventArgs e)
        {
            EventHandler<InstantMessageEventArgs> handler = m_InstantMessage;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_InstantMessageLock = new object();
        /// <summary>Raised when an ImprovedInstantMessage packet is recieved from the simulator, this is used for everything from
        /// private messaging to friendship offers. The Dialog field defines what type of message has arrived</summary>
        public event EventHandler<InstantMessageEventArgs> IM
        {
            add { lock (m_InstantMessageLock) { m_InstantMessage += value; } }
            remove { lock (m_InstantMessageLock) { m_InstantMessage -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<TeleportEventArgs> m_Teleport;

        /// <summary>Raises the TeleportProgress event</summary>
        /// <param name="e">A TeleportEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnTeleport(TeleportEventArgs e)
        {
            EventHandler<TeleportEventArgs> handler = m_Teleport;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_TeleportLock = new object();
        /// <summary>Raised when an agent has requested a teleport to another location, or when responding to a lure. Raised multiple times
        /// for each teleport indicating the progress of the request</summary>
        public event EventHandler<TeleportEventArgs> TeleportProgress
        {
            add { lock (m_TeleportLock) { m_Teleport += value; } }
            remove { lock (m_TeleportLock) { m_Teleport += value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<AgentDataReplyEventArgs> m_AgentData;

        /// <summary>Raises the AgentDataReply event</summary>
        /// <param name="e">A AgentDataReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAgentData(AgentDataReplyEventArgs e)
        {
            EventHandler<AgentDataReplyEventArgs> handler = m_AgentData;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AgentDataLock = new object();

        /// <summary>Raised when a simulator sends agent specific information for our avatar.</summary>
        public event EventHandler<AgentDataReplyEventArgs> AgentDataReply
        {
            add { lock (m_AgentDataLock) { m_AgentData += value; } }
            remove { lock (m_AgentDataLock) { m_AgentData -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<AnimationsChangedEventArgs> m_AnimationsChanged;

        /// <summary>Raises the AnimationsChanged event</summary>
        /// <param name="e">A AnimationsChangedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAnimationsChanged(AnimationsChangedEventArgs e)
        {
            EventHandler<AnimationsChangedEventArgs> handler = m_AnimationsChanged;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AnimationsChangedLock = new object();

        /// <summary>Raised when our agents animation playlist changes</summary>
        public event EventHandler<AnimationsChangedEventArgs> AnimationsChanged
        {
            add { lock (m_AnimationsChangedLock) { m_AnimationsChanged += value; } }
            remove { lock (m_AnimationsChangedLock) { m_AnimationsChanged -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<MeanCollisionEventArgs> m_MeanCollision;

        /// <summary>Raises the MeanCollision event</summary>
        /// <param name="e">A MeanCollisionEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnMeanCollision(MeanCollisionEventArgs e)
        {
            EventHandler<MeanCollisionEventArgs> handler = m_MeanCollision;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_MeanCollisionLock = new object();

        /// <summary>Raised when an object or avatar forcefully collides with our agent</summary>
        public event EventHandler<MeanCollisionEventArgs> MeanCollision
        {
            add { lock (m_MeanCollisionLock) { m_MeanCollision += value; } }
            remove { lock (m_MeanCollisionLock) { m_MeanCollision -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<RegionCrossedEventArgs> m_RegionCrossed;

        /// <summary>Raises the RegionCrossed event</summary>
        /// <param name="e">A RegionCrossedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnRegionCrossed(RegionCrossedEventArgs e)
        {
            EventHandler<RegionCrossedEventArgs> handler = m_RegionCrossed;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionCrossedLock = new object();

        /// <summary>Raised when our agent crosses a region border into another region</summary>
        public event EventHandler<RegionCrossedEventArgs> RegionCrossed
        {
            add { lock (m_RegionCrossedLock) { m_RegionCrossed += value; } }
            remove { lock (m_RegionCrossedLock) { m_RegionCrossed -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<GroupChatJoinedEventArgs> m_GroupChatJoined;

        /// <summary>Raises the GroupChatJoined event</summary>
        /// <param name="e">A GroupChatJoinedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnGroupChatJoined(GroupChatJoinedEventArgs e)
        {
            EventHandler<GroupChatJoinedEventArgs> handler = m_GroupChatJoined;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupChatJoinedLock = new object();

        /// <summary>Raised when our agent succeeds or fails to join a group chat session</summary>
        public event EventHandler<GroupChatJoinedEventArgs> GroupChatJoined
        {
            add { lock (m_GroupChatJoinedLock) { m_GroupChatJoined += value; } }
            remove { lock (m_GroupChatJoinedLock) { m_GroupChatJoined -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AlertMessageEventArgs> m_AlertMessage;

        /// <summary>Raises the AlertMessage event</summary>
        /// <param name="e">A AlertMessageEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAlertMessage(AlertMessageEventArgs e)
        {
            EventHandler<AlertMessageEventArgs> handler = m_AlertMessage;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AlertMessageLock = new object();

        /// <summary>Raised when a simulator sends an urgent message usually indication the recent failure of
        /// another action we have attempted to take such as an attempt to enter a parcel where we are denied access</summary>
        public event EventHandler<AlertMessageEventArgs> AlertMessage
        {
            add { lock (m_AlertMessageLock) { m_AlertMessage += value; } }
            remove { lock (m_AlertMessageLock) { m_AlertMessage -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<RegionRestartAlertMessageEventArgs> m_RegionRestartAlertMessage;

        /// <summary>Raises the RegionRestartAlertMessage event</summary>
        /// <param name="e">A RegionRestartAlertMessageEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnRegionRestartAlertMessage(RegionRestartAlertMessageEventArgs e)
        {
            EventHandler<RegionRestartAlertMessageEventArgs> handler = m_RegionRestartAlertMessage;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_RegionRestartAlertMessageLock = new object();

        /// <summary>Raised when simulator sends an imminent restart alert for the current region</summary>
        public event EventHandler<RegionRestartAlertMessageEventArgs> RegionRestartAlertMessage
        {
            add { lock (m_RegionRestartAlertMessageLock) { m_RegionRestartAlertMessage += value; } }
            remove { lock (m_RegionRestartAlertMessageLock) { m_RegionRestartAlertMessage -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ScriptControlEventArgs> m_ScriptControl;

        /// <summary>Raises the ScriptControlChange event</summary>
        /// <param name="e">A ScriptControlEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnScriptControlChange(ScriptControlEventArgs e)
        {
            EventHandler<ScriptControlEventArgs> handler = m_ScriptControl;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptControlLock = new object();

        /// <summary>Raised when a script attempts to take or release specified controls for our agent</summary>
        public event EventHandler<ScriptControlEventArgs> ScriptControlChange
        {
            add { lock (m_ScriptControlLock) { m_ScriptControl += value; } }
            remove { lock (m_ScriptControlLock) { m_ScriptControl -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<CameraConstraintEventArgs> m_CameraConstraint;

        /// <summary>Raises the CameraConstraint event</summary>
        /// <param name="e">A CameraConstraintEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnCameraConstraint(CameraConstraintEventArgs e)
        {
            EventHandler<CameraConstraintEventArgs> handler = m_CameraConstraint;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_CameraConstraintLock = new object();

        /// <summary>Raised when the simulator detects our agent is trying to view something
        /// beyond its limits</summary>
        public event EventHandler<CameraConstraintEventArgs> CameraConstraint
        {
            add { lock (m_CameraConstraintLock) { m_CameraConstraint += value; } }
            remove { lock (m_CameraConstraintLock) { m_CameraConstraint -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ScriptSensorReplyEventArgs> m_ScriptSensorReply;

        /// <summary>Raises the ScriptSensorReply event</summary>
        /// <param name="e">A ScriptSensorReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnScriptSensorReply(ScriptSensorReplyEventArgs e)
        {
            EventHandler<ScriptSensorReplyEventArgs> handler = m_ScriptSensorReply;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptSensorReplyLock = new object();

        /// <summary>Raised when a script sensor reply is received from a simulator</summary>
        public event EventHandler<ScriptSensorReplyEventArgs> ScriptSensorReply
        {
            add { lock (m_ScriptSensorReplyLock) { m_ScriptSensorReply += value; } }
            remove { lock (m_ScriptSensorReplyLock) { m_ScriptSensorReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<AvatarSitResponseEventArgs> m_AvatarSitResponse;

        /// <summary>Raises the AvatarSitResponse event</summary>
        /// <param name="e">A AvatarSitResponseEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAvatarSitResponse(AvatarSitResponseEventArgs e)
        {
            EventHandler<AvatarSitResponseEventArgs> handler = m_AvatarSitResponse;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarSitResponseLock = new object();

        /// <summary>Raised in response to a <see cref="RequestSit"/> request</summary>
        public event EventHandler<AvatarSitResponseEventArgs> AvatarSitResponse
        {
            add { lock (m_AvatarSitResponseLock) { m_AvatarSitResponse += value; } }
            remove { lock (m_AvatarSitResponseLock) { m_AvatarSitResponse -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ChatSessionMemberAddedEventArgs> m_ChatSessionMemberAdded;

        /// <summary>Raises the ChatSessionMemberAdded event</summary>
        /// <param name="e">A ChatSessionMemberAddedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnChatSessionMemberAdded(ChatSessionMemberAddedEventArgs e)
        {
            EventHandler<ChatSessionMemberAddedEventArgs> handler = m_ChatSessionMemberAdded;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ChatSessionMemberAddedLock = new object();

        /// <summary>Raised when an avatar enters a group chat session we are participating in</summary>
        public event EventHandler<ChatSessionMemberAddedEventArgs> ChatSessionMemberAdded
        {
            add { lock (m_ChatSessionMemberAddedLock) { m_ChatSessionMemberAdded += value; } }
            remove { lock (m_ChatSessionMemberAddedLock) { m_ChatSessionMemberAdded -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<ChatSessionMemberLeftEventArgs> m_ChatSessionMemberLeft;

        /// <summary>Raises the ChatSessionMemberLeft event</summary>
        /// <param name="e">A ChatSessionMemberLeftEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnChatSessionMemberLeft(ChatSessionMemberLeftEventArgs e)
        {
            EventHandler<ChatSessionMemberLeftEventArgs> handler = m_ChatSessionMemberLeft;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ChatSessionMemberLeftLock = new object();

        /// <summary>Raised when an agent exits a group chat session we are participating in</summary>
        public event EventHandler<ChatSessionMemberLeftEventArgs> ChatSessionMemberLeft
        {
            add { lock (m_ChatSessionMemberLeftLock) { m_ChatSessionMemberLeft += value; } }
            remove { lock (m_ChatSessionMemberLeftLock) { m_ChatSessionMemberLeft -= value; } }
        }

        /// <summary>The event subscribers, null of no subscribers</summary>
        private EventHandler<SetDisplayNameReplyEventArgs> m_SetDisplayNameReply;

        ///<summary>Raises the SetDisplayNameReply Event</summary>
        /// <param name="e">A SetDisplayNameReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnSetDisplayNameReply(SetDisplayNameReplyEventArgs e)
        {
            EventHandler<SetDisplayNameReplyEventArgs> handler = m_SetDisplayNameReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_SetDisplayNameReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the details of display name change</summary>
        public event EventHandler<SetDisplayNameReplyEventArgs> SetDisplayNameReply
        {
            add { lock (m_SetDisplayNameReplyLock) { m_SetDisplayNameReply += value; } }
            remove { lock (m_SetDisplayNameReplyLock) { m_SetDisplayNameReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<EventArgs> m_MuteListUpdated;

        /// <summary>Raises the MuteListUpdated event</summary>
        /// <param name="e">A EventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnMuteListUpdated(EventArgs e)
        {
            EventHandler<EventArgs> handler = m_MuteListUpdated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_MuteListUpdatedLock = new object();

        /// <summary>Raised when a scripted object or agent within range sends a public message</summary>
        public event EventHandler<EventArgs> MuteListUpdated
        {
            add { lock (m_MuteListUpdatedLock) { m_MuteListUpdated += value; } }
            remove { lock (m_MuteListUpdatedLock) { m_MuteListUpdated -= value; } }
        }
        #endregion Callbacks

        /// <summary>Reference to the GridClient instance</summary>
        private readonly GridClient Client;
        /// <summary>Used for movement and camera tracking</summary>
        public readonly AgentMovement Movement;
        /// <summary>Currently playing animations for the agent. Can be used to
        /// check the current movement status such as walking, hovering, aiming,
        /// etc. by checking against system animations found in the Animations class</summary>
        public InternalDictionary<UUID, int> SignaledAnimations = new InternalDictionary<UUID, int>();
        /// <summary>Dictionary containing current Group Chat sessions and members</summary>
        public InternalDictionary<UUID, List<ChatSessionMember>> GroupChatSessions = new InternalDictionary<UUID, List<ChatSessionMember>>();
        /// <summary>Dictionary containing mute list keyead on mute name and key</summary>
        public InternalDictionary<string, MuteEntry> MuteList = new InternalDictionary<string, MuteEntry>();
        public InternalDictionary<UUID, UUID> ActiveGestures { get; } = new InternalDictionary<UUID, UUID>();

        #region Properties

        /// <summary>Your (client) avatars <see cref="UUID"/></summary>
        /// <remarks>"client", "agent", and "avatar" all represent the same thing</remarks>
        public UUID AgentID => id;

        /// <summary>Temporary <seealso cref="UUID"/> assigned to this session, used for 
        /// verifying our identity in packets</summary>
        public UUID SessionID => sessionID;

        /// <summary>Shared secret <seealso cref="UUID"/> that is never sent over the wire</summary>
        public UUID SecureSessionID => secureSessionID;

        /// <summary>Your (client) avatar ID, local to the current region/sim</summary>
        public uint LocalID => localID;

        /// <summary>Where the avatar started at login. Can be "last", "home" 
        /// or a login <seealso cref="T:OpenMetaverse.URI"/></summary>
        public string StartLocation => startLocation;

        /// <summary>The access level of this agent, usually M, PG or A</summary>
        public string AgentAccess => agentAccess;

        /// <summary>The CollisionPlane of Agent</summary>
        public Vector4 CollisionPlane => collisionPlane;

        /// <summary>An <seealso cref="Vector3"/> representing the velocity of our agent</summary>
        public Vector3 Velocity => velocity;

        /// <summary>An <seealso cref="Vector3"/> representing the acceleration of our agent</summary>
        public Vector3 Acceleration => acceleration;

        /// <summary>A <seealso cref="Vector3"/> which specifies the angular speed, and axis about which an Avatar is rotating.</summary>
        public Vector3 AngularVelocity => angularVelocity;

        /// <summary>Position avatar client will goto when login to 'home' or during
        /// teleport request to 'home' region.</summary>
        public Vector3 HomePosition => homePosition;

        /// <summary>LookAt point saved/restored with HomePosition</summary>
        public Vector3 HomeLookAt => homeLookAt;

        /// <summary>Avatar First Name (i.e. Philip)</summary>
        public string FirstName => firstName;

        /// <summary>Avatar Last Name (i.e. Linden)</summary>
        public string LastName => lastName;

        /// <summary>LookAt point received with the login response message</summary>
        public Vector3 LookAt => lookAt;

        /// <summary>Avatar Full Name (i.e. Philip Linden)</summary>
        public string Name
        {
            get
            {
                // This is a fairly common request, so assume the name doesn't
                // change mid-session and cache the result
                if (fullName == null || fullName.Length < 2)
                    fullName = $"{firstName} {lastName}";
                return fullName;
            }
        }
        /// <summary>Gets the health of the agent</summary>
        public float Health => health;

        /// <summary>Gets the current balance of the agent</summary>
        public int Balance => balance;

        /// <summary>Gets the local ID of the prim the agent is sitting on,
        /// zero if the avatar is not currently sitting</summary>
        public uint SittingOn => sittingOn;

        /// <summary>Gets the <seealso cref="UUID"/> of the agents active group.</summary>
        public UUID ActiveGroup => activeGroup;

        /// <summary>Gets the Agents powers in the currently active group</summary>
        public GroupPowers ActiveGroupPowers => activeGroupPowers;

        /// <summary>Current status message for teleporting</summary>
        public string TeleportMessage => teleportMessage;

        /// <summary>Current position of the agent as a relative offset from
        /// the simulator, or the parent object if we are sitting on something</summary>
        public Vector3 RelativePosition { get { return relativePosition; } set { relativePosition = value; } }
        /// <summary>Current rotation of the agent as a relative rotation from
        /// the simulator, or the parent object if we are sitting on something</summary>
        public Quaternion RelativeRotation { get { return relativeRotation; } set { relativeRotation = value; } }
        /// <summary>Current position of the agent in the simulator</summary>
        public Vector3 SimPosition
        {
            get
            {
                // simple case, agent not seated
                if (sittingOn == 0)
                {
                    return relativePosition;
                }

                // a bit more complicatated, agent sitting on a prim
                Primitive p;
                Vector3 fullPosition = relativePosition;

                if (Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(sittingOn, out p))
                {
                    fullPosition = p.Position + relativePosition * p.Rotation;
                }

                // go up the hiearchy trying to find the root prim
                while (p != null && p.ParentID != 0)
                {
                    Avatar av;
                    if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(p.ParentID, out av))
                    {
                        p = av;
                        fullPosition += p.Position;
                    }
                    else
                    {
                        if (Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(p.ParentID, out p))
                        {
                            fullPosition += p.Position;
                        }
                    }
                }

                if (p != null) // we found the root prim
                {
                    return fullPosition;
                }

                // Didn't find the seat's root prim, try returning coarse loaction
                if (Client.Network.CurrentSim.avatarPositions.TryGetValue(AgentID, out fullPosition))
                {
                    return fullPosition;
                }

                Logger.Log("Failed to determine agents sim position", Helpers.LogLevel.Warning, Client);
                return relativePosition;
            }
        }
        /// <summary>
        /// A <seealso cref="Quaternion"/> representing the agents current rotation
        /// </summary>
        public Quaternion SimRotation
        {
            get
            {
                if (sittingOn != 0)
                {
                    Primitive parent;
                    if (Client.Network.CurrentSim != null && Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(sittingOn, out parent))
                    {
                        return relativeRotation * parent.Rotation;
                    }
                    Logger.Log(
                        $"Currently sitting on object {sittingOn} which is not tracked, SimRotation will be inaccurate",
                        Helpers.LogLevel.Warning, Client);
                    return relativeRotation;
                }
                return relativeRotation;
            }
        }
        /// <summary>Returns the global grid position of the avatar</summary>
        public Vector3d GlobalPosition
        {
            get
            {
                if (Client.Network.CurrentSim != null)
                {
                    uint globalX, globalY;
                    Utils.LongToUInts(Client.Network.CurrentSim.Handle, out globalX, out globalY);
                    Vector3 pos = SimPosition;

                    return new Vector3d(
                        globalX + pos.X,
                        globalY + pos.Y,
                        pos.Z);
                }
                return Vector3d.Zero;
            }
        }

        /// <summary>Various abilities and preferences sent by the grid</summary>
        public AgentStateUpdateMessage AgentStateStatus;

        #endregion Properties

        internal uint localID;
        internal Vector3 relativePosition;
        internal Quaternion relativeRotation = Quaternion.Identity;
        internal Vector4 collisionPlane;
        internal Vector3 velocity;
        internal Vector3 acceleration;
        internal Vector3 angularVelocity;
        internal uint sittingOn;
        internal int lastInterpolation;

        #region Private Members

        private UUID id;
        private UUID sessionID;
        private UUID secureSessionID;
        private string startLocation = string.Empty;
        private string agentAccess = string.Empty;
        private Vector3 homePosition;
        private Vector3 homeLookAt;
        private Vector3 lookAt;
        private string firstName = string.Empty;
        private string lastName = string.Empty;
        private string fullName;
        private string teleportMessage = string.Empty;
        private TeleportStatus teleportStat = TeleportStatus.None;
        private ManualResetEvent teleportEvent = new ManualResetEvent(false);
        private uint heightWidthGenCounter;
        private float health;
        private int balance;
        private UUID activeGroup;
        private GroupPowers activeGroupPowers;
        private Dictionary<UUID, AssetGesture> gestureCache = new Dictionary<UUID, AssetGesture>();
        #endregion Private Members

        /// <summary>
        /// Constructor, setup callbacks for packets related to our avatar
        /// </summary>
        /// <param name="client">A reference to the <seealso cref="T:OpenMetaverse.GridClient"/> Class</param>
        public AgentManager(GridClient client)
        {
            Client = client;

            Movement = new AgentMovement(Client);

            Client.Network.Disconnected += Network_OnDisconnected;

            // Teleport callbacks            
            Client.Network.RegisterCallback(PacketType.TeleportStart, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportProgress, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportFailed, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportCancel, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportLocal, TeleportHandler);
            // these come in via the EventQueue
            Client.Network.RegisterEventCallback("TeleportFailed", new Caps.EventQueueCallback(TeleportFailedEventHandler));
            Client.Network.RegisterEventCallback("TeleportFinish", new Caps.EventQueueCallback(TeleportFinishEventHandler));

            // Instant message callback
            Client.Network.RegisterCallback(PacketType.ImprovedInstantMessage, InstantMessageHandler);
            // Chat callback
            Client.Network.RegisterCallback(PacketType.ChatFromSimulator, ChatHandler);
            // Script dialog callback
            Client.Network.RegisterCallback(PacketType.ScriptDialog, ScriptDialogHandler);
            // Script question callback
            Client.Network.RegisterCallback(PacketType.ScriptQuestion, ScriptQuestionHandler);
            // Script URL callback
            Client.Network.RegisterCallback(PacketType.LoadURL, LoadURLHandler);
            // Movement complete callback
            Client.Network.RegisterCallback(PacketType.AgentMovementComplete, MovementCompleteHandler);
            // Health callback
            Client.Network.RegisterCallback(PacketType.HealthMessage, HealthHandler);
            // Money callback
            Client.Network.RegisterCallback(PacketType.MoneyBalanceReply, MoneyBalanceReplyHandler);
            //Agent update callback
            Client.Network.RegisterCallback(PacketType.AgentDataUpdate, AgentDataUpdateHandler);
            // Animation callback
            Client.Network.RegisterCallback(PacketType.AvatarAnimation, AvatarAnimationHandler, false);
            // Object colliding into our agent callback
            Client.Network.RegisterCallback(PacketType.MeanCollisionAlert, MeanCollisionAlertHandler);
            // Region Crossing
            Client.Network.RegisterCallback(PacketType.CrossedRegion, CrossedRegionHandler);
            Client.Network.RegisterEventCallback("CrossedRegion", CrossedRegionEventHandler);
            // CAPS callbacks
            Client.Network.RegisterEventCallback("EstablishAgentCommunication", EstablishAgentCommunicationEventHandler);
            Client.Network.RegisterEventCallback("SetDisplayNameReply", SetDisplayNameReplyEventHandler);
            Client.Network.RegisterEventCallback("AgentStateUpdate", AgentStateUpdateEventHandler);
            // Incoming Group Chat
            Client.Network.RegisterEventCallback("ChatterBoxInvitation", ChatterBoxInvitationEventHandler);
            // Outgoing Group Chat Reply
            Client.Network.RegisterEventCallback("ChatterBoxSessionEventReply", ChatterBoxSessionEventReplyEventHandler);
            Client.Network.RegisterEventCallback("ChatterBoxSessionStartReply", ChatterBoxSessionStartReplyEventHandler);
            Client.Network.RegisterEventCallback("ChatterBoxSessionAgentListUpdates", ChatterBoxSessionAgentListUpdatesEventHandler);
            // Login
            Client.Network.RegisterLoginResponseCallback(Network_OnLoginResponse);
            // Alert Messages
            Client.Network.RegisterCallback(PacketType.AlertMessage, AlertMessageHandler);
            Client.Network.RegisterCallback(PacketType.AgentAlertMessage, AgentAlertMessageHandler);
            // script control change messages, ie: when an in-world LSL script wants to take control of your agent.
            Client.Network.RegisterCallback(PacketType.ScriptControlChange, ScriptControlChangeHandler);
            // Camera Constraint (probably needs to move to AgentManagerCamera TODO:
            Client.Network.RegisterCallback(PacketType.CameraConstraint, CameraConstraintHandler);
            Client.Network.RegisterCallback(PacketType.ScriptSensorReply, ScriptSensorReplyHandler);
            Client.Network.RegisterCallback(PacketType.AvatarSitResponse, AvatarSitResponseHandler);
            // Process mute list update message
            Client.Network.RegisterCallback(PacketType.MuteListUpdate, MuteListUpdateHander);
        }

        #region Chat and instant messages

        protected int message_chunk_group_id = 0; // a int that starts at 1 goes upto 500 (then back to 1)

        /// <summary>
        /// Send a text message from the Agent to the Simulator
        /// </summary>
        /// <param name="message">A <see cref="string"/> containing the message</param>
        /// <param name="channel">The channel to send the message on, 0 is the public channel. Channels above 0
        /// can be used however only scripts listening on the specified channel will see the message</param>
        /// <param name="type">Denotes the type of message being sent, shout, whisper, etc.</param>
        /// <param name="allow_split_message">Enables large messages to be split into chunks of 900 with [CHUNKGROUPID|CHUNKID|TOTALCHUNKS] at the start</param>
        /// <param name="hide_chunk_grouping">Hides [CHUNKGROUPID|CHUNKID|TOTALCHUNKS] at the start of chunked messages</param>
        public void Chat(string message, int channel, ChatType type,bool allow_split_message=true,bool hide_chunk_grouping=true)
        {
            if ((message.Length > 900) && (allow_split_message == true))
            {
                int group_id = message_chunk_group_id;
                message_chunk_group_id++;
                if (message_chunk_group_id > 500) message_chunk_group_id = 1;
                string[] chunks = message.SplitBy(900).ToArray();
                int chunkid = 1;
                foreach(string C in chunks)
                {
                    string chunk_grouping = "";
                    if(hide_chunk_grouping == false)
                    {
                        chunk_grouping = "[" + group_id.ToString() + "|" + chunkid.ToString() + "|"+chunks.Length.ToString()+"]";
                    }
                    Chat(""+ chunk_grouping+"" + C + "", channel, type, false);
                    chunkid++;
                }
            }
            else if ((message.Length > 0) && (message.Length < 1000))
            {
                ChatFromViewerPacket chat = new ChatFromViewerPacket
                {
                    AgentData =
                        {
                            AgentID = id,
                            SessionID = Client.Self.SessionID
                        },
                    ChatData =
                        {
                            Channel = channel,
                            Message = Utils.StringToBytes(message),
                            Type = (byte) type
                        }
                };
                Client.Network.SendPacket(chat);
            }
        }

        /// <summary>
        /// Request any instant messages sent while the client was offline to be resent.
        /// </summary>
        public void RetrieveInstantMessages()
        {
            RetrieveInstantMessagesPacket p = new RetrieveInstantMessagesPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Send an Instant Message to another Avatar
        /// </summary>
        /// <param name="target">The recipients <see cref="UUID"/></param>
        /// <param name="message">A <see cref="string"/> containing the message to send</param>
        public void InstantMessage(UUID target, string message)
        {
            InstantMessage(Name, target, message, AgentID.Equals(target) ? AgentID : target ^ AgentID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

        /// <summary>
        /// Send an Instant Message to an existing group chat or conference chat
        /// </summary>
        /// <param name="target">The recipients <see cref="UUID"/></param>
        /// <param name="message">A <see cref="string"/> containing the message to send</param>
        /// <param name="imSessionID">IM session ID (to differentiate between IM windows)</param>
        public void InstantMessage(UUID target, string message, UUID imSessionID)
        {
            InstantMessage(Name, target, message, imSessionID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

        /// <summary>
        /// Send an Instant Message
        /// </summary>
        /// <param name="fromName">The name this IM will show up as being from</param>
        /// <param name="target">Key of Avatar</param>
        /// <param name="message">Text message being sent</param>
        /// <param name="imSessionID">IM session ID (to differentiate between IM windows)</param>
        /// <param name="conferenceIDs">IDs of sessions for a conference</param>
        public void InstantMessage(string fromName, UUID target, string message, UUID imSessionID,
            UUID[] conferenceIDs)
        {
            byte[] binaryBucket;

            if (conferenceIDs != null && conferenceIDs.Length > 0)
            {
                binaryBucket = new byte[16 * conferenceIDs.Length];
                for (int i = 0; i < conferenceIDs.Length; ++i)
                    Buffer.BlockCopy(conferenceIDs[i].GetBytes(), 0, binaryBucket, i * 16, 16);
            }
            else
            {
                binaryBucket = Utils.EmptyBytes;
            }

            InstantMessage(fromName, target, message, imSessionID, InstantMessageDialog.MessageFromAgent,
                InstantMessageOnline.Offline, Vector3.Zero, UUID.Zero, binaryBucket);
        }

        /// <summary>
        /// Send an Instant Message
        /// </summary>
        /// <param name="fromName">The name this IM will show up as being from</param>
        /// <param name="target">Key of Avatar</param>
        /// <param name="message">Text message being sent</param>
        /// <param name="imSessionID">IM session ID (to differentiate between IM windows)</param>
        /// <param name="dialog">Type of instant message to send</param>
        /// <param name="offline">Whether to IM offline avatars as well</param>
        /// <param name="position">Senders Position</param>
        /// <param name="regionID">RegionID Sender is In</param>
        /// <param name="binaryBucket">Packed binary data that is specific to
        /// the dialog type</param>
        public void InstantMessage(string fromName, UUID target, string message, UUID imSessionID,
            InstantMessageDialog dialog, InstantMessageOnline offline, Vector3 position, UUID regionID,
            byte[] binaryBucket)
        {
            if (target != UUID.Zero)
            {
                ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket();

                if (imSessionID.Equals(UUID.Zero) || imSessionID.Equals(AgentID))
                    imSessionID = AgentID.Equals(target) ? AgentID : target ^ AgentID;

                im.AgentData.AgentID = Client.Self.AgentID;
                im.AgentData.SessionID = Client.Self.SessionID;

                im.MessageBlock.Dialog = (byte)dialog;
                im.MessageBlock.FromAgentName = Utils.StringToBytes(fromName);
                im.MessageBlock.FromGroup = false;
                im.MessageBlock.ID = imSessionID;
                im.MessageBlock.Message = Utils.StringToBytes(message);
                im.MessageBlock.Offline = (byte)offline;
                im.MessageBlock.ToAgentID = target;

                im.MessageBlock.BinaryBucket = binaryBucket ?? Utils.EmptyBytes;

                // These fields are mandatory, even if we don't have valid values for them
                im.MessageBlock.Position = Vector3.Zero;
                //TODO: Allow region id to be correctly set by caller or fetched from Client.*
                im.MessageBlock.RegionID = regionID;

                // Send the message
                Client.Network.SendPacket(im);
            }
            else
            {
                Logger.Log($"Suppressing instant message \"{message}\" to UUID.Zero",
                    Helpers.LogLevel.Error, Client);
            }
        }

        /// <summary>
        /// Send an Instant Message to a group
        /// </summary>
        /// <param name="groupID"><seealso cref="UUID"/> of the group to send message to</param>
        /// <param name="message">Text Message being sent.</param>
        public void InstantMessageGroup(UUID groupID, string message)
        {
            InstantMessageGroup(Name, groupID, message);
        }

        /// <summary>
        /// Send an Instant Message to a group the agent is a member of
        /// </summary>
        /// <param name="fromName">The name this IM will show up as being from</param>
        /// <param name="groupID"><seealso cref="UUID"/> of the group to send message to</param>
        /// <param name="message">Text message being sent</param>
        public void InstantMessageGroup(string fromName, UUID groupID, string message)
        {
            lock (GroupChatSessions.Dictionary)
                if (GroupChatSessions.ContainsKey(groupID))
                {
                    ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        MessageBlock =
                        {
                            Dialog = (byte) InstantMessageDialog.SessionSend,
                            FromAgentName = Utils.StringToBytes(fromName),
                            FromGroup = false,
                            Message = Utils.StringToBytes(message),
                            Offline = 0,
                            ID = groupID,
                            ToAgentID = groupID,
                            Position = Vector3.Zero,
                            RegionID = UUID.Zero,
                            BinaryBucket = Utils.StringToBytes("\0")
                        }
                    };


                    Client.Network.SendPacket(im);
                }
                else
                {
                    Logger.Log("No Active group chat session appears to exist, use RequestJoinGroupChat() to join one",
                        Helpers.LogLevel.Error, Client);
                }
        }

        /// <summary>
        /// Send a request to join a group chat session
        /// </summary>
        /// <param name="groupID"><seealso cref="UUID"/> of Group to leave</param>
        public void RequestJoinGroupChat(UUID groupID)
        {
            ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MessageBlock =
                {
                    Dialog = (byte) InstantMessageDialog.SessionGroupStart,
                    FromAgentName = Utils.StringToBytes(Client.Self.Name),
                    FromGroup = false,
                    Message = Utils.EmptyBytes,
                    ParentEstateID = 0,
                    Offline = 0,
                    ID = groupID,
                    ToAgentID = groupID,
                    BinaryBucket = Utils.EmptyBytes,
                    Position = Client.Self.SimPosition,
                    RegionID = UUID.Zero
                }
            };


            Client.Network.SendPacket(im);
        }

        /// <summary>
        /// Exit a group chat session. This will stop further Group chat messages
        /// from being sent until session is rejoined.
        /// </summary>
        /// <param name="groupID"><seealso cref="UUID"/> of Group chat session to leave</param>
        public void RequestLeaveGroupChat(UUID groupID)
        {
            ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MessageBlock =
                {
                    Dialog = (byte) InstantMessageDialog.SessionDrop,
                    FromAgentName = Utils.StringToBytes(Client.Self.Name),
                    FromGroup = false,
                    Message = Utils.EmptyBytes,
                    Offline = 0,
                    ID = groupID,
                    ToAgentID = groupID,
                    BinaryBucket = Utils.EmptyBytes,
                    Position = Vector3.Zero,
                    RegionID = UUID.Zero
                }
            };


            Client.Network.SendPacket(im);

            lock (GroupChatSessions.Dictionary)
                if (GroupChatSessions.ContainsKey(groupID))
                    GroupChatSessions.Remove(groupID);
        }

        /// <summary>
        /// Reply to script dialog questions. 
        /// </summary>
        /// <param name="channel">Channel initial request came on</param>
        /// <param name="buttonIndex">Index of button you're "clicking"</param>
        /// <param name="buttonlabel">Label of button you're "clicking"</param>
        /// <param name="objectID"><seealso cref="UUID"/> of Object that sent the dialog request</param>
        /// <seealso cref="OnScriptDialog"/>
        public void ReplyToScriptDialog(int channel, int buttonIndex, string buttonlabel, UUID objectID)
        {
            ScriptDialogReplyPacket reply = new ScriptDialogReplyPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    ButtonIndex = buttonIndex,
                    ButtonLabel = Utils.StringToBytes(buttonlabel),
                    ChatChannel = channel,
                    ObjectID = objectID
                }
            };



            Client.Network.SendPacket(reply);
        }

        /// <summary>
        /// Accept invite for to a chatterbox session
        /// </summary>
        /// <param name="session_id"><seealso cref="UUID"/> of session to accept invite to</param>
        public void ChatterBoxAcceptInvite(UUID session_id)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("ChatSessionRequest");
            if (request != null)
            {
                ChatSessionAcceptInvitation acceptInvite = new ChatSessionAcceptInvitation {SessionID = session_id};
                request.BeginGetResponse(acceptInvite.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);

                lock (GroupChatSessions.Dictionary)
                    if (!GroupChatSessions.ContainsKey(session_id))
                        GroupChatSessions.Add(session_id, new List<ChatSessionMember>());
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

        }

        /// <summary>
        /// Start a friends conference
        /// </summary>
        /// <param name="participants"><seealso cref="UUID"/> List of UUIDs to start a conference with</param>
        /// <param name="tmp_session_id">the temporary session ID returned in the <see cref="OnJoinedGroupChat"/> callback></param>
        public void StartIMConference(List<UUID> participants, UUID tmp_session_id)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("ChatSessionRequest");
            if (request != null)
            {
                ChatSessionRequestStartConference startConference = new ChatSessionRequestStartConference
                {
                    AgentsBlock = new UUID[participants.Count]
                };

                for (int i = 0; i < participants.Count; i++)
                {
                    startConference.AgentsBlock[i] = participants[i];
                }

                startConference.SessionID = tmp_session_id;

                request.BeginGetResponse(startConference.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
        }

        #endregion Chat and instant messages

        #region Viewer Effects

        /// <summary>
        /// Start a particle stream between an agent and an object
        /// </summary>
        /// <param name="sourceAvatar"><seealso cref="UUID"/> Key of the source agent</param>
        /// <param name="targetObject"><seealso cref="UUID"/> Key of the target object</param>
        /// <param name="globalOffset"></param>
        /// <param name="type">The type from the <seealso cref="T:PointAtType"/> enum</param>
        /// <param name="effectID">A unique <seealso cref="UUID"/> for this effect</param>
        public void PointAtEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, PointAtType type,
            UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = new byte[4],
                Duration = (type == PointAtType.Clear) ? 0.0f : Single.MaxValue / 4.0f,
                ID = effectID,
                Type = (byte) EffectType.PointAt
            };

            byte[] typeData = new byte[57];
            if (sourceAvatar != UUID.Zero)
                Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            if (targetObject != UUID.Zero)
                Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);
            typeData[56] = (byte)type;

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        /// <summary>
        /// Start a particle stream between an agent and an object
        /// </summary>
        /// <param name="sourceAvatar"><seealso cref="UUID"/> Key of the source agent</param>
        /// <param name="targetObject"><seealso cref="UUID"/> Key of the target object</param>
        /// <param name="globalOffset">A <seealso cref="Vector3d"/> representing the beams offset from the source</param>
        /// <param name="type">A <seealso cref="T:PointAtType"/> which sets the avatars lookat animation</param>
        /// <param name="effectID"><seealso cref="UUID"/> of the Effect</param>
        public void LookAtEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, LookAtType type,
            UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };


            float duration;

            switch (type)
            {
                case LookAtType.Clear:
                    duration = 2.0f;
                    break;
                case LookAtType.Hover:
                    duration = 1.0f;
                    break;
                case LookAtType.FreeLook:
                    duration = 2.0f;
                    break;
                case LookAtType.Idle:
                    duration = 3.0f;
                    break;
                case LookAtType.AutoListen:
                case LookAtType.Respond:
                    duration = 4.0f;
                    break;
                case LookAtType.None:
                case LookAtType.Select:
                case LookAtType.Focus:
                case LookAtType.Mouselook:
                    duration = Single.MaxValue / 2.0f;
                    break;
                default:
                    duration = 0.0f;
                    break;
            }

            effect.Effect = new ViewerEffectPacket.EffectBlock[1];
            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = new byte[4],
                Duration = duration,
                ID = effectID,
                Type = (byte) EffectType.LookAt
            };

            byte[] typeData = new byte[57];
            Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);
            typeData[56] = (byte)type;

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        /// <summary>
        /// Create a particle beam between an avatar and an primitive 
        /// </summary>
        /// <param name="sourceAvatar">The ID of source avatar</param>
        /// <param name="targetObject">The ID of the target primitive</param>
        /// <param name="globalOffset">global offset</param>
        /// <param name="color">A <see cref="Color4"/> object containing the combined red, green, blue and alpha 
        /// color values of particle beam</param>
        /// <param name="duration">a float representing the duration the parcicle beam will last</param>
        /// <param name="effectID">A Unique ID for the beam</param>
        /// <seealso cref="ViewerEffectPacket"/>
        public void BeamEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, Color4 color,
            float duration, UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = color.GetBytes(),
                Duration = duration,
                ID = effectID,
                Type = (byte) EffectType.Beam
            };

            byte[] typeData = new byte[56];
            Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        /// <summary>
        /// Create a particle swirl around a target position using a <seealso cref="ViewerEffectPacket"/> packet
        /// </summary>
        /// <param name="globalOffset">global offset</param>
        /// <param name="color">A <see cref="Color4"/> object containing the combined red, green, blue and alpha 
        /// color values of particle beam</param>
        /// <param name="duration">a float representing the duration the parcicle beam will last</param>
        /// <param name="effectID">A Unique ID for the beam</param>
        public void SphereEffect(Vector3d globalOffset, Color4 color, float duration, UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = color.GetBytes(),
                Duration = duration,
                ID = effectID,
                Type = (byte) EffectType.Sphere
            };

            byte[] typeData = new byte[56];
            Buffer.BlockCopy(UUID.Zero.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(UUID.Zero.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }


        #endregion Viewer Effects

        #region Movement Actions

        /// <summary>
        /// Sends a request to sit on the specified object
        /// </summary>
        /// <param name="targetID"><seealso cref="UUID"/> of the object to sit on</param>
        /// <param name="offset">Sit at offset</param>
        public void RequestSit(UUID targetID, Vector3 offset)
        {
            AgentRequestSitPacket requestSit = new AgentRequestSitPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                TargetObject =
                {
                    TargetID = targetID,
                    Offset = offset
                }
            };
            Client.Network.SendPacket(requestSit);
        }

        /// <summary>
        /// Follows a call to <seealso cref="RequestSit"/> to actually sit on the object
        /// </summary>
        public void Sit()
        {
            AgentSitPacket sit = new AgentSitPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };
            Client.Network.SendPacket(sit);
        }

        /// <summary>Stands up from sitting on a prim or the ground</summary>
        /// <returns>true of AgentUpdate was sent</returns>
        public bool Stand()
        {
            if (Client.Settings.SEND_AGENT_UPDATES)
            {
                Movement.SitOnGround = false;
                Movement.StandUp = true;
                Movement.SendUpdate();
                Movement.StandUp = false;
                Movement.SendUpdate();
                return true;
            }
            else
            {
                Logger.Log("Attempted to Stand() but agent updates are disabled", Helpers.LogLevel.Warning, Client);
                return false;
            }
        }

        /// <summary>
        /// Does a "ground sit" at the avatar's current position
        /// </summary>
        public void SitOnGround()
        {
            Movement.SitOnGround = true;
            Movement.SendUpdate(true);
        }

        /// <summary>
        /// Starts or stops flying
        /// </summary>
        /// <param name="start">True to start flying, false to stop flying</param>
        public void Fly(bool start)
        {
            Movement.Fly = start;

            Movement.SendUpdate(true);
        }

        /// <summary>
        /// Starts or stops crouching
        /// </summary>
        /// <param name="crouching">True to start crouching, false to stop crouching</param>
        public void Crouch(bool crouching)
        {
            Movement.UpNeg = crouching;
            Movement.SendUpdate(true);
        }

        /// <summary>
        /// Starts a jump (begin holding the jump key)
        /// </summary>
        public void Jump(bool jumping)
        {
            Movement.UpPos = jumping;
            Movement.FastUp = jumping;
            Movement.SendUpdate(true);
        }

        /// <summary>
        /// Use the autopilot sim function to move the avatar to a new
        /// position. Uses double precision to get precise movements
        /// </summary>
        /// <remarks>The z value is currently not handled properly by the simulator</remarks>
        /// <param name="globalX">Global X coordinate to move to</param>
        /// <param name="globalY">Global Y coordinate to move to</param>
        /// <param name="z">Z coordinate to move to</param>
        public void AutoPilot(double globalX, double globalY, double z)
        {
            GenericMessagePacket autopilot = new GenericMessagePacket();

            autopilot.AgentData.AgentID = Client.Self.AgentID;
            autopilot.AgentData.SessionID = Client.Self.SessionID;
            autopilot.AgentData.TransactionID = UUID.Zero;
            autopilot.MethodData.Invoice = UUID.Zero;
            autopilot.MethodData.Method = Utils.StringToBytes("autopilot");
            autopilot.ParamList = new GenericMessagePacket.ParamListBlock[3];
            autopilot.ParamList[0] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(globalX.ToString())
            };
            autopilot.ParamList[1] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(globalY.ToString())
            };
            autopilot.ParamList[2] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(z.ToString())
            };

            Client.Network.SendPacket(autopilot);
        }

        /// <summary>
        /// Use the autopilot sim function to move the avatar to a new position
        /// </summary>
        /// <remarks>The z value is currently not handled properly by the simulator</remarks>
        /// <param name="globalX">Integer value for the global X coordinate to move to</param>
        /// <param name="globalY">Integer value for the global Y coordinate to move to</param>
        /// <param name="z">Floating-point value for the Z coordinate to move to</param>
        public void AutoPilot(ulong globalX, ulong globalY, float z)
        {
            GenericMessagePacket autopilot = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Invoice = UUID.Zero,
                    Method = Utils.StringToBytes("autopilot")
                },
                ParamList = new GenericMessagePacket.ParamListBlock[3]
            };

            autopilot.ParamList[0] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(globalX.ToString())
            };
            autopilot.ParamList[1] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(globalY.ToString())
            };
            autopilot.ParamList[2] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(z.ToString())
            };

            Client.Network.SendPacket(autopilot);
        }

        /// <summary>
        /// Use the autopilot sim function to move the avatar to a new position
        /// </summary>
        /// <remarks>The z value is currently not handled properly by the simulator</remarks>
        /// <param name="localX">Integer value for the local X coordinate to move to</param>
        /// <param name="localY">Integer value for the local Y coordinate to move to</param>
        /// <param name="z">Floating-point value for the Z coordinate to move to</param>
        public void AutoPilotLocal(int localX, int localY, float z)
        {
            uint x, y;
            Utils.LongToUInts(Client.Network.CurrentSim.Handle, out x, out y);
            AutoPilot((ulong)(x + localX), (ulong)(y + localY), z);
        }

        /// <summary>Macro to cancel autopilot sim function</summary>
        /// <remarks>Not certain if this is how it is really done</remarks>
        /// <returns>true if control flags were set and AgentUpdate was sent to the simulator</returns>
        public bool AutoPilotCancel()
        {
            if (Client.Settings.SEND_AGENT_UPDATES)
            {
                Movement.AtPos = true;
                Movement.SendUpdate();
                Movement.AtPos = false;
                Movement.SendUpdate();
                return true;
            }
            else
            {
                Logger.Log("Attempted to AutoPilotCancel() but agent updates are disabled", Helpers.LogLevel.Warning, Client);
                return false;
            }
        }

        #endregion Movement actions

        #region Touch and grab

        public static readonly Vector3 TOUCH_INVALID_TEXCOORD = new Vector3(-1.0f, -1.0f, 0.0f);
        public static readonly Vector3 TOUCH_INVALID_VECTOR = Vector3.Zero;

        /// <summary>
        /// Grabs an object
        /// </summary>
        /// <param name="objectLocalID">an unsigned integer of the objects ID within the simulator</param>
        /// <seealso cref="Simulator.ObjectsPrimitives"/>
        public void Grab(uint objectLocalID)
        {
            Grab(objectLocalID, Vector3.Zero, TOUCH_INVALID_TEXCOORD, TOUCH_INVALID_TEXCOORD, 
                0, TOUCH_INVALID_VECTOR, TOUCH_INVALID_VECTOR, TOUCH_INVALID_VECTOR);
        }

        /// <summary>
        /// Overload: Grab a simulated object
        /// </summary>
        /// <param name="objectLocalID">an unsigned integer of the objects ID within the simulator</param>
        /// <param name="grabOffset"></param>
        /// <param name="uvCoord">The texture coordinates to grab</param>
        /// <param name="stCoord">The surface coordinates to grab</param>
        /// <param name="faceIndex">The face of the position to grab</param>
        /// <param name="position">The region coordinates of the position to grab</param>
        /// <param name="normal">The surface normal of the position to grab (A normal is a vector perpendicular to the surface)</param>
        /// <param name="binormal">The surface bi-normal of the position to grab (A bi-normal is a vector tangent to the surface
        /// pointing along the U direction of the tangent space</param>
        public void Grab(uint objectLocalID, Vector3 grabOffset, Vector3 uvCoord, Vector3 stCoord, 
            int faceIndex, Vector3 position, Vector3 normal, Vector3 binormal)
        {
            ObjectGrabPacket grab = new ObjectGrabPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    LocalID = objectLocalID,
                    GrabOffset = grabOffset
                },
                SurfaceInfo = new ObjectGrabPacket.SurfaceInfoBlock[1]
            };

            grab.SurfaceInfo[0] = new ObjectGrabPacket.SurfaceInfoBlock
            {
                UVCoord = uvCoord,
                STCoord = stCoord,
                FaceIndex = faceIndex,
                Position = position,
                Normal = normal,
                Binormal = binormal
            };

            Client.Network.SendPacket(grab);
        }

        /// <summary>
        /// Drag an object
        /// </summary>
        /// <param name="objectID"><seealso cref="UUID"/> of the object to drag</param>
        /// <param name="grabPosition">Drag target in region coordinates</param>
        public void GrabUpdate(UUID objectID, Vector3 grabPosition)
        {
            GrabUpdate(objectID, grabPosition, Vector3.Zero, Vector3.Zero, Vector3.Zero, 
                0, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }

        /// <summary>
        /// Overload: Drag an object
        /// </summary>
        /// <param name="objectID"><seealso cref="UUID"/> of the object to drag</param>
        /// <param name="grabPosition">Drag target in region coordinates</param>
        /// <param name="grabOffset"></param>
        /// <param name="uvCoord">The texture coordinates to grab</param>
        /// <param name="stCoord">The surface coordinates to grab</param>
        /// <param name="faceIndex">The face of the position to grab</param>
        /// <param name="position">The region coordinates of the position to grab</param>
        /// <param name="normal">The surface normal of the position to grab (A normal is a vector perpendicular to the surface)</param>
        /// <param name="binormal">The surface bi-normal of the position to grab (A bi-normal is a vector tangent to the surface
        /// pointing along the U direction of the tangent space</param>
        public void GrabUpdate(UUID objectID, Vector3 grabPosition, Vector3 grabOffset, Vector3 uvCoord, Vector3 stCoord, 
            int faceIndex, Vector3 position, Vector3 normal, Vector3 binormal)
        {
            ObjectGrabUpdatePacket grab = new ObjectGrabUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData =
                {
                    ObjectID = objectID,
                    GrabOffsetInitial = grabOffset,
                    GrabPosition = grabPosition,
                    TimeSinceLast = 0
                },
                SurfaceInfo = new ObjectGrabUpdatePacket.SurfaceInfoBlock[1]
            };

            grab.SurfaceInfo[0] = new ObjectGrabUpdatePacket.SurfaceInfoBlock
            {
                UVCoord = uvCoord,
                STCoord = stCoord,
                FaceIndex = faceIndex,
                Position = position,
                Normal = normal,
                Binormal = binormal
            };

            Client.Network.SendPacket(grab);
        }

        /// <summary>
        /// Release a grabbed object
        /// </summary>
        /// <param name="objectLocalID">The Objects Simulator Local ID</param>
        /// <seealso cref="Simulator.ObjectsPrimitives"/>
        /// <seealso cref="Grab"/>
        /// <seealso cref="GrabUpdate"/>
        public void DeGrab(uint objectLocalID)
        {
            DeGrab(objectLocalID, TOUCH_INVALID_TEXCOORD, TOUCH_INVALID_TEXCOORD, 
                0, TOUCH_INVALID_VECTOR, TOUCH_INVALID_VECTOR, TOUCH_INVALID_VECTOR);
        }

        /// <summary>
        /// Release a grabbed object
        /// </summary>
        /// <param name="objectLocalID">The Objects Simulator Local ID</param>
        /// <param name="uvCoord">The texture coordinates to grab</param>
        /// <param name="stCoord">The surface coordinates to grab</param>
        /// <param name="faceIndex">The face of the position to grab</param>
        /// <param name="position">The region coordinates of the position to grab</param>
        /// <param name="normal">The surface normal of the position to grab (A normal is a vector perpendicular to the surface)</param>
        /// <param name="binormal">The surface bi-normal of the position to grab (A bi-normal is a vector tangent to the surface
        /// pointing along the U direction of the tangent space</param>
        public void DeGrab(uint objectLocalID, Vector3 uvCoord, Vector3 stCoord, 
            int faceIndex, Vector3 position, Vector3 normal, Vector3 binormal)
        {
            ObjectDeGrabPacket degrab = new ObjectDeGrabPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ObjectData = {LocalID = objectLocalID},
                SurfaceInfo = new ObjectDeGrabPacket.SurfaceInfoBlock[1]
            };


            degrab.SurfaceInfo[0] = new ObjectDeGrabPacket.SurfaceInfoBlock
            {
                UVCoord = uvCoord,
                STCoord = stCoord,
                FaceIndex = faceIndex,
                Position = position,
                Normal = normal,
                Binormal = binormal
            };

            Client.Network.SendPacket(degrab);
        }

        /// <summary>
        /// Touches an object
        /// </summary>
        /// <param name="objectLocalID">an unsigned integer of the objects ID within the simulator</param>
        /// <seealso cref="Simulator.ObjectsPrimitives"/>
        public void Touch(uint objectLocalID)
        {
            Client.Self.Grab(objectLocalID);
            Client.Self.DeGrab(objectLocalID);
        }

        #endregion Touch and grab

        #region Money

        /// <summary>
        /// Request the current L$ balance
        /// </summary>
        public void RequestBalance()
        {
            MoneyBalanceRequestPacket money = new MoneyBalanceRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MoneyData = {TransactionID = UUID.Zero}
            };

            Client.Network.SendPacket(money);
        }

        /// <summary>
        /// Give Money to destination Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Avatar</param>
        /// <param name="amount">Amount in L$</param>
        public void GiveAvatarMoney(UUID target, int amount)
        {
            GiveMoney(target, amount, string.Empty, MoneyTransactionType.Gift, TransactionFlags.None);
        }

        /// <summary>
        /// Give Money to destination Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Avatar</param>
        /// <param name="amount">Amount in L$</param>
        /// <param name="description">Description that will show up in the
        /// recipients transaction history</param>
        public void GiveAvatarMoney(UUID target, int amount, string description)
        {
            GiveMoney(target, amount, description, MoneyTransactionType.Gift, TransactionFlags.None);
        }

        /// <summary>
        /// Give L$ to an object
        /// </summary>
        /// <param name="target">object <seealso cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        /// <param name="objectName">name of object</param>
        public void GiveObjectMoney(UUID target, int amount, string objectName)
        {
            GiveMoney(target, amount, objectName, MoneyTransactionType.PayObject, TransactionFlags.None);
        }

        /// <summary>
        /// Give L$ to a group
        /// </summary>
        /// <param name="target">group <seealso cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        public void GiveGroupMoney(UUID target, int amount)
        {
            GiveMoney(target, amount, string.Empty, MoneyTransactionType.Gift, TransactionFlags.DestGroup);
        }

        /// <summary>
        /// Give L$ to a group
        /// </summary>
        /// <param name="target">group <seealso cref="UUID"/> to give money to</param>
        /// <param name="amount">amount of L$ to give</param>
        /// <param name="description">description of transaction</param>
        public void GiveGroupMoney(UUID target, int amount, string description)
        {
            GiveMoney(target, amount, description, MoneyTransactionType.Gift, TransactionFlags.DestGroup);
        }

        /// <summary>
        /// Pay texture/animation upload fee
        /// </summary>
        public void PayUploadFee()
        {
            GiveMoney(UUID.Zero, Client.Settings.UPLOAD_COST, string.Empty, MoneyTransactionType.UploadCharge,
                TransactionFlags.None);
        }

        /// <summary>
        /// Pay texture/animation upload fee
        /// </summary>
        /// <param name="description">description of the transaction</param>
        public void PayUploadFee(string description)
        {
            GiveMoney(UUID.Zero, Client.Settings.UPLOAD_COST, description, MoneyTransactionType.UploadCharge,
                TransactionFlags.None);
        }

        /// <summary>
        /// Give Money to destination Object or Avatar
        /// </summary>
        /// <param name="target">UUID of the Target Object/Avatar</param>
        /// <param name="amount">Amount in L$</param>
        /// <param name="description">Reason (Optional normally)</param>
        /// <param name="type">The type of transaction</param>
        /// <param name="flags">Transaction flags, mostly for identifying group
        /// transactions</param>
        public void GiveMoney(UUID target, int amount, string description, MoneyTransactionType type, TransactionFlags flags)
        {
            MoneyTransferRequestPacket money = new MoneyTransferRequestPacket
            {
                AgentData =
                {
                    AgentID = id,
                    SessionID = Client.Self.SessionID
                },
                MoneyData =
                {
                    Description = Utils.StringToBytes(description),
                    DestID = target,
                    SourceID = id,
                    TransactionType = (int) type,
                    AggregatePermInventory = 0,
                    AggregatePermNextOwner = 0,
                    Flags = (byte) flags,
                    Amount = amount
                }
            };
            // This is weird, apparently always set to zero though
            // This is weird, apparently always set to zero though

            Client.Network.SendPacket(money);
        }

        #endregion Money

        #region Gestures
        /// <summary>
        /// Plays a gesture
        /// </summary>
        /// <param name="gestureID">Asset <seealso cref="UUID"/> of the gesture</param>
        public void PlayGesture(UUID gestureID)
        {
            ThreadPool.QueueUserWorkItem((_) =>
            {
                // First fetch the guesture
                AssetGesture gesture = null;

                if (gestureCache.ContainsKey(gestureID))
                {
                    gesture = gestureCache[gestureID];
                }
                else
                {
                    AutoResetEvent gotAsset = new AutoResetEvent(false);

                    Client.Assets.RequestAsset(gestureID, AssetType.Gesture, true,
                        delegate(AssetDownload transfer, Asset asset)
                        {
                            if (transfer.Success)
                            {
                                gesture = (AssetGesture) asset;
                            }

                            gotAsset.Set();
                        }
                    );

                    gotAsset.WaitOne(30 * 1000, false);

                    if (gesture != null && gesture.Decode())
                    {
                        lock (gestureCache)
                        {
                            if (!gestureCache.ContainsKey(gestureID))
                            {
                                gestureCache[gestureID] = gesture;
                            }
                        }
                    }
                }

                // We got it, now we play it
                if (gesture == null) return;
                foreach (GestureStep step in gesture.Sequence)
                {
                    switch (step.GestureStepType)
                    {
                        case GestureStepType.Chat:
                            string text = ((GestureStepChat) step).Text;
                            int channel = 0;
                            Match m;

                            if (
                            (m =
                                Regex.Match(text, @"^/(?<channel>-?[0-9]+)\s*(?<text>.*)",
                                    RegexOptions.CultureInvariant)).Success)
                            {
                                if (int.TryParse(m.Groups["channel"].Value, out channel))
                                {
                                    text = m.Groups["text"].Value;
                                }
                            }

                            Chat(text, channel, ChatType.Normal);
                            break;

                        case GestureStepType.Animation:
                            GestureStepAnimation anim = (GestureStepAnimation) step;

                            if (anim.AnimationStart)
                            {
                                if (SignaledAnimations.ContainsKey(anim.ID))
                                {
                                    AnimationStop(anim.ID, true);
                                }
                                AnimationStart(anim.ID, true);
                            }
                            else
                            {
                                AnimationStop(anim.ID, true);
                            }
                            break;

                        case GestureStepType.Sound:
                            Client.Sound.PlaySound(((GestureStepSound) step).ID);
                            break;

                        case GestureStepType.Wait:
                            GestureStepWait wait = (GestureStepWait) step;
                            if (wait.WaitForTime)
                            {
                                Thread.Sleep((int) (1000f * wait.WaitTime));
                            }
                            if (wait.WaitForAnimation)
                            {
                                // TODO: implement waiting for all animations to end that were triggered
                                // during playing of this guesture sequence
                            }
                            break;
                    }
                }
            });
        }

        /// <summary>
        /// Mark gesture active
        /// </summary>
        /// <param name="invID">Inventory <seealso cref="UUID"/> of the gesture</param>
        /// <param name="assetID">Asset <seealso cref="UUID"/> of the gesture</param>
        public void ActivateGesture(UUID invID, UUID assetID)
        {
            ActivateGesturesPacket packet = new ActivateGesturesPacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID,
                    Flags = 0x00
                }
            };


            ActivateGesturesPacket.DataBlock block = new ActivateGesturesPacket.DataBlock
            {
                ItemID = invID,
                AssetID = assetID,
                GestureFlags = 0x00
            };

            packet.Data = new ActivateGesturesPacket.DataBlock[1];
            packet.Data[0] = block;

            Client.Network.SendPacket(packet);
            ActiveGestures[invID] = assetID;
        }

        /// <summary>
        /// Mark gesture inactive
        /// </summary>
        /// <param name="invID">Inventory <seealso cref="UUID"/> of the gesture</param>
        public void DeactivateGesture(UUID invID)
        {
            DeactivateGesturesPacket p = new DeactivateGesturesPacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID,
                    Flags = 0x00
                }
            };


            DeactivateGesturesPacket.DataBlock b = new DeactivateGesturesPacket.DataBlock
            {
                ItemID = invID,
                GestureFlags = 0x00
            };

            p.Data = new DeactivateGesturesPacket.DataBlock[1];
            p.Data[0] = b;

            Client.Network.SendPacket(p);
            ActiveGestures.Remove(invID);
        }
        #endregion

        #region Animations

        /// <summary>
        /// Send an AgentAnimation packet that toggles a single animation on
        /// </summary>
        /// <param name="animation">The <seealso cref="UUID"/> of the animation to start playing</param>
        /// <param name="reliable">Whether to ensure delivery of this packet or not</param>
        public void AnimationStart(UUID animation, bool reliable)
        {
            var animations = new Dictionary<UUID, bool> {[animation] = true};

            Animate(animations, reliable);
        }

        /// <summary>
        /// Send an AgentAnimation packet that toggles a single animation off
        /// </summary>
        /// <param name="animation">The <seealso cref="UUID"/> of a 
        /// currently playing animation to stop playing</param>
        /// <param name="reliable">Whether to ensure delivery of this packet or not</param>
        public void AnimationStop(UUID animation, bool reliable)
        {
            var animations = new Dictionary<UUID, bool> {[animation] = false};

            Animate(animations, reliable);
        }

        /// <summary>
        /// Send an AgentAnimation packet that will toggle animations on or off
        /// </summary>
        /// <param name="animations">A list of animation <seealso cref="UUID"/>s, and whether to
        /// turn that animation on or off</param>
        /// <param name="reliable">Whether to ensure delivery of this packet or not</param>
        public void Animate(Dictionary<UUID, bool> animations, bool reliable)
        {
            AgentAnimationPacket animate = new AgentAnimationPacket
            {
                Header = {Reliable = reliable},
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                AnimationList = new AgentAnimationPacket.AnimationListBlock[animations.Count]
            };

            int i = 0;

            foreach (var animation in animations)
            {
                animate.AnimationList[i] = new AgentAnimationPacket.AnimationListBlock
                {
                    AnimID = animation.Key,
                    StartAnim = animation.Value
                };

                i++;
            }

            // TODO: Implement support for this
            animate.PhysicalAvatarEventList = new AgentAnimationPacket.PhysicalAvatarEventListBlock[0];

            Client.Network.SendPacket(animate);
        }

        #endregion Animations

        #region Teleporting

        /// <summary>
        /// Teleports agent to their stored home location
        /// </summary>
        /// <returns>true on successful teleport to home location</returns>
        public bool GoHome()
        {
            return Teleport(UUID.Zero);
        }

        /// <summary>
        /// Teleport agent to a landmark
        /// </summary>
        /// <param name="landmark"><seealso cref="UUID"/> of the landmark to teleport agent to</param>
        /// <returns>true on success, false on failure</returns>
        public bool Teleport(UUID landmark)
        {
            teleportStat = TeleportStatus.None;
            teleportEvent.Reset();
            TeleportLandmarkRequestPacket p = new TeleportLandmarkRequestPacket
            {
                Info = new TeleportLandmarkRequestPacket.InfoBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    LandmarkID = landmark
                }
            };
            Client.Network.SendPacket(p);

            teleportEvent.WaitOne(Client.Settings.TELEPORT_TIMEOUT, false);

            if (teleportStat == TeleportStatus.None ||
                teleportStat == TeleportStatus.Start ||
                teleportStat == TeleportStatus.Progress)
            {
                teleportMessage = "Teleport timed out.";
                teleportStat = TeleportStatus.Failed;
            }

            return (teleportStat == TeleportStatus.Finished);
        }

        /// <summary>
        /// Attempt to look up a simulator name and teleport to the discovered
        /// destination
        /// </summary>
        /// <param name="simName">Region name to look up</param>
        /// <param name="position">Position to teleport to</param>
        /// <returns>True if the lookup and teleport were successful, otherwise
        /// false</returns>
        public bool Teleport(string simName, Vector3 position)
        {
            return Teleport(simName, position, new Vector3(0, 1.0f, 0));
        }

        /// <summary>
        /// Attempt to look up a simulator name and teleport to the discovered
        /// destination
        /// </summary>
        /// <param name="simName">Region name to look up</param>
        /// <param name="position">Position to teleport to</param>
        /// <param name="lookAt">Target to look at</param>
        /// <returns>True if the lookup and teleport were successful, otherwise
        /// false</returns>
        public bool Teleport(string simName, Vector3 position, Vector3 lookAt)
        {
            if (Client.Network.CurrentSim == null)
                return false;

            teleportStat = TeleportStatus.None;

            if (simName != Client.Network.CurrentSim.Name)
            {
                // Teleporting to a foreign sim
                GridRegion region;

                if (Client.Grid.GetGridRegion(simName, GridLayerType.Objects, out region))
                {
                    return Teleport(region.RegionHandle, position, lookAt);
                }
                else
                {
                    teleportMessage = "Unable to resolve name: " + simName;
                    teleportStat = TeleportStatus.Failed;
                    return false;
                }
            }
            else
            {
                // Teleporting to the sim we're already in
                return Teleport(Client.Network.CurrentSim.Handle, position, lookAt);
            }
        }

        /// <summary>
        /// Teleport agent to another region
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><seealso cref="Vector3"/> position in destination sim to teleport to</param>
        /// <returns>true on success, false on failure</returns>
        /// <remarks>This call is blocking</remarks>
        public bool Teleport(ulong regionHandle, Vector3 position)
        {
            return Teleport(regionHandle, position, new Vector3(0.0f, 1.0f, 0.0f));
        }

        /// <summary>
        /// Teleport agent to another region
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><seealso cref="Vector3"/> position in destination sim to teleport to</param>
        /// <param name="lookAt"><seealso cref="Vector3"/> direction in destination sim agent will look at</param>
        /// <returns>true on success, false on failure</returns>
        /// <remarks>This call is blocking</remarks>
        public bool Teleport(ulong regionHandle, Vector3 position, Vector3 lookAt)
        {
            if (Client.Network.CurrentSim == null ||
                Client.Network.CurrentSim.Caps == null ||
                !Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                // Wait a bit to see if the event queue comes online
                AutoResetEvent queueEvent = new AutoResetEvent(false);
                EventHandler<EventQueueRunningEventArgs> queueCallback =
                    delegate(object sender, EventQueueRunningEventArgs e)
                    {
                        if (e.Simulator == Client.Network.CurrentSim)
                            queueEvent.Set();
                    };

                Client.Network.EventQueueRunning += queueCallback;
                queueEvent.WaitOne(10 * 1000, false);
                Client.Network.EventQueueRunning -= queueCallback;
            }

            teleportStat = TeleportStatus.None;
            teleportEvent.Reset();

            RequestTeleport(regionHandle, position, lookAt);

            teleportEvent.WaitOne(Client.Settings.TELEPORT_TIMEOUT, false);

            if (teleportStat == TeleportStatus.None ||
                teleportStat == TeleportStatus.Start ||
                teleportStat == TeleportStatus.Progress)
            {
                teleportMessage = "Teleport timed out.";
                teleportStat = TeleportStatus.Failed;
            }

            return (teleportStat == TeleportStatus.Finished);
        }

        /// <summary>
        /// Request teleport to a another simulator
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><seealso cref="Vector3"/> position in destination sim to teleport to</param>
        public void RequestTeleport(ulong regionHandle, Vector3 position)
        {
            RequestTeleport(regionHandle, position, new Vector3(0.0f, 1.0f, 0.0f));
        }

        /// <summary>
        /// Request teleport to a another simulator
        /// </summary>
        /// <param name="regionHandle">handle of region to teleport agent to</param>
        /// <param name="position"><seealso cref="Vector3"/> position in destination sim to teleport to</param>
        /// <param name="lookAt"><seealso cref="Vector3"/> direction in destination sim agent will look at</param>
        public void RequestTeleport(ulong regionHandle, Vector3 position, Vector3 lookAt)
        {
            if (Client.Network.CurrentSim != null &&
                Client.Network.CurrentSim.Caps != null &&
                Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                TeleportLocationRequestPacket teleport = new TeleportLocationRequestPacket();
                teleport.AgentData.AgentID = Client.Self.AgentID;
                teleport.AgentData.SessionID = Client.Self.SessionID;
                teleport.Info.LookAt = lookAt;
                teleport.Info.Position = position;
                teleport.Info.RegionHandle = regionHandle;

                Logger.Log("Requesting teleport to region handle " + regionHandle.ToString(), Helpers.LogLevel.Info, Client);

                Client.Network.SendPacket(teleport);
            }
            else
            {
                teleportMessage = "CAPS event queue is not running";
                teleportEvent.Set();
                teleportStat = TeleportStatus.Failed;
            }
        }

        /// <summary>
        /// Teleport agent to a landmark
        /// </summary>
        /// <param name="landmark"><seealso cref="UUID"/> of the landmark to teleport agent to</param>
        public void RequestTeleport(UUID landmark)
        {
            TeleportLandmarkRequestPacket p = new TeleportLandmarkRequestPacket
            {
                Info = new TeleportLandmarkRequestPacket.InfoBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    LandmarkID = landmark
                }
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Send a teleport lure to another avatar with default "Join me in ..." invitation message
        /// </summary>
        /// <param name="targetID">target avatars <seealso cref="UUID"/> to lure</param>
        public void SendTeleportLure(UUID targetID)
        {
            SendTeleportLure(targetID, "Join me in " + Client.Network.CurrentSim.Name + "!");
        }

        /// <summary>
        /// Send a teleport lure to another avatar with custom invitation message
        /// </summary>
        /// <param name="targetID">target avatars <seealso cref="UUID"/> to lure</param>
        /// <param name="message">custom message to send with invitation</param>
        public void SendTeleportLure(UUID targetID, string message)
        {
            StartLurePacket p = new StartLurePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.id,
                    SessionID = Client.Self.SessionID
                },
                Info =
                {
                    LureType = 0,
                    Message = Utils.StringToBytes(message)
                },
                TargetData = new[] {new StartLurePacket.TargetDataBlock()}
            };
            p.TargetData[0].TargetID = targetID;
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Respond to a teleport lure by either accepting it and initiating 
        /// the teleport, or denying it
        /// </summary>
        /// <param name="requesterID"><seealso cref="UUID"/> of the avatar sending the lure</param>
        /// <param name="sessionID">IM session <seealso cref="UUID"/> of the incoming lure request</param>
        /// <param name="accept">true to accept the lure, false to decline it</param>
        public void TeleportLureRespond(UUID requesterID, UUID sessionID, bool accept)
        {
            if (accept)
            {
                TeleportLureRequestPacket lure = new TeleportLureRequestPacket
                {
                    Info =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        LureID = sessionID,
                        TeleportFlags = (uint) TeleportFlags.ViaLure
                    }
                };

                Client.Network.SendPacket(lure);
            }
            else
            {
                InstantMessage(Name, requesterID, string.Empty, sessionID, InstantMessageDialog.DenyTeleport,
                    InstantMessageOnline.Offline, SimPosition, UUID.Zero, Utils.EmptyBytes);
            }
        }

        /// <summary>
        /// Request a teleport lure from another agent
        /// </summary>
        /// <param name="targetID"><seealso cref="UUID"/> of the avatar lure is being requested from</param>
        /// <param name="sessionID">IM session <seealso cref="UUID"/></param>
        /// <param name="message">message to send with request</param>
        public void SendTeleportLureRequest(UUID targetID, UUID sessionID, string message)
        {
            if (targetID != AgentID)
            {
                InstantMessage(Name, targetID, message, sessionID, InstantMessageDialog.RequestLure,
                InstantMessageOnline.Online, SimPosition, UUID.Zero, Utils.EmptyBytes);
            }
        }
        public void SendTeleportLureRequest(UUID targetID, string message)
        {
            SendTeleportLureRequest(targetID, targetID, message);
        }
        public void SendTeleportLureRequest(UUID targetID)
        {
            SendTeleportLureRequest(targetID, "Hi there I would like to teleport to you");
        }

        #endregion Teleporting

        #region Misc

        /// <summary>
        /// Update agent profile
        /// </summary>
        /// <param name="profile"><seealso cref="OpenMetaverse.Avatar.AvatarProperties"/> struct containing updated 
        /// profile information</param>
        public void UpdateProfile(Avatar.AvatarProperties profile)
        {
            AvatarPropertiesUpdatePacket apup = new AvatarPropertiesUpdatePacket
            {
                AgentData =
                {
                    AgentID = id,
                    SessionID = sessionID
                },
                PropertiesData =
                {
                    AboutText = Utils.StringToBytes(profile.AboutText),
                    AllowPublish = profile.AllowPublish,
                    FLAboutText = Utils.StringToBytes(profile.FirstLifeText),
                    FLImageID = profile.FirstLifeImage,
                    ImageID = profile.ProfileImage,
                    MaturePublish = profile.MaturePublish,
                    ProfileURL = Utils.StringToBytes(profile.ProfileURL)
                }
            };

            Client.Network.SendPacket(apup);
        }

        /// <summary>
        /// Update agents profile interests
        /// </summary>
        /// <param name="interests">selection of interests from <seealso cref="T:OpenMetaverse.Avatar.Interests"/> struct</param>
        public void UpdateInterests(Avatar.Interests interests)
        {
            AvatarInterestsUpdatePacket aiup = new AvatarInterestsUpdatePacket
            {
                AgentData =
                {
                    AgentID = id,
                    SessionID = sessionID
                },
                PropertiesData =
                {
                    LanguagesText = Utils.StringToBytes(interests.LanguagesText),
                    SkillsMask = interests.SkillsMask,
                    SkillsText = Utils.StringToBytes(interests.SkillsText),
                    WantToMask = interests.WantToMask,
                    WantToText = Utils.StringToBytes(interests.WantToText)
                }
            };

            Client.Network.SendPacket(aiup);
        }

        /// <summary>
        /// Set the height and the width of the client window. This is used
        /// by the server to build a virtual camera frustum for our avatar
        /// </summary>
        /// <param name="height">New height of the viewer window</param>
        /// <param name="width">New width of the viewer window</param>
        public void SetHeightWidth(ushort height, ushort width)
        {
            AgentHeightWidthPacket heightwidth = new AgentHeightWidthPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    CircuitCode = Client.Network.CircuitCode
                },
                HeightWidthBlock =
                {
                    Height = height,
                    Width = width,
                    GenCounter = heightWidthGenCounter++
                }
            };

            Client.Network.SendPacket(heightwidth);
        }

        /// <summary>
        /// Request the list of muted objects and avatars for this agent
        /// </summary>
        public void RequestMuteList()
        {
            MuteListRequestPacket mute = new MuteListRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MuteData = {MuteCRC = 0}
            };

            Client.Network.SendPacket(mute);
        }

        /// <summary>
        /// Mute an object, resident, etc.
        /// </summary>
        /// <param name="type">Mute type</param>
        /// <param name="id">Mute UUID</param>
        /// <param name="name">Mute name</param>
        public void UpdateMuteListEntry(MuteType type, UUID id, string name)
        {
            UpdateMuteListEntry(type, id, name, MuteFlags.Default);
        }

        /// <summary>
        /// Mute an object, resident, etc.
        /// </summary>
        /// <param name="type">Mute type</param>
        /// <param name="id">Mute UUID</param>
        /// <param name="name">Mute name</param>
        /// <param name="flags">Mute flags</param>
        public void UpdateMuteListEntry(MuteType type, UUID id, string name, MuteFlags flags)
        {
            UpdateMuteListEntryPacket p = new UpdateMuteListEntryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MuteData =
                {
                    MuteType = (int) type,
                    MuteID = id,
                    MuteName = Utils.StringToBytes(name),
                    MuteFlags = (uint) flags
                }
            };


            Client.Network.SendPacket(p);

            MuteEntry me = new MuteEntry
            {
                Type = type,
                ID = id,
                Name = name,
                Flags = flags
            };
            lock (MuteList.Dictionary)
            {
                MuteList[$"{me.ID}|{me.Name}"] = me;
            }
            OnMuteListUpdated(EventArgs.Empty);

        }

        /// <summary>
        /// Unmute an object, resident, etc.
        /// </summary>
        /// <param name="id">Mute UUID</param>
        /// <param name="name">Mute name</param>
        public void RemoveMuteListEntry(UUID id, string name)
        {
            RemoveMuteListEntryPacket p = new RemoveMuteListEntryPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                MuteData =
                {
                    MuteID = id,
                    MuteName = Utils.StringToBytes(name)
                }
            };


            Client.Network.SendPacket(p);

            string listKey = $"{id}|{name}";
            if (MuteList.ContainsKey(listKey))
            {
                lock (MuteList.Dictionary)
                {
                    MuteList.Remove(listKey);
                }
                OnMuteListUpdated(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sets home location to agents current position
        /// </summary>
        /// <remarks>will fire an AlertMessage (<seealso cref="E:OpenMetaverse.AgentManager.OnAlertMessage"/>) with 
        /// success or failure message</remarks>
        public void SetHome()
        {
            SetStartLocationRequestPacket s = new SetStartLocationRequestPacket
            {
                AgentData = new SetStartLocationRequestPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };
            s.StartLocationData = new SetStartLocationRequestPacket.StartLocationDataBlock
            {
                LocationPos = Client.Self.SimPosition,
                LocationID = 1,
                SimName = Utils.StringToBytes(String.Empty),
                LocationLookAt = Movement.Camera.AtAxis
            };
            Client.Network.SendPacket(s);
        }

        /// <summary>
        /// Move an agent in to a simulator. This packet is the last packet
        /// needed to complete the transition in to a new simulator
        /// </summary>
        /// <param name="simulator"><seealso cref="T:OpenMetaverse.Simulator"/> Object</param>
        public void CompleteAgentMovement(Simulator simulator)
        {
            CompleteAgentMovementPacket move = new CompleteAgentMovementPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    CircuitCode = Client.Network.CircuitCode
                }
            };


            Client.Network.SendPacket(move, simulator);
        }

        /// <summary>
        /// Reply to script permissions request
        /// </summary>
        /// <param name="simulator"><seealso cref="T:OpenMetaverse.Simulator"/> Object</param>
        /// <param name="itemID"><seealso cref="UUID"/> of the itemID requesting permissions</param>
        /// <param name="taskID"><seealso cref="UUID"/> of the taskID requesting permissions</param>
        /// <param name="permissions"><seealso cref="OpenMetaverse.ScriptPermission"/> list of permissions to allow</param>
        public void ScriptQuestionReply(Simulator simulator, UUID itemID, UUID taskID, ScriptPermission permissions)
        {
            ScriptAnswerYesPacket yes = new ScriptAnswerYesPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    ItemID = itemID,
                    TaskID = taskID,
                    Questions = (int) permissions
                }
            };

            Client.Network.SendPacket(yes, simulator);
        }

        /// <summary>
        /// Respond to a group invitation by either accepting or denying it
        /// </summary>
        /// <param name="groupID">UUID of the group (sent in the AgentID field of the invite message)</param>
        /// <param name="imSessionID">IM Session ID from the group invitation message</param>
        /// <param name="accept">Accept the group invitation or deny it</param>
        public void GroupInviteRespond(UUID groupID, UUID imSessionID, bool accept)
        {
            InstantMessage(Name, groupID, string.Empty, imSessionID,
                accept ? InstantMessageDialog.GroupInvitationAccept : InstantMessageDialog.GroupInvitationDecline,
                InstantMessageOnline.Offline, Vector3.Zero, UUID.Zero, Utils.EmptyBytes);
        }

        /// <summary>
        /// Requests script detection of objects and avatars
        /// </summary>
        /// <param name="name">name of the object/avatar to search for</param>
        /// <param name="searchID">UUID of the object or avatar to search for</param>
        /// <param name="type">Type of search from ScriptSensorTypeFlags</param>
        /// <param name="range">range of scan (96 max?)</param>
        /// <param name="arc">the arc in radians to search within</param>
        /// <param name="requestID">an user generated ID to correlate replies with</param>
        /// <param name="sim">Simulator to perform search in</param>
        public void RequestScriptSensor(string name, UUID searchID, ScriptSensorTypeFlags type, float range, float arc, UUID requestID, Simulator sim)
        {
            ScriptSensorRequestPacket request = new ScriptSensorRequestPacket
            {
                Requester =
                {
                    Arc = arc,
                    Range = range,
                    RegionHandle = sim.Handle,
                    RequestID = requestID,
                    SearchDir = Quaternion.Identity,
                    SearchID = searchID,
                    SearchName = Utils.StringToBytes(name),
                    SearchPos = Vector3.Zero,
                    SearchRegions = 0,
                    SourceID = Client.Self.AgentID,
                    Type = (int) type
                }
            };
            // TODO: this needs to be tested
            // TODO: ?

            Client.Network.SendPacket(request, sim);
        }

        /// <summary>
        /// Create or update profile pick
        /// </summary>
        /// <param name="pickID">UUID of the pick to update, or random UUID to create a new pick</param>
        /// <param name="topPick">Is this a top pick? (typically false)</param>
        /// <param name="parcelID">UUID of the parcel (UUID.Zero for the current parcel)</param>
        /// <param name="name">Name of the pick</param>
        /// <param name="globalPosition">Global position of the pick landmark</param>
        /// <param name="textureID">UUID of the image displayed with the pick</param>
        /// <param name="description">Long description of the pick</param>
        public void PickInfoUpdate(UUID pickID, bool topPick, UUID parcelID, string name, Vector3d globalPosition, UUID textureID, string description)
        {
            PickInfoUpdatePacket pick = new PickInfoUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    PickID = pickID,
                    Desc = Utils.StringToBytes(description),
                    CreatorID = Client.Self.AgentID,
                    TopPick = topPick,
                    ParcelID = parcelID,
                    Name = Utils.StringToBytes(name),
                    SnapshotID = textureID,
                    PosGlobal = globalPosition,
                    SortOrder = 0,
                    Enabled = false
                }
            };

            Client.Network.SendPacket(pick);
        }

        /// <summary>
        /// Delete profile pick
        /// </summary>
        /// <param name="pickID">UUID of the pick to delete</param>
        public void PickDelete(UUID pickID)
        {
            PickDeletePacket delete = new PickDeletePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.sessionID
                },
                Data = {PickID = pickID}
            };

            Client.Network.SendPacket(delete);
        }

        /// <summary>
        /// Create or update profile Classified
        /// </summary>
        /// <param name="classifiedID">UUID of the classified to update, or random UUID to create a new classified</param>
        /// <param name="category">Defines what category the classified is in</param>
        /// <param name="snapshotID">UUID of the image displayed with the classified</param>
        /// <param name="price">Price that the classified will cost to place for a week</param>
        /// <param name="position">Global position of the classified landmark</param>
        /// <param name="name">Name of the classified</param>
        /// <param name="desc">Long description of the classified</param>
        /// <param name="autoRenew">if true, auto renew classified after expiration</param>
        public void UpdateClassifiedInfo(UUID classifiedID, DirectoryManager.ClassifiedCategories category,
            UUID snapshotID, int price, Vector3d position, string name, string desc, bool autoRenew)
        {
            ClassifiedInfoUpdatePacket classified = new ClassifiedInfoUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data =
                {
                    ClassifiedID = classifiedID,
                    Category = (uint) category,
                    ParcelID = UUID.Zero,
                    ParentEstate = 0,
                    SnapshotID = snapshotID,
                    PosGlobal = position,
                    ClassifiedFlags = autoRenew ? (byte) 32 : (byte) 0,
                    PriceForListing = price,
                    Name = Utils.StringToBytes(name),
                    Desc = Utils.StringToBytes(desc)
                }
            };

            Client.Network.SendPacket(classified);
        }

        /// <summary>
        /// Create or update profile Classified
        /// </summary>
        /// <param name="classifiedID">UUID of the classified to update, or random UUID to create a new classified</param>
        /// <param name="category">Defines what category the classified is in</param>
        /// <param name="snapshotID">UUID of the image displayed with the classified</param>
        /// <param name="price">Price that the classified will cost to place for a week</param>
        /// <param name="name">Name of the classified</param>
        /// <param name="desc">Long description of the classified</param>
        /// <param name="autoRenew">if true, auto renew classified after expiration</param>
        public void UpdateClassifiedInfo(UUID classifiedID, DirectoryManager.ClassifiedCategories category, UUID snapshotID, int price, string name, string desc, bool autoRenew)
        {
            UpdateClassifiedInfo(classifiedID, category, snapshotID, price, Client.Self.GlobalPosition, name, desc, autoRenew);
        }

        /// <summary>
        /// Delete a classified ad
        /// </summary>
        /// <param name="classifiedID">The classified ads ID</param>
        public void DeleteClassfied(UUID classifiedID)
        {
            ClassifiedDeletePacket classified = new ClassifiedDeletePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Data = {ClassifiedID = classifiedID}
            };

            Client.Network.SendPacket(classified);
        }

        /// <summary>
        /// Fetches resource usage by agents attachments
        /// </summary>
        /// <param name="callback">Called when the requested information is collected</param>
        public void GetAttachmentResources(AttachmentResourcesCallback callback)
        {
            try
            {
                CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("AttachmentResources");

                request.OnComplete += delegate(CapsClient client, OSD result, Exception error)
                {
                    try
                    {
                        if (result == null || error != null)
                        {
                            callback(false, null);
                        }
                        AttachmentResourcesMessage info = AttachmentResourcesMessage.FromOSD(result);
                        callback(true, info);

                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Failed fetching AttachmentResources", Helpers.LogLevel.Error, Client, ex);
                        callback(false, null);
                    }
                };

                request.BeginGetResponse(Client.Settings.CAPS_TIMEOUT);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed fetching AttachmentResources", Helpers.LogLevel.Error, Client, ex);
                callback(false, null);
            }
        }

        /// <summary>
        /// Initiates request to set a new display name
        /// </summary>
        /// <param name="oldName">Previous display name</param>
        /// <param name="newName">Desired new display name</param>
        public void SetDisplayName(string oldName, string newName)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                Logger.Log("Not connected to simulator. " +
                           "Unable to set display name.",
                    Helpers.LogLevel.Warning, Client);
                return;
            }

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("SetDisplayName");
            if (request == null)
            {
                Logger.Log("Unable to obtain capability. Unable to set display name.", 
                    Helpers.LogLevel.Warning, Client);
                return;
            }

            SetDisplayNameMessage msg = new SetDisplayNameMessage
            {
                OldDisplayName = oldName,
                NewDisplayName = newName
            };

            request.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }

        /// <summary>
        /// Tells the sim what UI language is used, and if it's ok to share that with scripts
        /// </summary>
        /// <param name="language">Two letter language code</param>
        /// <param name="isPublic">Share language info with scripts</param>
        public void UpdateAgentLanguage(string language, bool isPublic)
        {
            try
            {
                UpdateAgentLanguageMessage msg = new UpdateAgentLanguageMessage
                {
                    Language = language,
                    LanguagePublic = isPublic
                };

                CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateAgentLanguage");
                request?.BeginGetResponse(msg.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to update agent language", Helpers.LogLevel.Error, Client, ex);
            }
        }

        public delegate void AgentAccessCallback(AgentAccessEventArgs e);

        /// <summary>
        /// Sets agents maturity access level
        /// </summary>
        /// <param name="access">PG, M or A</param>
        public void SetAgentAccess(string access)
        {
            SetAgentAccess(access, null);
        }

        /// <summary>
        /// Sets agents maturity access level
        /// </summary>
        /// <param name="access">PG, M or A</param>
        /// <param name="callback">Callback function</param>
        public void SetAgentAccess(string access, AgentAccessCallback callback)
        {
            if (Client == null || !Client.Network.Connected || Client.Network.CurrentSim.Caps == null) return;

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("UpdateAgentInformation");
            if (request == null) return;

            request.OnComplete += (client, result, error) =>
            {
                bool success = true;

                if (error == null && result is OSDMap)
                {
                    var map = ((OSDMap)result)["access_prefs"];
                    agentAccess = ((OSDMap)map)["max"];
                    Logger.Log($"Max maturity access set to {agentAccess}", Helpers.LogLevel.Info, Client );
                }
                else if (error == null)
                {
                    Logger.Log($"Max maturity unchanged at {agentAccess}", Helpers.LogLevel.Info, Client);
                }
                else
                {
                    Logger.Log("Failed setting max maturity access.", Helpers.LogLevel.Warning, Client);
                    success = false;
                }
                
                if (callback != null)
                {
                    try { callback(new AgentAccessEventArgs(success, agentAccess)); }
                    catch { } // *TODO: So gross
                }

            };
            OSDMap req = new OSDMap();
            OSDMap prefs = new OSDMap {["max"] = access};
            req["access_prefs"] = prefs;

            request.BeginGetResponse(req, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }

        /// <summary>
        /// Sets agents hover height.
        /// </summary>
        /// <param name="hoverHeight">Hover height [-2.0, 2.0]</param>
        public void SetHoverHeight(double hoverHeight)
        {
            if (Client == null || !Client.Network.Connected || Client.Network.CurrentSim.Caps == null)
            {
                return;
            }

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("AgentPreferences");
            if (request == null) { return; }

            request.OnComplete += (client, result, error) =>
            {
                var resultMap = result as OSDMap;

                if(error != null)
                {
                    Logger.Log($"Failed to set hover height: {error}.", Helpers.LogLevel.Warning, Client);
                }
                else if (resultMap == null)
                {
                    Logger.Log($"Failed to set hover height: Expected {nameof(OSDMap)} response, but got {result.Type}", Helpers.LogLevel.Warning, Client);
                }
                else
                {
                    var confirmedHeight = resultMap["hover_height"];
                    Logger.Log($"Hover height set to {confirmedHeight}", Helpers.LogLevel.Info, Client);
                }
            };

            var postData = new OSDMap {
                ["hover_height"] = hoverHeight
            };
            request.BeginGetResponse(postData, OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
        }

        #endregion Misc

        #region Packet Handlers

        /// <summary>
        /// Take an incoming ImprovedInstantMessage packet, auto-parse, and if
        /// OnInstantMessage is defined call that with the appropriate arguments
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void InstantMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            if (packet.Type != PacketType.ImprovedInstantMessage) return;

            ImprovedInstantMessagePacket im = (ImprovedInstantMessagePacket)packet;

            if (m_InstantMessage != null)
            {
                InstantMessage message;
                message.FromAgentID = im.AgentData.AgentID;
                message.FromAgentName = Utils.BytesToString(im.MessageBlock.FromAgentName);
                message.ToAgentID = im.MessageBlock.ToAgentID;
                message.ParentEstateID = im.MessageBlock.ParentEstateID;
                message.RegionID = im.MessageBlock.RegionID;
                message.Position = im.MessageBlock.Position;
                message.Dialog = (InstantMessageDialog)im.MessageBlock.Dialog;
                message.GroupIM = im.MessageBlock.FromGroup;
                message.IMSessionID = im.MessageBlock.ID;
                message.Timestamp = new DateTime(im.MessageBlock.Timestamp);
                message.Message = Utils.BytesToString(im.MessageBlock.Message);
                message.Offline = (InstantMessageOnline)im.MessageBlock.Offline;
                message.BinaryBucket = im.MessageBlock.BinaryBucket;

                OnInstantMessage(new InstantMessageEventArgs(message, simulator));
            }
        }

        /// <summary>
        /// Take an incoming Chat packet, auto-parse, and if OnChat is defined call 
        ///   that with the appropriate arguments.
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ChatHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_Chat == null) return;
            Packet packet = e.Packet;

            ChatFromSimulatorPacket chat = (ChatFromSimulatorPacket)packet;

            OnChat(new ChatEventArgs(e.Simulator, Utils.BytesToString(chat.ChatData.Message),
                (ChatAudibleLevel)chat.ChatData.Audible,
                (ChatType)chat.ChatData.ChatType,
                (ChatSourceType)chat.ChatData.SourceType,
                Utils.BytesToString(chat.ChatData.FromName),
                chat.ChatData.SourceID,
                chat.ChatData.OwnerID,
                chat.ChatData.Position));
        }

        /// <summary>
        /// Used for parsing llDialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptDialogHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptDialog == null) return;
            Packet packet = e.Packet;

            ScriptDialogPacket dialog = (ScriptDialogPacket)packet;
            List<string> buttons = dialog.Buttons.Select(button => Utils.BytesToString(button.ButtonLabel)).ToList();

            UUID ownerID = UUID.Zero;

            if (dialog.OwnerData != null && dialog.OwnerData.Length > 0)
            {
                ownerID = dialog.OwnerData[0].OwnerID;
            }

            OnScriptDialog(new ScriptDialogEventArgs(Utils.BytesToString(dialog.Data.Message),
                Utils.BytesToString(dialog.Data.ObjectName),
                dialog.Data.ImageID,
                dialog.Data.ObjectID,
                Utils.BytesToString(dialog.Data.FirstName),
                Utils.BytesToString(dialog.Data.LastName),
                dialog.Data.ChatChannel,
                buttons,
                ownerID));
        }

        /// <summary>
        /// Used for parsing llRequestPermissions dialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptQuestionHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptQuestion == null) return;
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ScriptQuestionPacket question = (ScriptQuestionPacket)packet;

            OnScriptQuestion(new ScriptQuestionEventArgs(simulator,
                question.Data.TaskID,
                question.Data.ItemID,
                Utils.BytesToString(question.Data.ObjectName),
                Utils.BytesToString(question.Data.ObjectOwner),
                (ScriptPermission)question.Data.Questions));
        }

        /// <summary>
        /// Handles Script Control changes when Script with permissions releases or takes a control
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        private void ScriptControlChangeHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptControl == null) return;
            Packet packet = e.Packet;

            ScriptControlChangePacket change = (ScriptControlChangePacket)packet;
            foreach (ScriptControlChangePacket.DataBlock data in change.Data)
            {
                OnScriptControlChange(new ScriptControlEventArgs((ScriptControlChange)data.Controls,
                    data.PassToAgent,
                    data.TakeControls));
            }
        }

        /// <summary>
        /// Used for parsing llLoadURL Dialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void LoadURLHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_LoadURL == null) return;
            Packet packet = e.Packet;

            LoadURLPacket loadURL = (LoadURLPacket)packet;

            OnLoadURL(new LoadUrlEventArgs(
                Utils.BytesToString(loadURL.Data.ObjectName),
                loadURL.Data.ObjectID,
                loadURL.Data.OwnerID,
                loadURL.Data.OwnerIsGroup,
                Utils.BytesToString(loadURL.Data.Message),
                Utils.BytesToString(loadURL.Data.URL)
            ));
        }

        /// <summary>
        /// Update client's Position, LookAt and region handle from incoming packet
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        /// <remarks>This occurs when after an avatar moves into a new sim</remarks>
        private void MovementCompleteHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            AgentMovementCompletePacket movement = (AgentMovementCompletePacket)packet;

            relativePosition = movement.Data.Position;
            Movement.Camera.LookDirection(movement.Data.LookAt);
            simulator.Handle = movement.Data.RegionHandle;
            simulator.SimVersion = Utils.BytesToString(movement.SimData.ChannelVersion);
            simulator.AgentMovementComplete = true;
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void HealthHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            health = ((HealthMessagePacket)packet).HealthData.Health;
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AgentDataUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            AgentDataUpdatePacket p = (AgentDataUpdatePacket)packet;

            if (p.AgentData.AgentID == simulator.Client.Self.AgentID)
            {
                firstName = Utils.BytesToString(p.AgentData.FirstName);
                lastName = Utils.BytesToString(p.AgentData.LastName);
                activeGroup = p.AgentData.ActiveGroupID;
                activeGroupPowers = (GroupPowers)p.AgentData.GroupPowers;

                if (m_AgentData == null) return;

                string groupTitle = Utils.BytesToString(p.AgentData.GroupTitle);
                string groupName = Utils.BytesToString(p.AgentData.GroupName);

                OnAgentData(new AgentDataReplyEventArgs(firstName, lastName, activeGroup, groupTitle, activeGroupPowers, groupName));
            }
            else
            {
                Logger.Log("Got an AgentDataUpdate packet for avatar " + p.AgentData.AgentID.ToString() +
                    " instead of " + Client.Self.AgentID.ToString() + ", this shouldn't happen", Helpers.LogLevel.Error, Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MoneyBalanceReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            if (packet.Type == PacketType.MoneyBalanceReply)
            {
                MoneyBalanceReplyPacket reply = (MoneyBalanceReplyPacket)packet;
                this.balance = reply.MoneyData.MoneyBalance;

                if (m_MoneyBalance != null)
                {
                    TransactionInfo transactionInfo = new TransactionInfo
                    {
                        TransactionType = reply.TransactionInfo.TransactionType,
                        SourceID = reply.TransactionInfo.SourceID,
                        IsSourceGroup = reply.TransactionInfo.IsSourceGroup,
                        DestID = reply.TransactionInfo.DestID,
                        IsDestGroup = reply.TransactionInfo.IsDestGroup,
                        Amount = reply.TransactionInfo.Amount,
                        ItemDescription = Utils.BytesToString(reply.TransactionInfo.ItemDescription)
                    };

                    OnMoneyBalanceReply(new MoneyBalanceReplyEventArgs(reply.MoneyData.TransactionID,
                        reply.MoneyData.TransactionSuccess,
                        reply.MoneyData.MoneyBalance,
                        reply.MoneyData.SquareMetersCredit,
                        reply.MoneyData.SquareMetersCommitted,
                        Utils.BytesToString(reply.MoneyData.Description),
                        transactionInfo));
                }
            }

            if (m_Balance != null)
            {
                OnBalance(new BalanceEventArgs(balance));
            }
        }

        /// <summary>
        /// EQ Message fired with the result of SetDisplayName request
        /// </summary>
        /// <param name="capsKey">The message key</param>
        /// <param name="message">the IMessage object containing the deserialized data sent from the simulator</param>
        /// <param name="simulator">The <see cref="Simulator"/> which originated the packet</param>
        protected void SetDisplayNameReplyEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (m_SetDisplayNameReply == null) return;
            SetDisplayNameReplyMessage msg = (SetDisplayNameReplyMessage)message;
            OnSetDisplayNameReply(new SetDisplayNameReplyEventArgs(msg.Status, msg.Reason, msg.DisplayName));
        }

        protected void AgentStateUpdateEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (message is AgentStateUpdateMessage updateMessage)
            {
                AgentStateStatus = updateMessage;
            }
        }

        protected void EstablishAgentCommunicationEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            EstablishAgentCommunicationMessage msg = (EstablishAgentCommunicationMessage)message;

            if (!Client.Settings.MULTIPLE_SIMS) return;
            IPEndPoint endPoint = new IPEndPoint(msg.Address, msg.Port);
            Simulator sim = Client.Network.FindSimulator(endPoint);

            if (sim == null)
            {
                Logger.Log($"Got EstablishAgentCommunication for unknown sim {msg.Address}:{msg.Port}",
                    Helpers.LogLevel.Error, Client);

                // FIXME: Should we use this opportunity to connect to the simulator?
            }
            else
            {
                Logger.Log("Got EstablishAgentCommunication for " + sim,
                    Helpers.LogLevel.Info, Client);

                sim.SetSeedCaps(msg.SeedCapability.ToString());
            }
        }

        /// <summary>
        /// Process TeleportFailed message sent via EventQueue, informs agent its last teleport has failed and why.
        /// </summary>
        /// <param name="messageKey">The Message Key</param>
        /// <param name="message">An IMessage object Deserialized from the recieved message event</param>
        /// <param name="simulator">The simulator originating the event message</param>
        public void TeleportFailedEventHandler(string messageKey, IMessage message, Simulator simulator)
        {
            TeleportFailedMessage msg = (TeleportFailedMessage)message;

            TeleportFailedPacket failedPacket = new TeleportFailedPacket
            {
                Info =
                {
                    AgentID = msg.AgentID,
                    Reason = Utils.StringToBytes(msg.Reason)
                }
            };

            TeleportHandler(this, new PacketReceivedEventArgs(failedPacket, simulator));
        }

        /// <summary>
        /// Process TeleportFinish from Event Queue and pass it onto our TeleportHandler
        /// </summary>
        /// <param name="capsKey">The message system key for this event</param>
        /// <param name="message">IMessage object containing decoded data from OSD</param>
        /// <param name="simulator">The simulator originating the event message</param>
        private void TeleportFinishEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            TeleportFinishMessage msg = (TeleportFinishMessage)message;

            TeleportFinishPacket p = new TeleportFinishPacket
            {
                Info =
                {
                    AgentID = msg.AgentID,
                    LocationID = (uint) msg.LocationID,
                    RegionHandle = msg.RegionHandle,
                    SeedCapability = Utils.StringToBytes(msg.SeedCapability.ToString()),
                    SimAccess = (byte) msg.SimAccess,
                    SimIP = Utils.IPToUInt(msg.IP),
                    SimPort = (ushort) msg.Port,
                    TeleportFlags = (uint) msg.Flags
                }
            };
            // FIXME: Check This

            // pass the packet onto the teleport handler
            TeleportHandler(this, new PacketReceivedEventArgs(p, simulator));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void TeleportHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            bool finished = false;
            TeleportFlags flags = TeleportFlags.Default;

            if (packet.Type == PacketType.TeleportStart)
            {
                TeleportStartPacket start = (TeleportStartPacket)packet;

                teleportMessage = "Teleport started";
                flags = (TeleportFlags)start.Info.TeleportFlags;
                teleportStat = TeleportStatus.Start;

                Logger.DebugLog($"TeleportStart received, Flags: {flags}", Client);
            }
            else if (packet.Type == PacketType.TeleportProgress)
            {
                TeleportProgressPacket progress = (TeleportProgressPacket)packet;

                teleportMessage = Utils.BytesToString(progress.Info.Message);
                flags = (TeleportFlags)progress.Info.TeleportFlags;
                teleportStat = TeleportStatus.Progress;

                Logger.DebugLog($"TeleportProgress received, Message: {teleportMessage}, Flags: {flags}", Client);
            }
            else if (packet.Type == PacketType.TeleportFailed)
            {
                TeleportFailedPacket failed = (TeleportFailedPacket)packet;

                teleportMessage = Utils.BytesToString(failed.Info.Reason);
                teleportStat = TeleportStatus.Failed;
                finished = true;

                Logger.DebugLog($"TeleportFailed received, Reason: {teleportMessage}", Client);
            }
            else if (packet.Type == PacketType.TeleportFinish)
            {
                TeleportFinishPacket finish = (TeleportFinishPacket)packet;

                flags = (TeleportFlags)finish.Info.TeleportFlags;
                string seedcaps = Utils.BytesToString(finish.Info.SeedCapability);
                finished = true;

                Logger.DebugLog($"TeleportFinish received, Flags: {flags}", Client);

                // Connect to the new sim
                Client.Network.CurrentSim.AgentMovementComplete = false; // we're not there anymore
                Simulator newSimulator = Client.Network.Connect(new IPAddress(finish.Info.SimIP),
                    finish.Info.SimPort, finish.Info.RegionHandle, true, seedcaps);

                if (newSimulator != null)
                {
                    teleportMessage = "Teleport finished";
                    teleportStat = TeleportStatus.Finished;

                    Logger.Log($"Moved to new sim {newSimulator}", Helpers.LogLevel.Info, Client);
                }
                else
                {
                    teleportMessage = "Failed to connect to the new sim after a teleport";
                    teleportStat = TeleportStatus.Failed;

                    // We're going to get disconnected now
                    Logger.Log(teleportMessage, Helpers.LogLevel.Error, Client);
                }
            }
            else if (packet.Type == PacketType.TeleportCancel)
            {
                //TeleportCancelPacket cancel = (TeleportCancelPacket)packet;

                teleportMessage = "Cancelled";
                teleportStat = TeleportStatus.Cancelled;
                finished = true;

                Logger.DebugLog($"TeleportCancel received from {simulator}", Client);
            }
            else if (packet.Type == PacketType.TeleportLocal)
            {
                TeleportLocalPacket local = (TeleportLocalPacket)packet;

                teleportMessage = "Teleport finished";
                flags = (TeleportFlags)local.Info.TeleportFlags;
                teleportStat = TeleportStatus.Finished;
                relativePosition = local.Info.Position;
                Movement.Camera.LookDirection(local.Info.LookAt);
                // This field is apparently not used for anything
                //local.Info.LocationID;
                finished = true;

                Logger.DebugLog($"TeleportLocal received, Flags: {flags}", Client);
            }

            if (m_Teleport != null)
            {
                OnTeleport(new TeleportEventArgs(teleportMessage, teleportStat, flags));
            }

            if (finished) teleportEvent.Set();
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarAnimationHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            AvatarAnimationPacket animation = (AvatarAnimationPacket)packet;

            if (animation.Sender.ID == Client.Self.AgentID)
            {
                lock (SignaledAnimations.Dictionary)
                {
                    // Reset the signaled animation list
                    SignaledAnimations.Dictionary.Clear();

                    for (int i = 0; i < animation.AnimationList.Length; i++)
                    {
                        UUID animID = animation.AnimationList[i].AnimID;
                        int sequenceID = animation.AnimationList[i].AnimSequenceID;

                        // Add this animation to the list of currently signaled animations
                        SignaledAnimations.Dictionary[animID] = sequenceID;

                        if (i < animation.AnimationSourceList.Length)
                        {
                            // FIXME: The server tells us which objects triggered our animations,
                            // we should store this info

                            //animation.AnimationSourceList[i].ObjectID
                        }

                        if (i < animation.PhysicalAvatarEventList.Length)
                        {
                            // FIXME: What is this?
                        }

                        if (!Client.Settings.SEND_AGENT_UPDATES) continue;
                        // We have to manually tell the server to stop playing some animations
                        if (animID == Animations.STANDUP ||
                            animID == Animations.PRE_JUMP ||
                            animID == Animations.LAND ||
                            animID == Animations.MEDIUM_LAND)
                        {
                            Movement.FinishAnim = true;
                            Movement.SendUpdate(true);
                            Movement.FinishAnim = false;
                        }
                    }
                }
            }

            if (m_AnimationsChanged != null)
            {
                ThreadPool.QueueUserWorkItem(delegate(object o)
                { OnAnimationsChanged(new AnimationsChangedEventArgs(this.SignaledAnimations)); });
            }

        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MeanCollisionAlertHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_MeanCollision == null) return;
            Packet packet = e.Packet;
            MeanCollisionAlertPacket collision = (MeanCollisionAlertPacket)packet;

            foreach (MeanCollisionAlertPacket.MeanCollisionBlock block in collision.MeanCollision)
            {
                DateTime time = Utils.UnixTimeToDateTime(block.Time);
                MeanCollisionType type = (MeanCollisionType)block.Type;

                OnMeanCollision(new MeanCollisionEventArgs(type, block.Perp, block.Victim, block.Mag, time));
            }
        }

        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
            LoginResponseData reply)
        {
            id = reply.AgentID;
            sessionID = reply.SessionID;
            secureSessionID = reply.SecureSessionID;
            firstName = reply.FirstName;
            lastName = reply.LastName;
            startLocation = reply.StartLocation;
            agentAccess = reply.AgentAccess;
            Movement.Camera.LookDirection(reply.LookAt);
            homePosition = reply.HomePosition;
            homeLookAt = reply.HomeLookAt;
            lookAt = reply.LookAt;

            if (reply.Gestures != null)
            {
                foreach (var gesture in reply.Gestures)
                {
                    ActiveGestures.Add(gesture.Key, gesture.Value);
                }
            }
        }

        private void Network_OnDisconnected(object sender, DisconnectedEventArgs e)
        {
            // Null out the cached fullName since it can change after logging
            // in again (with a different account name or different login
            // server but using the same GridClient object
            fullName = null;
        }

        /// <summary>
        /// Crossed region handler for message that comes across the EventQueue. Sent to an agent
        /// when the agent crosses a sim border into a new region.
        /// </summary>
        /// <param name="capsKey">The message key</param>
        /// <param name="message">the IMessage object containing the deserialized data sent from the simulator</param>
        /// <param name="simulator">The <see cref="Simulator"/> which originated the packet</param>
        private void CrossedRegionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            CrossedRegionMessage crossed = (CrossedRegionMessage)message;

            IPEndPoint endPoint = new IPEndPoint(crossed.IP, crossed.Port);

            Logger.DebugLog($"Crossed in to new region area, attempting to connect to {endPoint}", Client);

            Simulator oldSim = Client.Network.CurrentSim;
            Simulator newSim = Client.Network.Connect(endPoint, crossed.RegionHandle, true, crossed.SeedCapability.ToString());

            if (newSim != null)
            {
                Logger.Log($"Finished crossing over in to region {newSim}", Helpers.LogLevel.Info, Client);
                oldSim.AgentMovementComplete = false; // We're no longer there
                if (m_RegionCrossed != null)
                {
                    OnRegionCrossed(new RegionCrossedEventArgs(oldSim, newSim));
                }
            }
            else
            {
                // The old simulator will (poorly) handle our movement still, so the connection isn't
                // completely shot yet
                Logger.Log($"Failed to connect to new region {endPoint} after crossing over",
                    Helpers.LogLevel.Warning, Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        /// <remarks>This packet is now being sent via the EventQueue</remarks>
        protected void CrossedRegionHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            CrossedRegionPacket crossing = (CrossedRegionPacket)packet;
            string seedCap = Utils.BytesToString(crossing.RegionData.SeedCapability);
            IPEndPoint endPoint = new IPEndPoint(crossing.RegionData.SimIP, crossing.RegionData.SimPort);

            Logger.DebugLog($"Crossed in to new region area, attempting to connect to {endPoint}", Client);

            Simulator oldSim = Client.Network.CurrentSim;
            Simulator newSim = Client.Network.Connect(endPoint, crossing.RegionData.RegionHandle, true, seedCap);

            if (newSim != null)
            {
                Logger.Log($"Finished crossing over in to region {newSim}", Helpers.LogLevel.Info, Client);

                if (m_RegionCrossed != null)
                {
                    OnRegionCrossed(new RegionCrossedEventArgs(oldSim, newSim));
                }
            }
            else
            {
                // The old simulator will (poorly) handle our movement still, so the connection isn't
                // completely shot yet
                Logger.Log($"Failed to connect to new region {endPoint} after crossing over",
                    Helpers.LogLevel.Warning, Client);
            }
        }

        /// <summary>
        /// Group Chat event handler
        /// </summary>
        /// <param name="capsKey">The capability Key</param>
        /// <param name="message">IMessage object containing decoded data from OSD</param>
        /// <param name="simulator"></param>
        protected void ChatterBoxSessionEventReplyEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            ChatterboxSessionEventReplyMessage msg = (ChatterboxSessionEventReplyMessage)message;

            if (msg.Success) return;
            RequestJoinGroupChat(msg.SessionID);
            Logger.Log($"Attempt to send group chat to non-existant session for group {msg.SessionID}",
                Helpers.LogLevel.Info, Client);
        }

        /// <summary>
        /// Response from request to join a group chat
        /// </summary>
        /// <param name="capsKey"></param>
        /// <param name="message">IMessage object containing decoded data from OSD</param>
        /// <param name="simulator"></param>
        protected void ChatterBoxSessionStartReplyEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            ChatterBoxSessionStartReplyMessage msg = (ChatterBoxSessionStartReplyMessage)message;

            if (msg.Success)
            {
                lock (GroupChatSessions.Dictionary)
                    if (!GroupChatSessions.ContainsKey(msg.SessionID))
                        GroupChatSessions.Add(msg.SessionID, new List<ChatSessionMember>());
            }

            OnGroupChatJoined(new GroupChatJoinedEventArgs(msg.SessionID, msg.SessionName, msg.TempSessionID, msg.Success));
        }

        /// <summary>
        /// Someone joined or left group chat
        /// </summary>
        /// <param name="capsKey"></param>
        /// <param name="message">IMessage object containing decoded data from OSD</param>
        /// <param name="simulator"></param>
        private void ChatterBoxSessionAgentListUpdatesEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            ChatterBoxSessionAgentListUpdatesMessage msg = (ChatterBoxSessionAgentListUpdatesMessage)message;

            lock (GroupChatSessions.Dictionary)
                if (!GroupChatSessions.ContainsKey(msg.SessionID))
                    GroupChatSessions.Add(msg.SessionID, new List<ChatSessionMember>());

            foreach (ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock t in msg.Updates)
            {
                ChatSessionMember fndMbr;
                lock (GroupChatSessions.Dictionary)
                {
                    fndMbr = GroupChatSessions[msg.SessionID].Find(member => member.AvatarKey == t.AgentID);
                }

                if (t.Transition != null)
                {
                    if (t.Transition.Equals("ENTER"))
                    {
                        if (fndMbr.AvatarKey == UUID.Zero)
                        {
                            fndMbr = new ChatSessionMember {AvatarKey = t.AgentID};

                            lock (GroupChatSessions.Dictionary)
                                GroupChatSessions[msg.SessionID].Add(fndMbr);

                            if (m_ChatSessionMemberAdded != null)
                            {
                                OnChatSessionMemberAdded(new ChatSessionMemberAddedEventArgs(msg.SessionID, fndMbr.AvatarKey));
                            }
                        }
                    }
                    else if (t.Transition.Equals("LEAVE"))
                    {
                        if (fndMbr.AvatarKey != UUID.Zero)
                            lock (GroupChatSessions.Dictionary)
                                GroupChatSessions[msg.SessionID].Remove(fndMbr);

                        if (m_ChatSessionMemberLeft != null)
                        {
                            OnChatSessionMemberLeft(new ChatSessionMemberLeftEventArgs(msg.SessionID, t.AgentID));
                        }
                    }
                }

                // handle updates
                ChatSessionMember update_member = GroupChatSessions.Dictionary[msg.SessionID].Find(
                    m => m.AvatarKey == t.AgentID);


                update_member.MuteText = t.MuteText;
                update_member.MuteVoice = t.MuteVoice;

                update_member.CanVoiceChat = t.CanVoiceChat;
                update_member.IsModerator = t.IsModerator;

                // replace existing member record
                lock (GroupChatSessions.Dictionary)
                {
                    int found = GroupChatSessions.Dictionary[msg.SessionID].FindIndex(m => m.AvatarKey == t.AgentID);

                    if (found >= 0)
                        GroupChatSessions.Dictionary[msg.SessionID][found] = update_member;
                }
            }
        }

        /// <summary>
        /// Handle a group chat Invitation
        /// </summary>
        /// <param name="capsKey">Caps Key</param>
        /// <param name="message">IMessage object containing decoded data from OSD</param>
        /// <param name="simulator">Originating Simulator</param>
        private void ChatterBoxInvitationEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (m_InstantMessage == null) return;
            ChatterBoxInvitationMessage msg = (ChatterBoxInvitationMessage)message;

            //TODO: do something about invitations to voice group chat/friends conference
            //Skip for now
            if (msg.Voice) return;

            InstantMessage im = new InstantMessage
            {
                FromAgentID = msg.FromAgentID,
                FromAgentName = msg.FromAgentName,
                ToAgentID = msg.ToAgentID,
                ParentEstateID = msg.ParentEstateID,
                RegionID = msg.RegionID,
                Position = msg.Position,
                Dialog = msg.Dialog,
                GroupIM = msg.GroupIM,
                IMSessionID = msg.IMSessionID,
                Timestamp = msg.Timestamp,
                Message = msg.Message,
                Offline = msg.Offline,
                BinaryBucket = msg.BinaryBucket
            };

            try
            {
                ChatterBoxAcceptInvite(msg.IMSessionID);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed joining IM:", Helpers.LogLevel.Warning, Client, ex);
            }
            OnInstantMessage(new InstantMessageEventArgs(im, simulator));
        }


        /// <summary>
        /// Moderate a chat session
        /// </summary>
        /// <param name="sessionID">the <see cref="UUID"/> of the session to moderate, for group chats this will be the groups UUID</param>
        /// <param name="memberID">the <see cref="UUID"/> of the avatar to moderate</param>
        /// <param name="key">Either "voice" to moderate users voice, or "text" to moderate users text session</param>
        /// <param name="moderate">true to moderate (silence user), false to allow avatar to speak</param>
        public void ModerateChatSessions(UUID sessionID, UUID memberID, string key, bool moderate)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("ChatSessionRequest capability is not currently available");

            CapsClient request = Client.Network.CurrentSim.Caps.CreateCapsClient("ChatSessionRequest");

            if (request != null)
            {
                ChatSessionRequestMuteUpdate req = new ChatSessionRequestMuteUpdate
                {
                    RequestKey = key,
                    RequestValue = moderate,
                    SessionID = sessionID,
                    AgentID = memberID
                };

                request.BeginGetResponse(req.Serialize(), OSDFormat.Xml, Client.Settings.CAPS_TIMEOUT);
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AlertMessage == null) return;
            Packet packet = e.Packet;

            AlertMessagePacket alert = (AlertMessagePacket)packet;

            string message = Utils.BytesToString(alert.AlertData.Message);

            // if we have additional alert data
            if (alert.AlertInfo.Length > 0)
            {
                string notification = Utils.BytesToString(alert.AlertInfo[0].Message);

                switch (notification)
                {
                    // adhere to SL-13824 skip notification when both joining a group and leaving a group
                    case "JoinGroupSuccess":
                    case "GroupDepart":
                        return;
                    // handle Region Restart alerts
                    case "RegionRestartMinutes":
                    {
                        OSDMap osdmap = (OSDMap)OSDParser.Deserialize(alert.AlertInfo[0].ExtraParams);
                        OnRegionRestartAlertMessage(new RegionRestartAlertMessageEventArgs(osdmap["MINUTES"].AsInteger() * 60));
                        break;
                    }
                    case "RegionRestartSeconds":
                    {
                        OSDMap osdmap = (OSDMap)OSDParser.Deserialize(alert.AlertInfo[0].ExtraParams);
                        OnRegionRestartAlertMessage(new RegionRestartAlertMessageEventArgs(osdmap["SECONDS"].AsInteger()));
                        break;
                    }
                }
            }
            OnAlertMessage(new AlertMessageEventArgs(message));
        }

        protected void AgentAlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AlertMessage == null) return;
            Packet packet = e.Packet;

            AgentAlertMessagePacket alert = (AgentAlertMessagePacket)packet;

            // HACK: Agent alerts support modal and Generic Alerts do not, but it's all the same for
            //       my simplified ass right now.
            OnAlertMessage(new AlertMessageEventArgs(Utils.BytesToString(alert.AlertData.Message)));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void CameraConstraintHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_CameraConstraint == null) return;
            Packet packet = e.Packet;

            CameraConstraintPacket camera = (CameraConstraintPacket)packet;
            OnCameraConstraint(new CameraConstraintEventArgs(camera.CameraCollidePlane.Plane));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptSensorReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptSensorReply == null) return;
            Packet packet = e.Packet;

            ScriptSensorReplyPacket reply = (ScriptSensorReplyPacket)packet;

            foreach (ScriptSensorReplyPacket.SensedDataBlock block in reply.SensedData)
            {
                ScriptSensorReplyPacket.RequesterBlock requestor = reply.Requester;

                OnScriptSensorReply(new ScriptSensorReplyEventArgs(requestor.SourceID, block.GroupID, Utils.BytesToString(block.Name),
                    block.ObjectID, block.OwnerID, block.Position, block.Range, block.Rotation, (ScriptSensorTypeFlags)block.Type, block.Velocity));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarSitResponseHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarSitResponse == null) return;
            Packet packet = e.Packet;

            AvatarSitResponsePacket sit = (AvatarSitResponsePacket)packet;

            OnAvatarSitResponse(new AvatarSitResponseEventArgs(sit.SitObject.ID, sit.SitTransform.AutoPilot, sit.SitTransform.CameraAtOffset,
                sit.SitTransform.CameraEyeOffset, sit.SitTransform.ForceMouselook, sit.SitTransform.SitPosition,
                sit.SitTransform.SitRotation));
        }

        protected void MuteListUpdateHander(object sender, PacketReceivedEventArgs e)
        {
            MuteListUpdatePacket packet = (MuteListUpdatePacket)e.Packet;
            if (packet.MuteData.AgentID != Client.Self.AgentID)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(sync =>
            {
                using (AutoResetEvent gotMuteList = new AutoResetEvent(false))
                {
                    string fileName = Utils.BytesToString(packet.MuteData.Filename);
                    string muteList = string.Empty;
                    ulong xferID = 0;
                    byte[] assetData = null;

                    EventHandler<XferReceivedEventArgs> xferCallback = (object xsender, XferReceivedEventArgs xe) =>
                    {
                        if (xe.Xfer.XferID != xferID) return;
                        assetData = xe.Xfer.AssetData;
                        gotMuteList.Set();
                    };


                    Client.Assets.XferReceived += xferCallback;
                    xferID = Client.Assets.RequestAssetXfer(fileName, true, false, UUID.Zero, AssetType.Unknown, true);

                    if (gotMuteList.WaitOne(60 * 1000, false))
                    {
                        muteList = Utils.BytesToString(assetData);

                        lock (MuteList.Dictionary)
                        {
                            MuteList.Dictionary.Clear();
                            foreach (var line in muteList.Split('\n'))
                            {
                                if (line.Trim() == string.Empty) continue;

                                try
                                {
                                    Match m;
                                    if ((m = Regex.Match(line, @"(?<MyteType>\d+)\s+(?<Key>[a-zA-Z0-9-]+)\s+(?<Name>[^|]+)|(?<Flags>.+)", RegexOptions.CultureInvariant)).Success)
                                    {
                                        MuteEntry me = new MuteEntry
                                        {
                                            Type = (MuteType) int.Parse(m.Groups["MyteType"].Value),
                                            ID = new UUID(m.Groups["Key"].Value),
                                            Name = m.Groups["Name"].Value
                                        };
                                        int flags = 0;
                                        int.TryParse(m.Groups["Flags"].Value, out flags);
                                        me.Flags = (MuteFlags)flags;
                                        MuteList[$"{me.ID}|{me.Name}"] = me;
                                    }
                                    else
                                    {
                                        throw new ArgumentException("Invalid mutelist entry line");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log("Failed to parse the mute list line: " + line, Helpers.LogLevel.Warning, Client, ex);
                                }
                            }
                        }

                        OnMuteListUpdated(EventArgs.Empty);
                    }
                    else
                    {
                        Logger.Log("Timed out waiting for mute list download", Helpers.LogLevel.Warning, Client);
                    }

                    Client.Assets.XferReceived -= xferCallback;

                }
            });
        }

        #endregion Packet Handlers
    }

    #region Event Argument Classes
    /// <summary>
    /// Class for sending info on the success of the opration
    /// of setting the maturity access level
    /// </summary>
    public class AgentAccessEventArgs : EventArgs
    {
        /// <summary>
        /// New maturity accesss level returned from the sim
        /// </summary>
        public string NewLevel { get; }

        /// <summary>
        /// True if setting the new maturity access level has succedded
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Creates new instance of the EventArgs class
        /// </summary>
        /// <param name="success">Has setting new maturty access level succeeded</param>
        /// <param name="newLevel">New maturity access level as returned by the simulator</param>
        public AgentAccessEventArgs(bool success, string newLevel)
        {
            NewLevel = newLevel;
            Success = success;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ChatEventArgs : EventArgs
    {
        /// <summary>Get the simulator sending the message</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the message sent</summary>
        public string Message { get; }

        /// <summary>Get the audible level of the message</summary>
        public ChatAudibleLevel AudibleLevel { get; }

        /// <summary>Get the type of message sent: whisper, shout, etc</summary>
        public ChatType Type { get; }

        /// <summary>Get the source type of the message sender</summary>
        public ChatSourceType SourceType { get; }

        /// <summary>Get the name of the agent or object sending the message</summary>
        public string FromName { get; }

        /// <summary>Get the ID of the agent or object sending the message</summary>
        public UUID SourceID { get; }

        /// <summary>Get the ID of the object owner, or the agent ID sending the message</summary>
        public UUID OwnerID { get; }

        /// <summary>Get the position of the agent or object sending the message</summary>
        public Vector3 Position { get; }

        /// <summary>
        /// Construct a new instance of the ChatEventArgs object
        /// </summary>
        /// <param name="simulator">Sim from which the message originates</param>
        /// <param name="message">The message sent</param>
        /// <param name="audible">The audible level of the message</param>
        /// <param name="type">The type of message sent: whisper, shout, etc</param>
        /// <param name="sourceType">The source type of the message sender</param>
        /// <param name="fromName">The name of the agent or object sending the message</param>
        /// <param name="sourceId">The ID of the agent or object sending the message</param>
        /// <param name="ownerid">The ID of the object owner, or the agent ID sending the message</param>
        /// <param name="position">The position of the agent or object sending the message</param>
        public ChatEventArgs(Simulator simulator, string message, ChatAudibleLevel audible, ChatType type,
        ChatSourceType sourceType, string fromName, UUID sourceId, UUID ownerid, Vector3 position)
        {
            Simulator = simulator;
            Message = message;
            AudibleLevel = audible;
            Type = type;
            SourceType = sourceType;
            FromName = fromName;
            SourceID = sourceId;
            Position = position;
            OwnerID = ownerid;
        }
    }

    /// <summary>Contains the data sent when a primitive opens a dialog with this agent</summary>
    public class ScriptDialogEventArgs : EventArgs
    {
        /// <summary>Get the dialog message</summary>
        public string Message { get; }

        /// <summary>Get the name of the object that sent the dialog request</summary>
        public string ObjectName { get; }

        /// <summary>Get the ID of the image to be displayed</summary>
        public UUID ImageID { get; }

        /// <summary>Get the ID of the primitive sending the dialog</summary>
        public UUID ObjectID { get; }

        /// <summary>Get the first name of the senders owner</summary>
        public string FirstName { get; }

        /// <summary>Get the last name of the senders owner</summary>
        public string LastName { get; }

        /// <summary>Get the communication channel the dialog was sent on, responses
        /// should also send responses on this same channel</summary>
        public int Channel { get; }

        /// <summary>Get the string labels containing the options presented in this dialog</summary>
        public List<string> ButtonLabels { get; }

        /// <summary>UUID of the scritped object owner</summary>
        public UUID OwnerID { get; }

        /// <summary>
        /// Construct a new instance of the ScriptDialogEventArgs
        /// </summary>
        /// <param name="message">The dialog message</param>
        /// <param name="objectName">The name of the object that sent the dialog request</param>
        /// <param name="imageID">The ID of the image to be displayed</param>
        /// <param name="objectID">The ID of the primitive sending the dialog</param>
        /// <param name="firstName">The first name of the senders owner</param>
        /// <param name="lastName">The last name of the senders owner</param>
        /// <param name="chatChannel">The communication channel the dialog was sent on</param>
        /// <param name="buttons">The string labels containing the options presented in this dialog</param>
        /// <param name="ownerID">UUID of the scritped object owner</param>
        public ScriptDialogEventArgs(string message, string objectName, UUID imageID,
            UUID objectID, string firstName, string lastName, int chatChannel, List<string> buttons, UUID ownerID)
        {
            Message = message;
            ObjectName = objectName;
            ImageID = imageID;
            ObjectID = objectID;
            FirstName = firstName;
            LastName = lastName;
            Channel = chatChannel;
            ButtonLabels = buttons;
            OwnerID = ownerID;
        }
    }

    /// <summary>Contains the data sent when a primitive requests debit or other permissions
    /// requesting a YES or NO answer</summary>
    public class ScriptQuestionEventArgs : EventArgs
    {
        /// <summary>Get the simulator containing the object sending the request</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the ID of the script making the request</summary>
        public UUID TaskID { get; }

        /// <summary>Get the ID of the primitive containing the script making the request</summary>
        public UUID ItemID { get; }

        /// <summary>Get the name of the primitive making the request</summary>
        public string ObjectName { get; }

        /// <summary>Get the name of the owner of the object making the request</summary>
        public string ObjectOwnerName { get; }

        /// <summary>Get the permissions being requested</summary>
        public ScriptPermission Questions { get; }

        /// <summary>
        /// Construct a new instance of the ScriptQuestionEventArgs
        /// </summary>
        /// <param name="simulator">The simulator containing the object sending the request</param>
        /// <param name="taskID">The ID of the script making the request</param>
        /// <param name="itemID">The ID of the primitive containing the script making the request</param>
        /// <param name="objectName">The name of the primitive making the request</param>
        /// <param name="objectOwner">The name of the owner of the object making the request</param>
        /// <param name="questions">The permissions being requested</param>
        public ScriptQuestionEventArgs(Simulator simulator, UUID taskID, UUID itemID, string objectName, string objectOwner, ScriptPermission questions)
        {
            Simulator = simulator;
            TaskID = taskID;
            ItemID = itemID;
            ObjectName = objectName;
            ObjectOwnerName = objectOwner;
            Questions = questions;
        }

    }

    /// <summary>Contains the data sent when a primitive sends a request 
    /// to an agent to open the specified URL</summary>
    public class LoadUrlEventArgs : EventArgs
    {
        /// <summary>Get the name of the object sending the request</summary>
        public string ObjectName { get; }

        /// <summary>Get the ID of the object sending the request</summary>
        public UUID ObjectID { get; }

        /// <summary>Get the ID of the owner of the object sending the request</summary>
        public UUID OwnerID { get; }

        /// <summary>True if the object is owned by a group</summary>
        public bool OwnerIsGroup { get; }

        /// <summary>Get the message sent with the request</summary>
        public string Message { get; }

        /// <summary>Get the URL the object sent</summary>
        public string URL { get; }

        /// <summary>
        /// Construct a new instance of the LoadUrlEventArgs
        /// </summary>
        /// <param name="objectName">The name of the object sending the request</param>
        /// <param name="objectID">The ID of the object sending the request</param>
        /// <param name="ownerID">The ID of the owner of the object sending the request</param>
        /// <param name="ownerIsGroup">True if the object is owned by a group</param>
        /// <param name="message">The message sent with the request</param>
        /// <param name="url">The URL the object sent</param>
        public LoadUrlEventArgs(string objectName, UUID objectID, UUID ownerID, bool ownerIsGroup, string message, string url)
        {
            ObjectName = objectName;
            ObjectID = objectID;
            OwnerID = ownerID;
            OwnerIsGroup = ownerIsGroup;
            Message = message;
            URL = url;
        }
    }

    /// <summary>The date received from an ImprovedInstantMessage</summary>
    public class InstantMessageEventArgs : EventArgs
    {
        /// <summary>Get the InstantMessage object</summary>
        public InstantMessage IM { get; }

        /// <summary>Get the simulator where the InstantMessage origniated</summary>
        public Simulator Simulator { get; }

        /// <summary>
        /// Construct a new instance of the InstantMessageEventArgs object
        /// </summary>
        /// <param name="im">the InstantMessage object</param>
        /// <param name="simulator">the simulator where the InstantMessage origniated</param>
        public InstantMessageEventArgs(InstantMessage im, Simulator simulator)
        {
            IM = im;
            Simulator = simulator;
        }
    }

    /// <summary>Contains the currency balance</summary>
    public class BalanceEventArgs : EventArgs
    {
        /// <summary>
        /// Get the currenct balance
        /// </summary>
        public int Balance { get; }

        /// <summary>
        /// Construct a new BalanceEventArgs object
        /// </summary>
        /// <param name="balance">The currenct balance</param>
        public BalanceEventArgs(int balance)
        {
            Balance = balance;
        }
    }

    /// <summary>Contains the transaction summary when an item is purchased, 
    /// money is given, or land is purchased</summary>
    public class MoneyBalanceReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the transaction</summary>
        public UUID TransactionID { get; }

        /// <summary>True of the transaction was successful</summary>
        public bool Success { get; }

        /// <summary>Get the remaining currency balance</summary>
        public int Balance { get; }

        /// <summary>Get the meters credited</summary>
        public int MetersCredit { get; }

        /// <summary>Get the meters comitted</summary>
        public int MetersCommitted { get; }

        /// <summary>Get the description of the transaction</summary>
        public string Description { get; }

        /// <summary>Detailed transaction information</summary>
        public TransactionInfo TransactionInfo { get; }

        /// <summary>
        /// Construct a new instance of the MoneyBalanceReplyEventArgs object
        /// </summary>
        /// <param name="transactionID">The ID of the transaction</param>
        /// <param name="transactionSuccess">True of the transaction was successful</param>
        /// <param name="balance">The current currency balance</param>
        /// <param name="metersCredit">The meters credited</param>
        /// <param name="metersCommitted">The meters comitted</param>
        /// <param name="description">A brief description of the transaction</param>
        /// <param name="transactionInfo">Transaction info</param>
        public MoneyBalanceReplyEventArgs(UUID transactionID, bool transactionSuccess, int balance, int metersCredit, int metersCommitted, string description, TransactionInfo transactionInfo)
        {
            TransactionID = transactionID;
            Success = transactionSuccess;
            Balance = balance;
            MetersCredit = metersCredit;
            MetersCommitted = metersCommitted;
            Description = description;
            TransactionInfo = transactionInfo;
        }
    }

    // string message, TeleportStatus status, TeleportFlags flags
    public class TeleportEventArgs : EventArgs
    {
        public string Message { get; }
        public TeleportStatus Status { get; }
        public TeleportFlags Flags { get; }

        public TeleportEventArgs(string message, TeleportStatus status, TeleportFlags flags)
        {
            Message = message;
            Status = status;
            Flags = flags;
        }
    }

    /// <summary>Data sent from the simulator containing information about your agent and active group information</summary>
    public class AgentDataReplyEventArgs : EventArgs
    {
        /// <summary>Get the agents first name</summary>
        public string FirstName { get; }

        /// <summary>Get the agents last name</summary>
        public string LastName { get; }

        /// <summary>Get the active group ID of your agent</summary>
        public UUID ActiveGroupID { get; }

        /// <summary>Get the active groups title of your agent</summary>
        public string GroupTitle { get; }

        /// <summary>Get the combined group powers of your agent</summary>
        public GroupPowers GroupPowers { get; }

        /// <summary>Get the active group name of your agent</summary>
        public string GroupName { get; }

        /// <summary>
        /// Construct a new instance of the AgentDataReplyEventArgs object
        /// </summary>
        /// <param name="firstName">The agents first name</param>
        /// <param name="lastName">The agents last name</param>
        /// <param name="activeGroupID">The agents active group ID</param>
        /// <param name="groupTitle">The group title of the agents active group</param>
        /// <param name="groupPowers">The combined group powers the agent has in the active group</param>
        /// <param name="groupName">The name of the group the agent has currently active</param>
        public AgentDataReplyEventArgs(string firstName, string lastName, UUID activeGroupID,
            string groupTitle, GroupPowers groupPowers, string groupName)
        {
            FirstName = firstName;
            LastName = lastName;
            ActiveGroupID = activeGroupID;
            GroupTitle = groupTitle;
            GroupPowers = groupPowers;
            GroupName = groupName;
        }
    }

    /// <summary>Data sent by the simulator to indicate the active/changed animations
    /// applied to your agent</summary>
    public class AnimationsChangedEventArgs : EventArgs
    {
        /// <summary>Get the dictionary that contains the changed animations</summary>
        public InternalDictionary<UUID, int> Animations { get; }

        /// <summary>
        /// Construct a new instance of the AnimationsChangedEventArgs class
        /// </summary>
        /// <param name="agentAnimations">The dictionary that contains the changed animations</param>
        public AnimationsChangedEventArgs(InternalDictionary<UUID, int> agentAnimations)
        {
            Animations = agentAnimations;
        }

    }

    /// <summary>
    /// Data sent from a simulator indicating a collision with your agent
    /// </summary>
    public class MeanCollisionEventArgs : EventArgs
    {
        /// <summary>Get the Type of collision</summary>
        public MeanCollisionType Type { get; }

        /// <summary>Get the ID of the agent or object that collided with your agent</summary>
        public UUID Aggressor { get; }

        /// <summary>Get the ID of the agent that was attacked</summary>
        public UUID Victim { get; }

        /// <summary>A value indicating the strength of the collision</summary>
        public float Magnitude { get; }

        /// <summary>Get the time the collision occurred</summary>
        public DateTime Time { get; }

        /// <summary>
        /// Construct a new instance of the MeanCollisionEventArgs class
        /// </summary>
        /// <param name="type">The type of collision that occurred</param>
        /// <param name="perp">The ID of the agent or object that perpetrated the agression</param>
        /// <param name="victim">The ID of the Victim</param>
        /// <param name="magnitude">The strength of the collision</param>
        /// <param name="time">The Time the collision occurred</param>
        public MeanCollisionEventArgs(MeanCollisionType type, UUID perp, UUID victim,
            float magnitude, DateTime time)
        {
            Type = type;
            Aggressor = perp;
            Victim = victim;
            Magnitude = magnitude;
            Time = time;
        }
    }

    /// <summary>Data sent to your agent when it crosses region boundaries</summary>
    public class RegionCrossedEventArgs : EventArgs
    {
        /// <summary>Get the simulator your agent just left</summary>
        public Simulator OldSimulator { get; }

        /// <summary>Get the simulator your agent is now in</summary>
        public Simulator NewSimulator { get; }

        /// <summary>
        /// Construct a new instance of the RegionCrossedEventArgs class
        /// </summary>
        /// <param name="oldSim">The simulator your agent just left</param>
        /// <param name="newSim">The simulator your agent is now in</param>
        public RegionCrossedEventArgs(Simulator oldSim, Simulator newSim)
        {
            OldSimulator = oldSim;
            NewSimulator = newSim;
        }
    }

    /// <summary>Data sent from the simulator when your agent joins a group chat session</summary>
    public class GroupChatJoinedEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group chat session</summary>
        public UUID SessionID { get; }

        /// <summary>Get the name of the session</summary>
        public string SessionName { get; }

        /// <summary>Get the temporary session ID used for establishing new sessions</summary>
        public UUID TmpSessionID { get; }

        /// <summary>True if your agent successfully joined the session</summary>
        public bool Success { get; }

        /// <summary>
        /// Construct a new instance of the GroupChatJoinedEventArgs class
        /// </summary>
        /// <param name="groupChatSessionID">The ID of the session</param>
        /// <param name="sessionName">The name of the session</param>
        /// <param name="tmpSessionID">A temporary session id used for establishing new sessions</param>
        /// <param name="success">True of your agent successfully joined the session</param>
        public GroupChatJoinedEventArgs(UUID groupChatSessionID, string sessionName, UUID tmpSessionID, bool success)
        {
            SessionID = groupChatSessionID;
            SessionName = sessionName;
            TmpSessionID = tmpSessionID;
            Success = success;
        }
    }

    /// <summary>Data sent by the simulator containing urgent messages</summary>
    public class AlertMessageEventArgs : EventArgs
    {
        /// <summary>Get the alert message</summary>
        public string Message { get; }

        /// <summary>
        /// Construct a new instance of the AlertMessageEventArgs class
        /// </summary>
        /// <param name="message">The alert message</param>
        public AlertMessageEventArgs(string message)
        {
            Message = message;
        }
    }

    /// <summary>Data sent by the simulator containing region restart alert</summary>
    public class RegionRestartAlertMessageEventArgs : EventArgs
    {
        /// <summary>Get the alert message</summary>
        public int SecondsRemaining { get; }

        /// <summary>
        /// Construct a new instance of the RegionRestartAlertMessageEventArgs class
        /// </summary>
        /// <param name="seconds_remaining">Seconds countdown to region restart</param>
        public RegionRestartAlertMessageEventArgs(int seconds_remaining)
        {
            SecondsRemaining = seconds_remaining;
        }
    }

    /// <summary>Data sent by a script requesting to take or release specified controls to your agent</summary>
    public class ScriptControlEventArgs : EventArgs
    {
        /// <summary>Get the controls the script is attempting to take or release to the agent</summary>
        public ScriptControlChange Controls { get; }

        /// <summary>True if the script is passing controls back to the agent</summary>
        public bool Pass { get; }

        /// <summary>True if the script is requesting controls be released to the script</summary>
        public bool Take { get; }

        /// <summary>
        /// Construct a new instance of the ScriptControlEventArgs class
        /// </summary>
        /// <param name="controls">The controls the script is attempting to take or release to the agent</param>
        /// <param name="pass">True if the script is passing controls back to the agent</param>
        /// <param name="take">True if the script is requesting controls be released to the script</param>
        public ScriptControlEventArgs(ScriptControlChange controls, bool pass, bool take)
        {
            Controls = controls;
            Pass = pass;
            Take = take;
        }
    }

    /// <summary>
    /// Data sent from the simulator to an agent to indicate its view limits
    /// </summary>
    public class CameraConstraintEventArgs : EventArgs
    {
        /// <summary>Get the collision plane</summary>
        public Vector4 CollidePlane { get; }

        /// <summary>
        /// Construct a new instance of the CameraConstraintEventArgs class
        /// </summary>
        /// <param name="collidePlane">The collision plane</param>
        public CameraConstraintEventArgs(Vector4 collidePlane)
        {
            CollidePlane = collidePlane;
        }
    }

    /// <summary>
    /// Data containing script sensor requests which allow an agent to know the specific details
    /// of a primitive sending script sensor requests
    /// </summary>
    public class ScriptSensorReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the primitive sending the sensor</summary>
        public UUID RequestorID { get; }

        /// <summary>Get the ID of the group associated with the primitive</summary>
        public UUID GroupID { get; }

        /// <summary>Get the name of the primitive sending the sensor</summary>
        public string Name { get; }

        /// <summary>Get the ID of the primitive sending the sensor</summary>
        public UUID ObjectID { get; }

        /// <summary>Get the ID of the owner of the primitive sending the sensor</summary>
        public UUID OwnerID { get; }

        /// <summary>Get the position of the primitive sending the sensor</summary>
        public Vector3 Position { get; }

        /// <summary>Get the range the primitive specified to scan</summary>
        public float Range { get; }

        /// <summary>Get the rotation of the primitive sending the sensor</summary>
        public Quaternion Rotation { get; }

        /// <summary>Get the type of sensor the primitive sent</summary>
        public ScriptSensorTypeFlags Type { get; }

        /// <summary>Get the velocity of the primitive sending the sensor</summary>
        public Vector3 Velocity { get; }

        /// <summary>
        /// Construct a new instance of the ScriptSensorReplyEventArgs
        /// </summary>
        /// <param name="requestorID">The ID of the primitive sending the sensor</param>
        /// <param name="groupID">The ID of the group associated with the primitive</param>
        /// <param name="name">The name of the primitive sending the sensor</param>
        /// <param name="objectID">The ID of the primitive sending the sensor</param>
        /// <param name="ownerID">The ID of the owner of the primitive sending the sensor</param>
        /// <param name="position">The position of the primitive sending the sensor</param>
        /// <param name="range">The range the primitive specified to scan</param>
        /// <param name="rotation">The rotation of the primitive sending the sensor</param>
        /// <param name="type">The type of sensor the primitive sent</param>
        /// <param name="velocity">The velocity of the primitive sending the sensor</param>
        public ScriptSensorReplyEventArgs(UUID requestorID, UUID groupID, string name,
            UUID objectID, UUID ownerID, Vector3 position, float range, Quaternion rotation,
            ScriptSensorTypeFlags type, Vector3 velocity)
        {
            RequestorID = requestorID;
            GroupID = groupID;
            Name = name;
            ObjectID = objectID;
            OwnerID = ownerID;
            Position = position;
            Range = range;
            Rotation = rotation;
            Type = type;
            Velocity = velocity;
        }
    }

    /// <summary>Contains the response data returned from the simulator in response to a <see cref="RequestSit"/></summary>
    public class AvatarSitResponseEventArgs : EventArgs
    {
        /// <summary>Get the ID of the primitive the agent will be sitting on</summary>
        public UUID ObjectID { get; }

        /// <summary>True if the simulator Autopilot functions were involved</summary>
        public bool Autopilot { get; }

        /// <summary>Get the camera offset of the agent when seated</summary>
        public Vector3 CameraAtOffset { get; }

        /// <summary>Get the camera eye offset of the agent when seated</summary>
        public Vector3 CameraEyeOffset { get; }

        /// <summary>True of the agent will be in mouselook mode when seated</summary>
        public bool ForceMouselook { get; }

        /// <summary>Get the position of the agent when seated</summary>
        public Vector3 SitPosition { get; }

        /// <summary>Get the rotation of the agent when seated</summary>
        public Quaternion SitRotation { get; }

        /// <summary>Construct a new instance of the AvatarSitResponseEventArgs object</summary>
        public AvatarSitResponseEventArgs(UUID objectID, bool autoPilot, Vector3 cameraAtOffset,
            Vector3 cameraEyeOffset, bool forceMouselook, Vector3 sitPosition, Quaternion sitRotation)
        {
            ObjectID = objectID;
            Autopilot = autoPilot;
            CameraAtOffset = cameraAtOffset;
            CameraEyeOffset = cameraEyeOffset;
            ForceMouselook = forceMouselook;
            SitPosition = sitPosition;
            SitRotation = sitRotation;
        }
    }

    /// <summary>Data sent when an agent joins a chat session your agent is currently participating in</summary>
    public class ChatSessionMemberAddedEventArgs : EventArgs
    {
        /// <summary>Get the ID of the chat session</summary>
        public UUID SessionID { get; }

        /// <summary>Get the ID of the agent that joined</summary>
        public UUID AgentID { get; }

        /// <summary>
        /// Construct a new instance of the ChatSessionMemberAddedEventArgs object
        /// </summary>
        /// <param name="sessionID">The ID of the chat session</param>
        /// <param name="agentID">The ID of the agent joining</param>
        public ChatSessionMemberAddedEventArgs(UUID sessionID, UUID agentID)
        {
            SessionID = sessionID;
            AgentID = agentID;
        }
    }

    /// <summary>Data sent when an agent exits a chat session your agent is currently participating in</summary>
    public class ChatSessionMemberLeftEventArgs : EventArgs
    {
        /// <summary>Get the ID of the chat session</summary>
        public UUID SessionID { get; }

        /// <summary>Get the ID of the agent that left</summary>
        public UUID AgentID { get; }

        /// <summary>
        /// Construct a new instance of the ChatSessionMemberLeftEventArgs object
        /// </summary>
        /// <param name="sessionID">The ID of the chat session</param>
        /// <param name="agentID">The ID of the Agent that left</param>
        public ChatSessionMemberLeftEventArgs(UUID sessionID, UUID agentID)
        {
            SessionID = sessionID;
            AgentID = agentID;
        }
    }

    /// <summary>Event arguments with the result of setting display name operation</summary>
    public class SetDisplayNameReplyEventArgs : EventArgs
    {
        /// <summary>Status code, 200 indicates settign display name was successful</summary>
        public int Status { get; }

        /// <summary>Textual description of the status</summary>
        public string Reason { get; }

        /// <summary>Details of the newly set display name</summary>
        public AgentDisplayName DisplayName { get; }

        /// <summary>Default constructor</summary>
        public SetDisplayNameReplyEventArgs(int status, string reason, AgentDisplayName displayName)
        {
            Status = status;
            Reason = reason;
            DisplayName = displayName;
        }
    }

    #endregion
}
