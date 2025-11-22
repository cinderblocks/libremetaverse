using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Appearance
{
    public class WearCommand : Command
    {
        public WearCommand(TestClient testClient)
        {
            Client = testClient;
            Name = "wear";
            Description = "Wear an outfit folder from inventory. Usage: wear [outfit name]";
            Category = CommandCategory.Appearance;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: wear [outfit name] eg: 'wear Clothing/My Outfit";

            string target = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            target = target.TrimEnd();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                UUID folder = await Client.Inventory.FindObjectByPathAsync(Client.Inventory.Store.RootFolder.UUID, Client.Self.AgentID, target, cts.Token).ConfigureAwait(false);

                if (folder == UUID.Zero)
                {
                    return "Outfit path " + target + " not found";
                }

                List<InventoryBase> contents;
                try
                {
                    contents = await Client.Inventory.RequestFolderContents(folder, Client.Self.AgentID, true, true, InventorySortOrder.ByName, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return "Failed to get contents of " + target + " (timeout)";
                }
                catch (Exception)
                {
                    return "Failed to get contents of " + target;
                }

                if (contents == null)
                {
                    return "Failed to get contents of " + target;
                }

                var items = new List<InventoryItem>();
                foreach (InventoryBase item in contents)
                {
                    if (item is InventoryItem inventoryItem)
                        items.Add(inventoryItem);
                }

                Client.Appearance.ReplaceOutfit(items);

                return "Starting to change outfit to " + target;
            }
        }
    }
}
