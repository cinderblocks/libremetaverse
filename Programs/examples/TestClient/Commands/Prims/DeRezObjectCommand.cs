using System.Linq;
using OpenMetaverse;

namespace TestClient.Commands.Prims
{
    public class DeRezCommand : Command
    {
        public DeRezCommand(TestClient testClient)
        {
            Name = "derez";
            Description = "De-Rezes a specified prim. " + "Usage: derez [prim-uuid]";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: derez [prim-uuid]";
            }

            if (!UUID.TryParse(args[0], out var primID))
            {
                return $"{args[0]} is not a valid UUID";
            }
            
            var kvp = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == primID);
            if (kvp.Value == null)
            {
                return $"Could not find object {primID}";
            }
            var target = kvp.Value;
            var objectLocalID = target.LocalID;
            Client.Inventory.RequestDeRezToInventory(objectLocalID, DeRezDestination.AgentInventoryTake,
                Client.Inventory.FindFolderForType(FolderType.Trash),
                UUID.Random());
            return $"Removing {target}";

        }
    }
}
