/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn, LLC
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Interfaces;

namespace OpenMetaverse
{
    #region Structs

    /// <summary>
    /// Avatar group management
    /// </summary>
    public struct GroupMember
    {
        /// <summary>Key of Group Member</summary>
        public UUID ID;
        /// <summary>Total land contribution</summary>
        public int Contribution;
        /// <summary>Online status information</summary>
        public string OnlineStatus;
        /// <summary>Abilities that the Group Member has</summary>
        public GroupPowers Powers;
        /// <summary>Current group title</summary>
        public string Title;
        /// <summary>Is a group owner</summary>
        public bool IsOwner;
    }

    /// <summary>
    /// Role manager for a group
    /// </summary>
    public struct GroupRole
    {
        /// <summary>Key of the group</summary>
        public UUID GroupID;
        /// <summary>Key of Role</summary>
        public UUID ID;
        /// <summary>Name of Role</summary>
        public string Name;
        /// <summary>Group Title associated with Role</summary>
        public string Title;
        /// <summary>Description of Role</summary>
        public string Description;
        /// <summary>Abilities Associated with Role</summary>
        public GroupPowers Powers;
        /// <summary>Returns the role's title</summary>
        /// <returns>The role's title</returns>
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Class to represent Group Title
    /// </summary>
    public struct GroupTitle
    {
        /// <summary>Key of the group</summary>
        public UUID GroupID;
        /// <summary>ID of the role title belongs to</summary>
        public UUID RoleID;
        /// <summary>Group Title</summary>
        public string Title;
        /// <summary>Whether title is Active</summary>
        public bool Selected;
        /// <summary>Returns group title</summary>
        public override string ToString()
        {
            return Title;
        }
    }

    /// <summary>
    /// Represents a group on the grid
    /// </summary>
    public struct Group
    {
        /// <summary>Key of Group</summary>
        public UUID ID;
        /// <summary>Key of Group Insignia</summary>
        public UUID InsigniaID;
        /// <summary>Key of Group Founder</summary>
        public UUID FounderID;
        /// <summary>Key of Group Role for Owners</summary>
        public UUID OwnerRole;
        /// <summary>Name of Group</summary>
        public string Name;
        /// <summary>Text of Group Charter</summary>
        public string Charter;
        /// <summary>Title of "everyone" role</summary>
        public string MemberTitle;
        /// <summary>Is the group open for enrolement to everyone</summary>
        public bool OpenEnrollment;
        /// <summary>Will group show up in search</summary>
        public bool ShowInList;
        /// <summary>Group powers</summary>
        public GroupPowers Powers;
        /// <summary>Accepts notices</summary>
        public bool AcceptNotices;
        /// <summary>Allow publish</summary>
        public bool AllowPublish;
        /// <summary>Is the group Mature</summary>
        public bool MaturePublish;
        /// <summary>Cost of group membership</summary>
        public int MembershipFee;
        /// <summary>Group money</summary>
        public int Money;
        /// <summary>Group contribution</summary>
        public int Contribution;
        /// <summary>The total number of current members this group has</summary>
        public int GroupMembershipCount;
        /// <summary>The number of roles this group has configured</summary>
        public int GroupRolesCount;
        /// <summary>Show this group in agent's profile</summary>
        public bool ListInProfile;

        /// <summary>Returns the name of the group</summary>
        /// <returns>A string containing the name of the group</returns>
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// A group Vote
    /// </summary>
    public struct Vote
    {
        /// <summary>Key of Avatar who created Vote</summary>
        public UUID Candidate;
        /// <summary>Text of the Vote proposal</summary>
        public string VoteString;
        /// <summary>Total number of votes</summary>
        public int NumVotes;
    }

    /// <summary>
    /// A group proposal
    /// </summary>
    public struct GroupProposal
    {
        /// <summary>The Text of the proposal</summary>
        public string VoteText;
        /// <summary>The minimum number of members that must vote before proposal passes or failes</summary>
        public int Quorum;
        /// <summary>The required ration of yes/no votes required for vote to pass</summary>
        /// <remarks>The three options are Simple Majority, 2/3 Majority, and Unanimous</remarks>
        /// TODO: this should be an enum
        public float Majority;
        /// <summary>The duration in days votes are accepted</summary>
        public int Duration;
    }

    /// <summary>
    /// Account summary
    /// </summary>
    public struct GroupAccountSummary
    {
        /// <summary></summary>
        public int IntervalDays;
        /// <summary></summary>
        public int CurrentInterval;
        /// <summary></summary>
        public string StartDate;
        /// <summary></summary>
        public int Balance;
        /// <summary></summary>
        public int TotalCredits;
        /// <summary></summary>
        public int TotalDebits;
        /// <summary></summary>
        public int ObjectTaxCurrent;
        /// <summary></summary>
        public int LightTaxCurrent;
        /// <summary></summary>
        public int LandTaxCurrent;
        /// <summary></summary>
        public int GroupTaxCurrent;
        /// <summary></summary>
        public int ParcelDirFeeCurrent;
        /// <summary></summary>
        public int ObjectTaxEstimate;
        /// <summary></summary>
        public int LightTaxEstimate;
        /// <summary></summary>
        public int LandTaxEstimate;
        /// <summary></summary>
        public int GroupTaxEstimate;
        /// <summary></summary>
        public int ParcelDirFeeEstimate;
        /// <summary></summary>
        public int NonExemptMembers;
        /// <summary></summary>
        public string LastTaxDate;
        /// <summary></summary>
        public string TaxDate;
    }

    /// <summary>
    /// Struct representing a group notice
    /// </summary>
    public struct GroupNotice
    {
        /// <summary>Subject of notice</summary>
        public string Subject;
        /// <summary>Notice message</summary>
        public string Message;
        /// <summary>Object ID of attachment</summary>
        public UUID AttachmentID;
        /// <summary>Notice owner</summary>
        public UUID OwnerID;

        /// <summary>
        /// OSD Serialize object for attaching
        /// </summary>
        /// <returns></returns>
        public byte[] SerializeAttachment()
        {
            if (OwnerID == UUID.Zero || AttachmentID == UUID.Zero)
                return Utils.EmptyBytes;

            OSDMap att = new OSDMap
            {
                {"item_id", OSD.FromUUID(AttachmentID)},
                {"owner_id", OSD.FromUUID(OwnerID)}
            };

            return OSDParser.SerializeLLSDXmlBytes(att);

            /*
            //I guess this is how this works, no gaurentees
            string lsd = "<llsd><item_id>" + AttachmentID.ToString() + "</item_id><owner_id>"
                + OwnerID.ToString() + "</owner_id></llsd>";
            return Utils.StringToBytes(lsd);
             */
        }
    }

    /// <summary>
    /// Struct representing a group notice list entry
    /// </summary>
    public struct GroupNoticesListEntry
    {
        /// <summary>Notice ID</summary>
        public UUID NoticeID;
        /// <summary>Creation timestamp of notice</summary>
        public uint Timestamp;
        /// <summary>Agent name who created notice</summary>
        public string FromName;
        /// <summary>Notice subject</summary>
        public string Subject;
        /// <summary>Is there an attachment?</summary>
        public bool HasAttachment;
        /// <summary>Attachment Type</summary>
        public AssetType AssetType;

    }

    /// <summary>
    /// Struct representing a member of a group chat session and their settings
    /// </summary>
    public struct ChatSessionMember
    {
        /// <summary>The <see cref="UUID"/> of the Avatar</summary>
        public UUID AvatarKey;
        /// <summary>True if user has voice chat enabled</summary>
        public bool CanVoiceChat;
        /// <summary>True of Avatar has moderator abilities</summary>
        public bool IsModerator;
        /// <summary>True if a moderator has muted this avatars chat</summary>
        public bool MuteText;
        /// <summary>True if a moderator has muted this avatars voice</summary>
        public bool MuteVoice;
    }

    #endregion Structs

    #region Enums

    /// <summary>
    /// Role update flags
    /// </summary>
    public enum GroupRoleUpdate : uint
    {
        /// <summary></summary>
        NoUpdate,
        /// <summary></summary>
        UpdateData,
        /// <summary></summary>
        UpdatePowers,
        /// <summary></summary>
        UpdateAll,
        /// <summary></summary>
        Create,
        /// <summary></summary>
        Delete
    }

    [Flags]
    public enum GroupPowers : ulong
    {
        None = 0,

        // Membership
        /// <summary>Can send invitations to groups default role</summary>
        Invite = 1UL << 1,
        /// <summary>Can eject members from group</summary>
        Eject = 1UL << 2,
        /// <summary>Can toggle 'Open Enrollment' and change 'Signup fee'</summary>
        ChangeOptions = 1UL << 3,
        /// <summary>Member is visible in the public member list</summary>
        MemberVisible = 1UL << 47,

        // Roles
        /// <summary>Can create new roles</summary>
        CreateRole = 1UL << 4,
        /// <summary>Can delete existing roles</summary>
        DeleteRole = 1UL << 5,
        /// <summary>Can change Role names, titles and descriptions</summary>
        RoleProperties = 1UL << 6,
        /// <summary>Can assign other members to assigners role</summary>
        AssignMemberLimited = 1UL << 7,
        /// <summary>Can assign other members to any role</summary>
        AssignMember = 1UL << 8,
        /// <summary>Can remove members from roles</summary>
        RemoveMember = 1UL << 9,
        /// <summary>Can assign and remove abilities in roles</summary>
        ChangeActions = 1UL << 10,

        // Identity
        /// <summary>Can change group Charter, Insignia, 'Publish on the web' and which
        /// members are publicly visible in group member listings</summary>
        ChangeIdentity = 1UL << 11,

