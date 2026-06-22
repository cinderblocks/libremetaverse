/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2022-2025, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Packets;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        private async Task<OSD> PostCapAsync(Uri uri, OSD payload, CancellationToken cancellationToken = default, IProgress<LibreMetaverse.HttpCapsClient.ProgressReport>? progress = null)
        {
            if (uri == null) { throw new ArgumentNullException(nameof(uri)); }

            var result = await Client.HttpCapsClient.PostAsync(uri, OSDFormat.Xml, payload, cancellationToken, progress).ConfigureAwait(false);
            var responseData = result.data ?? throw new InvalidOperationException("Empty response from capability POST");

            try { return OSDParser.Deserialize(responseData); }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to parse capability response: {ex.Message}", ex); }
        }

        private async Task<OSD> PostCapAsync(string capName, OSD payload, CancellationToken cancellationToken = default, IProgress<LibreMetaverse.HttpCapsClient.ProgressReport>? progress = null)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) { throw new InvalidOperationException($"Capability {capName} is not available"); }
            return await PostCapAsync(uri, payload, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task<OSD> PostBytesAsync(Uri uri, string contentType, byte[] data,
            CancellationToken cancellationToken = default, IProgress<LibreMetaverse.HttpCapsClient.ProgressReport>? progress = null)
        {
            if (uri == null) { throw new ArgumentNullException(nameof(uri)); }
 
            // Use the newer Task-based PostAsync overload for raw bytes
            var result = await Client.HttpCapsClient.PostAsync(uri, contentType, data, cancellationToken, progress).ConfigureAwait(false);
            var responseData = result.data ?? throw new InvalidOperationException("Empty response from capability POST");

            try { return OSDParser.Deserialize(responseData); }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to parse capability response: {ex.Message}", ex); }
        }

        private async Task<OSD> PostBytesAsync(string capName, string contentType, byte[] data,
            CancellationToken cancellationToken = default, IProgress<LibreMetaverse.HttpCapsClient.ProgressReport>? progress = null)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) { throw new InvalidOperationException($"Capability {capName} is not available"); }
            return await PostBytesAsync(uri, contentType, data, cancellationToken, progress).ConfigureAwait(false);
        }

        // Helper to POST a serialized string payload to a capability and deserialize the OSD response
        private async Task<OSD> PostStringAsync(Uri uri, string content, CancellationToken cancellationToken = default, IProgress<LibreMetaverse.HttpCapsClient.ProgressReport>? progress = null)
        {
            if (uri == null) { throw new ArgumentNullException(nameof(uri)); }
 
            // The Task-based API does not provide a string overload; convert to bytes and post with the XML LLSD content type
            var payload = System.Text.Encoding.UTF8.GetBytes(content ?? string.Empty);

            var result = await Client.HttpCapsClient.PostAsync(uri, LibreMetaverse.HttpCapsClient.LLSD_XML, payload, cancellationToken, progress).ConfigureAwait(false);
            var responseData = result.data ?? throw new InvalidOperationException("Empty response from capability POST");

            try { return OSDParser.Deserialize(responseData); }
            catch (Exception ex) { throw new InvalidOperationException($"Failed to parse capability response: {ex.Message}", ex); }
        }

        private void Self_IM(object? sender, InstantMessageEventArgs e)
        {
            // TODO: MainAvatar.InstantMessageDialog.GroupNotice can also be an inventory offer, should we
            // handle it here?

            if (m_InventoryObjectOffered != null &&
                (e.IM.Dialog == InstantMessageDialog.InventoryOffered
                 || e.IM.Dialog == InstantMessageDialog.TaskInventoryOffered))
            {
                var type = AssetType.Unknown;
                var objectID = UUID.Zero;
                var fromTask = false;

                switch (e.IM.Dialog)
                {
                    case InstantMessageDialog.InventoryOffered when e.IM.BinaryBucket.Length == 17:
                        type = (AssetType)e.IM.BinaryBucket[0];
                        objectID = new UUID(e.IM.BinaryBucket, 1);
                        fromTask = false;
                        break;
                    case InstantMessageDialog.InventoryOffered:
                        Logger.Warn("Malformed inventory offer from agent", Client);
                        return;
                    case InstantMessageDialog.TaskInventoryOffered when e.IM.BinaryBucket.Length == 1:
                        type = (AssetType)e.IM.BinaryBucket[0];
                        fromTask = true;
                        break;
                    case InstantMessageDialog.TaskInventoryOffered:
                        Logger.Warn("Malformed inventory offer from object", Client);
                        return;
                }

                // Find the folder where this is going to go
                var destinationFolderID = FindFolderForType(type);

                // Fire the callback
                try
                {
                    var imp = new ImprovedInstantMessagePacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        MessageBlock =
                        {
                            FromGroup = false,
                            ToAgentID = e.IM.FromAgentID,
                            Offline = 0,
                            ID = e.IM.IMSessionID,
                            Timestamp = 0,
                            FromAgentName = Utils.StringToBytes(Client.Self.Name),
                            Message = Utils.EmptyBytes,
                            ParentEstateID = 0,
                            RegionID = UUID.Zero,
                            Position = Client.Self.SimPosition
                        }
                    };

                    var args = new InventoryObjectOfferedEventArgs(e.IM, type, objectID, fromTask, destinationFolderID);

                    OnInventoryObjectOffered(args);

                    if (args.Accept)
                    {
                        // Accept the inventory offer
                        switch (e.IM.Dialog)
                        {
                            case InstantMessageDialog.InventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryAccepted;
                                break;
                            case InstantMessageDialog.TaskInventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryAccepted;
                                break;
                            case InstantMessageDialog.GroupNotice:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryAccepted;
                                break;
                        }

                        imp.MessageBlock.BinaryBucket = args.FolderID.GetBytes();
                        RequestFetchInventory(objectID, e.IM.ToAgentID);
                    }
                    else
                    {
                        // Decline the inventory offer
                        switch (e.IM.Dialog)
                        {
                            case InstantMessageDialog.InventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.InventoryDeclined;
                                break;
                            case InstantMessageDialog.TaskInventoryOffered:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.TaskInventoryDeclined;
                                break;
                            case InstantMessageDialog.GroupNotice:
                                imp.MessageBlock.Dialog = (byte)InstantMessageDialog.GroupNoticeInventoryDeclined;
                                break;
                        }

                        imp.MessageBlock.BinaryBucket = Utils.EmptyBytes;
                    }

                    Client.Network.SendPacket(imp, e.Simulator ?? Client.Network.CurrentSim);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex, Client);
                }
            }
        }

        internal async Task<(bool success, string status, UUID itemID, UUID assetID)> CreateItemFromAssetAsync(
            byte[] itemData, OSD? initialResult, CancellationToken cancellationToken = default,
            IProgress<HttpCapsClient.ProgressReport>? progress = null)
        {
            var result = initialResult;
            int iterations = 0;
            while (result is OSDMap contents)
            {
                if (++iterations > 3)
                    return (false, "Upload state machine exceeded maximum iterations", UUID.Zero, UUID.Zero);
                var state = contents["state"].AsString().ToLowerInvariant();
                if (state == "upload")
                {
                    Logger.DebugLog($"CreateItemFromAsset: uploading to {contents["uploader"]}");
                    var uploadUri = new Uri(contents["uploader"].AsString());
                    result = await PostBytesAsync(uploadUri, "application/octet-stream", itemData, cancellationToken, progress).ConfigureAwait(false);
                }
                else if (state == "complete")
                {
                    Logger.DebugLog("CreateItemFromAsset: completed");
                    if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
                    {
                        var itemID = contents["new_inventory_item"].AsUUID();
                        var assetID = contents["new_asset"].AsUUID();
                        RequestFetchInventory(itemID, Client.Self.AgentID);
                        return (true, string.Empty, itemID, assetID);
                    }
                    return (false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                }
                else
                {
                    return (false, state, UUID.Zero, UUID.Zero);
                }
            }
            return (false, "Unrecognized or empty response", UUID.Zero, UUID.Zero);
        }

        private async Task<(bool success, string status, UUID itemID, UUID assetID)> PerformInventoryUploadAsync(
            byte[] data, UUID itemId, OSD? initialResult, CancellationToken cancellationToken, IProgress<HttpCapsClient.ProgressReport>? progress)
        {
            var result = initialResult;
            int iterations = 0;
            while (result is OSDMap contents)
            {
                if (++iterations > 3)
                    return (false, "Upload state machine exceeded maximum iterations", UUID.Zero, UUID.Zero);
                var state = contents["state"].AsString();
                if (state == "upload")
                {
                    var uploadURL = contents["uploader"].AsUri();
                    if (uploadURL == null) return (false, "Missing uploader URL", UUID.Zero, UUID.Zero);
                    result = await PostBytesAsync(uploadURL, "application/octet-stream", data, cancellationToken, progress).ConfigureAwait(false);
                }
                else if (state == "complete")
                {
                    if (contents.ContainsKey("new_asset"))
                    {
                        RequestFetchInventory(itemId, Client.Self.AgentID);
                        return (true, string.Empty, itemId, contents["new_asset"].AsUUID());
                    }
                    return (false, "Failed to parse asset UUID", UUID.Zero, UUID.Zero);
                }
                else
                {
                    return (false, state, UUID.Zero, UUID.Zero);
                }
            }
            return (false, "Unrecognized or empty response", UUID.Zero, UUID.Zero);
        }

        private async Task<(bool uploadSuccess, string uploadStatus, bool compileSuccess, List<string>? compileMessages, UUID itemID, UUID assetID)> PerformScriptUploadAsync(
            byte[] data, UUID itemId, OSD? initialResult, CancellationToken cancellationToken, IProgress<HttpCapsClient.ProgressReport>? progress)
        {
            if (initialResult == null)
                return (false, "No response from server", false, null, UUID.Zero, UUID.Zero);

            var result = initialResult;
            int iterations = 0;
            while (result is OSDMap contents)
            {
                if (++iterations > 3)
                    return (false, "Upload state machine exceeded maximum iterations", false, null, UUID.Zero, UUID.Zero);
                var state = contents["state"].AsString();
                if (state == "upload")
                {
                    var uploadUri = new Uri(contents["uploader"].AsString());
                    result = await PostBytesAsync(uploadUri, "application/octet-stream", data, cancellationToken, progress).ConfigureAwait(false);
                }
                else if (state == "complete")
                {
                    if (contents.ContainsKey("new_asset"))
                    {
                        RequestFetchInventory(itemId, Client.Self.AgentID);
                        List<string>? compileErrors = null;
                        if (contents.TryGetValue("errors", out var errContent))
                        {
                            var errors = (OSDArray)errContent;
                            compileErrors = errors.Select(t => t.AsString()).ToList();
                        }
                        return (true, state, contents["compiled"].AsBoolean(), compileErrors, itemId, contents["new_asset"].AsUUID());
                    }
                    return (false, "Failed to parse asset UUID", false, null, UUID.Zero, UUID.Zero);
                }
                else
                {
                    return (false, state, false, null, UUID.Zero, UUID.Zero);
                }
            }
            return (false, "Unrecognized or empty response", false, null, UUID.Zero, UUID.Zero);
        }

        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
            LoginResponseData? replyData)
        {
            if (!loginSuccess)
            {
                return;
            }

            if (replyData == null || replyData.InventorySkeleton == null || replyData.LibrarySkeleton == null)
            {
                return;
            }

            // Initialize the store here to link it with the owner
            _Store = new Inventory(Client, Client.Self.AgentID);
            Logger.DebugLog($"Setting InventoryRoot to {replyData.InventoryRoot}", Client);
            var rootFolder = new InventoryFolder(replyData.InventoryRoot)
            {
                Name = string.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.RootFolder = rootFolder;

            foreach (var folder in replyData.InventorySkeleton)
                _Store.UpdateNodeFor(folder);

            var libraryRootFolder = new InventoryFolder(replyData.LibraryRoot)
            {
                Name = string.Empty,
                ParentUUID = UUID.Zero
            };
            _Store.LibraryFolder = libraryRootFolder;

            foreach (var folder in replyData.LibrarySkeleton)
                _Store.UpdateNodeFor(folder);
        }
    }
}

