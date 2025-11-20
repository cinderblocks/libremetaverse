using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class ListContentsCommand : Command
    {
        private InventoryManager Manager;
        private OpenMetaverse.Inventory Inventory;
        public ListContentsCommand(TestClient client)
        {
            Name = "ls";
            Description = "Lists the contents of the current working inventory folder.";
            Category = CommandCategory.Inventory;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length > 1)
                return "Usage: ls [-l]";
            bool longDisplay = args.Length > 0 && args[0] == "-l";

            Manager = Client.Inventory;
            Inventory = Manager.Store;

            // Use async folder contents
            List<InventoryBase> contents = await Manager.FolderContentsAsync(Client.CurrentDirectory.UUID, Client.Self.AgentID, true, true, InventorySortOrder.ByName).ConfigureAwait(false);

            StringBuilder display = new StringBuilder();
            // Pretty simple, just print out the contents.
            foreach (InventoryBase b in contents)
            {
                if (longDisplay)
                {
                    if (b is InventoryFolder folder)
                    {
                        display.Append("d--------- ");
                        display.Append(folder.UUID);
                        display.Append(" " + folder.Name);
                    }
                    else if (b is InventoryItem item)
                    {
                        display.Append("-");
                        display.Append(PermMaskString(item.Permissions.OwnerMask));
                        display.Append(PermMaskString(item.Permissions.GroupMask));
                        display.Append(PermMaskString(item.Permissions.EveryoneMask));
                        display.Append(" " + item.UUID);
                        display.Append(" " + item.Name);
                        display.Append('\n');
                        display.Append("  AssetID: " + item.AssetUUID);
                    }
                }
                else
                {
                    display.Append(b.Name);
                }
                display.Append('\n');
            }
            return display.ToString();
        }

        private static string PermMaskString(PermissionMask mask)
        {
            string str = "";
            if (((uint)mask | (uint)PermissionMask.Copy) == (uint)PermissionMask.Copy)
                str += "C";
            else
                str += "-";
            if (((uint)mask | (uint)PermissionMask.Modify) == (uint)PermissionMask.Modify)
                str += "M";
            else
                str += "-";
            if (((uint)mask | (uint)PermissionMask.Transfer) == (uint)PermissionMask.Transfer)
                str += "T";
            else
                str += "-";
            return str;
        }
    }
}
