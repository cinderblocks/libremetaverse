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
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
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
        /// <summary>Number of members assigned to this role</summary>
        public uint Members;
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
    public struct Group : IEquatable<Group>
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
        /// <summary>Is the group open for enrollment to everyone</summary>
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

        public bool Equals(Group other)
        {
            return ID.Equals(other.ID) 
                   && InsigniaID.Equals(other.InsigniaID) 
                   && FounderID.Equals(other.FounderID) 
                   && OwnerRole.Equals(other.OwnerRole)
                   && Name == other.Name 
                   && Charter == other.Charter
                   && MemberTitle == other.MemberTitle
                   && OpenEnrollment == other.OpenEnrollment
                   && ShowInList == other.ShowInList 
                   && Powers == other.Powers 
                   && AcceptNotices == other.AcceptNotices
                   && AllowPublish == other.AllowPublish 
                   && MaturePublish == other.MaturePublish
                   && MembershipFee == other.MembershipFee 
                   && Money == other.Money 
                   && Contribution == other.Contribution 
                   && GroupMembershipCount == other.GroupMembershipCount 
                   && GroupRolesCount == other.GroupRolesCount 
                   && ListInProfile == other.ListInProfile;
        }

        public override bool Equals(object? obj)
        {
            return obj is Group other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ID.GetHashCode();
                hashCode = (hashCode * 397) ^ InsigniaID.GetHashCode();
                hashCode = (hashCode * 397) ^ FounderID.GetHashCode();
                hashCode = (hashCode * 397) ^ OwnerRole.GetHashCode();
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Charter != null ? Charter.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (MemberTitle != null ? MemberTitle.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ OpenEnrollment.GetHashCode();
                hashCode = (hashCode * 397) ^ ShowInList.GetHashCode();
                hashCode = (hashCode * 397) ^ Powers.GetHashCode();
                hashCode = (hashCode * 397) ^ AcceptNotices.GetHashCode();
                hashCode = (hashCode * 397) ^ AllowPublish.GetHashCode();
                hashCode = (hashCode * 397) ^ MaturePublish.GetHashCode();
                hashCode = (hashCode * 397) ^ MembershipFee;
                hashCode = (hashCode * 397) ^ Money;
                hashCode = (hashCode * 397) ^ Contribution;
                hashCode = (hashCode * 397) ^ GroupMembershipCount;
                hashCode = (hashCode * 397) ^ GroupRolesCount;
                hashCode = (hashCode * 397) ^ ListInProfile.GetHashCode();
                return hashCode;
            }
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
        /// <summary>The required ratio of yes/no votes required for vote to pass</summary>
        /// <remarks>Use constants from <see cref="GroupVoteMajority"/> for the standard thresholds:
        /// Simple Majority (0.5), Two-Thirds Majority (0.667), or Unanimous (1.0)</remarks>
        public float Majority;
        /// <summary>The duration in days votes are accepted</summary>
        public int Duration;
    }

    /// <summary>Named constants for the standard vote majority thresholds used in <see cref="GroupProposal.Majority"/></summary>
    public static class GroupVoteMajority
    {
        /// <summary>Simple majority: more than half (50%)</summary>
        public const float SimpleMajority = 0.5f;
        /// <summary>Two-thirds majority: approximately 66.7%</summary>
        public const float TwoThirdsMajority = 0.667f;
        /// <summary>Unanimous: all members must vote yes (100%)</summary>
        public const float Unanimous = 1.0f;
    }

    /// <summary>Role membership change action used with <see cref="GroupManager.AddToRole"/> and <see cref="GroupManager.RemoveFromRole"/></summary>
    public enum GroupRoleChangeType : uint
    {
        /// <summary>Add the member to the role</summary>
        AddToRole = 0,
        /// <summary>Remove the member from the role</summary>
        RemoveFromRole = 1,
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
    public struct ChatSessionMember : IEquatable<ChatSessionMember>
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

        public bool Equals(ChatSessionMember other)
        {
            return AvatarKey.Equals(other.AvatarKey) 
                   && CanVoiceChat == other.CanVoiceChat 
                   && IsModerator == other.IsModerator 
                   && MuteText == other.MuteText
                   && MuteVoice == other.MuteVoice;
        }

        public override bool Equals(object? obj)
        {
            return obj is ChatSessionMember other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = AvatarKey.GetHashCode();
                hashCode = (hashCode * 397) ^ CanVoiceChat.GetHashCode();
                hashCode = (hashCode * 397) ^ IsModerator.GetHashCode();
                hashCode = (hashCode * 397) ^ MuteText.GetHashCode();
                hashCode = (hashCode * 397) ^ MuteVoice.GetHashCode();
                return hashCode;
            }
        }
    }

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
}
