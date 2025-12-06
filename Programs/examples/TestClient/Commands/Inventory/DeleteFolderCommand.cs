using System.Collections.Generic;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            // parse the command line
            string target = string.Join(" ", args).TrimEnd();

            // initialize results list
            List<InventoryBase> found = new List<InventoryBase>();

            // find the folder
            found = Client.Inventory.LocalFind(Client.Inventory.Store.RootFolder.UUID, target.Split('/'), 0, true);

            if (found.Count.Equals(1))
            {
                // move the folder to the trash folder using async API
                await Client.Inventory.MoveFolderAsync(found[0].UUID, Client.Inventory.FindFolderForType(FolderType.Trash));

                return $"Moved folder {found[0].Name} to Trash";
            }

            return string.Empty;
        }
    }
}