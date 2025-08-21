using System;

namespace LibreMetaverse.RLV
{
    /// <summary>
    /// Represents a request to attach an item to the avatar
    /// </summary>
    public class AttachmentRequest
    {
        public Guid ItemId { get; }
        public RlvAttachmentPoint AttachmentPoint { get; }
        public bool ReplaceExistingAttachments { get; }

        public AttachmentRequest(Guid itemId, RlvAttachmentPoint attachmentPoint, bool replaceExistingAttachments)
        {
            ItemId = itemId;
            AttachmentPoint = attachmentPoint;
            ReplaceExistingAttachments = replaceExistingAttachments;
        }

        public override bool Equals(object obj)
        {
            return obj is AttachmentRequest request &&
                   ItemId.Equals(request.ItemId) &&
                   AttachmentPoint == request.AttachmentPoint &&
                   ReplaceExistingAttachments == request.ReplaceExistingAttachments;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemId, AttachmentPoint, ReplaceExistingAttachments);
        }
    }
}
