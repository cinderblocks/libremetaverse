using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenMetaverse.TestClient
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
                return "Usage: objectinventory [objectID]";

            uint objectLocalID;
            UUID objectID;
            if (!UUID.TryParse(args[0], out objectID))
                return "Usage: objectinventory [objectID]";

            var found = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == objectID);
            
            if (found.Value != null)
            {
                objectLocalID = found.Value.LocalID;
            }
            else
            {
                return "Couldn't find prim " + objectID;
            }

            List<InventoryBase> items = Client.Inventory.GetTaskInventory(objectID, objectLocalID, 1000 * 30);

            if (items != null)
            {
                string result = string.Empty;

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
            else
            {
                return "Failed to download task inventory for " + objectLocalID;
            }
        }
    }
}
