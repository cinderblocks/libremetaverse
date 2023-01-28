/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without 
 *   modification, are permitted provided that the following conditions are met:
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
using System.Threading;
using OpenMetaverse.Http;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    [Flags]
    public enum FriendRights : int
    {
        /// <summary>The avatar has no rights</summary>
        None = 0,
        /// <summary>The avatar can see the online status of the target avatar</summary>
        CanSeeOnline = 1,
        /// <summary>The avatar can see the location of the target avatar on the map</summary>
        CanSeeOnMap = 2,
        /// <summary>The avatar can modify the ojects of the target avatar </summary>
        CanModifyObjects = 4
    }

    /// <summary>
    /// This class holds information about an avatar in the friends list.  There are two ways 
    /// to interface to this class.  The first is through the set of boolean properties.  This is the typical
    /// way clients of this class will use it.  The second interface is through two bitflag properties,
    /// TheirFriendsRights and MyFriendsRights
    /// </summary>
    public class FriendInfo
    {
        private bool m_canSeeMeOnline;
        private bool m_canSeeMeOnMap;

        #region Properties

        /// <summary>
        /// System ID of the avatar
        /// </summary>
        public UUID UUID { get; }

        /// <summary>
        /// full name of the avatar
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// True if the avatar is online
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// True if the friend can see if I am online
        /// </summary>
        public bool CanSeeMeOnline
        {
            get { return m_canSeeMeOnline; }
            set
            {
                m_canSeeMeOnline = value;

                // if I can't see them online, then I can't see them on the map
                if (!m_canSeeMeOnline)
                    m_canSeeMeOnMap = false;
            }
        }

        /// <summary>
        /// True if the friend can see me on the map 
        /// </summary>
        public bool CanSeeMeOnMap
        {
            get { return m_canSeeMeOnMap; }
            set
            {
                // if I can't see them online, then I can't see them on the map
                if (m_canSeeMeOnline)
                    m_canSeeMeOnMap = value;
            }
        }

        /// <summary>
        /// True if the freind can modify my objects
        /// </summary>
        public bool CanModifyMyObjects { get; set; }

        /// <summary>
        /// True if I can see if my friend is online
        /// </summary>
        public bool CanSeeThemOnline { get; private set; }

        /// <summary>
        /// True if I can see if my friend is on the map
        /// </summary>
        public bool CanSeeThemOnMap { get; private set; }

        /// <summary>
        /// True if I can modify my friend's objects
        /// </summary>
        public bool CanModifyTheirObjects { get; private set; }

        /// <summary>
        /// My friend's rights represented as bitmapped flags
        /// </summary>
        public FriendRights TheirFriendRights
        {
            get
            {
                FriendRights results = FriendRights.None;
                if (m_canSeeMeOnline)
                    results |= FriendRights.CanSeeOnline;
                if (m_canSeeMeOnMap)
                    results |= FriendRights.CanSeeOnMap;
                if (CanModifyMyObjects)
                    results |= FriendRights.CanModifyObjects;

                return results;
            }
            set
            {
                m_canSeeMeOnline = (value & FriendRights.CanSeeOnline) != 0;
                m_canSeeMeOnMap = (value & FriendRights.CanSeeOnMap) != 0;
                CanModifyMyObjects = (value & FriendRights.CanModifyObjects) != 0;
            }
        }

        /// <summary>
        /// My rights represented as bitmapped flags
        /// </summary>
        public FriendRights MyFriendRights
        {
            get
            {
                FriendRights results = FriendRights.None;
                if (CanSeeThemOnline)
                    results |= FriendRights.CanSeeOnline;
                if (CanSeeThemOnMap)
                    results |= FriendRights.CanSeeOnMap;
                if (CanModifyTheirObjects)
                    results |= FriendRights.CanModifyObjects;

                return results;
            }
            set
            {
                CanSeeThemOnline = (value & FriendRights.CanSeeOnline) != 0;
                CanSeeThemOnMap = (value & FriendRights.CanSeeOnMap) != 0;
                CanModifyTheirObjects = (value & FriendRights.CanModifyObjects) != 0;
            }
        }

        #endregion Properties

        /// <summary>
        /// Used internally when building the initial list of friends at login time
        /// </summary>
        /// <param name="id">System ID of the avatar being prepesented</param>
        /// <param name="theirRights">Rights the friend has to see you online and to modify your objects</param>
        /// <param name="myRights">Rights you have to see your friend online and to modify their objects</param>
        internal FriendInfo(UUID id, FriendRights theirRights, FriendRights myRights)
        {
            UUID = id;
            m_canSeeMeOnline = (theirRights & FriendRights.CanSeeOnline) != 0;
            m_canSeeMeOnMap = (theirRights & FriendRights.CanSeeOnMap) != 0;
            CanModifyMyObjects = (theirRights & FriendRights.CanModifyObjects) != 0;

            CanSeeThemOnline = (myRights & FriendRights.CanSeeOnline) != 0;
            CanSeeThemOnMap = (myRights & FriendRights.CanSeeOnMap) != 0;
            CanModifyTheirObjects = (myRights & FriendRights.CanModifyObjects) != 0;
        }

        /// <summary>
        /// FriendInfo represented as a string
        /// </summary>
        /// <returns>A string reprentation of both my rights and my friends rights</returns>
        public override string ToString()
        {
            return !string.IsNullOrEmpty(Name) 
                ? $"{Name} (Their Rights: {TheirFriendRights}, My Rights: {MyFriendRights})" 
                : $"{UUID} (Their Rights: {TheirFriendRights}, My Rights: {MyFriendRights})";
        }
    }

    /// <summary>
    /// This class is used to add and remove avatars from your friends list and to manage their permission.  
    /// </summary>
    public class FriendsManager
    {
        #region Delegates

        private EventHandler<FriendsReadyEventArgs> m_FriendsListReadyResponse;

        protected virtual void OnfriendsListReady(FriendsReadyEventArgs e)
        {
            EventHandler<FriendsReadyEventArgs> handler = m_FriendsListReadyResponse;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendsListReadyLock = new object();

        public event EventHandler<FriendsReadyEventArgs> friendsListReady
        {
            add { lock (m_FriendsListReadyLock) { m_FriendsListReadyResponse += value; } }
            remove { lock (m_FriendsListReadyLock) { m_FriendsListReadyResponse -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendInfoEventArgs> m_FriendOnline;

        /// <summary>Raises the FriendOnline event</summary>
        /// <param name="e">A FriendInfoEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendOnline(FriendInfoEventArgs e)
        {
            EventHandler<FriendInfoEventArgs> handler = m_FriendOnline;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendOnlineLock = new object();

        /// <summary>Raised when the simulator sends notification one of the members in our friends list comes online</summary>
        public event EventHandler<FriendInfoEventArgs> FriendOnline
        {
            add { lock (m_FriendOnlineLock) { m_FriendOnline += value; } }
            remove { lock (m_FriendOnlineLock) { m_FriendOnline -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendInfoEventArgs> m_FriendOffline;

        /// <summary>Raises the FriendOffline event</summary>
        /// <param name="e">A FriendInfoEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendOffline(FriendInfoEventArgs e)
        {
            EventHandler<FriendInfoEventArgs> handler = m_FriendOffline;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendOfflineLock = new object();

        /// <summary>Raised when the simulator sends notification one of the members in our friends list goes offline</summary>
        public event EventHandler<FriendInfoEventArgs> FriendOffline
        {
            add { lock (m_FriendOfflineLock) { m_FriendOffline += value; } }
            remove { lock (m_FriendOfflineLock) { m_FriendOffline -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendInfoEventArgs> m_FriendRights;

        /// <summary>Raises the FriendRightsUpdate event</summary>
        /// <param name="e">A FriendInfoEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendRights(FriendInfoEventArgs e)
        {
            EventHandler<FriendInfoEventArgs> handler = m_FriendRights;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendRightsLock = new object();

        /// <summary>Raised when the simulator sends notification one of the members in our friends list grants or revokes permissions</summary>
        public event EventHandler<FriendInfoEventArgs> FriendRightsUpdate
        {
            add { lock (m_FriendRightsLock) { m_FriendRights += value; } }
            remove { lock (m_FriendRightsLock) { m_FriendRights -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendNamesEventArgs> m_FriendNames;

        /// <summary>Raises the FriendNames event</summary>
        /// <param name="e">A FriendNamesEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendNames(FriendNamesEventArgs e)
        {
            EventHandler<FriendNamesEventArgs> handler = m_FriendNames;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendNamesLock = new object();

        /// <summary>Raised when the simulator sends us the names on our friends list</summary>
        public event EventHandler<FriendNamesEventArgs> FriendNames
        {
            add { lock (m_FriendNamesLock) { m_FriendNames += value; } }
            remove { lock (m_FriendNamesLock) { m_FriendNames -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendshipOfferedEventArgs> m_FriendshipOffered;

        /// <summary>Raises the FriendshipOffered event</summary>
        /// <param name="e">A FriendshipOfferedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendshipOffered(FriendshipOfferedEventArgs e)
        {
            EventHandler<FriendshipOfferedEventArgs> handler = m_FriendshipOffered;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendshipOfferedLock = new object();

        /// <summary>Raised when the simulator sends notification another agent is offering us friendship</summary>
        public event EventHandler<FriendshipOfferedEventArgs> FriendshipOffered
        {
            add { lock (m_FriendshipOfferedLock) { m_FriendshipOffered += value; } }
            remove { lock (m_FriendshipOfferedLock) { m_FriendshipOffered -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendshipResponseEventArgs> m_FriendshipResponse;

        /// <summary>Raises the FriendshipResponse event</summary>
        /// <param name="e">A FriendshipResponseEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendshipResponse(FriendshipResponseEventArgs e)
        {
            EventHandler<FriendshipResponseEventArgs> handler = m_FriendshipResponse;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendshipResponseLock = new object();

        /// <summary>Raised when a request we sent to friend another agent is accepted or declined</summary>
        public event EventHandler<FriendshipResponseEventArgs> FriendshipResponse
        {
            add { lock (m_FriendshipResponseLock) { m_FriendshipResponse += value; } }
            remove { lock (m_FriendshipResponseLock) { m_FriendshipResponse -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendshipTerminatedEventArgs> m_FriendshipTerminated;

        /// <summary>Raises the FriendshipTerminated event</summary>
        /// <param name="e">A FriendshipTerminatedEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendshipTerminated(FriendshipTerminatedEventArgs e)
        {
            EventHandler<FriendshipTerminatedEventArgs> handler = m_FriendshipTerminated;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendshipTerminatedLock = new object();

        /// <summary>Raised when the simulator sends notification one of the members in our friends list has terminated 
        /// our friendship</summary>
        public event EventHandler<FriendshipTerminatedEventArgs> FriendshipTerminated
        {
            add { lock (m_FriendshipTerminatedLock) { m_FriendshipTerminated += value; } }
            remove { lock (m_FriendshipTerminatedLock) { m_FriendshipTerminated -= value; } }
        }

        /// <summary>The event subscribers. null if no subcribers</summary>
        private EventHandler<FriendFoundReplyEventArgs> m_FriendFound;

        /// <summary>Raises the FriendFoundReply event</summary>
        /// <param name="e">A FriendFoundReplyEventArgs object containing the
        /// data returned from the data server</param>
        protected virtual void OnFriendFoundReply(FriendFoundReplyEventArgs e)
        {
            EventHandler<FriendFoundReplyEventArgs> handler = m_FriendFound;
            handler?.Invoke(this, e);
        }

        /// <summary>Thread sync lock object</summary>
        private readonly object m_FriendFoundLock = new object();

        /// <summary>Raised when the simulator sends the location of a friend we have 
        /// requested map location info for</summary>
        public event EventHandler<FriendFoundReplyEventArgs> FriendFoundReply
        {
            add { lock (m_FriendFoundLock) { m_FriendFound += value; } }
            remove { lock (m_FriendFoundLock) { m_FriendFound -= value; } }
        }

        #endregion Delegates

        #region Events

        #endregion Events

        private readonly GridClient Client;
        /// <summary>
        /// A dictionary of key/value pairs containing known friends of this avatar. 
        /// 
        /// The Key is the <seealso cref="UUID"/> of the friend, the value is a <seealso cref="FriendInfo"/>
        /// object that contains detailed information including permissions you have and have given to the friend
        /// </summary>
        public InternalDictionary<UUID, FriendInfo> FriendList = new InternalDictionary<UUID, FriendInfo>();

        /// <summary>
        /// A Dictionary of key/value pairs containing current pending frienship offers.
        /// 
        /// The key is the <seealso cref="UUID"/> of the avatar making the request, 
        /// the value is the <seealso cref="UUID"/> of the request which is used to accept
        /// or decline the friendship offer
        /// </summary>
        public InternalDictionary<UUID, UUID> FriendRequests = new InternalDictionary<UUID, UUID>();

        /// <summary>
        /// Internal constructor
        /// </summary>
        /// <param name="client">A reference to the GridClient Object</param>
        internal FriendsManager(GridClient client)
        {
            Client = client;

            Client.Network.LoginProgress += Network_OnConnect;
            Client.Avatars.UUIDNameReply += new EventHandler<UUIDNameReplyEventArgs>(Avatars_OnAvatarNames);
            Client.Self.IM += Self_IM;

            Client.Network.RegisterCallback(PacketType.OnlineNotification, OnlineNotificationHandler);
            Client.Network.RegisterCallback(PacketType.OfflineNotification, OfflineNotificationHandler);
            Client.Network.RegisterCallback(PacketType.ChangeUserRights, ChangeUserRightsHandler);
            Client.Network.RegisterCallback(PacketType.TerminateFriendship, TerminateFriendshipHandler);
            Client.Network.RegisterCallback(PacketType.FindAgent, OnFindAgentReplyHandler);

            Client.Network.RegisterLoginResponseCallback(new NetworkManager.LoginResponseCallback(Network_OnLoginResponse),
                new string[] { "buddy-list" });
        }

        #region Public Methods

        /// <summary>
        /// Accept a friendship request
        /// </summary>
        /// <param name="fromAgentID">agentID of avatar to form friendship with</param>
        /// <param name="imSessionID">imSessionID of the friendship request message</param>
        public void AcceptFriendship(UUID fromAgentID, UUID imSessionID)
        {
            UUID callingCardFolder = Client.Inventory.FindFolderForType(AssetType.CallingCard);

            AcceptFriendshipPacket request = new AcceptFriendshipPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                TransactionBlock =
                {
                    TransactionID = imSessionID
                },
                FolderData = new AcceptFriendshipPacket.FolderDataBlock[1]
            };
            request.FolderData[0] = new AcceptFriendshipPacket.FolderDataBlock
            {
                FolderID = callingCardFolder
            };

            Client.Network.SendPacket(request);

            FriendInfo friend = new FriendInfo(fromAgentID, FriendRights.CanSeeOnline,
                FriendRights.CanSeeOnline);

            if (!FriendList.ContainsKey(fromAgentID))
                FriendList.Add(friend.UUID, friend);

            if (FriendRequests.ContainsKey(fromAgentID))
                FriendRequests.Remove(fromAgentID);

            Client.Avatars.RequestAvatarName(fromAgentID);
        }

        /// <summary>
        /// Accept friendship request. Only to be used if request was sent via Offline Msg cap
        /// This can be determined by the presence of a <seealso cref="InstantMessageEventArgs.Simulator"/>
        /// value in <seealso cref="InstantMessageEventArgs" />
        /// </summary>
        /// <param name="fromAgentID">agentID of avatar to form friendship with</param>
        public void AcceptFriendshipCapability(UUID fromAgentID)
        {
            Uri acceptFriendshipCap = Client.Network.CurrentSim.Caps.CapabilityURI("AcceptFriendship");
            if (acceptFriendshipCap == null)
            {
                Logger.Log("AcceptFriendship capability not found.", Helpers.LogLevel.Warning);
                return;
            }
            UriBuilder builder = new UriBuilder(acceptFriendshipCap)
            {
                // Second Life has some infintely stupid escaped agent name as part of the uri query.
                // Hopefully we don't need it because it makes no goddamn sense at all. Period, but just in case:
                // ?from={fromAgentID}&agent_name=\"This%20Sucks\"
                Query = $"from={fromAgentID}"
            };
            acceptFriendshipCap = builder.Uri;

            _ = Client.HttpCapsClient.PostRequestAsync(acceptFriendshipCap, OSDFormat.Xml, new OSD(), CancellationToken.None,
                (response, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log($"AcceptFriendship failed for {fromAgentID}. ({error.Message})",
                            Helpers.LogLevel.Warning);
                        return;
                    }
                    OSD result = OSDParser.Deserialize(data);
                    if (result is OSDMap resMap && resMap.ContainsKey("success") && resMap["success"].AsBoolean())
                    {
                        FriendInfo friend = new FriendInfo(fromAgentID, FriendRights.CanSeeOnline, FriendRights.CanSeeOnline);

                        if (!FriendList.ContainsKey(fromAgentID))
                        {
                            FriendList.Add(friend.UUID, friend);
                        }
                        if (FriendRequests.ContainsKey(fromAgentID))
                        {
                            FriendRequests.Remove(fromAgentID);
                        }
                        Client.Avatars.RequestAvatarName(fromAgentID);
                    }
                });
        }

        /// <summary>
        /// Decline a friendship request
        /// </summary>
        /// <param name="fromAgentID"><seealso cref="UUID"/> of friend</param>
        /// <param name="imSessionID">imSessionID of the friendship request message</param>
        public void DeclineFriendship(UUID fromAgentID, UUID imSessionID)
        {
            DeclineFriendshipPacket request = new DeclineFriendshipPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                TransactionBlock =
                {
                    TransactionID = imSessionID
                }
            };
            Client.Network.SendPacket(request);

            if (FriendRequests.ContainsKey(fromAgentID))
            {
                FriendRequests.Remove(fromAgentID);
            }
        }

        /// <summary>
        /// Decline friendship request. Only to be used if request was sent via Offline Msg cap
        /// This can be determined by the presence of a <seealso cref="InstantMessageEventArgs.Simulator"/>
        /// value in <seealso cref="InstantMessageEventArgs" />
        /// </summary>
        /// <param name="fromAgentID"><seealso cref="UUID"/> of friend</param>
        public void DeclineFriendshipCap(UUID fromAgentID)
        {
            Uri declineFriendshipCap = Client.Network.CurrentSim.Caps.CapabilityURI("DeclineFriendship");
            if (declineFriendshipCap == null)
            {
                Logger.Log("DeclineFriendship capability not found.", Helpers.LogLevel.Warning);
                return;
            }
            UriBuilder builder = new UriBuilder(declineFriendshipCap)
            {
                Query = $"from={fromAgentID}"
            };
            declineFriendshipCap = builder.Uri;

            _ = Client.HttpCapsClient.DeleteRequestAsync(declineFriendshipCap, OSDFormat.Xml, new OSD(), CancellationToken.None,
                (response, data, error) =>
                {
                    if (error != null)
                    {
                        Logger.Log($"DeclineFriendship failed for {fromAgentID}. ({error.Message})",
                            Helpers.LogLevel.Warning);
                        return;
                    }

                    OSD result = OSDParser.Deserialize(data);
                    if (result is OSDMap resMap && resMap.ContainsKey("success") && resMap["success"].AsBoolean())
                    {
                        if (FriendRequests.ContainsKey(fromAgentID))
                        {
                            FriendRequests.Remove(fromAgentID);
                        }
                    }
                });
        }

        /// <summary>
        /// Overload: Offer friendship to an avatar.
        /// </summary>
        /// <param name="agentID">System ID of the avatar you are offering friendship to</param>
        public void OfferFriendship(UUID agentID)
        {
            OfferFriendship(agentID, "Do ya wanna be my buddy?");
        }

        /// <summary>
        /// Offer friendship to an avatar.
        /// </summary>
        /// <param name="agentID">System ID of the avatar you are offering friendship to</param>
        /// <param name="message">A message to send with the request</param>
        public void OfferFriendship(UUID agentID, string message)
        {
            Client.Self.InstantMessage(Client.Self.Name,
                agentID,
                message,
                UUID.Random(),
                InstantMessageDialog.FriendshipOffered,
                InstantMessageOnline.Offline,
                Client.Self.SimPosition,
                Client.Network.CurrentSim.ID,
                null);
        }


        /// <summary>
        /// Terminate a friendship with an avatar
        /// </summary>
        /// <param name="agentID">System ID of the avatar you are terminating the friendship with</param>
        public void TerminateFriendship(UUID agentID)
        {
            if (FriendList.ContainsKey(agentID))
            {
                TerminateFriendshipPacket request = new TerminateFriendshipPacket
                {
                    AgentData =
                    {
                        AgentID = Client.Self.AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    ExBlock =
                    {
                        OtherID = agentID
                    }
                };

                Client.Network.SendPacket(request);

                if (FriendList.ContainsKey(agentID))
                {
                    FriendList.Remove(agentID);
                }
            }
        }
        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        private void TerminateFriendshipHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            TerminateFriendshipPacket itsOver = (TerminateFriendshipPacket)packet;
            string name = string.Empty;

            if (FriendList.ContainsKey(itsOver.ExBlock.OtherID))
            {
                name = FriendList[itsOver.ExBlock.OtherID].Name;
                FriendList.Remove(itsOver.ExBlock.OtherID);
            }

            if (m_FriendshipTerminated != null)
            {
                OnFriendshipTerminated(new FriendshipTerminatedEventArgs(itsOver.ExBlock.OtherID, name));
            }
        }

        /// <summary>
        /// Change the rights of a friend avatar.
        /// </summary>
        /// <param name="friendID">the <seealso cref="UUID"/> of the friend</param>
        /// <param name="rights">the new rights to give the friend</param>
        /// <remarks>This method will implicitly set the rights to those passed in the rights parameter.</remarks>
        public void GrantRights(UUID friendID, FriendRights rights)
        {
            GrantUserRightsPacket request = new GrantUserRightsPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Rights = new GrantUserRightsPacket.RightsBlock[1]
            };
            request.Rights[0] = new GrantUserRightsPacket.RightsBlock
            {
                AgentRelated = friendID,
                RelatedRights = (int)rights
            };

            Client.Network.SendPacket(request);
        }

        /// <summary>
        /// Use to map a friends location on the grid.
        /// </summary>
        /// <param name="friendID">Friends UUID to find</param>
        /// <remarks><seealso cref="E:OnFriendFound"/></remarks>
        public void MapFriend(UUID friendID)
        {
            FindAgentPacket stalk = new FindAgentPacket
            {
                AgentBlock =
                {
                    Hunter = Client.Self.AgentID,
                    Prey = friendID,
                    SpaceIP = 0 // Will be filled in by the simulator
                },
                LocationBlock = new FindAgentPacket.LocationBlockBlock[1]
            };
            stalk.LocationBlock[0] = new FindAgentPacket.LocationBlockBlock
            {
                GlobalX = 0.0, // Filled in by the simulator
                GlobalY = 0.0
            };

            Client.Network.SendPacket(stalk);
        }

        /// <summary>
        /// Use to track a friends movement on the grid
        /// </summary>
        /// <param name="friendID">Friends Key</param>
        public void TrackFriend(UUID friendID)
        {
            TrackAgentPacket stalk = new TrackAgentPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                TargetData =
                {
                    PreyID = friendID
                }
            };

            Client.Network.SendPacket(stalk);
        }

        /// <summary>
        /// Ask for a notification of friend's online status
        /// </summary>
        /// <param name="friendID">Friend's UUID</param>
        public void RequestOnlineNotification(UUID friendID)
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
                    Method = Utils.StringToBytes("requestonlinenotification"),
                    Invoice = UUID.Zero
                },
                ParamList = new GenericMessagePacket.ParamListBlock[1]
            };

            gmp.ParamList[0] = new GenericMessagePacket.ParamListBlock
            {
                Parameter = Utils.StringToBytes(friendID.ToString())
            };

            Client.Network.SendPacket(gmp);
        }

        #endregion

        #region Internal events

        private void Network_OnConnect(object sender, LoginProgressEventArgs e)
        {
            if (e.Status != LoginStatus.Success)
            {
                return;
            }

            List<UUID> names = new List<UUID>();

            if (FriendList.Count > 0)
            {
                FriendList.ForEach(
                    kvp =>
                    {
                        if (string.IsNullOrEmpty(kvp.Value.Name))
                            names.Add(kvp.Key);
                    }
                );

                Client.Avatars.RequestAvatarNames(names);
            }
        }


        /// <summary>
        /// This handles the asynchronous response of a RequestAvatarNames call.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">names corresponding to the list of IDs sent to RequestAvatarNames.</param>
        private void Avatars_OnAvatarNames(object sender, UUIDNameReplyEventArgs e)
        {
            Dictionary<UUID, string> newNames = new Dictionary<UUID, string>();

            foreach (KeyValuePair<UUID, string> kvp in e.Names)
            {
                FriendInfo friend;
                lock (FriendList.Dictionary)
                {
                    if (FriendList.TryGetValue(kvp.Key, out friend))
                    {
                        if (friend.Name == null)
                            newNames.Add(kvp.Key, e.Names[kvp.Key]);

                        friend.Name = e.Names[kvp.Key];
                        FriendList[kvp.Key] = friend;
                    }
                }
            }

            if (newNames.Count > 0 && m_FriendNames != null)
            {
                OnFriendNames(new FriendNamesEventArgs(newNames));
            }
        }
        #endregion

        #region Packet Handlers

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void OnlineNotificationHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            if (packet.Type == PacketType.OnlineNotification)
            {
                OnlineNotificationPacket notification = ((OnlineNotificationPacket)packet);

                foreach (OnlineNotificationPacket.AgentBlockBlock block in notification.AgentBlock)
                {
                    FriendInfo friend;
                    lock (FriendList.Dictionary)
                    {
                        if (!FriendList.ContainsKey(block.AgentID))
                        {
                            friend = new FriendInfo(block.AgentID, FriendRights.CanSeeOnline,
                                FriendRights.CanSeeOnline);
                            FriendList.Add(block.AgentID, friend);
                        }
                        else
                        {
                            friend = FriendList[block.AgentID];
                        }
                    }

                    bool doNotify = !friend.IsOnline;
                    friend.IsOnline = true;

                    if (m_FriendOnline != null && doNotify)
                    {
                        OnFriendOnline(new FriendInfoEventArgs(friend));
                    }
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void OfflineNotificationHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            if (packet.Type == PacketType.OfflineNotification)
            {
                OfflineNotificationPacket notification = (OfflineNotificationPacket)packet;

                foreach (OfflineNotificationPacket.AgentBlockBlock block in notification.AgentBlock)
                {
                    FriendInfo friend = new FriendInfo(block.AgentID, FriendRights.CanSeeOnline, FriendRights.CanSeeOnline);

                    lock (FriendList.Dictionary)
                    {
                        if (!FriendList.Dictionary.ContainsKey(block.AgentID))
                            FriendList.Dictionary[block.AgentID] = friend;

                        friend = FriendList.Dictionary[block.AgentID];
                    }

                    friend.IsOnline = false;

                    if (m_FriendOffline != null)
                    {
                        OnFriendOffline(new FriendInfoEventArgs(friend));
                    }
                }
            }
        }


        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        private void ChangeUserRightsHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            if (packet.Type == PacketType.ChangeUserRights)
            {
                FriendInfo friend;
                ChangeUserRightsPacket rights = (ChangeUserRightsPacket)packet;

                foreach (ChangeUserRightsPacket.RightsBlock block in rights.Rights)
                {
                    FriendRights newRights = (FriendRights)block.RelatedRights;
                    if (FriendList.TryGetValue(block.AgentRelated, out friend))
                    {
                        friend.TheirFriendRights = newRights;
                        if (m_FriendRights != null)
                        {
                            OnFriendRights(new FriendInfoEventArgs(friend));
                        }
                    }
                    else if (block.AgentRelated == Client.Self.AgentID)
                    {
                        if (FriendList.TryGetValue(rights.AgentData.AgentID, out friend))
                        {
                            friend.MyFriendRights = newRights;
                            if (m_FriendRights != null)
                            {
                                OnFriendRights(new FriendInfoEventArgs(friend));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        public void OnFindAgentReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_FriendFound != null)
            {
                Packet packet = e.Packet;
                FindAgentPacket reply = (FindAgentPacket)packet;

                float x, y;
                UUID prey = reply.AgentBlock.Prey;
                ulong regionHandle = Helpers.GlobalPosToRegionHandle((float)reply.LocationBlock[0].GlobalX,
                    (float)reply.LocationBlock[0].GlobalY, out x, out y);
                Vector3 xyz = new Vector3(x, y, 0f);

                OnFriendFoundReply(new FriendFoundReplyEventArgs(prey, regionHandle, xyz));
            }
        }

        #endregion

        private void Self_IM(object sender, InstantMessageEventArgs e)
        {
            if (e.IM.Dialog == InstantMessageDialog.FriendshipOffered)
            {
                if (m_FriendshipOffered != null)
                {
                    if (FriendRequests.ContainsKey(e.IM.FromAgentID))
                        FriendRequests[e.IM.FromAgentID] = e.IM.IMSessionID;
                    else
                        FriendRequests.Add(e.IM.FromAgentID, e.IM.IMSessionID);

                    OnFriendshipOffered(new FriendshipOfferedEventArgs(e.IM.FromAgentID, e.IM.FromAgentName, e.IM.IMSessionID));
                }
            }
            else if (e.IM.Dialog == InstantMessageDialog.FriendshipAccepted)
            {
                FriendInfo friend = new FriendInfo(e.IM.FromAgentID, FriendRights.CanSeeOnline,
                    FriendRights.CanSeeOnline)
                {
                    Name = e.IM.FromAgentName
                };
                lock (FriendList.Dictionary) FriendList[friend.UUID] = friend;

                if (m_FriendshipResponse != null)
                {
                    OnFriendshipResponse(new FriendshipResponseEventArgs(e.IM.FromAgentID, e.IM.FromAgentName, true));
                }
                RequestOnlineNotification(e.IM.FromAgentID);
            }
            else if (e.IM.Dialog == InstantMessageDialog.FriendshipDeclined)
            {
                if (m_FriendshipResponse != null)
                {
                    OnFriendshipResponse(new FriendshipResponseEventArgs(e.IM.FromAgentID, e.IM.FromAgentName, false));
                }
            }
        }

        /// <summary>
        /// Populate FriendList <seealso cref="InternalDictionary"/> with data from the login reply
        /// </summary>
        /// <param name="loginSuccess">true if login was successful</param>
        /// <param name="redirect">true if login request is requiring a redirect</param>
        /// <param name="message">A string containing the response to the login request</param>
        /// <param name="reason">A string containing the reason for the request</param>
        /// <param name="replyData">A <seealso cref="LoginResponseData"/> object containing the decoded 
        /// reply from the login server</param>
        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
            LoginResponseData replyData)
        {
            int uuidLength = UUID.Zero.ToString().Length;

            if (loginSuccess && replyData.BuddyList != null)
            {
                foreach (BuddyListEntry buddy in replyData.BuddyList)
                {
                    UUID bubid;
                    string id = buddy.BuddyId.Length > uuidLength ? buddy.BuddyId.Substring(0, uuidLength) : buddy.BuddyId;
                    if (UUID.TryParse(id, out bubid))
                    {
                        lock (FriendList.Dictionary)
                        {
                            if (!FriendList.ContainsKey(bubid))
                            {
                                FriendList[bubid] = new FriendInfo(bubid, 
                                    (FriendRights)buddy.BuddyRightsGiven,
                                    (FriendRights)buddy.BuddyRightsHas);
                            }
                        }
                    }
                }
                OnfriendsListReady(new FriendsReadyEventArgs(FriendList.Count));
            }
        }
    }
    #region EventArgs

   
    public class FriendsReadyEventArgs : EventArgs
    {
        /// <summary>Number of friends we have</summary>
        public int Count { get; }

        /// <summary>Get the name of the agent we requested a friendship with</summary>

        /// <summary>
        /// Construct a new instance of the FriendsReadyEventArgs class
        /// </summary>
        /// <param name="count">The total number of people loaded into the friend list.</param>
        public FriendsReadyEventArgs(int count)
        {
            this.Count = count;
        }
    }

    /// <summary>Contains information on a member of our friends list</summary>
    public class FriendInfoEventArgs : EventArgs
    {
        /// <summary>Get the FriendInfo</summary>
        public FriendInfo Friend { get; }

        /// <summary>
        /// Construct a new instance of the FriendInfoEventArgs class
        /// </summary>
        /// <param name="friend">The FriendInfo</param>
        public FriendInfoEventArgs(FriendInfo friend)
        {
            this.Friend = friend;
        }
    }

    /// <summary>Contains Friend Names</summary>
    public class FriendNamesEventArgs : EventArgs
    {
        /// <summary>A dictionary where the Key is the ID of the Agent, 
        /// and the Value is a string containing their name</summary>
        public Dictionary<UUID, string> Names { get; }

        /// <summary>
        /// Construct a new instance of the FriendNamesEventArgs class
        /// </summary>
        /// <param name="names">A dictionary where the Key is the ID of the Agent, 
        /// and the Value is a string containing their name</param>
        public FriendNamesEventArgs(Dictionary<UUID, string> names)
        {
            this.Names = names;
        }
    }

    /// <summary>Sent when another agent requests a friendship with our agent</summary>
    public class FriendshipOfferedEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent requesting friendship</summary>
        public UUID AgentID { get; }

        /// <summary>Get the name of the agent requesting friendship</summary>
        public string AgentName { get; }

        /// <summary>Get the ID of the session, used in accepting or declining the 
        /// friendship offer</summary>
        public UUID SessionID { get; }

        /// <summary>
        /// Construct a new instance of the FriendshipOfferedEventArgs class
        /// </summary>
        /// <param name="agentID">The ID of the agent requesting friendship</param>
        /// <param name="agentName">The name of the agent requesting friendship</param>
        /// <param name="imSessionID">The ID of the session, used in accepting or declining the 
        /// friendship offer</param>
        public FriendshipOfferedEventArgs(UUID agentID, string agentName, UUID imSessionID)
        {
            this.AgentID = agentID;
            this.AgentName = agentName;
            this.SessionID = imSessionID;
        }
    }

    /// <summary>A response containing the results of our request to form a friendship with another agent</summary>
    public class FriendshipResponseEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent we requested a friendship with</summary>
        public UUID AgentID { get; }

        /// <summary>Get the name of the agent we requested a friendship with</summary>
        public string AgentName { get; }

        /// <summary>true if the agent accepted our friendship offer</summary>
        public bool Accepted { get; }

        /// <summary>
        /// Construct a new instance of the FriendShipResponseEventArgs class
        /// </summary>
        /// <param name="agentID">The ID of the agent we requested a friendship with</param>
        /// <param name="agentName">The name of the agent we requested a friendship with</param>
        /// <param name="accepted">true if the agent accepted our friendship offer</param>
        public FriendshipResponseEventArgs(UUID agentID, string agentName, bool accepted)
        {
            this.AgentID = agentID;
            this.AgentName = agentName;
            this.Accepted = accepted;
        }
    }

    /// <summary>Contains data sent when a friend terminates a friendship with us</summary>
    public class FriendshipTerminatedEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent that terminated the friendship with us</summary>
        public UUID AgentID { get; }

        /// <summary>Get the name of the agent that terminated the friendship with us</summary>
        public string AgentName { get; }

        /// <summary>
        /// Construct a new instance of the FriendshipTerminatedEventArgs class
        /// </summary>
        /// <param name="agentID">The ID of the friend who terminated the friendship with us</param>
        /// <param name="agentName">The name of the friend who terminated the friendship with us</param>
        public FriendshipTerminatedEventArgs(UUID agentID, string agentName)
        {
            this.AgentID = agentID;
            this.AgentName = agentName;
        }
    }

    /// <summary>
    /// Data sent in response to a <see cref="FindFriend"/> request which contains the information to allow us to map the friends location
    /// </summary>
    public class FriendFoundReplyEventArgs : EventArgs
    {
        /// <summary>Get the ID of the agent we have received location information for</summary>
        public UUID AgentID { get; }

        /// <summary>Get the region handle where our mapped friend is located</summary>
        public ulong RegionHandle { get; }

        /// <summary>Get the simulator local position where our friend is located</summary>
        public Vector3 Location { get; }

        /// <summary>
        /// Construct a new instance of the FriendFoundReplyEventArgs class
        /// </summary>
        /// <param name="agentID">The ID of the agent we have requested location information for</param>
        /// <param name="regionHandle">The region handle where our friend is located</param>
        /// <param name="location">The simulator local position our friend is located</param>
        public FriendFoundReplyEventArgs(UUID agentID, ulong regionHandle, Vector3 location)
        {
            this.AgentID = agentID;
            this.RegionHandle = regionHandle;
            this.Location = location;
        }
    }
    #endregion
}
