using System;

namespace LibreMetaverse.RLV.EventArguments
{
    public class RestrictionUpdatedEventArgs : EventArgs
    {
        public bool IsNew { get; }
        public bool IsDeleted { get; }
        public RlvRestriction Restriction { get; }

        public RestrictionUpdatedEventArgs(RlvRestriction restriction, bool isNew, bool isDeleted)
        {
            IsNew = isNew;
            IsDeleted = isDeleted;
            Restriction = restriction;
        }
    }
}
