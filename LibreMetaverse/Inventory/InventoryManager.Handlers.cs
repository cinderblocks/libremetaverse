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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        private async Task<OSD> PostCapAsync(Uri uri, OSD payload, CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var tcs = new TaskCompletionSource<OSD>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Kick off the HTTP POST and wire up the callback to the TaskCompletionSource
            await Client.HttpCapsClient.PostRequestAsync(uri, OSDFormat.Xml, payload, cancellationToken,
                (response, responseData, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(error);
                        return;
                    }

                    try
                    {
                        var osd = OSDParser.Deserialize(responseData);
                        tcs.TrySetResult(osd);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<OSD> PostCapAsync(string capName, OSD payload, CancellationToken cancellationToken = default)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) throw new InvalidOperationException($"Capability {capName} is not available");
            return await PostCapAsync(uri, payload, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OSD> PostBytesAsync(Uri uri, string contentType, byte[] data,
            CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var tcs = new TaskCompletionSource<OSD>(TaskCreationOptions.RunContinuationsAsynchronously);

            await Client.HttpCapsClient.PostRequestAsync(uri, contentType, data, cancellationToken,
                (response, responseData, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(error);
                        return;
                    }

                    try
                    {
                        var osd = OSDParser.Deserialize(responseData);
                        tcs.TrySetResult(osd);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<OSD> PostBytesAsync(string capName, string contentType, byte[] data,
            CancellationToken cancellationToken = default)
        {
            var uri = GetCapabilityURI(capName);
            if (uri == null) throw new InvalidOperationException($"Capability {capName} is not available");
            return await PostBytesAsync(uri, contentType, data, cancellationToken).ConfigureAwait(false);
        }

        private void Self_IM(object sender, InstantMessageEventArgs e)
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
                        Logger.Log("Malformed inventory offer from agent", Helpers.LogLevel.Warning, Client);
                        return;
                    case InstantMessageDialog.TaskInventoryOffered when e.IM.BinaryBucket.Length == 1:
                        type = (AssetType)e.IM.BinaryBucket[0];
                        fromTask = true;
                        break;
                    case InstantMessageDialog.TaskInventoryOffered:
                        Logger.Log("Malformed inventory offer from object", Helpers.LogLevel.Warning, Client);
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
                    Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex);
                }
            }
        }

        private void CreateItemFromAssetResponse(ItemCreatedFromAssetCallback callback, byte[] itemData, OSDMap request,
            OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            if (result == null)
            {
                try
                {
                    callback(false, error?.Message ?? "Unknown error", UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }

                return;
            }

            if (result.Type == OSDType.Unknown)
            {
                try
                {
                    callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }

            var contents = (OSDMap)result;

            var status = contents["state"].AsString().ToLower();

            if (status == "upload")
            {
                var uploadURL = contents["uploader"].AsString();

                Logger.DebugLog($"CreateItemFromAsset: uploading to {uploadURL}");

                // This makes the assumption that all uploads go to CurrentSim, to avoid
                // the problem of HttpRequestState not knowing anything about simulators
                var uploadUri = new Uri(uploadURL);

                // Fire-and-forget async upload using centralized helper
                Task.Run((Func<Task>)(async () =>
                {
                    try
                    {
                        var res = await PostBytesAsync(uploadUri, "application/octet-stream", itemData,
                            cancellationToken).ConfigureAwait(false);
                        CreateItemFromAssetResponse(callback, itemData, request, res, null, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        CreateItemFromAssetResponse(callback, itemData, request, null, ex, cancellationToken);
                    }
                }), cancellationToken);
            }
            else if (status == "complete")
            {
                Logger.DebugLog("CreateItemFromAsset: completed");

                if (contents.ContainsKey("new_inventory_item") && contents.ContainsKey("new_asset"))
                {
                    // Request full update on the item in order to update the local store
                    RequestFetchInventory(contents["new_inventory_item"].AsUUID(), Client.Self.AgentID);

                    try
                    {
                        callback(true, string.Empty, contents["new_inventory_item"].AsUUID(),
                            contents["new_asset"].AsUUID());
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
                else
                {
                    try
                    {
                        callback(false, "Failed to parse asset and item UUIDs", UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
            }
            else
            {
                // Failure
                try
                {
                    callback(false, status, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }

        private void UploadInventoryAssetResponse(KeyValuePair<InventoryUploadedAssetCallback, byte[]> kvp,
            UUID itemId, OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            var callback = kvp.Key;
            var itemData = (byte[])kvp.Value;

            if (error == null && result is OSDMap contents)
            {
                var status = contents["state"].AsString();

                if (status == "upload")
                {
                    var uploadURL = contents["uploader"].AsUri();

                    if (uploadURL != null)
                    {
                        Task.Run((Func<Task>)(async () =>
                        {
                            try
                            {
                                var res = await PostBytesAsync(uploadURL, "application/octet-stream", itemData,
                                    cancellationToken).ConfigureAwait(false);
                                UploadInventoryAssetResponse(kvp, itemId, res, null, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                UploadInventoryAssetResponse(kvp, itemId, null, ex, cancellationToken);
                            }
                        }), cancellationToken);
                    }
                    else
                    {
                        try
                        {
                            callback(false, "Missing uploader URL", UUID.Zero, UUID.Zero);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                        }
                    }
                }
                else if (status == "complete" && callback != null)
                {
                    if (contents.ContainsKey("new_asset"))
                    {
                        // Request full item update so we keep store in sync
                        RequestFetchInventory(itemId, Client.Self.AgentID);

                        try
                        {
                            callback(true, string.Empty, itemId, contents["new_asset"].AsUUID());
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                        }
                    }
                    else
                    {
                        try
                        {
                            callback(false, "Failed to parse asset UUID",
                                UUID.Zero, UUID.Zero);
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                        }
                    }
                }
                else if (callback != null)
                {
                    try
                    {
                        callback(false, status, UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
            }
            else
            {
                var message = "Unrecognized or empty response";

                if (error != null)
                {
                    if (error is WebException webEx && webEx.Response is HttpWebResponse http)
                        message = http.StatusDescription ?? webEx.Message;
                    else
                        message = error.Message;
                }

                try
                {
                    callback(false, message, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }

        private void UpdateScriptAgentInventoryResponse(KeyValuePair<ScriptUpdatedCallback, byte[]> kvpCb,
            UUID itemId, OSD result, Exception error, CancellationToken cancellationToken = default)
        {
            var callback = kvpCb.Key;
            var itemData = kvpCb.Value;

            if (result == null)
            {
                try
                {
                    callback(false, error?.Message ?? "Unknown error", false,
                        null, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }

                return;
            }

            var contents = (OSDMap)result;

            var status = contents["state"].AsString();
            if (status == "upload")
            {
                var uploadURL = contents["uploader"].AsString();

                // This makes the assumption that all uploads go to CurrentSim, to avoid
                // the problem of HttpRequestState not knowing anything about simulators
                var uploadUri = new Uri(uploadURL);

                Task.Run((Func<Task>)(async () =>
                {
                    try
                    {
                        var res = await PostBytesAsync(uploadUri, "application/octet-stream", itemData,
                            cancellationToken).ConfigureAwait(false);
                        UpdateScriptAgentInventoryResponse(kvpCb, itemId, res, null, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        UpdateScriptAgentInventoryResponse(kvpCb, itemId, null, ex, cancellationToken);
                    }
                }), cancellationToken);
            }
            else if (status == "complete" && callback != null)
            {
                if (contents.ContainsKey("new_asset"))
                {
                    // Request full item update so we keep store in sync
                    RequestFetchInventory(itemId, Client.Self.AgentID);

                    try
                    {
                        List<string> compileErrors = null;

                        if (contents.TryGetValue("errors", out var content))
                        {
                            var errors = (OSDArray)content;
                            compileErrors = new List<string>(errors.Count);
                            compileErrors.AddRange(errors.Select(t => t.AsString()));
                        }

                        callback(true,
                            status,
                            contents["compiled"].AsBoolean(),
                            compileErrors,
                            itemId,
                            contents["new_asset"].AsUUID());
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
                else
                {
                    try
                    {
                        callback(false, "Failed to parse asset UUID",
                            false, null, UUID.Zero, UUID.Zero);
                    }
                    catch (Exception e)
                    {
                        Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                    }
                }
            }
            else if (callback != null)
            {
                try
                {
                    callback(false, status, false,
                        null, UUID.Zero, UUID.Zero);
                }
                catch (Exception e)
                {
                    Logger.Log(e.Message, Helpers.LogLevel.Error, Client, e);
                }
            }
        }

        private void Network_OnLoginResponse(bool loginSuccess, bool redirect, string message, string reason,
            LoginResponseData replyData)
        {
            if (!loginSuccess)
            {
                return;
            }

            if (replyData.InventorySkeleton == null || replyData.LibrarySkeleton == null)
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
