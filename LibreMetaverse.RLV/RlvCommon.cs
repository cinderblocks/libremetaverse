using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace LibreMetaverse.RLV
{
    public static class RlvCommon
    {
        internal static readonly ImmutableDictionary<string, RlvWearableType> RlvWearableTypeMap = new Dictionary<string, RlvWearableType>()
        {
            {"gloves", RlvWearableType.Gloves},
            {"jacket", RlvWearableType.Jacket},
            {"pants", RlvWearableType.Pants},
            {"shirt", RlvWearableType.Shirt},
            {"shoes", RlvWearableType.Shoes},
            {"skirt", RlvWearableType.Skirt},
            {"socks", RlvWearableType.Socks},
            {"underpants", RlvWearableType.Underpants},
            {"undershirt", RlvWearableType.Undershirt},
            {"skin", RlvWearableType.Skin},
            {"eyes", RlvWearableType.Eyes},
            {"hair", RlvWearableType.Hair},
            {"shape", RlvWearableType.Shape},
            {"alpha", RlvWearableType.Alpha},
            {"tattoo", RlvWearableType.Tattoo},
            {"physics", RlvWearableType.Physics },
            {"universal", RlvWearableType.Universal},
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

        internal static readonly ImmutableDictionary<string, RlvAttachmentPoint> RlvAttachmentPointMap = new Dictionary<string, RlvAttachmentPoint>()
        {
            {"None", RlvAttachmentPoint.Default},
            {"Chest", RlvAttachmentPoint.Chest},
            {"Skull", RlvAttachmentPoint.Skull},
            {"Left Shoulder", RlvAttachmentPoint.LeftShoulder},
            {"Right Shoulder", RlvAttachmentPoint.RightShoulder},
            {"Left Hand", RlvAttachmentPoint.LeftHand},
            {"Right Hand", RlvAttachmentPoint.RightHand},
            {"Left Foot", RlvAttachmentPoint.LeftFoot},
            {"Right Foot", RlvAttachmentPoint.RightFoot},
            {"Spine", RlvAttachmentPoint.Spine},
            {"Pelvis", RlvAttachmentPoint.Pelvis},
            {"Mouth", RlvAttachmentPoint.Mouth},
            {"Chin", RlvAttachmentPoint.Chin},
            {"Left Ear", RlvAttachmentPoint.LeftEar},
            {"Right Ear", RlvAttachmentPoint.RightEar},
            {"Left Eyeball", RlvAttachmentPoint.LeftEyeball},
            {"Right Eyeball", RlvAttachmentPoint.RightEyeball},
            {"Nose", RlvAttachmentPoint.Nose},
            {"R Upper Arm", RlvAttachmentPoint.RightUpperArm},
            {"R Forearm", RlvAttachmentPoint.RightForearm},
            {"L Upper Arm", RlvAttachmentPoint.LeftUpperArm},
            {"L Forearm", RlvAttachmentPoint.LeftForearm},
            {"Right Hip", RlvAttachmentPoint.RightHip},
            {"R Upper Leg", RlvAttachmentPoint.RightUpperLeg},
            {"R Lower Leg", RlvAttachmentPoint.RightLowerLeg},
            {"Left Hip", RlvAttachmentPoint.LeftHip},
            {"L Upper Leg", RlvAttachmentPoint.LeftUpperLeg},
            {"L Lower Leg", RlvAttachmentPoint.LeftLowerLeg},
            {"Stomach", RlvAttachmentPoint.Stomach},
            {"Left Pec", RlvAttachmentPoint.LeftPec},
            {"Right Pec", RlvAttachmentPoint.RightPec},
            {"Center 2", RlvAttachmentPoint.HUDCenter2},
            {"Top Right", RlvAttachmentPoint.HUDTopRight},
            {"Top", RlvAttachmentPoint.HUDTop},
            {"Top Left", RlvAttachmentPoint.HUDTopLeft},
            {"Center", RlvAttachmentPoint.HUDCenter},
            {"Bottom Left", RlvAttachmentPoint.HUDBottomLeft},
            {"Bottom", RlvAttachmentPoint.HUDBottom},
            {"Bottom Right", RlvAttachmentPoint.HUDBottomRight},
            {"Neck", RlvAttachmentPoint.Neck},
            {"Root", RlvAttachmentPoint.AvatarCenter}, // RLV hack, "root" is "avatar center"
            {"Avatar Center", RlvAttachmentPoint.AvatarCenter},
            {"Left Ring Finger", RlvAttachmentPoint.LeftHandRing},
            {"Right Ring Finger", RlvAttachmentPoint.RightHandRing},
            {"Tail Base", RlvAttachmentPoint.TailBase},
            {"Tail Tip", RlvAttachmentPoint.TailTip},
            {"Left Wing", RlvAttachmentPoint.LeftWing},
            {"Right Wing", RlvAttachmentPoint.RightWing},
            {"Jaw", RlvAttachmentPoint.Jaw},
            {"Alt Left Ear", RlvAttachmentPoint.AltLeftEar},
            {"Alt Right Ear", RlvAttachmentPoint.AltRightEar},
            {"Alt Left Eye", RlvAttachmentPoint.AltLeftEye},
            {"Alt Right Eye", RlvAttachmentPoint.AltRightEye},
            {"Tongue", RlvAttachmentPoint.Tongue},
            {"Groin", RlvAttachmentPoint.Groin},
            {"Left Hind Foot", RlvAttachmentPoint.LeftHindFoot},
            {"Right Hind Foot", RlvAttachmentPoint.RightHindFoot},
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

        internal static readonly ImmutableDictionary<RlvAttachmentPoint, string> RlvAttachmentPointToNameMap = RlvAttachmentPointMap
            .Where(n => n.Key != "Root") // Skip rlv hack otherwise we have duplicate keys for AvatarCenter
            .ToImmutableDictionary(k => k.Value, v => v.Key);

        private static readonly Regex _attachmentPointTagRegex = new(@"\((?<tag>[^\)]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static bool TryGetAttachmentPointFromItemName(string itemName, [NotNullWhen(true)] out RlvAttachmentPoint? attachmentPoint)
        {
            attachmentPoint = null;

            var attachmentPointTag = _attachmentPointTagRegex
                .Matches(itemName)
                .Cast<Match>()
                .Where(n => n.Success && n.Groups["tag"].Success)
                .Select(n => n.Groups["tag"].Value)
                .ToList();

            for (var i = attachmentPointTag.Count - 1; i >= 0; i--)
            {
                if (RlvAttachmentPointMap.TryGetValue(attachmentPointTag[i].ToLowerInvariant(), out var attachmentPointTemp))
                {
                    attachmentPoint = attachmentPointTemp;
                    return true;
                }
            }

            return false;
        }
    }
}
