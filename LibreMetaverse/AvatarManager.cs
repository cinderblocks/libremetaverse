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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Http;
using OpenMetaverse.Packets;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{

    #region Structs
    /// <summary> Information about agents display name </summary>
    public class AgentDisplayName
    {
        /// <summary> Agent UUID </summary>
        public UUID ID;
        /// <summary> Username </summary>
        public string UserName;
        /// <summary> Display name </summary>
        public string DisplayName;
        /// <summary> First name (legacy) </summary>
        public string LegacyFirstName;
        /// <summary> Last name (legacy) </summary>
        public string LegacyLastName;
        /// <summary> Full name (legacy) </summary>
        public string LegacyFullName => $"{LegacyFirstName} {LegacyLastName}";
        /// <summary> Is display name default display name </summary>
        public bool IsDefaultDisplayName;
        /// <summary> Cache display name until </summary>
        public DateTime NextUpdate;
        /// <summary> Last updated timestamp </summary>
        public DateTime Updated;

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
                ["username"] = UserName,
                ["display_name"] = DisplayName,
                ["legacy_first_name"] = LegacyFirstName,
                ["legacy_last_name"] = LegacyLastName,
                ["is_display_name_default"] = IsDefaultDisplayName,
                ["display_name_next_update"] = NextUpdate,
                ["last_updated"] = Updated
            };
            return map;
        }

        public override string ToString()
        {
            return Helpers.StructToString(this);
        }
    }

    /// <summary>
    /// Holds group information for Avatars such as those you might find in a profile
    /// </summary>
    public struct AvatarGroup
    {
        /// <summary>true of Avatar accepts group notices</summary>
        public bool AcceptNotices;
        /// <summary>Groups Key</summary>
        public UUID GroupID;
        /// <summary>Texture Key for groups insignia</summary>
        public UUID GroupInsigniaID;
        /// <summary>Name of the group</summary>
        public string GroupName;
        /// <summary>Powers avatar has in the group</summary>
        public GroupPowers GroupPowers;
        /// <summary>Avatars Currently selected title</summary>
        public string GroupTitle;
        /// <summary>true of Avatar has chosen to list this in their profile</summary>
        public bool ListInProfile;
    }

    /// <summary>
    /// Contains an animation currently being played by an agent
    /// </summary>
    public struct Animation
    {
        /// <summary>The ID of the animation asset</summary>
        public UUID AnimationID;
        /// <summary>A number to indicate start order of currently playing animations</summary>
        /// <remarks>On Linden Grids this number is unique per region, with OpenSim it is per client</remarks>
        public int AnimationSequence;
        /// <summary></summary>
        public UUID AnimationSourceObjectID;
    }

    /// <summary>
    /// Holds group information on an individual profile pick
    /// </summary>
    public struct ProfilePick
    {
        public UUID PickID;
        public UUID CreatorID;
        public bool TopPick;
        public UUID ParcelID;
        public string Name;
        public string Desc;
        public UUID SnapshotID;
        public string User;
        public string OriginalName;
        public string SimName;
        public Vector3d PosGlobal;
        public int SortOrder;
        public bool Enabled;
    }

    public struct ClassifiedAd
    {
        public UUID ClassifiedID;
        public uint Catagory;
        public UUID ParcelID;
        public uint ParentEstate;
        public UUID SnapShotID;
        public Vector3d Position;
        public byte ClassifiedFlags;
        public int Price;
        public string Name;
        public string Desc;
    }
    #endregion

    /// <summary>
    /// Retrieve friend status notifications, and retrieve avatar names and
    /// profiles
    /// </summary>
    public class AvatarManager
    {
        const int MAX_UUIDS_PER_PACKET = 100;

        #region Events
        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarAnimationEventArgs> m_AvatarAnimation;

        ///<summary>Raises the AvatarAnimation Event</summary>
        /// <param name="e">An AvatarAnimationEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarAnimation(AvatarAnimationEventArgs e)
        {
            EventHandler<AvatarAnimationEventArgs> handler = m_AvatarAnimation;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarAnimationLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// an agents animation playlist</summary>
        public event EventHandler<AvatarAnimationEventArgs> AvatarAnimation
        {
            add { lock (m_AvatarAnimationLock) { m_AvatarAnimation += value; } }
            remove { lock (m_AvatarAnimationLock) { m_AvatarAnimation -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarAppearanceEventArgs> m_AvatarAppearance;

        ///<summary>Raises the AvatarAppearance Event</summary>
        /// <param name="e">A AvatarAppearanceEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarAppearance(AvatarAppearanceEventArgs e)
        {
            EventHandler<AvatarAppearanceEventArgs> handler = m_AvatarAppearance;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarAppearanceLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the appearance information for an agent</summary>
        public event EventHandler<AvatarAppearanceEventArgs> AvatarAppearance
        {
            add { lock (m_AvatarAppearanceLock) { m_AvatarAppearance += value; } }
            remove { lock (m_AvatarAppearanceLock) { m_AvatarAppearance -= value; } }
        }

        internal void TriggerAvatarAppearanceMessage(AvatarAppearanceEventArgs args)
        {
            lock (m_AvatarAppearanceLock)
            {
                m_AvatarAppearance?.Invoke(this, args);
            }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<UUIDNameReplyEventArgs> m_UUIDNameReply;

        ///<summary>Raises the UUIDNameReply Event</summary>
        /// <param name="e">A UUIDNameReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnUUIDNameReply(UUIDNameReplyEventArgs e)
        {
            EventHandler<UUIDNameReplyEventArgs> handler = m_UUIDNameReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_UUIDNameReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// agent names/id values</summary>
        public event EventHandler<UUIDNameReplyEventArgs> UUIDNameReply
        {
            add { lock (m_UUIDNameReplyLock) { m_UUIDNameReply += value; } }
            remove { lock (m_UUIDNameReplyLock) { m_UUIDNameReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarInterestsReplyEventArgs> m_AvatarInterestsReply;

        ///<summary>Raises the AvatarInterestsReply Event</summary>
        /// <param name="e">A AvatarInterestsReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarInterestsReply(AvatarInterestsReplyEventArgs e)
        {
            EventHandler<AvatarInterestsReplyEventArgs> handler = m_AvatarInterestsReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarInterestsReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the interests listed in an agents profile</summary>
        public event EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReply
        {
            add { lock (m_AvatarInterestsReplyLock) { m_AvatarInterestsReply += value; } }
            remove { lock (m_AvatarInterestsReplyLock) { m_AvatarInterestsReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarNotesReplyEventArgs> m_AvatarNotesReply;

        ///<summary>Raises the AvatarNotesReply Event</summary>
        /// <param name="e">A AvatarNotesReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarNotesReply(AvatarNotesReplyEventArgs e)
        {
            EventHandler<AvatarNotesReplyEventArgs> handler = m_AvatarNotesReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarNotesReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the private notes listed in an agents profile</summary>
        public event EventHandler<AvatarNotesReplyEventArgs> AvatarNotesReply
        {
            add { lock (m_AvatarNotesReplyLock) { m_AvatarNotesReply += value; } }
            remove { lock (m_AvatarNotesReplyLock) { m_AvatarNotesReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarPropertiesReplyEventArgs> m_AvatarPropertiesReply;

        ///<summary>Raises the AvatarPropertiesReply Event</summary>
        /// <param name="e">A AvatarPropertiesReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarPropertiesReply(AvatarPropertiesReplyEventArgs e)
        {
            EventHandler<AvatarPropertiesReplyEventArgs> handler = m_AvatarPropertiesReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarPropertiesReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// profile property information for an agent</summary>
        public event EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReply
        {
            add { lock (m_AvatarPropertiesReplyLock) { m_AvatarPropertiesReply += value; } }
            remove { lock (m_AvatarPropertiesReplyLock) { m_AvatarPropertiesReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarGroupsReplyEventArgs> m_AvatarGroupsReply;

        ///<summary>Raises the AvatarGroupsReply Event</summary>
        /// <param name="e">A AvatarGroupsReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarGroupsReply(AvatarGroupsReplyEventArgs e)
        {
            EventHandler<AvatarGroupsReplyEventArgs> handler = m_AvatarGroupsReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarGroupsReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the group membership an agent is a member of</summary>
        public event EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReply
        {
            add { lock (m_AvatarGroupsReplyLock) { m_AvatarGroupsReply += value; } }
            remove { lock (m_AvatarGroupsReplyLock) { m_AvatarGroupsReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarPickerReplyEventArgs> m_AvatarPickerReply;

        ///<summary>Raises the AvatarPickerReply Event</summary>
        /// <param name="e">A AvatarPickerReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarPickerReply(AvatarPickerReplyEventArgs e)
        {
            EventHandler<AvatarPickerReplyEventArgs> handler = m_AvatarPickerReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarPickerReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// name/id pair</summary>
        public event EventHandler<AvatarPickerReplyEventArgs> AvatarPickerReply
        {
            add { lock (m_AvatarPickerReplyLock) { m_AvatarPickerReply += value; } }
            remove { lock (m_AvatarPickerReplyLock) { m_AvatarPickerReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ViewerEffectPointAtEventArgs> m_ViewerEffectPointAt;

        ///<summary>Raises the ViewerEffectPointAt Event</summary>
        /// <param name="e">A ViewerEffectPointAtEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnViewerEffectPointAt(ViewerEffectPointAtEventArgs e)
        {
            EventHandler<ViewerEffectPointAtEventArgs> handler = m_ViewerEffectPointAt;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ViewerEffectPointAtLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the objects and effect when an agent is pointing at</summary>
        public event EventHandler<ViewerEffectPointAtEventArgs> ViewerEffectPointAt
        {
            add { lock (m_ViewerEffectPointAtLock) { m_ViewerEffectPointAt += value; } }
            remove { lock (m_ViewerEffectPointAtLock) { m_ViewerEffectPointAt -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ViewerEffectLookAtEventArgs> m_ViewerEffectLookAt;

        ///<summary>Raises the ViewerEffectLookAt Event</summary>
        /// <param name="e">A ViewerEffectLookAtEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnViewerEffectLookAt(ViewerEffectLookAtEventArgs e)
        {
            EventHandler<ViewerEffectLookAtEventArgs> handler = m_ViewerEffectLookAt;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ViewerEffectLookAtLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the objects and effect when an agent is looking at</summary>
        public event EventHandler<ViewerEffectLookAtEventArgs> ViewerEffectLookAt
        {
            add { lock (m_ViewerEffectLookAtLock) { m_ViewerEffectLookAt += value; } }
            remove { lock (m_ViewerEffectLookAtLock) { m_ViewerEffectLookAt -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ViewerEffectEventArgs> m_ViewerEffect;

        ///<summary>Raises the ViewerEffect Event</summary>
        /// <param name="e">A ViewerEffectEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnViewerEffect(ViewerEffectEventArgs e)
        {
            EventHandler<ViewerEffectEventArgs> handler = m_ViewerEffect;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ViewerEffectLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// an agents viewer effect information</summary>
        public event EventHandler<ViewerEffectEventArgs> ViewerEffect
        {
            add { lock (m_ViewerEffectLock) { m_ViewerEffect += value; } }
            remove { lock (m_ViewerEffectLock) { m_ViewerEffect -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarPicksReplyEventArgs> m_AvatarPicksReply;

        ///<summary>Raises the AvatarPicksReply Event</summary>
        /// <param name="e">A AvatarPicksReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarPicksReply(AvatarPicksReplyEventArgs e)
        {
            EventHandler<AvatarPicksReplyEventArgs> handler = m_AvatarPicksReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarPicksReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the top picks from an agents profile</summary>
        public event EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReply
        {
            add { lock (m_AvatarPicksReplyLock) { m_AvatarPicksReply += value; } }
            remove { lock (m_AvatarPicksReplyLock) { m_AvatarPicksReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<PickInfoReplyEventArgs> m_PickInfoReply;

        ///<summary>Raises the PickInfoReply Event</summary>
        /// <param name="e">A PickInfoReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnPickInfoReply(PickInfoReplyEventArgs e)
        {
            EventHandler<PickInfoReplyEventArgs> handler = m_PickInfoReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_PickInfoReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the Pick details</summary>
        public event EventHandler<PickInfoReplyEventArgs> PickInfoReply
        {
            add { lock (m_PickInfoReplyLock) { m_PickInfoReply += value; } }
            remove { lock (m_PickInfoReplyLock) { m_PickInfoReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<AvatarClassifiedReplyEventArgs> m_AvatarClassifiedReply;

        ///<summary>Raises the AvatarClassifiedReply Event</summary>
        /// <param name="e">A AvatarClassifiedReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnAvatarClassifiedReply(AvatarClassifiedReplyEventArgs e)
        {
            EventHandler<AvatarClassifiedReplyEventArgs> handler = m_AvatarClassifiedReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_AvatarClassifiedReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the classified ads an agent has placed</summary>
        public event EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReply
        {
            add { lock (m_AvatarClassifiedReplyLock) { m_AvatarClassifiedReply += value; } }
            remove { lock (m_AvatarClassifiedReplyLock) { m_AvatarClassifiedReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<ClassifiedInfoReplyEventArgs> m_ClassifiedInfoReply;

        ///<summary>Raises the ClassifiedInfoReply Event</summary>
        /// <param name="e">A ClassifiedInfoReplyEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnClassifiedInfoReply(ClassifiedInfoReplyEventArgs e)
        {
            EventHandler<ClassifiedInfoReplyEventArgs> handler = m_ClassifiedInfoReply;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_ClassifiedInfoReplyLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the details of a classified ad</summary>
        public event EventHandler<ClassifiedInfoReplyEventArgs> ClassifiedInfoReply
        {
            add { lock (m_ClassifiedInfoReplyLock) { m_ClassifiedInfoReply += value; } }
            remove { lock (m_ClassifiedInfoReplyLock) { m_ClassifiedInfoReply -= value; } }
        }

        /// <summary>The event subscribers, null if no subscribers</summary>
        private EventHandler<DisplayNameUpdateEventArgs> m_DisplayNameUpdate;

        ///<summary>Raises the DisplayNameUpdate Event</summary>
        /// <param name="e">A DisplayNameUpdateEventArgs object containing
        /// the data sent from the simulator</param>
        protected virtual void OnDisplayNameUpdate(DisplayNameUpdateEventArgs e)
        {
            EventHandler<DisplayNameUpdateEventArgs> handler = m_DisplayNameUpdate;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_DisplayNameUpdateLock = new object();

        /// <summary>Raised when the simulator sends us data containing
        /// the details of display name change</summary>
        public event EventHandler<DisplayNameUpdateEventArgs> DisplayNameUpdate
        {
            add { lock (m_DisplayNameUpdateLock) { m_DisplayNameUpdate += value; } }
            remove { lock (m_DisplayNameUpdateLock) { m_DisplayNameUpdate -= value; } }
        }

        #endregion Events

        #region Delegates
        /// <summary>
        /// Callback giving results when fetching display names
        /// </summary>
        /// <param name="success">If the request was successful</param>
        /// <param name="names">Array of display names</param>
        /// <param name="badIDs">Array of UUIDs that could not be fetched</param>
        public delegate void DisplayNamesCallback(bool success, AgentDisplayName[] names, UUID[] badIDs);

        /// <summary>
        /// Callback giving results when fetching AgentProfile
        /// </summary>
        /// <param name="success">If the request was successful</param>
        /// <param name="profile">AgentProfile result</param>
        public delegate void AgentProfileCallback(bool success, AgentProfileMessage profile);
        #endregion Delegates

        private readonly GridClient Client;

        /// <summary>
        /// Represents other avatars
        /// </summary>
        /// <param name="client"></param>
        public AvatarManager(GridClient client)
        {
            Client = client;

            // Avatar appearance callback
            Client.Network.RegisterCallback(PacketType.AvatarAppearance, AvatarAppearanceHandler);

            // Avatar profile callbacks
            Client.Network.RegisterCallback(PacketType.AvatarPropertiesReply, AvatarPropertiesHandler);
            // Client.Network.RegisterCallback(PacketType.AvatarStatisticsReply, AvatarStatisticsHandler);
            Client.Network.RegisterCallback(PacketType.AvatarInterestsReply, AvatarInterestsHandler);
            Client.Network.RegisterCallback(PacketType.AvatarNotesReply, AvatarNotesHandler);

            // Avatar group callback
            Client.Network.RegisterCallback(PacketType.AvatarGroupsReply, AvatarGroupsReplyHandler);
            Client.Network.RegisterEventCallback("AgentGroupDataUpdate", AvatarGroupsReplyMessageHandler);
            Client.Network.RegisterEventCallback("AvatarGroupsReply", AvatarGroupsReplyMessageHandler);

            // Viewer effect callback
            Client.Network.RegisterCallback(PacketType.ViewerEffect, ViewerEffectHandler);

            // Other callbacks
            Client.Network.RegisterCallback(PacketType.UUIDNameReply, UUIDNameReplyHandler);
            Client.Network.RegisterCallback(PacketType.AvatarPickerReply, AvatarPickerReplyHandler);
            Client.Network.RegisterCallback(PacketType.AvatarAnimation, AvatarAnimationHandler);

            // Picks callbacks
            Client.Network.RegisterCallback(PacketType.AvatarPicksReply, AvatarPicksReplyHandler);
            Client.Network.RegisterCallback(PacketType.PickInfoReply, PickInfoReplyHandler);

            // Classifieds callbacks
            Client.Network.RegisterCallback(PacketType.AvatarClassifiedReply, AvatarClassifiedReplyHandler);
            Client.Network.RegisterCallback(PacketType.ClassifiedInfoReply, ClassifiedInfoReplyHandler);

            Client.Network.RegisterEventCallback("DisplayNameUpdate", DisplayNameUpdateMessageHandler);
        }

        /// <summary>Tracks the specified avatar on your map</summary>
        /// <param name="preyID">Avatar ID to track</param>
        public void RequestTrackAgent(UUID preyID)
        {
            TrackAgentPacket p = new TrackAgentPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                TargetData = {PreyID = preyID}
            };
            Client.Network.SendPacket(p);
        }

        /// <summary>
        /// Request a single avatar name
        /// </summary>
        /// <param name="id">The avatar key to retrieve a name for</param>
        public void RequestAvatarName(UUID id)
        {
            UUIDNameRequestPacket request =
                new UUIDNameRequestPacket {UUIDNameBlock = new UUIDNameRequestPacket.UUIDNameBlockBlock[1]};
            request.UUIDNameBlock[0] = new UUIDNameRequestPacket.UUIDNameBlockBlock {ID = id};

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Request a list of avatar names
        /// </summary>
        /// <param name="ids">The avatar keys to retrieve names for</param>
        public void RequestAvatarNames(List<UUID> ids)
        {
            int m = MAX_UUIDS_PER_PACKET;
            int n = ids.Count / m; // Number of full requests to make
            int i = 0;

            UUIDNameRequestPacket request;

            for (int j = 0; j < n; j++)
            {
                request = new UUIDNameRequestPacket {UUIDNameBlock = new UUIDNameRequestPacket.UUIDNameBlockBlock[m]};

                for (; i < (j + 1) * m; i++)
                {
                    request.UUIDNameBlock[i % m] = new UUIDNameRequestPacket.UUIDNameBlockBlock {ID = ids[i]};
                }

                Client.Network.SendPacket(request);
            }

            // Get any remaining names after left after the full requests
            if (ids.Count > n * m)
            {
                request = new UUIDNameRequestPacket
                {
                    UUIDNameBlock = new UUIDNameRequestPacket.UUIDNameBlockBlock[ids.Count - n * m]
                };

                for (; i < ids.Count; i++)
                {
                    request.UUIDNameBlock[i % m] = new UUIDNameRequestPacket.UUIDNameBlockBlock {ID = ids[i]};
                }

                Client.Network.SendPacket(request);
            }
        }

        /// <summary>
        /// Check if Display Names functionality is available
        /// </summary>
        /// <returns>True if Display name functionality is available</returns>
        public bool DisplayNamesAvailable()
        {
            return Client.Network.CurrentSim?.Caps?.CapabilityURI("GetDisplayNames") != null;
        }

        /// <summary>
        /// Request retrieval of display names (max 90 names per request)
        /// </summary>
        /// <param name="ids">List of UUIDs to lookup</param>
        /// <param name="callback">Callback to report result of the operation</param>
        /// <param name="cancellationToken"></param>
        public async Task GetDisplayNames(List<UUID> ids, DisplayNamesCallback callback, CancellationToken cancellationToken = default)
        {
            if (!DisplayNamesAvailable() || ids.Count == 0)
            {
                callback(false, null, null);
            }

            var uri = new UriBuilder(Client.Network.CurrentSim.Caps.CapabilityURI("GetDisplayNames"))
            {
                Query = "ids=" + string.Join("&ids=", ids)
            };

            await Client.HttpCapsClient.GetRequestAsync(uri.Uri, cancellationToken, (response, data, error) =>
            {
                try
                {
                    if (error != null) { throw error; }
                    GetDisplayNamesMessage msg = new GetDisplayNamesMessage();
                    OSD result = OSDParser.Deserialize(data);
                    if (result is OSDMap respMap)
                    {
                        msg.Deserialize(respMap);
                        callback(true, msg.Agents, msg.BadIDs);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Failed to call GetDisplayNames capability: ",
                        Helpers.LogLevel.Warning, Client, ex);
                    callback(false, null, null);
                }
            });
        }

        /// <summary>
        /// Start a request for Avatar Properties
        /// </summary>
        /// <param name="avatarid"></param>
        public void RequestAvatarProperties(UUID avatarid)
        {
            AvatarPropertiesRequestPacket aprp =
                new AvatarPropertiesRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        AvatarID = avatarid
                    }
                };
            Client.Network.SendPacket(aprp);
        }

        /// <summary>
        /// Check if AgentProfile functionality is available
        /// </summary>
        /// <returns>True if AgentProfile functionality is available</returns>
        public bool AgentProfileAvailable()
        {
            return Client.Network.CurrentSim?.Caps?.CapabilityURI("AgentProfile") != null;
        }

        /// <summary>
        /// Requests the AgentProfile for the specified avatar
        /// </summary>
        /// <param name="avatarid">Avatar to request the AgentProfile of</param>
        /// <param name="callback">Callback to handle the AgentProfile response</param>
        /// <param name="cancellationToken"></param>
        public async Task RequestAgentProfile(UUID avatarid, AgentProfileCallback callback, CancellationToken cancellationToken = default)
        {
            if (!AgentProfileAvailable())
            {
                callback(false, null);
                return;
            }

            var baseUri = Client.Network.CurrentSim.Caps.CapabilityURI("AgentProfile");
            var uri = new Uri($"{baseUri}/{avatarid}");

            await Client.HttpCapsClient.GetRequestAsync(uri, cancellationToken, (response, data, error) =>
            {
                try
                {
                    if (error != null)
                    {
                        throw error;
                    }

                    var msg = new AgentProfileMessage();
                    var result = OSDParser.Deserialize(data);
                    if (result is OSDMap respMap)
                    {
                        msg.Deserialize(respMap);
                        callback(true, msg);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Failed to call AgentProfile capability: ", Helpers.LogLevel.Warning, Client, ex);
                    callback(false, null);
                }
            });
        }

        /// <summary>
        /// Search for an avatar (first name, last name)
        /// </summary>
        /// <param name="name">The name to search for</param>
        /// <param name="queryID">An ID to associate with this query</param>
        public void RequestAvatarNameSearch(string name, UUID queryID)
        {
            AvatarPickerRequestPacket aprp =
                new AvatarPickerRequestPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID,
                        QueryID = queryID
                    },
                    Data = {Name = Utils.StringToBytes(name)}
                };
            Client.Network.SendPacket(aprp);
        }

        /// <summary>
        /// Request avatar notes from simulator
        /// </summary>
        /// <param name="avatarid">Target agent UUID</param>
        public void RequestAvatarNotes(UUID avatarid)
        {
            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes("avatarnotesrequest"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[1]
            };

            gmp.ParamList[0] =
                new GenericMessagePacket.ParamListBlock
                {
                    Parameter = Utils.StringToBytes(avatarid.ToString())
                };
            Client.Network.SendPacket(gmp);
        }

        /// <summary>
        /// Start a request for Avatar Picks
        /// </summary>
        /// <param name="avatarid">UUID of the avatar</param>
        public void RequestAvatarPicks(UUID avatarid)
        {
            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes("avatarpicksrequest"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[1]
            };

            gmp.ParamList[0] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(avatarid.ToString())};

            Client.Network.SendPacket(gmp);
        }

        /// <summary>
        /// Start a request for Avatar Classifieds
        /// </summary>
        /// <param name="avatarid">UUID of the avatar</param>
        public void RequestAvatarClassified(UUID avatarid)
        {
            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes("avatarclassifiedsrequest"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[1]
            };
            gmp.ParamList[0] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(avatarid.ToString())};

            Client.Network.SendPacket(gmp);
        }

        /// <summary>
        /// Start a request for details of a specific profile pick
        /// </summary>
        /// <param name="avatarid">UUID of the avatar</param>
        /// <param name="pickid">UUID of the profile pick</param>
        public void RequestPickInfo(UUID avatarid, UUID pickid)
        {
            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes("pickinforequest"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[2]
            };
            gmp.ParamList[0] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(avatarid.ToString())};
            gmp.ParamList[1] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(pickid.ToString())};

            Client.Network.SendPacket(gmp);
        }

        /// <summary>
        /// Start a request for details of a specific profile classified
        /// </summary>
        /// <param name="avatarid">UUID of the avatar</param>
        /// <param name="classifiedid">UUID of the profile classified</param>
        public void RequestClassifiedInfo(UUID avatarid, UUID classifiedid)
        {
            GenericMessagePacket gmp = new GenericMessagePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    TransactionID = UUID.Zero
                },
                MethodData =
                {
                    Method = Utils.StringToBytes("classifiedinforequest"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[2]
            };
            gmp.ParamList[0] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(avatarid.ToString())};
            gmp.ParamList[1] =
                new GenericMessagePacket.ParamListBlock {Parameter = Utils.StringToBytes(classifiedid.ToString())};

            Client.Network.SendPacket(gmp);
        }

        #region Packet Handlers

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void UUIDNameReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_UUIDNameReply != null)
            {
                Packet packet = e.Packet;
                var names = new Dictionary<UUID, string>();
                UUIDNameReplyPacket reply = (UUIDNameReplyPacket)packet;

                foreach (var block in reply.UUIDNameBlock)
                {
                    names[block.ID] = Utils.BytesToString(block.FirstName) +
                        " " + Utils.BytesToString(block.LastName);
                }

                OnUUIDNameReply(new UUIDNameReplyEventArgs(names));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarAnimationHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            AvatarAnimationPacket data = (AvatarAnimationPacket)packet;

            var signaledAnimations = new List<Animation>(data.AnimationList.Length);

            for (int i = 0; i < data.AnimationList.Length; i++)
            {
                Animation animation = new Animation
                {
                    AnimationID = data.AnimationList[i].AnimID,
                    AnimationSequence = data.AnimationList[i].AnimSequenceID
                };
                if (i < data.AnimationSourceList.Length)
                {
                    animation.AnimationSourceObjectID = data.AnimationSourceList[i].ObjectID;
                }

                signaledAnimations.Add(animation);
            }

            bool found = false;
            foreach (var a in e.Simulator.ObjectsAvatars)
            {
                if (a.Value == null || a.Value.ID != data.Sender.ID) { continue; }

                found = true;
                var av = a.Value;
                lock (av)
                {
                    av.Animations = signaledAnimations;
                }

            }

            if (!found)
            {
                e.Simulator.Client.Objects.RequestObjectPropertiesFamily(e.Simulator, data.Sender.ID);
            }

            OnAvatarAnimation(new AvatarAnimationEventArgs(data.Sender.ID, signaledAnimations));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarAppearanceHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarAppearance != null || Client.Settings.AVATAR_TRACKING)
            {
                Packet packet = e.Packet;
                Simulator simulator = e.Simulator;

                AvatarAppearancePacket appearance = (AvatarAppearancePacket)packet;

                var hoverHeight = Vector3.Zero;

                if (appearance.AppearanceHover != null && appearance.AppearanceHover.Length > 0)
                {
                    hoverHeight = appearance.AppearanceHover[0].HoverHeight;
                }

                var visualParams = appearance.VisualParam.Select(block => block.ParamValue).ToList();
                
                var textureEntry = new Primitive.TextureEntry(appearance.ObjectData.TextureEntry, 0,
                        appearance.ObjectData.TextureEntry.Length);

                var defaultTexture = textureEntry.DefaultTexture;
                var faceTextures = textureEntry.FaceTextures;

                byte appearanceVersion = 0;
                int COFVersion = 0;
                int childCount = -1;
                AppearanceFlags appearanceFlags = 0;

                if (appearance.AppearanceData != null && appearance.AppearanceData.Length > 0)
                {
                    appearanceVersion = appearance.AppearanceData[0].AppearanceVersion;
                    COFVersion = appearance.AppearanceData[0].CofVersion;
                    appearanceFlags = (AppearanceFlags)appearance.AppearanceData[0].Flags;
                }

                if (appearance.AttachmentBlock != null && appearance.AttachmentBlock.Length > 0)
                {
                    if (appearance.AttachmentBlock != null && appearance.AttachmentBlock.Length > 0)
                    {
                        foreach (var a in e.Simulator.ObjectsAvatars)
                        {
                            if (a.Value == null || a.Value.ID != appearance.Sender.ID) { continue; }

                            var av = a.Value;
                            lock (av)
                            {
                                av.Attachments = new List<Avatar.Attachment>();
                                foreach (var block in appearance.AttachmentBlock)
                                {
                                    av.Attachments.Add(new Avatar.Attachment
                                    {
                                        AttachmentID = block.ID,
                                        AttachmentPoint = block.AttachmentPoint
                                    });
                                }

                                childCount = av.ChildCount = av.Attachments.Count;
                            }
                        }
                    }
                }

                // We need to ignore this for avatar self-appearance.
                // The data in this packet is incorrect, and only the 
                // mesh bake CAP response can be treated as fully reliable.
                if (appearance.Sender.ID == Client.Self.AgentID) { return; }

                foreach (var a in e.Simulator.ObjectsAvatars)
                {
                    if (a.Value == null || a.Value.ID != appearance.Sender.ID) { continue; }

                    var av = a.Value;
                    lock (av)
                    {
                        av.Textures = textureEntry;
                        av.VisualParameters = visualParams.ToArray();
                        av.AppearanceVersion = appearanceVersion;
                        av.COFVersion = COFVersion;
                        av.AppearanceFlags = appearanceFlags;
                        av.HoverHeight = hoverHeight;
                    }
                }

                OnAvatarAppearance(new AvatarAppearanceEventArgs(simulator,
                    appearance.Sender.ID,
                    appearance.Sender.IsTrial,
                    defaultTexture,
                    faceTextures,
                    visualParams,
                    appearanceVersion,
                    COFVersion,
                    appearanceFlags,
                    childCount));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarPropertiesHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarPropertiesReply != null)
            {
                Packet packet = e.Packet;
                AvatarPropertiesReplyPacket reply = (AvatarPropertiesReplyPacket)packet;
                Avatar.AvatarProperties properties =
                    new Avatar.AvatarProperties
                    {
                        ProfileImage = reply.PropertiesData.ImageID,
                        FirstLifeImage = reply.PropertiesData.FLImageID,
                        Partner = reply.PropertiesData.PartnerID,
                        AboutText = Utils.BytesToString(reply.PropertiesData.AboutText),
                        FirstLifeText = Utils.BytesToString(reply.PropertiesData.FLAboutText),
                        BornOn = Utils.BytesToString(reply.PropertiesData.BornOn)
                    };

                if (reply.PropertiesData.CharterMember.Length == 1)
                {
                    uint charter = Utils.BytesToUInt(reply.PropertiesData.CharterMember);
                    if (charter == 0)
                    {
                        properties.CharterMember = "Resident";
                    }
                    else if (charter == 1)
                    {
                        properties.CharterMember = "Trial";
                    }
                    else if (charter == 2)
                    {
                        properties.CharterMember = "Charter";
                    }
                    else if (charter == 3)
                    {
                        properties.CharterMember = "Employee";
                    }
                }
                else if (reply.PropertiesData.CharterMember.Length > 1)
                {
                    properties.CharterMember = Utils.BytesToString(reply.PropertiesData.CharterMember);
                }
                properties.Flags = (ProfileFlags)reply.PropertiesData.Flags;
                properties.ProfileURL = Utils.BytesToString(reply.PropertiesData.ProfileURL);

                OnAvatarPropertiesReply(new AvatarPropertiesReplyEventArgs(reply.AgentData.AvatarID, properties));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarInterestsHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarInterestsReply != null)
            {
                Packet packet = e.Packet;

                AvatarInterestsReplyPacket airp = (AvatarInterestsReplyPacket)packet;
                Avatar.Interests interests = new Avatar.Interests
                {
                    WantToMask = airp.PropertiesData.WantToMask,
                    WantToText = Utils.BytesToString(airp.PropertiesData.WantToText),
                    SkillsMask = airp.PropertiesData.SkillsMask,
                    SkillsText = Utils.BytesToString(airp.PropertiesData.SkillsText),
                    LanguagesText = Utils.BytesToString(airp.PropertiesData.LanguagesText)
                };
                OnAvatarInterestsReply(new AvatarInterestsReplyEventArgs(airp.AgentData.AvatarID, interests));
            }
        }

        protected void AvatarNotesHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarNotesReply != null)
            {
                Packet packet = e.Packet;
                AvatarNotesReplyPacket anrp = (AvatarNotesReplyPacket)packet;
                string notes = Utils.BytesToString(anrp.Data.Notes);

                OnAvatarNotesReply(new AvatarNotesReplyEventArgs(anrp.Data.TargetID, notes));
            }
        }

        /// <summary>
        /// EQ Message fired when someone nearby changes their display name
        /// </summary>
        /// <param name="capsKey">The message key</param>
        /// <param name="message">the IMessage object containing the deserialized data sent from the simulator</param>
        /// <param name="simulator">The <see cref="Simulator"/> which originated the packet</param>
        protected void DisplayNameUpdateMessageHandler(string capsKey, IMessage message, Simulator simulator)
        {
            if (m_DisplayNameUpdate != null)
            {
                DisplayNameUpdateMessage msg = (DisplayNameUpdateMessage)message;
                OnDisplayNameUpdate(new DisplayNameUpdateEventArgs(msg.OldDisplayName, msg.DisplayName));
            }
        }

        /// <summary>
        /// Crossed region handler for message that comes across the EventQueue. Sent to an agent
        /// when the agent crosses a sim border into a new region.
        /// </summary>
        /// <param name="capsKey">The message key</param>
        /// <param name="message">the IMessage object containing the deserialized data sent from the simulator</param>
        /// <param name="simulator">The <see cref="Simulator"/> which originated the packet</param>
        protected void AvatarGroupsReplyMessageHandler(string capsKey, IMessage message, Simulator simulator)
        {
            AgentGroupDataUpdateMessage msg = (AgentGroupDataUpdateMessage)message;
            List<AvatarGroup> avatarGroups = new List<AvatarGroup>(msg.GroupDataBlock.Length);
            for (int i = 0; i < msg.GroupDataBlock.Length; i++)
            {
                AvatarGroup avatarGroup = new AvatarGroup
                {
                    AcceptNotices = msg.GroupDataBlock[i].AcceptNotices,
                    GroupID = msg.GroupDataBlock[i].GroupID,
                    GroupInsigniaID = msg.GroupDataBlock[i].GroupInsigniaID,
                    GroupName = msg.GroupDataBlock[i].GroupName,
                    GroupPowers = msg.GroupDataBlock[i].GroupPowers,
                    GroupTitle = msg.GroupDataBlock[i].GroupTitle,
                    ListInProfile = msg.NewGroupDataBlock[i].ListInProfile
                };

                avatarGroups.Add(avatarGroup);
            }

            OnAvatarGroupsReply(new AvatarGroupsReplyEventArgs(msg.AvatarID, avatarGroups));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarGroupsReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarGroupsReply != null)
            {
                Packet packet = e.Packet;
                AvatarGroupsReplyPacket groups = (AvatarGroupsReplyPacket)packet;
                List<AvatarGroup> avatarGroups = new List<AvatarGroup>(groups.GroupData.Length);

                foreach (AvatarGroupsReplyPacket.GroupDataBlock groupData in groups.GroupData)
                {
                    AvatarGroup avatarGroup = new AvatarGroup
                    {
                        AcceptNotices = groupData.AcceptNotices,
                        GroupID = groupData.GroupID,
                        GroupInsigniaID = groupData.GroupInsigniaID,
                        GroupName = Utils.BytesToString(groupData.GroupName),
                        GroupPowers = (GroupPowers) groupData.GroupPowers,
                        GroupTitle = Utils.BytesToString(groupData.GroupTitle),
                        ListInProfile = groups.NewGroupData.ListInProfile
                    };
                    avatarGroups.Add(avatarGroup);
                }

                OnAvatarGroupsReply(new AvatarGroupsReplyEventArgs(groups.AgentData.AvatarID, avatarGroups));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarPickerReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarPickerReply != null)
            {
                Packet packet = e.Packet;
                AvatarPickerReplyPacket reply = (AvatarPickerReplyPacket)packet;
                Dictionary<UUID, string> avatars = new Dictionary<UUID, string>();

                foreach (AvatarPickerReplyPacket.DataBlock block in reply.Data)
                {
                    avatars[block.AvatarID] = Utils.BytesToString(block.FirstName) +
                        " " + Utils.BytesToString(block.LastName);
                }
                OnAvatarPickerReply(new AvatarPickerReplyEventArgs(reply.AgentData.QueryID, avatars));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ViewerEffectHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            ViewerEffectPacket effect = (ViewerEffectPacket)packet;

            foreach (ViewerEffectPacket.EffectBlock block in effect.Effect)
            {
                EffectType type = (EffectType)block.Type;

                // Each ViewerEffect type uses it's own custom binary format for additional data. Fun eh?
                switch (type)
                {
                    case EffectType.Text:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.Icon:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.Connector:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.FlexibleObject:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.AnimalControls:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.AnimationObject:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.Cloth:
                        Logger.Log("Received a ViewerEffect of type " + type + ", implement me!",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.Glow:
                        Logger.Log("Received a Glow ViewerEffect which is not implemented yet",
                            Helpers.LogLevel.Warning, Client);
                        break;
                    case EffectType.Beam:
                    case EffectType.Point:
                    case EffectType.Trail:
                    case EffectType.Sphere:
                    case EffectType.Spiral:
                    case EffectType.Edit:
                        if (m_ViewerEffect != null)
                        {
                            if (block.TypeData.Length == 56)
                            {
                                UUID sourceAvatar = new UUID(block.TypeData, 0);
                                UUID targetObject = new UUID(block.TypeData, 16);
                                Vector3d targetPos = new Vector3d(block.TypeData, 32);
                                OnViewerEffect(new ViewerEffectEventArgs(type, sourceAvatar, targetObject, targetPos, block.Duration, block.ID));
                            }
                            else
                            {
                                Logger.Log("Received a " + type +
                                    " ViewerEffect with an incorrect TypeData size of " +
                                    block.TypeData.Length + " bytes", Helpers.LogLevel.Warning, Client);
                            }
                        }
                        break;
                    case EffectType.LookAt:
                        if (m_ViewerEffectLookAt != null)
                        {
                            if (block.TypeData.Length == 57)
                            {
                                UUID sourceAvatar = new UUID(block.TypeData, 0);
                                UUID targetObject = new UUID(block.TypeData, 16);
                                Vector3d targetPos = new Vector3d(block.TypeData, 32);
                                LookAtType lookAt = (LookAtType)block.TypeData[56];

                                OnViewerEffectLookAt(new ViewerEffectLookAtEventArgs(sourceAvatar, targetObject, targetPos, lookAt,
                                    block.Duration, block.ID));
                            }
                            else
                            {
                                Logger.Log("Received a LookAt ViewerEffect with an incorrect TypeData size of " +
                                    block.TypeData.Length + " bytes", Helpers.LogLevel.Warning, Client);
                            }
                        }
                        break;
                    case EffectType.PointAt:
                        if (m_ViewerEffectPointAt != null)
                        {
                            if (block.TypeData.Length == 57)
                            {
                                UUID sourceAvatar = new UUID(block.TypeData, 0);
                                UUID targetObject = new UUID(block.TypeData, 16);
                                Vector3d targetPos = new Vector3d(block.TypeData, 32);
                                PointAtType pointAt = (PointAtType)block.TypeData[56];

                                OnViewerEffectPointAt(new ViewerEffectPointAtEventArgs(e.Simulator, sourceAvatar, targetObject, targetPos,
                                    pointAt, block.Duration, block.ID));
                            }
                            else
                            {
                                Logger.Log("Received a PointAt ViewerEffect with an incorrect TypeData size of " +
                                    block.TypeData.Length + " bytes", Helpers.LogLevel.Warning, Client);
                            }
                        }
                        break;
                    default:
                        Logger.Log("Received a ViewerEffect with an unknown type " + type, Helpers.LogLevel.Warning, Client);
                        break;
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarPicksReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarPicksReply == null)
            {
                return;
            }
            Packet packet = e.Packet;

            AvatarPicksReplyPacket p = (AvatarPicksReplyPacket)packet;
            var picks = p.Data.ToDictionary(b => b.PickID, b => Utils.BytesToString(b.PickName));

            OnAvatarPicksReply(new AvatarPicksReplyEventArgs(p.AgentData.TargetID, picks));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void PickInfoReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_PickInfoReply != null)
            {
                Packet packet = e.Packet;
                PickInfoReplyPacket p = (PickInfoReplyPacket)packet;
                ProfilePick ret = new ProfilePick
                {
                    CreatorID = p.Data.CreatorID,
                    Desc = Utils.BytesToString(p.Data.Desc),
                    Enabled = p.Data.Enabled,
                    Name = Utils.BytesToString(p.Data.Name),
                    OriginalName = Utils.BytesToString(p.Data.OriginalName),
                    ParcelID = p.Data.ParcelID,
                    PickID = p.Data.PickID,
                    PosGlobal = p.Data.PosGlobal,
                    SimName = Utils.BytesToString(p.Data.SimName),
                    SnapshotID = p.Data.SnapshotID,
                    SortOrder = p.Data.SortOrder,
                    TopPick = p.Data.TopPick,
                    User = Utils.BytesToString(p.Data.User)
                };

                OnPickInfoReply(new PickInfoReplyEventArgs(ret.PickID, ret));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarClassifiedReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarClassifiedReply != null)
            {
                Packet packet = e.Packet;
                AvatarClassifiedReplyPacket p = (AvatarClassifiedReplyPacket)packet;
                var classifieds = p.Data.ToDictionary(b => b.ClassifiedID, b => Utils.BytesToString(b.Name));

                OnAvatarClassifiedReply(new AvatarClassifiedReplyEventArgs(p.AgentData.TargetID, classifieds));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ClassifiedInfoReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarClassifiedReply != null)
            {
                Packet packet = e.Packet;
                ClassifiedInfoReplyPacket p = (ClassifiedInfoReplyPacket)packet;
                ClassifiedAd ret = new ClassifiedAd
                {
                    Desc = Utils.BytesToString(p.Data.Desc),
                    Name = Utils.BytesToString(p.Data.Name),
                    ParcelID = p.Data.ParcelID,
                    ClassifiedID = p.Data.ClassifiedID,
                    Position = p.Data.PosGlobal,
                    SnapShotID = p.Data.SnapshotID,
                    Price = p.Data.PriceForListing,
                    ParentEstate = p.Data.ParentEstate,
                    ClassifiedFlags = p.Data.ClassifiedFlags,
                    Catagory = p.Data.Category
                };

                OnClassifiedInfoReply(new ClassifiedInfoReplyEventArgs(ret.ClassifiedID, ret));
            }
        }

        #endregion Packet Handlers
    }

    #region EventArgs

    /// <summary>Provides data for the <see cref="AvatarManager.AvatarAnimation"/> event</summary>
    /// <remarks>The <see cref="AvatarManager.AvatarAnimation"/> event occurs when the simulator sends
    /// the animation playlist for an agent</remarks>
    /// <example>
    /// The following code example uses the <see cref="AvatarAnimationEventArgs.AvatarID"/> and <see cref="AvatarAnimationEventArgs.Animations"/>
    /// properties to display the animation playlist of an avatar on the <see cref="Console"/> window.
    /// <code>
    ///     // subscribe to the event
    ///     Client.Avatars.AvatarAnimation += Avatars_AvatarAnimation;
    ///     
    ///     private void Avatars_AvatarAnimation(object sender, AvatarAnimationEventArgs e)
    ///     {
    ///         // create a dictionary of "known" animations from the Animations class using System.Reflection
    ///         Dictionary&lt;UUID, string&gt; systemAnimations = new Dictionary&lt;UUID, string&gt;();
    ///         Type type = typeof(Animations);
    ///         System.Reflection.FieldInfo[] fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    ///         foreach (System.Reflection.FieldInfo field in fields)
    ///         {
    ///             systemAnimations.Add((UUID)field.GetValue(type), field.Name);
    ///         }
    ///
    ///         // find out which animations being played are known animations and which are assets
    ///         foreach (Animation animation in e.Animations)
    ///         {
    ///             if (systemAnimations.ContainsKey(animation.AnimationID))
    ///             {
    ///                 Console.WriteLine("{0} is playing {1} ({2}) sequence {3}", e.AvatarID,
    ///                     systemAnimations[animation.AnimationID], animation.AnimationSequence);
    ///             }
    ///             else
    ///             {
    ///                 Console.WriteLine("{0} is playing {1} (Asset) sequence {2}", e.AvatarID,
    ///                     animation.AnimationID, animation.AnimationSequence);
    ///             }
    ///         }
    ///     }
    /// </code>
    /// </example>
    public class AvatarAnimationEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        /// <summary>Get the list of animations to start</summary>
        public List<Animation> Animations { get; }

        /// <summary>
        /// Construct a new instance of the AvatarAnimationEventArgs class
        /// </summary>
        /// <param name="avatarID">The ID of the agent</param>
        /// <param name="anims">The list of animations to start</param>
        public AvatarAnimationEventArgs(UUID avatarID, List<Animation> anims)
        {
            this.AvatarID = avatarID;
            this.Animations = anims;
        }
    }

    /// <summary>Provides data for the <see cref="AvatarManager.AvatarAppearance"/> event</summary>
    /// <remarks>The <see cref="AvatarManager.AvatarAppearance"/> event occurs when the simulator sends
    /// the appearance data for an avatar</remarks>
    /// <example>
    /// The following code example uses the <see cref="AvatarAppearanceEventArgs.AvatarID"/> and <see cref="AvatarAppearanceEventArgs.VisualParams"/>
    /// properties to display the selected shape of an avatar on the <see cref="Console"/> window.
    /// <code>
    ///     // subscribe to the event
    ///     Client.Avatars.AvatarAppearance += Avatars_AvatarAppearance;
    /// 
    ///     // handle the data when the event is raised
    ///     void Avatars_AvatarAppearance(object sender, AvatarAppearanceEventArgs e)
    ///     {
    ///         Console.WriteLine("The Agent {0} is using a {1} shape.", e.AvatarID, (e.VisualParams[31] &gt; 0) : "male" ? "female")
    ///     }
    /// </code>
    /// </example>
    public class AvatarAppearanceEventArgs : EventArgs
    {
        /// <summary>Get the Simulator this request is from of the agent</summary>
        public Simulator Simulator { get; }

        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        /// <summary>true if the agent is a trial account</summary>
        public bool IsTrial { get; }

        /// <summary>Get the default agent texture</summary>
        public Primitive.TextureEntryFace DefaultTexture { get; }

        /// <summary>Get the agents appearance layer textures</summary>
        public Primitive.TextureEntryFace[] FaceTextures { get; }

        /// <summary>Get the <see cref="VisualParams"/> for the agent</summary>
        public List<byte> VisualParams { get; }

        /// <summary>Version of the appearance system used.
        /// Value greater than 0 indicates that server side baking is used</summary>
        public byte AppearanceVersion { get; }

        /// <summary>Version of the Current Outfit Folder the appearance is based on</summary>
        public int COFVersion { get; }

        /// <summary>Appearance flags, introduced with server side baking, currently unused</summary>
        public AppearanceFlags AppearanceFlags { get; }

        /// <summary>
        /// Number of attachments
        /// </summary>
        public int ChildCount { get; }

        /// <summary>
        /// Construct a new instance of the AvatarAppearanceEventArgs class
        /// </summary>
        /// <param name="sim">The simulator request was from</param>
        /// <param name="avatarID">The ID of the agent</param>
        /// <param name="isTrial">true of the agent is a trial account</param>
        /// <param name="defaultTexture">The default agent texture</param>
        /// <param name="faceTextures">The agents appearance layer textures</param>
        /// <param name="visualParams">The <see cref="VisualParams"/> for the agent</param>
        /// <param name="appearanceVersion">Appearance Version</param>
        /// <param name="COFVersion">Current outfit folder version</param>
        /// <param name="appearanceFlags">Appearance Flags</param>
        /// <param name="childCount">Child count</param>
        public AvatarAppearanceEventArgs(Simulator sim, UUID avatarID, bool isTrial, Primitive.TextureEntryFace defaultTexture,
            Primitive.TextureEntryFace[] faceTextures, List<byte> visualParams,
            byte appearanceVersion, int COFVersion, AppearanceFlags appearanceFlags, int childCount)
        {
            this.Simulator = sim;
            this.AvatarID = avatarID;
            this.IsTrial = isTrial;
            this.DefaultTexture = defaultTexture;
            this.FaceTextures = faceTextures;
            this.VisualParams = visualParams;
            this.AppearanceVersion = appearanceVersion;
            this.COFVersion = COFVersion;
            this.AppearanceFlags = appearanceFlags;
            this.ChildCount = childCount;
        }
    }

    /// <summary>Represents the interests from the profile of an agent</summary>
    public class AvatarInterestsReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        /// <summary>Get the interests of the agent</summary>
        public Avatar.Interests Interests { get; }

        public AvatarInterestsReplyEventArgs(UUID avatarID, Avatar.Interests interests)
        {
            this.AvatarID = avatarID;
            this.Interests = interests;
        }
    }

    /// <summary>Represents the private notes from the profile of an agent</summary>
    public class AvatarNotesReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        /// <summary>Get the interests of the agent</summary>
        public string Notes { get; }

        public AvatarNotesReplyEventArgs(UUID avatarID, string notes)
        {
            this.AvatarID = avatarID;
            this.Notes = notes;
        }
    }

    /// <summary>The properties of an agent</summary>
    public class AvatarPropertiesReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        public Avatar.AvatarProperties Properties { get; }

        public AvatarPropertiesReplyEventArgs(UUID avatarID, Avatar.AvatarProperties properties)
        {
            this.AvatarID = avatarID;
            this.Properties = properties;
        }
    }


    public class AvatarGroupsReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        public List<AvatarGroup> Groups { get; }

        public AvatarGroupsReplyEventArgs(UUID avatarID, List<AvatarGroup> avatarGroups)
        {
            this.AvatarID = avatarID;
            this.Groups = avatarGroups;
        }
    }

    public class AvatarPicksReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent</summary>
        public UUID AvatarID { get; }

        public Dictionary<UUID, string> Picks { get; }

        public AvatarPicksReplyEventArgs(UUID avatarid, Dictionary<UUID, string> picks)
        {
            this.AvatarID = avatarid;
            this.Picks = picks;
        }
    }

    public class PickInfoReplyEventArgs : EventArgs
    {
        public UUID PickID { get; }
        public ProfilePick Pick { get; }


        public PickInfoReplyEventArgs(UUID pickid, ProfilePick pick)
        {
            this.PickID = pickid;
            this.Pick = pick;
        }
    }

    public class AvatarClassifiedReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the avatar</summary>
        public UUID AvatarID { get; }
        public Dictionary<UUID, string> Classifieds { get; }

        public AvatarClassifiedReplyEventArgs(UUID avatarid, Dictionary<UUID, string> classifieds)
        {
            this.AvatarID = avatarid;
            this.Classifieds = classifieds;
        }
    }

    public class ClassifiedInfoReplyEventArgs : EventArgs
    {
        public UUID ClassifiedID { get; }
        public ClassifiedAd Classified { get; }

        public ClassifiedInfoReplyEventArgs(UUID classifiedID, ClassifiedAd Classified)
        {
            this.ClassifiedID = classifiedID;
            this.Classified = Classified;
        }
    }

    public class UUIDNameReplyEventArgs : EventArgs
    {
        public Dictionary<UUID, string> Names { get; }

        public UUIDNameReplyEventArgs(Dictionary<UUID, string> names)
        {
            this.Names = names;
        }
    }

    public class AvatarPickerReplyEventArgs : EventArgs
    {
        public UUID QueryID { get; }
        public Dictionary<UUID, string> Avatars { get; }

        public AvatarPickerReplyEventArgs(UUID queryID, Dictionary<UUID, string> avatars)
        {
            this.QueryID = queryID;
            this.Avatars = avatars;
        }
    }

    public class ViewerEffectEventArgs : EventArgs
    {
        public EffectType Type { get; }
        public UUID SourceID { get; }
        public UUID TargetID { get; }
        public Vector3d TargetPosition { get; }
        public float Duration { get; }
        public UUID EffectID { get; }

        public ViewerEffectEventArgs(EffectType type, UUID sourceID, UUID targetID, Vector3d targetPos, float duration, UUID id)
        {
            this.Type = type;
            this.SourceID = sourceID;
            this.TargetID = targetID;
            this.TargetPosition = targetPos;
            this.Duration = duration;
            this.EffectID = id;
        }
    }

    public class ViewerEffectPointAtEventArgs : EventArgs
    {
        public Simulator Simulator { get; }
        public UUID SourceID { get; }
        public UUID TargetID { get; }
        public Vector3d TargetPosition { get; }
        public PointAtType PointType { get; }
        public float Duration { get; }
        public UUID EffectID { get; }

        public ViewerEffectPointAtEventArgs(Simulator simulator, UUID sourceID, UUID targetID, Vector3d targetPos, PointAtType pointType, float duration, UUID id)
        {
            this.Simulator = simulator;
            this.SourceID = sourceID;
            this.TargetID = targetID;
            this.TargetPosition = targetPos;
            this.PointType = pointType;
            this.Duration = duration;
            this.EffectID = id;
        }
    }

    public class ViewerEffectLookAtEventArgs : EventArgs
    {
        public UUID SourceID { get; }
        public UUID TargetID { get; }
        public Vector3d TargetPosition { get; }
        public LookAtType LookType { get; }
        public float Duration { get; }
        public UUID EffectID { get; }

        public ViewerEffectLookAtEventArgs(UUID sourceID, UUID targetID, Vector3d targetPos, LookAtType lookType, float duration, UUID id)
        {
            this.SourceID = sourceID;
            this.TargetID = targetID;
            this.TargetPosition = targetPos;
            this.LookType = lookType;
            this.Duration = duration;
            this.EffectID = id;
        }
    }

    /// <summary>
    /// Event args class for display name notification messages
    /// </summary>
    public class DisplayNameUpdateEventArgs : EventArgs
    {
        public string OldDisplayName { get; }
        public AgentDisplayName DisplayName { get; }

        public DisplayNameUpdateEventArgs(string oldDisplayName, AgentDisplayName displayName)
        {
            this.OldDisplayName = oldDisplayName;
            this.DisplayName = displayName;
        }
    }
    #endregion
}
