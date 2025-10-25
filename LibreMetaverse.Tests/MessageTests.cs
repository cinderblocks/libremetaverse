/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2025, Sjofn LLC
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
using System.IO;
using System.Net;
using System.Text;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// These unit tests specifically test the Message class can serialize and deserialize its own data properly
    /// a passed test does not necessarily indicate the formatting is correct in the resulting OSD to be handled
    /// by the simulator.
    /// </summary>
    [TestFixture]
    public class MessageTests : Assert
    {
        private readonly Uri testURI = new Uri("https://sim3187.agni.lindenlab.com:12043/cap/6028fc44-c1e5-80a1-f902-19bde114458b");
        private readonly IPAddress testIP = IPAddress.Parse("127.0.0.1");
        private readonly ulong testHandle = 1106108697797888;

        [Test]
        public void AgentGroupDataUpdateMessage()
        {
            AgentGroupDataUpdateMessage s = new AgentGroupDataUpdateMessage
            {
                AgentID = UUID.Random()
            };


            AgentGroupDataUpdateMessage.GroupData[] blocks = new AgentGroupDataUpdateMessage.GroupData[2];
            AgentGroupDataUpdateMessage.GroupData g1 = new AgentGroupDataUpdateMessage.GroupData
            {
                AcceptNotices = false,
                Contribution = 1024,
                GroupID = UUID.Random(),
                GroupInsigniaID = UUID.Random(),
                GroupName = "Group Name Test 1",
                GroupPowers = GroupPowers.Accountable | GroupPowers.AllowLandmark | GroupPowers.AllowSetHome
            };

            blocks[0] = g1;

            AgentGroupDataUpdateMessage.GroupData g2 = new AgentGroupDataUpdateMessage.GroupData
            {
                AcceptNotices = false,
                Contribution = 16,
                GroupID = UUID.Random(),
                GroupInsigniaID = UUID.Random(),
                GroupName = "Group Name Test 2",
                GroupPowers = GroupPowers.ChangeActions | GroupPowers.DeedObject
            };
            blocks[1] = g2;

            s.GroupDataBlock = blocks;

            AgentGroupDataUpdateMessage.NewGroupData[] nblocks = new AgentGroupDataUpdateMessage.NewGroupData[2];

            AgentGroupDataUpdateMessage.NewGroupData ng1 = new AgentGroupDataUpdateMessage.NewGroupData
                {
                    ListInProfile = false
                };
            nblocks[0] = ng1;

            AgentGroupDataUpdateMessage.NewGroupData ng2 = new AgentGroupDataUpdateMessage.NewGroupData
                {
                    ListInProfile = true
                };
            nblocks[1] = ng2;

            s.NewGroupDataBlock = nblocks;

            OSDMap map = s.Serialize();

            AgentGroupDataUpdateMessage t = new AgentGroupDataUpdateMessage();
            t.Deserialize(map);

            Assert.That(t.AgentID, Is.EqualTo(s.AgentID));

            for (int i = 0; i < t.GroupDataBlock.Length; i++)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(t.GroupDataBlock[i].AcceptNotices, Is.EqualTo(s.GroupDataBlock[i].AcceptNotices));
                    Assert.That(t.GroupDataBlock[i].Contribution, Is.EqualTo(s.GroupDataBlock[i].Contribution));
                    Assert.That(t.GroupDataBlock[i].GroupID, Is.EqualTo(s.GroupDataBlock[i].GroupID));
                    Assert.That(t.GroupDataBlock[i].GroupInsigniaID, Is.EqualTo(s.GroupDataBlock[i].GroupInsigniaID));
                    Assert.That(t.GroupDataBlock[i].GroupName, Is.EqualTo(s.GroupDataBlock[i].GroupName));
                    Assert.That(t.GroupDataBlock[i].GroupPowers, Is.EqualTo(s.GroupDataBlock[i].GroupPowers));
                }
            }

            for (int i = 0; i < t.NewGroupDataBlock.Length; i++)
            {
                Assert.That(t.NewGroupDataBlock[i].ListInProfile, Is.EqualTo(s.NewGroupDataBlock[i].ListInProfile));
            }
        }

        [Test]
        public void TeleportFinishMessage()
        {
            TeleportFinishMessage s = new TeleportFinishMessage
            {
                AgentID = UUID.Random(),
                Flags = TeleportFlags.ViaLocation | TeleportFlags.IsFlying,
                IP = testIP,
                LocationID = 32767,
                Port = 3000,
                RegionHandle = testHandle,
                SeedCapability = testURI,
                SimAccess = SimAccess.Mature
            };

            OSDMap map = s.Serialize();

            TeleportFinishMessage t = new TeleportFinishMessage();
            t.Deserialize(map);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
                Assert.That(t.Flags, Is.EqualTo(s.Flags));
                Assert.That(t.IP, Is.EqualTo(s.IP));
                Assert.That(t.LocationID, Is.EqualTo(s.LocationID));
                Assert.That(t.Port, Is.EqualTo(s.Port));
                Assert.That(t.RegionHandle, Is.EqualTo(s.RegionHandle));
                Assert.That(t.SeedCapability, Is.EqualTo(s.SeedCapability));
                Assert.That(t.SimAccess, Is.EqualTo(s.SimAccess));
            }
        }

        [Test]
        public void EstablishAgentCommunicationMessage()
        {
            EstablishAgentCommunicationMessage s = new EstablishAgentCommunicationMessage
            {
                Address = testIP,
                AgentID = UUID.Random(),
                Port = 3000,
                SeedCapability = testURI
            };

            OSDMap map = s.Serialize();

            EstablishAgentCommunicationMessage t = new EstablishAgentCommunicationMessage();
            t.Deserialize(map);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(t.Address, Is.EqualTo(s.Address));
                Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
                Assert.That(t.Port, Is.EqualTo(s.Port));
                Assert.That(t.SeedCapability, Is.EqualTo(s.SeedCapability));
            }
        }

        [Test]
        public void ParcelObjectOwnersMessage()
        {
            ParcelObjectOwnersReplyMessage s = new ParcelObjectOwnersReplyMessage
            {
                PrimOwnersBlock = new ParcelObjectOwnersReplyMessage.PrimOwner[2]
            };

            ParcelObjectOwnersReplyMessage.PrimOwner obj = new ParcelObjectOwnersReplyMessage.PrimOwner
                {
                    OwnerID = UUID.Random(),
                    Count = 10,
                    IsGroupOwned = true,
                    OnlineStatus = false,
                    TimeStamp = new DateTime(2010, 4, 13, 7, 19, 43)
                };
            s.PrimOwnersBlock[0] = obj;

            ParcelObjectOwnersReplyMessage.PrimOwner obj1 = new ParcelObjectOwnersReplyMessage.PrimOwner
                {
                    OwnerID = UUID.Random(),
                    Count = 0,
                    IsGroupOwned = false,
                    OnlineStatus = false,
                    TimeStamp = new DateTime(1991, 1, 31, 3, 13, 31)
                };
            s.PrimOwnersBlock[1] = obj1;

            OSDMap map = s.Serialize();

            ParcelObjectOwnersReplyMessage t = new ParcelObjectOwnersReplyMessage();
            t.Deserialize(map);

            for (int i = 0; i < t.PrimOwnersBlock.Length; i++)
            {
                Assert.That(t.PrimOwnersBlock[i].Count, Is.EqualTo(s.PrimOwnersBlock[i].Count));
                Assert.That(t.PrimOwnersBlock[i].IsGroupOwned, Is.EqualTo(s.PrimOwnersBlock[i].IsGroupOwned));
                Assert.That(t.PrimOwnersBlock[i].OnlineStatus, Is.EqualTo(s.PrimOwnersBlock[i].OnlineStatus));
                Assert.That(t.PrimOwnersBlock[i].OwnerID, Is.EqualTo(s.PrimOwnersBlock[i].OwnerID));
                Assert.That(t.PrimOwnersBlock[i].TimeStamp, Is.EqualTo(s.PrimOwnersBlock[i].TimeStamp));
            }
        }

        [Test]
        public void ChatterBoxInvitationMessage()
        {
            ChatterBoxInvitationMessage s = new ChatterBoxInvitationMessage
            {
                BinaryBucket = Utils.EmptyBytes,
                Dialog = InstantMessageDialog.InventoryOffered,
                FromAgentID = UUID.Random(),
                FromAgentName = "Prokofy Neva",
                GroupIM = false
            };
            s.IMSessionID = s.FromAgentID ^ UUID.Random();
            s.Message = "Test Test Test";
            s.Offline = InstantMessageOnline.Online;
            s.ParentEstateID = 1;
            s.Position = Vector3.One;
            s.RegionID = UUID.Random();
            s.Timestamp = DateTime.UtcNow;
            s.ToAgentID = UUID.Random();

            OSDMap map = s.Serialize();

            ChatterBoxInvitationMessage t = new ChatterBoxInvitationMessage();
            t.Deserialize(map);

            Assert.That(t.BinaryBucket, Is.EqualTo(s.BinaryBucket));
            Assert.That(t.Dialog, Is.EqualTo(s.Dialog));
            Assert.That(t.FromAgentID, Is.EqualTo(s.FromAgentID));
            Assert.That(t.FromAgentName, Is.EqualTo(s.FromAgentName));
            Assert.That(t.GroupIM, Is.EqualTo(s.GroupIM));
            Assert.That(t.IMSessionID, Is.EqualTo(s.IMSessionID));
            Assert.That(t.Message, Is.EqualTo(s.Message));
            Assert.That(t.Offline, Is.EqualTo(s.Offline));
            Assert.That(t.ParentEstateID, Is.EqualTo(s.ParentEstateID));
            Assert.That(t.Position, Is.EqualTo(s.Position));
            Assert.That(t.RegionID, Is.EqualTo(s.RegionID));
            Assert.That(t.Timestamp, Is.EqualTo(s.Timestamp));
            Assert.That(t.ToAgentID, Is.EqualTo(s.ToAgentID));
        }

        [Test]
        public void ChatterboxSessionEventReplyMessage()
        {
            ChatterboxSessionEventReplyMessage s = new ChatterboxSessionEventReplyMessage
            {
                SessionID = UUID.Random(),
                Success = true
            };

            OSDMap map = s.Serialize();

            ChatterboxSessionEventReplyMessage t = new ChatterboxSessionEventReplyMessage();
            t.Deserialize(map);

            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
            Assert.That(t.Success, Is.EqualTo(s.Success));
        }

        [Test]
        public void ChatterBoxSessionStartReplyMessage()
        {
            ChatterBoxSessionStartReplyMessage s = new ChatterBoxSessionStartReplyMessage
            {
                ModeratedVoice = true,
                SessionID = UUID.Random(),
                SessionName = "Test Session",
                Success = true,
                TempSessionID = UUID.Random(),
                Type = 1,
                VoiceEnabled = true
            };

            OSDMap map = s.Serialize();

            ChatterBoxSessionStartReplyMessage t = new ChatterBoxSessionStartReplyMessage();
            t.Deserialize(map);

            Assert.That(t.ModeratedVoice, Is.EqualTo(s.ModeratedVoice));
            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
            Assert.That(t.SessionName, Is.EqualTo(s.SessionName));
            Assert.That(t.Success, Is.EqualTo(s.Success));
            Assert.That(t.TempSessionID, Is.EqualTo(s.TempSessionID));
            Assert.That(t.Type, Is.EqualTo(s.Type));
            Assert.That(t.VoiceEnabled, Is.EqualTo(s.VoiceEnabled));
        }

        [Test]
        public void ChatterBoxSessionAgentListUpdatesMessage()
        {
            ChatterBoxSessionAgentListUpdatesMessage s = new ChatterBoxSessionAgentListUpdatesMessage
                {
                    SessionID = UUID.Random(),
                    Updates = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock[1]
                };

            ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block1 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock
                {
                    AgentID = UUID.Random(),
                    CanVoiceChat = true,
                    IsModerator = true,
                    MuteText = true,
                    MuteVoice = true,
                    Transition = "ENTER"
                };

            ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block2 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock
                {
                    AgentID = UUID.Random(),
                    CanVoiceChat = true,
                    IsModerator = true,
                    MuteText = true,
                    MuteVoice = true,
                    Transition = "LEAVE"
                };

            s.Updates[0] = block1;
            // s.Updates[1] = block2;

            OSDMap map = s.Serialize();

            ChatterBoxSessionAgentListUpdatesMessage t = new ChatterBoxSessionAgentListUpdatesMessage();
            t.Deserialize(map);

            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
            for (int i = 0; i < t.Updates.Length; i++)
            {
                Assert.That(t.Updates[i].AgentID, Is.EqualTo(s.Updates[i].AgentID));
                Assert.That(t.Updates[i].CanVoiceChat, Is.EqualTo(s.Updates[i].CanVoiceChat));
                Assert.That(t.Updates[i].IsModerator, Is.EqualTo(s.Updates[i].IsModerator));
                Assert.That(t.Updates[i].MuteText, Is.EqualTo(s.Updates[i].MuteText));
                Assert.That(t.Updates[i].MuteVoice, Is.EqualTo(s.Updates[i].MuteVoice));
                Assert.That(t.Updates[i].Transition, Is.EqualTo(s.Updates[i].Transition));
            }
        }

        [Test]
        public void ViewerStatsMessage()
        {
            ViewerStatsMessage s = new ViewerStatsMessage
            {
                AgentFPS = 45.5f,
                AgentsInView = 1,
                SystemCPU = "Intel 80286",
                StatsDropped = 2,
                StatsFailedResends = 3,
                SystemGPU = "Vesa VGA+",
                SystemGPUClass = 4,
                SystemGPUVendor = "China",
                SystemGPUVersion = string.Empty,
                InCompressedPackets = 5000,
                InKbytes = 6000,
                InPackets = 22000,
                InSavings = 19,
                MiscInt1 = 5,
                MiscInt2 = 6,
                FailuresInvalid = 20,
                AgentLanguage = "en",
                AgentMemoryUsed = 12878728,
                MetersTraveled = 9999123,
                object_kbytes = 70001,
                FailuresOffCircuit = 201,
                SystemOS = "Palm OS 3.1",
                OutCompressedPackets = 8000,
                OutKbytes = 9000999,
                OutPackets = 21000210,
                OutSavings = 181,
                AgentPing = 135579,
                SystemInstalledRam = 4000000,
                RegionsVisited = 4579,
                FailuresResent = 9,
                AgentRuntime = 360023,
                FailuresSendPacket = 565,
                SessionID = UUID.Random(),
                SimulatorFPS = 454,
                AgentStartTime = new DateTime(1973, 1, 16, 5, 23, 33),
                MiscString1 = "Unused String",
                texture_kbytes = 9367498382,
                AgentVersion = "1",
                MiscVersion = 1,
                VertexBuffersEnabled = true,
                world_kbytes = 232344439
            };

            OSDMap map = s.Serialize();
            ViewerStatsMessage t = new ViewerStatsMessage();
            t.Deserialize(map);

            Assert.That(t.AgentFPS, Is.EqualTo(s.AgentFPS));
            Assert.That(t.AgentsInView, Is.EqualTo(s.AgentsInView));
            Assert.That(t.SystemCPU, Is.EqualTo(s.SystemCPU));
            Assert.That(t.StatsDropped, Is.EqualTo(s.StatsDropped));
            Assert.That(t.StatsFailedResends, Is.EqualTo(s.StatsFailedResends));
            Assert.That(t.SystemGPU, Is.EqualTo(s.SystemGPU));
            Assert.That(t.SystemGPUClass, Is.EqualTo(s.SystemGPUClass));
            Assert.That(t.SystemGPUVendor, Is.EqualTo(s.SystemGPUVendor));
            Assert.That(t.SystemGPUVersion, Is.EqualTo(s.SystemGPUVersion));
            Assert.That(t.InCompressedPackets, Is.EqualTo(s.InCompressedPackets));
            Assert.That(t.InKbytes, Is.EqualTo(s.InKbytes));
            Assert.That(t.InPackets, Is.EqualTo(s.InPackets));
            Assert.That(t.InSavings, Is.EqualTo(s.InSavings));
            Assert.That(t.MiscInt1, Is.EqualTo(s.MiscInt1));
            Assert.That(t.MiscInt2, Is.EqualTo(s.MiscInt2));
            Assert.That(t.FailuresInvalid, Is.EqualTo(s.FailuresInvalid));
            Assert.That(t.AgentLanguage, Is.EqualTo(s.AgentLanguage));
            Assert.That(t.AgentMemoryUsed, Is.EqualTo(s.AgentMemoryUsed));
            Assert.That(t.MetersTraveled, Is.EqualTo(s.MetersTraveled));
            Assert.That(t.object_kbytes, Is.EqualTo(s.object_kbytes));
            Assert.That(t.FailuresOffCircuit, Is.EqualTo(s.FailuresOffCircuit));
            Assert.That(t.SystemOS, Is.EqualTo(s.SystemOS));
            Assert.That(t.OutCompressedPackets, Is.EqualTo(s.OutCompressedPackets));
            Assert.That(t.OutKbytes, Is.EqualTo(s.OutKbytes));
            Assert.That(t.OutPackets, Is.EqualTo(s.OutPackets));
            Assert.That(t.OutSavings, Is.EqualTo(s.OutSavings));
            Assert.That(t.AgentPing, Is.EqualTo(s.AgentPing));
            Assert.That(t.SystemInstalledRam, Is.EqualTo(s.SystemInstalledRam));
            Assert.That(t.RegionsVisited, Is.EqualTo(s.RegionsVisited));
            Assert.That(t.FailuresResent, Is.EqualTo(s.FailuresResent));
            Assert.That(t.AgentRuntime, Is.EqualTo(s.AgentRuntime));
            Assert.That(t.FailuresSendPacket, Is.EqualTo(s.FailuresSendPacket));
            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
            Assert.That(t.SimulatorFPS, Is.EqualTo(s.SimulatorFPS));
            Assert.That(t.AgentStartTime, Is.EqualTo(s.AgentStartTime));
            Assert.That(t.MiscString1, Is.EqualTo(s.MiscString1));
            Assert.That(t.texture_kbytes, Is.EqualTo(s.texture_kbytes));
            Assert.That(t.AgentVersion, Is.EqualTo(s.AgentVersion));
            Assert.That(t.MiscVersion, Is.EqualTo(s.MiscVersion));
            Assert.That(t.VertexBuffersEnabled, Is.EqualTo(s.VertexBuffersEnabled));
            Assert.That(t.world_kbytes, Is.EqualTo(s.world_kbytes));


        }

        [Test]
        public void ParcelVoiceInfoRequestMessage()
        {
            ParcelVoiceInfoRequestMessage s = new ParcelVoiceInfoRequestMessage
            {
                SipChannelUri = testURI,
                ParcelID = 1,
                RegionName = "Hooper"
            };

            OSDMap map = s.Serialize();

            ParcelVoiceInfoRequestMessage t = new ParcelVoiceInfoRequestMessage();
            t.Deserialize(map);

            Assert.That(t.SipChannelUri, Is.EqualTo(s.SipChannelUri));
            Assert.That(t.ParcelID, Is.EqualTo(s.ParcelID));
            Assert.That(t.RegionName, Is.EqualTo(s.RegionName));
        }

        [Test]
        public void ScriptRunningReplyMessage()
        {
            ScriptRunningReplyMessage s = new ScriptRunningReplyMessage
            {
                ItemID = UUID.Random(),
                Mono = true,
                Running = true,
                ObjectID = UUID.Random()
            };

            OSDMap map = s.Serialize();

            ScriptRunningReplyMessage t = new ScriptRunningReplyMessage();
            t.Deserialize(map);

            Assert.That(t.ItemID, Is.EqualTo(s.ItemID));
            Assert.That(t.Mono, Is.EqualTo(s.Mono));
            Assert.That(t.ObjectID, Is.EqualTo(s.ObjectID));
            Assert.That(t.Running, Is.EqualTo(s.Running));

        }

        [Test]
        public void MapLayerMessage()
        {

            MapLayerReplyVariant s = new MapLayerReplyVariant
            {
                Flags = 1
            };

            MapLayerReplyVariant.LayerData[] blocks = new MapLayerReplyVariant.LayerData[2];

            MapLayerReplyVariant.LayerData block = new MapLayerReplyVariant.LayerData
            {
                ImageID = UUID.Random(),
                Bottom = 1,
                Top = 2,
                Left = 3,
                Right = 4
            };


            blocks[0] = block;

            block.ImageID = UUID.Random();
            block.Bottom = 5;
            block.Top = 6;
            block.Left = 7;
            block.Right = 9;

            blocks[1] = block;

            s.LayerDataBlocks = blocks;

            OSDMap map = s.Serialize();

            MapLayerReplyVariant t = new MapLayerReplyVariant();

            t.Deserialize(map);

            Assert.That(t.Flags, Is.EqualTo(s.Flags));


            for (int i = 0; i < s.LayerDataBlocks.Length; i++)
            {
                Assert.That(t.LayerDataBlocks[i].ImageID, Is.EqualTo(s.LayerDataBlocks[i].ImageID));
                Assert.That(t.LayerDataBlocks[i].Top, Is.EqualTo(s.LayerDataBlocks[i].Top));
                Assert.That(t.LayerDataBlocks[i].Left, Is.EqualTo(s.LayerDataBlocks[i].Left));
                Assert.That(t.LayerDataBlocks[i].Right, Is.EqualTo(s.LayerDataBlocks[i].Right));
                Assert.That(t.LayerDataBlocks[i].Bottom, Is.EqualTo(s.LayerDataBlocks[i].Bottom));
            }
        }

        [Test] // VARIANT A
        public void ChatSessionRequestStartConference()
        {
            ChatSessionRequestStartConference s = new ChatSessionRequestStartConference
            {
                SessionID = UUID.Random(),
                AgentsBlock = new UUID[2]
            };
            s.AgentsBlock[0] = UUID.Random();
            s.AgentsBlock[0] = UUID.Random();

            OSDMap map = s.Serialize();

            ChatSessionRequestStartConference t = new ChatSessionRequestStartConference();
            t.Deserialize(map);

            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
            Assert.That(t.Method, Is.EqualTo(s.Method));
            for (int i = 0; i < t.AgentsBlock.Length; i++)
            {
                Assert.That(t.AgentsBlock[i], Is.EqualTo(s.AgentsBlock[i]));
            }
        }

        [Test]
        public void ChatSessionRequestMuteUpdate()
        {
            ChatSessionRequestMuteUpdate s = new ChatSessionRequestMuteUpdate
            {
                AgentID = UUID.Random(),
                RequestKey = "text",
                RequestValue = true,
                SessionID = UUID.Random()
            };

            OSDMap map = s.Serialize();

            ChatSessionRequestMuteUpdate t = new ChatSessionRequestMuteUpdate();
            t.Deserialize(map);

            Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
            Assert.That(t.Method, Is.EqualTo(s.Method));
            Assert.That(t.RequestKey, Is.EqualTo(s.RequestKey));
            Assert.That(t.RequestValue, Is.EqualTo(s.RequestValue));
            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
        }

        [Test]
        public void ChatSessionAcceptInvitation()
        {
            ChatSessionAcceptInvitation s = new ChatSessionAcceptInvitation
            {
                SessionID = UUID.Random()
            };

            OSDMap map = s.Serialize();

            ChatSessionAcceptInvitation t = new ChatSessionAcceptInvitation();
            t.Deserialize(map);

            Assert.That(t.Method, Is.EqualTo(s.Method));
            Assert.That(t.SessionID, Is.EqualTo(s.SessionID));
        }

        [Test]
        public void RequiredVoiceVersionMessage()
        {
            RequiredVoiceVersionMessage s = new RequiredVoiceVersionMessage
            {
                MajorVersion = 1,
                MinorVersion = 0,
                RegionName = "Hooper"
            };

            OSDMap map = s.Serialize();

            RequiredVoiceVersionMessage t = new RequiredVoiceVersionMessage();
            t.Deserialize(map);

            Assert.That(t.MajorVersion, Is.EqualTo(s.MajorVersion));
            Assert.That(t.MinorVersion, Is.EqualTo(s.MinorVersion));
            Assert.That(t.RegionName, Is.EqualTo(s.RegionName));
        }

        [Test]
        public void CopyInventoryFromNotecardMessage()
        {
            CopyInventoryFromNotecardMessage s = new CopyInventoryFromNotecardMessage
            {
                CallbackID = 1,
                FolderID = UUID.Random(),
                ItemID = UUID.Random(),
                NotecardID = UUID.Random(),
                ObjectID = UUID.Random()
            };

            OSDMap map = s.Serialize();

            CopyInventoryFromNotecardMessage t = new CopyInventoryFromNotecardMessage();
            t.Deserialize(map);

            Assert.That(t.CallbackID, Is.EqualTo(s.CallbackID));
            Assert.That(t.FolderID, Is.EqualTo(s.FolderID));
            Assert.That(t.ItemID, Is.EqualTo(s.ItemID));
            Assert.That(t.NotecardID, Is.EqualTo(s.NotecardID));
            Assert.That(t.ObjectID, Is.EqualTo(s.ObjectID));
        }

        [Test]
        public void ProvisionVoiceAccountRequestMessage()
        {
            ProvisionVoiceAccountRequestMessage s = new ProvisionVoiceAccountRequestMessage
            {
                Username = "username",
                Password = "password"
            };

            OSDMap map = s.Serialize();

            ProvisionVoiceAccountRequestMessage t = new ProvisionVoiceAccountRequestMessage();
            t.Deserialize(map);

            Assert.That(t.Password, Is.EqualTo(s.Password));
            Assert.That(t.Username, Is.EqualTo(s.Username));
        }

        [Test]
        public void UpdateAgentLanguageMessage()
        {
            UpdateAgentLanguageMessage s = new UpdateAgentLanguageMessage
            {
                Language = "en",
                LanguagePublic = false
            };

            OSDMap map = s.Serialize();

            UpdateAgentLanguageMessage t = new UpdateAgentLanguageMessage();
            t.Deserialize(map);

            Assert.That(t.Language, Is.EqualTo(s.Language));
            Assert.That(t.LanguagePublic, Is.EqualTo(s.LanguagePublic));

        }

        [Test]
        public void ParcelPropertiesMessage()
        {
            ParcelPropertiesMessage s = new ParcelPropertiesMessage
            {
                AABBMax = Vector3.Parse("<1,2,3>"),
                AABBMin = Vector3.Parse("<2,3,1>"),
                AnyAVSounds = true,
                Area = 1024,
                AuctionID = uint.MaxValue,
                AuthBuyerID = UUID.Random(),
                Bitmap = Utils.EmptyBytes,
                Category = ParcelCategory.Educational,
                ClaimDate = new DateTime(2008, 12, 25, 3, 15, 22),
                ClaimPrice = 1000,
                Desc = "Test Description",
                GroupAVSounds = true,
                GroupID = UUID.Random(),
                GroupPrims = 50,
                IsGroupOwned = false,
                LandingType = LandingType.None,
                LocalID = 1,
                MaxPrims = 234,
                MediaAutoScale = false,
                MediaDesc = "Example Media Description",
                MediaHeight = 480,
                MediaID = UUID.Random(),
                MediaLoop = false,
                MediaType = "text/html",
                MediaURL = "http://www.openmetaverse.co",
                MediaWidth = 640,
                MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075", // Yee Haw
                Name = "Test Name",
                ObscureMedia = false,
                ObscureMusic = false,
                OtherCleanTime = 5,
                OtherCount = 200,
                OtherPrims = 300,
                OwnerID = UUID.Random(),
                OwnerPrims = 0,
                ParcelFlags = ParcelFlags.AllowDamage | ParcelFlags.AllowGroupScripts | ParcelFlags.AllowVoiceChat,
                ParcelPrimBonus = 0f,
                PassHours = 1.5f,
                PassPrice = 10,
                PublicCount = 20,
                RegionDenyAgeUnverified = false,
                RegionDenyAnonymous = false,
                RegionPushOverride = true,
                RentPrice = 0,
                RequestResult = ParcelResult.Single,
                SalePrice = 9999,
                SeeAVs = true,
                SelectedPrims = 1,
                SelfCount = 2,
                SequenceID = -4000,
                SimWideMaxPrims = 937,
                SimWideTotalPrims = 117,
                SnapSelection = false,
                SnapshotID = UUID.Random(),
                Status = ParcelStatus.Leased,
                TotalPrims = 219,
                UserLocation = Vector3.Parse("<3,4,5>"),
                UserLookAt = Vector3.Parse("<5,4,3>")
            };

            OSDMap map = s.Serialize();
            ParcelPropertiesMessage t = new ParcelPropertiesMessage();

            t.Deserialize(map);

            Assert.That(t.AABBMax, Is.EqualTo(s.AABBMax));
            Assert.That(t.AABBMin, Is.EqualTo(s.AABBMin));
            Assert.That(t.AnyAVSounds, Is.EqualTo(s.AnyAVSounds));
            Assert.That(t.Area, Is.EqualTo(s.Area));
            Assert.That(t.AuctionID, Is.EqualTo(s.AuctionID));
            Assert.That(t.AuthBuyerID, Is.EqualTo(s.AuthBuyerID));
            Assert.That(t.Bitmap, Is.EqualTo(s.Bitmap));
            Assert.That(t.Category, Is.EqualTo(s.Category));
            Assert.That(t.ClaimDate, Is.EqualTo(s.ClaimDate));
            Assert.That(t.ClaimPrice, Is.EqualTo(s.ClaimPrice));
            Assert.That(t.Desc, Is.EqualTo(s.Desc));
            Assert.That(t.GroupAVSounds, Is.EqualTo(s.GroupAVSounds));
            Assert.That(t.GroupID, Is.EqualTo(s.GroupID));
            Assert.That(t.GroupPrims, Is.EqualTo(s.GroupPrims));
            Assert.That(t.IsGroupOwned, Is.EqualTo(s.IsGroupOwned));
            Assert.That(t.LandingType, Is.EqualTo(s.LandingType));
            Assert.That(t.LocalID, Is.EqualTo(s.LocalID));
            Assert.That(t.MaxPrims, Is.EqualTo(s.MaxPrims));
            Assert.That(t.MediaAutoScale, Is.EqualTo(s.MediaAutoScale));
            Assert.That(t.MediaDesc, Is.EqualTo(s.MediaDesc));
            Assert.That(t.MediaHeight, Is.EqualTo(s.MediaHeight));
            Assert.That(t.MediaID, Is.EqualTo(s.MediaID));
            Assert.That(t.MediaLoop, Is.EqualTo(s.MediaLoop));
            Assert.That(t.MediaType, Is.EqualTo(s.MediaType));
            Assert.That(t.MediaURL, Is.EqualTo(s.MediaURL));
            Assert.That(t.MediaWidth, Is.EqualTo(s.MediaWidth));
            Assert.That(t.MusicURL, Is.EqualTo(s.MusicURL));
            Assert.That(t.Name, Is.EqualTo(s.Name));
            Assert.That(t.ObscureMedia, Is.EqualTo(s.ObscureMedia));
            Assert.That(t.ObscureMusic, Is.EqualTo(s.ObscureMusic));
            Assert.That(t.OtherCleanTime, Is.EqualTo(s.OtherCleanTime));
            Assert.That(t.OtherCount, Is.EqualTo(s.OtherCount));
            Assert.That(t.OtherPrims, Is.EqualTo(s.OtherPrims));
            Assert.That(t.OwnerID, Is.EqualTo(s.OwnerID));
            Assert.That(t.OwnerPrims, Is.EqualTo(s.OwnerPrims));
            Assert.That(t.ParcelFlags, Is.EqualTo(s.ParcelFlags));
            Assert.That(t.ParcelPrimBonus, Is.EqualTo(s.ParcelPrimBonus));
            Assert.That(t.PassHours, Is.EqualTo(s.PassHours));
            Assert.That(t.PassPrice, Is.EqualTo(s.PassPrice));
            Assert.That(t.PublicCount, Is.EqualTo(s.PublicCount));
            Assert.That(t.RegionDenyAgeUnverified, Is.EqualTo(s.RegionDenyAgeUnverified));
            Assert.That(t.RegionDenyAnonymous, Is.EqualTo(s.RegionDenyAnonymous));
            Assert.That(t.RegionPushOverride, Is.EqualTo(s.RegionPushOverride));
            Assert.That(t.RentPrice, Is.EqualTo(s.RentPrice));
            Assert.That(t.RequestResult, Is.EqualTo(s.RequestResult));
            Assert.That(t.SalePrice, Is.EqualTo(s.SalePrice));
            Assert.That(t.SeeAVs, Is.EqualTo(s.SeeAVs));
            Assert.That(t.SelectedPrims, Is.EqualTo(s.SelectedPrims));
            Assert.That(t.SelfCount, Is.EqualTo(s.SelfCount));
            Assert.That(t.SequenceID, Is.EqualTo(s.SequenceID));
            Assert.That(t.SimWideMaxPrims, Is.EqualTo(s.SimWideMaxPrims));
            Assert.That(t.SimWideTotalPrims, Is.EqualTo(s.SimWideTotalPrims));
            Assert.That(t.SnapSelection, Is.EqualTo(s.SnapSelection));
            Assert.That(t.SnapshotID, Is.EqualTo(s.SnapshotID));
            Assert.That(t.Status, Is.EqualTo(s.Status));
            Assert.That(t.TotalPrims, Is.EqualTo(s.TotalPrims));
            Assert.That(t.UserLocation, Is.EqualTo(s.UserLocation));
            Assert.That(t.UserLookAt, Is.EqualTo(s.UserLookAt));
        }

        [Test]
        public void ParcelPropertiesUpdateMessage()
        {
            ParcelPropertiesUpdateMessage s = new ParcelPropertiesUpdateMessage
            {
                AnyAVSounds = true,
                AuthBuyerID = UUID.Random(),
                Category = ParcelCategory.Gaming,
                Desc = "Example Description",
                GroupAVSounds = true,
                GroupID = UUID.Random(),
                Landing = LandingType.LandingPoint,
                LocalID = 160,
                MediaAutoScale = true,
                MediaDesc = "Example Media Description",
                MediaHeight = 600,
                MediaID = UUID.Random(),
                MediaLoop = false,
                MediaType = "image/jpeg",
                MediaURL = "http://www.openmetaverse.co/test.jpeg",
                MediaWidth = 800,
                MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075",
                Name = "Example Parcel Description",
                ObscureMedia = true,
                ObscureMusic = true,
                ParcelFlags = ParcelFlags.AllowVoiceChat | ParcelFlags.ContributeWithDeed,
                PassHours = 5.5f,
                PassPrice = 100,
                SalePrice = 99,
                SeeAVs = true,
                SnapshotID = UUID.Random(),
                UserLocation = Vector3.Parse("<128,128,128>"),
                UserLookAt = Vector3.Parse("<256,256,256>")
            };

            OSDMap map = s.Serialize();

            ParcelPropertiesUpdateMessage t = new ParcelPropertiesUpdateMessage();

            t.Deserialize(map);

            Assert.That(t.AnyAVSounds, Is.EqualTo(s.AnyAVSounds));
            Assert.That(t.AuthBuyerID, Is.EqualTo(s.AuthBuyerID));
            Assert.That(t.Category, Is.EqualTo(s.Category));
            Assert.That(t.Desc, Is.EqualTo(s.Desc));
            Assert.That(t.GroupAVSounds, Is.EqualTo(s.GroupAVSounds));
            Assert.That(t.GroupID, Is.EqualTo(s.GroupID));
            Assert.That(t.Landing, Is.EqualTo(s.Landing));
            Assert.That(t.LocalID, Is.EqualTo(s.LocalID));
            Assert.That(t.MediaAutoScale, Is.EqualTo(s.MediaAutoScale));
            Assert.That(t.MediaDesc, Is.EqualTo(s.MediaDesc));
            Assert.That(t.MediaHeight, Is.EqualTo(s.MediaHeight));
            Assert.That(t.MediaID, Is.EqualTo(s.MediaID));
            Assert.That(t.MediaLoop, Is.EqualTo(s.MediaLoop));
            Assert.That(t.MediaType, Is.EqualTo(s.MediaType));
            Assert.That(t.MediaURL, Is.EqualTo(s.MediaURL));
            Assert.That(t.MediaWidth, Is.EqualTo(s.MediaWidth));
            Assert.That(t.MusicURL, Is.EqualTo(s.MusicURL));
            Assert.That(t.Name, Is.EqualTo(s.Name));
            Assert.That(t.ObscureMedia, Is.EqualTo(s.ObscureMedia));
            Assert.That(t.ObscureMusic, Is.EqualTo(s.ObscureMusic));
            Assert.That(t.ParcelFlags, Is.EqualTo(s.ParcelFlags));
            Assert.That(t.PassHours, Is.EqualTo(s.PassHours));
            Assert.That(t.PassPrice, Is.EqualTo(s.PassPrice));
            Assert.That(t.SalePrice, Is.EqualTo(s.SalePrice));
            Assert.That(t.SeeAVs, Is.EqualTo(s.SeeAVs));
            Assert.That(t.SnapshotID, Is.EqualTo(s.SnapshotID));
            Assert.That(t.UserLocation, Is.EqualTo(s.UserLocation));
            Assert.That(t.UserLookAt, Is.EqualTo(s.UserLookAt));
        }
        [Test]
        public void EnableSimulatorMessage()
        {
            EnableSimulatorMessage s = new EnableSimulatorMessage
            {
                Simulators = new EnableSimulatorMessage.SimulatorInfoBlock[2]
            };

            EnableSimulatorMessage.SimulatorInfoBlock block1 = new EnableSimulatorMessage.SimulatorInfoBlock
                {
                    IP = testIP,
                    Port = 3000,
                    RegionHandle = testHandle
                };
            s.Simulators[0] = block1;

            EnableSimulatorMessage.SimulatorInfoBlock block2 = new EnableSimulatorMessage.SimulatorInfoBlock
                {
                    IP = testIP,
                    Port = 3001,
                    RegionHandle = testHandle
                };
            s.Simulators[1] = block2;

            OSDMap map = s.Serialize();

            EnableSimulatorMessage t = new EnableSimulatorMessage();
            t.Deserialize(map);

            for (int i = 0; i < t.Simulators.Length; i++)
            {
                Assert.That(t.Simulators[i].IP, Is.EqualTo(s.Simulators[i].IP));
                Assert.That(t.Simulators[i].Port, Is.EqualTo(s.Simulators[i].Port));
                Assert.That(t.Simulators[i].RegionHandle, Is.EqualTo(s.Simulators[i].RegionHandle));
            }
        }

        [Test]
        public void RemoteParcelRequestReply()
        {
            RemoteParcelRequestReply s = new RemoteParcelRequestReply
            {
                ParcelID = UUID.Random()
            };
            OSDMap map = s.Serialize();

            RemoteParcelRequestReply t = new RemoteParcelRequestReply();
            t.Deserialize(map);

            Assert.That(t.ParcelID, Is.EqualTo(s.ParcelID));
        }

        [Test]
        public void UpdateScriptTaskMessage()
        {
            UpdateScriptTaskUpdateMessage s = new UpdateScriptTaskUpdateMessage
            {
                TaskID = UUID.Random(),
                Target = "mono",
                ScriptRunning = true,
                ItemID = UUID.Random()
            };

            OSDMap map = s.Serialize();
            UpdateScriptTaskUpdateMessage t = new UpdateScriptTaskUpdateMessage();
            t.Deserialize(map);

            Assert.That(t.ItemID, Is.EqualTo(s.ItemID));
            Assert.That(t.ScriptRunning, Is.EqualTo(s.ScriptRunning));
            Assert.That(t.Target, Is.EqualTo(s.Target));
            Assert.That(t.TaskID, Is.EqualTo(s.TaskID));
        }

        [Test]
        public void UpdateScriptAgentMessage()
        {
            UpdateScriptAgentRequestMessage s = new UpdateScriptAgentRequestMessage
            {
                ItemID = UUID.Random(),
                Target = "lsl2"
            };

            OSDMap map = s.Serialize();

            UpdateScriptAgentRequestMessage t = new UpdateScriptAgentRequestMessage();
            t.Deserialize(map);

            Assert.That(t.ItemID, Is.EqualTo(s.ItemID));
            Assert.That(t.Target, Is.EqualTo(s.Target));
        }

        [Test]
        public void SendPostcardMessage()
        {
            SendPostcardMessage s = new SendPostcardMessage
            {
                FromEmail = "contact@openmetaverse.co",
                FromName = "Jim Radford",
                GlobalPosition = Vector3.One,
                Message = "Hello, How are you today?",
                Subject = "Postcard from the edge",
                ToEmail = "test1@example.com"
            };

            OSDMap map = s.Serialize();

            SendPostcardMessage t = new SendPostcardMessage();
            t.Deserialize(map);

            Assert.That(t.FromEmail, Is.EqualTo(s.FromEmail));
            Assert.That(t.FromName, Is.EqualTo(s.FromName));
            Assert.That(t.GlobalPosition, Is.EqualTo(s.GlobalPosition));
            Assert.That(t.Message, Is.EqualTo(s.Message));
            Assert.That(t.Subject, Is.EqualTo(s.Subject));
            Assert.That(t.ToEmail, Is.EqualTo(s.ToEmail));
        }

        [Test]
        public void UpdateNotecardAgentInventoryMessage()
        {
            UpdateAgentInventoryRequestMessage s = new UpdateAgentInventoryRequestMessage
            {
                ItemID = UUID.Random()
            };

            OSDMap map = s.Serialize();

            UpdateAgentInventoryRequestMessage t = new UpdateAgentInventoryRequestMessage();
            t.Deserialize(map);

            Assert.That(t.ItemID, Is.EqualTo(s.ItemID));
        }

        [Test]
        public void LandStatReplyMessage()
        {
            LandStatReplyMessage s = new LandStatReplyMessage
            {
                ReportType = 22,
                RequestFlags = 44,
                TotalObjectCount = 2,
                ReportDataBlocks = new LandStatReplyMessage.ReportDataBlock[2]
            };

            LandStatReplyMessage.ReportDataBlock block1 = new LandStatReplyMessage.ReportDataBlock
            {
                Location = Vector3.One,
                MonoScore = 99,
                OwnerName = "Profoky Neva",
                Score = 10,
                TaskID = UUID.Random(),
                TaskLocalID = 987341,
                TaskName = "Verbal Flogging",
                TimeStamp = new DateTime(2009, 5, 23, 4, 30, 0)
            };
            s.ReportDataBlocks[0] = block1;

            LandStatReplyMessage.ReportDataBlock block2 = new LandStatReplyMessage.ReportDataBlock
            {
                Location = Vector3.One,
                MonoScore = 1,
                OwnerName = "Philip Linden",
                Score = 5,
                TaskID = UUID.Random(),
                TaskLocalID = 987342,
                TaskName = "Happy Ant",
                TimeStamp = new DateTime(2008, 4, 22, 3, 29, 55)
            };
            s.ReportDataBlocks[1] = block2;

            OSDMap map = s.Serialize();

            LandStatReplyMessage t = new LandStatReplyMessage();
            t.Deserialize(map);

            Assert.That(t.ReportType, Is.EqualTo(s.ReportType));
            Assert.That(t.RequestFlags, Is.EqualTo(s.RequestFlags));
            Assert.That(t.TotalObjectCount, Is.EqualTo(s.TotalObjectCount));

            for (int i = 0; i < t.ReportDataBlocks.Length; i++)
            {
                Assert.That(t.ReportDataBlocks[i].Location, Is.EqualTo(s.ReportDataBlocks[i].Location));
                Assert.That(t.ReportDataBlocks[i].MonoScore, Is.EqualTo(s.ReportDataBlocks[i].MonoScore));
                Assert.That(t.ReportDataBlocks[i].OwnerName, Is.EqualTo(s.ReportDataBlocks[i].OwnerName));
                Assert.That(t.ReportDataBlocks[i].Score, Is.EqualTo(s.ReportDataBlocks[i].Score));
                Assert.That(t.ReportDataBlocks[i].TaskID, Is.EqualTo(s.ReportDataBlocks[i].TaskID));
                Assert.That(t.ReportDataBlocks[i].TaskLocalID, Is.EqualTo(s.ReportDataBlocks[i].TaskLocalID));
                Assert.That(t.ReportDataBlocks[i].TaskName, Is.EqualTo(s.ReportDataBlocks[i].TaskName));
                Assert.That(t.ReportDataBlocks[i].TimeStamp, Is.EqualTo(s.ReportDataBlocks[i].TimeStamp));
            }
        }

        [Test]
        public void TelportFailedMessage()
        {
            TeleportFailedMessage s = new TeleportFailedMessage
            {
                AgentID = UUID.Random(),
                MessageKey = "Key",
                Reason = "Unable To Teleport for some unspecified reason",
                ExtraParams = string.Empty
            };

            OSDMap map = s.Serialize();

            TeleportFailedMessage t = new TeleportFailedMessage();
            t.Deserialize(map);

            Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
            Assert.That(t.ExtraParams, Is.EqualTo(s.ExtraParams));
            Assert.That(t.MessageKey, Is.EqualTo(s.MessageKey));
            Assert.That(t.Reason, Is.EqualTo(s.Reason));

        }

        [Test]
        public void UpdateAgentInformationMessage()
        {
            UpdateAgentInformationMessage s = new UpdateAgentInformationMessage
            {
                MaxAccess = "PG"
            };
            OSDMap map = s.Serialize();

            UpdateAgentInformationMessage t = new UpdateAgentInformationMessage();
            t.Deserialize(map);

            Assert.That(t.MaxAccess, Is.EqualTo(s.MaxAccess));
        }

        [Test]
        public void PlacesReplyMessage()
        {
            PlacesReplyMessage s = new PlacesReplyMessage
            {
                TransactionID = UUID.Random(),
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryDataBlocks = new PlacesReplyMessage.QueryData[2]
            };

            PlacesReplyMessage.QueryData q1 = new PlacesReplyMessage.QueryData
            {
                ActualArea = 1024,
                BillableArea = 768,
                Description = "Test Description Q1",
                Dwell = 1435.4f,
                Flags = 1 << 6,
                GlobalX = 1,
                GlobalY = 2,
                GlobalZ = 3,
                Name = "Test Name Q1",
                OwnerID = UUID.Random(),
                Price = 1,
                ProductSku = "021",
                SimName = "Hooper",
                SnapShotID = UUID.Random()
            };

            s.QueryDataBlocks[0] = q1;

            PlacesReplyMessage.QueryData q2 = new PlacesReplyMessage.QueryData
            {
                ActualArea = 512,
                BillableArea = 384,
                Description = "Test Description Q2",
                Dwell = 1,
                Flags = 1 << 4,
                GlobalX = 4,
                GlobalY = 5,
                GlobalZ = 6,
                Name = "Test Name Q2",
                OwnerID = UUID.Random(),
                Price = 2,
                ProductSku = "022",
                SimName = "Tethys",
                SnapShotID = UUID.Random()
            };

            s.QueryDataBlocks[1] = q2;

            OSDMap map = s.Serialize();

            PlacesReplyMessage t = new PlacesReplyMessage();
            t.Deserialize(map);

            Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
            Assert.That(t.TransactionID, Is.EqualTo(s.TransactionID));
            Assert.That(t.QueryID, Is.EqualTo(s.QueryID));

            for (int i = 0; i < s.QueryDataBlocks.Length; i++)
            {
                Assert.That(t.QueryDataBlocks[i].ActualArea, Is.EqualTo(s.QueryDataBlocks[i].ActualArea));
                Assert.That(t.QueryDataBlocks[i].BillableArea, Is.EqualTo(s.QueryDataBlocks[i].BillableArea));
                Assert.That(t.QueryDataBlocks[i].Description, Is.EqualTo(s.QueryDataBlocks[i].Description));
                Assert.That(t.QueryDataBlocks[i].Dwell, Is.EqualTo(s.QueryDataBlocks[i].Dwell));
                Assert.That(t.QueryDataBlocks[i].Flags, Is.EqualTo(s.QueryDataBlocks[i].Flags));
                Assert.That(t.QueryDataBlocks[i].GlobalX, Is.EqualTo(s.QueryDataBlocks[i].GlobalX));
                Assert.That(t.QueryDataBlocks[i].GlobalY, Is.EqualTo(s.QueryDataBlocks[i].GlobalY));
                Assert.That(t.QueryDataBlocks[i].GlobalZ, Is.EqualTo(s.QueryDataBlocks[i].GlobalZ));
                Assert.That(t.QueryDataBlocks[i].Name, Is.EqualTo(s.QueryDataBlocks[i].Name));
                Assert.That(t.QueryDataBlocks[i].OwnerID, Is.EqualTo(s.QueryDataBlocks[i].OwnerID));
                Assert.That(t.QueryDataBlocks[i].Price, Is.EqualTo(s.QueryDataBlocks[i].Price));
                Assert.That(t.QueryDataBlocks[i].ProductSku, Is.EqualTo(s.QueryDataBlocks[i].ProductSku));
                Assert.That(t.QueryDataBlocks[i].SimName, Is.EqualTo(s.QueryDataBlocks[i].SimName));
                Assert.That(t.QueryDataBlocks[i].SnapShotID, Is.EqualTo(s.QueryDataBlocks[i].SnapShotID));
            }
        }

        [Test]
        public void DirLandReplyMessage()
        {
            DirLandReplyMessage s = new DirLandReplyMessage
            {
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryReplies = new DirLandReplyMessage.QueryReply[2]
            };

            DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply
            {
                ActualArea = 1024,
                Auction = true,
                ForSale = true,
                Name = "For Sale Parcel Q1",
                ProductSku = "023",
                SalePrice = 2193,
                ParcelID = UUID.Random()
            };

            s.QueryReplies[0] = q1;

            DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply
            {
                ActualArea = 512,
                Auction = true,
                ForSale = true,
                Name = "For Sale Parcel Q2",
                ProductSku = "023",
                SalePrice = 22193,
                ParcelID = UUID.Random()
            };

            s.QueryReplies[1] = q2;

            OSDMap map = s.Serialize();

            DirLandReplyMessage t = new DirLandReplyMessage();
            t.Deserialize(map);

            Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
            Assert.That(t.QueryID, Is.EqualTo(s.QueryID));

            for (int i = 0; i < s.QueryReplies.Length; i++)
            {
                Assert.That(t.QueryReplies[i].ActualArea, Is.EqualTo(s.QueryReplies[i].ActualArea));
                Assert.That(t.QueryReplies[i].Auction, Is.EqualTo(s.QueryReplies[i].Auction));
                Assert.That(t.QueryReplies[i].ForSale, Is.EqualTo(s.QueryReplies[i].ForSale));
                Assert.That(t.QueryReplies[i].Name, Is.EqualTo(s.QueryReplies[i].Name));
                Assert.That(t.QueryReplies[i].ProductSku, Is.EqualTo(s.QueryReplies[i].ProductSku));
                Assert.That(t.QueryReplies[i].ParcelID, Is.EqualTo(s.QueryReplies[i].ParcelID));
                Assert.That(t.QueryReplies[i].SalePrice, Is.EqualTo(s.QueryReplies[i].SalePrice));
            }
        }
        #region Performance Testing

        private const int TEST_ITER = 100000;

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceRemoteParcelResponse()
        {
            DateTime messageTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                RemoteParcelRequestReply s = new RemoteParcelRequestReply
                {
                    ParcelID = UUID.Random()
                };
                OSDMap map = s.Serialize();

                RemoteParcelRequestReply t = new RemoteParcelRequestReply();
                t.Deserialize(map);

                Assert.That(t.ParcelID, Is.EqualTo(s.ParcelID));
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("RemoteParcelRequestReply: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);
        }


        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply()
        {

            DateTime messageTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                DirLandReplyMessage s = new DirLandReplyMessage
                {
                    AgentID = UUID.Random(),
                    QueryID = UUID.Random(),
                    QueryReplies = new DirLandReplyMessage.QueryReply[2]
                };

                DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply
                {
                    ActualArea = 1024,
                    Auction = true,
                    ForSale = true,
                    Name = "For Sale Parcel Q1",
                    ProductSku = "023",
                    SalePrice = 2193,
                    ParcelID = UUID.Random()
                };

                s.QueryReplies[0] = q1;

                DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply
                {
                    ActualArea = 512,
                    Auction = true,
                    ForSale = true,
                    Name = "For Sale Parcel Q2",
                    ProductSku = "023",
                    SalePrice = 22193,
                    ParcelID = UUID.Random()
                };

                s.QueryReplies[1] = q2;

                OSDMap map = s.Serialize();
                DirLandReplyMessage t = new DirLandReplyMessage();

                t.Deserialize(map);
                Assert.That(t.AgentID, Is.EqualTo(s.AgentID));
                Assert.That(t.QueryID, Is.EqualTo(s.QueryID));

                for (int i = 0; i < s.QueryReplies.Length; i++)
                {
                    Assert.That(t.QueryReplies[i].ActualArea, Is.EqualTo(s.QueryReplies[i].ActualArea));
                    Assert.That(t.QueryReplies[i].Auction, Is.EqualTo(s.QueryReplies[i].Auction));
                    Assert.That(t.QueryReplies[i].ForSale, Is.EqualTo(s.QueryReplies[i].ForSale));
                    Assert.That(t.QueryReplies[i].Name, Is.EqualTo(s.QueryReplies[i].Name));
                    Assert.That(t.QueryReplies[i].ProductSku, Is.EqualTo(s.QueryReplies[i].ProductSku));
                    Assert.That(t.QueryReplies[i].ParcelID, Is.EqualTo(s.QueryReplies[i].ParcelID));
                    Assert.That(t.QueryReplies[i].SalePrice, Is.EqualTo(s.QueryReplies[i].SalePrice));
                }
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("DirLandReplyMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);
        }

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply2()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DirLandReplyMessage));

            DirLandReplyMessage s = new DirLandReplyMessage
            {
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryReplies = new DirLandReplyMessage.QueryReply[2]
            };

            DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply
            {
                ActualArea = 1024,
                Auction = true,
                ForSale = true,
                Name = "For Sale Parcel Q1",
                ProductSku = "023",
                SalePrice = 2193,
                ParcelID = UUID.Random()
            };

            s.QueryReplies[0] = q1;

            DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply
            {
                ActualArea = 512,
                Auction = true,
                ForSale = true,
                Name = "For Sale Parcel Q2",
                ProductSku = "023",
                SalePrice = 22193,
                ParcelID = UUID.Random()
            };

            s.QueryReplies[1] = q2;

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            for (int i = 0; i < TEST_ITER; ++i)
            {
                MemoryStream stream = new MemoryStream();
                OSDMap map = s.Serialize();
                byte[] jsonData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(map));
                stream.Write(jsonData, 0, jsonData.Length);
                stream.Flush();
                stream.Close();
            }
            timer.Stop();
            Console.WriteLine("OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, timer.Elapsed.TotalSeconds);

            timer.Reset();
            timer.Start();
            for (int i = 0; i < TEST_ITER; ++i)
            {
                MemoryStream stream = new MemoryStream();
                xmlSerializer.Serialize(stream, s);
                stream.Flush();
                stream.Close();
            }
            timer.Stop();
            Console.WriteLine(".NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, timer.Elapsed.TotalSeconds);
        }

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceParcelProperties()
        {
            DateTime messageTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                ParcelPropertiesMessage s = new ParcelPropertiesMessage
                {
                    AABBMax = Vector3.Parse("<1,2,3>"),
                    AABBMin = Vector3.Parse("<2,3,1>"),
                    Area = 1024,
                    AuctionID = uint.MaxValue,
                    AuthBuyerID = UUID.Random(),
                    Bitmap = Utils.EmptyBytes,
                    Category = ParcelCategory.Educational,
                    ClaimDate = new DateTime(2008, 12, 25, 3, 15, 22),
                    ClaimPrice = 1000,
                    Desc = "Test Description",
                    GroupID = UUID.Random(),
                    GroupPrims = 50,
                    IsGroupOwned = false,
                    LandingType = LandingType.None,
                    LocalID = 1,
                    MaxPrims = 234,
                    MediaAutoScale = false,
                    MediaDesc = "Example Media Description",
                    MediaHeight = 480,
                    MediaID = UUID.Random(),
                    MediaLoop = false,
                    MediaType = "text/html",
                    MediaURL = "http://www.openmetaverse.co",
                    MediaWidth = 640,
                    MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075", // Yee Haw
                    Name = "Test Name",
                    ObscureMedia = false,
                    ObscureMusic = false,
                    OtherCleanTime = 5,
                    OtherCount = 200,
                    OtherPrims = 300,
                    OwnerID = UUID.Random(),
                    OwnerPrims = 0,
                    ParcelFlags = ParcelFlags.AllowDamage | ParcelFlags.AllowGroupScripts | ParcelFlags.AllowVoiceChat,
                    ParcelPrimBonus = 0f,
                    PassHours = 1.5f,
                    PassPrice = 10,
                    PublicCount = 20,
                    RegionDenyAgeUnverified = false,
                    RegionDenyAnonymous = false,
                    RegionPushOverride = true,
                    RentPrice = 0,
                    RequestResult = ParcelResult.Single,
                    SalePrice = 9999,
                    SelectedPrims = 1,
                    SelfCount = 2,
                    SequenceID = -4000,
                    SimWideMaxPrims = 937,
                    SimWideTotalPrims = 117,
                    SnapSelection = false,
                    SnapshotID = UUID.Random(),
                    Status = ParcelStatus.Leased,
                    TotalPrims = 219,
                    UserLocation = Vector3.Parse("<3,4,5>"),
                    UserLookAt = Vector3.Parse("<5,4,3>")
                };

                OSDMap map = s.Serialize();
                ParcelPropertiesMessage t = new ParcelPropertiesMessage();

                t.Deserialize(map);

                Assert.That(t.AABBMax, Is.EqualTo(s.AABBMax));
                Assert.That(t.AABBMin, Is.EqualTo(s.AABBMin));
                Assert.That(t.Area, Is.EqualTo(s.Area));
                Assert.That(t.AuctionID, Is.EqualTo(s.AuctionID));
                Assert.That(t.AuthBuyerID, Is.EqualTo(s.AuthBuyerID));
                Assert.That(t.Bitmap, Is.EqualTo(s.Bitmap));
                Assert.That(t.Category, Is.EqualTo(s.Category));
                Assert.That(t.ClaimDate, Is.EqualTo(s.ClaimDate));
                Assert.That(t.ClaimPrice, Is.EqualTo(s.ClaimPrice));
                Assert.That(t.Desc, Is.EqualTo(s.Desc));
                Assert.That(t.GroupID, Is.EqualTo(s.GroupID));
                Assert.That(t.GroupPrims, Is.EqualTo(s.GroupPrims));
                Assert.That(t.IsGroupOwned, Is.EqualTo(s.IsGroupOwned));
                Assert.That(t.LandingType, Is.EqualTo(s.LandingType));
                Assert.That(t.LocalID, Is.EqualTo(s.LocalID));
                Assert.That(t.MaxPrims, Is.EqualTo(s.MaxPrims));
                Assert.That(t.MediaAutoScale, Is.EqualTo(s.MediaAutoScale));
                Assert.That(t.MediaDesc, Is.EqualTo(s.MediaDesc));
                Assert.That(t.MediaHeight, Is.EqualTo(s.MediaHeight));
                Assert.That(t.MediaID, Is.EqualTo(s.MediaID));
                Assert.That(t.MediaLoop, Is.EqualTo(s.MediaLoop));
                Assert.That(t.MediaType, Is.EqualTo(s.MediaType));
                Assert.That(t.MediaURL, Is.EqualTo(s.MediaURL));
                Assert.That(t.MediaWidth, Is.EqualTo(s.MediaWidth));
                Assert.That(t.MusicURL, Is.EqualTo(s.MusicURL));
                Assert.That(t.Name, Is.EqualTo(s.Name));
                Assert.That(t.ObscureMedia, Is.EqualTo(s.ObscureMedia));
                Assert.That(t.ObscureMusic, Is.EqualTo(s.ObscureMusic));
                Assert.That(t.OtherCleanTime, Is.EqualTo(s.OtherCleanTime));
                Assert.That(t.OtherCount, Is.EqualTo(s.OtherCount));
                Assert.That(t.OtherPrims, Is.EqualTo(s.OtherPrims));
                Assert.That(t.OwnerID, Is.EqualTo(s.OwnerID));
                Assert.That(t.OwnerPrims, Is.EqualTo(s.OwnerPrims));
                Assert.That(t.ParcelFlags, Is.EqualTo(s.ParcelFlags));
                Assert.That(t.ParcelPrimBonus, Is.EqualTo(s.ParcelPrimBonus));
                Assert.That(t.PassHours, Is.EqualTo(s.PassHours));
                Assert.That(t.PassPrice, Is.EqualTo(s.PassPrice));
                Assert.That(t.PublicCount, Is.EqualTo(s.PublicCount));
                Assert.That(t.RegionDenyAgeUnverified, Is.EqualTo(s.RegionDenyAgeUnverified));
                Assert.That(t.RegionDenyAnonymous, Is.EqualTo(s.RegionDenyAnonymous));
                Assert.That(t.RegionPushOverride, Is.EqualTo(s.RegionPushOverride));
                Assert.That(t.RentPrice, Is.EqualTo(s.RentPrice));
                Assert.That(t.RequestResult, Is.EqualTo(s.RequestResult));
                Assert.That(t.SalePrice, Is.EqualTo(s.SalePrice));
                Assert.That(t.SelectedPrims, Is.EqualTo(s.SelectedPrims));
                Assert.That(t.SelfCount, Is.EqualTo(s.SelfCount));
                Assert.That(t.SequenceID, Is.EqualTo(s.SequenceID));
                Assert.That(t.SimWideMaxPrims, Is.EqualTo(s.SimWideMaxPrims));
                Assert.That(t.SimWideTotalPrims, Is.EqualTo(s.SimWideTotalPrims));
                Assert.That(t.SnapSelection, Is.EqualTo(s.SnapSelection));
                Assert.That(t.SnapshotID, Is.EqualTo(s.SnapshotID));
                Assert.That(t.Status, Is.EqualTo(s.Status));
                Assert.That(t.TotalPrims, Is.EqualTo(s.TotalPrims));
                Assert.That(t.UserLocation, Is.EqualTo(s.UserLocation));
                Assert.That(t.UserLookAt, Is.EqualTo(s.UserLookAt));
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("ParcelPropertiesMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);
        }

        #endregion
    }
}

