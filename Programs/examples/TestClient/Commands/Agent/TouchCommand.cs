using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Agent
{
    public class TouchCommand: Command
    {
        public TouchCommand(TestClient testClient)
		{
			Name = "touch";
			Description = "Attempt to touch a prim with specified UUID";
            Category = CommandCategory.Objects;
		}
		
        public override string Execute(string[] args, UUID fromAgentID)
		{
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
		}

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
		{
            if (args.Length != 1)
            {
                return Task.FromResult("Usage: touch UUID");
            }

            if (!UUID.TryParse(args[0], out var target))
            {
                return Task.FromResult($"{args[0]} is not a valid UUID");
            }
            var targetPrim = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == target);

            if (targetPrim.Value == null)
            {
                return Task.FromResult($"Couldn't find an object to touch with UUID {args[0]}");
            }

            Client.Self.Touch(targetPrim.Value.LocalID);
            return Task.FromResult($"Touched object {targetPrim.Value.LocalID}");
        }
    }
}
