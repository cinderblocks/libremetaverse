using System.Linq;

namespace OpenMetaverse.TestClient
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
            if (args.Length != 1)
            {
                return "Usage: touch UUID";
            }

            if (!UUID.TryParse(args[0], out var target))
            {
                return $"{args[0]} is not a valid UUID";
            }
            var targetPrim = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == target);

            if (targetPrim.Value == null)
            {
                return $"Couldn't find an object to touch with UUID {args[0]}";
            }

            Client.Self.Touch(targetPrim.Value.LocalID);
            return $"Touched object {targetPrim.Value.LocalID}";

        }
    }
}
