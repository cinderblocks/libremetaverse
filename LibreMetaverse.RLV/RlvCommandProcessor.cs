using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    public class RlvCommandProcessor
    {
        private readonly ImmutableDictionary<string, Func<RlvMessage, CancellationToken, Task<bool>>> _rlvActionHandlers;

        // TODO: Swap manager out with an interface once it's been solidified into only useful stuff
        private readonly RlvPermissionsService _manager;
        private readonly IRlvQueryCallbacks _queryCallbacks;
        private readonly IRlvActionCallbacks _actionCallbacks;

        internal RlvCommandProcessor(RlvPermissionsService manager, IRlvQueryCallbacks callbacks, IRlvActionCallbacks actionCallbacks)
        {
            _manager = manager;
            _queryCallbacks = callbacks;
            _actionCallbacks = actionCallbacks;

            _rlvActionHandlers = new Dictionary<string, Func<RlvMessage, CancellationToken, Task<bool>>>()
            {
                { "setrot", HandleSetRot },
                { "adjustheight", HandleAdjustHeight},
                { "setcam_fov", HandleSetCamFOV},
                { "tpto", HandleTpTo},
                { "sit", HandleSit},
                { "unsit", HandleUnsit},
                { "sitground", HandleSitGround},
                { "remoutfit", HandleRemOutfit},
                { "detachme", HandleDetachMe},
                { "remattach", HandleRemAttach},
                { "detach", HandleRemAttach},
                { "detachall", HandleDetachAll},
                { "detachthis", (command, cancellationToken) => HandleDetachThis(command, false, cancellationToken)},
                { "detachallthis", (command, cancellationToken) => HandleDetachThis(command, true, cancellationToken)},
                { "setgroup", HandleSetGroup},
                { "setdebug_", HandleSetDebug},
                { "setenv_", HandleSetEnv},

                { "attach", (command, cancellationToken) => HandleAttach(command, true, false, cancellationToken)},
                { "attachall", (command, cancellationToken) => HandleAttach(command, true, true, cancellationToken)},
                { "attachover", (command, cancellationToken) => HandleAttach(command, false, false, cancellationToken)},
                { "attachallover", (command, cancellationToken) => HandleAttach(command, false, true, cancellationToken)},
                { "attachthis", (command, cancellationToken) => HandleAttachThis(command, true, false, cancellationToken)},
                { "attachallthis", (command, cancellationToken) => HandleAttachThis(command, true, true, cancellationToken)},
                { "attachthisover", (command, cancellationToken) => HandleAttachThis(command, false, false, cancellationToken)},
                { "attachallthisover", (command, cancellationToken) => HandleAttachThis(command, false, true, cancellationToken)},

                // addoutfit* -> attach* (These are all aliases of their corresponding attach command)
                { "addoutfit", (command, cancellationToken) => HandleAttach(command, true, false, cancellationToken)},
                { "addoutfitall", (command, cancellationToken) => HandleAttach(command, true, true, cancellationToken)},
                { "addoutfitover", (command, cancellationToken) => HandleAttach(command, false, false, cancellationToken)},
                { "addoutfitallover", (command, cancellationToken) => HandleAttach(command, false, true, cancellationToken)},
                { "addoutfitthis", (command, cancellationToken) => HandleAttachThis(command, true, false, cancellationToken)},
                { "addoutfitallthis", (command, cancellationToken) => HandleAttachThis(command, true, true, cancellationToken)},
                { "addoutfitthisover", (command, cancellationToken) => HandleAttachThis(command, false, false, cancellationToken)},
                { "addoutfitallthisover", (command, cancellationToken) => HandleAttachThis(command, false, true, cancellationToken)},

                // *overorreplace -> *  (These are all aliases of their corresponding attach command)
                { "attachoverorreplace", (command, cancellationToken) => HandleAttach(command, true, false, cancellationToken)},
                { "attachalloverorreplace", (command, cancellationToken) => HandleAttach(command, true, true, cancellationToken)},
                { "attachthisoverorreplace", (command, cancellationToken) => HandleAttachThis(command, true, false, cancellationToken)},
                { "attachallthisoverorreplace", (command, cancellationToken) => HandleAttachThis(command, true, true, cancellationToken)},
            }.ToImmutableDictionary();
        }

        internal async Task<bool> ProcessActionCommand(RlvMessage command, CancellationToken cancellationToken)
        {
            if (_rlvActionHandlers.TryGetValue(command.Behavior, out var func))
            {
                return await func(command, cancellationToken).ConfigureAwait(false);
            }
            else if (command.Behavior.StartsWith("setdebug_", StringComparison.OrdinalIgnoreCase))
            {
                return await _rlvActionHandlers["setdebug_"](command, cancellationToken).ConfigureAwait(false);
            }
            else if (command.Behavior.StartsWith("setenv_", StringComparison.OrdinalIgnoreCase))
            {
                return await _rlvActionHandlers["setenv_"](command, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        private async Task<bool> HandleSetDebug(RlvMessage command, CancellationToken cancellationToken)
        {
            var separatorIndex = command.Behavior.IndexOf('_');
            if (separatorIndex == -1)
            {
                return false;
            }

            var settingName = command.Behavior.Substring(separatorIndex + 1);
            if (settingName.Length == 0)
            {
                return false;
            }

            await _actionCallbacks.SetDebugAsync(settingName, command.Option, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSetEnv(RlvMessage command, CancellationToken cancellationToken)
        {
            var separatorIndex = command.Behavior.IndexOf('_');
            if (separatorIndex == -1)
            {
                return false;
            }

            var settingName = command.Behavior.Substring(separatorIndex + 1);
            if (settingName.Length == 0)
            {
                return false;
            }

            await _actionCallbacks.SetEnvAsync(settingName, command.Option, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSetGroup(RlvMessage command, CancellationToken cancellationToken)
        {
            var argParts = command.Option.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            if (argParts.Length == 0)
            {
                return false;
            }

            string? groupRole = null;
            if (argParts.Length > 1)
            {
                groupRole = argParts[1];
            }

            if (Guid.TryParse(argParts[0], out var groupId))
            {
                await _actionCallbacks.SetGroupAsync(groupId, groupRole, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _actionCallbacks.SetGroupAsync(argParts[0], groupRole, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        private bool CanRemAttachItem(RlvInventoryItem item, bool enforceNostrip, bool enforceRestrictions)
        {
            if (item.WornOn == null && item.AttachedTo == null)
            {
                return false;
            }

            if (item.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (enforceNostrip && item.Name.ToLowerInvariant().Contains("nostrip"))
            {
                return false;
            }

            if (enforceNostrip && item.Folder != null && item.Folder.Name.ToLowerInvariant().Contains("nostrip"))
            {
                return false;
            }

            if (enforceRestrictions && !_manager.CanDetach(item, true))
            {
                return false;
            }

            if (item.WornOn is RlvWearableType.Skin or RlvWearableType.Shape or RlvWearableType.Eyes or RlvWearableType.Hair)
            {
                return false;
            }

            return true;
        }

        private static void CollectItemsToAttach(RlvSharedFolder folder, bool replaceExistingAttachments, bool recursive, bool skipIfPrivateFolder, List<AttachmentRequest> itemsToAttach)
        {
            if (skipIfPrivateFolder && folder.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (folder.Name.StartsWith("+", StringComparison.OrdinalIgnoreCase))
            {
                replaceExistingAttachments = false;
            }

            RlvAttachmentPoint? folderAttachmentPoint = null;
            if (RlvCommon.TryGetAttachmentPointFromItemName(folder.Name, out var attachmentPointTemp))
            {
                folderAttachmentPoint = attachmentPointTemp;
            }

            foreach (var item in folder.Items)
            {
                if (item.AttachedTo != null || item.WornOn != null)
                {
                    continue;
                }

                if (item.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (RlvCommon.TryGetAttachmentPointFromItemName(item.Name, out var attachmentPoint))
                {
                    itemsToAttach.Add(new AttachmentRequest(item.Id, attachmentPoint.Value, replaceExistingAttachments));
                }
                else if (folderAttachmentPoint != null)
                {
                    itemsToAttach.Add(new AttachmentRequest(item.Id, folderAttachmentPoint.Value, replaceExistingAttachments));
                }
                else
                {
                    itemsToAttach.Add(new AttachmentRequest(item.Id, RlvAttachmentPoint.Default, replaceExistingAttachments));
                }
            }

            if (recursive)
            {
                foreach (var child in folder.Children)
                {
                    if (child.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CollectItemsToAttach(child, replaceExistingAttachments, recursive, true, itemsToAttach);
                }
            }
        }

        // @attach:[folder]=force
        private async Task<bool> HandleAttach(RlvMessage command, bool replaceExistingAttachments, bool recursive, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }
            var inventoryMap = new InventoryMap(sharedFolder);

            if (!inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                await _actionCallbacks.AttachAsync(Array.Empty<AttachmentRequest>(), cancellationToken).ConfigureAwait(false);
                return false;
            }
            else
            {
                var itemsToAttach = new List<AttachmentRequest>();
                CollectItemsToAttach(folder, replaceExistingAttachments, recursive, false, itemsToAttach);

                await _actionCallbacks.AttachAsync(itemsToAttach, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        private async Task<bool> HandleAttachThis(RlvMessage command, bool replaceExistingAttachments, bool recursive, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }

            var skipHiddenFolders = true;
            var inventoryMap = new InventoryMap(sharedFolder);
            var folderPaths = new List<RlvSharedFolder>();

            if (command.Option.Length == 0)
            {
                var parts = inventoryMap.FindFoldersContaining(false, command.Sender, null, null);
                folderPaths.AddRange(parts);
                skipHiddenFolders = false;
            }
            else if (Guid.TryParse(command.Option, out var attachedPrimId))
            {
                var item = inventoryMap.Items
                    .Where(n => n.Value.AttachedPrimId == attachedPrimId)
                    .Select(n => n.Value)
                    .FirstOrDefault();
                if (item == null)
                {
                    return false;
                }

                if (item.FolderId.HasValue && inventoryMap.Folders.TryGetValue(item.FolderId.Value, out var folder))
                {
                    folderPaths.Add(folder);
                }
            }
            else if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableType))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, null, wearableType);
                folderPaths.AddRange(parts);
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, attachmentPoint, null);
                folderPaths.AddRange(parts);
            }
            else
            {
                return false;
            }

            var itemsToAttach = new List<AttachmentRequest>();
            foreach (var item in folderPaths)
            {
                CollectItemsToAttach(item, replaceExistingAttachments, recursive, skipHiddenFolders, itemsToAttach);
            }

            await _actionCallbacks.AttachAsync(itemsToAttach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private static void CollectItemsToDetach(RlvSharedFolder folder, InventoryMap inventoryMap, bool recursive, bool skipIfPrivateFolder, List<Guid> itemsToDetach)
        {
            if (skipIfPrivateFolder && folder.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var item in folder.Items)
            {
                if (item.AttachedTo == null && item.WornOn == null)
                {
                    continue;
                }

                itemsToDetach.Add(item.Id);
            }

            if (recursive)
            {
                foreach (var child in folder.Children)
                {
                    CollectItemsToDetach(child, inventoryMap, recursive, true, itemsToDetach);
                }
            }
        }

        // @remattach[:<folder|attachpt|uuid>]=force
        // TODO: Add support for Attachment groups (RLVa)
        private async Task<bool> HandleRemAttach(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }

            var (hasCurrentOutfit, currentOutfit) = await _queryCallbacks.TryGetCurrentOutfitAsync(cancellationToken).ConfigureAwait(false);
            if (!hasCurrentOutfit || currentOutfit == null)
            {
                return false;
            }

            var inventoryMap = new InventoryMap(sharedFolder);

            var itemIdsToDetach = new List<Guid>();

            if (Guid.TryParse(command.Option, out var uuid))
            {
                var item = currentOutfit.FirstOrDefault(n => n.AttachedPrimId == uuid);
                if (item != null)
                {
                    if (CanRemAttachItem(item, true, false))
                    {
                        itemIdsToDetach.Add(item.Id);
                    }
                }
            }
            else if (inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                CollectItemsToDetach(folder, inventoryMap, false, false, itemIdsToDetach);
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                itemIdsToDetach = currentOutfit
                    .Where(n =>
                        n.AttachedTo == attachmentPoint &&
                        CanRemAttachItem(n, true, false)
                    )
                    .Select(n => n.Id)
                    .Distinct()
                    .ToList();
            }
            else if (command.Option.Length == 0)
            {
                // Everything attachable will be detached (excludes clothing/wearable types)
                itemIdsToDetach = currentOutfit
                    .Where(n =>
                        n.AttachedTo != null && CanRemAttachItem(n, true, false)
                    )
                    .Select(n => n.Id)
                    .Distinct()
                    .ToList();
            }
            else
            {
                return false;
            }

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleDetachAll(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }
            var inventoryMap = new InventoryMap(sharedFolder);

            if (!inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                return false;
            }

            var itemIdsToDetach = new List<Guid>();
            CollectItemsToDetach(folder, inventoryMap, true, false, itemIdsToDetach);

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleDetachThis(RlvMessage command, bool recursive, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }
            var inventoryMap = new InventoryMap(sharedFolder);
            var folderPaths = new List<RlvSharedFolder>();
            var ignoreHiddenFolders = true;

            if (command.Option.Length == 0)
            {
                var parts = inventoryMap.FindFoldersContaining(false, command.Sender, null, null);
                folderPaths.AddRange(parts);
                ignoreHiddenFolders = false;
            }
            else if (Guid.TryParse(command.Option, out var attachedPrimId))
            {
                var item = inventoryMap.Items
                    .Where(n => n.Value.AttachedPrimId == attachedPrimId)
                    .Select(n => n.Value)
                    .FirstOrDefault();
                if (item == null)
                {
                    return false;
                }

                if (item.FolderId.HasValue && inventoryMap.Folders.TryGetValue(item.FolderId.Value, out var folder))
                {
                    folderPaths.Add(folder);
                }
            }
            else if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableType))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, null, wearableType);
                folderPaths.AddRange(parts);
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, attachmentPoint, null);
                folderPaths.AddRange(parts);
            }
            else
            {
                return false;
            }

            var itemIdsToDetach = new List<Guid>();
            foreach (var item in folderPaths)
            {
                CollectItemsToDetach(item, inventoryMap, recursive, ignoreHiddenFolders, itemIdsToDetach);
            }

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @detachme=force
        private async Task<bool> HandleDetachMe(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }
            var inventoryMap = new InventoryMap(sharedFolder);

            var senderItem = inventoryMap.Items
                .Where(n => n.Value.AttachedPrimId == command.Sender)
                .Select(n => n.Value)
                .FirstOrDefault();

            if (senderItem == null)
            {
                return false;
            }

            if (!CanRemAttachItem(senderItem, false, false))
            {
                return false;
            }

            var itemIdsToDetach = new List<Guid>
            {
                senderItem.Id
            };

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @remoutfit[:<folder|layer>]=force
        // TODO: Add support for Attachment groups (RLVa)
        private async Task<bool> HandleRemOutfit(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasCurrentOutfit, currentOutfit) = await _queryCallbacks.TryGetCurrentOutfitAsync(cancellationToken).ConfigureAwait(false);
            if (!hasCurrentOutfit || currentOutfit == null)
            {
                return false;
            }
            var (hasSharedFolder, sharedFolder) = await _queryCallbacks.TryGetSharedFolderAsync(cancellationToken).ConfigureAwait(false);
            if (!hasSharedFolder || sharedFolder == null)
            {
                return false;
            }

            var inventoryMap = new InventoryMap(sharedFolder);

            Guid? folderId = null;
            RlvWearableType? wearableType = null;

            if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableTypeTemp))
            {
                wearableType = wearableTypeTemp;
            }
            else if (inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                folderId = folder.Id;
            }
            else if (command.Option.Length != 0)
            {
                return false;
            }

            var itemsToDetach = currentOutfit
                .Where(n =>
                    n.WornOn != null &&
                    (folderId == null || n.FolderId == folderId) &&
                    (wearableType == null || n.WornOn == wearableType) &&
                    CanRemAttachItem(n, true, false)
                )
                .ToList();

            var itemIdsToDetach = itemsToDetach
                .Select(n => n.Id)
                .Distinct()
                .ToList();

            await _actionCallbacks.RemOutfitAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleUnsit(RlvMessage command, CancellationToken cancellationToken)
        {
            if (!_manager.CanUnsit())
            {
                return false;
            }

            await _actionCallbacks.UnsitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSitGround(RlvMessage command, CancellationToken cancellationToken)
        {
            if (!_manager.CanSit())
            {
                return false;
            }

            await _actionCallbacks.SitGroundAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSetRot(RlvMessage command, CancellationToken cancellationToken)
        {
            if (!float.TryParse(command.Option, out var angleInRadians))
            {
                return false;
            }

            await _actionCallbacks.SetRotAsync(angleInRadians, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleAdjustHeight(RlvMessage command, CancellationToken cancellationToken)
        {
            var args = command.Option.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 1)
            {
                return false;
            }

            if (!float.TryParse(args[0], out var distance))
            {
                return false;
            }

            var factor = 1.0f;
            var deltaInMeters = 0.0f;

            if (args.Length > 1 && !float.TryParse(args[1], out factor))
            {
                factor = 1;
            }

            if (args.Length > 2 && !float.TryParse(args[2], out deltaInMeters))
            {
                deltaInMeters = 0;
            }

            await _actionCallbacks.AdjustHeightAsync(distance, factor, deltaInMeters, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSetCamFOV(RlvMessage command, CancellationToken cancellationToken)
        {
            var cameraRestrictions = _manager.GetCameraRestrictions();
            if (cameraRestrictions.IsLocked)
            {
                return false;
            }

            if (!float.TryParse(command.Option, out var fov))
            {
                return false;
            }

            await _actionCallbacks.SetCamFOVAsync(fov, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleSit(RlvMessage command, CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(command.Option, out var sitTarget))
            {
                return false;
            }

            if (!_manager.CanSit())
            {
                return false;
            }

            var objectExists = await _queryCallbacks.ObjectExistsAsync(sitTarget, cancellationToken).ConfigureAwait(false);
            if (!objectExists)
            {
                return false;
            }

            var isCurrentlySitting = await _queryCallbacks.IsSittingAsync(cancellationToken).ConfigureAwait(false);
            if (isCurrentlySitting)
            {
                if (!_manager.CanUnsit())
                {
                    return false;
                }

                if (!_manager.CanStandTp())
                {
                    return false;
                }
            }

            await _actionCallbacks.SitAsync(sitTarget, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private async Task<bool> HandleTpTo(RlvMessage command, CancellationToken cancellationToken)
        {
            // @tpto is inhibited by @tploc=n, by @unsit too.
            if (!_manager.CanTpLoc())
            {
                return false;
            }
            if (!_manager.CanUnsit())
            {
                return false;
            }

            var commandArgs = command.Option.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            var locationArgs = commandArgs[0].Split('/');

            if (locationArgs.Length is < 3 or > 4)
            {
                return false;
            }

            float? lookat = null;
            if (commandArgs.Length > 1)
            {
                if (!float.TryParse(commandArgs[1], out var val))
                {
                    return false;
                }

                lookat = val;
            }

            if (locationArgs.Length == 3)
            {
                if (!float.TryParse(locationArgs[0], out var x))
                {
                    return false;
                }
                if (!float.TryParse(locationArgs[1], out var y))
                {
                    return false;
                }
                if (!float.TryParse(locationArgs[2], out var z))
                {
                    return false;
                }

                await _actionCallbacks.TpToAsync(x, y, z, null, lookat, cancellationToken).ConfigureAwait(false);
                return true;
            }
            else if (locationArgs.Length == 4)
            {
                var regionName = locationArgs[0];

                if (!float.TryParse(locationArgs[1], out var x))
                {
                    return false;
                }
                if (!float.TryParse(locationArgs[2], out var y))
                {
                    return false;
                }
                if (!float.TryParse(locationArgs[3], out var z))
                {
                    return false;
                }

                await _actionCallbacks.TpToAsync(x, y, z, regionName, lookat, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }
    }
}
