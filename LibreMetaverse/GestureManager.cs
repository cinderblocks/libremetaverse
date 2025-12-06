/*
 * Radegast Metaverse Client
 * Copyright(c) 2009-2014, Radegast Development Team
 * Copyright(c) 2016-2025, Sjofn, LLC
 * All rights reserved.
 *  
 * Radegast is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.If not, see<https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.Assets;

namespace LibreMetaverse
{
    /// <summary>
    /// Lightweight gesture trigger helper. Monitors inventory for gesture assets and
    /// can preprocess chat lines to detect and play active gestures.
    /// </summary>
    public class GestureManager : IDisposable
    {
        private class GestureTrigger
        {
            public string TriggerLower { get; set; }
            public string Replacement { get; set; }
            public UUID AssetID { get; set; }
        }

        private readonly GridClient Client;
        private readonly ConcurrentDictionary<UUID, GestureTrigger> _gestures = new ConcurrentDictionary<UUID, GestureTrigger>();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<UUID, byte>> _triggersByWord = new ConcurrentDictionary<string, ConcurrentDictionary<UUID, byte>>(StringComparer.Ordinal);
        private readonly Random _rand = new Random();
        // Timeout for loading gesture assets from the asset service
        private readonly TimeSpan AssetLoadTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Called when a gesture is triggered. Provides the asset id and trigger word.</summary>
        public event Action<UUID, string> GestureTriggered;

        /// <summary>Create a GestureManager for the given client.</summary>
        public GestureManager(GridClient client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>Begin monitoring inventory changes for gesture items.</summary>
        public void BeginMonitoring()
        {
            Client.Inventory.Store.InventoryObjectAdded += Store_InventoryObjectAdded;
            Client.Inventory.Store.InventoryObjectUpdated += Store_InventoryObjectUpdated;

            try
            {
                foreach (var pair in Client.Self.ActiveGestures)
                {
                    if (pair.Value != UUID.Zero)
                    {
                        if (Client.Inventory.Store.TryGetValue(pair.Key, out var invBase) && invBase is InventoryItem it && it.InventoryType == InventoryType.Gesture)
                        {
                            _ = UpdateInventoryGestureAsync(it);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>Stop monitoring inventory changes.</summary>
        public void StopMonitoring()
        {
            try
            {
                Client.Inventory.Store.InventoryObjectAdded -= Store_InventoryObjectAdded;
                Client.Inventory.Store.InventoryObjectUpdated -= Store_InventoryObjectUpdated;
            }
            catch { }
        }

        /// <summary>Process a chat message. If a gesture trigger is detected and active the gesture will be played
        /// and the returned tuple indicates the processed message and whether a gesture was triggered.</summary>
        /// <param name="message">Input chat message.</param>
        /// <returns>Processed message and asset UUID of played gesture if any (UUID.Zero otherwise).</returns>
        public (string processed, UUID played) PreProcessChatMessage(string message)
        {
            TryPreProcessChatMessage(message, out var processed, out var played);
            return (processed, played);
        }

        /// <summary>
        /// Try to preprocess a chat message returning processed string and the UUID of any played gesture.
        /// Returns true if a gesture was triggered.
        /// </summary>
        public bool TryPreProcessChatMessage(string message, out string processed, out UUID played)
        {
            processed = message;
            played = UUID.Zero;

            if (string.IsNullOrWhiteSpace(message)) return false;

            var outString = new StringBuilder(message.Length);
            var words = message.Split(new[] { ' ' }, StringSplitOptions.None);
            var gestureWasTriggered = false;

            foreach (var word in words)
            {
                if (gestureWasTriggered)
                {
                    outString.Append(word);
                    outString.Append(' ');
                }
                else
                {
                    if (ProcessWord(word, outString, out var assetPlayed))
                    {
                        gestureWasTriggered = true;
                        played = assetPlayed;
                    }
                }
            }

            if (outString.Length > 0 && outString[outString.Length - 1] == ' ')
                outString.Remove(outString.Length - 1, 1);

            processed = outString.ToString();
            return gestureWasTriggered;
        }

        /// <summary>Try to process a single word. Returns true if gesture played.</summary>
        private bool ProcessWord(string word, StringBuilder outString, out UUID played)
        {
            played = UUID.Zero;
            if (string.IsNullOrEmpty(word))
            {
                outString.Append(word);
                outString.Append(' ');
                return false;
            }

            var lw = word.ToLowerInvariant();

            if (!_triggersByWord.TryGetValue(lw, out var idDict) || idDict.IsEmpty)
            {
                outString.Append(word);
                outString.Append(' ');
                return false;
            }

            // Collect only those gesture IDs that are currently active
            var possible = new List<GestureTrigger>();
            foreach (var kv in idDict)
            {
                var id = kv.Key;
                if (!Client.Self.ActiveGestures.ContainsKey(id)) continue;
                if (!_gestures.TryGetValue(id, out var g)) continue;
                possible.Add(g);
            }

            if (possible.Count == 0)
            {
                outString.Append(word);
                outString.Append(' ');
                return false;
            }

            GestureTrigger toPlay = possible.Count == 1 ? possible[0] : possible[_rand.Next(possible.Count)];

            try
            {
                // Call PlayGesture directly; avoid Task.Run to respect client threading model
                Client.Self.PlayGesture(toPlay.AssetID);
            }
            catch { }

            played = toPlay.AssetID;
            GestureTriggered?.Invoke(toPlay.AssetID, toPlay.TriggerLower);

            if (!string.IsNullOrEmpty(toPlay.Replacement))
            {
                outString.Append(toPlay.Replacement);
                outString.Append(' ');
            }

            return true;
        }

        private void Store_InventoryObjectUpdated(object sender, InventoryObjectUpdatedEventArgs e)
        {
            if (e.NewObject is InventoryItem item && item.InventoryType == InventoryType.Gesture)
            {
                _ = UpdateInventoryGestureAsync(item);
            }
        }

        private void Store_InventoryObjectAdded(object sender, InventoryObjectAddedEventArgs e)
        {
            if (e.Obj is InventoryItem item && item.InventoryType == InventoryType.Gesture)
            {
                _ = UpdateInventoryGestureAsync(item);
            }
        }

        private async Task UpdateInventoryGestureAsync(InventoryItem gestureItem)
        {
            try
            {
                UUID assetID = gestureItem.AssetUUID;

                var tcs = new TaskCompletionSource<AssetGesture>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    Client.Assets.RequestAsset(assetID, AssetType.Gesture, false, (transfer, asset) =>
                    {
                        try
                        {
                            if (asset is AssetGesture ag && ag.Decode())
                                tcs.TrySetResult(ag);
                            else
                                tcs.TrySetResult(null);
                        }
                        catch (Exception ex)
                        {
                            // Ensure we don't throw from the callback
                            tcs.TrySetException(ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error($"RequestAsset threw for gesture inventory {gestureItem.UUID} asset {assetID}: {ex.Message}", Client);
                    return;
                }

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(AssetLoadTimeout)).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    // timeout
                    Logger.Warn($"Timeout while loading gesture asset {assetID} for inventory item {gestureItem.UUID} after {AssetLoadTimeout.TotalSeconds}s", Client);
                    return;
                }

                AssetGesture assetGesture = null;
                try
                {
                    assetGesture = await tcs.Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception while loading gesture asset {assetID} for inventory item {gestureItem.UUID}: {ex.Message}", Client);
                    return;
                }

                if (assetGesture == null)
                {
                    Logger.Warn($"Failed to decode gesture asset {assetID} for inventory item {gestureItem.UUID}", Client);
                    return;
                }

                var newTrigger = (assetGesture.Trigger ?? string.Empty).ToLowerInvariant();
                var existing = _gestures.GetOrAdd(gestureItem.UUID, _ => new GestureTrigger());
                var oldTrigger = existing.TriggerLower;

                existing.TriggerLower = newTrigger;
                existing.Replacement = assetGesture.ReplaceWith;
                existing.AssetID = assetGesture.AssetID != UUID.Zero ? assetGesture.AssetID : assetID;

                // If trigger changed, update reverse index
                if (!string.Equals(oldTrigger, newTrigger, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(oldTrigger))
                    {
                        if (_triggersByWord.TryGetValue(oldTrigger, out var oldDict))
                        {
                            oldDict.TryRemove(gestureItem.UUID, out _);
                            if (oldDict.IsEmpty)
                            {
                                _triggersByWord.TryRemove(oldTrigger, out _);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(newTrigger))
                    {
                        var dict = _triggersByWord.GetOrAdd(newTrigger, _ => new ConcurrentDictionary<UUID, byte>());
                        dict[gestureItem.UUID] = 0;
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            StopMonitoring();
            _gestures.Clear();
            _triggersByWord.Clear();
        }
    }
}
