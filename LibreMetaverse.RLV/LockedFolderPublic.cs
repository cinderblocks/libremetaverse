using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LibreMetaverse.RLV
{
    public sealed class LockedFolderPublic
    {
        /// <summary>
        /// Folder ID
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Folder name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// All Detach restrictions for this folder
        /// </summary>
        public IReadOnlyList<RlvRestriction> DetachRestrictions { get; }

        /// <summary>
        /// All Attach restrictions for this folder
        /// </summary>
        public IReadOnlyList<RlvRestriction> AttachRestrictions { get; }

        /// <summary>
        /// All Detach exceptions for this folder
        /// </summary>
        public IReadOnlyList<RlvRestriction> DetachExceptions { get; }

        /// <summary>
        /// All Attach exceptions for this folder
        /// </summary>
        public IReadOnlyList<RlvRestriction> AttachExceptions { get; }


        /// <summary>
        /// Determines if items in this folder can be detached/unworn
        /// </summary>
        public bool CanDetach => DetachExceptions.Count != 0 || DetachRestrictions.Count == 0;

        /// <summary>
        /// Determines if items from this folder can be attached/worn
        /// </summary>
        public bool CanAttach => AttachExceptions.Count != 0 || AttachRestrictions.Count == 0;

        /// <summary>
        /// Determines if this folder is locked and cannot be modified
        /// </summary>
        public bool IsLocked => DetachRestrictions.Count != 0 || AttachRestrictions.Count != 0;

        internal LockedFolderPublic(LockedFolder folder)
        {
            if (folder == null)
            {
                throw new ArgumentException("Folder must not be null", nameof(folder));
            }

            Id = folder.Folder.Id;
            Name = folder.Folder.Name;

            DetachRestrictions = folder.DetachRestrictions.ToImmutableList();
            AttachRestrictions = folder.AttachRestrictions.ToImmutableList();
            DetachExceptions = folder.DetachExceptions.ToImmutableList();
            AttachExceptions = folder.AttachExceptions.ToImmutableList();
        }

        public override string ToString()
        {
            return $"LockedFolder: {Name} ({Id}) - Locked: {IsLocked}, CanAttach: {CanAttach}, CanDetach: {CanDetach}";
        }
    }
}
