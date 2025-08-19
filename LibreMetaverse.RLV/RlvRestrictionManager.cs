using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.RLV.EventArguments;

namespace LibreMetaverse.RLV
{
    public class RlvRestrictionManager : IRestrictionProvider
    {
        private static readonly ImmutableDictionary<string, RlvRestrictionType> _nameToRestrictionMap = new Dictionary<string, RlvRestrictionType>(StringComparer.OrdinalIgnoreCase)
        {
            { "notify", RlvRestrictionType.Notify },
            { "permissive", RlvRestrictionType.Permissive },
            { "fly", RlvRestrictionType.Fly },
            { "jump", RlvRestrictionType.Jump },
            { "temprun", RlvRestrictionType.TempRun },
            { "alwaysrun", RlvRestrictionType.AlwaysRun },
            { "camzoommax", RlvRestrictionType.CamZoomMax },
            { "camzoommin", RlvRestrictionType.CamZoomMin },
            { "camdrawmin", RlvRestrictionType.CamDrawMin },
            { "camdrawmax", RlvRestrictionType.CamDrawMax },
            { "setcam_fovmin", RlvRestrictionType.SetCamFovMin },
            { "setcam_fovmax", RlvRestrictionType.SetCamFovMax },
            { "camdistmax", RlvRestrictionType.CamDistMax },
            { "camdistmin", RlvRestrictionType.CamDistMin },
            { "camdrawalphamin", RlvRestrictionType.CamDrawAlphaMin },
            { "camdrawalphamax", RlvRestrictionType.CamDrawAlphaMax },
            { "setcam_avdistmax", RlvRestrictionType.SetCamAvDistMax },
            { "setcam_avdistmin", RlvRestrictionType.SetCamAvDistMin },
            { "camdrawcolor", RlvRestrictionType.CamDrawColor },
            { "camunlock", RlvRestrictionType.CamUnlock },
            { "setcam_unlock", RlvRestrictionType.SetCamUnlock },
            { "camavdist", RlvRestrictionType.CamAvDist },
            { "camtextures", RlvRestrictionType.CamTextures },
            { "setcam_textures", RlvRestrictionType.SetCamTextures },
            { "sendchat", RlvRestrictionType.SendChat },
            { "chatshout", RlvRestrictionType.ChatShout },
            { "chatnormal", RlvRestrictionType.ChatNormal },
            { "chatwhisper", RlvRestrictionType.ChatWhisper },
            { "redirchat", RlvRestrictionType.RedirChat },
            { "recvchat", RlvRestrictionType.RecvChat },
            { "recvchat_sec", RlvRestrictionType.RecvChatSec },
            { "recvchatfrom", RlvRestrictionType.RecvChatFrom },
            { "sendgesture", RlvRestrictionType.SendGesture },
            { "emote", RlvRestrictionType.Emote },
            { "rediremote", RlvRestrictionType.RedirEmote },
            { "recvemote", RlvRestrictionType.RecvEmote },
            { "recvemotefrom", RlvRestrictionType.RecvEmoteFrom },
            { "recvemote_sec", RlvRestrictionType.RecvEmoteSec },
            { "sendchannel", RlvRestrictionType.SendChannel },
            { "sendchannel_sec", RlvRestrictionType.SendChannelSec },
            { "sendchannel_except", RlvRestrictionType.SendChannelExcept },
            { "sendim", RlvRestrictionType.SendIm },
            { "sendim_sec", RlvRestrictionType.SendImSec },
            { "sendimto", RlvRestrictionType.SendImTo },
            { "startim", RlvRestrictionType.StartIm },
            { "startimto", RlvRestrictionType.StartImTo },
            { "recvim", RlvRestrictionType.RecvIm },
            { "recvim_sec", RlvRestrictionType.RecvImSec },
            { "recvimfrom", RlvRestrictionType.RecvImFrom },
            { "tplocal", RlvRestrictionType.TpLocal },
            { "tplm", RlvRestrictionType.TpLm },
            { "tploc", RlvRestrictionType.TpLoc },
            { "tplure", RlvRestrictionType.TpLure },
            { "tplure_sec", RlvRestrictionType.TpLureSec },
            { "sittp", RlvRestrictionType.SitTp },
            { "standtp", RlvRestrictionType.StandTp },
            { "accepttp", RlvRestrictionType.AcceptTp },
            { "accepttprequest", RlvRestrictionType.AcceptTpRequest },
            { "tprequest", RlvRestrictionType.TpRequest },
            { "tprequest_sec", RlvRestrictionType.TpRequestSec },
            { "showinv", RlvRestrictionType.ShowInv },
            { "viewnote", RlvRestrictionType.ViewNote },
            { "viewscript", RlvRestrictionType.ViewScript },
            { "viewtexture", RlvRestrictionType.ViewTexture },
            { "edit", RlvRestrictionType.Edit },
            { "rez", RlvRestrictionType.Rez },
            { "editobj", RlvRestrictionType.EditObj },
            { "editworld", RlvRestrictionType.EditWorld },
            { "editattach", RlvRestrictionType.EditAttach },
            { "share", RlvRestrictionType.Share },
            { "share_sec", RlvRestrictionType.ShareSec },
            { "unsit", RlvRestrictionType.Unsit },
            { "sit", RlvRestrictionType.Sit },
            { "detach", RlvRestrictionType.Detach },
            { "addattach", RlvRestrictionType.AddAttach },
            { "remattach", RlvRestrictionType.RemAttach },
            { "defaultwear", RlvRestrictionType.DefaultWear },
            { "addoutfit", RlvRestrictionType.AddOutfit },
            { "remoutfit", RlvRestrictionType.RemOutfit },
            { "acceptpermission", RlvRestrictionType.AcceptPermission },
            { "denypermission", RlvRestrictionType.DenyPermission },
            { "unsharedwear", RlvRestrictionType.UnsharedWear },
            { "unsharedunwear", RlvRestrictionType.UnsharedUnwear },
            { "sharedwear", RlvRestrictionType.SharedWear },
            { "sharedunwear", RlvRestrictionType.SharedUnwear },
            { "detachthis", RlvRestrictionType.DetachThis },
            { "detachallthis", RlvRestrictionType.DetachAllThis },
            { "attachthis", RlvRestrictionType.AttachThis },
            { "attachallthis", RlvRestrictionType.AttachAllThis },
            { "detachthis_except", RlvRestrictionType.DetachThisExcept },
            { "detachallthis_except", RlvRestrictionType.DetachAllThisExcept },
            { "attachthis_except", RlvRestrictionType.AttachThisExcept },
            { "attachallthis_except", RlvRestrictionType.AttachAllThisExcept },
            { "fartouch", RlvRestrictionType.FarTouch },
            { "touchfar", RlvRestrictionType.TouchFar },
            { "touchall", RlvRestrictionType.TouchAll },
            { "touchworld", RlvRestrictionType.TouchWorld },
            { "touchthis", RlvRestrictionType.TouchThis },
            { "touchme", RlvRestrictionType.TouchMe },
            { "touchattach", RlvRestrictionType.TouchAttach },
            { "touchattachself", RlvRestrictionType.TouchAttachSelf },
            { "touchattachother", RlvRestrictionType.TouchAttachOther },
            { "touchhud", RlvRestrictionType.TouchHud },
            { "interact", RlvRestrictionType.Interact },
            { "showworldmap", RlvRestrictionType.ShowWorldMap },
            { "showminimap", RlvRestrictionType.ShowMiniMap },
            { "showloc", RlvRestrictionType.ShowLoc },
            { "shownames", RlvRestrictionType.ShowNames },
            { "shownames_sec", RlvRestrictionType.ShowNamesSec },
            { "shownametags", RlvRestrictionType.ShowNameTags },
            { "shownearby", RlvRestrictionType.ShowNearby },
            { "showhovertextall", RlvRestrictionType.ShowHoverTextAll },
            { "showhovertext", RlvRestrictionType.ShowHoverText },
            { "showhovertexthud", RlvRestrictionType.ShowHoverTextHud },
            { "showhovertextworld", RlvRestrictionType.ShowHoverTextWorld },
            { "setgroup", RlvRestrictionType.SetGroup },
            { "setdebug", RlvRestrictionType.SetDebug },
            { "setenv", RlvRestrictionType.SetEnv },
            { "allowidle", RlvRestrictionType.AllowIdle },
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

        private static readonly ImmutableDictionary<RlvRestrictionType, string> _restrictionToNameMap = _nameToRestrictionMap
            .ToImmutableDictionary(k => k.Value, v => v.Key);

        private readonly Dictionary<RlvRestrictionType, HashSet<RlvRestriction>> _currentRestrictions = [];
        private readonly object _currentRestrictionsLock = new();

        private readonly IRlvQueryCallbacks _queryCallbacks;
        private readonly IRlvActionCallbacks _actionCallbacks;
        private readonly LockedFolderManager _lockedFolderManager;

        public event EventHandler<RestrictionUpdatedEventArgs>? RestrictionUpdated;

        internal RlvRestrictionManager(IRlvQueryCallbacks callbacks, IRlvActionCallbacks actionCallbacks)
        {
            _queryCallbacks = callbacks;
            _actionCallbacks = actionCallbacks;
            _lockedFolderManager = new LockedFolderManager(callbacks, this);
        }

        internal static bool TryGetRestrictionFromName(string name, [NotNullWhen(true)] out RlvRestrictionType? restrictionType)
        {
            if (!_nameToRestrictionMap.TryGetValue(name, out var restrictionTypeTemp))
            {
                restrictionType = null;
                return false;
            }

            restrictionType = restrictionTypeTemp;
            return true;
        }

        internal static bool TryGetRestrictionNameFromType(RlvRestrictionType restrictionType, [NotNullWhen(true)] out string? name)
        {
            return _restrictionToNameMap.TryGetValue(restrictionType, out name);
        }

        private async Task NotifyRestrictionChange(RlvRestriction restriction, bool wasAdded, CancellationToken cancellationToken)
        {
            if (!TryGetRestrictionNameFromType(restriction.OriginalBehavior, out var restrictionName))
            {
                return;
            }

            var notification = restrictionName;
            if (restriction.Args.Count > 0)
            {
                notification += ":" + string.Join(";", restriction.Args);
            }

            notification += wasAdded ? "=n" : "=y";

            await NotifyRestrictionChange(restrictionName, notification, cancellationToken).ConfigureAwait(false);
        }

        private async Task NotifyRestrictionChange(string restrictionName, string notificationMessage, CancellationToken cancellationToken)
        {
            List<RlvRestriction> notificationRestrictions;

            lock (_currentRestrictionsLock)
            {
                if (!_currentRestrictions.TryGetValue(RlvRestrictionType.Notify, out var notificationRestrictionsTemp))
                {
                    return;
                }

                notificationRestrictions = notificationRestrictionsTemp.ToList();
            }

            foreach (var notificationRestriction in notificationRestrictions)
            {
                var filter = "";

                if (notificationRestriction.Args.Count == 0)
                {
                    continue;
                }

                if (notificationRestriction.Args[0] is not int notificationChannel)
                {
                    continue;
                }

                if (notificationRestriction.Args.Count > 1)
                {
                    filter = notificationRestriction.Args[1].ToString();
                }

                if (!restrictionName.Contains(filter.ToLowerInvariant()))
                {
                    continue;
                }

                await _actionCallbacks.SendReplyAsync(
                    notificationChannel,
                    $"/{notificationMessage}",
                    cancellationToken
                ).ConfigureAwait(false);
            }
        }

        public IEnumerable<Guid> GetTrackedPrimIds()
        {
            lock (_currentRestrictionsLock)
            {
                return _currentRestrictions
                    .SelectMany(n => n.Value)
                    .Select(n => n.Sender)
                    .Distinct()
                    .ToList();
            }
        }

        public async Task RemoveRestrictionsForObjects(IEnumerable<Guid> primIds, CancellationToken cancellationToken = default)
        {
            var objectIdMap = primIds.ToImmutableHashSet();
            var removedRestrictions = new List<RlvRestriction>();

            lock (_currentRestrictionsLock)
            {
                var restrictionsToRemove = _currentRestrictions
                    .SelectMany(n => n.Value)
                    .Where(n => objectIdMap.Contains(n.Sender))
                    .ToList();

                foreach (var restriction in restrictionsToRemove)
                {
                    var removedRestriction = false;

                    removedRestriction = RemoveRestriction_InternalUnsafe(restriction);
                    if (removedRestriction)
                    {
                        removedRestrictions.Add(restriction);
                    }
                }
            }

            foreach (var restriction in removedRestrictions)
            {
                var handler = RestrictionUpdated;
                handler?.Invoke(this, new RestrictionUpdatedEventArgs(restriction, false, true));

                await NotifyRestrictionChange(restriction, false, cancellationToken).ConfigureAwait(false);
            }
        }

        public IReadOnlyList<RlvRestriction> GetRestrictionsByType(RlvRestrictionType restrictionType)
        {
            restrictionType = RlvRestriction.GetRealRestriction(restrictionType);

            lock (_currentRestrictionsLock)
            {
                if (!_currentRestrictions.TryGetValue(restrictionType, out var restrictions))
                {
                    return ImmutableList<RlvRestriction>.Empty;
                }

                return restrictions.ToImmutableList();
            }
        }

        public IReadOnlyList<RlvRestriction> FindRestrictions(string behaviorNameFilter = "", Guid? senderFilter = null)
        {
            var restrictions = new List<RlvRestriction>();

            lock (_currentRestrictionsLock)
            {
                foreach (var item in _currentRestrictions)
                {
                    if (!TryGetRestrictionNameFromType(item.Key, out var behaviorName))
                    {
                        throw new KeyNotFoundException($"_currentRestrictions has a behavior '{item.Key}' that is not defined in the reverse behavior map");
                    }

                    if (!behaviorName.Contains(behaviorNameFilter.ToLowerInvariant()))
                    {
                        continue;
                    }

                    foreach (var restriction in item.Value)
                    {
                        if (senderFilter != null && restriction.Sender != senderFilter)
                        {
                            continue;
                        }

                        restrictions.Add(restriction);
                    }
                }

                return restrictions.ToImmutableList();
            }
        }

        private bool RemoveRestriction_InternalUnsafe(RlvRestriction restriction)
        {
            var removedRestriction = false;

            if (_currentRestrictions.TryGetValue(restriction.Behavior, out var restrictions))
            {
                removedRestriction = restrictions.Remove(restriction);

                if (restrictions.Count == 0)
                {
                    _currentRestrictions.Remove(restriction.Behavior);
                }
            }

            return removedRestriction;
        }

        private async Task RemoveRestriction(RlvRestriction restriction, CancellationToken cancellationToken)
        {
            var removedRestriction = false;

            lock (_currentRestrictionsLock)
            {
                removedRestriction = RemoveRestriction_InternalUnsafe(restriction);
            }

            if (removedRestriction)
            {
                var handler = RestrictionUpdated;
                handler?.Invoke(this, new RestrictionUpdatedEventArgs(restriction, false, true));
            }

            await NotifyRestrictionChange(restriction, false, cancellationToken).ConfigureAwait(false);
        }

        private async Task AddRestriction(RlvRestriction newRestriction, CancellationToken cancellationToken)
        {
            var restrictionAdded = false;

            lock (_currentRestrictionsLock)
            {
                if (!_currentRestrictions.TryGetValue(newRestriction.Behavior, out var restrictions))
                {
                    restrictions = [];
                    _currentRestrictions.Add(newRestriction.Behavior, restrictions);
                }

                if (restrictions.Add(newRestriction))
                {
                    restrictionAdded = true;
                }
            }

            if (restrictionAdded)
            {
                var handler = RestrictionUpdated;
                handler?.Invoke(this, new RestrictionUpdatedEventArgs(newRestriction, true, false));
            }

            await NotifyRestrictionChange(newRestriction, true, cancellationToken).ConfigureAwait(false);
        }

        internal async Task<bool> ProcessClearCommand(RlvMessage command, CancellationToken cancellationToken)
        {
            var filteredRestrictions = _restrictionToNameMap
                .Where(n => n.Value.Contains(command.Param.ToLowerInvariant()))
                .Select(n => n.Key)
                .ToList();

            var removedRestrictions = new List<RlvRestriction>();
            lock (_currentRestrictionsLock)
            {
                foreach (var restrictionType in filteredRestrictions)
                {
                    if (!_currentRestrictions.TryGetValue(restrictionType, out var restrictionsToRemove))
                    {
                        continue;
                    }

                    var restrictionsToRemoveSnapshot = restrictionsToRemove.ToList();
                    foreach (var restrictionToRemove in restrictionsToRemoveSnapshot)
                    {
                        if (restrictionToRemove.Sender != command.Sender)
                        {
                            continue;
                        }

                        if (RemoveRestriction_InternalUnsafe(restrictionToRemove))
                        {
                            removedRestrictions.Add(restrictionToRemove);
                        }
                    }
                }
            }
            await _lockedFolderManager.RebuildLockedFolders(cancellationToken).ConfigureAwait(false);

            foreach (var removedRestriction in removedRestrictions)
            {
                var handler = RestrictionUpdated;
                handler?.Invoke(this, new RestrictionUpdatedEventArgs(removedRestriction, false, true));

                await NotifyRestrictionChange(removedRestriction, false, cancellationToken).ConfigureAwait(false);
            }

            var notificationMessage = "clear";
            if (command.Param != "")
            {
                notificationMessage += $":{command.Param}";
            }

            await NotifyRestrictionChange("clear", notificationMessage, cancellationToken).ConfigureAwait(false);

            return true;
        }

        internal async Task<bool> ProcessRestrictionCommand(RlvMessage message, string option, bool isAddingRestriction, CancellationToken cancellationToken)
        {
            if (!TryGetRestrictionFromName(message.Behavior, out var behavior))
            {
                return false;
            }

            if (!RlvRestriction.ParseOptions(behavior.Value, option, out var args))
            {
                return false;
            }

            var newCommand = new RlvRestriction(behavior.Value, message.Sender, message.SenderName, args);

            if (isAddingRestriction)
            {
                await AddRestriction(newCommand, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RemoveRestriction(newCommand, cancellationToken).ConfigureAwait(false);
            }

            switch (newCommand.Behavior)
            {
                case RlvRestrictionType.DetachThis:
                case RlvRestrictionType.DetachAllThis:
                case RlvRestrictionType.AttachThis:
                case RlvRestrictionType.AttachAllThis:
                {
                    if (isAddingRestriction)
                    {
                        await _lockedFolderManager.ProcessFolderException(newCommand, false, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _lockedFolderManager.RebuildLockedFolders(cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
                case RlvRestrictionType.DetachThisExcept:
                case RlvRestrictionType.DetachAllThisExcept:
                case RlvRestrictionType.AttachThisExcept:
                case RlvRestrictionType.AttachAllThisExcept:
                {
                    if (isAddingRestriction)
                    {
                        await _lockedFolderManager.ProcessFolderException(newCommand, true, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _lockedFolderManager.RebuildLockedFolders(cancellationToken).ConfigureAwait(false);
                    }
                    break;
                }
            }

            return true;
        }

        public bool TryGetLockedFolder(Guid folderId, [NotNullWhen(true)] out LockedFolderPublic? lockedFolder)
        {
            return _lockedFolderManager.TryGetLockedFolder(folderId, out lockedFolder);
        }

        public IReadOnlyDictionary<Guid, LockedFolderPublic> GetLockedFolders()
        {
            return _lockedFolderManager.GetLockedFolders();
        }
    }
}
