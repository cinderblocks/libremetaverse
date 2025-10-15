using System;
using System.Linq;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class ObjectInventoryCommand : Command
    {
        public ObjectInventoryCommand(TestClient testClient)
        {
            Name = "objectinventory";
            Description = "Retrieves a listing of items inside an object (task inventory). Usage: objectinventory [objectID]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: objectinventory [objectID]";
            }

            if (!UUID.TryParse(args[0], out var objectID))
            {
                return "Usage: objectinventory [objectID]";
            }

            var found = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == objectID);
            if (found.Value == null)
            {
                return $"Could not find ${objectID} object";
            } 
            
            var objectLocalID = found.Value.LocalID;

            var items = Client.Inventory.GetTaskInventory(objectID, objectLocalID, TimeSpan.FromSeconds(30));

            if (items == null)
            {
                return $"Failed to download task inventory for {objectLocalID}";
            }

            var result = string.Empty;

            foreach (var i in items)
            {
                if (i is InventoryFolder)
                {
                    result += $"[Folder] Name: {i.Name}" + Environment.NewLine;
                }
                else
                {
                    InventoryItem item = (InventoryItem)i;
                    result += $"[Item] Name: {item.Name} Desc: {item.Description} Type: {item.AssetType}" + Environment.NewLine;
                }
            }

            return result;

        }
    }
}
