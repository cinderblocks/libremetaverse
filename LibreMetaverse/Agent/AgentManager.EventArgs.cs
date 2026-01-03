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
using System.Collections.Generic;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    public class AgentAccessEventArgs : EventArgs
    {
        public string NewLevel { get; }
        public bool Success { get; }
        public AgentAccessEventArgs(bool success, string newLevel)
        {
            NewLevel = newLevel;
            Success = success;
        }
    }

    public class ChatEventArgs : EventArgs
    {
        public Simulator Simulator { get; }
        public string Message { get; }
        public ChatAudibleLevel AudibleLevel { get; }
        public ChatType Type { get; }
        public ChatSourceType SourceType { get; }
        public string FromName { get; }
        public UUID SourceID { get; }
        public UUID OwnerID { get; }
        public Vector3 Position { get; }

        public ChatEventArgs(Simulator simulator, string message, ChatAudibleLevel audible, ChatType type,
            ChatSourceType sourceType, string fromName, UUID sourceId, UUID ownerid, Vector3 position)
        {
            Simulator = simulator;
            Message = message;
            AudibleLevel = audible;
            Type = type;
            SourceType = sourceType;
            FromName = fromName;
            SourceID = sourceId;
            Position = position;
            OwnerID = ownerid;
        }

        public override string ToString() => $"[ChatEvent: Sim={Simulator}, Message={Message}, AudibleLevel={AudibleLevel}, Type={Type}, SourceType={SourceType}, FromName={FromName}, SourceID={SourceID}, Position={Position}, OwnerID={OwnerID}]";
    }

    public class ScriptDialogEventArgs : EventArgs
    {
        public string Message { get; }
        public string ObjectName { get; }
        public UUID ImageID { get; }
        public UUID ObjectID { get; }
        public string FirstName { get; }
        public string LastName { get; }
        public int Channel { get; }
        public List<string> ButtonLabels { get; }
        public UUID OwnerID { get; }

        public ScriptDialogEventArgs(string message, string objectName, UUID imageID,
            UUID objectID, string firstName, string lastName, int chatChannel, List<string> buttons, UUID ownerID)
        {
            Message = message;
            ObjectName = objectName;
            ImageID = imageID;
            ObjectID = objectID;
            FirstName = firstName;
            LastName = lastName;
            Channel = chatChannel;
            ButtonLabels = buttons;
            OwnerID = ownerID;
        }
    }

    public class ScriptQuestionEventArgs : EventArgs
    {
        public Simulator Simulator { get; }
        public UUID TaskID { get; }
        public UUID ItemID { get; }
        public string ObjectName { get; }
        public string ObjectOwnerName { get; }
        public ScriptPermission Questions { get; }

        public ScriptQuestionEventArgs(Simulator simulator, UUID taskID, UUID itemID, string objectName, string objectOwner, ScriptPermission questions)
        {
            Simulator = simulator;
            TaskID = taskID;
            ItemID = itemID;
            ObjectName = objectName;
            ObjectOwnerName = objectOwner;
            Questions = questions;
        }
    }

    public class LoadUrlEventArgs : EventArgs
    {
        public string ObjectName { get; }
        public UUID ObjectID { get; }
        public UUID OwnerID { get; }
        public bool OwnerIsGroup { get; }
        public string Message { get; }
        public string URL { get; }

        public LoadUrlEventArgs(string objectName, UUID objectID, UUID ownerID, bool ownerIsGroup, string message, string url)
        {
            ObjectName = objectName;
            ObjectID = objectID;
            OwnerID = ownerID;
            OwnerIsGroup = ownerIsGroup;
            Message = message;
            URL = url;
        }
    }

    public class InstantMessageEventArgs : EventArgs
    {
        public InstantMessage IM { get; }
        public Simulator Simulator { get; }
        public InstantMessageEventArgs(InstantMessage im, Simulator simulator)
        {
            IM = im;
            Simulator = simulator;
        }
    }

    public class BalanceEventArgs : EventArgs
    {
        public int Balance { get; }
        public BalanceEventArgs(int balance) { Balance = balance; }
    }

    public class MoneyBalanceReplyEventArgs : EventArgs
    {
        public UUID TransactionID { get; }
        public bool Success { get; }
        public int Balance { get; }
        public int MetersCredit { get; }
        public int MetersCommitted { get; }
        public string Description { get; }
        public TransactionInfo TransactionInfo { get; }

        public MoneyBalanceReplyEventArgs(UUID transactionID, bool transactionSuccess, int balance, int metersCredit, int metersCommitted, string description, TransactionInfo transactionInfo)
        {
            TransactionID = transactionID;
            Success = transactionSuccess;
            Balance = balance;
            MetersCredit = metersCredit;
            MetersCommitted = metersCommitted;
            Description = description;
            TransactionInfo = transactionInfo;
        }
    }

    public class TeleportEventArgs : EventArgs
    {
        public string Message { get; }
        public TeleportStatus Status { get; }
        public TeleportFlags Flags { get; }

        public TeleportEventArgs(string message, TeleportStatus status, TeleportFlags flags)
        {
            Message = message;
            Status = status;
            Flags = flags;
        }
    }

    public class AgentDataReplyEventArgs : EventArgs
    {
        public string FirstName { get; }
        public string LastName { get; }
        public UUID ActiveGroupID { get; }
        public string GroupTitle { get; }
        public GroupPowers GroupPowers { get; }
        public string GroupName { get; }

        public AgentDataReplyEventArgs(string firstName, string lastName, UUID activeGroupID,
            string groupTitle, GroupPowers groupPowers, string groupName)
        {
            FirstName = firstName;
            LastName = lastName;
            ActiveGroupID = activeGroupID;
            GroupTitle = groupTitle;
            GroupPowers = groupPowers;
            GroupName = groupName;
        }
    }

    public class AnimationsChangedEventArgs : EventArgs
    {
        public LockingDictionary<UUID, int> Animations { get; }
        public AnimationsChangedEventArgs(LockingDictionary<UUID, int> agentAnimations) { Animations = agentAnimations; }
    }

    public class MeanCollisionEventArgs : EventArgs
    {
        public MeanCollisionType Type { get; }
        public UUID Aggressor { get; }
        public UUID Victim { get; }
        public float Magnitude { get; }
        public DateTime Time { get; }

        public MeanCollisionEventArgs(MeanCollisionType type, UUID perp, UUID victim, float magnitude, DateTime time)
        {
            Type = type; Aggressor = perp; Victim = victim; Magnitude = magnitude; Time = time;
        }
    }

    public class RegionCrossedEventArgs : EventArgs
    {
        public Simulator OldSimulator { get; }
        public Simulator NewSimulator { get; }
        public RegionCrossedEventArgs(Simulator oldSim, Simulator newSim) { OldSimulator = oldSim; NewSimulator = newSim; }
    }

    public class GroupChatJoinedEventArgs : EventArgs
    {
        public UUID SessionID { get; }
        public string SessionName { get; }
        public UUID TmpSessionID { get; }
        public bool Success { get; }
        public GroupChatJoinedEventArgs(UUID groupChatSessionID, string sessionName, UUID tmpSessionID, bool success)
        {
            SessionID = groupChatSessionID; SessionName = sessionName; TmpSessionID = tmpSessionID; Success = success;
        }
    }

    public class AlertMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public string NotificationId { get; }
        public OSDMap ExtraParams { get; }
        public AlertMessageEventArgs(string message, string notificationid, OSDMap extraparams)
        {
            Message = message; NotificationId = notificationid; ExtraParams = extraparams;
        }
    }

    public class ScriptControlEventArgs : EventArgs
    {
        public ScriptControlChange Controls { get; }
        public bool Pass { get; }
        public bool Take { get; }
        public ScriptControlEventArgs(ScriptControlChange controls, bool pass, bool take)
        {
            Controls = controls; Pass = pass; Take = take;
        }
    }

    public class CameraConstraintEventArgs : EventArgs
    {
        public Vector4 CollidePlane { get; }
        public CameraConstraintEventArgs(Vector4 collidePlane) { CollidePlane = collidePlane; }
    }

    public class ScriptSensorReplyEventArgs : EventArgs
    {
        public UUID RequesterID { get; }
        public UUID GroupID { get; }
        public string Name { get; }
        public UUID ObjectID { get; }
        public UUID OwnerID { get; }
        public Vector3 Position { get; }
        public float Range { get; }
        public Quaternion Rotation { get; }
        public ScriptSensorTypeFlags Type { get; }
        public Vector3 Velocity { get; }

        public ScriptSensorReplyEventArgs(UUID requesterId, UUID groupID, string name,
            UUID objectID, UUID ownerID, Vector3 position, float range, Quaternion rotation,
            ScriptSensorTypeFlags type, Vector3 velocity)
        {
            RequesterID = requesterId; GroupID = groupID; Name = name; ObjectID = objectID; OwnerID = ownerID;
            Position = position; Range = range; Rotation = rotation; Type = type; Velocity = velocity;
        }
    }

    public class AvatarSitResponseEventArgs : EventArgs
    {
        public UUID ObjectID { get; }
        public bool Autopilot { get; }
        public Vector3 CameraAtOffset { get; }
        public Vector3 CameraEyeOffset { get; }
        public bool ForceMouselook { get; }
        public Vector3 SitPosition { get; }
        public Quaternion SitRotation { get; }

        public AvatarSitResponseEventArgs(UUID objectID, bool autoPilot, Vector3 cameraAtOffset,
            Vector3 cameraEyeOffset, bool forceMouselook, Vector3 sitPosition, Quaternion sitRotation)
        {
            ObjectID = objectID; Autopilot = autoPilot; CameraAtOffset = cameraAtOffset; CameraEyeOffset = cameraEyeOffset;
            ForceMouselook = forceMouselook; SitPosition = sitPosition; SitRotation = sitRotation;
        }
    }

    public class ChatSessionMemberAddedEventArgs : EventArgs
    {
        public UUID SessionID { get; }
        public UUID AgentID { get; }
        public ChatSessionMemberAddedEventArgs(UUID sessionID, UUID agentID) { SessionID = sessionID; AgentID = agentID; }
    }

    public class ChatSessionMemberLeftEventArgs : EventArgs
    {
        public UUID SessionID { get; }
        public UUID AgentID { get; }
        public ChatSessionMemberLeftEventArgs(UUID sessionID, UUID agentID) { SessionID = sessionID; AgentID = agentID; }
    }

    public class SetDisplayNameReplyEventArgs : EventArgs
    {
        public int Status { get; }
        public string Reason { get; }
        public AgentDisplayName DisplayName { get; }
        public SetDisplayNameReplyEventArgs(int status, string reason, AgentDisplayName displayName)
        {
            Status = status; Reason = reason; DisplayName = displayName;
        }
    }

    /// <summary>
    /// Direction an agent is crossing into a new region
    /// </summary>
    public enum BorderCrossingDirection
    {
        Unknown,
        North,
        South,
        East,
        West
    }

    /// <summary>
    /// Event args for region crossing prediction
    /// </summary>
    public class RegionCrossingPredictionEventArgs : EventArgs
    {
        public Simulator CurrentSimulator { get; }
        public BorderCrossingDirection Direction { get; }
        public float TimeUntilCrossing { get; }

        public RegionCrossingPredictionEventArgs(Simulator currentSim, BorderCrossingDirection direction, float timeUntilCrossing)
        {
            CurrentSimulator = currentSim;
            Direction = direction;
            TimeUntilCrossing = timeUntilCrossing;
        }
    }
}