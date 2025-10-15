using System;
using System.Collections.Generic;
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

            InventoryFolder rootFolder = Inventory.RootFolder;
            PrintFolder(rootFolder, result, 0);

            return result.ToString();
        }

        void PrintFolder(InventoryFolder f, StringBuilder result, int indent)
        {
            List<InventoryBase> contents = Manager.FolderContents(f.UUID, Client.Self.AgentID,
                true, true, InventorySortOrder.ByName, TimeSpan.FromSeconds(3));

            if (contents != null)
            {
                foreach (InventoryBase i in contents)
                {
                    result.AppendFormat("{0}{1} ({2})\n", new string(' ', indent * 2), i.Name, i.UUID);
                    if (i is InventoryFolder folder)
                    {
                        PrintFolder(folder, result, indent + 1);
                    }
                }
            }
        }
    }
}