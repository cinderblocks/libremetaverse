using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    internal sealed class LockedFolderManager
    {
        private readonly IRlvQueryCallbacks _queryCallbacks;
        private readonly RlvRestrictionManager _restrictionManager;

        private readonly Dictionary<Guid, LockedFolder> _lockedFolders = [];
        private readonly object _lockedFoldersLock = new();

        internal LockedFolderManager(IRlvQueryCallbacks queryCallbacks, RlvRestrictionManager restrictionManager)
        {
            _queryCallbacks = queryCallbacks;
            _restrictionManager = restrictionManager;
        }

        public IReadOnlyDictionary<Guid, LockedFolderPublic> GetLockedFolders()
        {
            lock (_lockedFoldersLock)
            {
                return _lockedFolders
                    .Select(n => new LockedFolderPublic(n.Value))
                    .ToImmutableDictionary(k => k.Id, v => v);
            }
        }

        public bool TryGetLockedFolder(Guid folderId, [NotNullWhen(true)] out LockedFolderPublic? lockedFolder)
        {
            lock (_lockedFoldersLock)
            {
                if (_lockedFolders.TryGetValue(folderId, out var lockedFolderPrivate))
                {
                    lockedFolder = new LockedFolderPublic(lockedFolderPrivate);
                    return true;
                }

                lockedFolder = default;
                return false;
            }
        }

        private void AddLockedFolder(RlvSharedFolder folder, RlvRestriction restriction)
        {
            lock (_lockedFoldersLock)
            {
                if (!_lockedFolders.TryGetValue(folder.Id, out var existingLockedFolder))
                {
                    existingLockedFolder = new LockedFolder(folder);
                    _lockedFolders[folder.Id] = existingLockedFolder;
                }

                if (restriction.Behavior is RlvRestrictionType.DetachAllThis or RlvRestrictionType.DetachThis)
                {
                    existingLockedFolder.DetachRestrictions.Add(restriction);
                }
                else if (restriction.Behavior is RlvRestrictionType.AttachAllThis or RlvRestrictionType.AttachThis)
                {
                    existingLockedFolder.AttachRestrictions.Add(restriction);
                }
                else if (restriction.Behavior is RlvRestrictionType.DetachAllThisExcept or RlvRestrictionType.DetachThisExcept)
                {
                    existingLockedFolder.DetachExceptions.Add(restriction);
                }
                else if (restriction.Behavior is RlvRestrictionType.AttachAllThisExcept or RlvRestrictionType.AttachThisExcept)
                {
                    existingLockedFolder.AttachExceptions.Add(restriction);
                }

                if (restriction.Behavior is RlvRestrictionType.DetachAllThis or
                    RlvRestrictionType.AttachAllThis or
                    RlvRestrictionType.AttachAllThisExcept or
                    RlvRestrictionType.DetachAllThisExcept)
                {
                    foreach (var child in folder.Children)
                    {
                        AddLockedFolder(child, restriction);
                    }
                }
            }
        }

        internal async Task RebuildLockedFolders(CancellationToken cancellationToken)
        {
            // AttachThis/DetachThis - Only search within the #RLV root
            //  Attachment:
            //      Find attachment object
            //      Get folder object exists in (assuming it exists in the #RLV folder) and add it to the locked folder list
            //
            //  Shared Folder:
            //      Just add the shared folder to the locked folder list
            //
            //  Attachment point / Wearable Type:
            //      Find and all all the folders for all of the attachments in the specified attachment point or of the wearable type.
            //      Add those folders to the locked folder list

            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return;
            }

            lock (_lockedFoldersLock)
            {
                _lockedFolders.Clear();

                var detachThisRestrictions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.DetachThis);
                var detachAllThisRestrictions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.DetachAllThis);
                var attachThisRestrictions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.AttachThis);
                var attachAllThisRestrictions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.AttachAllThis);
                var detachThisExceptions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.DetachThisExcept);
                var detachAllThisExceptions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.DetachAllThisExcept);
                var attachThisExceptions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.AttachThisExcept);
                var attachAllThisExceptions = _restrictionManager.GetRestrictionsByType(RlvRestrictionType.AttachAllThisExcept);

                foreach (var restriction in detachThisRestrictions)
                {
                    ProcessFolderRestrictions(restriction, inventoryMap.Root, inventoryMap);
                }
                foreach (var restriction in detachAllThisRestrictions)
                {
                    ProcessFolderRestrictions(restriction, inventoryMap.Root, inventoryMap);
                }
                foreach (var restriction in attachThisRestrictions)
                {
                    ProcessFolderRestrictions(restriction, inventoryMap.Root, inventoryMap);
                }
                foreach (var restriction in attachAllThisRestrictions)
                {
                    ProcessFolderRestrictions(restriction, inventoryMap.Root, inventoryMap);
                }
                foreach (var exception in detachThisExceptions)
                {
                    ProcessFolderException(exception, inventoryMap);
                }
                foreach (var exception in detachAllThisExceptions)
                {
                    ProcessFolderException(exception, inventoryMap);
                }
                foreach (var exception in attachThisExceptions)
                {
                    ProcessFolderException(exception, inventoryMap);
                }
                foreach (var exception in attachAllThisExceptions)
                {
                    ProcessFolderException(exception, inventoryMap);
                }
            }
        }

        internal async Task<bool> ProcessFolderException(RlvRestriction restriction, bool isException, CancellationToken cancellationToken)
        {
            var (hasInventoryMap, inventoryMap) = await _queryCallbacks.TryGetInventoryMapAsync(cancellationToken).ConfigureAwait(false);
            if (!hasInventoryMap || inventoryMap == null)
            {
                return false;
            }

            if (isException)
            {
                return ProcessFolderException(restriction, inventoryMap);
            }
            else
            {
                return ProcessFolderRestrictions(restriction, inventoryMap.Root, inventoryMap);
            }
        }

        private bool ProcessFolderException(RlvRestriction exception, InventoryMap inventoryMap)
        {
            if (exception.Args.Count == 0)
            {
                return false;
            }
            else if (exception.Args[0] is string path)
            {
                if (!inventoryMap.TryGetFolderFromPath(path, false, out var folder))
                {
                    return false;
                }

                AddLockedFolder(folder, exception);
            }

            return true;
        }

        private bool ProcessFolderRestrictions(RlvRestriction restriction, RlvSharedFolder sharedFolder, InventoryMap inventoryMap)
        {
            if (restriction.Args.Count == 0)
            {
                if (!inventoryMap.TryGetItemByPrimId(restriction.Sender, out var senderItems))
                {
                    return false;
                }

                foreach (var senderItem in senderItems)
                {
                    if (senderItem.Folder == null)
                    {
                        return false;
                    }

                    AddLockedFolder(senderItem.Folder, restriction);
                }
            }
            else if (restriction.Args[0] is RlvWearableType wearableType)
            {
                var foldersToLock = new Dictionary<Guid, RlvSharedFolder>();
                foreach (var items in inventoryMap.Items)
                {
                    foreach (var item in items.Value)
                    {
                        if (item.Folder != null && item.WornOn == wearableType)
                        {
                            foldersToLock[item.Folder.Id] = item.Folder;
                        }
                    }
                }

                foreach (var folder in foldersToLock.Values)
                {
                    AddLockedFolder(folder, restriction);
                }
            }
            else if (restriction.Args[0] is RlvAttachmentPoint attachmentPoint)
            {
                var foldersToLockMap = new Dictionary<Guid, RlvSharedFolder>();
                foreach (var items in inventoryMap.Items)
                {
                    foreach (var item in items.Value)
                    {
                        if (item.Folder != null && item.AttachedTo == attachmentPoint)
                        {
                            foldersToLockMap[item.Folder.Id] = item.Folder;
                        }
                    }
                }

                foreach (var folder in foldersToLockMap.Values)
                {
                    AddLockedFolder(folder, restriction);
                }
            }
            else if (restriction.Args[0] is string path)
            {
                if (!inventoryMap.TryGetFolderFromPath(path, false, out var folder))
                {
                    return false;
                }

                AddLockedFolder(folder, restriction);
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
