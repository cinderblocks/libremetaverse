using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            Manager = Client.Inventory;
            Inventory = Manager.Store;

            StringBuilder result = new StringBuilder();
            var watch = Stopwatch.StartNew();

            InventoryFolder rootFolder = Inventory.RootFolder;
            var itemCount = await PrintFolderAsync(rootFolder, result, 0).ConfigureAwait(false);
            watch.Stop();

            result.AppendLine();
            result.AppendLine($"Returned {itemCount} items in {watch.Elapsed.TotalSeconds} seconds.");
            return result.ToString();
        }

        private async Task<int> PrintFolderAsync(InventoryFolder f, StringBuilder result, int indent)
        {
            var numItems = 0;

            var contents = await Manager.FolderContentsAsync(f.UUID, Client.Self.AgentID, true, true, InventorySortOrder.ByName).ConfigureAwait(false);

            if (contents != null)
            {
                foreach (InventoryBase i in contents)
                {
                    result.AppendFormat("{0}{1} ({2})\n", new string(' ', indent * 2), i.Name, i.UUID);
                    numItems++;
                    if (i is InventoryFolder folder)
                    {
                        numItems += await PrintFolderAsync(folder, result, indent + 1).ConfigureAwait(false);
                    }
                }
            }

            return numItems;
        }
    }
}