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
using NUnit.Framework.Legacy;

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
        private Uri testURI = new Uri("https://sim3187.agni.lindenlab.com:12043/cap/6028fc44-c1e5-80a1-f902-19bde114458b");
        private IPAddress testIP = IPAddress.Parse("127.0.0.1");
        private ulong testHandle = 1106108697797888;

        [Test]
        public void AgentGroupDataUpdateMessage()
        {
            AgentGroupDataUpdateMessage s = new AgentGroupDataUpdateMessage();
            s.AgentID = UUID.Random();



            AgentGroupDataUpdateMessage.GroupData[] blocks = new AgentGroupDataUpdateMessage.GroupData[2];
            AgentGroupDataUpdateMessage.GroupData g1 = new AgentGroupDataUpdateMessage.GroupData();

            g1.AcceptNotices = false;
            g1.Contribution = 1024;
            g1.GroupID = UUID.Random();
            g1.GroupInsigniaID = UUID.Random();
            g1.GroupName = "Group Name Test 1";
            g1.GroupPowers = GroupPowers.Accountable | GroupPowers.AllowLandmark | GroupPowers.AllowSetHome;
            blocks[0] = g1;

            AgentGroupDataUpdateMessage.GroupData g2 = new AgentGroupDataUpdateMessage.GroupData();
            g2.AcceptNotices = false;
            g2.Contribution = 16;
            g2.GroupID = UUID.Random();
            g2.GroupInsigniaID = UUID.Random();
            g2.GroupName = "Group Name Test 2";
            g2.GroupPowers = GroupPowers.ChangeActions | GroupPowers.DeedObject;
            blocks[1] = g2;

            s.GroupDataBlock = blocks;

            AgentGroupDataUpdateMessage.NewGroupData[] nblocks = new AgentGroupDataUpdateMessage.NewGroupData[2];

            AgentGroupDataUpdateMessage.NewGroupData ng1 = new AgentGroupDataUpdateMessage.NewGroupData();
            ng1.ListInProfile = false;
            nblocks[0] = ng1;

            AgentGroupDataUpdateMessage.NewGroupData ng2 = new AgentGroupDataUpdateMessage.NewGroupData();
            ng2.ListInProfile = true;
            nblocks[1] = ng2;

            s.NewGroupDataBlock = nblocks;

            OSDMap map = s.Serialize();

            AgentGroupDataUpdateMessage t = new AgentGroupDataUpdateMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);

            for (int i = 0; i < t.GroupDataBlock.Length; i++)
            {
                ClassicAssert.AreEqual(s.GroupDataBlock[i].AcceptNotices, t.GroupDataBlock[i].AcceptNotices);
                ClassicAssert.AreEqual(s.GroupDataBlock[i].Contribution, t.GroupDataBlock[i].Contribution);
                ClassicAssert.AreEqual(s.GroupDataBlock[i].GroupID, t.GroupDataBlock[i].GroupID);
                ClassicAssert.AreEqual(s.GroupDataBlock[i].GroupInsigniaID, t.GroupDataBlock[i].GroupInsigniaID);
                ClassicAssert.AreEqual(s.GroupDataBlock[i].GroupName, t.GroupDataBlock[i].GroupName);
                ClassicAssert.AreEqual(s.GroupDataBlock[i].GroupPowers, t.GroupDataBlock[i].GroupPowers);
            }

            for (int i = 0; i < t.NewGroupDataBlock.Length; i++)
            {
                ClassicAssert.AreEqual(s.NewGroupDataBlock[i].ListInProfile, t.NewGroupDataBlock[i].ListInProfile);
            }
        }

        [Test]
        public void TeleportFinishMessage()
        {
            TeleportFinishMessage s = new TeleportFinishMessage();
            s.AgentID = UUID.Random();
            s.Flags = TeleportFlags.ViaLocation | TeleportFlags.IsFlying;
            s.IP = testIP;
            s.LocationID = 32767;
            s.Port = 3000;
            s.RegionHandle = testHandle;
            s.SeedCapability = testURI;
            s.SimAccess = SimAccess.Mature;

            OSDMap map = s.Serialize();

            TeleportFinishMessage t = new TeleportFinishMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.Flags, t.Flags);
            ClassicAssert.AreEqual(s.IP, t.IP);
            ClassicAssert.AreEqual(s.LocationID, t.LocationID);
            ClassicAssert.AreEqual(s.Port, t.Port);
            ClassicAssert.AreEqual(s.RegionHandle, t.RegionHandle);
            ClassicAssert.AreEqual(s.SeedCapability, t.SeedCapability);
            ClassicAssert.AreEqual(s.SimAccess, t.SimAccess);
        }

        [Test]
        public void EstablishAgentCommunicationMessage()
        {
            EstablishAgentCommunicationMessage s = new EstablishAgentCommunicationMessage();
            s.Address = testIP;
            s.AgentID = UUID.Random();
            s.Port = 3000;
            s.SeedCapability = testURI;

            OSDMap map = s.Serialize();

            EstablishAgentCommunicationMessage t = new EstablishAgentCommunicationMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.Address, t.Address);
            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.Port, t.Port);
            ClassicAssert.AreEqual(s.SeedCapability, t.SeedCapability);
        }

        [Test]
        public void ParcelObjectOwnersMessage()
        {
            ParcelObjectOwnersReplyMessage s = new ParcelObjectOwnersReplyMessage();
            s.PrimOwnersBlock = new ParcelObjectOwnersReplyMessage.PrimOwner[2];

            ParcelObjectOwnersReplyMessage.PrimOwner obj = new ParcelObjectOwnersReplyMessage.PrimOwner();
            obj.OwnerID = UUID.Random();
            obj.Count = 10;
            obj.IsGroupOwned = true;
            obj.OnlineStatus = false;
            obj.TimeStamp = new DateTime(2010, 4, 13, 7, 19, 43);
            s.PrimOwnersBlock[0] = obj;

            ParcelObjectOwnersReplyMessage.PrimOwner obj1 = new ParcelObjectOwnersReplyMessage.PrimOwner();
            obj1.OwnerID = UUID.Random();
            obj1.Count = 0;
            obj1.IsGroupOwned = false;
            obj1.OnlineStatus = false;
            obj1.TimeStamp = new DateTime(1991, 1, 31, 3, 13, 31);
            s.PrimOwnersBlock[1] = obj1;

            OSDMap map = s.Serialize();

            ParcelObjectOwnersReplyMessage t = new ParcelObjectOwnersReplyMessage();
            t.Deserialize(map);

            for (int i = 0; i < t.PrimOwnersBlock.Length; i++)
            {
                ClassicAssert.AreEqual(s.PrimOwnersBlock[i].Count, t.PrimOwnersBlock[i].Count);
                ClassicAssert.AreEqual(s.PrimOwnersBlock[i].IsGroupOwned, t.PrimOwnersBlock[i].IsGroupOwned);
                ClassicAssert.AreEqual(s.PrimOwnersBlock[i].OnlineStatus, t.PrimOwnersBlock[i].OnlineStatus);
                ClassicAssert.AreEqual(s.PrimOwnersBlock[i].OwnerID, t.PrimOwnersBlock[i].OwnerID);
                ClassicAssert.AreEqual(s.PrimOwnersBlock[i].TimeStamp, t.PrimOwnersBlock[i].TimeStamp);
            }
        }

        [Test]
        public void ChatterBoxInvitationMessage()
        {
            ChatterBoxInvitationMessage s = new ChatterBoxInvitationMessage();
            s.BinaryBucket = Utils.EmptyBytes;
            s.Dialog = InstantMessageDialog.InventoryOffered;
            s.FromAgentID = UUID.Random();
            s.FromAgentName = "Prokofy Neva";
            s.GroupIM = false;
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

            ClassicAssert.AreEqual(s.BinaryBucket, t.BinaryBucket);
            ClassicAssert.AreEqual(s.Dialog, t.Dialog);
            ClassicAssert.AreEqual(s.FromAgentID, t.FromAgentID);
            ClassicAssert.AreEqual(s.FromAgentName, t.FromAgentName);
            ClassicAssert.AreEqual(s.GroupIM, t.GroupIM);
            ClassicAssert.AreEqual(s.IMSessionID, t.IMSessionID);
            ClassicAssert.AreEqual(s.Message, t.Message);
            ClassicAssert.AreEqual(s.Offline, t.Offline);
            ClassicAssert.AreEqual(s.ParentEstateID, t.ParentEstateID);
            ClassicAssert.AreEqual(s.Position, t.Position);
            ClassicAssert.AreEqual(s.RegionID, t.RegionID);
            ClassicAssert.AreEqual(s.Timestamp, t.Timestamp);
            ClassicAssert.AreEqual(s.ToAgentID, t.ToAgentID);
        }

        [Test]
        public void ChatterboxSessionEventReplyMessage()
        {
            ChatterboxSessionEventReplyMessage s = new ChatterboxSessionEventReplyMessage();
            s.SessionID = UUID.Random();
            s.Success = true;

            OSDMap map = s.Serialize();

            ChatterboxSessionEventReplyMessage t = new ChatterboxSessionEventReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
            ClassicAssert.AreEqual(s.Success, t.Success);
        }

        [Test]
        public void ChatterBoxSessionStartReplyMessage()
        {
            ChatterBoxSessionStartReplyMessage s = new ChatterBoxSessionStartReplyMessage();
            s.ModeratedVoice = true;
            s.SessionID = UUID.Random();
            s.SessionName = "Test Session";
            s.Success = true;
            s.TempSessionID = UUID.Random();
            s.Type = 1;
            s.VoiceEnabled = true;

            OSDMap map = s.Serialize();

            ChatterBoxSessionStartReplyMessage t = new ChatterBoxSessionStartReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ModeratedVoice, t.ModeratedVoice);
            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
            ClassicAssert.AreEqual(s.SessionName, t.SessionName);
            ClassicAssert.AreEqual(s.Success, t.Success);
            ClassicAssert.AreEqual(s.TempSessionID, t.TempSessionID);
            ClassicAssert.AreEqual(s.Type, t.Type);
            ClassicAssert.AreEqual(s.VoiceEnabled, t.VoiceEnabled);
        }

        [Test]
        public void ChatterBoxSessionAgentListUpdatesMessage()
        {
            ChatterBoxSessionAgentListUpdatesMessage s = new ChatterBoxSessionAgentListUpdatesMessage();
            s.SessionID = UUID.Random();
            s.Updates = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock[1];

            ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block1 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock();
            block1.AgentID = UUID.Random();
            block1.CanVoiceChat = true;
            block1.IsModerator = true;
            block1.MuteText = true;
            block1.MuteVoice = true;
            block1.Transition = "ENTER";

            ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock block2 = new ChatterBoxSessionAgentListUpdatesMessage.AgentUpdatesBlock();
            block2.AgentID = UUID.Random();
            block2.CanVoiceChat = true;
            block2.IsModerator = true;
            block2.MuteText = true;
            block2.MuteVoice = true;
            block2.Transition = "LEAVE";

            s.Updates[0] = block1;
            // s.Updates[1] = block2;

            OSDMap map = s.Serialize();

            ChatterBoxSessionAgentListUpdatesMessage t = new ChatterBoxSessionAgentListUpdatesMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
            for (int i = 0; i < t.Updates.Length; i++)
            {
                ClassicAssert.AreEqual(s.Updates[i].AgentID, t.Updates[i].AgentID);
                ClassicAssert.AreEqual(s.Updates[i].CanVoiceChat, t.Updates[i].CanVoiceChat);
                ClassicAssert.AreEqual(s.Updates[i].IsModerator, t.Updates[i].IsModerator);
                ClassicAssert.AreEqual(s.Updates[i].MuteText, t.Updates[i].MuteText);
                ClassicAssert.AreEqual(s.Updates[i].MuteVoice, t.Updates[i].MuteVoice);
                ClassicAssert.AreEqual(s.Updates[i].Transition, t.Updates[i].Transition);
            }
        }

        [Test]
        public void ViewerStatsMessage()
        {
            ViewerStatsMessage s = new ViewerStatsMessage();

            s.AgentFPS = 45.5f;
            s.AgentsInView = 1;
            s.SystemCPU = "Intel 80286";
            s.StatsDropped = 2;
            s.StatsFailedResends = 3;
            s.SystemGPU = "Vesa VGA+";
            s.SystemGPUClass = 4;
            s.SystemGPUVendor = "China";
            s.SystemGPUVersion = string.Empty;
            s.InCompressedPackets = 5000;
            s.InKbytes = 6000;
            s.InPackets = 22000;
            s.InSavings = 19;
            s.MiscInt1 = 5;
            s.MiscInt2 = 6;
            s.FailuresInvalid = 20;
            s.AgentLanguage = "en";
            s.AgentMemoryUsed = 12878728;
            s.MetersTraveled = 9999123;
            s.object_kbytes = 70001;
            s.FailuresOffCircuit = 201;
            s.SystemOS = "Palm OS 3.1";
            s.OutCompressedPackets = 8000;
            s.OutKbytes = 9000999;
            s.OutPackets = 21000210;
            s.OutSavings = 181;
            s.AgentPing = 135579;
            s.SystemInstalledRam = 4000000;
            s.RegionsVisited = 4579;
            s.FailuresResent = 9;
            s.AgentRuntime = 360023;
            s.FailuresSendPacket = 565;
            s.SessionID = UUID.Random();
            s.SimulatorFPS = 454;
            s.AgentStartTime = new DateTime(1973, 1, 16, 5, 23, 33);
            s.MiscString1 = "Unused String";
            s.texture_kbytes = 9367498382;
            s.AgentVersion = "1";
            s.MiscVersion = 1;
            s.VertexBuffersEnabled = true;
            s.world_kbytes = 232344439;

            OSDMap map = s.Serialize();
            ViewerStatsMessage t = new ViewerStatsMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentFPS, t.AgentFPS);
            ClassicAssert.AreEqual(s.AgentsInView, t.AgentsInView);
            ClassicAssert.AreEqual(s.SystemCPU, t.SystemCPU);
            ClassicAssert.AreEqual(s.StatsDropped, t.StatsDropped);
            ClassicAssert.AreEqual(s.StatsFailedResends, t.StatsFailedResends);
            ClassicAssert.AreEqual(s.SystemGPU, t.SystemGPU);
            ClassicAssert.AreEqual(s.SystemGPUClass, t.SystemGPUClass);
            ClassicAssert.AreEqual(s.SystemGPUVendor, t.SystemGPUVendor);
            ClassicAssert.AreEqual(s.SystemGPUVersion, t.SystemGPUVersion);
            ClassicAssert.AreEqual(s.InCompressedPackets, t.InCompressedPackets);
            ClassicAssert.AreEqual(s.InKbytes, t.InKbytes);
            ClassicAssert.AreEqual(s.InPackets, t.InPackets);
            ClassicAssert.AreEqual(s.InSavings, t.InSavings);
            ClassicAssert.AreEqual(s.MiscInt1, t.MiscInt1);
            ClassicAssert.AreEqual(s.MiscInt2, t.MiscInt2);
            ClassicAssert.AreEqual(s.FailuresInvalid, t.FailuresInvalid);
            ClassicAssert.AreEqual(s.AgentLanguage, t.AgentLanguage);
            ClassicAssert.AreEqual(s.AgentMemoryUsed, t.AgentMemoryUsed);
            ClassicAssert.AreEqual(s.MetersTraveled, t.MetersTraveled);
            ClassicAssert.AreEqual(s.object_kbytes, t.object_kbytes);
            ClassicAssert.AreEqual(s.FailuresOffCircuit, t.FailuresOffCircuit);
            ClassicAssert.AreEqual(s.SystemOS, t.SystemOS);
            ClassicAssert.AreEqual(s.OutCompressedPackets, t.OutCompressedPackets);
            ClassicAssert.AreEqual(s.OutKbytes, t.OutKbytes);
            ClassicAssert.AreEqual(s.OutPackets, t.OutPackets);
            ClassicAssert.AreEqual(s.OutSavings, t.OutSavings);
            ClassicAssert.AreEqual(s.AgentPing, t.AgentPing);
            ClassicAssert.AreEqual(s.SystemInstalledRam, t.SystemInstalledRam);
            ClassicAssert.AreEqual(s.RegionsVisited, t.RegionsVisited);
            ClassicAssert.AreEqual(s.FailuresResent, t.FailuresResent);
            ClassicAssert.AreEqual(s.AgentRuntime, t.AgentRuntime);
            ClassicAssert.AreEqual(s.FailuresSendPacket, t.FailuresSendPacket);
            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
            ClassicAssert.AreEqual(s.SimulatorFPS, t.SimulatorFPS);
            ClassicAssert.AreEqual(s.AgentStartTime, t.AgentStartTime);
            ClassicAssert.AreEqual(s.MiscString1, t.MiscString1);
            ClassicAssert.AreEqual(s.texture_kbytes, t.texture_kbytes);
            ClassicAssert.AreEqual(s.AgentVersion, t.AgentVersion);
            ClassicAssert.AreEqual(s.MiscVersion, t.MiscVersion);
            ClassicAssert.AreEqual(s.VertexBuffersEnabled, t.VertexBuffersEnabled);
            ClassicAssert.AreEqual(s.world_kbytes, t.world_kbytes);


        }

        [Test]
        public void ParcelVoiceInfoRequestMessage()
        {
            ParcelVoiceInfoRequestMessage s = new ParcelVoiceInfoRequestMessage();
            s.SipChannelUri = testURI;
            s.ParcelID = 1;
            s.RegionName = "Hooper";

            OSDMap map = s.Serialize();

            ParcelVoiceInfoRequestMessage t = new ParcelVoiceInfoRequestMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.SipChannelUri, t.SipChannelUri);
            ClassicAssert.AreEqual(s.ParcelID, t.ParcelID);
            ClassicAssert.AreEqual(s.RegionName, t.RegionName);
        }

        [Test]
        public void ScriptRunningReplyMessage()
        {
            ScriptRunningReplyMessage s = new ScriptRunningReplyMessage();
            s.ItemID = UUID.Random();
            s.Mono = true;
            s.Running = true;
            s.ObjectID = UUID.Random();

            OSDMap map = s.Serialize();

            ScriptRunningReplyMessage t = new ScriptRunningReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ItemID, t.ItemID);
            ClassicAssert.AreEqual(s.Mono, t.Mono);
            ClassicAssert.AreEqual(s.ObjectID, t.ObjectID);
            ClassicAssert.AreEqual(s.Running, t.Running);

        }

        [Test]
        public void MapLayerMessage()
        {

            MapLayerReplyVariant s = new MapLayerReplyVariant();
            s.Flags = 1;

            MapLayerReplyVariant.LayerData[] blocks = new MapLayerReplyVariant.LayerData[2];

            MapLayerReplyVariant.LayerData block = new MapLayerReplyVariant.LayerData();
            block.ImageID = UUID.Random();
            block.Bottom = 1;
            block.Top = 2;
            block.Left = 3;
            block.Right = 4;



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

            ClassicAssert.AreEqual(s.Flags, t.Flags);


            for (int i = 0; i < s.LayerDataBlocks.Length; i++)
            {
                ClassicAssert.AreEqual(s.LayerDataBlocks[i].ImageID, t.LayerDataBlocks[i].ImageID);
                ClassicAssert.AreEqual(s.LayerDataBlocks[i].Top, t.LayerDataBlocks[i].Top);
                ClassicAssert.AreEqual(s.LayerDataBlocks[i].Left, t.LayerDataBlocks[i].Left);
                ClassicAssert.AreEqual(s.LayerDataBlocks[i].Right, t.LayerDataBlocks[i].Right);
                ClassicAssert.AreEqual(s.LayerDataBlocks[i].Bottom, t.LayerDataBlocks[i].Bottom);
            }
        }

        [Test] // VARIANT A
        public void ChatSessionRequestStartConference()
        {
            ChatSessionRequestStartConference s = new ChatSessionRequestStartConference();
            s.SessionID = UUID.Random();
            s.AgentsBlock = new UUID[2];
            s.AgentsBlock[0] = UUID.Random();
            s.AgentsBlock[0] = UUID.Random();

            OSDMap map = s.Serialize();

            ChatSessionRequestStartConference t = new ChatSessionRequestStartConference();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
            ClassicAssert.AreEqual(s.Method, t.Method);
            for (int i = 0; i < t.AgentsBlock.Length; i++)
            {
                ClassicAssert.AreEqual(s.AgentsBlock[i], t.AgentsBlock[i]);
            }
        }

        [Test]
        public void ChatSessionRequestMuteUpdate()
        {
            ChatSessionRequestMuteUpdate s = new ChatSessionRequestMuteUpdate();
            s.AgentID = UUID.Random();
            s.RequestKey = "text";
            s.RequestValue = true;
            s.SessionID = UUID.Random();

            OSDMap map = s.Serialize();

            ChatSessionRequestMuteUpdate t = new ChatSessionRequestMuteUpdate();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.Method, t.Method);
            ClassicAssert.AreEqual(s.RequestKey, t.RequestKey);
            ClassicAssert.AreEqual(s.RequestValue, t.RequestValue);
            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
        }

        [Test]
        public void ChatSessionAcceptInvitation()
        {
            ChatSessionAcceptInvitation s = new ChatSessionAcceptInvitation();
            s.SessionID = UUID.Random();

            OSDMap map = s.Serialize();

            ChatSessionAcceptInvitation t = new ChatSessionAcceptInvitation();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.Method, t.Method);
            ClassicAssert.AreEqual(s.SessionID, t.SessionID);
        }

        [Test]
        public void RequiredVoiceVersionMessage()
        {
            RequiredVoiceVersionMessage s = new RequiredVoiceVersionMessage();
            s.MajorVersion = 1;
            s.MinorVersion = 0;
            s.RegionName = "Hooper";

            OSDMap map = s.Serialize();

            RequiredVoiceVersionMessage t = new RequiredVoiceVersionMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.MajorVersion, t.MajorVersion);
            ClassicAssert.AreEqual(s.MinorVersion, t.MinorVersion);
            ClassicAssert.AreEqual(s.RegionName, t.RegionName);
        }

        [Test]
        public void CopyInventoryFromNotecardMessage()
        {
            CopyInventoryFromNotecardMessage s = new CopyInventoryFromNotecardMessage();
            s.CallbackID = 1;
            s.FolderID = UUID.Random();
            s.ItemID = UUID.Random();
            s.NotecardID = UUID.Random();
            s.ObjectID = UUID.Random();

            OSDMap map = s.Serialize();

            CopyInventoryFromNotecardMessage t = new CopyInventoryFromNotecardMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.CallbackID, t.CallbackID);
            ClassicAssert.AreEqual(s.FolderID, t.FolderID);
            ClassicAssert.AreEqual(s.ItemID, t.ItemID);
            ClassicAssert.AreEqual(s.NotecardID, t.NotecardID);
            ClassicAssert.AreEqual(s.ObjectID, t.ObjectID);
        }

        [Test]
        public void ProvisionVoiceAccountRequestMessage()
        {
            ProvisionVoiceAccountRequestMessage s = new ProvisionVoiceAccountRequestMessage();
            s.Username = "username";
            s.Password = "password";

            OSDMap map = s.Serialize();

            ProvisionVoiceAccountRequestMessage t = new ProvisionVoiceAccountRequestMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.Password, t.Password);
            ClassicAssert.AreEqual(s.Username, t.Username);
        }

        [Test]
        public void UpdateAgentLanguageMessage()
        {
            UpdateAgentLanguageMessage s = new UpdateAgentLanguageMessage();
            s.Language = "en";
            s.LanguagePublic = false;

            OSDMap map = s.Serialize();

            UpdateAgentLanguageMessage t = new UpdateAgentLanguageMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.Language, t.Language);
            ClassicAssert.AreEqual(s.LanguagePublic, t.LanguagePublic);

        }

        [Test]
        public void ParcelPropertiesMessage()
        {
            ParcelPropertiesMessage s = new ParcelPropertiesMessage();
            s.AABBMax = Vector3.Parse("<1,2,3>");
            s.AABBMin = Vector3.Parse("<2,3,1>");
            s.AnyAVSounds = true;
            s.Area = 1024;
            s.AuctionID = uint.MaxValue;
            s.AuthBuyerID = UUID.Random();
            s.Bitmap = Utils.EmptyBytes;
            s.Category = ParcelCategory.Educational;
            s.ClaimDate = new DateTime(2008, 12, 25, 3, 15, 22);
            s.ClaimPrice = 1000;
            s.Desc = "Test Description";
            s.GroupAVSounds = true;
            s.GroupID = UUID.Random();
            s.GroupPrims = 50;
            s.IsGroupOwned = false;
            s.LandingType = LandingType.None;
            s.LocalID = 1;
            s.MaxPrims = 234;
            s.MediaAutoScale = false;
            s.MediaDesc = "Example Media Description";
            s.MediaHeight = 480;
            s.MediaID = UUID.Random();
            s.MediaLoop = false;
            s.MediaType = "text/html";
            s.MediaURL = "http://www.openmetaverse.co";
            s.MediaWidth = 640;
            s.MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075"; // Yee Haw
            s.Name = "Test Name";
            s.ObscureMedia = false;
            s.ObscureMusic = false;
            s.OtherCleanTime = 5;
            s.OtherCount = 200;
            s.OtherPrims = 300;
            s.OwnerID = UUID.Random();
            s.OwnerPrims = 0;
            s.ParcelFlags = ParcelFlags.AllowDamage | ParcelFlags.AllowGroupScripts | ParcelFlags.AllowVoiceChat;
            s.ParcelPrimBonus = 0f;
            s.PassHours = 1.5f;
            s.PassPrice = 10;
            s.PublicCount = 20;
            s.RegionDenyAgeUnverified = false;
            s.RegionDenyAnonymous = false;
            s.RegionPushOverride = true;
            s.RentPrice = 0;
            s.RequestResult = ParcelResult.Single;
            s.SalePrice = 9999;
            s.SeeAVs = true;
            s.SelectedPrims = 1;
            s.SelfCount = 2;
            s.SequenceID = -4000;
            s.SimWideMaxPrims = 937;
            s.SimWideTotalPrims = 117;
            s.SnapSelection = false;
            s.SnapshotID = UUID.Random();
            s.Status = ParcelStatus.Leased;
            s.TotalPrims = 219;
            s.UserLocation = Vector3.Parse("<3,4,5>");
            s.UserLookAt = Vector3.Parse("<5,4,3>");

            OSDMap map = s.Serialize();
            ParcelPropertiesMessage t = new ParcelPropertiesMessage();

            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AABBMax, t.AABBMax);
            ClassicAssert.AreEqual(s.AABBMin, t.AABBMin);
            ClassicAssert.AreEqual(s.AnyAVSounds, t.AnyAVSounds);
            ClassicAssert.AreEqual(s.Area, t.Area);
            ClassicAssert.AreEqual(s.AuctionID, t.AuctionID);
            ClassicAssert.AreEqual(s.AuthBuyerID, t.AuthBuyerID);
            ClassicAssert.AreEqual(s.Bitmap, t.Bitmap);
            ClassicAssert.AreEqual(s.Category, t.Category);
            ClassicAssert.AreEqual(s.ClaimDate, t.ClaimDate);
            ClassicAssert.AreEqual(s.ClaimPrice, t.ClaimPrice);
            ClassicAssert.AreEqual(s.Desc, t.Desc);
            ClassicAssert.AreEqual(s.GroupAVSounds, t.GroupAVSounds);
            ClassicAssert.AreEqual(s.GroupID, t.GroupID);
            ClassicAssert.AreEqual(s.GroupPrims, t.GroupPrims);
            ClassicAssert.AreEqual(s.IsGroupOwned, t.IsGroupOwned);
            ClassicAssert.AreEqual(s.LandingType, t.LandingType);
            ClassicAssert.AreEqual(s.LocalID, t.LocalID);
            ClassicAssert.AreEqual(s.MaxPrims, t.MaxPrims);
            ClassicAssert.AreEqual(s.MediaAutoScale, t.MediaAutoScale);
            ClassicAssert.AreEqual(s.MediaDesc, t.MediaDesc);
            ClassicAssert.AreEqual(s.MediaHeight, t.MediaHeight);
            ClassicAssert.AreEqual(s.MediaID, t.MediaID);
            ClassicAssert.AreEqual(s.MediaLoop, t.MediaLoop);
            ClassicAssert.AreEqual(s.MediaType, t.MediaType);
            ClassicAssert.AreEqual(s.MediaURL, t.MediaURL);
            ClassicAssert.AreEqual(s.MediaWidth, t.MediaWidth);
            ClassicAssert.AreEqual(s.MusicURL, t.MusicURL);
            ClassicAssert.AreEqual(s.Name, t.Name);
            ClassicAssert.AreEqual(s.ObscureMedia, t.ObscureMedia);
            ClassicAssert.AreEqual(s.ObscureMusic, t.ObscureMusic);
            ClassicAssert.AreEqual(s.OtherCleanTime, t.OtherCleanTime);
            ClassicAssert.AreEqual(s.OtherCount, t.OtherCount);
            ClassicAssert.AreEqual(s.OtherPrims, t.OtherPrims);
            ClassicAssert.AreEqual(s.OwnerID, t.OwnerID);
            ClassicAssert.AreEqual(s.OwnerPrims, t.OwnerPrims);
            ClassicAssert.AreEqual(s.ParcelFlags, t.ParcelFlags);
            ClassicAssert.AreEqual(s.ParcelPrimBonus, t.ParcelPrimBonus);
            ClassicAssert.AreEqual(s.PassHours, t.PassHours);
            ClassicAssert.AreEqual(s.PassPrice, t.PassPrice);
            ClassicAssert.AreEqual(s.PublicCount, t.PublicCount);
            ClassicAssert.AreEqual(s.RegionDenyAgeUnverified, t.RegionDenyAgeUnverified);
            ClassicAssert.AreEqual(s.RegionDenyAnonymous, t.RegionDenyAnonymous);
            ClassicAssert.AreEqual(s.RegionPushOverride, t.RegionPushOverride);
            ClassicAssert.AreEqual(s.RentPrice, t.RentPrice);
            ClassicAssert.AreEqual(s.RequestResult, t.RequestResult);
            ClassicAssert.AreEqual(s.SalePrice, t.SalePrice);
            ClassicAssert.AreEqual(s.SeeAVs, t.SeeAVs);
            ClassicAssert.AreEqual(s.SelectedPrims, t.SelectedPrims);
            ClassicAssert.AreEqual(s.SelfCount, t.SelfCount);
            ClassicAssert.AreEqual(s.SequenceID, t.SequenceID);
            ClassicAssert.AreEqual(s.SimWideMaxPrims, t.SimWideMaxPrims);
            ClassicAssert.AreEqual(s.SimWideTotalPrims, t.SimWideTotalPrims);
            ClassicAssert.AreEqual(s.SnapSelection, t.SnapSelection);
            ClassicAssert.AreEqual(s.SnapshotID, t.SnapshotID);
            ClassicAssert.AreEqual(s.Status, t.Status);
            ClassicAssert.AreEqual(s.TotalPrims, t.TotalPrims);
            ClassicAssert.AreEqual(s.UserLocation, t.UserLocation);
            ClassicAssert.AreEqual(s.UserLookAt, t.UserLookAt);
        }

        [Test]
        public void ParcelPropertiesUpdateMessage()
        {
            ParcelPropertiesUpdateMessage s = new ParcelPropertiesUpdateMessage();
            s.AnyAVSounds = true;
            s.AuthBuyerID = UUID.Random();
            s.Category = ParcelCategory.Gaming;
            s.Desc = "Example Description";
            s.GroupAVSounds = true;
            s.GroupID = UUID.Random();
            s.Landing = LandingType.LandingPoint;
            s.LocalID = 160;
            s.MediaAutoScale = true;
            s.MediaDesc = "Example Media Description";
            s.MediaHeight = 600;
            s.MediaID = UUID.Random();
            s.MediaLoop = false;
            s.MediaType = "image/jpeg";
            s.MediaURL = "http://www.openmetaverse.co/test.jpeg";
            s.MediaWidth = 800;
            s.MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075";
            s.Name = "Example Parcel Description";
            s.ObscureMedia = true;
            s.ObscureMusic = true;
            s.ParcelFlags = ParcelFlags.AllowVoiceChat | ParcelFlags.ContributeWithDeed;
            s.PassHours = 5.5f;
            s.PassPrice = 100;
            s.SalePrice = 99;
            s.SeeAVs = true;
            s.SnapshotID = UUID.Random();
            s.UserLocation = Vector3.Parse("<128,128,128>");
            s.UserLookAt = Vector3.Parse("<256,256,256>");

            OSDMap map = s.Serialize();

            ParcelPropertiesUpdateMessage t = new ParcelPropertiesUpdateMessage();

            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AnyAVSounds, t.AnyAVSounds);
            ClassicAssert.AreEqual(s.AuthBuyerID, t.AuthBuyerID);
            ClassicAssert.AreEqual(s.Category, t.Category);
            ClassicAssert.AreEqual(s.Desc, t.Desc);
            ClassicAssert.AreEqual(s.GroupAVSounds, t.GroupAVSounds);
            ClassicAssert.AreEqual(s.GroupID, t.GroupID);
            ClassicAssert.AreEqual(s.Landing, t.Landing);
            ClassicAssert.AreEqual(s.LocalID, t.LocalID);
            ClassicAssert.AreEqual(s.MediaAutoScale, t.MediaAutoScale);
            ClassicAssert.AreEqual(s.MediaDesc, t.MediaDesc);
            ClassicAssert.AreEqual(s.MediaHeight, t.MediaHeight);
            ClassicAssert.AreEqual(s.MediaID, t.MediaID);
            ClassicAssert.AreEqual(s.MediaLoop, t.MediaLoop);
            ClassicAssert.AreEqual(s.MediaType, t.MediaType);
            ClassicAssert.AreEqual(s.MediaURL, t.MediaURL);
            ClassicAssert.AreEqual(s.MediaWidth, t.MediaWidth);
            ClassicAssert.AreEqual(s.MusicURL, t.MusicURL);
            ClassicAssert.AreEqual(s.Name, t.Name);
            ClassicAssert.AreEqual(s.ObscureMedia, t.ObscureMedia);
            ClassicAssert.AreEqual(s.ObscureMusic, t.ObscureMusic);
            ClassicAssert.AreEqual(s.ParcelFlags, t.ParcelFlags);
            ClassicAssert.AreEqual(s.PassHours, t.PassHours);
            ClassicAssert.AreEqual(s.PassPrice, t.PassPrice);
            ClassicAssert.AreEqual(s.SalePrice, t.SalePrice);
            ClassicAssert.AreEqual(s.SeeAVs, t.SeeAVs);
            ClassicAssert.AreEqual(s.SnapshotID, t.SnapshotID);
            ClassicAssert.AreEqual(s.UserLocation, t.UserLocation);
            ClassicAssert.AreEqual(s.UserLookAt, t.UserLookAt);
        }
        [Test]
        public void EnableSimulatorMessage()
        {
            EnableSimulatorMessage s = new EnableSimulatorMessage();
            s.Simulators = new EnableSimulatorMessage.SimulatorInfoBlock[2];

            EnableSimulatorMessage.SimulatorInfoBlock block1 = new EnableSimulatorMessage.SimulatorInfoBlock();
            block1.IP = testIP;
            block1.Port = 3000;
            block1.RegionHandle = testHandle;
            s.Simulators[0] = block1;

            EnableSimulatorMessage.SimulatorInfoBlock block2 = new EnableSimulatorMessage.SimulatorInfoBlock();
            block2.IP = testIP;
            block2.Port = 3001;
            block2.RegionHandle = testHandle;
            s.Simulators[1] = block2;

            OSDMap map = s.Serialize();

            EnableSimulatorMessage t = new EnableSimulatorMessage();
            t.Deserialize(map);

            for (int i = 0; i < t.Simulators.Length; i++)
            {
                ClassicAssert.AreEqual(s.Simulators[i].IP, t.Simulators[i].IP);
                ClassicAssert.AreEqual(s.Simulators[i].Port, t.Simulators[i].Port);
                ClassicAssert.AreEqual(s.Simulators[i].RegionHandle, t.Simulators[i].RegionHandle);
            }
        }

        [Test]
        public void RemoteParcelRequestReply()
        {
            RemoteParcelRequestReply s = new RemoteParcelRequestReply();
            s.ParcelID = UUID.Random();
            OSDMap map = s.Serialize();

            RemoteParcelRequestReply t = new RemoteParcelRequestReply();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ParcelID, t.ParcelID);
        }

        [Test]
        public void UpdateScriptTaskMessage()
        {
            UpdateScriptTaskUpdateMessage s = new UpdateScriptTaskUpdateMessage();
            s.TaskID = UUID.Random();
            s.Target = "mono";
            s.ScriptRunning = true;
            s.ItemID = UUID.Random();

            OSDMap map = s.Serialize();
            UpdateScriptTaskUpdateMessage t = new UpdateScriptTaskUpdateMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ItemID, t.ItemID);
            ClassicAssert.AreEqual(s.ScriptRunning, t.ScriptRunning);
            ClassicAssert.AreEqual(s.Target, t.Target);
            ClassicAssert.AreEqual(s.TaskID, t.TaskID);
        }

        [Test]
        public void UpdateScriptAgentMessage()
        {
            UpdateScriptAgentRequestMessage s = new UpdateScriptAgentRequestMessage();
            s.ItemID = UUID.Random();
            s.Target = "lsl2";

            OSDMap map = s.Serialize();

            UpdateScriptAgentRequestMessage t = new UpdateScriptAgentRequestMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ItemID, t.ItemID);
            ClassicAssert.AreEqual(s.Target, t.Target);
        }

        [Test]
        public void SendPostcardMessage()
        {
            SendPostcardMessage s = new SendPostcardMessage();
            s.FromEmail = "contact@openmetaverse.co";
            s.FromName = "Jim Radford";
            s.GlobalPosition = Vector3.One;
            s.Message = "Hello, How are you today?";
            s.Subject = "Postcard from the edge";
            s.ToEmail = "test1@example.com";

            OSDMap map = s.Serialize();

            SendPostcardMessage t = new SendPostcardMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.FromEmail, t.FromEmail);
            ClassicAssert.AreEqual(s.FromName, t.FromName);
            ClassicAssert.AreEqual(s.GlobalPosition, t.GlobalPosition);
            ClassicAssert.AreEqual(s.Message, t.Message);
            ClassicAssert.AreEqual(s.Subject, t.Subject);
            ClassicAssert.AreEqual(s.ToEmail, t.ToEmail);
        }

        [Test]
        public void UpdateNotecardAgentInventoryMessage()
        {
            UpdateAgentInventoryRequestMessage s = new UpdateAgentInventoryRequestMessage();
            s.ItemID = UUID.Random();

            OSDMap map = s.Serialize();

            UpdateAgentInventoryRequestMessage t = new UpdateAgentInventoryRequestMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ItemID, t.ItemID);
        }

        [Test]
        public void LandStatReplyMessage()
        {
            LandStatReplyMessage s = new LandStatReplyMessage();
            s.ReportType = 22;
            s.RequestFlags = 44;
            s.TotalObjectCount = 2;
            s.ReportDataBlocks = new LandStatReplyMessage.ReportDataBlock[2];

            LandStatReplyMessage.ReportDataBlock block1 = new LandStatReplyMessage.ReportDataBlock();
            block1.Location = Vector3.One;
            block1.MonoScore = 99;
            block1.OwnerName = "Profoky Neva";
            block1.Score = 10;
            block1.TaskID = UUID.Random();
            block1.TaskLocalID = 987341;
            block1.TaskName = "Verbal Flogging";
            block1.TimeStamp = new DateTime(2009, 5, 23, 4, 30, 0);
            s.ReportDataBlocks[0] = block1;

            LandStatReplyMessage.ReportDataBlock block2 = new LandStatReplyMessage.ReportDataBlock();
            block2.Location = Vector3.One;
            block2.MonoScore = 1;
            block2.OwnerName = "Philip Linden";
            block2.Score = 5;
            block2.TaskID = UUID.Random();
            block2.TaskLocalID = 987342;
            block2.TaskName = "Happy Ant";
            block2.TimeStamp = new DateTime(2008, 4, 22, 3, 29, 55);
            s.ReportDataBlocks[1] = block2;

            OSDMap map = s.Serialize();

            LandStatReplyMessage t = new LandStatReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.ReportType, t.ReportType);
            ClassicAssert.AreEqual(s.RequestFlags, t.RequestFlags);
            ClassicAssert.AreEqual(s.TotalObjectCount, t.TotalObjectCount);

            for (int i = 0; i < t.ReportDataBlocks.Length; i++)
            {
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].Location, t.ReportDataBlocks[i].Location);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].MonoScore, t.ReportDataBlocks[i].MonoScore);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].OwnerName, t.ReportDataBlocks[i].OwnerName);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].Score, t.ReportDataBlocks[i].Score);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].TaskID, t.ReportDataBlocks[i].TaskID);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].TaskLocalID, t.ReportDataBlocks[i].TaskLocalID);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].TaskName, t.ReportDataBlocks[i].TaskName);
                ClassicAssert.AreEqual(s.ReportDataBlocks[i].TimeStamp, t.ReportDataBlocks[i].TimeStamp);
            }
        }

        [Test]
        public void TelportFailedMessage()
        {
            TeleportFailedMessage s = new TeleportFailedMessage();
            s.AgentID = UUID.Random();
            s.MessageKey = "Key";
            s.Reason = "Unable To Teleport for some unspecified reason";
            s.ExtraParams = string.Empty;

            OSDMap map = s.Serialize();

            TeleportFailedMessage t = new TeleportFailedMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.ExtraParams, t.ExtraParams);
            ClassicAssert.AreEqual(s.MessageKey, t.MessageKey);
            ClassicAssert.AreEqual(s.Reason, t.Reason);

        }

        [Test]
        public void UpdateAgentInformationMessage()
        {
            UpdateAgentInformationMessage s = new UpdateAgentInformationMessage();
            s.MaxAccess = "PG";
            OSDMap map = s.Serialize();

            UpdateAgentInformationMessage t = new UpdateAgentInformationMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.MaxAccess, t.MaxAccess);
        }

        [Test]
        public void PlacesReplyMessage()
        {
            PlacesReplyMessage s = new PlacesReplyMessage();
            s.TransactionID = UUID.Random();
            s.AgentID = UUID.Random();
            s.QueryID = UUID.Random();
            s.QueryDataBlocks = new PlacesReplyMessage.QueryData[2];

            PlacesReplyMessage.QueryData q1 = new PlacesReplyMessage.QueryData();
            q1.ActualArea = 1024;
            q1.BillableArea = 768;
            q1.Description = "Test Description Q1";
            q1.Dwell = 1435.4f;
            q1.Flags = 1 << 6;
            q1.GlobalX = 1;
            q1.GlobalY = 2;
            q1.GlobalZ = 3;
            q1.Name = "Test Name Q1";
            q1.OwnerID = UUID.Random();
            q1.Price = 1;
            q1.ProductSku = "021";
            q1.SimName = "Hooper";
            q1.SnapShotID = UUID.Random();

            s.QueryDataBlocks[0] = q1;

            PlacesReplyMessage.QueryData q2 = new PlacesReplyMessage.QueryData();
            q2.ActualArea = 512;
            q2.BillableArea = 384;
            q2.Description = "Test Description Q2";
            q2.Dwell = 1;
            q2.Flags = 1 << 4;
            q2.GlobalX = 4;
            q2.GlobalY = 5;
            q2.GlobalZ = 6;
            q2.Name = "Test Name Q2";
            q2.OwnerID = UUID.Random();
            q2.Price = 2;
            q2.ProductSku = "022";
            q2.SimName = "Tethys";
            q2.SnapShotID = UUID.Random();

            s.QueryDataBlocks[1] = q2;

            OSDMap map = s.Serialize();

            PlacesReplyMessage t = new PlacesReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.TransactionID, t.TransactionID);
            ClassicAssert.AreEqual(s.QueryID, t.QueryID);

            for (int i = 0; i < s.QueryDataBlocks.Length; i++)
            {
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].ActualArea, t.QueryDataBlocks[i].ActualArea);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].BillableArea, t.QueryDataBlocks[i].BillableArea);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].Description, t.QueryDataBlocks[i].Description);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].Dwell, t.QueryDataBlocks[i].Dwell);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].Flags, t.QueryDataBlocks[i].Flags);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].GlobalX, t.QueryDataBlocks[i].GlobalX);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].GlobalY, t.QueryDataBlocks[i].GlobalY);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].GlobalZ, t.QueryDataBlocks[i].GlobalZ);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].Name, t.QueryDataBlocks[i].Name);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].OwnerID, t.QueryDataBlocks[i].OwnerID);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].Price, t.QueryDataBlocks[i].Price);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].ProductSku, t.QueryDataBlocks[i].ProductSku);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].SimName, t.QueryDataBlocks[i].SimName);
                ClassicAssert.AreEqual(s.QueryDataBlocks[i].SnapShotID, t.QueryDataBlocks[i].SnapShotID);
            }
        }

        [Test]
        public void DirLandReplyMessage()
        {
            DirLandReplyMessage s = new DirLandReplyMessage();
            s.AgentID = UUID.Random();
            s.QueryID = UUID.Random();
            s.QueryReplies = new DirLandReplyMessage.QueryReply[2];

            DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply();
            q1.ActualArea = 1024;
            q1.Auction = true;
            q1.ForSale = true;
            q1.Name = "For Sale Parcel Q1";
            q1.ProductSku = "023";
            q1.SalePrice = 2193;
            q1.ParcelID = UUID.Random();

            s.QueryReplies[0] = q1;

            DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply();
            q2.ActualArea = 512;
            q2.Auction = true;
            q2.ForSale = true;
            q2.Name = "For Sale Parcel Q2";
            q2.ProductSku = "023";
            q2.SalePrice = 22193;
            q2.ParcelID = UUID.Random();

            s.QueryReplies[1] = q2;

            OSDMap map = s.Serialize();

            DirLandReplyMessage t = new DirLandReplyMessage();
            t.Deserialize(map);

            ClassicAssert.AreEqual(s.AgentID, t.AgentID);
            ClassicAssert.AreEqual(s.QueryID, t.QueryID);

            for (int i = 0; i < s.QueryReplies.Length; i++)
            {
                ClassicAssert.AreEqual(s.QueryReplies[i].ActualArea, t.QueryReplies[i].ActualArea);
                ClassicAssert.AreEqual(s.QueryReplies[i].Auction, t.QueryReplies[i].Auction);
                ClassicAssert.AreEqual(s.QueryReplies[i].ForSale, t.QueryReplies[i].ForSale);
                ClassicAssert.AreEqual(s.QueryReplies[i].Name, t.QueryReplies[i].Name);
                ClassicAssert.AreEqual(s.QueryReplies[i].ProductSku, t.QueryReplies[i].ProductSku);
                ClassicAssert.AreEqual(s.QueryReplies[i].ParcelID, t.QueryReplies[i].ParcelID);
                ClassicAssert.AreEqual(s.QueryReplies[i].SalePrice, t.QueryReplies[i].SalePrice);
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
                RemoteParcelRequestReply s = new RemoteParcelRequestReply();
                s.ParcelID = UUID.Random();
                OSDMap map = s.Serialize();

                RemoteParcelRequestReply t = new RemoteParcelRequestReply();
                t.Deserialize(map);

                ClassicAssert.AreEqual(s.ParcelID, t.ParcelID);
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("RemoteParcelRequestReply: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            BinaryFormatter formatter = new BinaryFormatter();
            DateTime xmlTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                RemoteParcelRequestReply s = new RemoteParcelRequestReply();
                s.ParcelID = UUID.Random();

                MemoryStream stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);
                RemoteParcelRequestReply t = (RemoteParcelRequestReply)formatter.Deserialize(stream);

                ClassicAssert.AreEqual(s.ParcelID, t.ParcelID);
            }
            TimeSpan durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("RemoteParcelRequestReply: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }


        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply()
        {

            DateTime messageTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                DirLandReplyMessage s = new DirLandReplyMessage();
                s.AgentID = UUID.Random();
                s.QueryID = UUID.Random();
                s.QueryReplies = new DirLandReplyMessage.QueryReply[2];

                DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply();
                q1.ActualArea = 1024;
                q1.Auction = true;
                q1.ForSale = true;
                q1.Name = "For Sale Parcel Q1";
                q1.ProductSku = "023";
                q1.SalePrice = 2193;
                q1.ParcelID = UUID.Random();

                s.QueryReplies[0] = q1;

                DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply();
                q2.ActualArea = 512;
                q2.Auction = true;
                q2.ForSale = true;
                q2.Name = "For Sale Parcel Q2";
                q2.ProductSku = "023";
                q2.SalePrice = 22193;
                q2.ParcelID = UUID.Random();

                s.QueryReplies[1] = q2;

                OSDMap map = s.Serialize();
                DirLandReplyMessage t = new DirLandReplyMessage();

                t.Deserialize(map);
                ClassicAssert.AreEqual(s.AgentID, t.AgentID);
                ClassicAssert.AreEqual(s.QueryID, t.QueryID);

                for (int i = 0; i < s.QueryReplies.Length; i++)
                {
                    ClassicAssert.AreEqual(s.QueryReplies[i].ActualArea, t.QueryReplies[i].ActualArea);
                    ClassicAssert.AreEqual(s.QueryReplies[i].Auction, t.QueryReplies[i].Auction);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ForSale, t.QueryReplies[i].ForSale);
                    ClassicAssert.AreEqual(s.QueryReplies[i].Name, t.QueryReplies[i].Name);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ProductSku, t.QueryReplies[i].ProductSku);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ParcelID, t.QueryReplies[i].ParcelID);
                    ClassicAssert.AreEqual(s.QueryReplies[i].SalePrice, t.QueryReplies[i].SalePrice);
                }
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("DirLandReplyMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            BinaryFormatter formatter = new BinaryFormatter();
            DateTime xmlTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {
                DirLandReplyMessage s = new DirLandReplyMessage();
                s.AgentID = UUID.Random();
                s.QueryID = UUID.Random();
                s.QueryReplies = new DirLandReplyMessage.QueryReply[2];

                DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply();
                q1.ActualArea = 1024;
                q1.Auction = true;
                q1.ForSale = true;
                q1.Name = "For Sale Parcel Q1";
                q1.ProductSku = "023";
                q1.SalePrice = 2193;
                q1.ParcelID = UUID.Random();

                s.QueryReplies[0] = q1;

                DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply();
                q2.ActualArea = 512;
                q2.Auction = true;
                q2.ForSale = true;
                q2.Name = "For Sale Parcel Q2";
                q2.ProductSku = "023";
                q2.SalePrice = 22193;
                q2.ParcelID = UUID.Random();

                s.QueryReplies[1] = q2;

                MemoryStream stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);
                DirLandReplyMessage t = (DirLandReplyMessage)formatter.Deserialize(stream);

                ClassicAssert.AreEqual(s.AgentID, t.AgentID);
                ClassicAssert.AreEqual(s.QueryID, t.QueryID);

                for (int i = 0; i < s.QueryReplies.Length; i++)
                {
                    ClassicAssert.AreEqual(s.QueryReplies[i].ActualArea, t.QueryReplies[i].ActualArea);
                    ClassicAssert.AreEqual(s.QueryReplies[i].Auction, t.QueryReplies[i].Auction);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ForSale, t.QueryReplies[i].ForSale);
                    ClassicAssert.AreEqual(s.QueryReplies[i].Name, t.QueryReplies[i].Name);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ProductSku, t.QueryReplies[i].ProductSku);
                    ClassicAssert.AreEqual(s.QueryReplies[i].ParcelID, t.QueryReplies[i].ParcelID);
                    ClassicAssert.AreEqual(s.QueryReplies[i].SalePrice, t.QueryReplies[i].SalePrice);
                }
            }
            TimeSpan durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("DirLandReplyMessage: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }

        [Test]
        [Category("Benchmark")]
        public void ReflectionPerformanceDirLandReply2()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DirLandReplyMessage));

            DirLandReplyMessage s = new DirLandReplyMessage();
            s.AgentID = UUID.Random();
            s.QueryID = UUID.Random();
            s.QueryReplies = new DirLandReplyMessage.QueryReply[2];

            DirLandReplyMessage.QueryReply q1 = new DirLandReplyMessage.QueryReply();
            q1.ActualArea = 1024;
            q1.Auction = true;
            q1.ForSale = true;
            q1.Name = "For Sale Parcel Q1";
            q1.ProductSku = "023";
            q1.SalePrice = 2193;
            q1.ParcelID = UUID.Random();

            s.QueryReplies[0] = q1;

            DirLandReplyMessage.QueryReply q2 = new DirLandReplyMessage.QueryReply();
            q2.ActualArea = 512;
            q2.Auction = true;
            q2.ForSale = true;
            q2.Name = "For Sale Parcel Q2";
            q2.ProductSku = "023";
            q2.SalePrice = 22193;
            q2.ParcelID = UUID.Random();

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
                ParcelPropertiesMessage s = new ParcelPropertiesMessage();
                s.AABBMax = Vector3.Parse("<1,2,3>");
                s.AABBMin = Vector3.Parse("<2,3,1>");
                s.Area = 1024;
                s.AuctionID = uint.MaxValue;
                s.AuthBuyerID = UUID.Random();
                s.Bitmap = Utils.EmptyBytes;
                s.Category = ParcelCategory.Educational;
                s.ClaimDate = new DateTime(2008, 12, 25, 3, 15, 22);
                s.ClaimPrice = 1000;
                s.Desc = "Test Description";
                s.GroupID = UUID.Random();
                s.GroupPrims = 50;
                s.IsGroupOwned = false;
                s.LandingType = LandingType.None;
                s.LocalID = 1;
                s.MaxPrims = 234;
                s.MediaAutoScale = false;
                s.MediaDesc = "Example Media Description";
                s.MediaHeight = 480;
                s.MediaID = UUID.Random();
                s.MediaLoop = false;
                s.MediaType = "text/html";
                s.MediaURL = "http://www.openmetaverse.co";
                s.MediaWidth = 640;
                s.MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075"; // Yee Haw
                s.Name = "Test Name";
                s.ObscureMedia = false;
                s.ObscureMusic = false;
                s.OtherCleanTime = 5;
                s.OtherCount = 200;
                s.OtherPrims = 300;
                s.OwnerID = UUID.Random();
                s.OwnerPrims = 0;
                s.ParcelFlags = ParcelFlags.AllowDamage | ParcelFlags.AllowGroupScripts | ParcelFlags.AllowVoiceChat;
                s.ParcelPrimBonus = 0f;
                s.PassHours = 1.5f;
                s.PassPrice = 10;
                s.PublicCount = 20;
                s.RegionDenyAgeUnverified = false;
                s.RegionDenyAnonymous = false;
                s.RegionPushOverride = true;
                s.RentPrice = 0;
                s.RequestResult = ParcelResult.Single;
                s.SalePrice = 9999;
                s.SelectedPrims = 1;
                s.SelfCount = 2;
                s.SequenceID = -4000;
                s.SimWideMaxPrims = 937;
                s.SimWideTotalPrims = 117;
                s.SnapSelection = false;
                s.SnapshotID = UUID.Random();
                s.Status = ParcelStatus.Leased;
                s.TotalPrims = 219;
                s.UserLocation = Vector3.Parse("<3,4,5>");
                s.UserLookAt = Vector3.Parse("<5,4,3>");

                OSDMap map = s.Serialize();
                ParcelPropertiesMessage t = new ParcelPropertiesMessage();

                t.Deserialize(map);

                ClassicAssert.AreEqual(s.AABBMax, t.AABBMax);
                ClassicAssert.AreEqual(s.AABBMin, t.AABBMin);
                ClassicAssert.AreEqual(s.Area, t.Area);
                ClassicAssert.AreEqual(s.AuctionID, t.AuctionID);
                ClassicAssert.AreEqual(s.AuthBuyerID, t.AuthBuyerID);
                ClassicAssert.AreEqual(s.Bitmap, t.Bitmap);
                ClassicAssert.AreEqual(s.Category, t.Category);
                ClassicAssert.AreEqual(s.ClaimDate, t.ClaimDate);
                ClassicAssert.AreEqual(s.ClaimPrice, t.ClaimPrice);
                ClassicAssert.AreEqual(s.Desc, t.Desc);
                ClassicAssert.AreEqual(s.GroupID, t.GroupID);
                ClassicAssert.AreEqual(s.GroupPrims, t.GroupPrims);
                ClassicAssert.AreEqual(s.IsGroupOwned, t.IsGroupOwned);
                ClassicAssert.AreEqual(s.LandingType, t.LandingType);
                ClassicAssert.AreEqual(s.LocalID, t.LocalID);
                ClassicAssert.AreEqual(s.MaxPrims, t.MaxPrims);
                ClassicAssert.AreEqual(s.MediaAutoScale, t.MediaAutoScale);
                ClassicAssert.AreEqual(s.MediaDesc, t.MediaDesc);
                ClassicAssert.AreEqual(s.MediaHeight, t.MediaHeight);
                ClassicAssert.AreEqual(s.MediaID, t.MediaID);
                ClassicAssert.AreEqual(s.MediaLoop, t.MediaLoop);
                ClassicAssert.AreEqual(s.MediaType, t.MediaType);
                ClassicAssert.AreEqual(s.MediaURL, t.MediaURL);
                ClassicAssert.AreEqual(s.MediaWidth, t.MediaWidth);
                ClassicAssert.AreEqual(s.MusicURL, t.MusicURL);
                ClassicAssert.AreEqual(s.Name, t.Name);
                ClassicAssert.AreEqual(s.ObscureMedia, t.ObscureMedia);
                ClassicAssert.AreEqual(s.ObscureMusic, t.ObscureMusic);
                ClassicAssert.AreEqual(s.OtherCleanTime, t.OtherCleanTime);
                ClassicAssert.AreEqual(s.OtherCount, t.OtherCount);
                ClassicAssert.AreEqual(s.OtherPrims, t.OtherPrims);
                ClassicAssert.AreEqual(s.OwnerID, t.OwnerID);
                ClassicAssert.AreEqual(s.OwnerPrims, t.OwnerPrims);
                ClassicAssert.AreEqual(s.ParcelFlags, t.ParcelFlags);
                ClassicAssert.AreEqual(s.ParcelPrimBonus, t.ParcelPrimBonus);
                ClassicAssert.AreEqual(s.PassHours, t.PassHours);
                ClassicAssert.AreEqual(s.PassPrice, t.PassPrice);
                ClassicAssert.AreEqual(s.PublicCount, t.PublicCount);
                ClassicAssert.AreEqual(s.RegionDenyAgeUnverified, t.RegionDenyAgeUnverified);
                ClassicAssert.AreEqual(s.RegionDenyAnonymous, t.RegionDenyAnonymous);
                ClassicAssert.AreEqual(s.RegionPushOverride, t.RegionPushOverride);
                ClassicAssert.AreEqual(s.RentPrice, t.RentPrice);
                ClassicAssert.AreEqual(s.RequestResult, t.RequestResult);
                ClassicAssert.AreEqual(s.SalePrice, t.SalePrice);
                ClassicAssert.AreEqual(s.SelectedPrims, t.SelectedPrims);
                ClassicAssert.AreEqual(s.SelfCount, t.SelfCount);
                ClassicAssert.AreEqual(s.SequenceID, t.SequenceID);
                ClassicAssert.AreEqual(s.SimWideMaxPrims, t.SimWideMaxPrims);
                ClassicAssert.AreEqual(s.SimWideTotalPrims, t.SimWideTotalPrims);
                ClassicAssert.AreEqual(s.SnapSelection, t.SnapSelection);
                ClassicAssert.AreEqual(s.SnapshotID, t.SnapshotID);
                ClassicAssert.AreEqual(s.Status, t.Status);
                ClassicAssert.AreEqual(s.TotalPrims, t.TotalPrims);
                ClassicAssert.AreEqual(s.UserLocation, t.UserLocation);
                ClassicAssert.AreEqual(s.UserLookAt, t.UserLookAt);
            }
            TimeSpan duration = DateTime.UtcNow - messageTestTime;
            Console.WriteLine("ParcelPropertiesMessage: OMV Message System Serialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, duration);

            BinaryFormatter formatter = new BinaryFormatter();

            DateTime xmlTestTime = DateTime.UtcNow;
            for (int x = 0; x < TEST_ITER; x++)
            {

                ParcelPropertiesMessage s = new ParcelPropertiesMessage();
                s.AABBMax = Vector3.Parse("<1,2,3>");
                s.AABBMin = Vector3.Parse("<2,3,1>");
                s.Area = 1024;
                s.AuctionID = uint.MaxValue;
                s.AuthBuyerID = UUID.Random();
                s.Bitmap = Utils.EmptyBytes;
                s.Category = ParcelCategory.Educational;
                s.ClaimDate = new DateTime(2008, 12, 25, 3, 15, 22);
                s.ClaimPrice = 1000;
                s.Desc = "Test Description";
                s.GroupID = UUID.Random();
                s.GroupPrims = 50;
                s.IsGroupOwned = false;
                s.LandingType = LandingType.None;
                s.LocalID = 1;
                s.MaxPrims = 234;
                s.MediaAutoScale = false;
                s.MediaDesc = "Example Media Description";
                s.MediaHeight = 480;
                s.MediaID = UUID.Random();
                s.MediaLoop = false;
                s.MediaType = "text/html";
                s.MediaURL = "http://www.openmetaverse.co";
                s.MediaWidth = 640;
                s.MusicURL = "http://scfire-ntc-aa04.stream.aol.com:80/stream/1075"; // Yee Haw
                s.Name = "Test Name";
                s.ObscureMedia = false;
                s.ObscureMusic = false;
                s.OtherCleanTime = 5;
                s.OtherCount = 200;
                s.OtherPrims = 300;
                s.OwnerID = UUID.Random();
                s.OwnerPrims = 0;
                s.ParcelFlags = ParcelFlags.AllowDamage | ParcelFlags.AllowGroupScripts | ParcelFlags.AllowVoiceChat;
                s.ParcelPrimBonus = 0f;
                s.PassHours = 1.5f;
                s.PassPrice = 10;
                s.PublicCount = 20;
                s.RegionDenyAgeUnverified = false;
                s.RegionDenyAnonymous = false;
                s.RegionPushOverride = true;
                s.RentPrice = 0;
                s.RequestResult = ParcelResult.Single;
                s.SalePrice = 9999;
                s.SelectedPrims = 1;
                s.SelfCount = 2;
                s.SequenceID = -4000;
                s.SimWideMaxPrims = 937;
                s.SimWideTotalPrims = 117;
                s.SnapSelection = false;
                s.SnapshotID = UUID.Random();
                s.Status = ParcelStatus.Leased;
                s.TotalPrims = 219;
                s.UserLocation = Vector3.Parse("<3,4,5>");
                s.UserLookAt = Vector3.Parse("<5,4,3>");

                MemoryStream stream = new MemoryStream();

                formatter.Serialize(stream, s);

                stream.Seek(0, SeekOrigin.Begin);

                ParcelPropertiesMessage t = (ParcelPropertiesMessage)formatter.Deserialize(stream);

                ClassicAssert.AreEqual(s.AABBMax, t.AABBMax);
                ClassicAssert.AreEqual(s.AABBMin, t.AABBMin);
                ClassicAssert.AreEqual(s.Area, t.Area);
                ClassicAssert.AreEqual(s.AuctionID, t.AuctionID);
                ClassicAssert.AreEqual(s.AuthBuyerID, t.AuthBuyerID);
                ClassicAssert.AreEqual(s.Bitmap, t.Bitmap);
                ClassicAssert.AreEqual(s.Category, t.Category);
                ClassicAssert.AreEqual(s.ClaimDate, t.ClaimDate);
                ClassicAssert.AreEqual(s.ClaimPrice, t.ClaimPrice);
                ClassicAssert.AreEqual(s.Desc, t.Desc);
                ClassicAssert.AreEqual(s.GroupID, t.GroupID);
                ClassicAssert.AreEqual(s.GroupPrims, t.GroupPrims);
                ClassicAssert.AreEqual(s.IsGroupOwned, t.IsGroupOwned);
                ClassicAssert.AreEqual(s.LandingType, t.LandingType);
                ClassicAssert.AreEqual(s.LocalID, t.LocalID);
                ClassicAssert.AreEqual(s.MaxPrims, t.MaxPrims);
                ClassicAssert.AreEqual(s.MediaAutoScale, t.MediaAutoScale);
                ClassicAssert.AreEqual(s.MediaDesc, t.MediaDesc);
                ClassicAssert.AreEqual(s.MediaHeight, t.MediaHeight);
                ClassicAssert.AreEqual(s.MediaID, t.MediaID);
                ClassicAssert.AreEqual(s.MediaLoop, t.MediaLoop);
                ClassicAssert.AreEqual(s.MediaType, t.MediaType);
                ClassicAssert.AreEqual(s.MediaURL, t.MediaURL);
                ClassicAssert.AreEqual(s.MediaWidth, t.MediaWidth);
                ClassicAssert.AreEqual(s.MusicURL, t.MusicURL);
                ClassicAssert.AreEqual(s.Name, t.Name);
                ClassicAssert.AreEqual(s.ObscureMedia, t.ObscureMedia);
                ClassicAssert.AreEqual(s.ObscureMusic, t.ObscureMusic);
                ClassicAssert.AreEqual(s.OtherCleanTime, t.OtherCleanTime);
                ClassicAssert.AreEqual(s.OtherCount, t.OtherCount);
                ClassicAssert.AreEqual(s.OtherPrims, t.OtherPrims);
                ClassicAssert.AreEqual(s.OwnerID, t.OwnerID);
                ClassicAssert.AreEqual(s.OwnerPrims, t.OwnerPrims);
                ClassicAssert.AreEqual(s.ParcelFlags, t.ParcelFlags);
                ClassicAssert.AreEqual(s.ParcelPrimBonus, t.ParcelPrimBonus);
                ClassicAssert.AreEqual(s.PassHours, t.PassHours);
                ClassicAssert.AreEqual(s.PassPrice, t.PassPrice);
                ClassicAssert.AreEqual(s.PublicCount, t.PublicCount);
                ClassicAssert.AreEqual(s.RegionDenyAgeUnverified, t.RegionDenyAgeUnverified);
                ClassicAssert.AreEqual(s.RegionDenyAnonymous, t.RegionDenyAnonymous);
                ClassicAssert.AreEqual(s.RegionPushOverride, t.RegionPushOverride);
                ClassicAssert.AreEqual(s.RentPrice, t.RentPrice);
                ClassicAssert.AreEqual(s.RequestResult, t.RequestResult);
                ClassicAssert.AreEqual(s.SalePrice, t.SalePrice);
                ClassicAssert.AreEqual(s.SelectedPrims, t.SelectedPrims);
                ClassicAssert.AreEqual(s.SelfCount, t.SelfCount);
                ClassicAssert.AreEqual(s.SequenceID, t.SequenceID);
                ClassicAssert.AreEqual(s.SimWideMaxPrims, t.SimWideMaxPrims);
                ClassicAssert.AreEqual(s.SimWideTotalPrims, t.SimWideTotalPrims);
                ClassicAssert.AreEqual(s.SnapSelection, t.SnapSelection);
                ClassicAssert.AreEqual(s.SnapshotID, t.SnapshotID);
                ClassicAssert.AreEqual(s.Status, t.Status);
                ClassicAssert.AreEqual(s.TotalPrims, t.TotalPrims);
                ClassicAssert.AreEqual(s.UserLocation, t.UserLocation);
                ClassicAssert.AreEqual(s.UserLookAt, t.UserLookAt);
            }
            TimeSpan durationxml = DateTime.UtcNow - xmlTestTime;
            Console.WriteLine("ParcelPropertiesMessage: .NET BinarySerialization/Deserialization Passes: {0} Total time: {1}", TEST_ITER, durationxml);
        }

        #endregion
    }
}

