using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreMetaverse.RLV
{
    public class RlvPermissionsService
    {
        private readonly IRestrictionProvider _restrictionProvider;
        private static readonly char[] _invalidMessageCharacters = new char[] { '(', ')', '"', '-', '*', '=', '_', '^' };

        internal RlvPermissionsService(IRestrictionProvider restrictionProvider)
        {
            _restrictionProvider = restrictionProvider;
        }

        internal static bool TryGetRestrictionValueMax(IRestrictionProvider restrictionProvider, RlvRestrictionType restrictionType, out float val)
        {
            var restriction = restrictionProvider.GetRestrictionsByType(restrictionType);
            if (restriction.Count == 0)
            {
                val = default;
                return false;
            }

            val = restriction
                .Where(n => n.Args.Count > 0 && n.Args[0] is float)
                .Select(n => (float)n.Args[0])
                .Max();

            return true;
        }

        internal static bool TryGetRestrictionValueMin(IRestrictionProvider restrictionProvider, RlvRestrictionType restrictionType, out float val)
        {
            var restriction = restrictionProvider.GetRestrictionsByType(restrictionType);
            if (restriction.Count == 0)
            {
                val = default;
                return false;
            }

            val = restriction
                .Where(n => n.Args.Count > 0 && n.Args[0] is float)
                .Select(n => (float)n.Args[0])
                .Min();

            return true;
        }

        internal static bool TryGetOptionalRestrictionValueMin(IRestrictionProvider restrictionProvider, RlvRestrictionType restrictionType, float defaultVal, out float val)
        {
            var restrictions = restrictionProvider.GetRestrictionsByType(restrictionType);
            if (restrictions.Count == 0)
            {
                val = defaultVal;
                return false;
            }

            if (restrictions.FirstOrDefault(n => n.Args.Count == 0) != null)
            {
                val = defaultVal;
            }
            else
            {
                val = restrictions
                    .Where(n => n.Args.Count > 0 && n.Args[0] is float)
                    .Select(n => (float)n.Args[0])
                    .Min();
            }

            return true;
        }

        private bool CheckSecureRestriction(Guid? userId, string? groupName, RlvRestrictionType normalType, RlvRestrictionType? secureType, RlvRestrictionType? fromToType)
        {
            // Explicit restrictions
            if (fromToType != null)
            {
                var isRestrictedBySendImTo = _restrictionProvider.GetRestrictionsByType(fromToType.Value)
                    .Where(n => n.Args.Count == 1 &&
                        ((userId != null && n.Args[0] is Guid restrictedId && userId == restrictedId) ||
                        (groupName != null && n.Args[0] is string restrictedGroupName && (restrictedGroupName == "allgroups" || restrictedGroupName == groupName)))
                    ).Any();
                if (isRestrictedBySendImTo)
                {
                    return false;
                }
            }

            var sendImRestrictions = _restrictionProvider.GetRestrictionsByType(normalType);
            var sendImExceptions = sendImRestrictions
                .Where(n => n.IsException && n.Args.Count == 1 &&
                    ((userId != null && n.Args[0] is Guid restrictedId && userId == restrictedId) ||
                    (groupName != null && n.Args[0] is string restrictedGroupName && (restrictedGroupName == "allgroups" || restrictedGroupName == groupName)))
                ).ToList();

            // Secure restrictions
            if (secureType != null)
            {
                var sendImRestrictionsSecure = _restrictionProvider.GetRestrictionsByType(secureType.Value);
                foreach (var item in sendImRestrictionsSecure)
                {
                    var hasException = sendImExceptions
                        .Where(n => n.Sender == item.Sender)
                        .Any();
                    if (hasException)
                    {
                        continue;
                    }

                    return false;
                }
            }

            // Normal restrictions
            var permissiveMode = IsPermissive();
            foreach (var restriction in sendImRestrictions.Where(n => !n.IsException && n.Args.Count == 0))
            {
                var hasException = sendImExceptions
                    .Where(n => permissiveMode || n.Sender == restriction.Sender)
                    .Any();
                if (hasException)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public bool CanFly()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Fly).Count == 0;
        }
        public bool CanJump()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Jump).Count == 0;
        }
        public bool CanTempRun()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TempRun).Count == 0;
        }
        public bool CanAlwaysRun()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.AlwaysRun).Count == 0;
        }
        public bool CanUnsit()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Unsit).Count == 0;
        }
        public bool CanSit()
        {
            if (!CanInteract())
            {
                return false;
            }

            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Sit).Count == 0;
        }

        #region TP

        public bool CanTpLm()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TpLm).Count == 0;
        }
        public bool CanTpLoc()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TpLoc).Count == 0;
        }
        public bool CanSitTp(out float maxDistance)
        {
            return TryGetOptionalRestrictionValueMin(_restrictionProvider, RlvRestrictionType.SitTp, 1.5f, out maxDistance);
        }
        public bool CanTpLocal(out float maxDistance)
        {
            return TryGetOptionalRestrictionValueMin(_restrictionProvider, RlvRestrictionType.TpLocal, 0.0f, out maxDistance);
        }
        public bool CanStandTp()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.StandTp).Count == 0;
        }

        public bool CanTPLure(Guid? userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.TpLure, RlvRestrictionType.TpLureSec, null);
        }

        public bool CanTpRequest(Guid? userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.TpRequest, RlvRestrictionType.TpRequestSec, null);
        }

        public bool IsAutoAcceptTp(Guid? userId = null)
        {
            var restrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.AcceptTp);
            foreach (var restriction in restrictions)
            {
                if (restriction.Args.Count == 0)
                {
                    return true;
                }

                if (restriction.Args[0] is Guid allowedUserID && allowedUserID == userId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsAutoAcceptTpRequest(Guid? userId = null)
        {
            var restrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.AcceptTpRequest);
            foreach (var restriction in restrictions)
            {
                if (restriction.Args.Count == 0)
                {
                    return true;
                }

                if (restriction.Args[0] is Guid allowedUserID && allowedUserID == userId)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion

        public bool CanShowInv()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowInv).Count == 0;
        }
        public bool CanViewNote()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ViewNote).Count == 0;
        }
        public bool CanViewScript()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ViewScript).Count == 0;
        }
        public bool CanViewTexture()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ViewTexture).Count == 0;
        }

        public bool CanDefaultWear()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.DefaultWear).Count == 0;
        }
        public bool CanSetGroup()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SetGroup).Count == 0;
        }
        public bool CanSetDebug()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SetDebug).Count == 0;
        }
        public bool CanSetEnv()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SetEnv).Count == 0;
        }
        public bool CanAllowIdle()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.AllowIdle).Count == 0;
        }
        public bool CanInteract()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Interact).Count == 0;
        }
        public bool CanShowWorldMap()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowWorldMap).Count == 0;
        }
        public bool CanShowMiniMap()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowMiniMap).Count == 0;
        }
        public bool CanShowLoc()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowLoc).Count == 0;
        }
        public bool CanShowNearby()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowNearby).Count == 0;
        }

        public bool IsAutoDenyPermissions()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.DenyPermission).Count != 0;
        }

        #region Camera

        public CameraRestrictions GetCameraRestrictions()
        {
            var restrictions = new CameraRestrictions(_restrictionProvider);
            return restrictions;
        }

        #endregion

        #region Chat
        public bool CanStartIM(Guid userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.StartIm, null, RlvRestrictionType.StartImTo);
        }

        public bool CanSendIM(string message, Guid? userId, string? groupName = null)
        {
            return CheckSecureRestriction(userId, groupName, RlvRestrictionType.SendIm, RlvRestrictionType.SendImSec, RlvRestrictionType.SendImTo);
        }

        public bool CanReceiveIM(string message, Guid? userId, string? groupName = null)
        {
            return CheckSecureRestriction(userId, groupName, RlvRestrictionType.RecvIm, RlvRestrictionType.RecvImSec, RlvRestrictionType.RecvImFrom);
        }

        public bool CanReceiveChat(string message, Guid userId)
        {
            if (message.StartsWith("/me ", StringComparison.OrdinalIgnoreCase))
            {
                return CheckSecureRestriction(userId, null, RlvRestrictionType.RecvEmote, RlvRestrictionType.RecvEmoteSec, RlvRestrictionType.RecvEmoteFrom);
            }
            else
            {
                return CheckSecureRestriction(userId, null, RlvRestrictionType.RecvChat, RlvRestrictionType.RecvChatSec, RlvRestrictionType.RecvChatFrom);
            }
        }

        public bool CanChatShout()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ChatShout).Count == 0;
        }
        public bool CanChatWhisper()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ChatWhisper).Count == 0;
        }
        public bool CanChatNormal()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ChatNormal).Count == 0;
        }
        public bool CanSendChat()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SendChat).Count == 0;
        }
        public bool CanEmote()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Emote).Count == 0;
        }
        public bool CanSendGesture()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SendGesture).Count == 0;
        }

        public bool TryGetRedirChatChannels(out IReadOnlyList<int> channels)
        {
            channels = _restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.RedirChat)
                .Where(n => n.Args.Count == 1 && n.Args[0] is int)
                .Select(n => (int)n.Args[0])
                .Distinct()
                .ToList();

            return channels.Count > 0;
        }

        public bool TryGetRedirEmoteChannels(out IReadOnlyList<int> channels)
        {
            channels = _restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.RedirEmote)
                .Where(n => n.Args.Count == 1 && n.Args[0] is int)
                .Select(n => (int)n.Args[0])
                .Distinct()
                .ToList();

            return channels.Count > 0;
        }

        private bool CanChatOnChannelPrivateChannel(int channel)
        {
            var sendChannelExceptRestrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SendChannelExcept);

            foreach (var restriction in sendChannelExceptRestrictions)
            {
                if (restriction.Args.Count == 0)
                {
                    continue;
                }

                if (restriction.Args[0] is not int restrictedChannel)
                {
                    continue;
                }

                if (channel == restrictedChannel)
                {
                    return false;
                }
            }

            var sendChannelRestrictionsSecure = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SendChannelSec);
            var sendChannelRestrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SendChannel);
            var channelExceptions = sendChannelRestrictions
                .Where(n =>
                    n.IsException &&
                    n.Args.Count > 0 &&
                    n.Args[0] is int exceptionChannel &&
                    exceptionChannel == channel
                )
                .ToList();

            foreach (var restriction in sendChannelRestrictionsSecure)
            {
                var hasSecureException = channelExceptions
                    .Where(n => n.Sender == restriction.Sender)
                    .Any();
                if (hasSecureException)
                {
                    continue;
                }

                return false;
            }

            var permissiveMode = IsPermissive();
            foreach (var restriction in sendChannelRestrictions.Where(n => !n.IsException && n.Args.Count == 0))
            {
                var hasException = channelExceptions
                    .Where(n => permissiveMode || n.Sender == restriction.Sender)
                    .Any();
                if (hasException)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public bool CanChat(int channel, string message)
        {
            if (channel == 0)
            {
                var canEmote = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Emote).Count == 0;
                if (message.StartsWith("/me ", StringComparison.OrdinalIgnoreCase) && !canEmote)
                {
                    return false;
                }

                if (!CanSendChat())
                {
                    // TODO: Implement weird hacked on restrictions from @sendchat?
                    //  emotes and messages beginning with a slash ('/') will go through,
                    //  truncated to strings of 30 and 15 characters long respectively (likely
                    //  to change later). Messages with special signs like ()"-*=_^ are prohibited,
                    //  and will be discarded. When a period ('.') is present, the rest of the
                    //  message is discarded. 

                    if (message.IndexOfAny(_invalidMessageCharacters) != -1)
                    {
                        return false;
                    }

                    if (!message.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!CanChatOnChannelPrivateChannel(channel))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        public bool CanShowNames(Guid? userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.ShowNames, RlvRestrictionType.ShowNamesSec, null);
        }

        public bool CanShowNameTags(Guid? userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.ShowNameTags, null, null);
        }

        public bool CanShare(Guid? userId)
        {
            return CheckSecureRestriction(userId, null, RlvRestrictionType.Share, RlvRestrictionType.ShareSec, null);
        }

        public bool IsAutoAcceptPermissions()
        {
            if (IsAutoDenyPermissions())
            {
                return false;
            }

            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.AcceptPermission).Count != 0;
        }


        public bool IsPermissive()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Permissive).Count == 0;
        }

        public bool CanRez()
        {
            if (!CanInteract())
            {
                return false;
            }

            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Rez).Count == 0;
        }

        public enum ObjectLocation
        {
            Hud,
            Attached,
            RezzedInWorld
        }
        public bool CanEdit(ObjectLocation objectLocation, Guid? objectId)
        {
            if (!CanInteract())
            {
                return false;
            }

            var canEditObject = CheckSecureRestriction(objectId, null, RlvRestrictionType.Edit, null, RlvRestrictionType.EditObj);
            if (!canEditObject)
            {
                return false;
            }

            if (objectLocation == ObjectLocation.RezzedInWorld)
            {
                var hasEditWorldRestriction = _restrictionProvider
                    .GetRestrictionsByType(RlvRestrictionType.EditWorld)
                    .Count != 0;
                if (hasEditWorldRestriction)
                {
                    return false;
                }
            }

            if (objectLocation == ObjectLocation.Attached)
            {
                var hasEditAttachRestriction = _restrictionProvider
                    .GetRestrictionsByType(RlvRestrictionType.EditAttach)
                    .Count != 0;
                if (hasEditAttachRestriction)
                {
                    return false;
                }
            }

            return true;
        }

        #region Touch
        public bool TryGetMaxFarTouchDistance(out float maxDistance)
        {
            return TryGetOptionalRestrictionValueMin(_restrictionProvider, RlvRestrictionType.FarTouch, 1.5f, out maxDistance);
        }

        private bool CanTouchHud(Guid objectId)
        {
            if (!CanInteract())
            {
                return false;
            }

            return !_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.TouchHud)
                .Where(n => n.Args.Count == 0 || (n.Args[0] is Guid restrictedObjectId && restrictedObjectId == objectId))
                .Any();
        }

        private bool CanTouchAttachment(bool isAttachedToSelf, Guid? otherUserId)
        {
            if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TouchAttach).Count != 0)
            {
                return false;
            }

            if (isAttachedToSelf)
            {
                if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TouchAttachSelf).Count != 0)
                {
                    return false;
                }
            }
            else
            {
                var isForbiddenFromTouchingOthers = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TouchAttachOther)
                    .Where(n => n.Args.Count == 0 || (n.Args[0] is Guid restrictedUserId && restrictedUserId == otherUserId))
                    .Any();
                if (isForbiddenFromTouchingOthers)
                {
                    return false;
                }
            }

            return true;
        }

        public enum TouchLocation
        {
            Hud,
            AttachedSelf,
            AttachedOther,
            RezzedInWorld
        }
        public bool CanTouch(TouchLocation location, Guid primId, Guid? userId, float? distance)
        {
            if (distance != null)
            {
                if (TryGetRestrictionValueMin(_restrictionProvider, RlvRestrictionType.FarTouch, out var maxTouchDistance))
                {
                    if (distance > maxTouchDistance)
                    {
                        return false;
                    }
                }
            }

            if (!CanInteract())
            {
                return false;
            }

            if (_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.TouchMe)
                .Where(n => n.Sender == primId)
                .Any())
            {
                return true;
            }

            if (_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.TouchThis)
                .Where(n => n.Args.Count == 1 && n.Args[0] is Guid restrictedItemId && restrictedItemId == primId)
                .Any())
            {
                return false;
            }

            if (location != TouchLocation.Hud)
            {
                if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TouchAll).Count != 0)
                {
                    return false;
                }
            }

            if (location == TouchLocation.RezzedInWorld)
            {
                var touchWorldRestrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.TouchWorld);
                var hasException = touchWorldRestrictions
                    .Where(n => n.IsException && n.Args.Count == 1 && n.Args[0] is Guid allowedObjectId && allowedObjectId == primId)
                    .Any();

                if (!hasException && touchWorldRestrictions.Any(n => n.Args.Count == 0))
                {
                    return false;
                }
            }
            else if (location is TouchLocation.AttachedSelf or TouchLocation.AttachedOther)
            {
                if (!CanTouchAttachment(location == TouchLocation.AttachedSelf, userId))
                {
                    return false;
                }
            }

            if (location == TouchLocation.Hud)
            {
                if (!CanTouchHud(primId))
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        public enum HoverTextLocation
        {
            World,
            Hud
        }
        public bool CanShowHoverText(HoverTextLocation location, Guid? objectId)
        {
            if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowHoverTextAll).Count != 0)
            {
                return false;
            }

            if (_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.ShowHoverText)
                .Where(n => n.Args.Count == 1 && n.Args[0] is Guid restrictedObjectId && restrictedObjectId == objectId)
                .Any())
            {
                return false;
            }

            if (location == HoverTextLocation.Hud)
            {
                if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowHoverTextHud).Count != 0)
                {
                    return false;
                }
            }
            else if (location == HoverTextLocation.World)
            {
                if (_restrictionProvider.GetRestrictionsByType(RlvRestrictionType.ShowHoverTextWorld).Count != 0)
                {
                    return false;
                }
            }

            return true;
        }

        #region Attach / Detach
        private bool CanUnsharedWear()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.UnsharedWear).Count == 0;
        }
        private bool CanUnsharedUnwear()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.UnsharedUnwear).Count == 0;
        }
        private bool CanSharedWear()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SharedWear).Count == 0;
        }
        private bool CanSharedUnwear()
        {
            return _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.SharedUnwear).Count == 0;
        }

        private bool CanAttachWearable(RlvWearableType? typeToRemove)
        {
            return !_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.AddOutfit)
                .Where(n => n.Args.Count == 0 || (n.Args[0] is RlvWearableType restrictedType && typeToRemove == restrictedType))
                .Any();
        }
        private bool CanDetachWearable(RlvWearableType? typeToRemove)
        {
            return !_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.RemOutfit)
                .Where(n => n.Args.Count == 0 || (n.Args[0] is RlvWearableType restrictedType && typeToRemove == restrictedType))
                .Any();
        }
        private bool CanDetachAttached(RlvAttachmentPoint? attachmentPoint)
        {
            return !_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.RemAttach)
                .Where(n => n.Args.Count == 0 || (n.Args[0] is RlvAttachmentPoint restrictedAttachmentPoint && attachmentPoint == restrictedAttachmentPoint))
                .Any();
        }
        private bool CanAttachAttached(RlvAttachmentPoint? attachmentPoint)
        {
            return !_restrictionProvider
                .GetRestrictionsByType(RlvRestrictionType.AddAttach)
                .Where(n => n.Args.Count == 0 || (n.Args[0] is RlvAttachmentPoint restrictedAttachmentPoint && attachmentPoint == restrictedAttachmentPoint))
                .Any();
        }

        public bool CanAttach(RlvInventoryItem item, bool isShared)
        {
            return CanAttach(
                item.FolderId,
                isShared,
                item.AttachedTo,
                item.WornOn
            );
        }
        public bool CanAttach(Guid? objectFolderId, bool isShared, RlvAttachmentPoint? attachmentPoint, RlvWearableType? wearableType)
        {
            if (wearableType != null && !CanAttachWearable(wearableType))
            {
                return false;
            }

            if (attachmentPoint != null && !CanAttachAttached(attachmentPoint))
            {
                return false;
            }

            if (isShared)
            {
                if (!CanSharedWear())
                {
                    return false;
                }

                if (!objectFolderId.HasValue)
                {
                    return false;
                }

                if (_restrictionProvider.TryGetLockedFolder(objectFolderId.Value, out var lockedFolder))
                {
                    if (!lockedFolder.CanAttach)
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!CanUnsharedWear())
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanDetach(RlvInventoryItem item)
        {
            return CanDetach(
                item.Id,
                item.AttachedPrimId,
                item.FolderId,
                item.Folder != null,
                item.AttachedTo,
                item.WornOn
            );
        }
        public bool CanDetach(Guid? itemId, Guid? primId, Guid? folderId, bool isShared, RlvAttachmentPoint? attachmentPoint, RlvWearableType? wearableType)
        {
            if (wearableType != null && !CanDetachWearable(wearableType))
            {
                return false;
            }

            if (attachmentPoint != null && !CanDetachAttached(attachmentPoint))
            {
                return false;
            }

            var detachRestrictions = _restrictionProvider.GetRestrictionsByType(RlvRestrictionType.Detach);
            foreach (var restriction in detachRestrictions)
            {
                if (restriction.Args.Count == 0)
                {
                    if (primId != null && primId == restriction.Sender)
                    {
                        return false;
                    }
                }
                else if (restriction.Args[0] is RlvAttachmentPoint restrictedAttachmentPoint && attachmentPoint == restrictedAttachmentPoint)
                {
                    return false;
                }
            }

            if (isShared)
            {
                if (!CanSharedUnwear())
                {
                    return false;
                }

                if (!folderId.HasValue)
                {
                    return false;
                }

                if (_restrictionProvider.TryGetLockedFolder(folderId.Value, out var lockedFolder))
                {
                    if (!lockedFolder.CanDetach)
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!CanUnsharedUnwear())
                {
                    return false;
                }
            }

            return true;
        }
        #endregion
    }
}