        // Parcel management
        /// <summary>Can buy land or deed land to group</summary>
        LandDeed = 1UL << 12,
        /// <summary>Can abandon group owned land to Governor Linden on mainland, or Estate owner for
        /// private estates</summary>
        LandRelease = 1UL << 13,
        /// <summary>Can set land for-sale information on group owned parcels</summary>
        LandSetSale = 1UL << 14,
        /// <summary>Can subdivide and join parcels</summary>
        LandDivideJoin = 1UL << 15,

        // Parcel identity
        /// <summary>Can toggle "Show in Find Places" and set search category</summary>
        FindPlaces = 1UL << 17,
        /// <summary>Can change parcel name, description, and 'Publish on web' settings</summary>
        LandChangeIdentity = 1UL << 18,
        /// <summary>Can set the landing point and teleport routing on group land</summary>
        SetLandingPoint = 1UL << 19,

        // Parcel settings
        /// <summary>Can change music and media settings</summary>
        ChangeMedia = 1UL << 20,
        /// <summary>Can toggle 'Edit Terrain' option in Land settings</summary>
        LandEdit = 1UL << 21,
        /// <summary>Can toggle various About Land > Options settings</summary>
        LandOptions = 1UL << 22,

        // Parcel powers
        /// <summary>Can always terraform land, even if parcel settings have it turned off</summary>
        AllowEditLand = 1UL << 23,
        /// <summary>Can always fly while over group owned land</summary>
        AllowFly = 1UL << 24,
        /// <summary>Can always rez objects on group owned land</summary>
        AllowRez = 1UL << 25,
        /// <summary>Can always create landmarks for group owned parcels</summary>
        AllowLandmark = 1UL << 26,
        /// <summary>Can set home location on any group owned parcel</summary>
        AllowSetHome = 1UL << 28,


        // Parcel access
        /// <summary>Can modify public access settings for group owned parcels</summary>
        LandManageAllowed = 1UL << 29,
        /// <summary>Can manager parcel ban lists on group owned land</summary>
        LandManageBanned = 1UL << 30,
        /// <summary>Can manage pass list sales information</summary>
        LandManagePasses = 1UL << 31,
        /// <summary>Can eject and freeze other avatars on group owned land</summary>
        LandEjectAndFreeze = 1UL << 32,

        // Parcel content
        /// <summary>Can return objects set to group</summary>
        ReturnGroupSet = 1UL << 33,
        /// <summary>Can return non-group owned/set objects</summary>
        ReturnNonGroup = 1UL << 34,
        /// <summary>Can return group owned objects</summary>
        ReturnGroupOwned = 1UL << 48,

        /// <summary>Can landscape using Linden plants</summary>
        LandGardening = 1UL << 35,

        // Object Management
        /// <summary>Can deed objects to group</summary>
        DeedObject = 1UL << 36,
        /// <summary>Can move group owned objects</summary>
        ObjectManipulate = 1UL << 38,
        /// <summary>Can set group owned objects for-sale</summary>
        ObjectSetForSale = 1UL << 39,
        
        // Accounting
        /// <summary>Pay group liabilities and receive group dividends</summary>
        Accountable = 1UL << 40,

        /// <summary>List and Host group events</summary>
        HostEvent = 1UL << 41,

        // Notices and proposals
        /// <summary>Can send group notices</summary>
        SendNotices = 1UL << 42,
        /// <summary>Can receive group notices</summary>
        ReceiveNotices = 1UL << 43,
        /// <summary>Can create group proposals</summary>
        StartProposal = 1UL << 44,
        /// <summary>Can vote on group proposals</summary>
        VoteOnProposal = 1UL << 45,
        
        // Group chat moderation related
        /// <summary>Can join group chat sessions</summary>
        JoinChat = 1UL << 16,
        /// <summary>Can use voice chat in Group Chat sessions</summary>
        AllowVoiceChat = 1UL << 27,
        /// <summary>Can moderate group chat sessions</summary>
        ModerateChat = 1UL << 37,
        
        // Experiences
        /// <summary>Has admin rights to any experiences owned by this group</summary>
        ExperienceAdmin = 1UL << 49,
        /// <summary>Can sign scripts for experiences owned by this group</summary>
        ExperienceCreator = 1UL << 50,
        
        // Group Banning
        /// <summary>Allows access to ban / un-ban agents from a group</summary>
        GroupBanAccess = 1UL << 51
    }

    /// <summary>
    /// Ban actions available for group members
    /// </summary>
    public enum GroupBanAction : int
    {
        /// <summary> Ban agent from joining a group </summary>
        Ban = 1,
        /// <summary> Remove restriction on agent jointing a group </summary>
        Unban = 2,
    }

    #endregion Enums

    /// <summary>
    /// Handles all network traffic related to reading and writing group
    /// information
    /// </summary>
    public class GroupManager
    {
        #region Delegates

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<CurrentGroupsEventArgs> m_CurrentGroups;

