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
            {"none", RlvAttachmentPoint.Default},
            {"chest", RlvAttachmentPoint.Chest },
            {"skull", RlvAttachmentPoint.Skull},
            {"left shoulder", RlvAttachmentPoint.LeftShoulder},
            {"right shoulder", RlvAttachmentPoint.RightShoulder},
            {"left hand", RlvAttachmentPoint.LeftHand},
            {"right hand", RlvAttachmentPoint.RightHand},
            {"left foot", RlvAttachmentPoint.LeftFoot},
            {"right foot", RlvAttachmentPoint.RightFoot},
            {"spine", RlvAttachmentPoint.Spine},
            {"pelvis", RlvAttachmentPoint.Pelvis},
            {"mouth", RlvAttachmentPoint.Mouth},
            {"chin", RlvAttachmentPoint.Chin},
            {"left ear", RlvAttachmentPoint.LeftEar},
            {"right ear", RlvAttachmentPoint.RightEar},
            {"left eyeball", RlvAttachmentPoint.LeftEyeball},
            {"right eyeball", RlvAttachmentPoint.RightEyeball},
            {"nose", RlvAttachmentPoint.Nose},
            {"r upper arm", RlvAttachmentPoint.RightUpperArm},
            {"r forearm", RlvAttachmentPoint.RightForearm},
            {"l upper arm", RlvAttachmentPoint.LeftUpperArm},
            {"l forearm", RlvAttachmentPoint.LeftForearm},
            {"right hip", RlvAttachmentPoint.RightHip},
            {"r upper leg", RlvAttachmentPoint.RightUpperLeg},
            {"r lower leg", RlvAttachmentPoint.RightLowerLeg},
            {"left hip", RlvAttachmentPoint.LeftHip},
            {"l upper leg", RlvAttachmentPoint.LeftUpperLeg},
            {"l lower leg", RlvAttachmentPoint.LeftLowerLeg},
            {"stomach", RlvAttachmentPoint.Stomach},
            {"left pec", RlvAttachmentPoint.LeftPec},
            {"right pec", RlvAttachmentPoint.RightPec},
            {"center 2", RlvAttachmentPoint.HUDCenter2},
            {"top right", RlvAttachmentPoint.HUDTopRight},
            {"top", RlvAttachmentPoint.HUDTop},
            {"top left", RlvAttachmentPoint.HUDTopLeft},
            {"center", RlvAttachmentPoint.HUDCenter},
            {"bottom left", RlvAttachmentPoint.HUDBottomLeft},
            {"bottom", RlvAttachmentPoint.HUDBottom},
            {"bottom right", RlvAttachmentPoint.HUDBottomRight},
            {"neck", RlvAttachmentPoint.Neck},
            {"root", RlvAttachmentPoint.Root},
            {"avatar center", RlvAttachmentPoint.Root}, // RLV hack
            {"left ring finger", RlvAttachmentPoint.LeftHandRing},
            {"right ring finger", RlvAttachmentPoint.RightHandRing},
            {"tail base", RlvAttachmentPoint.TailBase},
            {"tail tip", RlvAttachmentPoint.TailTip},
            {"left wing", RlvAttachmentPoint.LeftWing},
            {"right wing", RlvAttachmentPoint.RightWing},
            {"jaw", RlvAttachmentPoint.Jaw},
            {"alt left ear", RlvAttachmentPoint.AltLeftEar},
            {"alt right ear", RlvAttachmentPoint.AltRightEar},
            {"alt left eye", RlvAttachmentPoint.AltLeftEye},
            {"alt right eye", RlvAttachmentPoint.AltRightEye},
            {"tongue", RlvAttachmentPoint.Tongue},
            {"groin", RlvAttachmentPoint.Groin},
            {"left hind foot", RlvAttachmentPoint.LeftHindFoot},
            {"right hind foot", RlvAttachmentPoint.RightHindFoot},
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

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
