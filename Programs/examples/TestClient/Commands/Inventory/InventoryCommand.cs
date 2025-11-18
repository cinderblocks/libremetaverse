using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class InventoryCommand : Command
    {
        private OpenMetaverse.Inventory Inventory;
        private InventoryManager Manager;

        public InventoryCommand(TestClient testClient)
        {
            Name = "i";
            Description = "Prints out inventory.";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            Manager = Client.Inventory;
            Inventory = Manager.Store;

            StringBuilder result = new StringBuilder();
            var watch = Stopwatch.StartNew();

            InventoryFolder rootFolder = Inventory.RootFolder;
            var itemCount = PrintFolder(rootFolder, result, 0);
            watch.Stop();

            result.AppendLine();
            result.AppendLine($"Returned {itemCount} items in {watch.Elapsed.TotalSeconds} seconds.");
            return result.ToString();
        }

        int PrintFolder(InventoryFolder f, StringBuilder result, int indent)
        {
            var numItems = 0;
            List<InventoryBase> contents = Manager.FolderContents(f.UUID, Client.Self.AgentID,
                true, true, InventorySortOrder.ByName, TimeSpan.FromSeconds(3));

            if (contents != null)
            {
                foreach (InventoryBase i in contents)
                {
                    result.AppendFormat("{0}{1} ({2})\n", new string(' ', indent * 2), i.Name, i.UUID);
                    numItems++;
                    if (i is InventoryFolder folder)
                    {
                        numItems += PrintFolder(folder, result, indent + 1);
                    }
                }
            }

            return numItems;
        }
    }
}