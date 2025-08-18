using System;
using System.Collections.Generic;

namespace LibreMetaverse.RLV
{
    internal sealed class LockedFolder
    {
        internal LockedFolder(RlvSharedFolder folder)
        {
            Folder = folder ?? throw new ArgumentException("Folder cannot be null", nameof(folder));
        }

        public RlvSharedFolder Folder { get; }

        public ICollection<RlvRestriction> DetachRestrictions { get; } = new List<RlvRestriction>();
        public ICollection<RlvRestriction> AttachRestrictions { get; } = new List<RlvRestriction>();
        public ICollection<RlvRestriction> DetachExceptions { get; } = new List<RlvRestriction>();
        public ICollection<RlvRestriction> AttachExceptions { get; } = new List<RlvRestriction>();

        public bool CanDetach => DetachExceptions.Count != 0 || DetachRestrictions.Count == 0;
        public bool CanAttach => AttachExceptions.Count != 0 || AttachRestrictions.Count == 0;
        public bool IsLocked => DetachRestrictions.Count != 0 || AttachRestrictions.Count != 0;
    }
}
