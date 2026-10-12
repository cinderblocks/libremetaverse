/*
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

namespace LibreMetaverse
{
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

    public enum InstantMessageOnline
    {
        Online = 0,
        Offline = 1
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

    public enum PointAtType : byte
    {
        None,
        Select,
        Grab,
        Clear
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

    public enum ChatAudibleLevel : sbyte
    {
        Not = -1,
        Barely = 0,
        Fully = 1
    }

    [Flags]
    public enum AgentState : byte
    {
        None = 0x00,
        Typing = 0x04,
        Editing = 0x10
    }

    [Flags]
    public enum AgentFlags : byte
    {
        None = 0,
        HideTitle = 0x01,
    }
}
