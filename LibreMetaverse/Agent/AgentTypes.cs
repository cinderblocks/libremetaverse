/**
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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

namespace OpenMetaverse
{
    [Flags]
    public enum ScriptPermission : int
    {
        None = 0,
        Debit = 1 << 1,
        TakeControls = 1 << 2,
        RemapControls = 1 << 3,
        TriggerAnimation = 1 << 4,
        Attach = 1 << 5,
        ReleaseOwnership = 1 << 6,
        ChangeLinks = 1 << 7,
        ChangeJoints = 1 << 8,
        ChangePermissions = 1 << 9,
        TrackCamera = 1 << 10,
        ControlCamera = 1 << 11,
        Teleport = 1 << 12
    }

    public enum InstantMessageDialog : byte
    {
        MessageFromAgent = 0,
        MessageBox = 1,
        GroupInvitation = 3,
        InventoryOffered = 4,
        InventoryAccepted = 5,
        InventoryDeclined = 6,
        GroupVote = 7,
        TaskInventoryOffered = 9,
        TaskInventoryAccepted = 10,
        TaskInventoryDeclined = 11,
        NewUserDefault = 12,
        SessionAdd = 13,
        SessionOfflineAdd = 14,
        SessionGroupStart = 15,
        SessionCardlessStart = 16,
        SessionSend = 17,
        SessionDrop = 18,
        MessageFromObject = 19,
        BusyAutoResponse = 20,
        ConsoleAndChatHistory = 21,
        RequestTeleport = 22,
        AcceptTeleport = 23,
        DenyTeleport = 24,
        GodLikeRequestTeleport = 25,
        RequestLure = 26,
        GotoUrl = 28,
        Session911Start = 29,
        Lure911 = 30,
        FromTaskAsAlert = 31,
        GroupNotice = 32,
        GroupNoticeInventoryAccepted = 33,
        GroupNoticeInventoryDeclined = 34,
        GroupInvitationAccept = 35,
        GroupInvitationDecline = 36,
        GroupNoticeRequested = 37,
        FriendshipOffered = 38,
        FriendshipAccepted = 39,
        FriendshipDeclined = 40,
        StartTyping = 41,
        StopTyping = 42
    }

    public enum InstantMessageOnline
    {
        Online = 0,
        Offline = 1
    }

    public enum ChatType : byte
    {
        Whisper = 0,
        Normal = 1,
        Shout = 2,
        StartTyping = 4,
        StopTyping = 5,
        Debug = 6,
        OwnerSay = 8,
        RegionSayTo = 9,
        RegionSay = byte.MaxValue,
    }

    public enum ChatSourceType : byte
    {
        System = 0,
        Agent = 1,
        Object = 2
    }

    public enum ChatAudibleLevel : sbyte
    {
        Not = -1,
        Barely = 0,
        Fully = 1
    }

    public enum EffectType : byte
    {
        Text = 0,
        Icon,
        Connector,
        FlexibleObject,
        AnimalControls,
        AnimationObject,
        Cloth,
        Beam,
        Glow,
        Point,
        Trail,
        Sphere,
        Spiral,
        Edit,
        LookAt,
        PointAt
    }

    public enum LookAtType : byte
    {
        None,
        Idle,
        AutoListen,
        FreeLook,
        Respond,
        Hover,
        [Obsolete]
        Conversation,
        Select,
        Focus,
        Mouselook,
        Clear
    }

    public enum PointAtType : byte
    {
        None,
        Select,
        Grab,
        Clear
    }

    public enum MoneyTransactionType : int
    {
        None = 0,
        FailSimulatorTimeout = 1,
        FailDataserverTimeout = 2,
        ObjectClaim = 1000,
        LandClaim = 1001,
        GroupCreate = 1002,
        ObjectPublicClaim = 1003,
        GroupJoin = 1004,
        TeleportCharge = 1100,
        UploadCharge = 1101,
        LandAuction = 1102,
        ClassifiedCharge = 1103,
        ObjectTax = 2000,
        LandTax = 2001,
        LightTax = 2002,
        ParcelDirFee = 2003,
        GroupTax = 2004,
        ClassifiedRenew = 2005,
        GiveInventory = 3000,
        ObjectSale = 5000,
        Gift = 5001,
        LandSale = 5002,
        ReferBonus = 5003,
        InventorySale = 5004,
        RefundPurchase = 5005,
        LandPassSale = 5006,
        DwellBonus = 5007,
        PayObject = 5008,
        ObjectPays = 5009,
        GroupLandDeed = 6001,
        GroupObjectDeed = 6002,
        GroupLiability = 6003,
        GroupDividend = 6004,
        GroupMembershipDues = 6005,
        ObjectRelease = 8000,
        LandRelease = 8001,
        ObjectDelete = 8002,
        ObjectPublicDecay = 8003,
        ObjectPublicDelete = 8004,
        LindenAdjustment = 9000,
        LindenGrant = 9001,
        LindenPenalty = 9002,
        EventFee = 9003,
        EventPrize = 9004,
        StipendBasic = 10000,
        StipendDeveloper = 10001,
        StipendAlways = 10002,
        StipendDaily = 10003,
        StipendRating = 10004,
        StipendDelta = 10005
    }

    [Flags]
    public enum TransactionFlags : byte
    {
        None = 0,
        SourceGroup = 1,
        DestGroup = 2,
        OwnerGroup = 4,
        SimultaneousContribution = 8,
        ContributionRemoval = 16
    }

    public enum MeanCollisionType : byte
    {
        None,
        Bump,
        LLPushObject,
        SelectedObjectCollide,
        ScriptedObjectCollide,
        PhysicalObjectCollide
    }

    [Flags]
    public enum ScriptControlChange : uint
    {
        None = 0,
        Forward = 1,
        Back = 2,
        Left = 4,
        Right = 8,
        Up = 16,
        Down = 32,
        RotateLeft = 256,
        RotateRight = 512,
        LeftButton = 268435456,
        MouseLookLeftButton = 1073741824
    }

    [Flags]
    public enum AgentFlags : byte
    {
        None = 0,
        HideTitle = 0x01,
    }

    [Flags]
    public enum AgentState : byte
    {
        None = 0x00,
        Typing = 0x04,
        Editing = 0x10
    }

    public enum TeleportStatus
    {
        None,
        Start,
        Progress,
        Failed,
        Finished,
        Cancelled
    }

    [Flags]
    public enum TeleportFlags : uint
    {
        Default = 0,
        SetHomeToTarget = 1 << 0,
        SetLastToTarget = 1 << 1,
        ViaLure = 1 << 2,
        ViaLandmark = 1 << 3,
        ViaLocation = 1 << 4,
        ViaHome = 1 << 5,
        ViaTelehub = 1 << 6,
        ViaLogin = 1 << 7,
        ViaGodlikeLure = 1 << 8,
        Godlike = 1 << 9,
        NineOneOne = 1 << 10,
        DisableCancel = 1 << 11,
        ViaRegionID = 1 << 12,
        IsFlying = 1 << 13,
        ResetHome = 1 << 14,
        ForceRedirect = 1 << 15,
        FinishedViaLure = 1 << 26,
        FinishedViaNewSim = 1 << 28,
        FinishedViaSameSim = 1 << 29
    }

    [Flags]
    public enum TeleportLureFlags
    {
        NormalLure = 0,
        GodlikeLure = 1,
        GodlikePursuit = 2
    }

    [Flags]
    public enum ScriptSensorTypeFlags
    {
        Agent = 1,
        Active = 2,
        Passive = 4,
        Scripted = 8,
    }

    public enum MuteType
    {
        ByName = 0,
        Resident = 1,
        Object = 2,
        Group = 3,
        External = 4
    }

    [Flags]
    public enum MuteFlags : int
    {
        Default = 0x0,
        TextChat = 0x1,
        VoiceChat = 0x2,
        Particles = 0x4,
        ObjectSounds = 0x8,
        All = 0xf
    }

    public struct InstantMessage
    {
        public UUID FromAgentID;
        public string FromAgentName;
        public UUID ToAgentID;
        public uint ParentEstateID;
        public UUID RegionID;
        public Vector3 Position;
        public InstantMessageDialog Dialog;
        public bool GroupIM;
        public UUID IMSessionID;
        public DateTime Timestamp;
        public string Message;
        public InstantMessageOnline Offline;
        public byte[] BinaryBucket;

        public override string ToString()
        {
            return Helpers.StructToString(this);
        }
    }

    public class MuteEntry
    {
        public MuteType Type;
        public UUID ID;
        public string Name;
        public MuteFlags Flags;
    }

    public class TransactionInfo
    {
        public int TransactionType;
        public UUID SourceID;
        public bool IsSourceGroup;
        public UUID DestID;
        public bool IsDestGroup;
        public int Amount;
        public string ItemDescription;
    }
}
