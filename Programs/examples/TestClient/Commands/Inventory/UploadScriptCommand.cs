using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using OpenMetaverse;

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

                var desc = $"{file} created by OpenMetaverse TestClient {DateTime.Now}";

                var createTcs = new TaskCompletionSource<InventoryItem>(TaskCreationOptions.RunContinuationsAsynchronously);

                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.LSLText),
                    file, desc, AssetType.LSLText, UUID.Random(),
                    InventoryType.LSL, PermissionMask.All, (success, item) =>
                    {
                        if (success) createTcs.TrySetResult(item);
                        else createTcs.TrySetException(new Exception("Item creation failed"));
                    });

                var createCompleted = await Task.WhenAny(createTcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (createCompleted != createTcs.Task)
                    return "Timed out creating inventory item";

                var createdItem = await createTcs.Task.ConfigureAwait(false);

                var uploadTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                var uploadTask = Client.Inventory.RequestUpdateScriptAgentInventoryAsync(Encoding.UTF8.GetBytes(body), createdItem.UUID, true,
                    (uploadSuccess, uploadStatus, compileSuccess, compileMessages, itemId, assetId) =>
                    {
                        var log = $"Filename: {file}";
                        if (uploadSuccess)
                            log += $" Script successfully uploaded, ItemID {itemId} AssetID {assetId}";
                        else
                            log += $" Script failed to upload, ItemID {itemId}";

                        if (compileSuccess)
                            log += " compilation successful";
                        else
                            log += " compilation failed";

                        uploadTcs.TrySetResult(log);
                    });

                var uploadCompleted = await Task.WhenAny(uploadTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                if (uploadCompleted != uploadTcs.Task)
                    return "Timed out uploading script";

                var resultLog = await uploadTcs.Task.ConfigureAwait(false);
                Logger.Info(resultLog, Client);
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

