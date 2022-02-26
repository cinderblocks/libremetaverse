using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Inventory Example, Moves a folder to the Trash folder
    /// </summary>
    public class DeleteFolderCommand : Command
    {
        public DeleteFolderCommand(TestClient testClient)
        {
            Name = "deleteFolder";
            Description = "Moves a folder to the Trash Folder";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            // parse the command line
            string target = args.Aggregate(string.Empty, (current, t) => current + t + " ");
            target = target.TrimEnd();

            // initialize results list
            List<InventoryBase> found = new List<InventoryBase>();

            // find the folder
            found = Client.Inventory.LocalFind(Client.Inventory.Store.RootFolder.UUID, target.Split('/'), 0, true);
            
            if (found.Count.Equals(1))
            {
                // move the folder to the trash folder
                Client.Inventory.MoveFolder(found[0].UUID, Client.Inventory.FindFolderForType(FolderType.Trash));
                
                return $"Moved folder {found[0].Name} to Trash";
            }

            return string.Empty;
        }
    }
}