using System;
using OpenMetaverse;
using OpenMetaverse.Assets;

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

        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="args"></param>
        /// <param name="fromAgentID"></param>
        /// <returns></returns>
        public override string Execute(string[] args, UUID fromAgentID)
        {

            if (args.Length < 1)
            {
                return "Usage: viewnote [notecard asset uuid]";
            }
            UUID note;
            if (!UUID.TryParse(args[0], out note))
            {
                return "First argument expected agent UUID.";
            }

            global::System.Threading.AutoResetEvent waitEvent = new global::System.Threading.AutoResetEvent(false);

            global::System.Text.StringBuilder result = new global::System.Text.StringBuilder();

            // verify asset is loaded in store
            if (Client.Inventory.Store.Contains(note))
            {
                // retrieve asset from store
                InventoryItem ii = (InventoryItem)Client.Inventory.Store[note];

                // make request for asset
                var transferID = UUID.Random();
                Client.Assets.RequestInventoryAsset(ii, true, transferID,
                    delegate(AssetDownload transfer, Asset asset)
                    {
                        if (transfer.Success)
                        {
                            result.AppendFormat("Raw Notecard Data: " + global::System.Environment.NewLine + " {0}", Utils.BytesToString(asset.AssetData));
                            waitEvent.Set();
                        }
                    }
                );

                // wait for reply or timeout
                if (!waitEvent.WaitOne(TimeSpan.FromSeconds(10), false))
                {
                    result.Append("Timeout waiting for notecard to download.");
                }
            }
            else
            {
                result.Append("Cannot find asset in inventory store, use 'i' to populate store");
            }

            // return results
            return result.ToString();
        }
    }
}
