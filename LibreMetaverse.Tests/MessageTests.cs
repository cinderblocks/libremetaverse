/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2021-2024, Sjofn LLC
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
using System.Runtime.Serialization.Formatters.Binary;
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
        private readonly Uri _testUri = new Uri("https://sim3187.agni.lindenlab.com:12043/cap/6028fc44-c1e5-80a1-f902-19bde114458b");
        private readonly IPAddress _testIp = IPAddress.Parse("127.0.0.1");
        private const ulong TEST_HANDLE = 1106108697797888;

        [Test]
        public void AgentGroupDataUpdateMessage()
        {
            var s = new AgentGroupDataUpdateMessage
            {
                AgentID = UUID.Random()
            };

            var blocks = new AgentGroupDataUpdateMessage.GroupData[2];
            var g1 = new AgentGroupDataUpdateMessage.GroupData
            {
                AcceptNotices = false,
                Contribution = 1024,
                GroupID = UUID.Random(),
                GroupInsigniaID = UUID.Random(),
                GroupName = "Group Name Test 1",
                GroupPowers = GroupPowers.Accountable | GroupPowers.AllowLandmark | GroupPowers.AllowSetHome
            };

            blocks[0] = g1;

            var g2 = new AgentGroupDataUpdateMessage.GroupData
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

            var nblocks = new AgentGroupDataUpdateMessage.NewGroupData[2];

            var ng1 = new AgentGroupDataUpdateMessage.NewGroupData
                {
                    ListInProfile = false
                };
            nblocks[0] = ng1;

            var ng2 = new AgentGroupDataUpdateMessage.NewGroupData
                {
                    ListInProfile = true
                };
            nblocks[1] = ng2;

            s.NewGroupDataBlock = nblocks;

            var map = s.Serialize();

            var t = new AgentGroupDataUpdateMessage();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));

            for (var i = 0; i < t.GroupDataBlock.Length; i++)
            {
                Assert.That(s.GroupDataBlock[i].AcceptNotices, Is.EqualTo(t.GroupDataBlock[1].AcceptNotices));
                Assert.That(s.GroupDataBlock[i].Contribution, Is.EqualTo(t.GroupDataBlock[1].Contribution));
                Assert.That(s.GroupDataBlock[i].GroupID, Is.EqualTo(t.GroupDataBlock[1].GroupID));
                Assert.That(s.GroupDataBlock[i].GroupInsigniaID, Is.EqualTo(t.GroupDataBlock[1].GroupInsigniaID));
                Assert.That(s.GroupDataBlock[i].GroupName, Is.EqualTo(t.GroupDataBlock[1].GroupName));
                Assert.That(s.GroupDataBlock[i].GroupPowers, Is.EqualTo(t.GroupDataBlock[1].GroupPowers));
            }

            for (var i = 0; i < t.NewGroupDataBlock.Length; i++)
            {
                Assert.That(s.NewGroupDataBlock[i].ListInProfile, Is.EqualTo(t.NewGroupDataBlock[i].ListInProfile));
            }
        }

        [Test]
        public void TeleportFinishMessage()
        {
            var s = new TeleportFinishMessage
            {
                AgentID = UUID.Random(),
                Flags = TeleportFlags.ViaLocation | TeleportFlags.IsFlying,
                IP = _testIp,
                LocationID = 32767,
                Port = 3000,
                RegionHandle = TEST_HANDLE,
                SeedCapability = _testUri,
                SimAccess = SimAccess.Mature
            };

            var map = s.Serialize();

            var t = new TeleportFinishMessage();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.Flags, Is.EqualTo(t.Flags));
            Assert.That(s.IP, Is.EqualTo(t.IP));
            Assert.That(s.LocationID, Is.EqualTo(t.LocationID));
            Assert.That(s.Port, Is.EqualTo(t.Port));
            Assert.That(s.RegionHandle, Is.EqualTo(t.RegionHandle));
            Assert.That(s.SeedCapability, Is.EqualTo(t.SeedCapability));
            Assert.That(s.SimAccess, Is.EqualTo(t.SimAccess));
        }

        [Test]
        public void EstablishAgentCommunicationMessage()
        {
            var s = new EstablishAgentCommunicationMessage
            {
                Address = _testIp,
                AgentID = UUID.Random(),
                Port = 3000,
                SeedCapability = _testUri
            };

            var map = s.Serialize();

            var t = new EstablishAgentCommunicationMessage();
            t.Deserialize(map);

            Assert.That(s.Address, Is.EqualTo(t.Address));
            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.Port, Is.EqualTo(t.Port));
            Assert.That(s.SeedCapability, Is.EqualTo(t.SeedCapability));
        }

        [Test]
        public void ParcelObjectOwnersMessage()
        {
            var s = new ParcelObjectOwnersReplyMessage
            {
                PrimOwnersBlock = new ParcelObjectOwnersReplyMessage.PrimOwner[2]
            };

            var obj = new ParcelObjectOwnersReplyMessage.PrimOwner
                {
                    OwnerID = UUID.Random(),
                    Count = 10,
                    IsGroupOwned = true,
                    OnlineStatus = false,
                    TimeStamp = new DateTime(2010, 4, 13, 7, 19, 43)
                };
            s.PrimOwnersBlock[0] = obj;

            var obj1 = new ParcelObjectOwnersReplyMessage.PrimOwner
                {
                    OwnerID = UUID.Random(),
                    Count = 0,
                    IsGroupOwned = false,
                    OnlineStatus = false,
                    TimeStamp = new DateTime(1991, 1, 31, 3, 13, 31)
                };
            s.PrimOwnersBlock[1] = obj1;

            var map = s.Serialize();

            var t = new ParcelObjectOwnersReplyMessage();
            t.Deserialize(map);

            for (var i = 0; i < t.PrimOwnersBlock.Length; i++)
            {
                Assert.That(s.PrimOwnersBlock[i].Count, Is.EqualTo(t.PrimOwnersBlock[i].Count));
                Assert.That(s.PrimOwnersBlock[i].IsGroupOwned, Is.EqualTo(t.PrimOwnersBlock[i].IsGroupOwned));
                Assert.That(s.PrimOwnersBlock[i].OnlineStatus, Is.EqualTo(t.PrimOwnersBlock[i].OnlineStatus));
                Assert.That(s.PrimOwnersBlock[i].OwnerID, Is.EqualTo(t.PrimOwnersBlock[i].OwnerID));
                Assert.That(s.PrimOwnersBlock[i].TimeStamp, Is.EqualTo(t.PrimOwnersBlock[i].TimeStamp));
            }
        }

        [Test]
        public void ChatterBoxInvitationMessage()
        {
            var s = new ChatterBoxInvitationMessage
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

            var map = s.Serialize();

            var t = new ChatterBoxInvitationMessage();
            t.Deserialize(map);

            Assert.That(s.BinaryBucket, Is.EqualTo(t.BinaryBucket));
            Assert.That(s.Dialog, Is.EqualTo(t.Dialog));
            Assert.That(s.FromAgentID, Is.EqualTo(t.FromAgentID));
            Assert.That(s.FromAgentName, Is.EqualTo(t.FromAgentName));
            Assert.That(s.GroupIM, Is.EqualTo(t.GroupIM));
            Assert.That(s.IMSessionID, Is.EqualTo(t.IMSessionID));
            Assert.That(s.Message, Is.EqualTo(t.Message));
            Assert.That(s.Offline, Is.EqualTo(t.Offline));
            Assert.That(s.ParentEstateID, Is.EqualTo(t.ParentEstateID));
            Assert.That(s.Position, Is.EqualTo(t.Position));
            Assert.That(s.RegionID, Is.EqualTo(t.RegionID));
            Assert.That(s.Timestamp, Is.EqualTo(t.Timestamp));
            Assert.That(s.ToAgentID, Is.EqualTo(t.ToAgentID));
        }

        [Test]
        public void ChatterboxSessionEventReplyMessage()
        {
            var s = new ChatterboxSessionEventReplyMessage
            {
                SessionID = UUID.Random(),
                Success = true
            };

            var map = s.Serialize();

            var t = new ChatterboxSessionEventReplyMessage();
            t.Deserialize(map);

            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
            Assert.That(s.Success, Is.EqualTo(t.Success));
        }

        [Test]
        public void ChatterBoxSessionStartReplyMessage()
        {
            var s = new ChatterBoxSessionStartReplyMessage
            {
                ModeratedVoice = true,
                SessionID = UUID.Random(),
                SessionName = "Test Session",
                Success = true,
                TempSessionID = UUID.Random(),
                Type = 1,
                VoiceEnabled = true
            };

            var map = s.Serialize();

            var t = new ChatterBoxSessionStartReplyMessage();
            t.Deserialize(map);

            Assert.That(s.ModeratedVoice, Is.EqualTo(t.ModeratedVoice));
            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
            Assert.That(s.SessionName, Is.EqualTo(t.SessionName));
            Assert.That(s.Success, Is.EqualTo(t.Success));
            Assert.That(s.TempSessionID, Is.EqualTo(t.TempSessionID));
            Assert.That(s.Type, Is.EqualTo(t.Type));
            Assert.That(s.VoiceEnabled, Is.EqualTo(t.VoiceEnabled));
        }

        [Test]
        public void ChatterBoxSessionAgentListUpdatesMessage()
        {
            var s = new ChatterBoxSessionAgentListUpdatesMessage
                {
                    SessionID = UUID.Random(),
                    Updates = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock[1]
                };

            var block1 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock
                {
                    AgentID = UUID.Random(),
                    CanVoiceChat = true,
                    IsModerator = true,
                    MuteText = true,
                    MuteVoice = true,
                    Transition = "ENTER"
                };

            var block2 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock
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

            var map = s.Serialize();

            var t = new ChatterBoxSessionAgentListUpdatesMessage();
            t.Deserialize(map);

            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
            for (var i = 0; i < t.Updates.Length; i++)
            {
                Assert.That(s.Updates[i].AgentID, Is.EqualTo(t.Updates[i].AgentID));
                Assert.That(s.Updates[i].CanVoiceChat, Is.EqualTo(t.Updates[i].CanVoiceChat));
                Assert.That(s.Updates[i].IsModerator, Is.EqualTo(t.Updates[i].IsModerator));
                Assert.That(s.Updates[i].MuteText, Is.EqualTo(t.Updates[i].MuteText));
                Assert.That(s.Updates[i].MuteVoice, Is.EqualTo(t.Updates[i].MuteVoice));
                Assert.That(s.Updates[i].Transition, Is.EqualTo(t.Updates[i].Transition));
            }
        }

        [Test]
        public void ViewerStatsMessage()
        {
            var s = new ViewerStatsMessage
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

            var map = s.Serialize();
            var t = new ViewerStatsMessage();
            t.Deserialize(map);

            Assert.That(s.AgentFPS, Is.EqualTo(t.AgentFPS));
            Assert.That(s.AgentsInView, Is.EqualTo(t.AgentsInView));
            Assert.That(s.SystemCPU, Is.EqualTo(t.SystemCPU));
            Assert.That(s.StatsDropped, Is.EqualTo(t.StatsDropped));
            Assert.That(s.StatsFailedResends, Is.EqualTo(t.StatsFailedResends));
            Assert.That(s.SystemGPU, Is.EqualTo(t.SystemGPU));
            Assert.That(s.SystemGPUClass, Is.EqualTo(t.SystemGPUClass));
            Assert.That(s.SystemGPUVendor, Is.EqualTo(t.SystemGPUVendor));
            Assert.That(s.SystemGPUVersion, Is.EqualTo(t.SystemGPUVersion));
            Assert.That(s.InCompressedPackets, Is.EqualTo(t.InCompressedPackets));
            Assert.That(s.InKbytes, Is.EqualTo(t.InKbytes));
            Assert.That(s.InPackets, Is.EqualTo(t.InPackets));
            Assert.That(s.InSavings, Is.EqualTo(t.InSavings));
            Assert.That(s.MiscInt1, Is.EqualTo(t.MiscInt1));
            Assert.That(s.MiscInt2, Is.EqualTo(t.MiscInt2));
            Assert.That(s.FailuresInvalid, Is.EqualTo(t.FailuresInvalid));
            Assert.That(s.AgentLanguage, Is.EqualTo(t.AgentLanguage));
            Assert.That(s.AgentMemoryUsed, Is.EqualTo(t.AgentMemoryUsed));
            Assert.That(s.MetersTraveled, Is.EqualTo(t.MetersTraveled));
            Assert.That(s.object_kbytes, Is.EqualTo(t.object_kbytes));
            Assert.That(s.FailuresOffCircuit, Is.EqualTo(t.FailuresOffCircuit));
            Assert.That(s.SystemOS, Is.EqualTo(t.SystemOS));
            Assert.That(s.OutCompressedPackets, Is.EqualTo(t.OutCompressedPackets));
            Assert.That(s.OutKbytes, Is.EqualTo(t.OutKbytes));
            Assert.That(s.OutPackets, Is.EqualTo(t.OutPackets));
            Assert.That(s.OutSavings, Is.EqualTo(t.OutSavings));
            Assert.That(s.AgentPing, Is.EqualTo(t.AgentPing));
            Assert.That(s.SystemInstalledRam, Is.EqualTo(t.SystemInstalledRam));
            Assert.That(s.RegionsVisited, Is.EqualTo(t.RegionsVisited));
            Assert.That(s.FailuresResent, Is.EqualTo(t.FailuresResent));
            Assert.That(s.AgentRuntime, Is.EqualTo(t.AgentRuntime));
            Assert.That(s.FailuresSendPacket, Is.EqualTo(t.FailuresSendPacket));
            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
            Assert.That(s.SimulatorFPS, Is.EqualTo(t.SimulatorFPS));
            Assert.That(s.AgentStartTime, Is.EqualTo(t.AgentStartTime));
            Assert.That(s.MiscString1, Is.EqualTo(t.MiscString1));
            Assert.That(s.texture_kbytes, Is.EqualTo(t.texture_kbytes));
            Assert.That(s.AgentVersion, Is.EqualTo(t.AgentVersion));
            Assert.That(s.MiscVersion, Is.EqualTo(t.MiscVersion));
            Assert.That(s.VertexBuffersEnabled, Is.EqualTo(t.VertexBuffersEnabled));
            Assert.That(s.world_kbytes, Is.EqualTo(t.world_kbytes));


        }

        [Test]
        public void ParcelVoiceInfoRequestMessage()
        {
            var s = new ParcelVoiceInfoRequestMessage
            {
                SipChannelUri = _testUri,
                ParcelID = 1,
                RegionName = "Hooper"
            };

            var map = s.Serialize();

            var t = new ParcelVoiceInfoRequestMessage();
            t.Deserialize(map);

            Assert.That(s.SipChannelUri, Is.EqualTo(t.SipChannelUri));
            Assert.That(s.ParcelID, Is.EqualTo(t.ParcelID));
            Assert.That(s.RegionName, Is.EqualTo(t.RegionName));
        }

        [Test]
        public void ScriptRunningReplyMessage()
        {
            var s = new ScriptRunningReplyMessage
            {
                ItemID = UUID.Random(),
                Mono = true,
                Running = true,
                ObjectID = UUID.Random()
            };

            var map = s.Serialize();

            var t = new ScriptRunningReplyMessage();
            t.Deserialize(map);

            Assert.That(s.ItemID, Is.EqualTo(t.ItemID));
            Assert.That(s.Mono, Is.EqualTo(t.Mono));
            Assert.That(s.ObjectID, Is.EqualTo(t.ObjectID));
            Assert.That(s.Running, Is.EqualTo(t.Running));

        }

        [Test]
        public void MapLayerMessage()
        {

            var s = new MapLayerReplyVariant
            {
                Flags = 1
            };

            var blocks = new MapLayerReplyVariant.LayerData[2];

            var block = new MapLayerReplyVariant.LayerData
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

            var map = s.Serialize();

            var t = new MapLayerReplyVariant();

            t.Deserialize(map);

            Assert.That(s.Flags, Is.EqualTo(t.Flags));


            for (var i = 0; i < s.LayerDataBlocks.Length; i++)
            {
                Assert.That(s.LayerDataBlocks[i].ImageID, Is.EqualTo(t.LayerDataBlocks[i].ImageID));
                Assert.That(s.LayerDataBlocks[i].Top, Is.EqualTo(t.LayerDataBlocks[i].Top));
                Assert.That(s.LayerDataBlocks[i].Left, Is.EqualTo(t.LayerDataBlocks[i].Left));
                Assert.That(s.LayerDataBlocks[i].Right, Is.EqualTo(t.LayerDataBlocks[i].Right));
                Assert.That(s.LayerDataBlocks[i].Bottom, Is.EqualTo(t.LayerDataBlocks[i].Bottom));
            }
        }

        [Test] // VARIANT A
        public void ChatSessionRequestStartConference()
        {
            var s = new ChatSessionRequestStartConference
            {
                SessionID = UUID.Random(),
                AgentsBlock = new UUID[2]
            };
            s.AgentsBlock[0] = UUID.Random();
            s.AgentsBlock[0] = UUID.Random();

            var map = s.Serialize();

            var t = new ChatSessionRequestStartConference();
            t.Deserialize(map);

            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
            Assert.That(s.Method, Is.EqualTo(t.Method));
            for (var i = 0; i < t.AgentsBlock.Length; i++)
            {
                Assert.That(s.AgentsBlock[i], Is.EqualTo(t.AgentsBlock[i]));
            }
        }

        [Test]
        public void ChatSessionRequestMuteUpdate()
        {
            var s = new ChatSessionRequestMuteUpdate
            {
                AgentID = UUID.Random(),
                RequestKey = "text",
                RequestValue = true,
                SessionID = UUID.Random()
            };

            var map = s.Serialize();

            var t = new ChatSessionRequestMuteUpdate();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.Method, Is.EqualTo(t.Method));
            Assert.That(s.RequestKey, Is.EqualTo(t.RequestKey));
            Assert.That(s.RequestValue, Is.EqualTo(t.RequestValue));
            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
        }

        [Test]
        public void ChatSessionAcceptInvitation()
        {
            var s = new ChatSessionAcceptInvitation
            {
                SessionID = UUID.Random()
            };

            var map = s.Serialize();

            var t = new ChatSessionAcceptInvitation();
            t.Deserialize(map);

            Assert.That(s.Method, Is.EqualTo(t.Method));
            Assert.That(s.SessionID, Is.EqualTo(t.SessionID));
        }

        [Test]
        public void RequiredVoiceVersionMessage()
        {
            var s = new RequiredVoiceVersionMessage
            {
                MajorVersion = 1,
                MinorVersion = 0,
                RegionName = "Hooper"
            };

            var map = s.Serialize();

            var t = new RequiredVoiceVersionMessage();
            t.Deserialize(map);

            Assert.That(s.MajorVersion, Is.EqualTo(t.MajorVersion));
            Assert.That(s.MinorVersion, Is.EqualTo(t.MinorVersion));
            Assert.That(s.RegionName, Is.EqualTo(t.RegionName));
        }

        [Test]
        public void CopyInventoryFromNotecardMessage()
        {
            var s = new CopyInventoryFromNotecardMessage
            {
                CallbackID = 1,
                FolderID = UUID.Random(),
                ItemID = UUID.Random(),
                NotecardID = UUID.Random(),
                ObjectID = UUID.Random()
            };

            var map = s.Serialize();

            var t = new CopyInventoryFromNotecardMessage();
            t.Deserialize(map);

            Assert.That(s.CallbackID, Is.EqualTo(t.CallbackID));
            Assert.That(s.FolderID, Is.EqualTo(t.FolderID));
            Assert.That(s.ItemID, Is.EqualTo(t.ItemID));
            Assert.That(s.NotecardID, Is.EqualTo(t.NotecardID));
            Assert.That(s.ObjectID, Is.EqualTo(t.ObjectID));
        }

        [Test]
        public void ProvisionVoiceAccountRequestMessage()
        {
            var s = new ProvisionVoiceAccountRequestMessage
            {
                Username = "username",
                Password = "password"
            };

            var map = s.Serialize();

            var t = new ProvisionVoiceAccountRequestMessage();
            t.Deserialize(map);

            Assert.That(s.Password, Is.EqualTo(t.Password));
            Assert.That(s.Username, Is.EqualTo(t.Username));
        }

        [Test]
        public void UpdateAgentLanguageMessage()
        {
            var s = new UpdateAgentLanguageMessage
            {
                Language = "en",
                LanguagePublic = false
            };

            var map = s.Serialize();

            var t = new UpdateAgentLanguageMessage();
            t.Deserialize(map);

            Assert.That(s.Language, Is.EqualTo(t.Language));
            Assert.That(s.LanguagePublic, Is.EqualTo(t.LanguagePublic));

        }

        [Test]
        public void ParcelPropertiesMessage()
        {
            var s = new ParcelPropertiesMessage
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

            var map = s.Serialize();
            var t = new ParcelPropertiesMessage();

            t.Deserialize(map);

            Assert.That(s.AABBMax, Is.EqualTo(t.AABBMax));
            Assert.That(s.AABBMin, Is.EqualTo(t.AABBMin));
            Assert.That(s.AnyAVSounds, Is.EqualTo(t.AnyAVSounds));
            Assert.That(s.Area, Is.EqualTo(t.Area));
            Assert.That(s.AuctionID, Is.EqualTo(t.AuctionID));
            Assert.That(s.AuthBuyerID, Is.EqualTo(t.AuthBuyerID));
            Assert.That(s.Bitmap, Is.EqualTo(t.Bitmap));
            Assert.That(s.Category, Is.EqualTo(t.Category));
            Assert.That(s.ClaimDate, Is.EqualTo(t.ClaimDate));
            Assert.That(s.ClaimPrice, Is.EqualTo(t.ClaimPrice));
            Assert.That(s.Desc, Is.EqualTo(t.Desc));
            Assert.That(s.GroupAVSounds, Is.EqualTo(t.GroupAVSounds));
            Assert.That(s.GroupID, Is.EqualTo(t.GroupID));
            Assert.That(s.GroupPrims, Is.EqualTo(t.GroupPrims));
            Assert.That(s.IsGroupOwned, Is.EqualTo(t.IsGroupOwned));
            Assert.That(s.LandingType, Is.EqualTo(t.LandingType));
            Assert.That(s.LocalID, Is.EqualTo(t.LocalID));
            Assert.That(s.MaxPrims, Is.EqualTo(t.MaxPrims));
            Assert.That(s.MediaAutoScale, Is.EqualTo(t.MediaAutoScale));
            Assert.That(s.MediaDesc, Is.EqualTo(t.MediaDesc));
            Assert.That(s.MediaHeight, Is.EqualTo(t.MediaHeight));
            Assert.That(s.MediaID, Is.EqualTo(t.MediaID));
            Assert.That(s.MediaLoop, Is.EqualTo(t.MediaLoop));
            Assert.That(s.MediaType, Is.EqualTo(t.MediaType));
            Assert.That(s.MediaURL, Is.EqualTo(t.MediaURL));
            Assert.That(s.MediaWidth, Is.EqualTo(t.MediaWidth));
            Assert.That(s.MusicURL, Is.EqualTo(t.MusicURL));
            Assert.That(s.Name, Is.EqualTo(t.Name));
            Assert.That(s.ObscureMedia, Is.EqualTo(t.ObscureMedia));
            Assert.That(s.ObscureMusic, Is.EqualTo(t.ObscureMusic));
            Assert.That(s.OtherCleanTime, Is.EqualTo(t.OtherCleanTime));
            Assert.That(s.OtherCount, Is.EqualTo(t.OtherCount));
            Assert.That(s.OtherPrims, Is.EqualTo(t.OtherPrims));
            Assert.That(s.OwnerID, Is.EqualTo(t.OwnerID));
            Assert.That(s.OwnerPrims, Is.EqualTo(t.OwnerPrims));
            Assert.That(s.ParcelFlags, Is.EqualTo(t.ParcelFlags));
            Assert.That(s.ParcelPrimBonus, Is.EqualTo(t.ParcelPrimBonus));
            Assert.That(s.PassHours, Is.EqualTo(t.PassHours));
            Assert.That(s.PassPrice, Is.EqualTo(t.PassPrice));
            Assert.That(s.PublicCount, Is.EqualTo(t.PublicCount));
            Assert.That(s.RegionDenyAgeUnverified, Is.EqualTo(t.RegionDenyAgeUnverified));
            Assert.That(s.RegionDenyAnonymous, Is.EqualTo(t.RegionDenyAnonymous));
            Assert.That(s.RegionPushOverride, Is.EqualTo(t.RegionPushOverride));
            Assert.That(s.RentPrice, Is.EqualTo(t.RentPrice));
            Assert.That(s.RequestResult, Is.EqualTo(t.RequestResult));
            Assert.That(s.SalePrice, Is.EqualTo(t.SalePrice));
            Assert.That(s.SeeAVs, Is.EqualTo(t.SeeAVs));
            Assert.That(s.SelectedPrims, Is.EqualTo(t.SelectedPrims));
            Assert.That(s.SelfCount, Is.EqualTo(t.SelfCount));
            Assert.That(s.SequenceID, Is.EqualTo(t.SequenceID));
            Assert.That(s.SimWideMaxPrims, Is.EqualTo(t.SimWideMaxPrims));
            Assert.That(s.SimWideTotalPrims, Is.EqualTo(t.SimWideTotalPrims));
            Assert.That(s.SnapSelection, Is.EqualTo(t.SnapSelection));
            Assert.That(s.SnapshotID, Is.EqualTo(t.SnapshotID));
            Assert.That(s.Status, Is.EqualTo(t.Status));
            Assert.That(s.TotalPrims, Is.EqualTo(t.TotalPrims));
            Assert.That(s.UserLocation, Is.EqualTo(t.UserLocation));
            Assert.That(s.UserLookAt, Is.EqualTo(t.UserLookAt));
        }

        [Test]
        public void ParcelPropertiesUpdateMessage()
        {
            var s = new ParcelPropertiesUpdateMessage
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

            var map = s.Serialize();

            var t = new ParcelPropertiesUpdateMessage();

            t.Deserialize(map);

            Assert.That(s.AnyAVSounds, Is.EqualTo(t.AnyAVSounds));
            Assert.That(s.AuthBuyerID, Is.EqualTo(t.AuthBuyerID));
            Assert.That(s.Category, Is.EqualTo(t.Category));
            Assert.That(s.Desc, Is.EqualTo(t.Desc));
            Assert.That(s.GroupAVSounds, Is.EqualTo(t.GroupAVSounds));
            Assert.That(s.GroupID, Is.EqualTo(t.GroupID));
            Assert.That(s.Landing, Is.EqualTo(t.Landing));
            Assert.That(s.LocalID, Is.EqualTo(t.LocalID));
            Assert.That(s.MediaAutoScale, Is.EqualTo(t.MediaAutoScale));
            Assert.That(s.MediaDesc, Is.EqualTo(t.MediaDesc));
            Assert.That(s.MediaHeight, Is.EqualTo(t.MediaHeight));
            Assert.That(s.MediaID, Is.EqualTo(t.MediaID));
            Assert.That(s.MediaLoop, Is.EqualTo(t.MediaLoop));
            Assert.That(s.MediaType, Is.EqualTo(t.MediaType));
            Assert.That(s.MediaURL, Is.EqualTo(t.MediaURL));
            Assert.That(s.MediaWidth, Is.EqualTo(t.MediaWidth));
            Assert.That(s.MusicURL, Is.EqualTo(t.MusicURL));
            Assert.That(s.Name, Is.EqualTo(t.Name));
            Assert.That(s.ObscureMedia, Is.EqualTo(t.ObscureMedia));
            Assert.That(s.ObscureMusic, Is.EqualTo(t.ObscureMusic));
            Assert.That(s.ParcelFlags, Is.EqualTo(t.ParcelFlags));
            Assert.That(s.PassHours, Is.EqualTo(t.PassHours));
            Assert.That(s.PassPrice, Is.EqualTo(t.PassPrice));
            Assert.That(s.SalePrice, Is.EqualTo(t.SalePrice));
            Assert.That(s.SeeAVs, Is.EqualTo(t.SeeAVs));
            Assert.That(s.SnapshotID, Is.EqualTo(t.SnapshotID));
            Assert.That(s.UserLocation, Is.EqualTo(t.UserLocation));
            Assert.That(s.UserLookAt, Is.EqualTo(t.UserLookAt));
        }
        [Test]
        public void EnableSimulatorMessage()
        {
            var s = new EnableSimulatorMessage
            {
                Simulators = new EnableSimulatorMessage.SimulatorInfoBlock[2]
            };

            var block1 = new EnableSimulatorMessage.SimulatorInfoBlock
                {
                    IP = _testIp,
                    Port = 3000,
                    RegionHandle = TEST_HANDLE
                };
            s.Simulators[0] = block1;

            var block2 = new EnableSimulatorMessage.SimulatorInfoBlock
                {
                    IP = _testIp,
                    Port = 3001,
                    RegionHandle = TEST_HANDLE
                };
            s.Simulators[1] = block2;

            var map = s.Serialize();

            var t = new EnableSimulatorMessage();
            t.Deserialize(map);

            for (var i = 0; i < t.Simulators.Length; i++)
            {
                Assert.That(s.Simulators[i].IP, Is.EqualTo(t.Simulators[i].IP));
                Assert.That(s.Simulators[i].Port, Is.EqualTo(t.Simulators[i].Port));
                Assert.That(s.Simulators[i].RegionHandle, Is.EqualTo(t.Simulators[i].RegionHandle));
            }
        }

        [Test]
        public void RemoteParcelRequestReply()
        {
            var s = new RemoteParcelRequestReply
            {
                ParcelID = UUID.Random()
            };
            var map = s.Serialize();

            var t = new RemoteParcelRequestReply();
            t.Deserialize(map);

            Assert.That(s.ParcelID, Is.EqualTo(t.ParcelID));
        }

        [Test]
        public void UpdateScriptTaskMessage()
        {
            var s = new UpdateScriptTaskUpdateMessage
            {
                TaskID = UUID.Random(),
                Target = "mono",
                ScriptRunning = true,
                ItemID = UUID.Random()
            };

            var map = s.Serialize();
            var t = new UpdateScriptTaskUpdateMessage();
            t.Deserialize(map);

            Assert.That(s.ItemID, Is.EqualTo(t.ItemID));
            Assert.That(s.ScriptRunning, Is.EqualTo(t.ScriptRunning));
            Assert.That(s.Target, Is.EqualTo(t.Target));
            Assert.That(s.TaskID, Is.EqualTo(t.TaskID));
        }

        [Test]
        public void UpdateScriptAgentMessage()
        {
            var s = new UpdateScriptAgentRequestMessage
            {
                ItemID = UUID.Random(),
                Target = "lsl2"
            };

            var map = s.Serialize();

            var t = new UpdateScriptAgentRequestMessage();
            t.Deserialize(map);

            Assert.That(s.ItemID, Is.EqualTo(t.ItemID));
            Assert.That(s.Target, Is.EqualTo(t.Target));
        }

        [Test]
        public void SendPostcardMessage()
        {
            var s = new SendPostcardMessage
            {
                FromEmail = "contact@openmetaverse.co",
                FromName = "Jim Radford",
                GlobalPosition = Vector3.One,
                Message = "Hello, How are you today?",
                Subject = "Postcard from the edge",
                ToEmail = "test1@example.com"
            };

            var map = s.Serialize();

            var t = new SendPostcardMessage();
            t.Deserialize(map);

            Assert.That(s.FromEmail, Is.EqualTo(t.FromEmail));
            Assert.That(s.FromName, Is.EqualTo(t.FromName));
            Assert.That(s.GlobalPosition, Is.EqualTo(t.GlobalPosition));
            Assert.That(s.Message, Is.EqualTo(t.Message));
            Assert.That(s.Subject, Is.EqualTo(t.Subject));
            Assert.That(s.ToEmail, Is.EqualTo(t.ToEmail));
        }

        [Test]
        public void UpdateNotecardAgentInventoryMessage()
        {
            var s = new UpdateAgentInventoryRequestMessage
            {
                ItemID = UUID.Random()
            };

            var map = s.Serialize();

            var t = new UpdateAgentInventoryRequestMessage();
            t.Deserialize(map);

            Assert.That(s.ItemID, Is.EqualTo(t.ItemID));
        }

        [Test]
        public void LandStatReplyMessage()
        {
            var s = new LandStatReplyMessage
            {
                ReportType = 22,
                RequestFlags = 44,
                TotalObjectCount = 2,
                ReportDataBlocks = new LandStatReplyMessage.ReportDataBlock[2]
            };

            var block1 = new LandStatReplyMessage.ReportDataBlock
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

            var block2 = new LandStatReplyMessage.ReportDataBlock
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

            var map = s.Serialize();

            var t = new LandStatReplyMessage();
            t.Deserialize(map);

            Assert.That(s.ReportType, Is.EqualTo(t.ReportType));
            Assert.That(s.RequestFlags, Is.EqualTo(t.RequestFlags));
            Assert.That(s.TotalObjectCount, Is.EqualTo(t.TotalObjectCount));

            for (var i = 0; i < t.ReportDataBlocks.Length; i++)
            {
                Assert.That(s.ReportDataBlocks[i].Location, Is.EqualTo(t.ReportDataBlocks[i].Location));
                Assert.That(s.ReportDataBlocks[i].MonoScore, Is.EqualTo(t.ReportDataBlocks[i].MonoScore));
                Assert.That(s.ReportDataBlocks[i].OwnerName, Is.EqualTo(t.ReportDataBlocks[i].OwnerName));
                Assert.That(s.ReportDataBlocks[i].Score, Is.EqualTo(t.ReportDataBlocks[i].Score));
                Assert.That(s.ReportDataBlocks[i].TaskID, Is.EqualTo(t.ReportDataBlocks[i].TaskID));
                Assert.That(s.ReportDataBlocks[i].TaskLocalID, Is.EqualTo(t.ReportDataBlocks[i].TaskLocalID));
                Assert.That(s.ReportDataBlocks[i].TaskName, Is.EqualTo(t.ReportDataBlocks[i].TaskName));
                Assert.That(s.ReportDataBlocks[i].TimeStamp, Is.EqualTo(t.ReportDataBlocks[i].TimeStamp));
            }
        }

        [Test]
        public void TelportFailedMessage()
        {
            var s = new TeleportFailedMessage
            {
                AgentID = UUID.Random(),
                MessageKey = "Key",
                Reason = "Unable To Teleport for some unspecified reason",
                ExtraParams = string.Empty
            };

            var map = s.Serialize();

            var t = new TeleportFailedMessage();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.ExtraParams, Is.EqualTo(t.ExtraParams));
            Assert.That(s.MessageKey, Is.EqualTo(t.MessageKey));
            Assert.That(s.Reason, Is.EqualTo(t.Reason));

        }

        [Test]
        public void UpdateAgentInformationMessage()
        {
            var s = new UpdateAgentInformationMessage
            {
                MaxAccess = "PG"
            };
            var map = s.Serialize();

            var t = new UpdateAgentInformationMessage();
            t.Deserialize(map);

            Assert.That(s.MaxAccess, Is.EqualTo(t.MaxAccess));
        }

        [Test]
        public void PlacesReplyMessage()
        {
            var s = new PlacesReplyMessage
            {
                TransactionID = UUID.Random(),
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryDataBlocks = new PlacesReplyMessage.QueryData[2]
            };

            var q1 = new PlacesReplyMessage.QueryData
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

            var q2 = new PlacesReplyMessage.QueryData
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

            var map = s.Serialize();

            var t = new PlacesReplyMessage();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.TransactionID, Is.EqualTo(t.TransactionID));
            Assert.That(s.QueryID, Is.EqualTo(t.QueryID));

            for (var i = 0; i < s.QueryDataBlocks.Length; i++)
            {
                Assert.That(s.QueryDataBlocks[i].ActualArea, Is.EqualTo(t.QueryDataBlocks[i].ActualArea));
                Assert.That(s.QueryDataBlocks[i].BillableArea, Is.EqualTo(t.QueryDataBlocks[i].BillableArea));
                Assert.That(s.QueryDataBlocks[i].Description, Is.EqualTo(t.QueryDataBlocks[i].Description));
                Assert.That(s.QueryDataBlocks[i].Dwell, Is.EqualTo(t.QueryDataBlocks[i].Dwell));
                Assert.That(s.QueryDataBlocks[i].Flags, Is.EqualTo(t.QueryDataBlocks[i].Flags));
                Assert.That(s.QueryDataBlocks[i].GlobalX, Is.EqualTo(t.QueryDataBlocks[i].GlobalX));
                Assert.That(s.QueryDataBlocks[i].GlobalY, Is.EqualTo(t.QueryDataBlocks[i].GlobalY));
                Assert.That(s.QueryDataBlocks[i].GlobalZ, Is.EqualTo(t.QueryDataBlocks[i].GlobalZ));
                Assert.That(s.QueryDataBlocks[i].Name, Is.EqualTo(t.QueryDataBlocks[i].Name));
                Assert.That(s.QueryDataBlocks[i].OwnerID, Is.EqualTo(t.QueryDataBlocks[i].OwnerID));
                Assert.That(s.QueryDataBlocks[i].Price, Is.EqualTo(t.QueryDataBlocks[i].Price));
                Assert.That(s.QueryDataBlocks[i].ProductSku, Is.EqualTo(t.QueryDataBlocks[i].ProductSku));
                Assert.That(s.QueryDataBlocks[i].SimName, Is.EqualTo(t.QueryDataBlocks[i].SimName));
                Assert.That(s.QueryDataBlocks[i].SnapShotID, Is.EqualTo(t.QueryDataBlocks[i].SnapShotID));
            }
        }

        [Test]
        public void DirLandReplyMessage()
        {
            var s = new DirLandReplyMessage
            {
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryReplies = new DirLandReplyMessage.QueryReply[2]
            };

            var q1 = new DirLandReplyMessage.QueryReply
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

            var q2 = new DirLandReplyMessage.QueryReply
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

            var map = s.Serialize();

            var t = new DirLandReplyMessage();
            t.Deserialize(map);

            Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
            Assert.That(s.QueryID, Is.EqualTo(t.QueryID));

            for (var i = 0; i < s.QueryReplies.Length; i++)
            {
                Assert.That(s.QueryReplies[i].ActualArea, Is.EqualTo(t.QueryReplies[i].ActualArea));
                Assert.That(s.QueryReplies[i].Auction, Is.EqualTo(t.QueryReplies[i].Auction));
                Assert.That(s.QueryReplies[i].ForSale, Is.EqualTo(t.QueryReplies[i].ForSale));
                Assert.That(s.QueryReplies[i].Name, Is.EqualTo(t.QueryReplies[i].Name));
                Assert.That(s.QueryReplies[i].ProductSku, Is.EqualTo(t.QueryReplies[i].ProductSku));
                Assert.That(s.QueryReplies[i].ParcelID, Is.EqualTo(t.QueryReplies[i].ParcelID));
                Assert.That(s.QueryReplies[i].SalePrice, Is.EqualTo(t.QueryReplies[i].SalePrice));
            }
        }
        #region Performance Testing

        private const int TEST_ITER = 100000;

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceRemoteParcelResponse()
        {
            var messageTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {
                var s = new RemoteParcelRequestReply
                {
                    ParcelID = UUID.Random()
                };
                var map = s.Serialize();

                var t = new RemoteParcelRequestReply();
                t.Deserialize(map);

                Assert.That(s.ParcelID, Is.EqualTo(t.ParcelID));
            }
            var duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("RemoteParcelRequestReply: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            var formatter = new BinaryFormatter();
            var xmlTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {
                var s = new RemoteParcelRequestReply
                {
                    ParcelID = UUID.Random()
                };

                var stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);
                var t = (RemoteParcelRequestReply)formatter.Deserialize(stream);

                Assert.That(s.ParcelID, Is.EqualTo(t.ParcelID));
            }
            var durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("RemoteParcelRequestReply: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }


        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply()
        {

            var messageTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {
                var s = new DirLandReplyMessage
                {
                    AgentID = UUID.Random(),
                    QueryID = UUID.Random(),
                    QueryReplies = new DirLandReplyMessage.QueryReply[2]
                };

                var q1 = new DirLandReplyMessage.QueryReply
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

                var q2 = new DirLandReplyMessage.QueryReply
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

                var map = s.Serialize();
                var t = new DirLandReplyMessage();

                t.Deserialize(map);
                Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
                Assert.That(s.QueryID, Is.EqualTo(t.QueryID));

                for (var i = 0; i < s.QueryReplies.Length; i++)
                {
                    Assert.That(s.QueryReplies[i].ActualArea, Is.EqualTo(t.QueryReplies[i].ActualArea));
                    Assert.That(s.QueryReplies[i].Auction, Is.EqualTo(t.QueryReplies[i].Auction));
                    Assert.That(s.QueryReplies[i].ForSale, Is.EqualTo(t.QueryReplies[i].ForSale));
                    Assert.That(s.QueryReplies[i].Name, Is.EqualTo(t.QueryReplies[i].Name));
                    Assert.That(s.QueryReplies[i].ProductSku, Is.EqualTo(t.QueryReplies[i].ProductSku));
                    Assert.That(s.QueryReplies[i].ParcelID, Is.EqualTo(t.QueryReplies[i].ParcelID));
                    Assert.That(s.QueryReplies[i].SalePrice, Is.EqualTo(t.QueryReplies[i].SalePrice));
                }
            }
            var duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("DirLandReplyMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            var formatter = new BinaryFormatter();
            var xmlTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {
                var s = new DirLandReplyMessage
                {
                    AgentID = UUID.Random(),
                    QueryID = UUID.Random(),
                    QueryReplies = new DirLandReplyMessage.QueryReply[2]
                };

                var q1 = new DirLandReplyMessage.QueryReply
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

                var q2 = new DirLandReplyMessage.QueryReply
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

                var stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);
                var t = (DirLandReplyMessage)formatter.Deserialize(stream);

                Assert.That(s.AgentID, Is.EqualTo(t.AgentID));
                Assert.That(s.QueryID, Is.EqualTo(t.QueryID));

                for (var i = 0; i < s.QueryReplies.Length; i++)
                {
                    Assert.That(s.QueryReplies[i].ActualArea, Is.EqualTo(t.QueryReplies[i].ActualArea));
                    Assert.That(s.QueryReplies[i].Auction, Is.EqualTo(t.QueryReplies[i].Auction));
                    Assert.That(s.QueryReplies[i].ForSale, Is.EqualTo(t.QueryReplies[i].ForSale));
                    Assert.That(s.QueryReplies[i].Name, Is.EqualTo(t.QueryReplies[i].Name));
                    Assert.That(s.QueryReplies[i].ProductSku, Is.EqualTo(t.QueryReplies[i].ProductSku));
                    Assert.That(s.QueryReplies[i].ParcelID, Is.EqualTo(t.QueryReplies[i].ParcelID));
                    Assert.That(s.QueryReplies[i].SalePrice, Is.EqualTo(t.QueryReplies[i].SalePrice));
                }
            }
            var durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("DirLandReplyMessage: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply2()
        {
            var xmlSerializer = new XmlSerializer(typeof(DirLandReplyMessage));

            var s = new DirLandReplyMessage
            {
                AgentID = UUID.Random(),
                QueryID = UUID.Random(),
                QueryReplies = new DirLandReplyMessage.QueryReply[2]
            };

            var q1 = new DirLandReplyMessage.QueryReply
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

            var q2 = new DirLandReplyMessage.QueryReply
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

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();
            for (var i = 0; i < TEST_ITER; ++i)
            {
                var stream = new MemoryStream();
                var map = s.Serialize();
                var jsonData = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(map));
                stream.Write(jsonData, 0, jsonData.Length);
                stream.Flush();
                stream.Close();
            }
            timer.Stop();
            Console.WriteLine("OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, timer.Elapsed.TotalSeconds);

            timer.Reset();
            timer.Start();
            for (var i = 0; i < TEST_ITER; ++i)
            {
                var stream = new MemoryStream();
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
            var messageTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {
                var s = new ParcelPropertiesMessage
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

                var map = s.Serialize();
                var t = new ParcelPropertiesMessage();

                t.Deserialize(map);

                Assert.That(s.AABBMax, Is.EqualTo(t.AABBMax));
                Assert.That(s.AABBMin, Is.EqualTo(t.AABBMin));
                Assert.That(s.Area, Is.EqualTo(t.Area));
                Assert.That(s.AuctionID, Is.EqualTo(t.AuctionID));
                Assert.That(s.AuthBuyerID, Is.EqualTo(t.AuthBuyerID));
                Assert.That(s.Bitmap, Is.EqualTo(t.Bitmap));
                Assert.That(s.Category, Is.EqualTo(t.Category));
                Assert.That(s.ClaimDate, Is.EqualTo(t.ClaimDate));
                Assert.That(s.ClaimPrice, Is.EqualTo(t.ClaimPrice));
                Assert.That(s.Desc, Is.EqualTo(t.Desc));
                Assert.That(s.GroupID, Is.EqualTo(t.GroupID));
                Assert.That(s.GroupPrims, Is.EqualTo(t.GroupPrims));
                Assert.That(s.IsGroupOwned, Is.EqualTo(t.IsGroupOwned));
                Assert.That(s.LandingType, Is.EqualTo(t.LandingType));
                Assert.That(s.LocalID, Is.EqualTo(t.LocalID));
                Assert.That(s.MaxPrims, Is.EqualTo(t.MaxPrims));
                Assert.That(s.MediaAutoScale, Is.EqualTo(t.MediaAutoScale));
                Assert.That(s.MediaDesc, Is.EqualTo(t.MediaDesc));
                Assert.That(s.MediaHeight, Is.EqualTo(t.MediaHeight));
                Assert.That(s.MediaID, Is.EqualTo(t.MediaID));
                Assert.That(s.MediaLoop, Is.EqualTo(t.MediaLoop));
                Assert.That(s.MediaType, Is.EqualTo(t.MediaType));
                Assert.That(s.MediaURL, Is.EqualTo(t.MediaURL));
                Assert.That(s.MediaWidth, Is.EqualTo(t.MediaWidth));
                Assert.That(s.MusicURL, Is.EqualTo(t.MusicURL));
                Assert.That(s.Name, Is.EqualTo(t.Name));
                Assert.That(s.ObscureMedia, Is.EqualTo(t.ObscureMedia));
                Assert.That(s.ObscureMusic, Is.EqualTo(t.ObscureMusic));
                Assert.That(s.OtherCleanTime, Is.EqualTo(t.OtherCleanTime));
                Assert.That(s.OtherCount, Is.EqualTo(t.OtherCount));
                Assert.That(s.OtherPrims, Is.EqualTo(t.OtherPrims));
                Assert.That(s.OwnerID, Is.EqualTo(t.OwnerID));
                Assert.That(s.OwnerPrims, Is.EqualTo(t.OwnerPrims));
                Assert.That(s.ParcelFlags, Is.EqualTo(t.ParcelFlags));
                Assert.That(s.ParcelPrimBonus, Is.EqualTo(t.ParcelPrimBonus));
                Assert.That(s.PassHours, Is.EqualTo(t.PassHours));
                Assert.That(s.PassPrice, Is.EqualTo(t.PassPrice));
                Assert.That(s.PublicCount, Is.EqualTo(t.PublicCount));
                Assert.That(s.RegionDenyAgeUnverified, Is.EqualTo(t.RegionDenyAgeUnverified));
                Assert.That(s.RegionDenyAnonymous, Is.EqualTo(t.RegionDenyAnonymous));
                Assert.That(s.RegionPushOverride, Is.EqualTo(t.RegionPushOverride));
                Assert.That(s.RentPrice, Is.EqualTo(t.RentPrice));
                Assert.That(s.RequestResult, Is.EqualTo(t.RequestResult));
                Assert.That(s.SalePrice, Is.EqualTo(t.SalePrice));
                Assert.That(s.SelectedPrims, Is.EqualTo(t.SelectedPrims));
                Assert.That(s.SelfCount, Is.EqualTo(t.SelfCount));
                Assert.That(s.SequenceID, Is.EqualTo(t.SequenceID));
                Assert.That(s.SimWideMaxPrims, Is.EqualTo(t.SimWideMaxPrims));
                Assert.That(s.SimWideTotalPrims, Is.EqualTo(t.SimWideTotalPrims));
                Assert.That(s.SnapSelection, Is.EqualTo(t.SnapSelection));
                Assert.That(s.SnapshotID, Is.EqualTo(t.SnapshotID));
                Assert.That(s.Status, Is.EqualTo(t.Status));
                Assert.That(s.TotalPrims, Is.EqualTo(t.TotalPrims));
                Assert.That(s.UserLocation, Is.EqualTo(t.UserLocation));
                Assert.That(s.UserLookAt, Is.EqualTo(t.UserLookAt));
            }
            var duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("ParcelPropertiesMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            var formatter = new BinaryFormatter();

            var xmlTestTime = DateTime.UtcNow;
            for (var x = 0; x < TEST_ITER; x++)
            {

                var s = new ParcelPropertiesMessage
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

                var stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);

                var t = (ParcelPropertiesMessage)formatter.Deserialize(stream);

                Assert.That(s.AABBMax, Is.EqualTo(t.AABBMax));
                Assert.That(s.AABBMin, Is.EqualTo(t.AABBMin));
                Assert.That(s.Area, Is.EqualTo(t.Area));
                Assert.That(s.AuctionID, Is.EqualTo(t.AuctionID));
                Assert.That(s.AuthBuyerID, Is.EqualTo(t.AuthBuyerID));
                Assert.That(s.Bitmap, Is.EqualTo(t.Bitmap));
                Assert.That(s.Category, Is.EqualTo(t.Category));
                Assert.That(s.ClaimDate, Is.EqualTo(t.ClaimDate));
                Assert.That(s.ClaimPrice, Is.EqualTo(t.ClaimPrice));
                Assert.That(s.Desc, Is.EqualTo(t.Desc));
                Assert.That(s.GroupID, Is.EqualTo(t.GroupID));
                Assert.That(s.GroupPrims, Is.EqualTo(t.GroupPrims));
                Assert.That(s.IsGroupOwned, Is.EqualTo(t.IsGroupOwned));
                Assert.That(s.LandingType, Is.EqualTo(t.LandingType));
                Assert.That(s.LocalID, Is.EqualTo(t.LocalID));
                Assert.That(s.MaxPrims, Is.EqualTo(t.MaxPrims));
                Assert.That(s.MediaAutoScale, Is.EqualTo(t.MediaAutoScale));
                Assert.That(s.MediaDesc, Is.EqualTo(t.MediaDesc));
                Assert.That(s.MediaHeight, Is.EqualTo(t.MediaHeight));
                Assert.That(s.MediaID, Is.EqualTo(t.MediaID));
                Assert.That(s.MediaLoop, Is.EqualTo(t.MediaLoop));
                Assert.That(s.MediaType, Is.EqualTo(t.MediaType));
                Assert.That(s.MediaURL, Is.EqualTo(t.MediaURL));
                Assert.That(s.MediaWidth, Is.EqualTo(t.MediaWidth));
                Assert.That(s.MusicURL, Is.EqualTo(t.MusicURL));
                Assert.That(s.Name, Is.EqualTo(t.Name));
                Assert.That(s.ObscureMedia, Is.EqualTo(t.ObscureMedia));
                Assert.That(s.ObscureMusic, Is.EqualTo(t.ObscureMusic));
                Assert.That(s.OtherCleanTime, Is.EqualTo(t.OtherCleanTime));
                Assert.That(s.OtherCount, Is.EqualTo(t.OtherCount));
                Assert.That(s.OtherPrims, Is.EqualTo(t.OtherPrims));
                Assert.That(s.OwnerID, Is.EqualTo(t.OwnerID));
                Assert.That(s.OwnerPrims, Is.EqualTo(t.OwnerPrims));
                Assert.That(s.ParcelFlags, Is.EqualTo(t.ParcelFlags));
                Assert.That(s.ParcelPrimBonus, Is.EqualTo(t.ParcelPrimBonus));
                Assert.That(s.PassHours, Is.EqualTo(t.PassHours));
                Assert.That(s.PassPrice, Is.EqualTo(t.PassPrice));
                Assert.That(s.PublicCount, Is.EqualTo(t.PublicCount));
                Assert.That(s.RegionDenyAgeUnverified, Is.EqualTo(t.RegionDenyAgeUnverified));
                Assert.That(s.RegionDenyAnonymous, Is.EqualTo(t.RegionDenyAnonymous));
                Assert.That(s.RegionPushOverride, Is.EqualTo(t.RegionPushOverride));
                Assert.That(s.RentPrice, Is.EqualTo(t.RentPrice));
                Assert.That(s.RequestResult, Is.EqualTo(t.RequestResult));
                Assert.That(s.SalePrice, Is.EqualTo(t.SalePrice));
                Assert.That(s.SelectedPrims, Is.EqualTo(t.SelectedPrims));
                Assert.That(s.SelfCount, Is.EqualTo(t.SelfCount));
                Assert.That(s.SequenceID, Is.EqualTo(t.SequenceID));
                Assert.That(s.SimWideMaxPrims, Is.EqualTo(t.SimWideMaxPrims));
                Assert.That(s.SimWideTotalPrims, Is.EqualTo(t.SimWideTotalPrims));
                Assert.That(s.SnapSelection, Is.EqualTo(t.SnapSelection));
                Assert.That(s.SnapshotID, Is.EqualTo(t.SnapshotID));
                Assert.That(s.Status, Is.EqualTo(t.Status));
                Assert.That(s.TotalPrims, Is.EqualTo(t.TotalPrims));
                Assert.That(s.UserLocation, Is.EqualTo(t.UserLocation));
                Assert.That(s.UserLookAt, Is.EqualTo(t.UserLookAt));
            }
            var durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("ParcelPropertiesMessage: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }

        #endregion
    }
}

