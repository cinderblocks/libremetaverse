/*
 * Copyright (c) 2019-2025, Sjofn LLC
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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    public class InventoryAISClient
    {
        public const string INVENTORY_CAP_NAME = "InventoryAPIv3";
        public const string LIBRARY_CAP_NAME = "LibraryAPIv3";

        private const int MAX_FOLDER_DEPTH_REQUEST = 50;

        [NonSerialized]
        private readonly GridClient Client;

        public InventoryAISClient(GridClient client)
        {
            Client = client;
        }

        public bool IsAvailable => (Client.Network.CurrentSim.Caps?.CapabilityURI(INVENTORY_CAP_NAME) != null);

        public async Task CreateInventory(UUID parentUuid, OSD newInventory, bool createLink, 
            InventoryManager.ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;
            InventoryItem item = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{parentUuid}?tid={UUID.Random()}", 
                        UriKind.Absolute, out var uri))
                {
                    success = false;

                    return;
                }
                
                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(newInventory), 
                           Encoding.UTF8, "application/llsd+xml"))
                {
                    using (var reply = await Client.HttpCapsClient.PostAsync(uri, content, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not create inventory: {reply.ReasonPhrase}", Helpers.LogLevel.Warning);
                            return;
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map && map["_embedded"] is OSDMap embedded)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map && map["_embedded"] is OSDMap embedded)
#endif
                        {
                            var items = !createLink ?
                                parseItemsFromResponse((OSDMap)embedded["items"]) :
                                parseLinksFromResponse((OSDMap)embedded["links"]);

                            item = items.First();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback(success, item);
            }
        }

        public async Task SlamFolder(UUID folderUuid, OSD newInventory, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{folderUuid}/links?tid={UUID.Random()}",
                        UriKind.Absolute, out var uri))
                {
                    return;
                }

                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(newInventory), 
                           Encoding.UTF8, "application/llsd+xml"))
                {
                    using (var reply = await Client.HttpCapsClient.PutAsync(uri, content, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not slam folder: {folderUuid}: {reply.ReasonPhrase}", 
                                Helpers.LogLevel.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task RemoveCategory(UUID categoryUuid, Action<bool, UUID> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}", UriKind.Absolute, out var uri))
                {
                    return;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken))
                {
                    success = reply.IsSuccessStatusCode;

                    if (!success)
                    {
                        Logger.Log($"Could not remove folder {categoryUuid}: {reply.ReasonPhrase}",
                            Helpers.LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success, categoryUuid);
            }
        }

        public async Task RemoveItem(UUID itemUuid, Action<bool, UUID> callback, 
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri)) { return; }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken))
                {
                    success = reply.IsSuccessStatusCode;

                    if (!success)
                    {
                        Logger.Log($"Could not remove item {itemUuid}: {reply.ReasonPhrase}",
                            Helpers.LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success, itemUuid);
            }
        }

        public async Task CopyLibraryCategory(UUID sourceUuid, UUID destUuid, bool copySubfolders, Action<bool> callback, 
            CancellationToken cancellationToken = default)
        {
            if (!getLibraryCap(out var cap)) { return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{sourceUuid}?tid={UUID.Random()}");
                if (copySubfolders) { url.Append(",depth=0"); }

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri)) { return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not copy library folder {sourceUuid}: {reply.ReasonPhrase}", 
                                Helpers.LogLevel.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task PurgeDescendents(UUID categoryUuid, Action<bool, UUID> callback, 
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}/children", UriKind.Absolute, out var uri))
                {
                    return;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken))
                {
                    success = reply.IsSuccessStatusCode;

                    if (!success)
                    {
                        Logger.Log($"Could not purge descendents of {categoryUuid}: {reply.ReasonPhrase}", 
                            Helpers.LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success, categoryUuid);
            }
        }

        public async Task UpdateCategory(UUID categoryUuid, OSD updates, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}", UriKind.Absolute, out var uri))
                {
                    return;
                }
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates), 
                               Encoding.UTF8, "application/llsd+xml"))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                        {
                            success = reply.IsSuccessStatusCode;

                            if (!success)
                            {
                                Logger.Log($"Could not update folder {categoryUuid}: {reply.ReasonPhrase}",
                                    Helpers.LogLevel.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task UpdateItem(UUID itemUuid, OSD updates, Action<bool> callback, 
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri))
                {
                    return;
                }
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates),
                               Encoding.UTF8, "application/llsd+xml"))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                        {
                            success = reply.IsSuccessStatusCode;

                            if (!success)
                            {
                                Logger.Log($"Could not update item {itemUuid}: {reply.ReasonPhrase}",
                                    Helpers.LogLevel.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }
        /*
        public async Task FetchItem(UUID itemUUID, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUUID}", UriKind.Absolute, out var uri))
                {
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch {itemUUID}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback(success);
            }
        }

        public async Task FetchCategoryChildren(UUID categoryUuid, bool recursive, int depth, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{categoryUuid}/children");
                url.Append(recursive
                    ? $"?depth={MAX_FOLDER_DEPTH_REQUEST}"
                    : $"?depth={Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST)}");

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch children for {categoryUuid}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        // some folders can be requested by name, like
        // animatn | bodypart | clothing | current | favorite | gesture | inbox | landmark | lsltext
        // lstndfnd | my_otfts | notecard | object | outbox | root | snapshot | sound | texture | trash
        public async Task FetchCategoryChildren(string identifier, bool recursive, int depth, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{identifier}/children");
                url.Append(recursive
                    ? $"?depth={MAX_FOLDER_DEPTH_REQUEST}"
                    : $"?depth={Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST)}");

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch children for {identifier}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task FetchCategoryCategories(UUID categoryUuid, bool recursive, int depth, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{categoryUuid}/categories");
                url.Append(recursive
                    ? $"?depth={MAX_FOLDER_DEPTH_REQUEST}"
                    : $"?depth={Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST)}");

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch categories for {categoryUuid}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task FetchCategorySubset(UUID categoryUuid, IEnumerable<UUID> children, bool recursive, int depth,
            Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{categoryUuid}/children");
                url.Append(recursive
                    ? $"?depth={MAX_FOLDER_DEPTH_REQUEST}"
                    : $"?depth={Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST)}");
                using (var child = children.GetEnumerator())
                {
                    url.Append($"&children={child.Current}");
                    while (child.MoveNext())
                    {
                        url.Append($",{child.Current}");
                    }
                }

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch categories for {categoryUuid}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task FetchCategoryLinks(UUID categoryUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}/links", UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch links for {categoryUuid}: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task FetchCOF(Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/current/links", UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {

                    var payload = OSDParser.SerializeLLSDXmlString(new OSDMap { { "depth", 0 } });
                    using (var content = new StringContent(payload, Encoding.UTF8, "application/llsd+xml"))
                    {
                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                        {
                            success = reply.IsSuccessStatusCode;

                            if (!success)
                            {
                                Logger.Log($"Could not fetch from Current Outfit Folder: {reply.ReasonPhrase}",
                                    Helpers.LogLevel.Warning);
                                return;
                            }
#if NET5_0_OR_GREATER
                            if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                            if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                            {
                                if (map.TryGetValue("folders", out var folderArray))
                                {
                                    var folders = parseFoldersFromResponse((OSDArray)folderArray);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task FetchOrphans(Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/orphans", UriKind.Absolute, out var uri))
                {
                    success = false;
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken))
                    {
                        success = reply.IsSuccessStatusCode;

                        if (!success)
                        {
                            Logger.Log($"Could not fetch orphans: {reply.ReasonPhrase}",
                                Helpers.LogLevel.Warning);
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync()) is OSDMap map)
#endif
                        {

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        public async Task<bool> EmptyTrash(CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return false; }

            try
            {
                if (!Uri.TryCreate($"{cap}/category/trash/children", UriKind.Absolute, out var uri))
                {
                    return false;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken))
                {
                    if (!reply.IsSuccessStatusCode)
                    {
                        Logger.Log($"Could not empty Trash folder: {reply.ReasonPhrase}",
                            Helpers.LogLevel.Warning);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message, Helpers.LogLevel.Warning);
            }
            return true;
        }
        */
        private List<InventoryItem> parseItemsFromResponse(OSDMap itemsOsd)
        {
            List<InventoryItem> ret = new List<InventoryItem>();

            foreach (KeyValuePair<string, OSD> o in itemsOsd)
            {
                var item = (OSDMap)o.Value;
                ret.Add(InventoryItem.FromOSD(item));
            }
            return ret;
        }

        private List<InventoryFolder> parseFoldersFromResponse(OSDArray foldersOsd)
        {
            List<InventoryFolder> ret = new List<InventoryFolder>();

            foreach (var osd in foldersOsd)
            {
                var folder = (OSDMap)osd;
                ret.Add(InventoryFolder.FromOSD(folder));
            }
            return ret;
        }

        private List<InventoryItem> parseLinksFromResponse(OSDMap linksOsd)
        {
            List<InventoryItem> ret = new List<InventoryItem>();

            foreach (KeyValuePair<string, OSD> o in linksOsd)
            {
                var link = (OSDMap)o.Value;
                InventoryType type = (InventoryType)link["inv_type"].AsInteger();
                if (type == InventoryType.Texture && (AssetType)link["type"].AsInteger() == AssetType.Object)
                {
                    type = InventoryType.Attachment;
                }
                InventoryItem item = InventoryManager.CreateInventoryItem(type, link["item_id"]);

                item.ParentUUID = link["parent_id"];
                item.Name = link["name"];
                item.Description = link["desc"];
                item.OwnerID = link["agent_id"];
                item.ParentUUID = link["parent_id"];
                item.AssetUUID = link["linked_id"];
                item.AssetType = AssetType.Link;
                item.CreationDate = Utils.UnixTimeToDateTime(link["created_at"]);

                item.CreatorID = link["agent_id"]; // hack
                item.LastOwnerID = link["agent_id"]; // hack
                item.Permissions = Permissions.NoPermissions;
                item.GroupOwned = false;
                item.GroupID = UUID.Zero;

                item.SalePrice = 0;
                item.SaleType = SaleType.Not;

                ret.Add(item);
            }

            return ret;
        }

        private bool getInventoryCap(out Uri inventoryCapUri)
        {
            inventoryCapUri = Client.Network.CurrentSim?.Caps?.CapabilityURI(INVENTORY_CAP_NAME);
            if (inventoryCapUri != null) { return true; }
            Logger.Log("AISv3 Capability not found!", Helpers.LogLevel.Warning, Client);
            return false;
        }

        private bool getLibraryCap(out Uri libraryCapUri)
        {
            libraryCapUri = Client.Network.CurrentSim?.Caps?.CapabilityURI(LIBRARY_CAP_NAME);
            if (libraryCapUri != null) { return true; }
            Logger.Log("AISv3 Capability not found!", Helpers.LogLevel.Warning, Client);
            return false;
        }
    }
}
