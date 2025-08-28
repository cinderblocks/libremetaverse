using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    internal sealed class RlvGetRequestHandler
    {
        private readonly ImmutableDictionary<string, RlvDataRequestType> _rlvDataRequestToNameMap;
        private readonly IRestrictionProvider _restrictions;
        private readonly IBlacklistProvider _blacklist;
        private readonly IRlvQueryCallbacks _queryCallbacks;
        private readonly IRlvActionCallbacks _actionCallbacks;

        internal RlvGetRequestHandler(IBlacklistProvider blacklist, IRestrictionProvider restrictions, IRlvQueryCallbacks queryCallbacks, IRlvActionCallbacks actionCallbacks)
        {
            _restrictions = restrictions;
            _blacklist = blacklist;
            _queryCallbacks = queryCallbacks;
            _actionCallbacks = actionCallbacks;

            _rlvDataRequestToNameMap = new Dictionary<string, RlvDataRequestType>()
            {
                { "getcam_avdistmin", RlvDataRequestType.GetCamAvDistMin },
                { "getcam_avdistmax", RlvDataRequestType.GetCamAvDistMax },
                { "getcam_fovmin", RlvDataRequestType.GetCamFovMin },
                { "getcam_fovmax", RlvDataRequestType.GetCamFovMax },
                { "getcam_zoommin", RlvDataRequestType.GetCamZoomMin },
                { "getcam_fov", RlvDataRequestType.GetCamFov },
                { "getsitid", RlvDataRequestType.GetSitId },
                { "getoutfit", RlvDataRequestType.GetOutfit },
                { "getattach", RlvDataRequestType.GetAttach },
                { "getinv", RlvDataRequestType.GetInv },
                { "getinvworn", RlvDataRequestType.GetInvWorn },
                { "findfolder", RlvDataRequestType.FindFolder },
                { "findfolders", RlvDataRequestType.FindFolders },
                { "getpath", RlvDataRequestType.GetPath },
                { "getpathnew", RlvDataRequestType.GetPathNew },
                { "getgroup", RlvDataRequestType.GetGroup }
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        private string HandleGetStatus(string option, Guid? sender)
        {
            var parts = option.Split(';');
            var filter = string.Empty;
            var separator = "/";

            if (parts.Length > 0)
            {
                filter = parts[0].ToLowerInvariant();
            }
            if (parts.Length > 1)
            {
                separator = parts[1];
            }

            var restrictions = _restrictions.FindRestrictions(filter, sender);
            StringBuilder sb = new StringBuilder();
            foreach (var restriction in restrictions)
            {
                if (!RlvRestrictionManager.TryGetRestrictionNameFromType(restriction.OriginalBehavior, out var behaviorName))
                {
                    continue;
                }

                sb.Append($"{separator}{behaviorName}");
                if (restriction.Args.Count > 0)
                {
                    sb.Append($":{string.Join(";", restriction.Args)}");
                }
            }

            return sb.ToString();
        }

        private async Task<string> ProcessGetOutfit(RlvWearableType? specificType, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var wornTypes = new HashSet<RlvWearableType>();
            foreach (var kvp in inventoryMap.Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.WornOn.HasValue)
                    {
                        wornTypes.Add(item.WornOn.Value);
                    }
                }
            }
            foreach (var kvp in inventoryMap.ExternalItems)
            {
                if (kvp.Value.WornOn.HasValue)
                {
                    wornTypes.Add(kvp.Value.WornOn.Value);
                }
            }

            if (specificType.HasValue)
            {
                if (wornTypes.Contains(specificType.Value))
                {
                    return "1";
                }
                else
                {
                    return "0";
                }
            }

            var sb = new StringBuilder(16);

            // gloves,jacket,pants,shirt,shoes,skirt,socks,underpants,undershirt,skin,eyes,hair,shape,alpha,tattoo,physics
            sb.Append(wornTypes.Contains(RlvWearableType.Gloves) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Jacket) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Pants) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Shirt) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Shoes) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Skirt) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Socks) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Underpants) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Undershirt) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Skin) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Eyes) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Hair) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Shape) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Alpha) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Tattoo) ? "1" : "0");
            sb.Append(wornTypes.Contains(RlvWearableType.Physics) ? "1" : "0");

            return sb.ToString();
        }

        private async Task<string> ProcessGetAttach(RlvAttachmentPoint? specificType, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var attachedTypes = new HashSet<RlvAttachmentPoint>();
            foreach (var kvp in inventoryMap.Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.AttachedTo.HasValue)
                    {
                        attachedTypes.Add(item.AttachedTo.Value);
                    }
                }
            }
            foreach (var kvp in inventoryMap.ExternalItems)
            {
                if (kvp.Value.AttachedTo.HasValue)
                {
                    attachedTypes.Add(kvp.Value.AttachedTo.Value);
                }
            }

            if (specificType.HasValue)
            {
                if (attachedTypes.Contains(specificType.Value))
                {
                    return "1";
                }
                else
                {
                    return "0";
                }
            }

            var attachmentPointTypes = Enum.GetValues(typeof(RlvAttachmentPoint));
            var sb = new StringBuilder(attachmentPointTypes.Length);

            // digit corresponds directly to the value of enum, unlike ProcessGetOutfit
            foreach (RlvAttachmentPoint attachmentPoint in attachmentPointTypes)
            {
                sb.Append(attachedTypes.Contains(attachmentPoint) ? '1' : '0');
            }

            return sb.ToString();
        }

        internal async Task<bool> ProcessGetCommand(RlvMessage rlvMessage, int channel, CancellationToken cancellationToken)
        {
            var blacklist = _blacklist.GetBlacklist();

            string? response = null;
            switch (rlvMessage.Behavior)
            {
                case "version":
                case "versionnew":
                    response = RlvService.RLVVersion;
                    break;
                case "versionnum":
                    response = RlvService.RLVVersionNum;
                    break;
                case "versionnumbl":
                    if (blacklist.Count > 0)
                    {
                        response = $"{RlvService.RLVVersionNum},{string.Join(",", blacklist)}";
                    }
                    else
                    {
                        response = RlvService.RLVVersionNum;
                    }
                    break;
                case "getblacklist":
                    var filteredBlacklist = blacklist
                        .Where(n => n.Contains(rlvMessage.Option));
                    response = string.Join(",", filteredBlacklist);
                    break;
                case "getstatus":
                    response = HandleGetStatus(rlvMessage.Option, rlvMessage.Sender);
                    break;
                case "getstatusall":
                    response = HandleGetStatus(rlvMessage.Option, null);
                    break;
            }

            if (_rlvDataRequestToNameMap.TryGetValue(rlvMessage.Behavior, out var name))
            {
                switch (name)
                {
                    case RlvDataRequestType.GetSitId:
                    {
                        var (hasSitId, sitId) = await _queryCallbacks.TryGetSitIdAsync(cancellationToken).ConfigureAwait(false);
                        if (!hasSitId || sitId == Guid.Empty)
                        {
                            response = "NULL_KEY";
                        }
                        else
                        {
                            response = sitId.ToString();
                        }

                        break;
                    }
                    case RlvDataRequestType.GetCamAvDistMin:
                    case RlvDataRequestType.GetCamAvDistMax:
                    case RlvDataRequestType.GetCamFovMin:
                    case RlvDataRequestType.GetCamFovMax:
                    case RlvDataRequestType.GetCamZoomMin:
                    case RlvDataRequestType.GetCamFov:
                    {
                        var (hasCameraSettings, cameraSettings) = await _queryCallbacks.TryGetCameraSettingsAsync(cancellationToken).ConfigureAwait(false);
                        if (!hasCameraSettings || cameraSettings == null)
                        {
                            return false;
                        }

                        switch (name)
                        {
                            case RlvDataRequestType.GetCamAvDistMin:
                            {
                                response = cameraSettings.AvDistMin.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                            case RlvDataRequestType.GetCamAvDistMax:
                            {
                                response = cameraSettings.AvDistMax.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                            case RlvDataRequestType.GetCamFovMin:
                            {
                                response = cameraSettings.FovMin.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                            case RlvDataRequestType.GetCamFovMax:
                            {
                                response = cameraSettings.FovMax.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                            case RlvDataRequestType.GetCamZoomMin:
                            {
                                response = cameraSettings.ZoomMin.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                            case RlvDataRequestType.GetCamFov:
                            {
                                response = cameraSettings.CurrentFov.ToString(CultureInfo.InvariantCulture);
                                break;
                            }
                        }

                        break;
                    }

                    case RlvDataRequestType.GetGroup:
                    {
                        var (hasGroup, group) = await _queryCallbacks.TryGetActiveGroupNameAsync(cancellationToken).ConfigureAwait(false);

                        if (!hasGroup)
                        {
                            response = "none";
                        }
                        else
                        {
                            response = group;
                        }
                        break;
                    }
                    case RlvDataRequestType.GetOutfit:
                    {
                        RlvWearableType? wearableType = null;
                        if (RlvCommon.RlvWearableTypeMap.TryGetValue(rlvMessage.Option, out var wearableTypeTemp))
                        {
                            wearableType = wearableTypeTemp;
                        }

                        response = await ProcessGetOutfit(wearableType, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case RlvDataRequestType.GetAttach:
                    {
                        RlvAttachmentPoint? attachmentPointType = null;
                        if (RlvCommon.RlvAttachmentPointMap.TryGetValue(rlvMessage.Option, out var attachmentPointTemp))
                        {
                            attachmentPointType = attachmentPointTemp;
                        }

                        response = await ProcessGetAttach(attachmentPointType, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case RlvDataRequestType.GetInv:
                        response = await HandleGetInv(rlvMessage.Option, cancellationToken).ConfigureAwait(false);
                        break;
                    case RlvDataRequestType.GetInvWorn:
                        response = await HandleGetInvWorn(rlvMessage.Option, cancellationToken).ConfigureAwait(false);
                        break;
                    case RlvDataRequestType.FindFolder:
                    case RlvDataRequestType.FindFolders:
                    {
                        var findFolderParts = rlvMessage.Option.Split(';');
                        var separator = ",";
                        var searchTerms = findFolderParts[0]
                            .Split(["&&"], StringSplitOptions.RemoveEmptyEntries);

                        if (findFolderParts.Length > 1)
                        {
                            separator = findFolderParts[1];
                        }

                        response = await HandleFindFolders(name == RlvDataRequestType.FindFolder, searchTerms, separator, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case RlvDataRequestType.GetPath:
                    case RlvDataRequestType.GetPathNew:
                    {
                        // [] | [attachedPrimId | layer | attachpt ]
                        var parsedOptions = rlvMessage.Option.Split([';'], StringSplitOptions.RemoveEmptyEntries).ToList();

                        if (parsedOptions.Count > 1)
                        {
                            return false;
                        }

                        if (parsedOptions.Count == 0)
                        {
                            response = await HandleGetPath(name == RlvDataRequestType.GetPath, rlvMessage.Sender, null, null, cancellationToken).ConfigureAwait(false);
                        }
                        else if (Guid.TryParse(parsedOptions[0], out var attachedPrimId))
                        {
                            response = await HandleGetPath(name == RlvDataRequestType.GetPath, attachedPrimId, null, null, cancellationToken).ConfigureAwait(false);
                        }
                        else if (RlvCommon.RlvWearableTypeMap.TryGetValue(parsedOptions[0], out var wearableType))
                        {
                            response = await HandleGetPath(name == RlvDataRequestType.GetPath, null, null, wearableType, cancellationToken).ConfigureAwait(false);
                        }
                        else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(parsedOptions[0], out var attachmentPoint))
                        {
                            response = await HandleGetPath(name == RlvDataRequestType.GetPath, null, attachmentPoint, null, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            return false;
                        }

                        break;
                    }
                }
            }
            else if (rlvMessage.Behavior.StartsWith("getdebug_", StringComparison.OrdinalIgnoreCase))
            {
                var commandRaw = rlvMessage.Behavior.Substring("getdebug_".Length);
                var (success, debugInfo) = await _queryCallbacks.TryGetDebugSettingValueAsync(commandRaw, cancellationToken).ConfigureAwait(false);

                if (success)
                {
                    response = debugInfo;
                }
            }
            else if (rlvMessage.Behavior.StartsWith("getenv_", StringComparison.OrdinalIgnoreCase))
            {
                var commandRaw = rlvMessage.Behavior.Substring("getenv_".Length);
                var (success, envInfo) = await _queryCallbacks.TryGetEnvironmentSettingValueAsync(commandRaw, cancellationToken).ConfigureAwait(false);

                if (success)
                {
                    response = envInfo;
                }
            }

            if (response != null)
            {
                await _actionCallbacks.SendReplyAsync(channel, response, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        private sealed class InvWornInfoContainer
        {
            public string FolderName { get; }
            public string CountIndicator { get; }

            public InvWornInfoContainer(string folderName, string countIndicator)
            {
                FolderName = folderName;
                CountIndicator = countIndicator;
            }

            public override string ToString()
            {
                return $"{FolderName}|{CountIndicator}";
            }
        }
        private static void GetInvWornInfo_Internal(RlvSharedFolder folder, bool recursive, ref int totalItems, ref int totalItemsWorn)
        {
            totalItemsWorn += folder.Items.Count(n => n.AttachedTo != null || n.WornOn != null || n.GestureState == RlvGestureState.Active);
            totalItems += folder.Items.Count;

            if (recursive)
            {
                foreach (var child in folder.Children)
                {
                    GetInvWornInfo_Internal(child, recursive, ref totalItems, ref totalItemsWorn);
                }
            }
        }

        private static string GetInvWornInfo(RlvSharedFolder folder)
        {
            // 0 : No item is present in that folder
            // 1 : Some items are present in that folder, but none of them is worn
            // 2 : Some items are present in that folder, and some of them are worn
            // 3 : Some items are present in that folder, and all of them are worn

            var totalItemsWorn = 0;
            var totalItems = 0;
            GetInvWornInfo_Internal(folder, false, ref totalItems, ref totalItemsWorn);

            var result = string.Empty;
            if (totalItems == 0)
            {
                result += "0";
            }
            else if (totalItemsWorn == 0)
            {
                result += "1";
            }
            else if (totalItems != totalItemsWorn)
            {
                result += "2";
            }
            else
            {
                result += "3";
            }

            var totalItemsWornRecursive = 0;
            var totalItemsRecursive = 0;
            GetInvWornInfo_Internal(folder, true, ref totalItemsRecursive, ref totalItemsWornRecursive);

            if (totalItemsRecursive == 0)
            {
                result += "0";
            }
            else if (totalItemsWornRecursive == 0)
            {
                result += "1";
            }
            else if (totalItemsRecursive != totalItemsWornRecursive)
            {
                result += "2";
            }
            else
            {
                result += "3";
            }

            return result;
        }

        private async Task<string> HandleGetInvWorn(string args, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var folders = new List<RlvSharedFolder>();

            var target = inventoryMap.Root;
            if (args.Length != 0)
            {
                if (!inventoryMap.TryGetFolderFromPath(args, false, out target))
                {
                    return string.Empty;
                }
            }

            var resultItems = new List<InvWornInfoContainer>
            {
                new("", GetInvWornInfo(target))
            };

            var foldersInInv = target.Children
                .Where(n => !n.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase));

            foreach (var folder in foldersInInv)
            {
                var invWornInfo = GetInvWornInfo(folder);
                resultItems.Add(new InvWornInfoContainer(folder.Name, invWornInfo));
            }

            var result = string.Join(",", resultItems);
            return result;
        }

        private static void SearchFoldersForName(RlvSharedFolder root, bool stopOnFirstResult, IEnumerable<string> searchTerms, List<RlvSharedFolder> outFoundFolders)
        {
            if (searchTerms.All(root.Name.Contains))
            {
                outFoundFolders.Add(root);

                if (stopOnFirstResult)
                {
                    return;
                }
            }

            foreach (var child in root.Children)
            {
                if (child.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                    child.Name.StartsWith("~", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SearchFoldersForName(child, stopOnFirstResult, searchTerms, outFoundFolders);
                if (stopOnFirstResult && outFoundFolders.Count > 0)
                {
                    return;
                }
            }
        }

        private async Task<string> HandleFindFolders(bool stopOnFirstResult, IEnumerable<string> searchTerms, string separator, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var foundFolders = new List<RlvSharedFolder>();
            SearchFoldersForName(inventoryMap.Root, stopOnFirstResult, searchTerms, foundFolders);

            var foundFolderPaths = new List<string>();
            foreach (var folder in foundFolders)
            {
                if (inventoryMap.TryBuildPathToFolder(folder.Id, out var foundPath))
                {
                    foundFolderPaths.Add(foundPath);
                }
            }

            var result = string.Join(separator, foundFolderPaths);

            return result;
        }

        private async Task<string> HandleGetInv(string args, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var target = inventoryMap.Root;
            if (args.Length != 0)
            {
                if (!inventoryMap.TryGetFolderFromPath(args, false, out target))
                {
                    return string.Empty;
                }
            }

            var foldersNamesInInv = target.Children
                .Where(n => !n.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                .Select(n => n.Name);

            var result = string.Join(",", foldersNamesInInv);
            return result;
        }

        private async Task<string> HandleGetPath(bool limitToOneResult, Guid? attachedPrimId, RlvAttachmentPoint? attachmentPoint, RlvWearableType? wearableType, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return string.Empty;
            }

            var folders = inventoryMap.FindFoldersContaining(limitToOneResult, attachedPrimId, attachmentPoint, wearableType);

            var sb = new StringBuilder();
            foreach (var folder in folders.OrderBy(n => n.Name))
            {
                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                if (inventoryMap.TryBuildPathToFolder(folder.Id, out var foundPath))
                {
                    sb.Append(foundPath);
                }
            }

            return sb.ToString();
        }

        internal async Task<bool> ProcessInstantMessageCommand(string message, Guid senderId, CancellationToken cancellationToken)
        {
            switch (message)
            {
                case "@version":
                    await _actionCallbacks.SendInstantMessageAsync(senderId, RlvService.RLVVersion, cancellationToken).ConfigureAwait(false);
                    return true;
                case "@getblacklist":
                    var blacklist = _blacklist.GetBlacklist();
                    await _actionCallbacks.SendInstantMessageAsync(senderId, string.Join(",", blacklist), cancellationToken).ConfigureAwait(false);
                    return true;
            }

            return false;
        }
    }
}
