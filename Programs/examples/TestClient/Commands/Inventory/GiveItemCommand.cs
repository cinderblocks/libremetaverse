using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.TestClient.Commands.Inventory.Shell
{
    class GiveItemCommand : Command
    {
        private InventoryManager Manager;
        private OpenMetaverse.Inventory Inventory;
        public GiveItemCommand(TestClient client)
        {
            Name = "give";
            Description = "Gives items from the current working directory to an avatar.";
            Category = CommandCategory.Inventory;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 2)
            {
                return "Usage: give <agent uuid> itemname";
            }
            UUID dest;
            if (!UUID.TryParse(args[0], out dest))
            {
                return "First argument expected agent UUID.";
            }
            Manager = Client.Inventory;
            Inventory = Manager.Store;
            string ret = "";

            string target = args.Aggregate(string.Empty, (current, t) => current + t + " ");
            target = target.TrimEnd();

            string inventoryName = target;
            // WARNING: Uses local copy of inventory contents, need to download them first.
            List<InventoryBase> contents = Inventory.GetContents(Client.CurrentDirectory);
            bool found = false;
            foreach (var b in contents.Where(b => inventoryName == b.Name || inventoryName == b.UUID.ToString()))
            {
                found = true;
                if (b is InventoryItem item)
                {
                    Manager.GiveItem(item.UUID, item.Name, item.AssetType, dest, true);
                    ret += "Gave " + item.Name + " (" + item.AssetType + ")" + '\n';
                }
                else
                {
                    ret += "Unable to give folder " + b.Name + '\n';
                }
            }
            if (!found)
                ret += "No inventory item named " + inventoryName + " found." + '\n';

            return ret;
        }
    }
}
