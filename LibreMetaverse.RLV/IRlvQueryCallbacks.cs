using System;
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
        /// Gets the complete RLV inventory map including shared folder structure, current outfit, and externally worn/attached items
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success flag and inventory map if successful</returns>
        Task<(bool Success, InventoryMap? InventoryMap)> TryGetInventoryMapAsync(CancellationToken cancellationToken);
    }
}
