using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using LibreMetaverse;

namespace TestClient.Commands.Inventory
{
    /// <summary>
    /// Example of how to put a new script in your inventory
    /// </summary>
    public class UploadScriptCommand : Command
    {
        /// <summary>
        ///  The default constructor for TestClient commands
        /// </summary>
        /// <param name="testClient"></param>
        public UploadScriptCommand(TestClient testClient)
        {
            Name = "uploadscript";
            Description = "Upload a local .lsl file file into your inventory.";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentId)
        {
            return ExecuteAsync(args, fromAgentId).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentId)
        {
            if (args.Length < 1)
                return "Usage: uploadscript filename.lsl";

            var file = args.Aggregate(string.Empty, (current, t) => $"{current}{t} ");
            file = file.TrimEnd();

            if (!File.Exists(file))
                return $"Filename '{file}' does not exist";

            try
            {
                string body;
                using (var reader = new StreamReader(file))
                {
                    body = await reader.ReadToEndAsync();
                }

                var desc = $"{file} created by LibreMetaverse TestClient {DateTime.Now}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                InventoryItem? createdItem;
                try
                {
                    createdItem = await Client.Inventory.CreateItemAsync(
                        Client.Inventory.FindFolderForType(AssetType.LSLText),
                        file, desc, AssetType.LSLText, UUID.Random(),
                        InventoryType.LSL, PermissionMask.All, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return "Timed out creating inventory item";
                }

                if (createdItem == null)
                {
                    return "Failed to create inventory item";
                }

                using var uploadCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var (uploadSuccess, _, compileSuccess, _, itemId, assetId) = await Client.Inventory
                    .RequestUpdateScriptAgentInventoryAsync(Encoding.UTF8.GetBytes(body), createdItem.UUID, true, uploadCts.Token).ConfigureAwait(false);

                var log = $"Filename: {file}";
                log += uploadSuccess ? $" Script successfully uploaded, ItemID {itemId} AssetID {assetId}" : $" Script failed to upload, ItemID {itemId}";
                log += compileSuccess ? " compilation successful" : " compilation failed";
                Logger.Info(log, Client);
                return $"Filename: {file} is being uploaded.";
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString(), Client);
                return $"Error creating script for {file}";
            }
        }
    }
}

