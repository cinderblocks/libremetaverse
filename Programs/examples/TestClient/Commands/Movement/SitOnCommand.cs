using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
                return Task.FromResult("Usage: siton UUID");

            if (UUID.TryParse(args[0], out var target))
            {
                var kvp = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == target);

                if (kvp.Value != null)
                {
                    var targetPrim = kvp.Value;
                    Client.Self.RequestSit(targetPrim.ID, Vector3.Zero);
                    Client.Self.Sit();
                    return Task.FromResult("Requested to sit on prim " + targetPrim.ID +
                        " (" + targetPrim.LocalID + ")");
                }
            }

            return Task.FromResult("Couldn't find a prim to sit on with UUID " + args[0]);
        }
    }
}
