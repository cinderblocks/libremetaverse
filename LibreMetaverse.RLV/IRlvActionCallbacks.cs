using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    public interface IRlvActionCallbacks
    {
        /// <summary>
        /// Sends a message on the given channel
        /// </summary>
        /// <param name="channel">Channel to send on</param>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendReplyAsync(int channel, string message, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an instant message to a user
        /// </summary>
        /// <param name="targetUser">User to message</param>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendInstantMessageAsync(Guid targetUser, string message, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the avatar's rotation to the specified angle
        /// </summary>
        /// <param name="angleInRadians">Angle in radians to set the avatar's rotation to</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetRotAsync(float angleInRadians, CancellationToken cancellationToken);

        /// <summary>
        /// Adjusts the avatar's height
        /// </summary>
        /// <param name="distance">Distance from pelvis to foot in meters</param>
        /// <param name="factor">Factor to multiply the distance by</param>
        /// <param name="delta">Delta in meters to add to the final height</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task AdjustHeightAsync(float distance, float factor, float delta, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the camera's field of view
        /// </summary>
        /// <param name="fovInRadians">Field of view in radians</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetCamFOVAsync(float fovInRadians, CancellationToken cancellationToken);

        /// <summary>
        /// Teleports the avatar to the specified location
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="z">Z coordinate</param>
        /// <param name="regionName">Optional region name, null for current region</param>
        /// <param name="lookat">Optional lookat direction in radians</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task TpToAsync(float x, float y, float z, string? regionName, float? lookat, CancellationToken cancellationToken);

        /// <summary>
        /// Makes the avatar sit on the specified object
        /// </summary>
        /// <param name="target">Object ID to sit on</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SitAsync(Guid target, CancellationToken cancellationToken);

        /// <summary>
        /// Makes the avatar stand up from sitting
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UnsitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Makes the avatar sit on the ground
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SitGroundAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Removes outfit items (wearables)
        /// </summary>
        /// <param name="itemIds">List of item IDs to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RemOutfitAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);

        /// <summary>
        /// Attaches items to the avatar
        /// </summary>
        /// <param name="itemsToAttach">List of attachment requests</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task AttachAsync(IReadOnlyList<AttachmentRequest> itemsToAttach, CancellationToken cancellationToken);

        /// <summary>
        /// Detaches items from the avatar
        /// </summary>
        /// <param name="itemIds">List of item IDs to detach</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DetachAsync(IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the active group for the avatar
        /// </summary>
        /// <param name="groupId">Group ID to set active</param>
        /// <param name="roleName">Optional role name within the group to activate, null for default role</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetGroupAsync(Guid groupId, string? roleName, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the active group for the avatar
        /// </summary>
        /// <param name="groupName">Group name to set active</param>
        /// <param name="roleName">Optional role name within the group to activate, null for default role</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetGroupAsync(string groupName, string? roleName, CancellationToken cancellationToken);

        /// <summary>
        /// Sets an environment setting
        /// </summary>
        /// <param name="settingName">Name of the environment setting</param>
        /// <param name="settingValue">Value to set</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetEnvAsync(string settingName, string settingValue, CancellationToken cancellationToken);

        /// <summary>
        /// Sets a debug setting
        /// </summary>
        /// <param name="settingName">Name of the debug setting</param>
        /// <param name="settingValue">Value to set</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetDebugAsync(string settingName, string settingValue, CancellationToken cancellationToken);
    }
}
