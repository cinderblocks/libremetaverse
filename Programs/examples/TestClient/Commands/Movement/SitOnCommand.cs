using System.Linq;

namespace OpenMetaverse.TestClient
{
    public class SitOnCommand : Command
    {
        public SitOnCommand(TestClient testClient)
        {
            Name = "siton";
            Description = "Attempt to sit on a particular prim, with specified UUID";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: siton UUID";

            UUID target;

            if (UUID.TryParse(args[0], out target))
            {
                var targetPrim = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(
                    prim => prim.Value.ID == target
                );

                if (targetPrim.Value != null)
                {
                    Client.Self.RequestSit(targetPrim.Value.ID, Vector3.Zero);
                    Client.Self.Sit();
                    return "Requested to sit on prim " + targetPrim.Value.ID +
                           " (" + targetPrim.Value.LocalID + ")";
                }
            }

            return "Couldn't find a prim to sit on with UUID " + args[0];
        }
    }
}
