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

using OpenMetaverse.Interfaces;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenMetaverse
{
    public partial class AgentManager
    {
        /// <summary>
        /// Resolve the correct simulator for a CAPS/EventQueue message by examining message data.
        /// Attempts to match by region handle or IP/port from the message.
        /// </summary>
        /// <param name="msg">The CAPS message (EstablishAgentCommunication, TeleportFinish, CrossedRegion, etc.)</param>
        /// <param name="fallbackSimulator">Fallback simulator if resolution fails</param>
        /// <returns>Resolved simulator or fallback</returns>
        private Simulator ResolveSimulatorFromMessage(object msg, Simulator fallbackSimulator)
        {
            if (msg == null)
                return fallbackSimulator ?? Client?.Network?.CurrentSim;

            // Try to extract region handle from common message types
            ulong regionHandle = 0;
            IPEndPoint endPoint = null;

            // Check known message types that contain region information
            switch (msg)
            {
                case TeleportFinishMessage tfm:
                    regionHandle = tfm.RegionHandle;
                    endPoint = new IPEndPoint(tfm.IP, tfm.Port);
                    break;
                case CrossedRegionMessage crm:
                    regionHandle = crm.RegionHandle;
                    endPoint = new IPEndPoint(crm.IP, crm.Port);
                    break;
                case EstablishAgentCommunicationMessage eacm:
                    endPoint = new IPEndPoint(eacm.Address, eacm.Port);
                    break;
            }

            // Try to find simulator by endpoint first (most specific)
            if (endPoint != null)
            {
                var sim = Client?.Network?.FindSimulator(endPoint);
                if (sim != null)
                {
                    Logger.Debug($"Resolved simulator by endpoint {endPoint}: {sim.Name}", Client);
                    return sim;
                }
            }

            // Try to find by region handle
            if (regionHandle != 0)
            {
                var sim = Client?.Network?.FindSimulator(regionHandle);
                if (sim != null)
                {
                    Logger.Debug($"Resolved simulator by handle {regionHandle}: {sim.Name}", Client);
                    return sim;
                }
            }

            // If we have endpoint info but no simulator, log it
            if (endPoint != null || regionHandle != 0)
            {
                Logger.Debug($"Could not resolve simulator for handle={regionHandle}, endpoint={endPoint}, using fallback", Client);
            }

            return fallbackSimulator ?? Client?.Network?.CurrentSim;
        }

        /// <summary>
        /// Take an incoming ImprovedInstantMessage packet, auto-parse, and if
        /// OnInstantMessage is defined call that with the appropriate arguments
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void InstantMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            
            // Resolve the simulator for this IM
            Simulator simulator = ResolveSimulator(e);

            if (packet.Type != PacketType.ImprovedInstantMessage) return;

            ImprovedInstantMessagePacket im = (ImprovedInstantMessagePacket)packet;

            if (m_InstantMessage != null)
            {
                InstantMessage message;
                message.FromAgentID = im.AgentData.AgentID;
                message.FromAgentName = Utils.BytesToString(im.MessageBlock.FromAgentName);
                message.ToAgentID = im.MessageBlock.ToAgentID;
                message.ParentEstateID = im.MessageBlock.ParentEstateID;
                message.RegionID = im.MessageBlock.RegionID;
                message.Position = im.MessageBlock.Position;
                message.Dialog = (InstantMessageDialog)im.MessageBlock.Dialog;
                message.GroupIM = im.MessageBlock.FromGroup;
                message.IMSessionID = im.MessageBlock.ID;
                message.Timestamp = new DateTime(im.MessageBlock.Timestamp);
                message.Message = Utils.BytesToString(im.MessageBlock.Message);
                message.Offline = (InstantMessageOnline)im.MessageBlock.Offline;
                message.BinaryBucket = im.MessageBlock.BinaryBucket;

                if (IsGroupMessage(message))
                {
                    lock (GroupChatSessions.Dictionary)
                    {
                        if (!GroupChatSessions.ContainsKey(message.IMSessionID))
                            GroupChatSessions.Add(message.IMSessionID, new List<ChatSessionMember>());
                    }
                }

                OnInstantMessage(new InstantMessageEventArgs(message, simulator));
            }
        }

        /// <summary>
        /// Take an incoming Chat packet, auto-parse, and if OnChat is defined call 
        ///   that with the appropriate arguments.
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ChatHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_Chat == null) return;
            Packet packet = e.Packet;

            ChatFromSimulatorPacket chat = (ChatFromSimulatorPacket)packet;

            OnChat(new ChatEventArgs(e.Simulator, Utils.BytesToString(chat.ChatData.Message),
                (ChatAudibleLevel)chat.ChatData.Audible,
                (ChatType)chat.ChatData.ChatType,
                (ChatSourceType)chat.ChatData.SourceType,
                Utils.BytesToString(chat.ChatData.FromName),
                chat.ChatData.SourceID,
                chat.ChatData.OwnerID,
                chat.ChatData.Position));
        }

        /// <summary>
        /// Used for parsing llDialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptDialogHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptDialog == null) return;
            Packet packet = e.Packet;

            ScriptDialogPacket dialog = (ScriptDialogPacket)packet;
            List<string> buttons = dialog.Buttons.Select(button => Utils.BytesToString(button.ButtonLabel)).ToList();

            UUID ownerID = UUID.Zero;

            if (dialog.OwnerData != null && dialog.OwnerData.Length > 0)
            {
                ownerID = dialog.OwnerData[0].OwnerID;
            }

            OnScriptDialog(new ScriptDialogEventArgs(Utils.BytesToString(dialog.Data.Message),
                Utils.BytesToString(dialog.Data.ObjectName),
                dialog.Data.ImageID,
                dialog.Data.ObjectID,
                Utils.BytesToString(dialog.Data.FirstName),
                Utils.BytesToString(dialog.Data.LastName),
                dialog.Data.ChatChannel,
                buttons,
                ownerID));
        }

        /// <summary>
        /// Used for parsing llRequestPermissions dialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptQuestionHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptQuestion == null) return;
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            ScriptQuestionPacket question = (ScriptQuestionPacket)packet;

            OnScriptQuestion(new ScriptQuestionEventArgs(simulator,
                question.Data.TaskID,
                question.Data.ItemID,
                Utils.BytesToString(question.Data.ObjectName),
                Utils.BytesToString(question.Data.ObjectOwner),
                (ScriptPermission)question.Data.Questions));
        }

        /// <summary>
        /// Handles Script Control changes when Script with permissions releases or takes a control
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        private void ScriptControlChangeHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptControl == null) return;
            Packet packet = e.Packet;

            ScriptControlChangePacket change = (ScriptControlChangePacket)packet;
            foreach (ScriptControlChangePacket.DataBlock data in change.Data)
            {
                OnScriptControlChange(new ScriptControlEventArgs((ScriptControlChange)data.Controls,
                    data.PassToAgent,
                    data.TakeControls));
            }
        }

        /// <summary>
        /// Used for parsing llLoadURL Dialogs
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void LoadURLHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_LoadURL == null) return;
            Packet packet = e.Packet;

            LoadURLPacket loadURL = (LoadURLPacket)packet;

            OnLoadURL(new LoadUrlEventArgs(
                Utils.BytesToString(loadURL.Data.ObjectName),
                loadURL.Data.ObjectID,
                loadURL.Data.OwnerID,
                loadURL.Data.OwnerIsGroup,
                Utils.BytesToString(loadURL.Data.Message),
                Utils.BytesToString(loadURL.Data.URL)
            ));
        }

        /// <summary>
        /// Update client's Position, LookAt and region handle from incoming packet
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        /// <remarks>This occurs when after an avatar moves into a new sim</remarks>
        private void MovementCompleteHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            
            // Use ResolveSimulator to get the correct simulator for this movement
            Simulator simulator = ResolveSimulator(e);

            AgentMovementCompletePacket movement = (AgentMovementCompletePacket)packet;

            relativePosition = movement.Data.Position;
            LastPositionUpdate = DateTime.UtcNow;
            Movement.Camera.LookDirection(movement.Data.LookAt);
            
            if (simulator != null)
            {
                simulator.Handle = movement.Data.RegionHandle;
                simulator.SimVersion = Utils.BytesToString(movement.SimData.ChannelVersion);
                simulator.AgentMovementComplete = true;
                
                // Notify crossing state machine if we're crossing
                NotifyMovementComplete(simulator);
                
                // Update per-simulator state
                UpdateSimulatorState(simulator);
                
                // Check if we're near borders and should connect to neighbors
                CheckAndConnectNeighbors();
                
                // Proactively establish child agents in neighboring regions
                ProactiveChildAgentSetup();
                
                // Clean up stale object tracking in ObjectManager
                Client.Objects.CleanupObjectTracking();
                
                // Clean up stale child agent tracking
                CleanupChildAgentTracking();
                
                Logger.Debug($"Movement complete in simulator {simulator.Name}", Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void HealthHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Health = ((HealthMessagePacket)packet).HealthData.Health;
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AgentDataUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            AgentDataUpdatePacket p = (AgentDataUpdatePacket)packet;

            if (p.AgentData.AgentID == simulator.Client.Self.AgentID)
            {
                FirstName = Utils.BytesToString(p.AgentData.FirstName);
                LastName = Utils.BytesToString(p.AgentData.LastName);
                ActiveGroup = p.AgentData.ActiveGroupID;
                ActiveGroupPowers = (GroupPowers)p.AgentData.GroupPowers;

                if (m_AgentData == null) return;

                string groupTitle = Utils.BytesToString(p.AgentData.GroupTitle);
                string groupName = Utils.BytesToString(p.AgentData.GroupName);

                OnAgentData(new AgentDataReplyEventArgs(FirstName, LastName, ActiveGroup, groupTitle, ActiveGroupPowers,
                    groupName));
            }
            else
            {
                Logger.Error($"Got an AgentDataUpdate packet for avatar {p.AgentData.AgentID}", Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MoneyBalanceReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;

            if (packet.Type == PacketType.MoneyBalanceReply)
            {
                MoneyBalanceReplyPacket reply = (MoneyBalanceReplyPacket)packet;
                this.Balance = reply.MoneyData.MoneyBalance;

                if (m_MoneyBalance != null)
                {
                    TransactionInfo transactionInfo = new TransactionInfo
                    {
                        TransactionType = reply.TransactionInfo.TransactionType,
                        SourceID = reply.TransactionInfo.SourceID,
                        IsSourceGroup = reply.TransactionInfo.IsSourceGroup,
                        DestID = reply.TransactionInfo.DestID,
                        IsDestGroup = reply.TransactionInfo.IsDestGroup,
                        Amount = reply.TransactionInfo.Amount,
                        ItemDescription = Utils.BytesToString(reply.TransactionInfo.ItemDescription)
                    };

                    OnMoneyBalanceReply(new MoneyBalanceReplyEventArgs(reply.MoneyData.TransactionID,
                        reply.MoneyData.TransactionSuccess,
                        reply.MoneyData.MoneyBalance,
                        reply.MoneyData.SquareMetersCredit,
                        reply.MoneyData.SquareMetersCommitted,
                        Utils.BytesToString(reply.MoneyData.Description),
                        transactionInfo));
                }
            }

            if (m_Balance != null)
            {
                OnBalance(new BalanceEventArgs(Balance));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void TeleportHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            Simulator simulator = e.Simulator;

            bool finished = false;
            TeleportFlags flags = TeleportFlags.Default;

            if (packet.Type == PacketType.TeleportStart)
            {
                TeleportStartPacket start = (TeleportStartPacket)packet;

                TeleportMessage = "Teleport started";
                flags = (TeleportFlags)start.Info.TeleportFlags;
                teleportStatus = TeleportStatus.Start;

                Logger.DebugLog($"TeleportStart received, Flags: {flags}", Client);
            }
            else if (packet.Type == PacketType.TeleportProgress)
            {
                TeleportProgressPacket progress = (TeleportProgressPacket)packet;

                TeleportMessage = Utils.BytesToString(progress.Info.Message);
                flags = (TeleportFlags)progress.Info.TeleportFlags;
                teleportStatus = TeleportStatus.Progress;

                Logger.DebugLog($"TeleportProgress received, Message: {TeleportMessage}, Flags: {flags}", Client);
            }
            else if (packet.Type == PacketType.TeleportFailed)
            {
                TeleportFailedPacket failed = (TeleportFailedPacket)packet;

                TeleportMessage = Utils.BytesToString(failed.Info.Reason);

                // expiry failure may come after teleport has finished. Ignore it.
                if (teleportStatus == TeleportStatus.Finished || teleportStatus == TeleportStatus.None)
                {
                    Logger.DebugLog($"Received TeleportFailed after teleport finished, Reason: {TeleportMessage}");
                    return;
                }

                teleportStatus = TeleportStatus.Failed;
                finished = true;

                Logger.DebugLog($"TeleportFailed received, Reason: {TeleportMessage}", Client);
            }
            else if (packet.Type == PacketType.TeleportFinish)
            {
                TeleportFinishPacket finish = (TeleportFinishPacket)packet;

                flags = (TeleportFlags)finish.Info.TeleportFlags;
                Uri seedcaps = new Uri(Utils.BytesToString(finish.Info.SeedCapability));
                finished = true;

                Logger.DebugLog($"TeleportFinish received, Flags: {flags}", Client);

                // Connect to the new sim
                Client.Network.CurrentSim.AgentMovementComplete = false; // we're not there anymore
                Simulator newSimulator = Client.Network.Connect(new IPAddress(finish.Info.SimIP),
                    finish.Info.SimPort, finish.Info.RegionHandle, true, seedcaps);

                if (newSimulator != null)
                {
                    TeleportMessage = "Teleport finished";
                    teleportStatus = TeleportStatus.Finished;

                    Logger.Info($"Moved to {newSimulator}", Client);
                }
                else
                {
                    TeleportMessage = $"Failed to connect to simulator after teleport";
                    teleportStatus = TeleportStatus.Failed;

                    // We're going to get disconnected now
                    Logger.Error(TeleportMessage, Client);
                }
            }
            else if (packet.Type == PacketType.TeleportCancel)
            {
                //TeleportCancelPacket cancel = (TeleportCancelPacket)packet;

                TeleportMessage = "Cancelled";
                teleportStatus = TeleportStatus.Cancelled;
                finished = true;

                Logger.DebugLog($"TeleportCancel received from {simulator}", Client);
            }
            else if (packet.Type == PacketType.TeleportLocal)
            {
                TeleportLocalPacket local = (TeleportLocalPacket)packet;

                TeleportMessage = "Teleport finished";
                flags = (TeleportFlags)local.Info.TeleportFlags;
                teleportStatus = TeleportStatus.Finished;
                relativePosition = local.Info.Position;
                LastPositionUpdate = DateTime.UtcNow;
                Movement.Camera.LookDirection(local.Info.LookAt);
                // This field is apparently not used for anything
                //local.Info.LocationID;
                finished = true;

                Logger.DebugLog($"TeleportLocal received, Flags: {flags}", Client);
            }

            if (m_Teleport != null)
            {
                OnTeleport(new TeleportEventArgs(TeleportMessage, teleportStatus, flags));
            }

            if (finished)
            {
                teleportEvent.Set();
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarAnimationHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            AvatarAnimationPacket animation = (AvatarAnimationPacket)packet;

            if (animation.Sender.ID == Client.Self.AgentID)
            {
                lock (SignaledAnimations.Dictionary)
                {
                    // Reset the signaled animation list
                    SignaledAnimations.Dictionary.Clear();

                    for (int i = 0; i < animation.AnimationList.Length; i++)
                    {
                        UUID animID = animation.AnimationList[i].AnimID;
                        int sequenceID = animation.AnimationList[i].AnimSequenceID;

                        // Add this animation to the list of currently signaled animations
                        SignaledAnimations.Dictionary[animID] = sequenceID;

                        if (i < animation.AnimationSourceList.Length)
                        {
                            // FIXME: The server tells us which objects triggered our animations,
                            // we should store this info

                            //animation.AnimationSourceList[i].ObjectID
                        }

                        if (i < animation.PhysicalAvatarEventList.Length)
                        {
                            // FIXME: What is this?
                        }

                        if (!Client.Settings.SEND_AGENT_UPDATES) continue;
                        // We have to manually tell the server to stop playing some animations
                        if (animID == Animations.STANDUP ||
                            animID == Animations.PRE_JUMP ||
                            animID == Animations.LAND ||
                            animID == Animations.MEDIUM_LAND)
                        {
                            Movement.FinishAnim = true;
                            Movement.SendUpdate(true);
                            Movement.FinishAnim = false;
                        }
                    }
                }
            }

            if (m_AnimationsChanged != null)
            {
                ThreadPool.QueueUserWorkItem(delegate (object o)
                {
                    OnAnimationsChanged(new AnimationsChangedEventArgs(this.SignaledAnimations));
                });
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void MeanCollisionAlertHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_MeanCollision == null) return;
            Packet packet = e.Packet;
            MeanCollisionAlertPacket collision = (MeanCollisionAlertPacket)packet;

            foreach (MeanCollisionAlertPacket.MeanCollisionBlock block in collision.MeanCollision)
            {
                DateTime time = Utils.UnixTimeToDateTime(block.Time);
                MeanCollisionType type = (MeanCollisionType)block.Type;

                OnMeanCollision(new MeanCollisionEventArgs(type, block.Perp, block.Victim, block.Mag, time));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        /// <remarks>This packet is now being sent via the EventQueue</remarks>
        protected void CrossedRegionHandler(object sender, PacketReceivedEventArgs e)
        {
            Packet packet = e.Packet;
            CrossedRegionPacket crossing = (CrossedRegionPacket)packet;
            Uri seedCap = new Uri(Utils.BytesToString(crossing.RegionData.SeedCapability));
            IPEndPoint endPoint = new IPEndPoint(crossing.RegionData.SimIP, crossing.RegionData.SimPort);

            Logger.Info($"Crossed in to new region area, attempting to connect to {endPoint}", Client);

            // Use ResolveSimulator to get the old simulator context
            Simulator oldSim = ResolveSimulator(e);
            
            // Use the state machine to handle the crossing
            if (!BeginRegionCrossing(oldSim, crossing.RegionData.RegionHandle, endPoint, seedCap, 
                crossing.Info.Position, crossing.Info.LookAt))
            {
                Logger.Warn($"Failed to begin region crossing to {endPoint}", Client);
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AlertMessage == null) return;
            Packet packet = e.Packet;

            AlertMessagePacket alert = (AlertMessagePacket)packet;

            string message = Utils.BytesToString(alert.AlertData.Message);

            if (alert.AlertInfo.Length > 0)
            {
                string notificationid = Utils.BytesToString(alert.AlertInfo[0].Message);
                OSDMap extra = (alert.AlertInfo[0].ExtraParams != null && alert.AlertInfo[0].ExtraParams.Length > 0)
                    ? OSDParser.Deserialize(alert.AlertInfo[0].ExtraParams) as OSDMap
                    : null;
                OnAlertMessage(new AlertMessageEventArgs(message, notificationid, extra));
            }
            else
            {
                OnAlertMessage(new AlertMessageEventArgs(message, null, null));
            }
        }

        protected void AgentAlertMessageHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AlertMessage == null) return;
            Packet packet = e.Packet;

            AgentAlertMessagePacket alert = (AgentAlertMessagePacket)packet;
            // HACK: Agent alerts support modal and Generic Alerts do not, but it's all the same for
            //       my simplified ass right now.
            OnAlertMessage(new AlertMessageEventArgs(Utils.BytesToString(alert.AlertData.Message), null, null));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void CameraConstraintHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_CameraConstraint == null) return;
            Packet packet = e.Packet;

            CameraConstraintPacket camera = (CameraConstraintPacket)packet;
            OnCameraConstraint(new CameraConstraintEventArgs(camera.CameraCollidePlane.Plane));
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void ScriptSensorReplyHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_ScriptSensorReply == null) return;
            Packet packet = e.Packet;

            ScriptSensorReplyPacket reply = (ScriptSensorReplyPacket)packet;

            foreach (ScriptSensorReplyPacket.SensedDataBlock block in reply.SensedData)
            {
                ScriptSensorReplyPacket.RequesterBlock requestor = reply.Requester;

                OnScriptSensorReply(new ScriptSensorReplyEventArgs(requestor.SourceID, block.GroupID,
                    Utils.BytesToString(block.Name),
                    block.ObjectID, block.OwnerID, block.Position, block.Range, block.Rotation,
                    (ScriptSensorTypeFlags)block.Type, block.Velocity));
            }
        }

        /// <summary>Process an incoming packet and raise the appropriate events</summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs object containing the packet data</param>
        protected void AvatarSitResponseHandler(object sender, PacketReceivedEventArgs e)
        {
            if (m_AvatarSitResponse == null) return;
            Packet packet = e.Packet;

            AvatarSitResponsePacket sit = (AvatarSitResponsePacket)packet;

            OnAvatarSitResponse(new AvatarSitResponseEventArgs(sit.SitObject.ID, sit.SitTransform.AutoPilot,
                sit.SitTransform.CameraAtOffset,
                sit.SitTransform.CameraEyeOffset, sit.SitTransform.ForceMouselook, sit.SitTransform.SitPosition,
                sit.SitTransform.SitRotation));
        }

        protected void MuteListUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            MuteListUpdatePacket packet = (MuteListUpdatePacket)e.Packet;
            if (packet.MuteData.AgentID != Client.Self.AgentID)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(sync =>
            {
                using (AutoResetEvent gotMuteList = new AutoResetEvent(false))
                {
                    string fileName = Utils.BytesToString(packet.MuteData.Filename);
                    string muteList = string.Empty;
                    ulong xferID = 0;
                    byte[] assetData = null;

                    EventHandler<XferReceivedEventArgs> xferCallback = (object xsender, XferReceivedEventArgs xe) =>
                    {
                        if (xe.Xfer.XferID != xferID) return;
                        assetData = xe.Xfer.AssetData;
                        gotMuteList.Set();
                    };


                    Client.Assets.XferReceived += xferCallback;
                    xferID = Client.Assets.RequestAssetXfer(fileName, true, false, UUID.Zero, AssetType.Unknown, true);

                    if (gotMuteList.WaitOne(TimeSpan.FromMinutes(1), false))
                    {
                        muteList = Utils.BytesToString(assetData);

                        lock (MuteList.Dictionary)
                        {
                            MuteList.Dictionary.Clear();
                            foreach (var line in muteList.Split('\n'))
                            {
                                if (line.Trim() == string.Empty) continue;

                                try
                                {
                                    Match m;
                                    if ((m = Regex.Match(line,
                                            @"(?<MyteType>\d+)\s+(?<Key>[a-zA-Z0-9-]+)\s+(?<Name>[^|]+)|(?<Flags>.+)",
                                            RegexOptions.CultureInvariant)).Success)
                                    {
                                        MuteEntry me = new MuteEntry
                                        {
                                            Type = (MuteType)int.Parse(m.Groups["MyteType"].Value),
                                            ID = new UUID(m.Groups["Key"].Value),
                                            Name = m.Groups["Name"].Value
                                        };
                                        int flags = 0;
                                        int.TryParse(m.Groups["Flags"].Value, out flags);
                                        me.Flags = (MuteFlags)flags;
                                        MuteList[$"{me.ID}|{me.Name}"] = me;
                                    }
                                    else
                                    {
                                        throw new ArgumentException("Invalid mutelist entry line");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Warn("Failed to parse the mute list line: " + line, ex, Client);
                                }
                            }
                        }

                        OnMuteListUpdated(EventArgs.Empty);
                    }
                    else
                    {
                        Logger.Warn("Timed out waiting for mute list download", Client);
                    }

                    Client.Assets.XferReceived -= xferCallback;
                }
            });
        }

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

            // Resolve the correct simulator context using message data
            Simulator contextSim = ResolveSimulatorFromMessage(msg, simulator);
            
            IPEndPoint endPoint = new IPEndPoint(msg.Address, msg.Port);
            Simulator sim = Client.Network.FindSimulator(endPoint);

            if (sim == null)
            {
                Logger.Info($"EstablishAgentCommunication for unknown sim {msg.Address}:{msg.Port}, attempting to connect", Client);
                
                // Attempt to connect to the new simulator
                // Note: We don't have region handle in this message, so we need to handle that gracefully
                // The sim will get the handle when it connects
                sim = Client.Network.Connect(endPoint, 0, false, msg.SeedCapability, 
                    Simulator.DefaultRegionSizeX, Simulator.DefaultRegionSizeY);
                
                if (sim == null)
                {
                    Logger.Error($"Failed to connect to new sim {msg.Address}:{msg.Port}", Client);
                }
                else
                {
                    Logger.Info($"Successfully connected to new sim {sim}", Client);
                }
            }
            else
            {
                Logger.Info($"Got EstablishAgentCommunication for {sim}, updating seed capability", Client);
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

            // Resolve the simulator context
            Simulator resolvedSim = ResolveSimulatorFromMessage(msg, simulator);

            TeleportFailedPacket failedPacket = new TeleportFailedPacket
            {
                Info =
                {
                    AgentID = msg.AgentID,
                    Reason = Utils.StringToBytes(msg.Reason)
                }
            };

            TeleportHandler(this, new PacketReceivedEventArgs(failedPacket, resolvedSim));
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
            
            // Resolve the correct simulator for this teleport finish event
            Simulator resolvedSim = ResolveSimulatorFromMessage(msg, simulator);

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

            // Pass the resolved simulator to the teleport handler
            TeleportHandler(this, new PacketReceivedEventArgs(p, resolvedSim));
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

            // Resolve the simulator context - the old simulator from which we're crossing
            Simulator oldSim = ResolveSimulatorFromMessage(crossed, simulator);
            
            // Use the state machine to handle the crossing
            if (!BeginRegionCrossing(oldSim, crossed.RegionHandle, endPoint, crossed.SeedCapability,
                crossed.Position, crossed.LookAt))
            {
                Logger.Warn($"Failed to begin region crossing to {endPoint}", Client);
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
