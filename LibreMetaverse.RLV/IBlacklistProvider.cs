using System.Collections.Generic;

namespace LibreMetaverse.RLV
{
    internal interface IBlacklistProvider
    {
        IReadOnlyCollection<string> GetBlacklist();
    }
}
