using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMetaverse
{
    public partial class InventoryAISClient
    {
        // Async Task-returning overloads that return parsed results as tuples
        /// <summary>
        /// Async overload that fetches a category and returns parsed results as a tuple.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple containing success flag and lists of folders, items and links.</returns>
        public async Task<(bool success, List<InventoryFolder> folders, List<InventoryItem> items, List<InventoryItem> links)> GetCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap))
            {
                return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
            }

            var success = false;
            List<InventoryFolder> folders = new List<InventoryFolder>();
            List<InventoryItem> items = new List<InventoryItem>();
            List<InventoryItem> links = new List<InventoryItem>();

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}?tid={UUID.Random()}", UriKind.Absolute, out var uri))
                {
                    return (false, folders, items, links);
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

            return (success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
        }

        /// <summary>
        /// Async overload that fetches category children and returns parsed results as a tuple.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="depth">Requested depth.</param>
        /// <param name="recursive">If true, request recursion.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple containing success flag and lists of folders, items and links.</returns>
        public async Task<(bool success, List<InventoryFolder> folders, List<InventoryItem> items, List<InventoryItem> links)> GetCategoryChildrenAsync(string category, int depth, bool recursive, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap))
            {
                return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
            }

            var success = false;
            List<InventoryFolder> folders = null;
            List<InventoryItem> items = null;
            List<InventoryItem> links = null;

            try
            {
                var requestedDepth = recursive ? MAX_FOLDER_DEPTH_REQUEST : Math.Min(depth, MAX_FOLDER_DEPTH_REQUEST);
                var url = new StringBuilder($"{cap}/category/{category}/children?tid={UUID.Random()}");
                url.Append($"&depth={requestedDepth}");

                if (!Uri.TryCreate(url.ToString(), UriKind.Absolute, out var uri))
                {
                    return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
                }

                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                using (var reply = await Client.HttpCapsClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    success = HandleResponseStatus(reply, $"Fetch children for {category}");

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

            return (success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
        }

        /// <summary>
        /// Async overload that fetches category links and returns parsed results as a tuple.
        /// </summary>
        /// <param name="category">Category identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple containing success flag and lists of folders, items and links.</returns>
        public async Task<(bool success, List<InventoryFolder> folders, List<InventoryItem> items, List<InventoryItem> links)> GetCategoryLinksAsync(string category, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap))
            {
                return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
            }

            var success = false;
            List<InventoryFolder> folders = null;
            List<InventoryItem> items = null;
            List<InventoryItem> links = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/category/{category}/links?tid={UUID.Random()}", UriKind.Absolute, out var uri))
                {
                    return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
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

            return (success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
        }

        /// <summary>
        /// Async overload that fetches an item and returns parsed results as a tuple.
        /// </summary>
        /// <param name="itemUuid">Item UUID to fetch.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple containing success flag and lists of folders, items and links.</returns>
        public async Task<(bool success, List<InventoryFolder> folders, List<InventoryItem> items, List<InventoryItem> links)> FetchItemAsync(UUID itemUuid, CancellationToken cancellationToken = default)
        {
            if (!getInventoryCap(out var cap))
            {
                return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
            }

            var success = false;
            List<InventoryFolder> folders = null;
            List<InventoryItem> items = null;
            List<InventoryItem> links = null;

            try
            {
                if (!Uri.TryCreate($"{cap}/item/{itemUuid}?tid={UUID.Random()}", UriKind.Absolute, out var uri))
                {
                    return (false, new List<InventoryFolder>(), new List<InventoryItem>(), new List<InventoryItem>());
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
                            ParseEmbedded(m, out folders, out items, out links);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }

            return (success, folders ?? new List<InventoryFolder>(), items ?? new List<InventoryItem>(), links ?? new List<InventoryItem>());
        }

        public async Task<bool> MoveCategoryAsync(UUID sourceUuid, UUID destUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await MoveCategory(sourceUuid, destUuid, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> MoveItemAsync(UUID itemUuid, UUID destUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await MoveItem(itemUuid, destUuid, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> CopyItemAsync(UUID itemUuid, UUID destUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await CopyItem(itemUuid, destUuid, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> CopyCategoryAsync(UUID sourceUuid, UUID destUuid, bool simulate = false, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await CopyCategory(sourceUuid, destUuid, simulate, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> RemoveItemAsync(UUID itemUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await RemoveItem(itemUuid, (success, id) => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> RemoveCategoryAsync(UUID categoryUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await RemoveCategory(categoryUuid, (success, id) => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> PurgeDescendentsAsync(UUID categoryUuid, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await PurgeDescendents(categoryUuid, (success, id) => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        public async Task<bool> CopyLibraryCategoryAsync(UUID sourceUuid, UUID destUuid, bool copySubfolders, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await CopyLibraryCategory(sourceUuid, destUuid, copySubfolders, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Task-based wrapper that creates inventory and returns the created item.
        /// </summary>
        public async Task<(bool success, InventoryItem created)> CreateInventoryAsync(UUID parentUuid, OSD newInventory, bool createLink, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<(bool, InventoryItem)>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await CreateInventory(parentUuid, newInventory, createLink, (success, item) => tcs.TrySetResult((success, item)), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Task-based wrapper for UpdateCategory.
        /// </summary>
        public async Task<bool> UpdateCategoryAsync(UUID categoryUuid, OSD updates, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await UpdateCategory(categoryUuid, updates, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Task-based wrapper for UpdateItem.
        /// </summary>
        public async Task<bool> UpdateItemAsync(UUID itemUuid, OSD updates, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    await UpdateItem(itemUuid, updates, success => tcs.TrySetResult(success), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
