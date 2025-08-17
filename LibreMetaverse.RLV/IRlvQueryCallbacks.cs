using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    public interface IRlvQueryCallbacks
    {
        /// <summary>
        /// Checks if an object exists in the world
        /// </summary>
        /// <param name="objectId">Object ID to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the object exists</returns>
        Task<bool> ObjectExistsAsync(Guid objectId, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the user is currently sitting
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if sitting</returns>
        Task<bool> IsSittingAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets environment info for a setting
        /// </summary>
        /// <param name="settingName">Setting name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and environment info if successful</returns>
        Task<(bool Success, string EnvironmentSettingValue)> TryGetEnvironmentSettingValueAsync(string settingName, CancellationToken cancellationToken);

        /// <summary>
        /// Gets debug info for a setting
        /// </summary>
        /// <param name="settingName">Setting name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and debug info if successful</returns>
        Task<(bool Success, string DebugSettingValue)> TryGetDebugSettingValueAsync(string settingName, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the ID of the object the user is sitting on
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and sit ID if successful</returns>
        Task<(bool Success, Guid SitId)> TryGetSitIdAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the RLV shared folder
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and shared folder successful</returns>
        Task<(bool Success, RlvSharedFolder? SharedFolder)> TryGetSharedFolderAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets current camera settings
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and camera settings if successful</returns>
        Task<(bool Success, CameraSettings? CameraSettings)> TryGetCameraSettingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the current user's active group name
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and active group name if successful</returns>
        Task<(bool Success, string ActiveGroupName)> TryGetActiveGroupNameAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the current user's outfit. This will be all worn and attached items and may include
        /// items outside of the shared #RLV folder
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and current outfit if successful</returns>
        Task<(bool Success, IReadOnlyList<RlvInventoryItem>? CurrentOutfit)> TryGetCurrentOutfitAsync(CancellationToken cancellationToken);
    }

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
