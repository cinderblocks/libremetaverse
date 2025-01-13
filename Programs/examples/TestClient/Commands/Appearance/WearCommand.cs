using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.TestClient
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
            if (args.Length < 1)
                return "Usage: wear [outfit name] eg: 'wear Clothing/My Outfit";

            string target = args.Aggregate(string.Empty, (current, t) => current + (t + " "));

            target = target.TrimEnd();

            UUID folder = Client.Inventory.FindObjectByPath(Client.Inventory.Store.RootFolder.UUID, Client.Self.AgentID, target, TimeSpan.FromSeconds(20));

            if (folder == UUID.Zero)
            {
                return "Outfit path " + target + " not found";
            }

            List<InventoryBase> contents =  Client.Inventory.FolderContents(folder, Client.Self.AgentID, true, true, InventorySortOrder.ByName, TimeSpan.FromSeconds(20));
            List<InventoryItem> items = new List<InventoryItem>();

            if (contents == null)
            {
                return "Failed to get contents of " + target;
            }

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
