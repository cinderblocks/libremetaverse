using System.Linq;

namespace OpenMetaverse.TestClient
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
            UUID primID;

            if (args.Length != 1)
                return "Usage: derez [prim-uuid]";

            if (UUID.TryParse(args[0], out primID))
            {
                var target = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(
                    prim => prim.Value.ID == primID
                );

                if (target.Value != null)
                {
                    uint objectLocalID = target.Value.LocalID;
                    Client.Inventory.RequestDeRezToInventory(objectLocalID, DeRezDestination.AgentInventoryTake,
                                                             Client.Inventory.FindFolderForType(FolderType.Trash),
                                                             UUID.Random());
                    return "removing " + target;
                }
                else
                {
                    return "Could not find prim " + primID;
                }
            }
            else
            {
                return "Usage: derez [prim-uuid]";
            }
        }
    }
}
