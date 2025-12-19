/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn LLC
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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Interfaces;
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    public partial class AgentManager
    {
        public bool IsGroupMessage(InstantMessage message)
        {
            if (message.GroupIM)
                return true;
            
            lock (Client.Groups.GroupName2KeyCache.Dictionary)
            {
                if (Client.Groups.GroupName2KeyCache.ContainsKey(message.IMSessionID))
                    return true;
            }

            lock (GroupChatSessions.Dictionary)
            {
                if (GroupChatSessions.ContainsKey(message.IMSessionID))
                    return true;
            }

            return false;
        }

        protected void OfflineMessageHandlerCallback(HttpResponseMessage response, byte[] data, Exception error)
        {
            if (error != null)
            {
                Logger.Warn($"Failed to retrieve offline messages from the simulator: {error.Message}");
                RetrieveInstantMessagesLegacy();
                return;
            }

            if (m_InstantMessage == null) { return; } // don't bother if we don't have any listeners

            OSD result = OSDParser.Deserialize(data);
            if (result == null)
            {
                Logger.Warn("Failed to decode offline messages from data, trying legacy method (Null reply)");
                RetrieveInstantMessagesLegacy();
                return;
            }
            if (result.Type != OSDType.Array)
            {
                Logger.Warn("Failed to decode offline messages from data trying legacy method (Wrong unpack type expected array)");
                RetrieveInstantMessagesLegacy();
                return;
            }
            try
            {
                OSDArray respMap = (OSDArray)result;
                ArrayList listEntrys = respMap.ToArrayList();
                foreach (OSDArray listEntry in listEntrys)
                {
                    foreach (var osd in listEntry)
                    {
                        var msg = (OSDMap)osd;

                        InstantMessage message;
                        message.FromAgentID = msg["from_agent_id"].AsUUID();
                        message.FromAgentName = msg["from_agent_name"].AsString();
                        message.ToAgentID = msg["to_agent_id"].AsUUID();
                        message.RegionID = msg["region_id"].AsUUID();
                        message.Dialog = (InstantMessageDialog)msg["dialog"].AsInteger();
                        message.IMSessionID = msg["transaction-id"].AsUUID();
                        message.Timestamp = new DateTime(msg["timestamp"].AsInteger());
                        message.Message = msg["message"].AsString();
                        message.Offline = msg.ContainsKey("offline")
                            ? (InstantMessageOnline)msg["offline"].AsInteger()
                            : InstantMessageOnline.Offline;
                        message.ParentEstateID = msg.ContainsKey("parent_estate_id")
                            ? msg["parent_estate_id"].AsUInteger() : 1;
                        message.Position = msg.ContainsKey("position")
                            ? msg["position"].AsVector3()
                            : new Vector3(msg["local_x"], msg["local_y"], msg["local_z"]);
                        message.BinaryBucket = msg.ContainsKey("binary_bucket")
                            ? msg["binary_bucket"].AsBinary() : new byte[] { 0 };
                        message.GroupIM = msg.ContainsKey("from_group") && msg["from_group"].AsBoolean();

                        if (message.GroupIM)
                        {
                            lock (GroupChatSessions.Dictionary)
                            {
                                if (!GroupChatSessions.ContainsKey(message.IMSessionID))
                                    GroupChatSessions.Add(message.IMSessionID, new List<ChatSessionMember>());
                            }
                        }
                        
                        OnInstantMessage(new InstantMessageEventArgs(message, null));
                    }
                }
            }
            catch
            {
                Logger.Warn("Failed to decode offline messages from data; trying legacy method");
                RetrieveInstantMessagesLegacy();
            }
        }

        /// <summary>
        /// EQ Message fired with the result of SetDisplayName request
        /// </summary>
        /// <param name="capsKey">The message key</param>
        /// <param name="message">the IMessage object containing the deserialized data sent from the simulator</param>
        /// <param name="simulator">The simulator originating the event message</param>
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

            if (!Client.Settings.MULTIPLE_SIMS) { return; }
            IPEndPoint endPoint = new IPEndPoint(msg.Address, msg.Port);
            Simulator sim = Client.Network.FindSimulator(endPoint);

            if (sim == null)
            {
                Logger.Error($"Got EstablishAgentCommunication for unknown sim {msg.Address}:{msg.Port}", Client);

                // FIXME: Should we use this opportunity to connect to the simulator?
            }
            else
            {
                Logger.Info($"Got EstablishAgentCommunication for {sim}", Client);

                sim.SetSeedCaps(msg.SeedCapability);
            }
        }

        /// <summary>
        /// Process TeleportFailed message sent via EventQueue, informs agent its last teleport has failed and why.
        /// </summary>
        /// <param name="messageKey">The message key</param>
        /// <param name="message">An IMessage object Deserialized from the received message event</param>
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
        /// <param name="capsKey">The message key</param>
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

            // pass the packet onto the teleport handler
            TeleportHandler(this, new PacketReceivedEventArgs(p, simulator));
        }

        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
            LoginResponseData reply)
        {
            AgentID = reply.AgentID;
            SessionID = reply.SessionID;
            SecureSessionID = reply.SecureSessionID;
            FirstName = reply.FirstName;
            LastName = reply.LastName;
            StartLocation = reply.StartLocation;
            AgentAccess = reply.AgentAccess;
            Movement.Camera.LookDirection(reply.LookAt);
            home = reply.Home;
            LookAt = reply.LookAt;
            Benefits = reply.AccountLevelBenefits;

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
        /// <param name="simulator">The simulator originating the event message</param>
        private void CrossedRegionEventHandler(string capsKey, IMessage message, Simulator simulator)
        {
            CrossedRegionMessage crossed = (CrossedRegionMessage)message;

            IPEndPoint endPoint = new IPEndPoint(crossed.IP, crossed.Port);

            Logger.Info($"Crossed in to new region area, attempting to connect to {endPoint}", Client);

            // Resolve the simulator context for this event. Use the simulator parameter as the
            // originating (old) simulator.
            Simulator oldSim = ResolveSimulator(null, simulator);
            Simulator newSim = Client.Network.Connect(endPoint, crossed.RegionHandle, true, crossed.SeedCapability,
                        crossed.RegionSizeX, crossed.RegionSizeY);

            if (newSim != null)
            {
                Logger.Info($"Finished crossing over in to region {newSim}", Client);
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
                Logger.Warn($"Failed to connect to new region {endPoint} after crossing over", Client);
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
            Logger.Info($"Attempt to send group chat to non-existent session for group {msg.SessionID}", Client);
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
                {
                    if (!GroupChatSessions.ContainsKey(msg.SessionID))
                        GroupChatSessions.Add(msg.SessionID, new List<ChatSessionMember>());
                }
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
            {
                if (!GroupChatSessions.ContainsKey(msg.SessionID))
                    GroupChatSessions.Add(msg.SessionID, new List<ChatSessionMember>());
            }

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
                        {
                            lock (GroupChatSessions.Dictionary)
                                GroupChatSessions[msg.SessionID].Remove(fndMbr);
                        }

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

            lock (GroupChatSessions.Dictionary)
            {
                if (!GroupChatSessions.ContainsKey(msg.IMSessionID))
                {
                    GroupChatSessions.Add(msg.IMSessionID, new List<ChatSessionMember>());
                }
            }

            try
            {
                ChatterBoxAcceptInvite(msg.IMSessionID).Wait();
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed joining IM:", ex, Client);
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
        /// <param name="cancellationToken"></param>
        public void ModerateChatSessions(UUID sessionID, UUID memberID, string key, bool moderate, 
            CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("ChatSessionRequest capability is not currently available");

            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("ChatSessionRequest");

            if (cap == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

            ChatSessionRequestMuteUpdate payload = new ChatSessionRequestMuteUpdate
            {
                RequestKey = key,
                RequestValue = moderate,
                SessionID = sessionID,
                AgentID = memberID
            };
            _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, payload.Serialize(), cancellationToken);
        }
    }
}
