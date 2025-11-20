using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class ViewNotecardCommand : Command
    {
        /// <summary>
        /// TestClient command to download and display a notecard asset
        /// </summary>
        /// <param name="testClient"></param>
        public ViewNotecardCommand(TestClient testClient)
        {
            Name = "viewnote";
            Description = "Downloads and displays a notecard asset";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
            {
                return "Usage: viewnote [notecard asset uuid]";
            }

            if (!UUID.TryParse(args[0], out var note))
            {
                return "First argument expected agent UUID.";
            }

            var sb = new StringBuilder();

            // verify asset is loaded in store
            if (Client.Inventory.Store.Contains(note))
            {
                // retrieve asset from store
                InventoryItem ii = (InventoryItem)Client.Inventory.Store[note];

                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                var transferID = UUID.Random();
                Client.Assets.RequestInventoryAsset(ii, true, transferID,
                    (transfer, asset) =>
                    {
                        if (transfer.Success)
                        {
                            sb.AppendFormat("Raw Notecard Data: " + global::System.Environment.NewLine + " {0}", Utils.BytesToString(asset.AssetData));
                            tcs.TrySetResult(sb.ToString());
                        }
                        else
                        {
                            tcs.TrySetResult("Failed to download notecard");
                        }
                    }
                );

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);

                if (completed != tcs.Task)
                    return "Timeout waiting for notecard to download.";

                return await tcs.Task.ConfigureAwait(false);
            }
            else
            {
                return "Cannot find asset in inventory store, use 'i' to populate store";
            }
        }
    }
}
