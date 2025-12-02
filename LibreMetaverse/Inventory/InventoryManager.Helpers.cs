using System;
using System.Threading.Tasks;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse
{
    public partial class InventoryManager
    {
        // Helpers to reduce duplicated AIS Task continuation and local store merge logic
        private void ContinueWithLog(Task<bool> task, string context, Action onSuccess = null)
        {
            if (task == null) return;
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Logger.Warn($"AIS {context} exception: {t.Exception?.GetBaseException().Message}", Client);
                    return;
                }
                if (!t.Result)
                {
                    Logger.Warn($"AIS {context} failed", Client);
                    return;
                }
                try { onSuccess?.Invoke(); } catch (Exception ex) { Logger.Debug($"AIS {context} onSuccess handler threw: {ex.Message}", ex, Client); }
            }, TaskScheduler.Default);
        }

        private void ContinueWithWhenAllLog(Task<bool[]> task, string context, Action<bool[]> onSuccess = null)
        {
            if (task == null) return;
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Logger.Warn($"AIS {context} exception: {t.Exception?.GetBaseException().Message}", Client);
                    return;
                }
                var results = t.Result;
                if (results == null)
                {
                    Logger.Warn($"AIS {context} returned null results", Client);
                    return;
                }
                try { onSuccess?.Invoke(results); } catch (Exception ex) { Logger.Debug($"AIS {context} onSuccess handler threw: {ex.Message}", ex, Client); }
            }, TaskScheduler.Default);
        }

        private void MergeUpdateIntoStore(OSDMap update, UUID itemUUID)
        {
            try
            {
                if (_Store != null)
                {
                    using (var writeLock = _storeLock.WriteLock())
                    {
                        if (_Store.TryGetValue(itemUUID, out var storeItem) && storeItem is InventoryItem existing)
                        {
                            try
                            {
                                existing.Update(update);
                                _Store.UpdateNodeFor(existing);
                            }
                            catch (Exception ex)
                            {
                                Logger.Debug($"Failed to apply AIS update to local item {itemUUID}: {ex.Message}", ex, Client);
                            }
                        }
                    }

                    if (_Store != null && _Store.TryGetValue(itemUUID, out var updated) && updated is InventoryItem updatedItem)
                    {
                        OnItemReceived(new ItemReceivedEventArgs(updatedItem));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"MergeUpdateIntoStore threw: {ex.Message}", ex, Client);
            }
        }

        private ItemCreatedCallback WrapItemCreatedCallback(ItemCreatedCallback userCallback)
        {
            return (success, createdItem) =>
            {
                try
                {
                    if (success && createdItem != null)
                    {
                        if (_Store != null)
                        {
                            using (var writeLock = _storeLock.WriteLock())
                            {
                                _Store[createdItem.UUID] = createdItem;
                            }
                        }
                        else
                        {
                            Logger.Debug("Inventory store is not initialized, created item will not be cached locally", Client);
                        }

                        try
                        {
                            OnItemReceived(new ItemReceivedEventArgs(createdItem));
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"OnItemReceived handler threw: {ex.Message}", ex, Client);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Failed merging created inventory item into local store: {ex.Message}", ex, Client);
                }

                try { userCallback?.Invoke(success, createdItem); } catch (Exception ex) { Logger.Debug($"ItemCreated callback threw: {ex.Message}", ex, Client); }
            };
        }
    }
}
