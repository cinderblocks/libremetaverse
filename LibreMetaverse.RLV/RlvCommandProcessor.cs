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
            if (item.WornOn == null && item.AttachedTo == null && item.GestureState != RlvGestureState.Active)
            {
                return false;
            }

            if (enforceNostrip && item.Name.ToLowerInvariant().Contains("nostrip"))
            {
                return false;
            }

            // Special exception: If a folder with (nostrip) contains inventory links to other items, those linked items can still
            //   be removed. Only the objects actual parent folder or the actual item itself counts.
            if (enforceNostrip && !item.IsLink && item.Folder != null && item.Folder.Name.ToLowerInvariant().Contains("nostrip"))
            {
                return false;
            }

            if (item.WornOn is RlvWearableType.Skin or RlvWearableType.Shape or RlvWearableType.Eyes or RlvWearableType.Hair)
            {
                return false;
            }

            if (enforceRestrictions && !_manager.CanDetach(item))
            {
                return false;
            }

            return true;
        }

        private static void CollectItemsToAttach(RlvSharedFolder folder, InventoryMap inventoryMap, bool replaceExistingAttachments, bool recursive, bool skipIfPrivateFolder, Dictionary<Guid, AttachmentRequest> attachableItemMap)
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
                if (item.AttachedTo != null || item.WornOn != null || item.GestureState == RlvGestureState.Active)
                {
                    continue;
                }

                if (RlvCommon.TryGetAttachmentPointFromItemName(item.Name, out var attachmentPoint))
                {
                    if (!attachableItemMap.ContainsKey(item.Id))
                    {
                        attachableItemMap[item.Id] = new AttachmentRequest(item.Id, attachmentPoint.Value, replaceExistingAttachments);
                    }
                }
                else if (folderAttachmentPoint != null)
                {
                    if (!attachableItemMap.ContainsKey(item.Id))
                    {
                        attachableItemMap[item.Id] = new AttachmentRequest(item.Id, folderAttachmentPoint.Value, replaceExistingAttachments);
                    }
                }
                else
                {
                    if (!attachableItemMap.ContainsKey(item.Id))
                    {
                        attachableItemMap[item.Id] = new AttachmentRequest(item.Id, RlvAttachmentPoint.Default, replaceExistingAttachments);
                    }
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

                    CollectItemsToAttach(child, inventoryMap, replaceExistingAttachments, recursive, true, attachableItemMap);
                }
            }
        }

        private void CollectItemsToDetach(RlvSharedFolder folder, InventoryMap inventoryMap, bool recursive, bool skipIfPrivateFolder, bool enforceNoStrip, bool enforceRestrictions, Dictionary<Guid, bool> detachableItemMap)
        {
            if (skipIfPrivateFolder && folder.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UpdateDetachableItemsMap(folder.Items, enforceNoStrip, enforceRestrictions, detachableItemMap);

            if (recursive)
            {
                foreach (var child in folder.Children)
                {
                    CollectItemsToDetach(child, inventoryMap, recursive, true, enforceNoStrip, enforceRestrictions, detachableItemMap);
                }
            }
        }

        private void UpdateDetachableItemsMap(IEnumerable<RlvInventoryItem> items, bool enforceNostrip, bool enforceRestrictions, Dictionary<Guid, bool> detachableItemMap)
        {
            foreach (var item in items)
            {
                if (item.AttachedTo == null && item.WornOn == null && item.GestureState != RlvGestureState.Active)
                {
                    continue;
                }

                if (detachableItemMap.TryGetValue(item.Id, out var canDetach) && canDetach == false)
                {
                    continue;
                }

                canDetach = CanRemAttachItem(item, enforceNostrip, enforceRestrictions);
                detachableItemMap[item.Id] = canDetach;
            }
        }

        // @attach:[folder]=force
        private async Task<bool> HandleAttach(RlvMessage command, bool replaceExistingAttachments, bool recursive, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            if (!inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                await _actionCallbacks.AttachAsync(Array.Empty<AttachmentRequest>(), cancellationToken).ConfigureAwait(false);
                return false;
            }
            else
            {
                var attachableItemMap = new Dictionary<Guid, AttachmentRequest>();
                CollectItemsToAttach(folder, inventoryMap, replaceExistingAttachments, recursive, false, attachableItemMap);

                var itemsToAttach = attachableItemMap
                    .Values
                    .ToList();

                await _actionCallbacks.AttachAsync(itemsToAttach, cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        // @attachthis[:<attachableType>|<wearableType>|<attachedPrimId>]=force
        //
        //  - @attachthis
        //      * Attach all items in the folders where the sender of this command exists
        //
        //  - @attachthis:attachedPrimId
        //      * Attach all items in the folders where the attached prim id exists
        //
        //  - @attachthis:wearableType
        //      * Attach all items in folders containing worn wearables of the specified wearable type
        //
        //  - @attachthis:attachableType
        //      * Attach all items in folders containing attached attachables of the specified attachable type
        //
        //
        //  * BUG: Ignores locked folders restrictions (@attach[all]this=n)
        //  * Ignores .private folders
        //  
        private async Task<bool> HandleAttachThis(RlvMessage command, bool replaceExistingAttachments, bool recursive, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            var skipHiddenFolders = true;
            var foldersToAttachMap = new Dictionary<Guid, RlvSharedFolder>();

            if (command.Option.Length == 0)
            {
                var folders = inventoryMap.FindFoldersContaining(false, command.Sender, null, null);
                foreach (var folder in folders)
                {
                    foldersToAttachMap[folder.Id] = folder;
                }

                skipHiddenFolders = false;
            }
            else if (Guid.TryParse(command.Option, out var attachedPrimId))
            {
                if (!inventoryMap.TryGetItemByPrimId(attachedPrimId, out var items))
                {
                    return false;
                }

                foreach (var item in items)
                {
                    if (item.Folder != null)
                    {
                        foldersToAttachMap[item.Folder.Id] = item.Folder;
                    }
                }
            }
            else if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableType))
            {
                var folders = inventoryMap.FindFoldersContaining(false, null, null, wearableType);
                foreach (var folder in folders)
                {
                    foldersToAttachMap[folder.Id] = folder;
                }
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                var folders = inventoryMap.FindFoldersContaining(false, null, attachmentPoint, null);
                foreach (var folder in folders)
                {
                    foldersToAttachMap[folder.Id] = folder;
                }
            }
            else
            {
                return false;
            }

            var attachableItemMap = new Dictionary<Guid, AttachmentRequest>();
            foreach (var folder in foldersToAttachMap)
            {
                CollectItemsToAttach(folder.Value, inventoryMap, replaceExistingAttachments, recursive, skipHiddenFolders, attachableItemMap);
            }

            var itemsToAttach = attachableItemMap
                .Values
                .ToList();

            await _actionCallbacks.AttachAsync(itemsToAttach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @remattach[:<folder|attachpt|attachedPrimId>]=force
        //
        //  - @remattach
        //      * Detach all attached items
        //
        //  - @remattach:folder
        //      * Detach and unwear and deactivate everything possible in the folder
        //
        //  - @remattach:attachpt -
        //      * Detach all attachables of the specified type, as long as they are not restricted.
        //        Having a link to an object in multiple folder, and one of those folders is locked,
        //        the command will fail because one of the links is in a locked folder
        //
        //  - @remattach:attachedPrimId -
        //      * Detach the item that has the specified prim id (this is the id you get when
        //        you click 'copy keys' when editing the item attached to your avatar.
        //
        //  * Items with (nostrip) will be ignored
        //  * If any links to the targeted item exists in a locked folder (restricted detach), then
        //    the item will be ignored and not removed.
        //
        private async Task<bool> HandleRemAttach(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            var detachableItemMap = new Dictionary<Guid, bool>();

            if (command.Option.Length == 0)
            {
                var currentOutfit = inventoryMap.GetCurrentOutfit()
                    .Where(n => n.AttachedTo != null);

                UpdateDetachableItemsMap(currentOutfit, true, true, detachableItemMap);
            }
            else if (Guid.TryParse(command.Option, out var attachedPrimId))
            {
                if (inventoryMap.TryGetItemByPrimId(attachedPrimId, out var items))
                {
                    UpdateDetachableItemsMap(items, true, true, detachableItemMap);
                }
            }
            else if (inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                UpdateDetachableItemsMap(folder.Items, true, true, detachableItemMap);
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                if (inventoryMap.TryGetItemByAttachmentPoint(attachmentPoint, out var items))
                {
                    UpdateDetachableItemsMap(items, true, true, detachableItemMap);
                }
            }
            else
            {
                return false;
            }

            var itemIdsToDetach = detachableItemMap
                .Where(n => n.Value == true)
                .Select(n => n.Key)
                .ToList();

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }


        // @detachall:<folder>=force
        //
        //  - @detachall
        //      * Detach all attached items, unwear all worn items, and deactivate all gestures in the specified folder recursively
        //
        //  * Private folder (. prefix) will be ignored unless it's the target folder
        //  * Items with (nostrip) will be ignored
        //  * If any links to the targeted item exists in a locked folder (restricted detach), then
        //    the item will be ignored and not removed.
        // 
        private async Task<bool> HandleDetachAll(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            if (!inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                return false;
            }

            var detachableItemMap = new Dictionary<Guid, bool>();

            CollectItemsToDetach(folder, inventoryMap, true, false, true, true, detachableItemMap);

            var itemIdsToDetach = detachableItemMap
                .Where(n => n.Value == true)
                .Select(n => n.Key)
                .ToList();

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @detachthis[:<wearableType>|<attachableType>|<uuid>]=force
        //
        //  - @detachthis
        //      * Find all links and objects with the sender prim id and detach/unwear/deactivate everything from those folders
        //
        //  - @detachthis:wearableType
        //      * Find all links and objects worn as the specified wearable type and detach/unwear/deactivate everything from those folders
        //
        //  - @detachthis:attachableType
        //      * Find all links and objects attached to the specified location and detach/unwear/deactivate everything from those folders
        //
        //  - @detachthis:uuid
        //      * Find all links and objects with the given prim ID and detach/unwear/deactivate everything from those folders
        //
        //  * BUG: Takes into account rlv restrictions (@attachthis ignores restrictions, this one enforces restrictions. one of these is a bug in firestorm)
        //  * Private folders (. prefix) will be ignored
        //  * Items with (nostrip) will be ignored
        //  * If any links to the targeted item exists in a locked folder (restricted detach), then
        //    the item will be ignored and not removed.
        //
        private async Task<bool> HandleDetachThis(RlvMessage command, bool recursive, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            var folderPaths = new Dictionary<Guid, RlvSharedFolder>();
            var ignoreHiddenFolders = true;

            if (command.Option.Length == 0)
            {
                var parts = inventoryMap.FindFoldersContaining(false, command.Sender, null, null);
                foreach (var item in parts)
                {
                    folderPaths[item.Id] = item;
                }

                ignoreHiddenFolders = false;
            }
            else if (Guid.TryParse(command.Option, out var attachedPrimId))
            {
                if (!inventoryMap.TryGetItemByPrimId(attachedPrimId, out var items))
                {
                    return false;
                }

                foreach (var item in items)
                {
                    if (item.Folder != null)
                    {
                        folderPaths[item.Id] = item.Folder;
                    }
                }
            }
            else if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableType))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, null, wearableType);
                foreach (var item in parts)
                {
                    folderPaths[item.Id] = item;
                }
            }
            else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(command.Option, out var attachmentPoint))
            {
                var parts = inventoryMap.FindFoldersContaining(false, null, attachmentPoint, null);
                foreach (var item in parts)
                {
                    folderPaths[item.Id] = item;
                }
            }
            else
            {
                return false;
            }

            var detachableItemMap = new Dictionary<Guid, bool>();
            foreach (var item in folderPaths)
            {
                CollectItemsToDetach(item.Value, inventoryMap, recursive, ignoreHiddenFolders, true, true, detachableItemMap);
            }

            var itemIdsToDetach = detachableItemMap
                .Where(n => n.Value == true)
                .Select(n => n.Key)
                .ToList();

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @detachme=force
        //  Detach the item calling this command, unless it's inside of a locked folder
        //
        //  * Detaches the item even if the name or folder contains (nostrip)
        //
        //  * Does not detach the item if any links to it exist in a locked folder (restricted detach)
        private async Task<bool> HandleDetachMe(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            if (!inventoryMap.TryGetItemByPrimId(command.Sender, out var senderItems))
            {
                return false;
            }

            var detachableItemMap = new Dictionary<Guid, bool>();

            // NOTE: @detachme ignores nostrip tags
            UpdateDetachableItemsMap(senderItems, false, true, detachableItemMap);

            var itemIdsToDetach = detachableItemMap
                .Where(n => n.Value == true)
                .Select(n => n.Key)
                .ToList();

            await _actionCallbacks.DetachAsync(itemIdsToDetach, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // @remoutfit[:<folder|wearableType>]=force
        //
        //  - @remoutfit
        //      * Find all worn items and unwear them
        //      * This will search .private folders
        //
        //  - @remoutfit:wearableType
        //      * Find all links and objects worn as the specified wearable type and unwear them.
        //      * This will search .private folders
        //
        //  - @remoutfit:folder
        //      * Find all links and objects worn as the specified wearable type and detach/unwear/deactivate everything from those folders
        //
        //  * Items with (nostrip) will be ignored
        //  * If any links to the targeted item exists in a locked folder (restricted detach), then
        //    the item will be ignored and not removed.
        //
        // TODO: Add support for Attachment groups (RLVa)
        private async Task<bool> HandleRemOutfit(RlvMessage command, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            var detachableItemMap = new Dictionary<Guid, bool>();

            if (command.Option.Length == 0)
            {
                var currentlyWornItems = inventoryMap
                    .GetCurrentOutfit()
                    .Where(n => n.WornOn != null);

                UpdateDetachableItemsMap(currentlyWornItems, true, true, detachableItemMap);
            }
            else if (RlvCommon.RlvWearableTypeMap.TryGetValue(command.Option, out var wearableType))
            {
                if (inventoryMap.TryGetItemByWearableType(wearableType, out var wearableItems))
                {
                    UpdateDetachableItemsMap(wearableItems, true, true, detachableItemMap);
                }
            }
            else if (inventoryMap.TryGetFolderFromPath(command.Option, false, out var folder))
            {
                CollectItemsToDetach(folder, inventoryMap, false, false, true, true, detachableItemMap);
            }
            else
            {
                return false;
            }

            var itemIdsToDetach = detachableItemMap
                .Where(n => n.Value == true)
                .Select(n => n.Key)
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
