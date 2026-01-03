/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2026, Sjofn LLC
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
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Packets;
using OpenMetaverse.Messages.Linden;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    /// <summary>
    /// Manager class for our own avatar
    /// </summary>
    public partial class AgentManager
    {
        #region Delegates
        /// <summary>
        /// Called once attachment resource usage information has been collected
        /// </summary>
        /// <param name="success">Indicates if operation was successful</param>
        /// <param name="info">Attachment resource usage information</param>
        public delegate void AttachmentResourcesCallback(bool success, AttachmentResourcesMessage info);
        public delegate void AgentAccessCallback(AgentAccessEventArgs e);
        #endregion Delegates

        #region Event Delegates

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ScriptDialogEventArgs> m_ScriptDialog;

        /// <summary>Raises the ScriptDialog event</summary>
        /// <param name="e">A ScriptDialogEventArgs object containing the
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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
        /// <summary>Raised when an ImprovedInstantMessage packet is received from the simulator, this is used for everything from
        /// private messaging to friendship offers. The Dialog field defines what type of message has arrived</summary>
        public event EventHandler<InstantMessageEventArgs> IM
        {
            add { lock (m_InstantMessageLock) { m_InstantMessage += value; } }
            remove { lock (m_InstantMessageLock) { m_InstantMessage -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
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
            remove { lock (m_TeleportLock) { m_Teleport -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<CameraConstraintEventArgs> m_CameraConstraint;

        /// <summary>Raises the CameraConstraint event</summary>
        /// <param name="e">A CameraConstraintEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnCameraConstraint(CameraConstraintEventArgs e)
        {
            EventHandler<CameraConstraintEventArgs> handler = m_CameraConstraint;
            handler?.Invoke(this, e);
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

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<ScriptSensorReplyEventArgs> m_ScriptSensorReply;

        /// <summary>Raises the ScriptSensorReply event</summary>
        /// <param name="e">A ScriptSensorReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnScriptSensorReply(ScriptSensorReplyEventArgs e)
        {
            EventHandler<ScriptSensorReplyEventArgs> handler = m_ScriptSensorReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ScriptSensorReplyLock = new object();

        /// <summary>Raised when a script sensor reply is received from a simulator</summary>
        public event EventHandler<ScriptSensorReplyEventArgs> ScriptSensorReply
        {
            add { lock (m_ScriptSensorReplyLock) { m_ScriptSensorReply += value; } }
            remove { lock (m_ScriptSensorReplyLock) { m_ScriptSensorReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<AvatarSitResponseEventArgs> m_AvatarSitResponse;

        /// <summary>Raises the AvatarSitResponse event</summary>
        /// <param name="e">A AvatarSitResponseEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnAvatarSitResponse(AvatarSitResponseEventArgs e)
        {
            EventHandler<AvatarSitResponseEventArgs> handler = m_AvatarSitResponse;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarSitResponseLock = new object();

        /// <summary>Raised in response to a <see cref="RequestSit"/> request</summary>
        public event EventHandler<AvatarSitResponseEventArgs> AvatarSitResponse
        {
            add { lock (m_AvatarSitResponseLock) { m_AvatarSitResponse += value; } }
            remove { lock (m_AvatarSitResponseLock) { m_AvatarSitResponse -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        /// <summary>The event subscribers, null if no subscribers</summary>
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

        /// <summary>The event subscribers. null if no subscribers</summary>
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

        private const string AGENT_PROFILE_CAP = "AgentProfile";
        private const string UPDATE_AGENT_PROFILE_IMG_CAP = "UploadAgentProfileImage";

        /// <summary>Reference to the GridClient instance</summary>
        private readonly GridClient Client;
        /// <summary>Used for movement and camera tracking</summary>
        public readonly AgentMovement Movement;
        /// <summary>Currently playing animations for the agent. Can be used to
        /// check the current movement status such as walking, hovering, aiming,
        /// etc. by checking against system animations found in the Animations class</summary>
        public LockingDictionary<UUID, int> SignaledAnimations = new LockingDictionary<UUID, int>();
        /// <summary>Dictionary containing current Group Chat sessions and members</summary>
        public LockingDictionary<UUID, List<ChatSessionMember>> GroupChatSessions = new LockingDictionary<UUID, List<ChatSessionMember>>();
        /// <summary>Dictionary containing mute list keyed on mute name and key</summary>
        public LockingDictionary<string, MuteEntry> MuteList = new LockingDictionary<string, MuteEntry>();
        public LockingDictionary<UUID, UUID> ActiveGestures { get; } = new LockingDictionary<UUID, UUID>();

        #region Properties

        /// <summary>Your (client) avatar's <see cref="UUID"/></summary>
        /// <remarks>"client", "agent", and "avatar" all represent the same thing</remarks>
        public UUID AgentID { get; private set; }

        /// <summary>Temporary <see cref="UUID"/> assigned to this session, used for 
        /// verifying our identity in packets</summary>
        public UUID SessionID { get; private set; }

        /// <summary>Shared secret <see cref="UUID"/> that is never sent over the wire</summary>
        public UUID SecureSessionID { get; private set; }

        /// <summary>Your (client) avatar ID, local to the current region/sim</summary>
        public uint LocalID => localID;

        /// <summary>Where the avatar started at login. Can be "last", "home" 
        /// or a login <see cref="T:OpenMetaverse.URI"/></summary>
        public string StartLocation { get; private set; } = string.Empty;

        /// <summary>The access level of this agent, usually M, PG or A</summary>
        public string AgentAccess { get; private set; } = string.Empty;

        /// <summary>The CollisionPlane of Agent</summary>
        public Vector4 CollisionPlane => collisionPlane;

        public DateTime LastPositionUpdate { get; set; }

        /// <summary>An <see cref="Vector3"/> representing the velocity of our agent</summary>
        public Vector3 Velocity => velocity;

        /// <summary>An <see cref="Vector3"/> representing the acceleration of our agent</summary>
        public Vector3 Acceleration => acceleration;

        /// <summary>A <see cref="Vector3"/> which specifies the angular speed, and axis about which an Avatar is rotating.</summary>
        public Vector3 AngularVelocity => angularVelocity;

        /// <summary>Region handle for 'home' region</summary>
        public ulong HomeRegionHandle => home.RegionHandle;

        /// <summary>Position avatar client will goto when login to 'home' or during
        /// teleport request to 'home' region.</summary>
        public Vector3 HomePosition => home.Position;

        /// <summary>LookAt point saved/restored with HomePosition</summary>
        public Vector3 HomeLookAt => home.LookAt;

        /// <summary>Avatar First Name (i.e. Philip)</summary>
        public string FirstName { get; private set; } = string.Empty;

        /// <summary>Avatar Last Name (i.e. Linden)</summary>
        public string LastName { get; private set; } = string.Empty;

        /// <summary>LookAt point received with the login response message</summary>
        public Vector3 LookAt { get; private set; }

        /// <summary>Avatar Full Name (i.e. Philip Linden)</summary>
        public string Name
        {
            get
            {
                // This is a fairly common request, so assume the name doesn't
                // change mid-session and cache the result
                if (fullName == null || fullName.Length < 2)
                    fullName = $"{FirstName} {LastName}";
                return fullName;
            }
        }
        /// <summary>Gets the health of the agent</summary>
        public float Health { get; private set; }

        /// <summary>Gets the current balance of the agent</summary>
        public int Balance { get; private set; }

        /// <summary>Gets the local ID of the prim the agent is sitting on,
        /// zero if the avatar is not currently sitting</summary>
        public uint SittingOn => sittingOn;

        /// <summary>Gets the <see cref="UUID"/> of the agents active group.</summary>
        public UUID ActiveGroup { get; private set; }

        /// <summary>Gets the Agents powers in the currently active group</summary>
        public GroupPowers ActiveGroupPowers { get; private set; }

        /// <summary>Current status message for teleporting</summary>
        public string TeleportMessage { get; private set; } = string.Empty;

        /// <summary>Current position of the agent as a relative offset from
        /// the simulator, or the parent object if we are sitting on something</summary>
        public Vector3 RelativePosition { get => relativePosition;
            set => relativePosition = value;
        }
        /// <summary>
        /// Calculates the relative position of the agent, with velocity and
        /// the time since the last update factored in. This is an estimate,
        /// and could 'overshoot'; however it is much more likely to be correct
        /// than RelativePosition while an agent is moving.
        /// </summary>
        public Vector3 RelativePositionEstimate
        {
            get => relativePosition + (velocity * (float)(DateTime.UtcNow - LastPositionUpdate).TotalSeconds);
            set => relativePosition = value;
        }
        /// <summary>Current rotation of the agent as a relative rotation from
        /// the simulator, or the parent object if we are sitting on something</summary>
        public Quaternion RelativeRotation { get => relativeRotation;
            set => relativeRotation = value;
        }

        /// <summary>
        /// Helper to resolve the simulator context for packet handlers. Prefer the simulator supplied
        /// by PacketReceivedEventArgs (e.Simulator), then an explicit simulator parameter, and
        /// finally fall back to the current network simulator for compatibility.
        /// </summary>
        /// <param name="e">PacketReceivedEventArgs containing the simulator</param>
        /// <param name="simulator">Explicit simulator parameter</param>
        /// <returns>Resolved simulator or current simulator as fallback</returns>
        private Simulator ResolveSimulator(PacketReceivedEventArgs e = null, Simulator simulator = null)
        {
            // First priority: simulator from PacketReceivedEventArgs
            if (e != null && e.Simulator != null)
                return e.Simulator;

            // Second priority: explicitly passed simulator
            if (simulator != null)
                return simulator;

            // Last resort: current simulator
            return Client?.Network?.CurrentSim;
        }

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

                // a bit more complicated, agent sitting on a prim
                Vector3 fullPosition = relativePosition;

                if (Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(sittingOn, out var p))
                {
                    fullPosition = p.Position + relativePosition * p.Rotation;
                }

                // go up the hierarchy trying to find the root prim
                while (p != null && p.ParentID != 0)
                {
                    if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(p.ParentID, out var av))
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

                // Didn't find the seat's root prim, try returning coarse location
                if (Client.Network.CurrentSim.avatarPositions.TryGetValue(AgentID, out fullPosition))
                {
                    return fullPosition;
                }

                Logger.Warn("Failed to determine agent sim position", Client);
                return relativePosition;
            }
        }
        /// <summary>
        /// A <see cref="Quaternion"/> representing the agents current rotation
        /// </summary>
        public Quaternion SimRotation
        {
            get
            {
                if (sittingOn != 0)
                {
                    if (Client.Network.CurrentSim != null 
                        && Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(sittingOn, out var parent))
                    {
                        return relativeRotation * parent.Rotation;
                    }
                    Logger.Warn($"Currently sitting on object {sittingOn} which is not tracked, SimRotation will be inaccurate", Client);
                }
                return relativeRotation;
            }
        }
        /// <summary>Returns the global grid position of the avatar</summary>
        public Vector3d GlobalPosition
        {
            get
            {
                if (Client.Network.CurrentSim == null)
                {
                    return Vector3d.Zero;
                }
                Utils.LongToUInts(Client.Network.CurrentSim.Handle, out var globalX, out var globalY);
                Vector3 pos = SimPosition;

                return new Vector3d(
                    globalX + pos.X,
                    globalY + pos.Y,
                    pos.Z);
            }
        }

        /// <summary>Various abilities and preferences sent by the grid</summary>
        public AgentStateUpdateMessage AgentStateStatus;

        public AccountLevelBenefits Benefits { get; protected set; }
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

        private HomeInfo home;
        private string fullName;
        private TeleportStatus teleportStatus = TeleportStatus.None;
        private readonly ManualResetEvent teleportEvent = new ManualResetEvent(false);
        private uint heightWidthGenCounter;
        private bool disposed = false;

        #endregion Private Members

        /// <summary>
        /// Constructor, setup callbacks for packets related to our avatar
        /// </summary>
        /// <param name="client">A reference to the <see cref="T:OpenMetaverse.GridClient"/> Class</param>
        public AgentManager(GridClient client)
        {
            Client = client;

            Movement = new AgentMovement(Client);
            
            // Initialize region crossing state machine
            InitializeCrossingStateMachine();

            Client.Network.Disconnected += Network_OnDisconnected;

            // Teleport callbacks            
            Client.Network.RegisterCallback(PacketType.TeleportStart, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportProgress, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportFailed, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportCancel, TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportLocal, TeleportHandler);
            // these come in via the EventQueue
            Client.Network.RegisterEventCallback("TeleportFailed", TeleportFailedEventHandler);
            Client.Network.RegisterEventCallback("TeleportFinish", TeleportFinishEventHandler);

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
            Client.Network.RegisterCallback(PacketType.ScriptSensorReply, AvatarSitResponseHandler);
            Client.Network.RegisterCallback(PacketType.AvatarSitResponse, AvatarSitResponseHandler);
            // Process mute list update message
            Client.Network.RegisterCallback(PacketType.MuteListUpdate, MuteListUpdateHandler);
        }

        #region Movement Actions

        /// <summary>
        /// Sends a request to sit on the specified object
        /// </summary>
        /// <param name="targetID"><see cref="UUID"/> of the object to sit on</param>
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
        /// Follows a call to <see cref="RequestSit"/> to actually sit on the object
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
                Logger.Warn("Attempted to Stand() but agent updates are disabled", Client);
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
                Parameter = Utils.StringToBytes(globalX.ToString(CultureInfo.InvariantCulture))
            };
            autopilot.ParamList[1] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(globalY.ToString(CultureInfo.InvariantCulture))
            };
            autopilot.ParamList[2] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(z.ToString(CultureInfo.InvariantCulture))
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
                Parameter = Utils.StringToBytes(z.ToString(CultureInfo.InvariantCulture))
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
            Utils.LongToUInts(Client.Network.CurrentSim.Handle, out var x, out var y);
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
                Logger.Warn("Attempted to AutoPilotCancel() but agent updates are disabled", Client);
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
        /// <param name="objectID"><see cref="UUID"/> of the object to drag</param>
        /// <param name="grabPosition">Drag target in region coordinates</param>
        public void GrabUpdate(UUID objectID, Vector3 grabPosition)
        {
            GrabUpdate(objectID, grabPosition, Vector3.Zero, Vector3.Zero, Vector3.Zero, 
                0, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }

        /// <summary>
        /// Overload: Drag an object
        /// </summary>
        /// <param name="objectID"><see cref="UUID"/> of the object to drag</param>
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
        /// <seealso cref="AgentManager.Grab"/>
        /// <seealso cref="AgentManager.GrabUpdate"/>
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

        #region Animations

        /// <summary>
        /// Send an AgentAnimation packet that toggles a single animation on
        /// </summary>
        /// <param name="animation">The <see cref="UUID"/> of the animation to start playing</param>
        /// <param name="reliable">Whether to ensure delivery of this packet or not</param>
        public void AnimationStart(UUID animation, bool reliable)
        {
            var animations = new Dictionary<UUID, bool> {[animation] = true};

            Animate(animations, reliable);
        }

        /// <summary>
        /// Send an AgentAnimation packet that toggles a single animation off
        /// </summary>
        /// <param name="animation">The <see cref="UUID"/> of a 
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
        /// <param name="animations">A list of animation <see cref="UUID"/>s, and whether to
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
            animate.PhysicalAvatarEventList = Array.Empty<AgentAnimationPacket.PhysicalAvatarEventListBlock>();

            Client.Network.SendPacket(animate);
        }

        #endregion Animations

        #region Mute List

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

        #endregion Mute List

        #region Misc

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
        /// Sets home location to agents current position
        /// </summary>
        /// <remarks>will fire an AlertMessage (<see cref="OpenMetaverse.AgentManager.OnAlertMessage"/>) with 
        /// success or failure message</remarks>
        public void SetHome()
        {
            SetStartLocationRequestPacket s = new SetStartLocationRequestPacket
            {
                AgentData = new SetStartLocationRequestPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                StartLocationData = new SetStartLocationRequestPacket.StartLocationDataBlock
                {
                    LocationPos = Client.Self.SimPosition,
                    LocationID = 1,
                    SimName = Utils.StringToBytes(string.Empty),
                    LocationLookAt = Movement.Camera.AtAxis
                }
            };
            Client.Network.SendPacket(s);
        }

        /// <summary>
        /// Move an agent in to a simulator. This packet is the last packet
        /// needed to complete the transition in to a new simulator
        /// </summary>
        /// <param name="simulator"><see cref="OpenMetaverse.Simulator"/> Object</param>
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
            Logger.Info($"Sending complete agent movement to {simulator.Handle} / {simulator.Name}", Client);
        }

        /// <summary>
        /// Reply to script permissions request
        /// </summary>
        /// <param name="simulator"><see cref="OpenMetaverse.Simulator"/> Object</param>
        /// <param name="itemID"><see cref="UUID"/> of the itemID requesting permissions</param>
        /// <param name="taskID"><see cref="UUID"/> of the taskID requesting permissions</param>
        /// <param name="permissions"><see cref="ScriptPermission"/> list of permissions to allow</param>
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
        /// <param name="imSessionID">IM session ID from the group invitation message</param>
        /// <param name="accept">true to accept the group invitation, false to deny it</param>
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

            Client.Network.SendPacket(request, sim);
        }

        /// <summary>
        /// Fetches resource usage by agents attachments
        /// </summary>
        /// <param name="callback">Called when the requested information is collected</param>
        /// <param name="cancellationToken">Cancellation token for capability requests</param>
        public async Task GetAttachmentResources(AttachmentResourcesCallback callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var cap = Client?.Network?.CurrentSim?.Caps?.CapabilityURI("AttachmentResources");
                if (cap == null)
                {
                    Logger.Warn("AttachmentResources capability not available, cannot fetch attachment resources.", Client);
                    callback(false, null);
                    return;
                }

                await Client.HttpCapsClient.GetRequestAsync(cap, cancellationToken,
                    (response, data, error) =>
                    {
                        if (error != null)
                        {
                            callback(false, null);
                            return;
                        }

                        if (response == null)
                        {
                            Logger.Warn("AttachmentResources request failed: no response.", Client);
                            callback(false, null);
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Logger.Warn($"AttachmentResources request returned non-success status: {response.StatusCode}", Client);
                            callback(false, null);
                            return;
                        }

                        try
                        {
                            OSD result = OSDParser.Deserialize(data);
                            AttachmentResourcesMessage info = AttachmentResourcesMessage.FromOSD(result);
                            callback(true, info);

                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed fetching AttachmentResources", ex, Client);
                            callback(false, null);
                        }
                    }).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Failed fetching AttachmentResources", ex, Client);
                callback(false, null);
                throw;
            }
        }

        /// <summary>
        /// Initiates request to set a new display name
        /// </summary>
        /// <param name="oldName">Previous display name</param>
        /// <param name="newName">Desired new display name</param>
        /// <param name="cancellationToken"></param>
        [Obsolete("Use SetDisplayNameAsync instead", false)]
        public void SetDisplayName(string oldName, string newName, CancellationToken cancellationToken = default)
        {
            // Synchronous wrapper for compatibility
            SetDisplayNameAsync(oldName, newName, cancellationToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Initiates request to set a new display name (async)
        /// </summary>
        public async Task SetDisplayNameAsync(string oldName, string newName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Client?.Network?.CurrentSim?.Caps == null)
            {
                Logger.Warn("Not connected to simulator to set display name.", Client);
                return;
            }

            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("SetDisplayName");
            if (cap == null)
            {
                Logger.Warn("Unable to obtain capability to set display name.", Client);
                return;
            }

            SetDisplayNameMessage msg = new SetDisplayNameMessage
            {
                OldDisplayName = oldName,
                NewDisplayName = newName
            };

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, msg.Serialize(), cancellationToken).ConfigureAwait(false);
        }
 
         /// <summary>
         /// Tells the sim what UI language is used, and if it's ok to share that with scripts
         /// </summary>
         /// <param name="language">Two letter language code</param>
         /// <param name="isPublic">Share language info with scripts</param>
         /// <param name="cancellationToken"></param>
        [Obsolete("Use UpdateAgentLanguageAsync instead", false)]
        public void UpdateAgentLanguage(string language, bool isPublic, CancellationToken cancellationToken = default)
        {
            UpdateAgentLanguageAsync(language, isPublic, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task UpdateAgentLanguageAsync(string language, bool isPublic, CancellationToken cancellationToken = default)
         {
             cancellationToken.ThrowIfCancellationRequested();
             try
             {
                 UpdateAgentLanguageMessage msg = new UpdateAgentLanguageMessage
                 {
                     Language = language,
                     LanguagePublic = isPublic
                 };

                 Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateAgentLanguage");
                 if (cap == null) { return; }

                 await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, msg.Serialize(), cancellationToken).ConfigureAwait(false);
             }
             catch ( Exception ex) when (!(ex is OperationCanceledException))
             {
                 Logger.Error("Failed to update agent language", ex, Client);
                 throw;
             }
         }

        /// <summary>
        /// Sets agents maturity access level
        /// </summary>
        /// <param name="access">PG, M or A</param>
        /// <param name="callback">Callback function</param>
        /// <param name="cancellationToken"></param>
        public async Task SetAgentAccessAsync(string access, AgentAccessCallback callback, CancellationToken cancellationToken = default)
         {
             if (Client == null || !Client.Network.Connected || Client.Network.CurrentSim?.Caps == null) { return; }

             cancellationToken.ThrowIfCancellationRequested();

             OSDMap payload = new OSDMap
             {
                 ["access_prefs"] = new OSDMap { ["max"] = access }
             };
             Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("UpdateAgentInformation");
             if (cap == null) { return; }

             await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, cancellationToken,
                 (response, data, error) =>
                 {
                     bool success = true;

                     if (error != null)
                     {
                         Logger.Warn("Failed setting max maturity access.", Client);
                         success = false;
                     }
                     else if (response == null)
                     {
                         Logger.Warn("UpdateAgentInformation returned no response.", Client);
                         success = false;
                     }
                     else if (!response.IsSuccessStatusCode)
                     {
                         Logger.Warn($"UpdateAgentInformation returned non-success status: {response.StatusCode}", Client);
                         success = false;
                     }
                     else
                     {
                         try
                         {
                             OSD result = OSDParser.Deserialize(data);
                             if (result is OSDMap osdMap && osdMap.TryGetValue("access_prefs", out var mapObj) && mapObj is OSDMap accessMap && accessMap.TryGetValue("max", out var maxVal))
                             {
                                 AgentAccess = maxVal;
                                 Logger.Info($"Max maturity access set to {AgentAccess}", Client);
                             }
                             else
                             {
                                 Logger.Info($"Max maturity unchanged at {AgentAccess}", Client);
                             }
                         }
                         catch (Exception ex)
                         {
                             Logger.Warn($"Failed to parse UpdateAgentInformation response: {ex.Message}", Client);
                             success = false;
                         }
                     }

                     if (callback != null)
                     {
                         try { callback(new AgentAccessEventArgs(success, AgentAccess)); }
                         catch { } // *TODO: So gross
                     }
                 }).ConfigureAwait(false);
         }

        /// <summary>
        /// Sets agents hover height.
        /// </summary>
        /// <param name="hoverHeight">Hover height [-2.0, 2.0]</param>
        /// <param name="cancellationToken"></param>
        [Obsolete("Use SetHoverHeightAsync instead", false)]
        public void SetHoverHeight(double hoverHeight, CancellationToken cancellationToken = default)
        {
            SetHoverHeightAsync(hoverHeight, cancellationToken).GetAwaiter().GetResult();
        }

        public async Task SetHoverHeightAsync(double hoverHeight, CancellationToken cancellationToken = default)
         {
             if (Client == null || !Client.Network.Connected || Client.Network.CurrentSim?.Caps == null) { return; }

             cancellationToken.ThrowIfCancellationRequested();

            var postData = new OSDMap { ["hover_height"] = hoverHeight };

            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("AgentPreferences");
            if (cap == null) { return; }

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, postData, cancellationToken,
                 (response, data, error) =>
             {
                 if (error != null)
                 {
                     Logger.Warn($"Failed to set hover height: {error}.", Client);
                     return;
                 }

                 if (response == null)
                 {
                     Logger.Warn("Failed to set hover height: no response.", Client);
                     return;
                 }

                 if (!response.IsSuccessStatusCode)
                 {
                     Logger.Warn($"Failed to set hover height: status {response.StatusCode}.", Client);
                     return;
                 }

                 OSD result;
                 try
                 {
                     result = OSDParser.Deserialize(data);
                 }
                 catch (Exception ex)
                 {
                     Logger.Warn($"Failed to parse hover height response: {ex.Message}", Client);
                     return;
                 }

                 if (!(result is OSDMap resultMap))
                 {
                     Logger.Warn($"Failed to set hover height: Expected {nameof(OSDMap)} response, but got {result.Type}", Client);
                 }
                 else
                 {
                     var confirmedHeight = resultMap["hover_height"];
                     Logger.Debug($"Hover height set to {confirmedHeight}", Client);
                 }
             }).ConfigureAwait(false);
         }

        #endregion Misc

        /// <summary>
        /// Dispose AgentManager and unregister all network callbacks/events.
        /// Safe to call multiple times.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                try
                {
                    if (Client?.Network == null)
                    {
                        disposed = true;
                        return;
                    }

                    // Unsubscribe simple events
                    try { Client.Network.Disconnected -= Network_OnDisconnected; } catch { }

                    // Unregister packet callbacks
                    try { Client.Network.UnregisterCallback(PacketType.TeleportStart, TeleportHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.TeleportProgress, TeleportHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.TeleportFailed, TeleportHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.TeleportCancel, TeleportHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.TeleportLocal, TeleportHandler); } catch { }

                    // Unregister event queue callbacks
                    try { Client.Network.UnregisterEventCallback("TeleportFailed", TeleportFailedEventHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("TeleportFinish", TeleportFinishEventHandler); } catch { }

                    // Instant message callback
                    try { Client.Network.UnregisterCallback(PacketType.ImprovedInstantMessage, InstantMessageHandler); } catch { }
                    // Chat callback
                    try { Client.Network.UnregisterCallback(PacketType.ChatFromSimulator, ChatHandler); } catch { }
                    // Script dialog callback
                    try { Client.Network.UnregisterCallback(PacketType.ScriptDialog, ScriptDialogHandler); } catch { }
                    // Script question callback
                    try { Client.Network.UnregisterCallback(PacketType.ScriptQuestion, ScriptQuestionHandler); } catch { }
                    // Script URL callback
                    try { Client.Network.UnregisterCallback(PacketType.LoadURL, LoadURLHandler); } catch { }
                    // Movement complete callback
                    try { Client.Network.UnregisterCallback(PacketType.AgentMovementComplete, MovementCompleteHandler); } catch { }
                    // Health callback
                    try { Client.Network.UnregisterCallback(PacketType.HealthMessage, HealthHandler); } catch { }
                    // Money callback
                    try { Client.Network.UnregisterCallback(PacketType.MoneyBalanceReply, MoneyBalanceReplyHandler); } catch { }
                    //Agent update callback
                    try { Client.Network.UnregisterCallback(PacketType.AgentDataUpdate, AgentDataUpdateHandler); } catch { }
                    // Animation callback
                    try { Client.Network.UnregisterCallback(PacketType.AvatarAnimation, AvatarAnimationHandler); } catch { }
                    // Object colliding into our agent callback
                    try { Client.Network.UnregisterCallback(PacketType.MeanCollisionAlert, MeanCollisionAlertHandler); } catch { }
                    // Region Crossing
                    try { Client.Network.UnregisterCallback(PacketType.CrossedRegion, CrossedRegionHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("CrossedRegion", CrossedRegionEventHandler); } catch { }
                    // CAPS event callbacks
                    try { Client.Network.UnregisterEventCallback("EstablishAgentCommunication", EstablishAgentCommunicationEventHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("SetDisplayNameReply", SetDisplayNameReplyEventHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("AgentStateUpdate", AgentStateUpdateEventHandler); } catch { }
                    // Incoming Group Chat
                    try { Client.Network.UnregisterEventCallback("ChatterBoxInvitation", ChatterBoxInvitationEventHandler); } catch { }
                    // Outgoing Group Chat Reply
                    try { Client.Network.UnregisterEventCallback("ChatterBoxSessionEventReply", ChatterBoxSessionEventReplyEventHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("ChatterBoxSessionStartReply", ChatterBoxSessionStartReplyEventHandler); } catch { }
                    try { Client.Network.UnregisterEventCallback("ChatterBoxSessionAgentListUpdates", ChatterBoxSessionAgentListUpdatesEventHandler); } catch { }
                    // Login
                    try { Client.Network.UnregisterLoginResponseCallback(Network_OnLoginResponse); } catch { }
                    // Alert Messages
                    try { Client.Network.UnregisterCallback(PacketType.AlertMessage, AlertMessageHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.AgentAlertMessage, AgentAlertMessageHandler); } catch { }
                    // script control change messages, ie: when an in-world LSL script wants to take control of your agent.
                    try { Client.Network.UnregisterCallback(PacketType.ScriptControlChange, ScriptControlChangeHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.CameraConstraint, CameraConstraintHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.ScriptSensorReply, AvatarSitResponseHandler); } catch { }
                    try { Client.Network.UnregisterCallback(PacketType.AvatarSitResponse, AvatarSitResponseHandler); } catch { }
                    // Process mute list update message
                    try { Client.Network.UnregisterCallback(PacketType.MuteListUpdate, MuteListUpdateHandler); } catch { }

                    // Clear local collections
                    try { GroupChatSessions.Dictionary.Clear(); } catch { }
                    try { MuteList.Dictionary.Clear(); } catch { }
                    try { ActiveGestures.Dictionary.Clear(); } catch { }
                    try { SignaledAnimations.Dictionary.Clear(); } catch { }
                    try { gestureCache.Clear(); } catch { }

                    // Clear multi-sim tracking data
                    try { _simulatorStates.Clear(); } catch { }
                    try { _childAgentStatus.Clear(); } catch { }
                    
                    // Clean up crossing state machine
                    try 
                    { 
                        _crossingTimeoutTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                        _crossingTimeoutTimer?.Dispose();
                        _crossingTimeoutTimer = null;
                    } 
                    catch { }
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception while disposing AgentManager", ex, Client);
                }
            }

            disposed = true;
        }

        /// <summary>
        /// Dispose AgentManager and unregister all network callbacks/events.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are released if Dispose is not called.
        /// </summary>
        ~AgentManager()
        {
            Dispose(false);
        }
    }
}
