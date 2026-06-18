/*
 * Copyright (c) 2019-2026, Sjofn LLC
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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse
{
    /// <summary>
    /// Client for interacting with Inventory API v3 (AIS) endpoints.
    /// Provides helpers to call inventory and library capabilities and parse
    /// returned `_embedded` content into typed inventory objects.
    /// </summary>
    /// <remarks>
    /// This implementation is a lightweight HTTP client and parser and is intentionally
    /// different from the viewer-side C++ `AISAPI` (e.g. `llaisapi.cpp`). Notable
    /// behavior differences:
    /// - The C++ viewer integrates updates directly into the local inventory model
    ///   (version accounting, descendant counts, observers, notifications). This C#
    ///   client only returns parsed results (or invokes callbacks); it does not
    ///   mutate any global inventory model.
    /// - The C++ code contains recovery and policy logic for specific HTTP errors
    ///   (e.g. special handling for 403 content-limit cases, 410->refetch/delete
    ///   behavior, notifications). This client performs logging and returns success
    ///   flags but leaves recovery and remediation to the caller.
    /// - Concurrency and scheduling: the C++ viewer uses its coroutine manager and
    ///   a postponed queue to throttle requests; this client uses async/await and
    ///   relies on the caller for any throttling or queuing policy.
    /// - The C++ callbacks often return LLSD and identifiers for viewer-side
    ///   processing; this client exposes typed `InventoryFolder`/`InventoryItem`
    ///   lists and Task-based overloads for convenience.
    /// Use callers should apply returned data into their inventory model and
    /// implement any viewer-specific recovery or notification behavior if needed.
    /// </remarks>
    /// <summary>
    /// Side-effect metadata returned by AISv3 mutation operations.
    /// Contains IDs of objects the server removed as a side-effect of the primary operation,
    /// and the updated version numbers of affected categories.
    /// </summary>
    public readonly struct AISResponseMeta
    {
        /// <summary>Links removed by the server because their target no longer exists.</summary>
        public IReadOnlyList<UUID> BrokenLinksRemoved { get; }
        /// <summary>Items removed as a side-effect (e.g. collateral removal on purge).</summary>
        public IReadOnlyList<UUID> ItemsRemoved { get; }
        /// <summary>Categories removed as a side-effect.</summary>
        public IReadOnlyList<UUID> CategoriesRemoved { get; }
        /// <summary>New version numbers for affected categories, keyed by category UUID.</summary>
        public IReadOnlyDictionary<UUID, int> CategoryVersionUpdates { get; }

        public bool HasAnyData =>
            BrokenLinksRemoved.Count > 0 || ItemsRemoved.Count > 0 ||
            CategoriesRemoved.Count > 0 || CategoryVersionUpdates.Count > 0;

        public AISResponseMeta(
            IReadOnlyList<UUID> brokenLinks,
            IReadOnlyList<UUID> items,
            IReadOnlyList<UUID> categories,
            IReadOnlyDictionary<UUID, int> versions)
        {
            BrokenLinksRemoved = brokenLinks;
            ItemsRemoved = items;
            CategoriesRemoved = categories;
            CategoryVersionUpdates = versions;
        }

        public static readonly AISResponseMeta Empty = new AISResponseMeta(
            Array.Empty<UUID>(), Array.Empty<UUID>(), Array.Empty<UUID>(),
            new Dictionary<UUID, int>());
    }

    public partial class InventoryAISClient
    {
        public const string INVENTORY_CAP_NAME = "InventoryAPIv3";
        public const string LIBRARY_CAP_NAME = "LibraryAPIv3";

        private const int MAX_FOLDER_DEPTH_REQUEST = 50;

        [NonSerialized]
        private readonly GridClient Client;

        /// <summary>
        /// Fired after any successful AIS mutation whose response contains side-effect metadata
        /// (<c>_broken_links_removed</c>, <c>_removed_items</c>, <c>_categories_removed</c>,
        /// <c>_updated_category_versions</c>). Subscribers (e.g. InventoryManager) should apply
        /// the metadata to their inventory store.
        /// </summary>
        internal event Action<AISResponseMeta>? AISMetaReceived;

        /// <summary>
        /// Create a new AIS client bound to the provided GridClient.
        /// </summary>
        /// <param name="client">Grid client used to access HTTP capability client and current sim caps.</param>
        public InventoryAISClient(GridClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Indicates whether the InventoryAPIv3 capability is available on the current simulator.
        /// </summary>
        public bool IsAvailable => (Client.Network?.CurrentSim?.Caps?.CapabilityURI(INVENTORY_CAP_NAME) != null);

        /// <summary>
        /// Create a new inventory item or link under the specified parent category.
        /// The callback receives a success flag and the first created InventoryItem (if any).
        /// </summary>
        /// <param name="parentUuid">Parent category UUID.</param>
        /// <param name="newInventory">OSD payload describing the item or link to create.</param>
        /// <param name="createLink">If true, payload represents a link; otherwise an actual item.</param>
        /// <param name="callback">Callback invoked with (success, createdItem).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CreateInventory(UUID parentUuid, OSD newInventory, bool createLink,
            InventoryManager.ItemCreatedCallback callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, null); return; }

            var success = false;
            InventoryItem? item = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{parentUuid}?tid={UUID.Random()}",
                        UriKind.Absolute, out var uri))
                {
                    success = false;

                    callback?.Invoke(false, null);
                    return;
                }

                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(newInventory),
                           Encoding.UTF8, HttpCapsClient.LLSD_XML))
                {
                    using (var reply = await Client.HttpCapsClient.PostAsync(uri, content, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Create inventory in {parentUuid}");

                        if (!success)
                        {
                            return;
                        }
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) is OSDMap map)
#endif
                        {
                            FireAISMeta(map);
                            if (map["_embedded"] is OSDMap embedded)
                            {
                                var items = !createLink ?
                                    parseItemsFromResponse((OSDMap)embedded["items"]) :
                                    parseLinksFromResponse((OSDMap)embedded["links"]);
                                item = items.FirstOrDefault();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback(success, item);
            }
        }

        /// <summary>
        /// Creates multiple inventory links in a single AISv3 request and returns all created items.
        /// Use <see cref="InventoryManager.CreateLinksAsync"/> instead of calling this directly.
        /// </summary>
        internal async Task<IList<InventoryItem>> CreateInventoryLinksAsync(UUID parentUuid, OSD newInventory, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return Array.Empty<InventoryItem>(); }

            if (!Uri.TryCreate($"{cap}/category/{parentUuid}?tid={UUID.Random()}", UriKind.Absolute, out var uri))
                return Array.Empty<InventoryItem>();

            // Network errors and HTTP failures propagate — callers (CreateLinksAsync) own the catch.
            using var content = new StringContent(OSDParser.SerializeLLSDXmlString(newInventory), Encoding.UTF8, HttpCapsClient.LLSD_XML);
            using var reply = await Client.HttpCapsClient.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

            if (!HandleResponseStatus(reply, $"Create inventory links in {parentUuid}"))
                throw new HttpRequestException($"AISv3 POST category/{parentUuid} failed ({(int)reply.StatusCode})");

            try
            {
#if NET5_0_OR_GREATER
                var osd = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                var osd = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                return ParseLinksFromEmbedded(osd as OSDMap);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
                return Array.Empty<InventoryItem>();
            }
        }

        /// <summary>
        /// Replace (slam) the links in the specified folder with the provided payload.
        /// </summary>
        /// <param name="folderUuid">Folder UUID to replace links in.</param>
        /// <param name="newInventory">OSD payload containing links.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SlamFolder(UUID folderUuid, OSD newInventory, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{folderUuid}/links?tid={UUID.Random()}",
                        UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false);
                    return;
                }

                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(newInventory),
                           Encoding.UTF8, HttpCapsClient.LLSD_XML))
                {
                    using (var reply = await Client.HttpCapsClient.PutAsync(uri, content, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Slam folder {folderUuid}");

                        // no further parsing expected
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Remove a category from the inventory.
        /// </summary>
        /// <param name="categoryUuid">Category UUID to remove.</param>
        /// <param name="callback">Callback invoked with (success, categoryUuid).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RemoveCategory(UUID categoryUuid, Action<bool, UUID> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, categoryUuid); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, categoryUuid);
                    return;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    // 410 GONE: folder already deleted on server — desired state achieved
                    if ((int)reply.StatusCode == 410)
                    {
                        Logger.Info($"Remove folder {categoryUuid}: already gone on server (410)");
                        success = true;
                    }
                    else
                    {
                        success = HandleResponseStatus(reply, $"Remove folder {categoryUuid}");
                    }
                    if (success)
                    {
#if NET5_0_OR_GREATER
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) as OSDMap);
#else
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) as OSDMap);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, categoryUuid);
            }
        }

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        /// <param name="itemUuid">Item UUID to remove.</param>
        /// <param name="callback">Callback invoked with (success, itemUuid).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task RemoveItem(UUID itemUuid, Action<bool, UUID> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, itemUuid); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri)) { callback?.Invoke(false, itemUuid); return; }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    // 410 GONE: item already deleted on server — desired state achieved
                    if ((int)reply.StatusCode == 410)
                    {
                        Logger.Info($"Remove item {itemUuid}: already gone on server (410)");
                        success = true;
                    }
                    else
                    {
                        success = HandleResponseStatus(reply, $"Remove item {itemUuid}");
                    }
                    if (success)
                    {
#if NET5_0_OR_GREATER
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) as OSDMap);
#else
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) as OSDMap);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, itemUuid);
            }
        }

        /// <summary>
        /// Copy a category from the library to the user's inventory.
        /// </summary>
        /// <param name="sourceUuid">Source library category UUID.</param>
        /// <param name="destUuid">Destination category UUID in inventory.</param>
        /// <param name="copySubfolders">If true, include subfolders.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CopyLibraryCategory(UUID sourceUuid, UUID destUuid, bool copySubfolders, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getLibraryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{sourceUuid}?tid={UUID.Random()}");
                if (!copySubfolders) { url.Append(",depth=0"); }

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Copy library folder {sourceUuid} to {destUuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Purge all descendants of the given category.
        /// </summary>
        /// <param name="categoryUuid">Category to purge descendants from.</param>
        /// <param name="callback">Callback invoked with (success, categoryUuid).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task PurgeDescendents(UUID categoryUuid, Action<bool, UUID> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, categoryUuid); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}/children", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, categoryUuid);
                    return;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Purge descendents of {categoryUuid}");
                    if (success)
                    {
#if NET5_0_OR_GREATER
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) as OSDMap);
#else
                        FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) as OSDMap);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, categoryUuid);
            }
        }

        /// <summary>
        /// Update a category using a partial LLSD update (PATCH).
        /// </summary>
        /// <param name="categoryUuid">Target category UUID.</param>
        /// <param name="updates">OSD map of updates to apply.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task UpdateCategory(UUID categoryUuid, OSD updates, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{categoryUuid}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false);
                    return;
                }
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates),
                               Encoding.UTF8, HttpCapsClient.LLSD_XML))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            success = HandleResponseStatus(reply, $"Update folder {categoryUuid}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Update an item using a partial LLSD update (PATCH).
        /// </summary>
        /// <param name="itemUuid">Item UUID to update.</param>
        /// <param name="updates">OSD map of updates to apply.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task UpdateItem(UUID itemUuid, OSD updates, Action<bool> callback,
            CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false);
                    return;
                }
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates),
                               Encoding.UTF8, HttpCapsClient.LLSD_XML))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            success = HandleResponseStatus(reply, $"Update item {itemUuid}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        // --- GET methods (parsed) ---

        /// <summary>
        /// Fetch a category and return parsed folders, items and links via callback.
        /// </summary>
        /// <param name="category">Category identifier (UUID or well-known name such as "current").</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task GetCategory(string category, Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap))
            {
                callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                return;
            }

            var success = false;
            List<InventoryFolder> folders = new List<InventoryFolder>();
            List<InventoryItem> items = new List<InventoryItem>();
            List<InventoryItem> links = new List<InventoryItem>();

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, folders, items, links);
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch category {category}");

                    if (reply.IsSuccessStatusCode)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Fetch category children and return parsed folders, items and links via callback.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="depth">Requested depth (will be clamped).</param>
        /// <param name="recursive">If true, request full recursion (clamped to server limits).</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task GetCategoryChildren(string category, int depth, bool recursive, Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>()); return; }

            var success = false;
            List<InventoryFolder>? folders = null;
            List<InventoryItem>? items = null;
            List<InventoryItem>? links = null;

            try
            {
                var requestedDepth = recursive ? MAX_FOLDER_DEPTH_REQUEST : Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST);
                var url = new StringBuilder($"{cap}/category/{category}/children?depth={requestedDepth}");

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch children for {category}");

                    // 403 at depth=0 means the folder content exceeds the server's item limit
                    if (!success && reply.StatusCode == System.Net.HttpStatusCode.Forbidden && requestedDepth == 0)
                        Logger.Warn($"GetCategoryChildren: '{category}' at depth=0 returned 403 — folder exceeds inventory size limit");

                    if (reply.IsSuccessStatusCode)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Fetch only the subfolder (category) descendants of a category — no items or links.
        /// Equivalent to C++ <c>AISAPI::FetchCategoryCategories</c>.
        /// </summary>
        /// <param name="catId">Category UUID.</param>
        /// <param name="useInventoryCap">True for user inventory, false for library.</param>
        /// <param name="recursive">If true, fetch full depth (clamped to server limit).</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="depth">Requested depth when not recursive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task FetchCategoryCategories(UUID catId, bool useInventoryCap, bool recursive,
            Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback,
            int depth = 0, CancellationToken cancellationToken = default)
        {
            var hasCap = useInventoryCap
                ? getInventoryCap(out var cap)
                : getLibraryCap(out cap);
            if (!hasCap)
            {
                callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                return;
            }

            var success = false;
            List<InventoryFolder>? folders = null;
            List<InventoryItem>? items = null;
            List<InventoryItem>? links = null;

            try
            {
                var requestedDepth = recursive ? MAX_FOLDER_DEPTH_REQUEST : Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST);
                if (!Uri.TryCreate($"{cap}/category/{catId}/categories?depth={requestedDepth}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch categories for {catId}");

                    if (success)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Fetch specific children of a category by UUID.
        /// Equivalent to C++ <c>AISAPI::FetchCategorySubset</c>.
        /// URL: <c>GET /category/{catId}/children?depth={depth}&amp;children={id1},{id2},...</c>
        /// </summary>
        /// <param name="catId">Parent category UUID.</param>
        /// <param name="specificChildren">UUIDs of the children to fetch. Must not be empty.</param>
        /// <param name="useInventoryCap">True for user inventory, false for library.</param>
        /// <param name="recursive">If true, fetch full depth (clamped to server limit).</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="depth">Requested depth when not recursive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task FetchCategorySubset(UUID catId, IEnumerable<UUID> specificChildren,
            bool useInventoryCap, bool recursive,
            Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback,
            int depth = 0, CancellationToken cancellationToken = default)
        {
            var childList = specificChildren?.ToList();
            if (childList == null || childList.Count == 0)
            {
                Logger.Warn($"FetchCategorySubset called with empty children list for {catId}");
                callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                return;
            }

            var hasCap = useInventoryCap
                ? getInventoryCap(out var cap)
                : getLibraryCap(out cap);
            if (!hasCap)
            {
                callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                return;
            }

            var success = false;
            List<InventoryFolder>? folders = null;
            List<InventoryItem>? items = null;
            List<InventoryItem>? links = null;

            try
            {
                var requestedDepth = recursive ? MAX_FOLDER_DEPTH_REQUEST : Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST);
                var url = new StringBuilder($"{cap}/category/{catId}/children?depth={requestedDepth}&children=");
                url.Append(string.Join(",", childList.Select(id => id.ToString())));

                const int MAX_URL_LENGTH = 2000; // RFC recommends max 2048
                if (url.Length > MAX_URL_LENGTH)
                {
                    Logger.Warn($"FetchCategorySubset URL exceeds {MAX_URL_LENGTH} chars ({url.Length}) for {catId}");
                }

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch subset of {catId}");

                    if (success)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Fetch category links and return parsed folders, items and links via callback.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task GetCategoryLinks(string category, Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>()); return; }

            var success = false;
            List<InventoryFolder>? folders = null;
            List<InventoryItem>? items = null;
            List<InventoryItem>? links = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/links", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch links for {category}");

                    if (reply.IsSuccessStatusCode)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Fetch an item resource and return parsed folders, items and links via callback.
        /// </summary>
        /// <param name="itemUuid">Item UUID to fetch.</param>
        /// <param name="callback">Callback invoked with (success, folders, items, links).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task FetchItem(UUID itemUuid, Action<bool, List<InventoryFolder>, List<InventoryItem>, List<InventoryItem>> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>()); return; }

            var success = false;
            List<InventoryFolder>? folders = null;
            List<InventoryItem>? items = null;
            List<InventoryItem>? links = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch item {itemUuid}");

                    if (reply.IsSuccessStatusCode)
                    {
#if NET5_0_OR_GREATER
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
#else
                        var deserialized = OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false));
#endif
                        if (deserialized is OSDMap m)
                        {
                            // GET /item/{id} returns the item at the top level, not inside _embedded.
                            // C++ parseContent checks item_id/linked_id directly on the response root.
                            if (m.ContainsKey("item_id"))
                            {
                                var parsed = InventoryItem.FromOSD(m);
                                if (parsed.IsLink())
                                    links = new List<InventoryItem> { parsed };
                                else
                                    items = new List<InventoryItem> { parsed };
                            }
                            else
                            {
                                ParseEmbedded(m, out folders, out items, out links);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
            }
        }

        /// <summary>
        /// Copy a category within inventory.
        /// </summary>
        /// <param name="sourceUuid">Source category UUID.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="simulate">If true, perform a simulation only.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CopyCategory(UUID sourceUuid, UUID destUuid, bool simulate, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                var url = new StringBuilder($"{cap}/category/{sourceUuid}?tid={UUID.Random()}");
                if (simulate)
                {
                    url.Append("&simulate=1");
                }

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Copy category {sourceUuid} to {destUuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Move a category within inventory by patching its parent_id.
        /// </summary>
        /// <param name="sourceUuid">Source category UUID.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task MoveCategory(UUID sourceUuid, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{sourceUuid}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                var updates = new OSDMap { ["parent_id"] = OSD.FromUUID(destUuid) };
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates),
                               Encoding.UTF8, HttpCapsClient.LLSD_XML))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            success = HandleResponseStatus(reply, $"Move category {sourceUuid} to {destUuid}");
                            if (success)
                            {
#if NET5_0_OR_GREATER
                                FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) as OSDMap);
#else
                                FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) as OSDMap);
#endif
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Replace children of a category using PUT.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="childrenPayload">OSD payload for children.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task PutCategoryChildren(string category, OSD childrenPayload, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/children?tid={UUID.Random()}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(childrenPayload), Encoding.UTF8, HttpCapsClient.LLSD_XML))
                {
                    using (var reply = await Client.HttpCapsClient.PutAsync(uri, content, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Put children for {category}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Copy children of a category to a destination category.
        /// </summary>
        /// <param name="category">Source category identifier.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CopyCategoryChildren(string category, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/children?tid={UUID.Random()}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Copy children for {category} to {destUuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// AIS v3 does not expose a batch "move children" endpoint; this operation is not supported.
        /// </summary>
        public Task MoveCategoryChildren(string category, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            Logger.Warn($"MoveCategoryChildren has no AIS v3 equivalent and is not supported ({category} -> {destUuid})");
            callback?.Invoke(false);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete children of a category.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteCategoryChildren(string category, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/children", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Delete children for {category}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Replace links in a category using PUT.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="linksPayload">OSD payload containing links.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task PutCategoryLinks(string category, OSD linksPayload, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/links?tid={UUID.Random()}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(linksPayload), Encoding.UTF8, HttpCapsClient.LLSD_XML))
                {
                    using (var reply = await Client.HttpCapsClient.PutAsync(uri, content, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Put links for {category}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Copy links from a category to a destination category.
        /// </summary>
        /// <param name="category">Source category identifier.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CopyCategoryLinks(string category, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/links?tid={UUID.Random()}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Copy links for {category} to {destUuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// AIS v3 does not expose a batch "move links" endpoint; this operation is not supported.
        /// </summary>
        public Task MoveCategoryLinks(string category, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            Logger.Warn($"MoveCategoryLinks has no AIS v3 equivalent and is not supported ({category} -> {destUuid})");
            callback?.Invoke(false);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete links in a category.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task DeleteCategoryLinks(string category, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/links", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Delete links for {category}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Copy an item to a destination category.
        /// </summary>
        /// <param name="itemUuid">Item UUID to copy.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task CopyItem(UUID itemUuid, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}?tid={UUID.Random()}", UriKind.Absolute, out var uri)) { callback?.Invoke(false); return; }

                using (var request = new HttpRequestMessage(new HttpMethod("COPY"), uri))
                {
                    request.Headers.Add("Destination", destUuid.ToString());

                    using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        success = HandleResponseStatus(reply, $"Copy item {itemUuid} to {destUuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Move an item to a destination category by patching its parent_id.
        /// </summary>
        /// <param name="itemUuid">Item UUID to move.</param>
        /// <param name="destUuid">Destination category UUID.</param>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task MoveItem(UUID itemUuid, UUID destUuid, Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}", UriKind.Absolute, out var uri))
                {
                    callback?.Invoke(false); return;
                }

                var updates = new OSDMap { ["parent_id"] = OSD.FromUUID(destUuid) };
#if (NETSTANDARD2_1_OR_GREATER || NET)
                using (var request = new HttpRequestMessage(HttpMethod.Patch, uri))
#else
                using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri))
#endif
                {
                    using (var content = new StringContent(OSDParser.SerializeLLSDXmlString(updates),
                               Encoding.UTF8, HttpCapsClient.LLSD_XML))
                    {
                        request.Content = content;

                        using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            success = HandleResponseStatus(reply, $"Move item {itemUuid} to {destUuid}");
                            if (success)
                            {
#if NET5_0_OR_GREATER
                                FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) as OSDMap);
#else
                                FireAISMeta(OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) as OSDMap);
#endif
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }


        // Misc endpoints

        /// <summary>
        /// Fetch the Current Outfit Folder (COF).
        /// </summary>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task FetchCOF(Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/current/links", UriKind.Absolute, out var uri))
                {
                    success = false;
                    callback?.Invoke(false);
                    return;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, "Fetch COF");

                    if (success)
                    {
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) is OSDMap map)
#endif
                        {
                            ParseEmbedded(map, out _, out _, out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Fetch orphaned inventory items.
        /// </summary>
        /// <param name="callback">Callback invoked with success flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task FetchOrphans(Action<bool> callback, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { callback?.Invoke(false); return; }

            var success = false;

            try
            {
                if (!Uri.TryCreate($"{cap}/orphans", UriKind.Absolute, out var uri))
                {
                    success = false;
                    callback?.Invoke(false);
                    return;
                }
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, "Fetch orphans");

                    if (success)
                    {
#if NET5_0_OR_GREATER
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) is OSDMap map)
#else
                        if (OSDParser.Deserialize(await reply.Content.ReadAsStreamAsync().ConfigureAwait(false)) is OSDMap map)
#endif
                        {
                            ParseEmbedded(map, out _, out _, out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            finally
            {
                callback?.Invoke(success);
            }
        }

        /// <summary>
        /// Empty the Trash folder.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> EmptyTrash(CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap)) { return false; }

            try
            {
                if (!Uri.TryCreate($"{cap}/category/trash/children", UriKind.Absolute, out var uri))
                {
                    return false;
                }

                using (var reply = await Client.HttpCapsClient.DeleteAsync(uri, cancellationToken).ConfigureAwait(false))
                {
                    if (!reply.IsSuccessStatusCode)
                    {
                        Logger.Warn($"Could not empty Trash folder: {reply.ReasonPhrase}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Extract concrete InventoryItem objects from an AIS `_embedded` response.
        /// </summary>
        /// <param name="response">OSDMap response that may contain `_embedded`.</param>
        /// <returns>List of parsed InventoryItem objects (might be empty).</returns>
        public List<InventoryItem> ParseItemsFromEmbedded(OSDMap response)
        {
            var ret = new List<InventoryItem>();
            if (response == null) return ret;

            if (response.TryGetValue("_embedded", out var embeddedObj) && embeddedObj is OSDMap embedded)
            {
                if (embedded.TryGetValue("items", out var itemsObj) && itemsObj is OSDMap itemsMap)
                    ret.AddRange(parseItemsFromResponse(itemsMap));

                // "item" (singular): target item embedded inline when following a link
                if (embedded.TryGetValue("item", out var singleItemObj) && singleItemObj is OSDMap singleItem
                    && singleItem.ContainsKey("item_id"))
                {
                    ret.Add(InventoryItem.FromOSD(singleItem));
                }
            }
            else
            {
                if (response.TryGetValue("items", out var itemsObj) && itemsObj is OSDMap itemsMap)
                    ret.AddRange(parseItemsFromResponse(itemsMap));

                if (response.TryGetValue("item", out var singleItemObj) && singleItemObj is OSDMap singleItem
                    && singleItem.ContainsKey("item_id"))
                {
                    ret.Add(InventoryItem.FromOSD(singleItem));
                }
            }

            return ret;
        }

        /// <summary>
        /// Extract InventoryFolder objects from an AIS `_embedded` response.
        /// </summary>
        /// <param name="response">OSDMap response that may contain `_embedded`.</param>
        /// <returns>List of parsed InventoryFolder objects (might be empty).</returns>
        public List<InventoryFolder> ParseFoldersFromEmbedded(OSDMap response)
        {
            var ret = new List<InventoryFolder>();
            if (response == null) return ret;

            if (response.TryGetValue("_embedded", out var embeddedObj) && embeddedObj is OSDMap embedded)
            {
                // AISv3 uses "categories" (OSDMap keyed by UUID) for folder collections,
                // and "category" (OSDMap) for a single embedded folder (e.g., target of a link).
                if (embedded.TryGetValue("categories", out var catsObj) && catsObj is OSDMap catsMap)
                {
                    ret = parseFoldersFromResponse(catsMap);
                }
                if (embedded.TryGetValue("category", out var catObj) && catObj is OSDMap singleCat
                    && singleCat.ContainsKey("category_id"))
                {
                    ret.Add(InventoryFolder.FromOSD(singleCat));
                }
            }
            else
            {
                if (response.TryGetValue("categories", out var catsObj) && catsObj is OSDMap catsMap)
                {
                    ret = parseFoldersFromResponse(catsMap);
                }
                if (response.TryGetValue("category", out var catObj) && catObj is OSDMap singleCat
                    && singleCat.ContainsKey("category_id"))
                {
                    ret.Add(InventoryFolder.FromOSD(singleCat));
                }
            }

            return ret;
        }

        /// <summary>
        /// Extract link InventoryItem objects from an AIS `_embedded` response.
        /// </summary>
        /// <param name="response">OSDMap response that may contain `_embedded`.</param>
        /// <returns>List of parsed InventoryItem link objects (might empty).</returns>
        public List<InventoryItem> ParseLinksFromEmbedded(OSDMap response)
        {
            var ret = new List<InventoryItem>();
            if (response == null) return ret;

            if (response.TryGetValue("_embedded", out var embeddedObj) && embeddedObj is OSDMap embedded)
            {
                if (embedded.TryGetValue("links", out var linksObj) && linksObj is OSDMap linksMap)
                {
                    ret = parseLinksFromResponse(linksMap);
                }
            }
            else
            {
                if (response.TryGetValue("links", out var linksObj) && linksObj is OSDMap linksMap)
                {
                    ret = parseLinksFromResponse(linksMap);
                }
            }

            return ret;
        }

        /// <summary>
        /// Convenience that parses folders, items and links from an AIS response into out parameters.
        /// </summary>
        /// <param name="response">OSDMap AIS response.</param>
        /// <param name="folders">Out list of folders.</param>
        /// <param name="items">Out list of items.</param>
        /// <param name="links">Out list of links.</param>
        public void ParseEmbedded(OSDMap response, out List<InventoryFolder> folders, out List<InventoryItem> items, out List<InventoryItem> links)
        {
            folders = ParseFoldersFromEmbedded(response);
            items = ParseItemsFromEmbedded(response);
            links = ParseLinksFromEmbedded(response);
        }

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

        private List<InventoryFolder> parseFoldersFromResponse(OSDMap categoriesOsd)
        {
            var ret = new List<InventoryFolder>();
            foreach (KeyValuePair<string, OSD> kv in categoriesOsd)
            {
                if (kv.Value is OSDMap folder)
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
                /*
                 * Objects that have been attached in-world prior to being stored on the
                 * asset server are stored with the InventoryType of 0 (Texture)
                 * instead of 17 (Attachment)
                 *
                 * This corrects that behavior by forcing Object Asset types that have an
                 * invalid InventoryType with the proper InventoryType of Attachment.
                 */
                InventoryType type = (InventoryType)link["inv_type"].AsInteger();
                var assetType = (AssetType)link["type"].AsInteger();
                if (type == InventoryType.Texture && (assetType == AssetType.Object || assetType == AssetType.Mesh))
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

        private bool getInventoryCap(out Uri? inventoryCapUri)
        {
            inventoryCapUri = Client.Network?.CurrentSim?.Caps?.CapabilityURI(INVENTORY_CAP_NAME);
            if (inventoryCapUri != null) { return true; }
            Logger.Warn("AISv3 Inventory Capability not found!", Client);
            return false;
        }

        private bool getLibraryCap(out Uri? libraryCapUri)
        {
            libraryCapUri = Client.Network?.CurrentSim?.Caps?.CapabilityURI(LIBRARY_CAP_NAME);
            if (libraryCapUri != null) { return true; }
            Logger.Warn("AISv3 Library Capability not found!", Client);
            return false;
        }

        /// <summary>
        /// Parses the AISv3 side-effect meta fields from any AIS response map.
        /// Returns <see cref="AISResponseMeta.Empty"/> when the response contains no meta fields.
        /// </summary>
        public static AISResponseMeta ParseAISResponseMeta(OSDMap? response)
        {
            if (response == null) return AISResponseMeta.Empty;

            var broken = ParseUuidList(response, "_broken_links_removed");
            var removed = ParseUuidList(response, "_removed_items");
            // _category_items_removed is additive with _removed_items
            removed.AddRange(ParseUuidList(response, "_category_items_removed"));
            var cats = ParseUuidList(response, "_categories_removed");

            var versions = new Dictionary<UUID, int>();
            if (response.TryGetValue("_updated_category_versions", out var vOsd) && vOsd is OSDMap vMap)
            {
                foreach (KeyValuePair<string, OSD> kv in vMap)
                {
                    if (UUID.TryParse(kv.Key, out var id))
                        versions[id] = kv.Value.AsInteger();
                }
            }

            if (broken.Count == 0 && removed.Count == 0 && cats.Count == 0 && versions.Count == 0)
                return AISResponseMeta.Empty;

            return new AISResponseMeta(broken, removed, cats, versions);
        }

        private static List<UUID> ParseUuidList(OSDMap map, string key)
        {
            var result = new List<UUID>();
            if (!map.TryGetValue(key, out var osd)) return result;
            if (osd is OSDArray arr)
            {
                foreach (var item in arr)
                {
                    var id = item.AsUUID();
                    if (id != UUID.Zero) result.Add(id);
                }
            }
            else if (osd is OSDMap dict)
            {
                foreach (KeyValuePair<string, OSD> kv in dict)
                {
                    if (UUID.TryParse(kv.Key, out var id))
                        result.Add(id);
                }
            }
            return result;
        }

        private void FireAISMeta(OSDMap? response)
        {
            if (response == null || AISMetaReceived == null) return;
            var meta = ParseAISResponseMeta(response);
            if (meta.HasAnyData)
                AISMetaReceived.Invoke(meta);
        }

        private bool HandleResponseStatus(HttpResponseMessage reply, string context)
        {
            if (reply == null) return false;

            if (reply.IsSuccessStatusCode) return true;

            switch (reply.StatusCode)
            {
                case HttpStatusCode.NotModified: // 304
                    Logger.Info($"{context}: Not Modified (304)");
                    return true; // caller can decide how to handle empty body
                case HttpStatusCode.BadRequest: // 400
                    Logger.Warn($"{context}: Bad Request (400): {reply.ReasonPhrase}");
                    break;
                case HttpStatusCode.UnsupportedMediaType: // 415
                    Logger.Warn($"{context}: Unsupported Media Type (415): {reply.ReasonPhrase}");
                    break;
                case HttpStatusCode.Forbidden: // 403
                    Logger.Warn($"{context}: Forbidden (403): {reply.ReasonPhrase}");
                    break;
                case HttpStatusCode.NotFound: // 404
                    Logger.Warn($"{context}: Not Found (404): {reply.ReasonPhrase}");
                    break;
                case HttpStatusCode.Conflict: // 409
                    Logger.Warn($"{context}: Conflict (409): {reply.ReasonPhrase}");
                    break;
                case (HttpStatusCode)410: // Gone
                    Logger.Warn($"{context}: Gone (410): {reply.ReasonPhrase}");
                    break;
                case (HttpStatusCode)412: // Precondition Failed
                    Logger.Warn($"{context}: Precondition Failed (412): {reply.ReasonPhrase}");
                    break;
                default:
                    Logger.Warn($"{context}: HTTP {(int)reply.StatusCode} ({reply.StatusCode}): {reply.ReasonPhrase}");
                    break;
            }

            return false;
        }
    }
}

