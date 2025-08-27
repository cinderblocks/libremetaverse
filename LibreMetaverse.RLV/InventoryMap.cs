using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace LibreMetaverse.RLV
{
    public class InventoryMap
    {
        public ImmutableDictionary<Guid, ImmutableList<RlvInventoryItem>> Items { get; }
        public ImmutableDictionary<Guid, RlvInventoryItem> ExternalItems { get; }
        public ImmutableDictionary<Guid, RlvSharedFolder> Folders { get; }
        public RlvSharedFolder Root { get; }

        /// <summary>
        /// Creates a mapping of all items and folders for a given InventoryFolder.
        /// </summary>
        /// <param name="root">Root of the shared folder. Generally the #RLV folder.</param>
        /// <param name="externalItems">Collection of worn or attached external items that exist outside of the #RLV folder.</param>
        /// <exception cref="ArgumentNullException">root is null</exception>
        public InventoryMap(RlvSharedFolder root, IReadOnlyCollection<RlvInventoryItem> externalItems)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var itemsTemp = new Dictionary<Guid, List<RlvInventoryItem>>();
            var foldersTemp = new Dictionary<Guid, RlvSharedFolder>();

            CreateInventoryMap(root, foldersTemp, itemsTemp);

            Root = root;
            Items = itemsTemp.ToImmutableDictionary(k => k.Key, v => v.Value.ToImmutableList());
            Folders = foldersTemp.ToImmutableDictionary();
            ExternalItems = externalItems.ToImmutableDictionary(k => k.Id, v => v);
        }

        private bool TryGetFolderFromPath_Internal(string path, bool skipPrivateFolders, [NotNullWhen(returnValue: true)] out RlvSharedFolder? folder)
        {
            if (string.IsNullOrEmpty(path))
            {
                folder = null;
                return false;
            }

            var iter = Root;
            while (true)
            {
                RlvSharedFolder? candidate = null;
                var candidateNameLengthSelected = 0;
                var candidatePathRemaining = string.Empty;
                var candidateHasPrefix = false;

                foreach (var child in iter.Children)
                {
                    if (skipPrivateFolders && child.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var fixedChildName = child.Name;
                    var hasPrefix = false;

                    // Only fix the child name if we don't already have an exact match with path
                    if (!path.StartsWith(child.Name, StringComparison.OrdinalIgnoreCase) &&
                        (
                            child.Name.StartsWith(".", StringComparison.OrdinalIgnoreCase) ||
                            child.Name.StartsWith("~", StringComparison.OrdinalIgnoreCase) ||
                            child.Name.StartsWith("+", StringComparison.OrdinalIgnoreCase)
                        ))
                    {
                        fixedChildName = fixedChildName.Substring(1);
                        hasPrefix = true;
                    }

                    if (path.StartsWith(fixedChildName, StringComparison.OrdinalIgnoreCase))
                    {
                        // This whole candidate system should probably be redone as a recursive search to find the best possible exact path, but this
                        //   should be good enough for now
                        //
                        // We currently pick the best candidate based on:
                        //  1. The longest candidate that exists at the start of the path and ends with a '/' or matches the remaining path exactly.
                        //     For example, a folder containing "Clothing/Hats" and "Clothing" with a subfolder of "Hats", we would prefer the longest
                        //     match of "Clothing/Hats" first even though in the path they both represent is "#RLV/Clothing/Hats"
                        //
                        //  2. Exact matches are preferred over matches that have the prefix removed, for example if we are searching for a "Clothing"
                        //     folder in a folder that contains "Clothing" and "+Clothing", we prefer the one without the prefix first
                        //
                        //  3. The first exact match is preferred. If there are multiple "Clothing" folders, just pick the first one that appears

                        if (candidate == null ||
                            fixedChildName.Length > candidateNameLengthSelected ||
                            (fixedChildName.Length == candidateNameLengthSelected && !hasPrefix && candidateHasPrefix))
                        {
                            if (path.Length == fixedChildName.Length)
                            {
                                candidatePathRemaining = "";
                                candidate = child;
                                candidateNameLengthSelected = fixedChildName.Length;
                                candidateHasPrefix = hasPrefix;
                                break;
                            }

                            if (path.Length > fixedChildName.Length && path[fixedChildName.Length] == '/')
                            {
                                candidatePathRemaining = path.Substring(fixedChildName.Length + 1);
                                candidate = child;
                                candidateNameLengthSelected = fixedChildName.Length;
                                candidateHasPrefix = hasPrefix;
                            }
                        }
                    }
                }

                if (candidate == null)
                {
                    folder = null;
                    return false;
                }

                path = candidatePathRemaining;
                if (path.Length == 0)
                {
                    folder = candidate;
                    return true;
                }

                iter = candidate;
            }
        }

        /// <summary>
        /// Attempts to find a folder under the root rlv folder #RLV by the given path.
        /// Folders are not case sensitive. Folders may containing a special prefix (~, +),
        /// which will be treated as if the folder did not have the prefix, unless the path
        /// contains the prefix as well then an exact match will be made.
        /// Example:
        ///     Existing shared folder path: #RLV/Clothing/+Hats/+Fancy
        ///     search term: "clothing/hats/fancy"
        ///     results: The object representing Clothing/+Hats/+Fancy
        /// </summary>
        /// <param name="path">Forward-slash separated folder path. Do not include "#RLV/" as part of the path. Do not start with or end with a forward slash.</param>
        /// <param name="skipPrivateFolders">If true, ignores folders starting with '.'</param>
        /// <param name="folder">The found folder, or null if not found</param>
        /// <returns>True if folder was found, false otherwise</returns>
        public bool TryGetFolderFromPath(string path, bool skipPrivateFolders, [NotNullWhen(returnValue: true)] out RlvSharedFolder? folder)
        {
            if (string.IsNullOrEmpty(path))
            {
                folder = null;
                return false;
            }

            if (TryGetFolderFromPath_Internal(path, skipPrivateFolders, out folder))
            {
                return true;
            }

            // Try without a leading '/' if one was supplied "/~MyOutfit" -> "~MyOutfit"
            if (path.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                var newPath = path.Substring(1);
                if (TryGetFolderFromPath_Internal(newPath, skipPrivateFolders, out folder))
                {
                    return true;
                }
            }

            // Try without a trailing '/' if one was supplied "~MyOutfit/" -> "~MyOutfit"
            if (path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                var newPath = path.Substring(path.Length - 1);
                if (TryGetFolderFromPath_Internal(newPath, skipPrivateFolders, out folder))
                {
                    return true;
                }
            }

            // Try without a leading and trailing '/' if they were both supplied "/~MyOutfit/" -> "~MyOutfit"
            if (path.StartsWith("/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                var newPath = path.Substring(1, path.Length - 2);
                if (TryGetFolderFromPath_Internal(newPath, skipPrivateFolders, out folder))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds all folders containing an item that either has the specified prim ID, is attached to specified attachment point, or is worn as the specified wearable type
        /// </summary>
        /// <param name="limitToOneResult">Deprecated, should always be false. Returns only the first found folder. This only exists to support the deprecated @GetPath command</param>
        /// <param name="attachedPrimId">If specified, find the folder containing this prim ID</param>
        /// <param name="attachmentPoint">If specified, find all folders containing an outFoundItems currently attached to this attachment point</param>
        /// <param name="wearableType">If specified, find all folders containing an outFoundItems currently worn as this type</param>
        /// <returns>Collection of folders matching the search criteria</returns>
        public IEnumerable<RlvSharedFolder> FindFoldersContaining(
            bool limitToOneResult,
            Guid? attachedPrimId,
            RlvAttachmentPoint? attachmentPoint,
            RlvWearableType? wearableType)
        {
            var folders = new List<RlvSharedFolder>();
            IReadOnlyList<RlvInventoryItem>? foundItems = null;

            if (attachedPrimId.HasValue)
            {
                if (!TryGetItemByPrimId(attachedPrimId.Value, out foundItems))
                {
                    return [];
                }
            }
            else if (attachmentPoint.HasValue)
            {
                if (!TryGetItemByAttachmentPoint(attachmentPoint.Value, out foundItems))
                {
                    return [];
                }
            }
            else if (wearableType.HasValue)
            {
                if (!TryGetItemByWearableType(wearableType.Value, out foundItems))
                {
                    return [];
                }
            }

            if (foundItems != null)
            {
                var foundFolders = new HashSet<Guid>();
                foreach (var item in foundItems)
                {
                    if (item.Folder == null)
                    {
                        return [];
                    }

                    if (foundFolders.Add(item.Folder.Id))
                    {
                        folders.Add(item.Folder);
                    }
                }
            }

            if (limitToOneResult && folders.Count > 0)
            {
                return [folders[0]];
            }

            return folders;
        }

        /// <summary>
        /// Attempts to create a path to the specified folder ID
        /// Example result:
        ///     ID of folder (#RLV/Clothing/Hats/Fancy) sets finalPath to "Clothing/Hats/Fancy"
        /// </summary>
        /// <param name="folderId">ID of the folder to get the path to</param>
        /// <param name="finalPath">The path to the folder if function is successful, otherwise null</param>
        /// <returns>True if the folder was found and a path was generated, otherwise false</returns>
        public bool TryBuildPathToFolder(Guid folderId, [NotNullWhen(true)] out string? finalPath)
        {
            var path = new Stack<string>();

            if (!Folders.TryGetValue(folderId, out var folder))
            {
                finalPath = null;
                return false;
            }

            var iter = folder;
            while (iter != null)
            {
                // Don't include the root (#RLV) folder itself in the path
                if (iter.Parent == null)
                {
                    break;
                }

                path.Push(iter.Name);
                iter = iter.Parent;
            }

            finalPath = string.Join("/", path);
            return true;
        }

        /// <summary>
        /// Attempts to find a known outFoundItems by ID. This will search the shared folder as well as all worn non-shared/external items
        /// </summary>
        /// <param name="itemId">ID of the outFoundItems</param>
        /// <param name="outFoundItems">Found items if successful, otherwise null</param>
        /// <returns>True on success</returns>
        public bool TryGetItem(Guid itemId, [NotNullWhen(true)] out IReadOnlyList<RlvInventoryItem>? outFoundItems)
        {
            List<RlvInventoryItem> foundItems = new List<RlvInventoryItem>();
            if (Items.TryGetValue(itemId, out var tempFoundItems))
            {
                foundItems.AddRange(tempFoundItems);
            }

            if (ExternalItems.TryGetValue(itemId, out var tempFoundExternalItem))
            {
                foundItems.Add(tempFoundExternalItem);
            }

            if (foundItems.Count > 0)
            {
                outFoundItems = foundItems.ToImmutableList();
                return true;
            }

            outFoundItems = null;
            return false;
        }


        public bool TryGetItemByPrimId(Guid primId, [NotNullWhen(true)] out IReadOnlyList<RlvInventoryItem>? outFoundItems)
        {
            List<RlvInventoryItem> foundItems = new List<RlvInventoryItem>();

            foreach (var kvp in Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.AttachedPrimId == primId)
                    {
                        foundItems.Add(item);
                    }
                }
            }

            foreach (var kvp in ExternalItems)
            {
                if (kvp.Value.AttachedPrimId == primId)
                {
                    foundItems.Add(kvp.Value);
                }
            }

            if (foundItems.Count > 0)
            {
                outFoundItems = foundItems.ToImmutableList();
                return true;
            }

            outFoundItems = null;
            return false;
        }

        public bool TryGetItemByAttachmentPoint(RlvAttachmentPoint attachmentPoint, [NotNullWhen(true)] out IReadOnlyList<RlvInventoryItem>? outFoundItems)
        {
            List<RlvInventoryItem> foundItems = new List<RlvInventoryItem>();

            foreach (var kvp in Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.AttachedTo == attachmentPoint)
                    {
                        foundItems.Add(item);
                    }
                }
            }

            foreach (var kvp in ExternalItems)
            {
                if (kvp.Value.AttachedTo == attachmentPoint)
                {
                    foundItems.Add(kvp.Value);
                }
            }

            if (foundItems.Count > 0)
            {
                outFoundItems = foundItems.ToImmutableList();
                return true;
            }

            outFoundItems = null;
            return false;
        }

        public bool TryGetItemByWearableType(RlvWearableType wearableType, [NotNullWhen(true)] out IReadOnlyList<RlvInventoryItem>? outFoundItems)
        {
            List<RlvInventoryItem> foundItems = new List<RlvInventoryItem>();

            foreach (var kvp in Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.WornOn == wearableType)
                    {
                        foundItems.Add(item);
                    }
                }
            }

            foreach (var kvp in ExternalItems)
            {
                if (kvp.Value.WornOn == wearableType)
                {
                    foundItems.Add(kvp.Value);
                }
            }

            if (foundItems.Count > 0)
            {
                outFoundItems = foundItems.ToImmutableList();
                return true;
            }

            outFoundItems = null;
            return false;
        }

        public List<RlvInventoryItem> GetCurrentOutfit()
        {
            List<RlvInventoryItem> foundItems = new List<RlvInventoryItem>();

            foreach (var kvp in Items)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.WornOn != null || item.AttachedTo != null || item.GestureState == RlvGestureState.Active)
                    {
                        foundItems.Add(item);
                    }
                }
            }

            foreach (var kvp in ExternalItems)
            {
                if (kvp.Value.WornOn != null || kvp.Value.AttachedTo != null || kvp.Value.GestureState == RlvGestureState.Active)
                {
                    foundItems.Add(kvp.Value);
                }
            }

            return foundItems;
        }

        private static void CreateInventoryMap(
            RlvSharedFolder root,
            Dictionary<Guid, RlvSharedFolder> folders,
            Dictionary<Guid, List<RlvInventoryItem>> items)
        {
            if (folders.ContainsKey(root.Id))
            {
                return;
            }

            folders[root.Id] = root;
            foreach (var item in root.Items)
            {
                if (!items.TryGetValue(item.Id, out var itemsSharingItemId))
                {
                    itemsSharingItemId = new List<RlvInventoryItem>();
                    items.Add(item.Id, itemsSharingItemId);
                }

                itemsSharingItemId.Add(item);
            }

            foreach (var child in root.Children)
            {
                CreateInventoryMap(child, folders, items);
            }
        }
    }
}