        /// <summary>Raises the CurrentGroups event</summary>
        /// <param name="e">A CurrentGroupsEventArgs object containing the
        /// data sent from the simulator</param>
        protected virtual void OnCurrentGroups(CurrentGroupsEventArgs e)
        {
            EventHandler<CurrentGroupsEventArgs> handler = m_CurrentGroups;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_CurrentGroupsLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// our current group membership</summary>
        public event EventHandler<CurrentGroupsEventArgs> CurrentGroups
        {
            add { lock (m_CurrentGroupsLock) { m_CurrentGroups += value; } }
            remove { lock (m_CurrentGroupsLock) { m_CurrentGroups -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupNamesEventArgs> m_GroupNames;

        /// <summary>Raises the GroupNamesReply event</summary>
        /// <param name="e">A GroupNamesEventArgs object containing the
        /// data response from the simulator</param>
        protected virtual void OnGroupNamesReply(GroupNamesEventArgs e)
        {
            EventHandler<GroupNamesEventArgs> handler = m_GroupNames;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupNamesLock = new object();

        /// <summary>Raised when the simulator responds to a RequestGroupName 
        /// or RequestGroupNames request</summary>
        public event EventHandler<GroupNamesEventArgs> GroupNamesReply
        {
            add { lock (m_GroupNamesLock) { m_GroupNames += value; } }
            remove { lock (m_GroupNamesLock) { m_GroupNames -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupProfileEventArgs> m_GroupProfile;

        /// <summary>Raises the GroupProfile event</summary>
        /// <param name="e">An GroupProfileEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupProfile(GroupProfileEventArgs e)
        {
            EventHandler<GroupProfileEventArgs> handler = m_GroupProfile;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupProfileLock = new object();

        /// <summary>Raised when the simulator responds to a <see cref="RequestGroupProfile"/> request</summary>
        public event EventHandler<GroupProfileEventArgs> GroupProfile
        {
            add { lock (m_GroupProfileLock) { m_GroupProfile += value; } }
            remove { lock (m_GroupProfileLock) { m_GroupProfile -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupMembersReplyEventArgs> m_GroupMembers;

        /// <summary>Raises the GroupMembers event</summary>
        /// <param name="e">A GroupMembersEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupMembersReply(GroupMembersReplyEventArgs e)
        {
            EventHandler<GroupMembersReplyEventArgs> handler = m_GroupMembers;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupMembersLock = new object();

        /// <summary>Raised when the simulator responds to a <see cref="RequestGroupMembers"/> request</summary>
        public event EventHandler<GroupMembersReplyEventArgs> GroupMembersReply
        {
            add { lock (m_GroupMembersLock) { m_GroupMembers += value; } }
            remove { lock (m_GroupMembersLock) { m_GroupMembers -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupRolesDataReplyEventArgs> m_GroupRoles;

        /// <summary>Raises the GroupRolesDataReply event</summary>
        /// <param name="e">A GroupRolesDataReplyEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupRoleDataReply(GroupRolesDataReplyEventArgs e)
        {
            EventHandler<GroupRolesDataReplyEventArgs> handler = m_GroupRoles;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupRolesLock = new object();

        /// <summary>Raised when the simulator responds to a <see cref="RequestGroupRoleData"/> request</summary>
        public event EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataReply
        {
            add { lock (m_GroupRolesLock) { m_GroupRoles += value; } }
            remove { lock (m_GroupRolesLock) { m_GroupRoles -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupRolesMembersReplyEventArgs> m_GroupRoleMembers;

        /// <summary>Raises the GroupRoleMembersReply event</summary>
        /// <param name="e">A GroupRolesRoleMembersReplyEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupRoleMembers(GroupRolesMembersReplyEventArgs e)
        {
            EventHandler<GroupRolesMembersReplyEventArgs> handler = m_GroupRoleMembers;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupRolesMembersLock = new object();

        /// <summary>Raised when the simulator responds to a <see cref="RequestGroupRolesMembers"/> request</summary>
        public event EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersReply
        {
            add { lock (m_GroupRolesMembersLock) { m_GroupRoleMembers += value; } }
            remove { lock (m_GroupRolesMembersLock) { m_GroupRoleMembers -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupTitlesReplyEventArgs> m_GroupTitles;


        /// <summary>Raises the GroupTitlesReply event</summary>
        /// <param name="e">A GroupTitlesReplyEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupTitles(GroupTitlesReplyEventArgs e)
        {
            EventHandler<GroupTitlesReplyEventArgs> handler = m_GroupTitles;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupTitlesLock = new object();

        /// <summary>Raised when the simulator responds to a <see cref="RequestGroupTitles"/> request</summary>
        public event EventHandler<GroupTitlesReplyEventArgs> GroupTitlesReply
        {
            add { lock (m_GroupTitlesLock) { m_GroupTitles += value; } }
            remove { lock (m_GroupTitlesLock) { m_GroupTitles -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupAccountSummaryReplyEventArgs> m_GroupAccountSummary;

        /// <summary>Raises the GroupAccountSummary event</summary>
        /// <param name="e">A GroupAccountSummaryReplyEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupAccountSummaryReply(GroupAccountSummaryReplyEventArgs e)
        {
            EventHandler<GroupAccountSummaryReplyEventArgs> handler = m_GroupAccountSummary;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupAccountSummaryLock = new object();

        /// <summary>Raised when a response to a RequestGroupAccountSummary is returned
        /// by the simulator</summary>
        public event EventHandler<GroupAccountSummaryReplyEventArgs> GroupAccountSummaryReply
        {
            add { lock (m_GroupAccountSummaryLock) { m_GroupAccountSummary += value; } }
            remove { lock (m_GroupAccountSummaryLock) { m_GroupAccountSummary -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupCreatedReplyEventArgs> m_GroupCreated;

        /// <summary>Raises the GroupCreated event</summary>
        /// <param name="e">An GroupCreatedEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupCreatedReply(GroupCreatedReplyEventArgs e)
        {
            EventHandler<GroupCreatedReplyEventArgs> handler = m_GroupCreated;
            handler?.Invoke(this, e);
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupCreatedLock = new object();

        /// <summary>Raised when a request to create a group is successful</summary>
        public event EventHandler<GroupCreatedReplyEventArgs> GroupCreatedReply
        {
            add { lock (m_GroupCreatedLock) { m_GroupCreated += value; } }
            remove { lock (m_GroupCreatedLock) { m_GroupCreated -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupOperationEventArgs> m_GroupJoined;

        /// <summary>Raises the GroupJoined event</summary>
        /// <param name="e">A GroupOperationEventArgs object containing the
        /// result of the operation returned from the simulator</param>
        protected virtual void OnGroupJoinedReply(GroupOperationEventArgs e)
        {
            EventHandler<GroupOperationEventArgs> handler = m_GroupJoined;
            handler?.Invoke(this, e);
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupJoinedLock = new object();

        /// <summary>Raised when a request to join a group either
        /// fails or succeeds</summary>
        public event EventHandler<GroupOperationEventArgs> GroupJoinedReply
        {
            add { lock (m_GroupJoinedLock) { m_GroupJoined += value; } }
            remove { lock (m_GroupJoinedLock) { m_GroupJoined -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupOperationEventArgs> m_GroupLeft;

        /// <summary>Raises the GroupLeft event</summary>
        /// <param name="e">A GroupOperationEventArgs object containing the
        /// result of the operation returned from the simulator</param>
        protected virtual void OnGroupLeaveReply(GroupOperationEventArgs e)
        {
            EventHandler<GroupOperationEventArgs> handler = m_GroupLeft;
            handler?.Invoke(this, e);
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupLeftLock = new object();

        /// <summary>Raised when a request to leave a group either
        /// fails or succeeds</summary>
        public event EventHandler<GroupOperationEventArgs> GroupLeaveReply
        {
            add { lock (m_GroupLeftLock) { m_GroupLeft += value; } }
            remove { lock (m_GroupLeftLock) { m_GroupLeft -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupDroppedEventArgs> m_GroupDropped;

        /// <summary>Raises the GroupDropped event</summary>
        /// <param name="e">An GroupDroppedEventArgs object containing the
        /// the group your agent left</param>
        protected virtual void OnGroupDropped(GroupDroppedEventArgs e)
        {
            EventHandler<GroupDroppedEventArgs> handler = m_GroupDropped;
            handler?.Invoke(this, e);
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupDroppedLock = new object();

        /// <summary>Raised when A group is removed from the group server</summary>
        public event EventHandler<GroupDroppedEventArgs> GroupDropped
        {
            add { lock (m_GroupDroppedLock) { m_GroupDropped += value; } }
            remove { lock (m_GroupDroppedLock) { m_GroupDropped -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupOperationEventArgs> m_GroupMemberEjected;

        /// <summary>Raises the GroupMemberEjected event</summary>
        /// <param name="e">An GroupMemberEjectedEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupMemberEjected(GroupOperationEventArgs e)
        {
            EventHandler<GroupOperationEventArgs> handler = m_GroupMemberEjected;
            handler?.Invoke(this, e);
        }
        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupMemberEjectedLock = new object();

        /// <summary>Raised when a request to eject a member from a group either
        /// fails or succeeds</summary>
        public event EventHandler<GroupOperationEventArgs> GroupMemberEjected
        {
            add { lock (m_GroupMemberEjectedLock) { m_GroupMemberEjected += value; } }
            remove { lock (m_GroupMemberEjectedLock) { m_GroupMemberEjected -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupNoticesListReplyEventArgs> m_GroupNoticesListReply;

        /// <summary>Raises the GroupNoticesListReply event</summary>
        /// <param name="e">An GroupNoticesListReplyEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupNoticesListReply(GroupNoticesListReplyEventArgs e)
        {
            EventHandler<GroupNoticesListReplyEventArgs> handler = m_GroupNoticesListReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupNoticesListReplyLock = new object();

        /// <summary>Raised when the simulator sends us group notices</summary>
        /// <see cref="RequestGroupNoticesList"/>
        public event EventHandler<GroupNoticesListReplyEventArgs> GroupNoticesListReply
        {
            add { lock (m_GroupNoticesListReplyLock) { m_GroupNoticesListReply += value; } }
            remove { lock (m_GroupNoticesListReplyLock) { m_GroupNoticesListReply -= value; } }
        }

        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<GroupInvitationEventArgs> m_GroupInvitation;

        /// <summary>Raises the GroupInvitation event</summary>
        /// <param name="e">An GroupInvitationEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnGroupInvitation(GroupInvitationEventArgs e)
        {
            EventHandler<GroupInvitationEventArgs> handler = m_GroupInvitation;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_GroupInvitationLock = new object();

        /// <summary>Raised when another agent invites our avatar to join a group</summary>
        public event EventHandler<GroupInvitationEventArgs> GroupInvitation
        {
            add { lock (m_GroupInvitationLock) { m_GroupInvitation += value; } }
            remove { lock (m_GroupInvitationLock) { m_GroupInvitation -= value; } }
        }






        /// <summary>The event subscribers. null if no subscribers</summary>
        private EventHandler<BannedAgentsEventArgs> m_BannedAgents;

        /// <summary>Raises the BannedAgents event</summary>
        /// <param name="e">An BannedAgentsEventArgs object containing the
        /// data returned from the simulator</param>
        protected virtual void OnBannedAgents(BannedAgentsEventArgs e)
        {
            EventHandler<BannedAgentsEventArgs> handler = m_BannedAgents;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_BannedAgentsLock = new object();

        /// <summary>Raised when a group ban is processed</summary>
        public event EventHandler<BannedAgentsEventArgs> BannedAgents
        {
            add { lock (m_BannedAgentsLock) { m_BannedAgents += value; } }
            remove { lock (m_BannedAgentsLock) { m_BannedAgents -= value; } }
        }

        #endregion Delegates

        /// <summary>A reference to the current <see cref="GridClient"/> instance</summary>
        private readonly GridClient Client;
        /// <summary>Currently-active group members requests</summary>
        private readonly List<UUID> GroupMembersRequests;
        /// <summary>Currently-active group roles requests</summary>
        private readonly List<UUID> GroupRolesRequests;
        /// <summary>Currently-active group role-member requests</summary>
        private readonly List<UUID> GroupRolesMembersRequests;
        /// <summary>Dictionary keeping group members while request is in progress</summary>
        private readonly LockingDictionary<UUID, Dictionary<UUID, GroupMember>> TempGroupMembers;
        /// <summary>Dictionary keeping member/role mapping while request is in progress</summary>
        private readonly LockingDictionary<UUID, List<KeyValuePair<UUID, UUID>>> TempGroupRolesMembers;
        /// <summary>Dictionary keeping GroupRole information while request is in progress</summary>
        private readonly LockingDictionary<UUID, Dictionary<UUID, GroupRole>> TempGroupRoles;
        /// <summary>Caches group name lookups</summary>
        public LockingDictionary<UUID, string> GroupName2KeyCache;

        /// <summary>
        /// Construct a new instance of the GroupManager class
        /// </summary>
        /// <param name="client">A reference to the current <see cref="GridClient"/> instance</param>
        public GroupManager(GridClient client)
        {
            Client = client;

            TempGroupMembers = new LockingDictionary<UUID, Dictionary<UUID, GroupMember>>();
            GroupMembersRequests = new List<UUID>();
            TempGroupRoles = new LockingDictionary<UUID, Dictionary<UUID, GroupRole>>();
            GroupRolesRequests = new List<UUID>();
            TempGroupRolesMembers = new LockingDictionary<UUID, List<KeyValuePair<UUID, UUID>>>();
            GroupRolesMembersRequests = new List<UUID>();
            GroupName2KeyCache = new LockingDictionary<UUID, string>();

            Client.Self.IM += Self_IM;

            Client.Network.RegisterEventCallback("AgentGroupDataUpdate", AgentGroupDataUpdateMessageHandler);
            // deprecated in simulator v1.27
            Client.Network.RegisterCallback(PacketType.AgentDropGroup, AgentDropGroupHandler);
            Client.Network.RegisterCallback(PacketType.GroupTitlesReply, GroupTitlesReplyHandler);
            Client.Network.RegisterCallback(PacketType.GroupProfileReply, GroupProfileReplyHandler);
            Client.Network.RegisterCallback(PacketType.GroupMembersReply, GroupMembersHandler);
            Client.Network.RegisterCallback(PacketType.GroupRoleDataReply, GroupRoleDataReplyHandler);
            Client.Network.RegisterCallback(PacketType.GroupRoleMembersReply, GroupRoleMembersReplyHandler);
            Client.Network.RegisterCallback(PacketType.GroupActiveProposalItemReply, GroupActiveProposalItemHandler);
            Client.Network.RegisterCallback(PacketType.GroupVoteHistoryItemReply, GroupVoteHistoryItemHandler);
            Client.Network.RegisterCallback(PacketType.GroupAccountSummaryReply, GroupAccountSummaryReplyHandler);
            Client.Network.RegisterCallback(PacketType.CreateGroupReply, CreateGroupReplyHandler);
            Client.Network.RegisterCallback(PacketType.JoinGroupReply, JoinGroupReplyHandler);
            Client.Network.RegisterCallback(PacketType.LeaveGroupReply, LeaveGroupReplyHandler);
            Client.Network.RegisterCallback(PacketType.UUIDGroupNameReply, UUIDGroupNameReplyHandler);
            Client.Network.RegisterCallback(PacketType.EjectGroupMemberReply, EjectGroupMemberReplyHandler);
            Client.Network.RegisterCallback(PacketType.GroupNoticesListReply, GroupNoticesListReplyHandler);

            Client.Network.RegisterEventCallback("AgentDropGroup", AgentDropGroupMessageHandler);
        }

        private void Self_IM(object sender, InstantMessageEventArgs e)
        {
            if (m_GroupInvitation == null || e.IM.Dialog != InstantMessageDialog.GroupInvitation) return;

            GroupInvitationEventArgs args = new GroupInvitationEventArgs(e.Simulator, e.IM.FromAgentID, e.IM.FromAgentName, e.IM.Message);
            OnGroupInvitation(args);

            if (args.Accept)
            {
                Client.Self.InstantMessage("name", e.IM.FromAgentID, "message", e.IM.IMSessionID, InstantMessageDialog.GroupInvitationAccept,
                    InstantMessageOnline.Online, Client.Self.SimPosition, UUID.Zero, Utils.EmptyBytes);
            }
            else
            {
                Client.Self.InstantMessage("name", e.IM.FromAgentID, "message", e.IM.IMSessionID, InstantMessageDialog.GroupInvitationDecline,
                    InstantMessageOnline.Online, Client.Self.SimPosition, UUID.Zero, new byte[1] { 0 });
            }
        }

        #region Public Methods

        /// <summary>
        /// Request a current list of groups the avatar is a member of.
        /// </summary>
        /// <remarks>CAPS Event Queue must be running for this to work since the results
        /// come across CAPS.</remarks>
        public void RequestCurrentGroups()
        {
            AgentDataUpdateRequestPacket request =
                new AgentDataUpdateRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    }
                };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Lookup name of group based on groupID
        /// </summary>
        /// <param name="groupID">groupID of group to lookup name for.</param>
        public void RequestGroupName(UUID groupID)
        {
            // if we already have this in the cache, return from cache instead of making a request
            if (GroupName2KeyCache.ContainsKey(groupID))
            {
                Dictionary<UUID, string> groupNames = new Dictionary<UUID, string>();
                lock (GroupName2KeyCache.Dictionary)
                    groupNames.Add(groupID, GroupName2KeyCache.Dictionary[groupID]);

                if (m_GroupNames != null)
                {
                    OnGroupNamesReply(new GroupNamesEventArgs(groupNames));
                }
            }
            else
            {
                UUIDGroupNameRequestPacket req = new UUIDGroupNameRequestPacket();
                UUIDGroupNameRequestPacket.UUIDNameBlockBlock[] block = new UUIDGroupNameRequestPacket.UUIDNameBlockBlock[1];
                block[0] = new UUIDGroupNameRequestPacket.UUIDNameBlockBlock {ID = groupID};
                req.UUIDNameBlock = block;
                Client.Network.SendPacket(req);
            }
        }

        /// <summary>
        /// Request lookup of multiple group names
        /// </summary>
        /// <param name="groupIDs">List of group IDs to request.</param>
        public void RequestGroupNames(List<UUID> groupIDs)
        {
            Dictionary<UUID, string> groupNames = new Dictionary<UUID, string>();
            lock (GroupName2KeyCache.Dictionary)
            {
                foreach (UUID groupID in groupIDs)
                {
                    if (GroupName2KeyCache.ContainsKey(groupID))
                        groupNames[groupID] = GroupName2KeyCache.Dictionary[groupID];
                }
            }

            if (groupIDs.Count > 0)
            {
                UUIDGroupNameRequestPacket req = new UUIDGroupNameRequestPacket();
                UUIDGroupNameRequestPacket.UUIDNameBlockBlock[] block = new UUIDGroupNameRequestPacket.UUIDNameBlockBlock[groupIDs.Count];

                for (int i = 0; i < groupIDs.Count; i++)
                {
                    block[i] = new UUIDGroupNameRequestPacket.UUIDNameBlockBlock {ID = groupIDs[i]};
                }

                req.UUIDNameBlock = block;
                Client.Network.SendPacket(req);
            }

            // fire handler from cache
            if (groupNames.Count > 0 && m_GroupNames != null)
            {
                OnGroupNamesReply(new GroupNamesEventArgs(groupNames));
            }
        }

        /// <summary>Lookup group profile data such as name, enrollment, founder, logo, etc</summary>
        /// <remarks>Subscribe to <code>OnGroupProfile</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        public void RequestGroupProfile(UUID group)
        {
            GroupProfileRequestPacket request =
                new GroupProfileRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    GroupData = {GroupID = @group}
                };

            Client.Network.SendPacket(request);
        }

        /// <summary>Request a list of group members.</summary>
        /// <remarks>Subscribe to <code>OnGroupMembers</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        /// <returns>UUID of the request, use to index into cache</returns>
        public UUID RequestGroupMembers(UUID group)
        {
            UUID requestID = UUID.Random();
            Uri cap = Client.Network.CurrentSim?.Caps?.CapabilityURI("GroupMemberData");

            // Request from Capability
            if (cap != null)
            {
                OSDMap payload = new OSDMap(1) { ["group_id"] = @group };
                Task req = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload, CancellationToken.None,
                    (response, data, error) =>
                {
                    if (error != null) { return; }

                    OSD result = OSDParser.Deserialize(data);
                    GroupMembersHandlerCaps(requestID, result);
                });
            }
            else // fallback to LLUDP
            {
                lock (GroupMembersRequests) GroupMembersRequests.Add(requestID);

                GroupMembersRequestPacket request =
                    new GroupMembersRequestPacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        GroupData =
                        {
                            GroupID = @group,
                            RequestID = requestID
                        }
                    };

                Client.Network.SendPacket(request);
            }

            return requestID;
        }

        /// <summary>Request group roles</summary>
        /// <remarks>Subscribe to <code>OnGroupRoles</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        /// <returns>UUID of the request, use to index into cache</returns>
        public UUID RequestGroupRoles(UUID group)
        {
            UUID requestID = UUID.Random();
            lock (GroupRolesRequests) GroupRolesRequests.Add(requestID);

            GroupRoleDataRequestPacket request =
                new GroupRoleDataRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    GroupData =
                    {
                        GroupID = @group,
                        RequestID = requestID
                    }
                };

            Client.Network.SendPacket(request);
            return requestID;
        }

        /// <summary>Request members (members,role) role mapping for a group.</summary>
        /// <remarks>Subscribe to <code>OnGroupRolesMembers</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        /// <returns>UUID of the request, use to index into cache</returns>
        public UUID RequestGroupRolesMembers(UUID group)
        {
            UUID requestID = UUID.Random();
            lock (GroupRolesRequests) GroupRolesMembersRequests.Add(requestID);

            GroupRoleMembersRequestPacket request =
                new GroupRoleMembersRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    GroupData =
                    {
                        GroupID = @group,
                        RequestID = requestID
                    }
                };
            Client.Network.SendPacket(request);
            return requestID;
        }

        /// <summary>Request a groups Titles</summary>
        /// <remarks>Subscribe to <code>OnGroupTitles</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        /// <returns>UUID of the request, use to index into cache</returns>
        public UUID RequestGroupTitles(UUID group)
        {
            UUID requestID = UUID.Random();

            GroupTitlesRequestPacket request =
                new GroupTitlesRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group,
                        RequestID = requestID
                    }
                };

            Client.Network.SendPacket(request);
            return requestID;
        }

        /// <summary>Begin to get the group account summary</summary>
        /// <remarks>Subscribe to the <code>OnGroupAccountSummary</code> event to receive the results.</remarks>
        /// <param name="group">group ID (UUID)</param>
        /// <param name="intervalDays">How long of an interval</param>
        /// <param name="currentInterval">Which interval (0 for current, 1 for last)</param>
        public void RequestGroupAccountSummary(UUID group, int intervalDays, int currentInterval)
        {
            GroupAccountSummaryRequestPacket p =
                new GroupAccountSummaryRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group
                    },
                    MoneyData =
                    {
                        RequestID = UUID.Random(),
                        CurrentInterval = currentInterval,
                        IntervalDays = intervalDays
                    }
                };
            Client.Network.SendPacket(p);
        }

        /// <summary>Invites a user to a group</summary>
        /// <param name="group">The group to invite to</param>
        /// <param name="roles">A list of roles to invite an agent to</param>
        /// <param name="requestedAgent">Key of agent to invite</param>
        public void Invite(UUID group, List<UUID> roles, UUID requestedAgent)
        {
            InviteGroupRequestPacket igp = new InviteGroupRequestPacket
            {
                AgentData = new InviteGroupRequestPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData = new InviteGroupRequestPacket.GroupDataBlock {GroupID = @group},
                InviteData = new InviteGroupRequestPacket.InviteDataBlock[roles.Count]
            };
            for (int i = 0; i < roles.Count; i++)
            {
                igp.InviteData[i] = new InviteGroupRequestPacket.InviteDataBlock
                {
                    InviteeID = requestedAgent,
                    RoleID = roles[i]
                };
            }

            Client.Network.SendPacket(igp);
        }

        /// <summary>Set a group as the current active group</summary>
        /// <param name="id">group ID (UUID)</param>
        public void ActivateGroup(UUID id)
        {
            ActivateGroupPacket activate = new ActivateGroupPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    GroupID = id
                }
            };

            Client.Network.SendPacket(activate);
        }

        /// <summary>Change the role that determines your active title</summary>
        /// <param name="group">Group ID to use</param>
        /// <param name="role">Role ID to change to</param>
        public void ActivateTitle(UUID group, UUID role)
        {
            GroupTitleUpdatePacket gtu = new GroupTitleUpdatePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TitleRoleID = role,
                    GroupID = @group
                }
            };

            Client.Network.SendPacket(gtu);
        }

        /// <summary>Set this avatar's tier contribution</summary>
        /// <param name="group">Group ID to change tier in</param>
        /// <param name="contribution">amount of tier to donate</param>
        public void SetGroupContribution(UUID group, int contribution)
        {
            SetGroupContributionPacket sgp =
                new SetGroupContributionPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data =
                    {
                        GroupID = @group,
                        Contribution = contribution
                    }
                };

            Client.Network.SendPacket(sgp);
        }

        /// <summary>
        /// Set agent's group preferences
        /// </summary>
        /// <param name="groupID">Group <see cref="UUID"/></param>
        /// <param name="acceptNotices">Accept notices from this group</param>
        /// <param name="listInProfile">List this group in the profile</param>
        public void SetGroupAcceptNotices(UUID groupID, bool acceptNotices, bool listInProfile)
        {
            SetGroupAcceptNoticesPacket p =
                new SetGroupAcceptNoticesPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data =
                    {
                        GroupID = groupID,
                        AcceptNotices = acceptNotices
                    },
                    NewData = {ListInProfile = listInProfile}
                };

            Client.Network.SendPacket(p);
        }

        /// <summary>Request to join a group</summary>
        /// <remarks>Subscribe to <code>OnGroupJoined</code> event for confirmation.</remarks>
        /// <param name="id">group ID (UUID) to join.</param>
        public void RequestJoinGroup(UUID id)
        {
            JoinGroupRequestPacket join = new JoinGroupRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData =
                {
                    GroupID = id
                }
            };

            Client.Network.SendPacket(join);
        }

        /// <summary>
        /// Request to create a new group. If the group is successfully
        /// created, L$100 will automatically be deducted
        /// </summary>
        /// <remarks>Subscribe to <code>OnGroupCreated</code> event to receive confirmation.</remarks>
        /// <param name="group">Group struct containing the new group info</param>
        public void RequestCreateGroup(Group group)
        {
            CreateGroupRequestPacket cgrp = new CreateGroupRequestPacket
            {
                AgentData = new CreateGroupRequestPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData = new CreateGroupRequestPacket.GroupDataBlock
                {
                    AllowPublish = @group.AllowPublish,
                    Charter = Utils.StringToBytes(@group.Charter),
                    InsigniaID = @group.InsigniaID,
                    MaturePublish = @group.MaturePublish,
                    MembershipFee = @group.MembershipFee,
                    Name = Utils.StringToBytes(@group.Name),
                    OpenEnrollment = @group.OpenEnrollment,
                    ShowInList = @group.ShowInList
                }
            };

            Client.Network.SendPacket(cgrp);
        }

        /// <summary>Update a group's profile and other information</summary>
        /// <param name="id">Groups ID (UUID) to update.</param>
        /// <param name="group">Group struct to update.</param>
        public void UpdateGroup(UUID id, Group group)
        {
            UpdateGroupInfoPacket cgrp = new UpdateGroupInfoPacket
            {
                AgentData = new UpdateGroupInfoPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData = new UpdateGroupInfoPacket.GroupDataBlock
                {
                    GroupID = id,
                    AllowPublish = group.AllowPublish,
                    Charter = Utils.StringToBytes(group.Charter),
                    InsigniaID = group.InsigniaID,
                    MaturePublish = group.MaturePublish,
                    MembershipFee = group.MembershipFee,
                    OpenEnrollment = group.OpenEnrollment,
                    ShowInList = group.ShowInList
                }
            };

            Client.Network.SendPacket(cgrp);
        }

        /// <summary>Eject a user from a group</summary>
        /// <param name="group">Group ID to eject the user from</param>
        /// <param name="member">Avatar's key to eject</param>
        public void EjectUser(UUID group, UUID member)
        {
            EjectGroupMemberRequestPacket eject = new EjectGroupMemberRequestPacket
            {
                AgentData = new EjectGroupMemberRequestPacket.AgentDataBlock
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData = new EjectGroupMemberRequestPacket.GroupDataBlock {GroupID = group},
                EjectData = new EjectGroupMemberRequestPacket.EjectDataBlock[1]
            };
            eject.EjectData[0] = new EjectGroupMemberRequestPacket.EjectDataBlock {EjecteeID = member};

            Client.Network.SendPacket(eject);
        }

        /// <summary>Update role information</summary>
        /// <param name="role">Modified role to be updated</param>
        public void UpdateRole(GroupRole role)
        {
            GroupRoleUpdatePacket gru =
                new GroupRoleUpdatePacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = role.GroupID
                    },
                    RoleData = new GroupRoleUpdatePacket.RoleDataBlock[1]
                };
            gru.RoleData[0] = new GroupRoleUpdatePacket.RoleDataBlock
            {
                Name = Utils.StringToBytes(role.Name),
                Description = Utils.StringToBytes(role.Description),
                Powers = (ulong) role.Powers,
                RoleID = role.ID,
                Title = Utils.StringToBytes(role.Title),
                UpdateType = (byte) GroupRoleUpdate.UpdateAll
            };
            Client.Network.SendPacket(gru);
        }

        /// <summary>Create a new group role</summary>
        /// <param name="group">Group ID to update</param>
        /// <param name="role">Role to create</param>
        public void CreateRole(UUID group, GroupRole role)
        {
            GroupRoleUpdatePacket gru =
                new GroupRoleUpdatePacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group
                    },
                    RoleData = new GroupRoleUpdatePacket.RoleDataBlock[1]
                };
            gru.RoleData[0] = new GroupRoleUpdatePacket.RoleDataBlock
            {
                RoleID = UUID.Random(),
                Name = Utils.StringToBytes(role.Name),
                Description = Utils.StringToBytes(role.Description),
                Powers = (ulong) role.Powers,
                Title = Utils.StringToBytes(role.Title),
                UpdateType = (byte) GroupRoleUpdate.Create
            };
            Client.Network.SendPacket(gru);
        }

        /// <summary>Delete a group role</summary>
        /// <param name="group">Group ID to update</param>
        /// <param name="roleID">Role to delete</param>
        public void DeleteRole(UUID group, UUID roleID)
        {
            GroupRoleUpdatePacket gru =
                new GroupRoleUpdatePacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group
                    },
                    RoleData = new GroupRoleUpdatePacket.RoleDataBlock[1]
                };
            gru.RoleData[0] = new GroupRoleUpdatePacket.RoleDataBlock
            {
                RoleID = roleID,
                Name = Utils.StringToBytes(string.Empty),
                Description = Utils.StringToBytes(string.Empty),
                Powers = 0u,
                Title = Utils.StringToBytes(string.Empty),
                UpdateType = (byte) GroupRoleUpdate.Delete
            };
            Client.Network.SendPacket(gru);
        }

        /// <summary>Remove an avatar from a role</summary>
        /// <param name="group">Group ID to update</param>
        /// <param name="role">Role ID to be removed from</param>
        /// <param name="member">Avatar's Key to remove</param>
        public void RemoveFromRole(UUID group, UUID role, UUID member)
        {
            GroupRoleChangesPacket grc =
                new GroupRoleChangesPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group
                    },
                    RoleChange = new GroupRoleChangesPacket.RoleChangeBlock[1]
                };
            grc.RoleChange[0] = new GroupRoleChangesPacket.RoleChangeBlock
            {
                MemberID = member,
                RoleID = role,
                Change = 1 //1 = Remove From Role TODO: this should be in an enum
            };

            Client.Network.SendPacket(grc);
        }

        /// <summary>Assign an avatar to a role</summary>
        /// <param name="group">Group ID to update</param>
        /// <param name="role">Role ID to assign to</param>
        /// <param name="member">Avatar's ID to assign to role</param>
        public void AddToRole(UUID group, UUID role, UUID member)
        {
            GroupRoleChangesPacket grc =
                new GroupRoleChangesPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        GroupID = @group
                    },
                    RoleChange = new GroupRoleChangesPacket.RoleChangeBlock[1]
                };
            grc.RoleChange[0] = new GroupRoleChangesPacket.RoleChangeBlock
            {
                MemberID = member,
                RoleID = role,
                Change = 0 //0 = Add to Role TODO: this should be in an enum
            };

            Client.Network.SendPacket(grc);
        }

        /// <summary>Request the group notices list</summary>
        /// <param name="group">Group ID to fetch notices for</param>
        public void RequestGroupNoticesList(UUID group)
        {
            GroupNoticesListRequestPacket gnl =
                new GroupNoticesListRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data = {GroupID = @group}
                };
            Client.Network.SendPacket(gnl);
        }

        /// <summary>Request a group notice by key</summary>
        /// <param name="noticeID">ID of group notice</param>
        public void RequestGroupNotice(UUID noticeID)
        {
            GroupNoticeRequestPacket gnr =
                new GroupNoticeRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data = {GroupNoticeID = noticeID}
                };
            Client.Network.SendPacket(gnr);
        }

        /// <summary>Send out a group notice</summary>
        /// <param name="group">Group ID to update</param>
        /// <param name="notice"><code>GroupNotice</code> structure containing notice data</param>
        public void SendGroupNotice(UUID group, GroupNotice notice)
        {
            Client.Self.InstantMessage(Client.Self.Name, group, notice.Subject + "|" + notice.Message,
                UUID.Zero, InstantMessageDialog.GroupNotice, InstantMessageOnline.Online,
                Vector3.Zero, UUID.Zero, notice.SerializeAttachment());
        }

        /// <summary>Start a group proposal (vote)</summary>
        /// <param name="group">The Group ID to send proposal to</param>
        /// <param name="prop"><code>GroupProposal</code> structure containing the proposal</param>
        public void StartProposal(UUID group, GroupProposal prop)
        {
            StartGroupProposalPacket p = new StartGroupProposalPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                ProposalData =
                {
                    GroupID = @group,
                    ProposalText = Utils.StringToBytes(prop.VoteText),
                    Quorum = prop.Quorum,
                    Majority = prop.Majority,
                    Duration = prop.Duration
                }
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>Request to leave a group</summary>
        /// <remarks>Subscribe to <code>OnGroupLeft</code> event to receive confirmation</remarks>
        /// <param name="groupID">The group to leave</param>
        public void LeaveGroup(UUID groupID)
        {
            LeaveGroupRequestPacket p = new LeaveGroupRequestPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                GroupData = {GroupID = groupID}
            };

            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Request a list of residents banned from joining a group
        /// </summary>
        /// <param name="groupID">UUID of the group</param>
        /// <param name="callback">Callback on request completion</param>
        /// <param name="cancellationToken"></param>
        public async Task RequestBannedAgents(UUID groupID, EventHandler<BannedAgentsEventArgs> callback = null, 
            CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(Client.Network.CurrentSim.Caps.CapabilityURI("GroupAPIv1"))
                {Query = $"group_id={groupID}"}.Uri;
            await Client.HttpCapsClient.GetRequestAsync(uri, cancellationToken, (response, data, error) =>
            {
                try
                {
                    if (error != null) { throw error; }

                    OSD result = OSDParser.Deserialize(data);
                    UUID gid = ((OSDMap)result)["group_id"];
                    var banList = (OSDMap)((OSDMap)result)["ban_list"];
                    var bannedAgents = new Dictionary<UUID, DateTime>(banList.Count);

                    foreach (var id in banList.Keys)
                    {
                        bannedAgents[new UUID(id)] = ((OSDMap)banList[id])["ban_date"].AsDate();
                    }

                    var ret = new BannedAgentsEventArgs(groupID, true, bannedAgents);
                    OnBannedAgents(ret);
                    if (callback != null)
                    {
                        try { callback(this, ret); }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to fetch group ban list for {groupID}: {ex.Message}", Helpers.LogLevel.Warning, Client);
                    var ret = new BannedAgentsEventArgs(groupID, false, null);
                    OnBannedAgents(ret);
                    if (callback != null)
                    {
                        try { callback(this, ret); }
                        catch { }
                    }
                }
            });
        }

        /// <summary>
        /// Request that group of agents be banned or unbanned from the group
        /// </summary>
        /// <param name="groupID">Group ID</param>
        /// <param name="action">Ban/Unban action</param>
        /// <param name="agents">Array of agents UUIDs to ban</param>
        /// <param name="callback">Callback</param>
        /// <param name="cancellationToken"></param>
        public async Task RequestBanAction(UUID groupID, GroupBanAction action, UUID[] agents, EventHandler<EventArgs> callback = null, 
            CancellationToken cancellationToken = default)
        {
            var uri = new UriBuilder(Client.Network.CurrentSim.Caps.CapabilityURI("GroupAPIv1")) {Query = $"group_id={groupID}"}.Uri;

            OSDMap payload = new OSDMap { ["ban_action"] = (int)action };
            OSDArray banIDs = new OSDArray(agents.Length);
            foreach (var agent in agents)
            {
                banIDs.Add(agent);
            }
            payload["ban_ids"] = banIDs;

            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, payload, cancellationToken,
                (response, data, error) =>
            {
                if (error != null)
                {
                    Logger.Log($"Failed to ban members from {groupID}: {error.Message}", Helpers.LogLevel.Warning, Client);
                    return;
                }
                if (callback != null)
                {
                    try { callback(this, EventArgs.Empty); }
                    catch { }
                }
            });
        }

        #endregion

        #region Packet Handlers

        protected void AgentGroupDataUpdateMessageHandler(string capsKey, IMessage message, Simulator simulator)
        {
            AgentGroupDataUpdateMessage msg = (AgentGroupDataUpdateMessage)message;

            Dictionary<UUID, Group> currentGroups = new Dictionary<UUID, Group>();
            for (int i = 0; i < msg.GroupDataBlock.Length; i++)
            {
                Group group = new Group
                {
                    ID = msg.GroupDataBlock[i].GroupID,
                    InsigniaID = msg.GroupDataBlock[i].GroupInsigniaID,
                    Name = msg.GroupDataBlock[i].GroupName,
                    Contribution = msg.GroupDataBlock[i].Contribution,
                    AcceptNotices = msg.GroupDataBlock[i].AcceptNotices,
                    Powers = msg.GroupDataBlock[i].GroupPowers,
                    ListInProfile = msg.NewGroupDataBlock[i].ListInProfile
                };

                currentGroups.Add(group.ID, group);

                lock (GroupName2KeyCache.Dictionary)
                {
                    if (!GroupName2KeyCache.Dictionary.ContainsKey(group.ID))
                        GroupName2KeyCache.Dictionary.Add(group.ID, group.Name);
                }
            }

            if (m_CurrentGroups != null)
            {
                OnCurrentGroups(new CurrentGroupsEventArgs(currentGroups));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AgentDropGroupHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupDropped != null)
            {
                Packet packet = e.Packet;
                OnGroupDropped(new GroupDroppedEventArgs(((AgentDropGroupPacket)packet).AgentData.GroupID));
            }
        }

        protected void AgentDropGroupMessageHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (m_GroupDropped != null)
            {
                AgentDropGroupMessage msg = (AgentDropGroupMessage)message;
                foreach (AgentDropGroupMessage.AgentData ad in msg.AgentDataBlock)
                {
                    OnGroupDropped(new GroupDroppedEventArgs(ad.GroupID));
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupProfileReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupProfile != null)
            {
                Packet packet = e.Packet;
                GroupProfileReplyPacket profile = (GroupProfileReplyPacket)packet;
                Group group = new Group
                {
                    ID = profile.GroupData.GroupID,
                    AllowPublish = profile.GroupData.AllowPublish,
                    Charter = Utils.BytesToString(profile.GroupData.Charter),
                    FounderID = profile.GroupData.FounderID,
                    GroupMembershipCount = profile.GroupData.GroupMembershipCount,
                    GroupRolesCount = profile.GroupData.GroupRolesCount,
                    InsigniaID = profile.GroupData.InsigniaID,
                    MaturePublish = profile.GroupData.MaturePublish,
                    MembershipFee = profile.GroupData.MembershipFee,
                    MemberTitle = Utils.BytesToString(profile.GroupData.MemberTitle),
                    Money = profile.GroupData.Money,
                    Name = Utils.BytesToString(profile.GroupData.Name),
                    OpenEnrollment = profile.GroupData.OpenEnrollment,
                    OwnerRole = profile.GroupData.OwnerRole,
                    Powers = (GroupPowers)profile.GroupData.PowersMask,
                    ShowInList = profile.GroupData.ShowInList
                };

                OnGroupProfile(new GroupProfileEventArgs(group));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupNoticesListReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupNoticesListReply != null)
            {
                Packet packet = e.Packet;
                GroupNoticesListReplyPacket reply = (GroupNoticesListReplyPacket)packet;

                List<GroupNoticesListEntry> notices = reply.Data.Select(entry => new GroupNoticesListEntry
                    {
                        FromName = Utils.BytesToString(entry.FromName),
                        Subject = Utils.BytesToString(entry.Subject),
                        NoticeID = entry.NoticeID,
                        Timestamp = entry.Timestamp,
                        HasAttachment = entry.HasAttachment,
                        AssetType = (AssetType)entry.AssetType
                    })
                    .ToList();

                OnGroupNoticesListReply(new GroupNoticesListReplyEventArgs(reply.AgentData.GroupID, notices));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupTitlesReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupTitles != null)
            {
                Packet packet = e.Packet;
                GroupTitlesReplyPacket titles = (GroupTitlesReplyPacket)packet;
                Dictionary<UUID, GroupTitle> groupTitleCache = new Dictionary<UUID, GroupTitle>();

                foreach (GroupTitlesReplyPacket.GroupDataBlock block in titles.GroupData)
                {
                    GroupTitle groupTitle = new GroupTitle
                    {
                        GroupID = titles.AgentData.GroupID,
                        RoleID = block.RoleID,
                        Title = Utils.BytesToString(block.Title),
                        Selected = block.Selected
                    };

                    groupTitleCache[block.RoleID] = groupTitle;
                }
                OnGroupTitles(new GroupTitlesReplyEventArgs(titles.AgentData.RequestID, titles.AgentData.GroupID, groupTitleCache));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupMembersHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            GroupMembersReplyPacket members = (GroupMembersReplyPacket)packet;
            Dictionary<UUID, GroupMember> groupMemberCache = null;

            lock (GroupMembersRequests)
            {
                // If nothing is registered to receive this RequestID drop the data
                if (GroupMembersRequests.Contains(members.GroupData.RequestID))
                {
                    lock (TempGroupMembers.Dictionary)
                    {
                        if (!TempGroupMembers.TryGetValue(members.GroupData.RequestID, out groupMemberCache))
                        {
                            groupMemberCache = new Dictionary<UUID, GroupMember>();
                            TempGroupMembers[members.GroupData.RequestID] = groupMemberCache;
                        }

                        foreach (GroupMembersReplyPacket.MemberDataBlock block in members.MemberData)
                        {
                            GroupMember groupMember = new GroupMember
                            {
                                ID = block.AgentID,
                                Contribution = block.Contribution,
                                IsOwner = block.IsOwner,
                                OnlineStatus = Utils.BytesToString(block.OnlineStatus),
                                Powers = (GroupPowers) block.AgentPowers,
                                Title = Utils.BytesToString(block.Title)
                            };

                            groupMemberCache[block.AgentID] = groupMember;
                        }

                        if (groupMemberCache.Count >= members.GroupData.MemberCount)
                        {
                            GroupMembersRequests.Remove(members.GroupData.RequestID);
                            TempGroupMembers.Remove(members.GroupData.RequestID);
                        }
                    }
                }
            }

            if (m_GroupMembers != null && groupMemberCache != null && groupMemberCache.Count >= members.GroupData.MemberCount)
            {
                OnGroupMembersReply(new GroupMembersReplyEventArgs(members.GroupData.RequestID, members.GroupData.GroupID, groupMemberCache));
            }
        }

        protected void GroupMembersHandlerCaps(UUID requestID, OSD result)
        {
            try
            {
                OSDMap res = (OSDMap)result;
                int memberCount = res["member_count"];
                OSDArray titlesOSD = (OSDArray)res["titles"];
                string[] titles = new string[titlesOSD.Count];
                for (int i = 0; i < titlesOSD.Count; i++)
                {
                    titles[i] = titlesOSD[i];
                }
                UUID groupID = res["group_id"];
                GroupPowers defaultPowers = (GroupPowers)(ulong)((OSDMap)res["defaults"])["default_powers"];
                OSDMap membersOSD = (OSDMap)res["members"];
                Dictionary<UUID, GroupMember> groupMembers = new Dictionary<UUID, GroupMember>(membersOSD.Count);
                foreach (var memberID in membersOSD.Keys)
                {
                    OSDMap member = (OSDMap)membersOSD[memberID];

                    GroupMember groupMember = new GroupMember
                    {
                        ID = (UUID) memberID,
                        Contribution = member["donated_square_meters"],
                        IsOwner = "Y" == member["owner"].AsString(),
                        OnlineStatus = member["last_login"],
                        Powers = defaultPowers
                    };
                    if (member.TryGetValue("powers", out var power))
                    {
                        groupMember.Powers = (GroupPowers)(ulong)power;
                    }
                    groupMember.Title = titles[(int)member["title"]];

                    groupMembers[groupMember.ID] = groupMember;
                }

                OnGroupMembersReply(new GroupMembersReplyEventArgs(requestID, groupID, groupMembers));
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to decode result of GroupMemberData capability: ", Helpers.LogLevel.Error, Client, ex);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupRoleDataReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            GroupRoleDataReplyPacket roles = (GroupRoleDataReplyPacket)packet;
            Dictionary<UUID, GroupRole> groupRoleCache = null;

            lock (GroupRolesRequests)
            {
                // If nothing is registered to receive this RequestID drop the data
                if (GroupRolesRequests.Contains(roles.GroupData.RequestID))
                {
                    lock (TempGroupRoles.Dictionary)
                    {
                        if (!TempGroupRoles.TryGetValue(roles.GroupData.RequestID, out groupRoleCache))
                        {
                            groupRoleCache = new Dictionary<UUID, GroupRole>();
                            TempGroupRoles[roles.GroupData.RequestID] = groupRoleCache;
                        }

                        foreach (GroupRoleDataReplyPacket.RoleDataBlock block in roles.RoleData)
                        {
                            GroupRole groupRole = new GroupRole
                            {
                                GroupID = roles.GroupData.GroupID,
                                ID = block.RoleID,
                                Description = Utils.BytesToString(block.Description),
                                Name = Utils.BytesToString(block.Name),
                                Powers = (GroupPowers) block.Powers,
                                Title = Utils.BytesToString(block.Title)
                            };

                            groupRoleCache[block.RoleID] = groupRole;
                        }

                        if (groupRoleCache.Count >= roles.GroupData.RoleCount)
                        {
                            GroupRolesRequests.Remove(roles.GroupData.RequestID);
                            TempGroupRoles.Remove(roles.GroupData.RequestID);
                        }
                    }
                }
            }

            if (m_GroupRoles != null && groupRoleCache != null && groupRoleCache.Count >= roles.GroupData.RoleCount)
            {
                OnGroupRoleDataReply(new GroupRolesDataReplyEventArgs(roles.GroupData.RequestID, roles.GroupData.GroupID, groupRoleCache));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupRoleMembersReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            GroupRoleMembersReplyPacket members = (GroupRoleMembersReplyPacket)packet;
            List<KeyValuePair<UUID, UUID>> groupRoleMemberCache = null;

            try
            {
                lock (GroupRolesMembersRequests)
                {
                    // If nothing is registered to receive this RequestID drop the data
                    if (GroupRolesMembersRequests.Contains(members.AgentData.RequestID))
                    {
                        lock (TempGroupRolesMembers.Dictionary)
                        {
                            if (!TempGroupRolesMembers.TryGetValue(members.AgentData.RequestID, out groupRoleMemberCache))
                            {
                                groupRoleMemberCache = new List<KeyValuePair<UUID, UUID>>();
                                TempGroupRolesMembers[members.AgentData.RequestID] = groupRoleMemberCache;
                            }

                            groupRoleMemberCache.AddRange(members.MemberData.Select(block => new KeyValuePair<UUID, UUID>(block.RoleID, block.MemberID)));

                            if (groupRoleMemberCache.Count >= members.AgentData.TotalPairs)
                            {
                                GroupRolesMembersRequests.Remove(members.AgentData.RequestID);
                                TempGroupRolesMembers.Remove(members.AgentData.RequestID);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
            }

            if (m_GroupRoleMembers != null && groupRoleMemberCache != null && groupRoleMemberCache.Count >= members.AgentData.TotalPairs)
            {
                OnGroupRoleMembers(new GroupRolesMembersReplyEventArgs(members.AgentData.RequestID, members.AgentData.GroupID, groupRoleMemberCache));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupActiveProposalItemHandler(object sender, PacketReceivedEventArgs e)
        {
            GroupActiveProposalItemReplyPacket proposal = (GroupActiveProposalItemReplyPacket)e.Packet;

            // NOTE: Second Life server removed this many years back.
            Logger.Log($"Received GroupActiveProposalItemReplyPacket: {proposal}", Helpers.LogLevel.Info);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupVoteHistoryItemHandler(object sender, PacketReceivedEventArgs e)
        {
            GroupVoteHistoryItemReplyPacket history = (GroupVoteHistoryItemReplyPacket)e.Packet;

            // NOTE: Second Life server removed  this many years back.
            Logger.Log($"Received GroupVoteHistoryItemReplyPacket: {history}", Helpers.LogLevel.Info);
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void GroupAccountSummaryReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupAccountSummary != null)
            {
                Packet packet = e.Packet;
                GroupAccountSummaryReplyPacket summary = (GroupAccountSummaryReplyPacket)packet;
                GroupAccountSummary account = new GroupAccountSummary
                {
                    Balance = summary.MoneyData.Balance,
                    CurrentInterval = summary.MoneyData.CurrentInterval,
                    GroupTaxCurrent = summary.MoneyData.GroupTaxCurrent,
                    GroupTaxEstimate = summary.MoneyData.GroupTaxEstimate,
                    IntervalDays = summary.MoneyData.IntervalDays,
                    LandTaxCurrent = summary.MoneyData.LandTaxCurrent,
                    LandTaxEstimate = summary.MoneyData.LandTaxEstimate,
                    LastTaxDate = Utils.BytesToString(summary.MoneyData.LastTaxDate),
                    LightTaxCurrent = summary.MoneyData.LightTaxCurrent,
                    LightTaxEstimate = summary.MoneyData.LightTaxEstimate,
                    NonExemptMembers = summary.MoneyData.NonExemptMembers,
                    ObjectTaxCurrent = summary.MoneyData.ObjectTaxCurrent,
                    ObjectTaxEstimate = summary.MoneyData.ObjectTaxEstimate,
                    ParcelDirFeeCurrent = summary.MoneyData.ParcelDirFeeCurrent,
                    ParcelDirFeeEstimate = summary.MoneyData.ParcelDirFeeEstimate,
                    StartDate = Utils.BytesToString(summary.MoneyData.StartDate),
                    TaxDate = Utils.BytesToString(summary.MoneyData.TaxDate),
                    TotalCredits = summary.MoneyData.TotalCredits,
                    TotalDebits = summary.MoneyData.TotalDebits
                };

                OnGroupAccountSummaryReply(new GroupAccountSummaryReplyEventArgs(summary.AgentData.GroupID, account));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void CreateGroupReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupCreated != null)
            {
                Packet packet = e.Packet;
                CreateGroupReplyPacket reply = (CreateGroupReplyPacket)packet;

                string message = Utils.BytesToString(reply.ReplyData.Message);

                OnGroupCreatedReply(new GroupCreatedReplyEventArgs(reply.ReplyData.GroupID, reply.ReplyData.Success, message));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void JoinGroupReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupJoined != null)
            {
                Packet packet = e.Packet;
                JoinGroupReplyPacket reply = (JoinGroupReplyPacket)packet;

                OnGroupJoinedReply(new GroupOperationEventArgs(reply.GroupData.GroupID, reply.GroupData.Success));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void LeaveGroupReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_GroupLeft != null)
            {
                Packet packet = e.Packet;
                LeaveGroupReplyPacket reply = (LeaveGroupReplyPacket)packet;

                OnGroupLeaveReply(new GroupOperationEventArgs(reply.GroupData.GroupID, reply.GroupData.Success));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        private void UUIDGroupNameReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            UUIDGroupNameReplyPacket reply = (UUIDGroupNameReplyPacket)packet;
            UUIDGroupNameReplyPacket.UUIDNameBlockBlock[] blocks = reply.UUIDNameBlock;

            Dictionary<UUID, string> groupNames = new Dictionary<UUID, string>();

            foreach (UUIDGroupNameReplyPacket.UUIDNameBlockBlock block in blocks)
            {
                groupNames.Add(block.ID, Utils.BytesToString(block.GroupName));
                if (!GroupName2KeyCache.ContainsKey(block.ID))
                    GroupName2KeyCache.Add(block.ID, Utils.BytesToString(block.GroupName));
            }

            if (m_GroupNames != null)
            {
                OnGroupNamesReply(new GroupNamesEventArgs(groupNames));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void EjectGroupMemberReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            EjectGroupMemberReplyPacket reply = (EjectGroupMemberReplyPacket)packet;

            // TODO: On Success remove the member from the cache(s)

            if (m_GroupMemberEjected != null)
            {
                OnGroupMemberEjected(new GroupOperationEventArgs(reply.GroupData.GroupID, reply.EjectData.Success));
            }
        }

        #endregion Packet Handlers
    }

    #region EventArgs

    /// <summary>Contains the current groups your agent is a member of</summary>
    public class CurrentGroupsEventArgs : EventArgs
    {
        /// <summary>Get the current groups your agent is a member of</summary>
        public Dictionary<UUID, Group> Groups { get; }

        /// <summary>Construct a new instance of the CurrentGroupsEventArgs class</summary>
        /// <param name="groups">The current groups your agent is a member of</param>
        public CurrentGroupsEventArgs(Dictionary<UUID, Group> groups)
        {
            Groups = groups;
        }
    }

    /// <summary>A Dictionary of group names, where the Key is the groups ID and the value is the groups name</summary>
    public class GroupNamesEventArgs : EventArgs
    {
        /// <summary>Get the Group Names dictionary</summary>
        public Dictionary<UUID, string> GroupNames { get; }

        /// <summary>Construct a new instance of the GroupNamesEventArgs class</summary>
        /// <param name="groupNames">The Group names dictionary</param>
        public GroupNamesEventArgs(Dictionary<UUID, string> groupNames)
        {
            GroupNames = groupNames;
        }
    }

    /// <summary>Represents the members of a group</summary>
    public class GroupMembersReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID as returned by the request to correlate
        /// this result set and the request</summary>
        public UUID RequestID { get; }

        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the dictionary of members</summary>
        public Dictionary<UUID, GroupMember> Members { get; }

        /// <summary>
        /// Construct a new instance of the GroupMembersReplyEventArgs class
        /// </summary>
        /// <param name="requestID">The ID of the request</param>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="members">The membership list of the group</param>
        public GroupMembersReplyEventArgs(UUID requestID, UUID groupID, Dictionary<UUID, GroupMember> members)
        {
            RequestID = requestID;
            GroupID = groupID;
            Members = members;
        }
    }

    /// <summary>Represents the roles associated with a group</summary>
    public class GroupRolesDataReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID as returned by the request to correlate
        /// this result set and the request</summary>
        public UUID RequestID { get; }

        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the dictionary containing the roles</summary>
        public Dictionary<UUID, GroupRole> Roles { get; }

        /// <summary>Construct a new instance of the GroupRolesDataReplyEventArgs class</summary>
        /// <param name="requestID">The ID as returned by the request to correlate
        /// this result set and the request</param>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="roles">The dictionary containing the roles</param>
        public GroupRolesDataReplyEventArgs(UUID requestID, UUID groupID, Dictionary<UUID, GroupRole> roles)
        {
            RequestID = requestID;
            GroupID = groupID;
            Roles = roles;
        }
    }

    /// <summary>Represents the Role to Member mappings for a group</summary>
    public class GroupRolesMembersReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID as returned by the request to correlate
        /// this result set and the request</summary>
        public UUID RequestID { get; }

        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the member to roles map</summary>
        public List<KeyValuePair<UUID, UUID>> RolesMembers { get; }

        /// <summary>Construct a new instance of the GroupRolesMembersReplyEventArgs class</summary>
        /// <param name="requestID">The ID as returned by the request to correlate
        /// this result set and the request</param>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="rolesMembers">The member to roles map</param>
        public GroupRolesMembersReplyEventArgs(UUID requestID, UUID groupID, List<KeyValuePair<UUID, UUID>> rolesMembers)
        {
            RequestID = requestID;
            GroupID = groupID;
            RolesMembers = rolesMembers;
        }
    }

    /// <summary>Represents the titles for a group</summary>
    public class GroupTitlesReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID as returned by the request to correlate
        /// this result set and the request</summary>
        public UUID RequestID { get; }

        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the titles</summary>
        public Dictionary<UUID, GroupTitle> Titles { get; }

        /// <summary>Construct a new instance of the GroupTitlesReplyEventArgs class</summary>
        /// <param name="requestID">The ID as returned by the request to correlate
        /// this result set and the request</param>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="titles">The titles</param>
        public GroupTitlesReplyEventArgs(UUID requestID, UUID groupID, Dictionary<UUID, GroupTitle> titles)
        {
            RequestID = requestID;
            GroupID = groupID;
            Titles = titles;
        }
    }

    /// <summary>Represents the summary data for a group</summary>
    public class GroupAccountSummaryReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the summary data</summary>
        public GroupAccountSummary Summary { get; }

        /// <summary>Construct a new instance of the GroupAccountSummaryReplyEventArgs class</summary>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="summary">The summary data</param>
        public GroupAccountSummaryReplyEventArgs(UUID groupID, GroupAccountSummary summary)
        {
            GroupID = groupID;
            Summary = summary;
        }
    }

    /// <summary>A response to a group create request</summary>
    public class GroupCreatedReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>true of the  group was created successfully</summary>
        public bool Success { get; }

        /// <summary>A string containing the message</summary>
        public string Message { get; }

        /// <summary>Construct a new instance of the GroupCreatedReplyEventArgs class</summary>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="success">the success or failure of the request</param>
        /// <param name="message">A string containing additional information</param>
        public GroupCreatedReplyEventArgs(UUID groupID, bool success, string message)
        {
            GroupID = groupID;
            Success = success;
            Message = message;
        }
    }

    /// <summary>Represents a response to a request</summary>
    public class GroupOperationEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>true of the request was successful</summary>
        public bool Success { get; }

        /// <summary>Construct a new instance of the GroupOperationEventArgs class</summary>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="success">true of the request was successful</param>
        public GroupOperationEventArgs(UUID groupID, bool success)
        {
            GroupID = groupID;
            Success = success;
        }
    }

    /// <summary>Represents your agent leaving a group</summary>
    public class GroupDroppedEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Construct a new instance of the GroupDroppedEventArgs class</summary>
        /// <param name="groupID">The ID of the group</param>
        public GroupDroppedEventArgs(UUID groupID)
        {
            GroupID = groupID;
        }
    }

    /// <summary>Represents a list of active group notices</summary>
    public class GroupNoticesListReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the group</summary>
        public UUID GroupID { get; }

        /// <summary>Get the notices list</summary>
        public List<GroupNoticesListEntry> Notices { get; }

        /// <summary>Construct a new instance of the GroupNoticesListReplyEventArgs class</summary>
        /// <param name="groupID">The ID of the group</param>
        /// <param name="notices">The list containing active notices</param>
        public GroupNoticesListReplyEventArgs(UUID groupID, List<GroupNoticesListEntry> notices)
        {
            GroupID = groupID;
            Notices = notices;
        }
    }

    /// <summary>Represents the profile of a group</summary>
    public class GroupProfileEventArgs : EventArgs
    {
        /// <summary>Get the group profile</summary>
        public Group Group { get; }

        /// <summary>Construct a new instance of the GroupProfileEventArgs class</summary>
        /// <param name="group">The group profile</param>
        public GroupProfileEventArgs(Group group)
        {
            Group = group;
        }
    }

    /// <summary>
    /// Provides notification of a group invitation request sent by another Avatar
    /// </summary>
    /// <remarks>The <see cref="GroupInvitation"/> invitation is raised when another avatar makes an offer for our avatar
    /// to join a group.</remarks>
    public class GroupInvitationEventArgs : EventArgs
    {
        /// <summary>The ID of the Avatar sending the group invitation</summary>
        public UUID AgentID { get; }

        /// <summary>The name of the Avatar sending the group invitation</summary>
        public string FromName { get; }

        /// <summary>A message containing the request information which includes
        /// the name of the group, the groups charter and the fee to join details</summary>
        public string Message { get; }

        /// <summary>The Simulator</summary>
        public Simulator Simulator { get; }

        /// <summary>Set to true to accept invitation, false to decline</summary>
        public bool Accept { get; set; }

        public GroupInvitationEventArgs(Simulator simulator, UUID agentID, string agentName, string message)
        {
            Simulator = simulator;
            AgentID = agentID;
            FromName = agentName;
            Message = message;
        }
    }

    /// <summary>
    /// Result of the request for list of agents banned from a group
    /// </summary>
    public class BannedAgentsEventArgs : EventArgs
    {
        /// <summary> Indicates if list of banned agents for a group was successfully retrieved </summary>
        public UUID GroupID { get; }

        /// <summary> Indicates if list of banned agents for a group was successfully retrieved </summary>
        public bool Success { get; }

        /// <summary> Array containing a list of UUIDs of the agents banned from a group </summary>
        public Dictionary<UUID, DateTime> BannedAgents { get; }

        public BannedAgentsEventArgs(UUID groupID, bool success, Dictionary<UUID, DateTime> bannedAgents)
        {
            GroupID = groupID;
            Success = success;
            BannedAgents = bannedAgents;
        }
    }

    #endregion
}
