using System;
using System.Threading.Tasks;
using LibreMetaverse.StructuredData;

namespace LibreMetaverse
{
    public partial class InventoryManager
    {
        // Helper to reduce duplicated AIS Task continuation and local store merge logic
        private void ContinueWithWhenAllLog(Task<bool[]> task, string context, Action<bool[]>? onSuccess = null)
        {
            if (task == null) return;
            task.ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    if (t.IsFaulted)
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
                if (_Store == null) return;

                // Capture the updated item inside the write lock so we can fire the event
                // outside it without a second lock-free store read.
                InventoryItem? toNotify = null;
                using (var writeLock = _storeLock.WriteLock())
                {
                    if (_Store.TryGetValue(itemUUID, out var storeItem) && storeItem is InventoryItem existing)
                    {
                        try
                        {
                            existing.Update(update);
                            _Store.UpdateNodeFor(existing);
                            toNotify = existing;
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"Failed to apply AIS update to local item {itemUUID}: {ex.Message}", ex, Client);
                        }
                    }
                }

                if (toNotify != null)
                    OnItemReceived(new ItemReceivedEventArgs(toNotify));
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
