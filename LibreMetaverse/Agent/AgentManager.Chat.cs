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
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
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

        public async Task RetrieveInstantMessages(CancellationToken cancellationToken = default)
        {
            Uri offlineMsgsCap = Client.Network.CurrentSim.Caps?.CapabilityURI("ReadOfflineMsgs");
            if (offlineMsgsCap == null 
                || Client.Network.CurrentSim.Caps.CapabilityURI("AcceptFriendship") == null
                || Client.Network.CurrentSim.Caps.CapabilityURI("AcceptGroupInvite") == null)
            {
                RetrieveInstantMessagesLegacy();
                return;
            }

            await Client.HttpCapsClient.GetRequestAsync(offlineMsgsCap, cancellationToken, OfflineMessageHandlerCallback);
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

        public void InstantMessage(UUID target, string message)
        {
            InstantMessage(Name, target, message, AgentID.Equals(target) ? AgentID : target ^ AgentID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

        public void InstantMessage(UUID target, string message, UUID imSessionID)
        {
            InstantMessage(Name, target, message, imSessionID,
                InstantMessageDialog.MessageFromAgent, InstantMessageOnline.Offline, SimPosition,
                UUID.Zero, Utils.EmptyBytes);
        }

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

        public void InstantMessageGroup(UUID groupID, string message)
        {
            InstantMessageGroup(Name, groupID, message);
        }

        public void InstantMessageGroup(string fromName, UUID groupID, string message)
        {
            lock (GroupChatSessions.Dictionary)
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
        }

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
            {
                if (GroupChatSessions.ContainsKey(groupID))
                    GroupChatSessions.Remove(groupID);
            }
        }

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

        public async Task ChatterBoxAcceptInvite(UUID session_id, CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("ChatSessionRequest");
            if (cap != null)
            {
                ChatSessionAcceptInvitation acceptInvite = new ChatSessionAcceptInvitation {SessionID = session_id};
                await Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, acceptInvite.Serialize(), cancellationToken);
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
        }

        public void StartIMConference(List<UUID> participants, UUID tmp_session_id, CancellationToken cancellationToken = default)
        {
            if (Client.Network.CurrentSim == null || Client.Network.CurrentSim.Caps == null)
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }

            Uri cap = Client.Network.CurrentSim.Caps.CapabilityURI("ChatSessionRequest");
            if (cap != null)
            {
                ChatSessionRequestStartConference startConference = new ChatSessionRequestStartConference
                {
                    AgentsBlock = new UUID[participants.Count]
                };

                for (var i = 0; i < participants.Count; i++)
                {
                    startConference.AgentsBlock[i] = participants[i];
                }

                startConference.SessionID = tmp_session_id;

                _ = Client.HttpCapsClient.PostRequestAsync(cap, OSDFormat.Xml, startConference.Serialize(), cancellationToken);
            }
            else
            {
                throw new Exception("ChatSessionRequest capability is not currently available");
            }
        }
    }
}
