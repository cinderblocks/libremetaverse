/*
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
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using LibreMetaverse.Messages.Linden;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public partial class AgentManager
    {
        /// <summary>
        /// Maximum size in bytes of chat messages
        /// </summary>
        public const int MaxChatMessageSize = 1023;

        /// <summary>
        /// Maximum size in bytes of script dialog labels
        /// </summary>
        public const int MaxScriptDialogLabelSize = 254;

        private static List<string> SplitMultibyteString(string message, int splitSizeInBytes, int maxParts = int.MaxValue)
        {
            if (splitSizeInBytes < 1)
            {
                throw new Exception("Split size must be at least 1 byte");
            }
            if (message.Length == 0)
            {
                return new List<string>() { string.Empty };
            }

            var messageParts = new List<string>();
            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
            var messageBytesOffset = 0;

            while (messageParts.Count < maxParts && messageBytesOffset < messageBytes.Length)
            {
                var partEndOffset = Math.Min(messageBytesOffset + splitSizeInBytes, messageBytes.Length);
                if (partEndOffset < messageBytes.Length)
                {
                    int scan = partEndOffset;
                    while (scan > messageBytesOffset)
                    {
                        var currentByte = messageBytes[scan - 1];
                        if (currentByte < 0x80 || currentByte > 0xBF)
                        {
                            break;
                        }
                        scan--;
                    }

                    if (scan == messageBytesOffset)
                    {
                        partEndOffset = Math.Min(messageBytesOffset + 1, messageBytes.Length);
                    }
                    else
                    {
                        partEndOffset = scan;
                    }
                }

                var newMessagePart = System.Text.Encoding.UTF8.GetString(
                    messageBytes,
                    messageBytesOffset, partEndOffset - messageBytesOffset
                );
                messageParts.Add(newMessagePart);

                messageBytesOffset = partEndOffset;
            }

            return messageParts;
        }

        private void ChatOnNegativeChannel(string message, int channel)
        {
            var messageChunks = SplitMultibyteString(message, MaxScriptDialogLabelSize, 1);

            foreach (var messageChunk in messageChunks)
            {
                var chatPacket = new ScriptDialogReplyPacket
                {
                    AgentData =
                    {
                        AgentID = AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    Data =
                    {
                        ObjectID = AgentID,
                        ChatChannel = channel,
                        ButtonIndex = 0,
                        ButtonLabel = Utils.StringToBytes(messageChunk)
                    }
                };
                Client.Network.SendPacket(chatPacket);
            }
        }

        /// <summary>Send a chat message to the region on a given channel</summary>
        /// <param name="message">Text to send</param>
        /// <param name="channel">Chat channel number (0 = public chat)</param>
        /// <param name="type">Chat type (normal, whisper, shout, etc.)</param>
        /// <param name="splitLargeMessages">When true, messages exceeding the server limit are split into multiple packets</param>
        public void Chat(string message, int channel, ChatType type, bool splitLargeMessages = true)
        {
            if (channel < 0)
            {
                ChatOnNegativeChannel(message, channel);
                return;
            }

            var messageChunks = SplitMultibyteString(message, MaxChatMessageSize, splitLargeMessages ? int.MaxValue : 1);
            foreach (var messageChunk in messageChunks)
            {
                var chatPacket = new ChatFromViewerPacket
                {
                    AgentData =
                    {
                        AgentID = AgentID,
                        SessionID = Client.Self.SessionID
                    },
                    ChatData =
                    {
                        Channel = channel,
                        Message = Utils.StringToBytes(messageChunk),
                        Type = (byte) type
                    }
                };
                Client.Network.SendPacket(chatPacket);
            }
        }

        /// <summary>Fetch offline instant messages stored by the server and deliver them as IM events</summary>
        /// <param name="cancellationToken">Token to cancel the capability request</param>
        public async Task RetrieveInstantMessagesAsync(CancellationToken cancellationToken = default)
        {
            var sim = Client.Network.CurrentSim;
            Uri? offlineMsgsCap = sim?.Caps?.CapabilityURI("ReadOfflineMsgs");
            if (offlineMsgsCap == null 
                || sim?.Caps?.CapabilityURI("AcceptFriendship") == null
                || sim?.Caps?.CapabilityURI("AcceptGroupInvite") == null)
            {
                RetrieveInstantMessagesLegacy();
                return;
            }

            try
            {
                var (response, data) = await Client.HttpCapsClient.GetAsync(offlineMsgsCap, cancellationToken);
                OfflineMessageHandlerCallback(response, data, null);
            }
            catch (Exception ex)
            {
                OfflineMessageHandlerCallback(null, null, ex);
            }
        }

        private void RetrieveInstantMessagesLegacy()
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

        /// <summary>Send an instant message to another avatar using default session and dialog settings</summary>
        /// <param name="target">UUID of the recipient avatar</param>
        /// <param name="message">Message text to send</param>
        public void InstantMessage(UUID target, string message)
        {
            InstantMessage(Name, target, message, AgentID.Equals(target) ? AgentID : target ^ AgentID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

        /// <summary>Send an instant message to another avatar using an explicit session ID</summary>
        /// <param name="target">UUID of the recipient avatar</param>
        /// <param name="message">Message text to send</param>
        /// <param name="imSessionID">IM session UUID; used to correlate replies in an ongoing conversation</param>
        public void InstantMessage(UUID target, string message, UUID imSessionID)
        {
            InstantMessage(Name, target, message, imSessionID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

        /// <summary>Send an instant message to a conference session that includes multiple participants</summary>
        /// <param name="fromName">Display name of the sender shown to recipients</param>
        /// <param name="target">UUID of the recipient or conference session</param>
        /// <param name="message">Message text to send</param>
        /// <param name="imSessionID">IM session UUID</param>
        /// <param name="conferenceIDs">Array of avatar UUIDs participating in the conference; packed into the binary bucket</param>
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

        /// <summary>Send a fully specified instant message packet</summary>
        /// <param name="fromName">Display name of the sender</param>
        /// <param name="target">UUID of the recipient</param>
        /// <param name="message">Message text to send</param>
        /// <param name="imSessionID">IM session UUID</param>
        /// <param name="dialog">IM dialog type controlling how the message is presented</param>
        /// <param name="offline">Whether to store the message for offline delivery</param>
        /// <param name="position">Sender's region position at time of send</param>
        /// <param name="regionID">UUID of the sender's current region</param>
        /// <param name="binaryBucket">Additional data attached to the message (format depends on dialog type)</param>
        public void InstantMessage(string fromName, UUID target, string message, UUID imSessionID,
            InstantMessageDialog dialog, InstantMessageOnline offline, Vector3 position, UUID regionID,
            byte[] binaryBucket)
        {
            if (target == UUID.Zero)
            {
                Logger.Error($"Suppressing instant message \"{message}\" to UUID.Zero", Client);
                return;
            }

            var messageParts = SplitMultibyteString(message, MaxChatMessageSize);
            foreach (var messagePart in messageParts)
            {
                ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket();

                if (imSessionID.Equals(UUID.Zero) || imSessionID.Equals(AgentID))
                {
                    imSessionID = AgentID.Equals(target) ? AgentID : target ^ AgentID;
                }

                im.AgentData.AgentID = Client.Self.AgentID;
                im.AgentData.SessionID = Client.Self.SessionID;

                im.MessageBlock.Dialog = (byte)dialog;
                im.MessageBlock.FromAgentName = Utils.StringToBytes(fromName);
                im.MessageBlock.FromGroup = false;
                im.MessageBlock.ID = imSessionID;
                im.MessageBlock.Message = Utils.StringToBytes(messagePart);
                im.MessageBlock.Offline = (byte)offline;
                im.MessageBlock.ToAgentID = target;

                im.MessageBlock.BinaryBucket = binaryBucket ?? Utils.EmptyBytes;

                im.MessageBlock.Position = Vector3.Zero;
                im.MessageBlock.RegionID = regionID;

                Client.Network.SendPacket(im);
            }
        }

        /// <summary>Send an instant message to a group chat session using the agent's own name as the sender</summary>
        /// <param name="groupID">UUID of the group whose chat session to send to</param>
        /// <param name="message">Message text to send</param>
        public void InstantMessageGroup(UUID groupID, string message)
        {
            InstantMessageGroup(Name, groupID, message);
        }

        /// <summary>Send an instant message to a group chat session with an explicit sender name</summary>
        /// <param name="fromName">Display name shown as the sender</param>
        /// <param name="groupID">UUID of the group whose chat session to send to</param>
        /// <param name="message">Message text to send</param>
        public void InstantMessageGroup(string fromName, UUID groupID, string message)
        {
            if (!GroupChatSessions.ContainsKey(groupID))
            {
                Logger.Error("No Active group chat session appears to exist, use RequestJoinGroupChat() to join one", Client);
                return;
            }

            var messageChunks = SplitMultibyteString(message, MaxChatMessageSize);
            foreach (var messageChunk in messageChunks)
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
                        Message = Utils.StringToBytes(messageChunk),
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
        }

        /// <summary>Request to join a group's chat session; raises <see cref="GroupChatJoined"/> on success</summary>
        /// <param name="groupID">UUID of the group to join the chat session for</param>
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

        /// <summary>Leave a group chat session and remove it from the local session tracking list</summary>
        /// <param name="groupID">UUID of the group chat session to leave</param>
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

            GroupChatSessions.TryRemove(groupID, out _);
        }

        /// <summary>Reply to an in-world script dialog by selecting one of its buttons</summary>
        /// <param name="channel">Chat channel the dialog reply is sent on</param>
        /// <param name="buttonIndex">Zero-based index of the selected button</param>
        /// <param name="buttonLabel">Label text of the selected button</param>
        /// <param name="objectID">UUID of the scripted object that sent the dialog</param>
        public void ReplyToScriptDialog(int channel, int buttonIndex, string buttonLabel, UUID objectID)
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
                    ButtonLabel = Utils.StringToBytes(buttonLabel),
                    ChatChannel = channel,
                    ObjectID = objectID
                }
            };

            Client.Network.SendPacket(reply);
        }

        /// <summary>Accept an invitation to a ChatterBox (group or conference) chat session via the ChatSessionRequest capability</summary>
        /// <param name="session_id">Session UUID from the invitation</param>
        /// <param name="cancellationToken">Token to cancel the capability request</param>
        public async Task ChatterBoxAcceptInviteAsync(UUID session_id, CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

                Uri? cap = Client.Network.CurrentSim.Caps.CapabilityURI("ChatSessionRequest");
            if (cap != null)
            {
                ChatSessionAcceptInvitation acceptInvite = new ChatSessionAcceptInvitation {SessionID = session_id};
                await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, acceptInvite.Serialize(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
        }

        /// <summary>Start a multi-party IM conference session via the ChatSessionRequest capability</summary>
        /// <param name="participants">List of avatar UUIDs to invite into the conference</param>
        /// <param name="tmp_session_id">Temporary session UUID generated by the caller to track this conference</param>
        /// <param name="cancellationToken">Token to cancel the capability request</param>
        public async Task StartIMConferenceAsync(List<UUID> participants, UUID tmp_session_id, CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
                throw new Exception("ChatSessionRequest capability is not currently available");

            Uri? cap = Client.Network.CurrentSim.Caps.CapabilityURI("ChatSessionRequest");
            if (cap == null)
                throw new Exception("ChatSessionRequest capability is not currently available");

            ChatSessionRequestStartConference startConference = new ChatSessionRequestStartConference
            {
                AgentsBlock = new UUID[participants.Count]
            };
            for (var i = 0; i < participants.Count; i++)
                startConference.AgentsBlock[i] = participants[i];
            startConference.SessionID = tmp_session_id;

            await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, startConference.Serialize(), cancellationToken).ConfigureAwait(false);
        }
    }
}
