using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse.RLV
{
    public class RlvService
    {
        public const string RLVVersion = "RestrainedLove viewer v3.4.3 (RLVa 2.4.2)";
        public const string RLVVersionNum = "2040213";

        private volatile bool _enabled;
        private volatile bool _enableInstantMessageProcessing;

        internal IRlvQueryCallbacks Callbacks { get; }
        internal IRlvActionCallbacks ActionCallbacks { get; }
        internal RlvGetRequestHandler GetRequestHandler { get; }

        private readonly Regex _rlvRegexPattern = new(@"(?<behavior>[^:=]+)(:(?<option>[^=]*))?=(?<param>.+)", RegexOptions.Compiled);

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public bool EnableInstantMessageProcessing
        {
            get => _enableInstantMessageProcessing;
            set => _enableInstantMessageProcessing = value;
        }

        public RlvCommandProcessor Commands { get; }
        public RlvRestrictionManager Restrictions { get; }
        public RlvPermissionsService Permissions { get; }
        public RlvBlacklist Blacklist { get; }

        public RlvService(IRlvQueryCallbacks callbacks, IRlvActionCallbacks actionCallbacks, bool enabled)
        {
            Callbacks = callbacks;
            ActionCallbacks = actionCallbacks;

            Blacklist = new RlvBlacklist();
            Restrictions = new RlvRestrictionManager(Callbacks, actionCallbacks);
            GetRequestHandler = new RlvGetRequestHandler(Blacklist, Restrictions, Callbacks, actionCallbacks);
            Permissions = new RlvPermissionsService(Restrictions);
            Commands = new RlvCommandProcessor(Permissions, Callbacks, actionCallbacks);
            Enabled = enabled;
        }

        #region public
        /// <summary>
        /// Process an RLV command
        /// </summary>
        /// <param name="message">Message containing the command or commands</param>
        /// <param name="senderId">ID of the object sending the command</param>
        /// <param name="senderName">Name of the object sending the command</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if all of the command were processed successfully</returns>
        public async Task<bool> ProcessMessage(string message, Guid senderId, string senderName, CancellationToken cancellationToken = default)
        {
            if (!Enabled || !message.StartsWith("@", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var result = true;
            foreach (var singleMessage in message.Substring(1).Split(','))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isSuccessful = await ProcessSingleMessage(singleMessage, senderId, senderName, cancellationToken).ConfigureAwait(false);
                if (!isSuccessful)
                {
                    result = false;
                }
            }

            return result;
        }

        /// <summary>
        /// Process an instant message containing an RLV command
        /// </summary>
        /// <param name="message">Instant message command</param>
        /// <param name="senderId">ID of the user sending the instant message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the command was successfully processed</returns>
        public async Task<bool> ProcessInstantMessage(string message, Guid senderId, CancellationToken cancellationToken = default)
        {
            if (!EnableInstantMessageProcessing || !Enabled || !message.StartsWith("@", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (Blacklist.IsBlacklisted(message))
            {
                return false;
            }

            return await GetRequestHandler.ProcessInstantMessageCommand(message.ToLowerInvariant(), senderId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report the sending of a public message by the current user
        /// </summary>
        /// <param name="message">Message being sent to public chat (channel 0)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportSendPublicMessage(string message, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<int> channels;

            if (message.StartsWith("/me ", StringComparison.OrdinalIgnoreCase))
            {
                if (!Permissions.TryGetRedirEmoteChannels(out channels))
                {
                    return;
                }
            }
            else
            {
                if (!Permissions.TryGetRedirChatChannels(out channels))
                {
                    return;
                }
            }

            foreach (var channel in channels)
            {
                await ActionCallbacks.SendReplyAsync(channel, message, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Report that the user has just accepted an inventory offer
        /// </summary>
        /// <param name="folderPath">Path to the accepted folder</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportInventoryOfferAccepted(string folderPath, CancellationToken cancellationToken = default)
        {
            var isSharedFolder = false;

            if (folderPath.StartsWith("#RLV/", StringComparison.Ordinal))
            {
                folderPath = folderPath.Substring("#RLV/".Length);
                isSharedFolder = true;
            }

            var notificationText = "";
            if (isSharedFolder)
            {
                notificationText = $"/accepted_in_rlv inv_offer {folderPath}";
            }
            else
            {
                notificationText = $"/accepted_in_inv inv_offer {folderPath}";
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user has just declined an inventory offer
        /// </summary>
        /// <param name="folderPath">Path to the declined folder</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportInventoryOfferDeclined(string folderPath, CancellationToken cancellationToken = default)
        {
            if (folderPath.StartsWith("#RLV/", StringComparison.Ordinal))
            {
                folderPath = folderPath.Substring("#RLV/".Length);
            }

            var notificationText = $"/declined inv_offer {folderPath}";
            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user has worn an item
        /// </summary>
        /// <param name="objectFolderId">Folder id containing the item being worn</param>
        /// <param name="isShared">True if this folder is a shared folder</param>
        /// <param name="wearableType">Type of wearable being worn</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportItemWorn(Guid objectFolderId, bool isShared, RlvWearableType wearableType, CancellationToken cancellationToken = default)
        {
            var notificationText = "";
            var isLegal = Permissions.CanAttach(objectFolderId, isShared, null, wearableType);

            if (isLegal)
            {
                notificationText = $"/worn legally {wearableType.ToString().ToLowerInvariant()}";
            }
            else
            {
                notificationText = $"/worn illegally {wearableType.ToString().ToLowerInvariant()}";
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user has removed a worn item
        /// </summary>
        /// <param name="itemId">ID of the item being removed</param>
        /// <param name="objectFolderId">Folder id containing the item being removed</param>
        /// <param name="isShared">True if this folder is a shared folder</param>
        /// <param name="wearableType">Type of wearable being removed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportItemUnworn(Guid itemId, Guid objectFolderId, bool isShared, RlvWearableType wearableType, CancellationToken cancellationToken = default)
        {
            var notificationText = "";
            var isLegal = Permissions.CanDetach(itemId, null, objectFolderId, isShared, null, wearableType);

            if (isLegal)
            {
                notificationText = $"/unworn legally {wearableType.ToString().ToLowerInvariant()}";
            }
            else
            {
                notificationText = $"/unworn illegally {wearableType.ToString().ToLowerInvariant()}";
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user has attached an item
        /// </summary>
        /// <param name="objectFolderId">ID of the folder containing the item being attached</param>
        /// <param name="isShared">True if the folder is a shared folder</param>
        /// <param name="attachmentPoint">Attachment point where the item was attached</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportItemAttached(Guid objectFolderId, bool isShared, RlvAttachmentPoint attachmentPoint, CancellationToken cancellationToken = default)
        {
            var notificationText = "";
            var isLegal = Permissions.CanAttach(objectFolderId, isShared, attachmentPoint, null);

            if (!RlvCommon.RlvAttachmentPointToNameMap.TryGetValue(attachmentPoint, out var attachmentPointName))
            {
                attachmentPointName = "Unknown";
            }

            if (isLegal)
            {
                notificationText = $"/attached legally {attachmentPointName}";
            }
            else
            {
                notificationText = $"/attached illegally {attachmentPointName}";
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user has detached an item
        /// </summary>
        /// <param name="itemId">ID of the item being detached</param>
        /// <param name="objectFolderId">ID of the folder containing the item being detached</param>
        /// <param name="isShared">True if the folder is a shared folder</param>
        /// <param name="attachmentPoint">Attachment point where the item was detached from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportItemDetached(Guid itemId, Guid primId, Guid objectFolderId, bool isShared, RlvAttachmentPoint attachmentPoint, CancellationToken cancellationToken = default)
        {
            var notificationText = "";
            var isLegal = Permissions.CanDetach(itemId, primId, objectFolderId, isShared, attachmentPoint, null);

            if (!RlvCommon.RlvAttachmentPointToNameMap.TryGetValue(attachmentPoint, out var attachmentPointName))
            {
                attachmentPointName = "Unknown";
            }

            if (isLegal)
            {
                notificationText = $"/detached legally {attachmentPointName}";
            }
            else
            {
                notificationText = $"/detached illegally {attachmentPointName}";
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the current user just sat on the ground or an object
        /// </summary>
        /// <param name="objectId">Null if user sat on the ground, otherwise ID of the object being sat on</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public async Task ReportSit(Guid? objectId, CancellationToken cancellationToken = default)
        {
            var notificationText = "";

            if (objectId != null)
            {
                var isLegal = Permissions.CanInteract() && Permissions.CanSit();

                if (isLegal)
                {
                    notificationText = $"/sat object legally {objectId}";
                }
                else
                {
                    notificationText = $"/sat object illegally {objectId}";
                }
            }
            else
            {
                var isLegal = Permissions.CanSit();

                if (isLegal)
                {
                    notificationText = $"/sat ground legally";
                }
                else
                {
                    notificationText = $"/sat ground illegally";
                }
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Report that the user stands up from sitting on the ground or an object
        /// </summary>
        /// <param name="objectId">Null if user was sitting on the ground, otherwise ID of the object user was sitting on</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task</returns>
        public async Task ReportUnsit(Guid? objectId, CancellationToken cancellationToken = default)
        {
            var notificationText = "";

            if (objectId != null)
            {
                var isLegal = Permissions.CanInteract() && Permissions.CanUnsit();

                if (isLegal)
                {
                    notificationText = $"/unsat object legally {objectId}";
                }
                else
                {
                    notificationText = $"/unsat object illegally {objectId}";
                }
            }
            else
            {
                var isLegal = Permissions.CanUnsit();

                if (isLegal)
                {
                    notificationText = $"/unsat ground legally";
                }
                else
                {
                    notificationText = $"/unsat ground illegally";
                }
            }

            await SendNotification(notificationText, cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Private
        private async Task<bool> ProcessRLVMessage(RlvMessage rlvMessage, CancellationToken cancellationToken)
        {
            if (Blacklist.IsBlacklisted(rlvMessage.Behavior))
            {
                if (int.TryParse(rlvMessage.Param, out var channel))
                {
                    await ActionCallbacks.SendReplyAsync(channel, "", cancellationToken).ConfigureAwait(false);
                }

                return false;
            }

            if (rlvMessage.Behavior == "clear")
            {
                return await Restrictions.ProcessClearCommand(rlvMessage, cancellationToken).ConfigureAwait(false);
            }
            else if (rlvMessage.Param == "force")
            {
                return await Commands.ProcessActionCommand(rlvMessage, cancellationToken).ConfigureAwait(false);
            }
            else if (rlvMessage.Param is "y" or "n" or "add" or "rem")
            {
                return await Restrictions.ProcessRestrictionCommand(rlvMessage, rlvMessage.Option, rlvMessage.Param is "n" or "add", cancellationToken).ConfigureAwait(false);
            }
            else if (int.TryParse(rlvMessage.Param, out var channel))
            {
                if (channel == 0)
                {
                    return false;
                }

                return await GetRequestHandler.ProcessGetCommand(rlvMessage, channel, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        private async Task<bool> ProcessSingleMessage(string message, Guid senderId, string senderName, CancellationToken cancellationToken)
        {
            // Special hack for @clear, which doesn't match the standard pattern of @behavior=param
            if (message.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                return await ProcessRLVMessage(new RlvMessage(
                    behavior: "clear",
                    option: "",
                    param: "",
                    sender: senderId,
                    senderName: senderName
                ), cancellationToken).ConfigureAwait(false);
            }

            var match = _rlvRegexPattern.Match(message);
            if (!match.Success)
            {
                return false;
            }

            var rlvMessage = new RlvMessage(
                behavior: match.Groups["behavior"].Value.ToLowerInvariant(),
                option: match.Groups["option"].Value,
                param: match.Groups["param"].Value.ToLowerInvariant(),
                sender: senderId,
                senderName: senderName
            );

            return await ProcessRLVMessage(rlvMessage, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendNotification(string notificationText, CancellationToken cancellationToken)
        {
            var notificationRestrictions = Restrictions.GetRestrictionsByType(RlvRestrictionType.Notify);

            foreach (var notificationRestriction in notificationRestrictions)
            {
                if (notificationRestriction.Args[0] is not int channel)
                {
                    continue;
                }

                if (!(notificationRestriction.Args.Count > 1 && notificationRestriction.Args[1] is string filter))
                {
                    filter = "";
                }

                if (notificationText.Contains(filter))
                {
                    await ActionCallbacks.SendReplyAsync(channel, notificationText, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        #endregion
    }
}
