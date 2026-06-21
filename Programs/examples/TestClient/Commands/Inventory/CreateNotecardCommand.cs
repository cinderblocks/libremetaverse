using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;
using LibreMetaverse.Assets;

namespace TestClient.Commands.Inventory
{
    public class CreateNotecardCommand : Command
    {
        const int NOTECARD_CREATE_TIMEOUT = 1000 * 10;
        const int NOTECARD_FETCH_TIMEOUT = 1000 * 10;
        const int INVENTORY_FETCH_TIMEOUT = 1000 * 10;

        public CreateNotecardCommand(TestClient testClient)
        {
            Name = "createnotecard";
            Description = "Creates a notecard from a local text file and optionally embed an inventory item. Usage: createnotecard filename.txt [itemid]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            UUID embedItemID = UUID.Zero, notecardItemID = UUID.Zero, notecardAssetID = UUID.Zero;
            string filename, fileData;
            bool finalUploadSuccess = false;

            if (args.Length == 1)
            {
                filename = args[0];
            }
            else if (args.Length == 2)
            {
                filename = args[0];
                UUID.TryParse(args[1], out embedItemID);
            }
            else
            {
                return "Usage: createnotecard filename.txt";
            }

            if (!File.Exists(filename))
                return "File \"" + filename + "\" does not exist";

            try { fileData = File.ReadAllText(filename); }
            catch (Exception ex) { return "Failed to open " + filename + ": " + ex.Message; }

            #region Notecard asset data

            AssetNotecard notecard = new AssetNotecard
            {
                BodyText = fileData
            };

            // Item embedding
            if (embedItemID != UUID.Zero)
            {
                // Try to fetch the inventory item
                var item = await FetchItemAsync(embedItemID).ConfigureAwait(false);
                if (item != null)
                {
                    notecard.EmbeddedItems = new List<InventoryItem> { item };
                    notecard.BodyText += (char)0xdbc0 + (char)0xdc00;
                }
                else
                {
                    return "Failed to fetch inventory item " + embedItemID;
                }
            }

            notecard.Encode();

            #endregion Notecard asset data

            InventoryItem? createdItem;
            try
            {
                using var createCts = new CancellationTokenSource(NOTECARD_CREATE_TIMEOUT);
                createdItem = await Client.Inventory.CreateItemAsync(
                    Client.Inventory.FindFolderForType(AssetType.Notecard),
                    filename, filename + " created by LibreMetaverse TestClient " + DateTime.Now,
                    AssetType.Notecard, UUID.Random(), InventoryType.Notecard, PermissionMask.All,
                    createCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                createdItem = null;
            }

            if (createdItem == null)
                return "Notecard item creation failed or timed out";

            notecardItemID = createdItem.UUID;

            // Upload the notecard asset
            using var uploadCts = new CancellationTokenSource(NOTECARD_CREATE_TIMEOUT);
            (finalUploadSuccess, _, _, notecardAssetID) = await Client.Inventory.RequestUploadNotecardAssetAsync(notecard.AssetData, createdItem.UUID, uploadCts.Token).ConfigureAwait(false);

            if (finalUploadSuccess)
            {
                Logger.Info($"Notecard successfully created, ItemID {notecardItemID} AssetID {notecardAssetID}");
                return await DownloadNotecardAsync(notecardItemID, notecardAssetID).ConfigureAwait(false);
            }
            else
                return "Notecard creation failed during upload";
        }

        private async Task<InventoryItem?> FetchItemAsync(UUID itemID)
        {
            var tcs = new TaskCompletionSource<InventoryItem?>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<ItemReceivedEventArgs> itemReceivedCallback = null;
            itemReceivedCallback = (sender, e) =>
            {
                if (e.Item.UUID == itemID)
                {
                    tcs.TrySetResult(e.Item);
                }
            };

            try
            {
                Client.Inventory.ItemReceived += itemReceivedCallback;
                Client.Inventory.RequestFetchInventory(itemID, Client.Self.AgentID);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(INVENTORY_FETCH_TIMEOUT)).ConfigureAwait(false);
                if (completed == tcs.Task)
                    return await tcs.Task.ConfigureAwait(false);
                else
                    return null;
            }
            finally
            {
                Client.Inventory.ItemReceived -= itemReceivedCallback;
            }
        }

        private async Task<string> DownloadNotecardAsync(UUID itemID, UUID assetID)
        {
            using var cts = new CancellationTokenSource(NOTECARD_FETCH_TIMEOUT);
            var asset = await Client.Assets.RequestInventoryAssetAsync(assetID, itemID, UUID.Zero, Client.Self.AgentID,
                AssetType.Notecard, true, UUID.Random(), cts.Token).ConfigureAwait(false);

            if (asset != null)
                return Encoding.UTF8.GetString(asset.AssetData);
            return "Error downloading notecard asset";
        }
    }
}
