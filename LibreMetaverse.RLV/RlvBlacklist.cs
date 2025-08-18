using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LibreMetaverse.RLV
{
    public class RlvBlacklist : IBlacklistProvider
    {
        private readonly HashSet<string> _blacklist = new();
        private readonly object _blacklistLock = new();

        internal RlvBlacklist()
        {

        }

        /// <summary>
        /// Gets a copy of the current blacklist
        /// </summary>
        /// <returns>Copy of the active blacklist</returns>
        public IReadOnlyCollection<string> GetBlacklist()
        {
            lock (_blacklistLock)
            {
                return _blacklist
                    .OrderBy(n => n)
                    .ToImmutableList();
            }
        }

        /// <summary>
        /// Blacklist a specific RLV behavior
        /// </summary>
        /// <param name="behavior">Behavior to blacklist</param>
        public void BlacklistBehavior(string behavior)
        {
            if (string.IsNullOrWhiteSpace(behavior))
            {
                throw new ArgumentException("Behavior cannot be null or empty.", nameof(behavior));
            }

            lock (_blacklistLock)
            {
                _blacklist.Add(behavior.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Removes a blacklisted behavior
        /// </summary>
        /// <param name="behavior">Behavior to un-blacklist</param>
        public void UnBlacklistBehavior(string behavior)
        {
            if (string.IsNullOrWhiteSpace(behavior))
            {
                throw new ArgumentException("Behavior cannot be null or empty.", nameof(behavior));
            }

            lock (_blacklistLock)
            {
                _blacklist.Remove(behavior.ToLowerInvariant());
            }
        }

        /// <summary>
        /// Checks to see if the specific RLV behavior is blacklisted
        /// </summary>
        /// <param name="behavior">Behavior to check</param>
        /// <returns>True if behavior is blacklisted</returns>
        public bool IsBlacklisted(string behavior)
        {
            if (string.IsNullOrWhiteSpace(behavior))
            {
                return false;
            }

            lock (_blacklistLock)
            {
                return _blacklist.Contains(behavior.ToLowerInvariant());
            }
        }
    }
}
