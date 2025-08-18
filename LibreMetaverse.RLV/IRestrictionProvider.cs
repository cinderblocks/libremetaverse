using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LibreMetaverse.RLV
{
    internal interface IRestrictionProvider
    {
        IReadOnlyList<RlvRestriction> GetRestrictionsByType(RlvRestrictionType restrictionType);
        IReadOnlyList<RlvRestriction> FindRestrictions(string behaviorNameFilter = "", Guid? senderFilter = null);
        bool TryGetLockedFolder(Guid folderId, [NotNullWhen(true)] out LockedFolderPublic? lockedFolder);
        IReadOnlyDictionary<Guid, LockedFolderPublic> GetLockedFolders();
    }
}
