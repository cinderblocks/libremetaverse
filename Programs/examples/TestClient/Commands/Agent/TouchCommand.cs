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
            UUID target;

            if (args.Length != 1)
                return "Usage: touch UUID";
            
            if (UUID.TryParse(args[0], out target))
            {
                var targetPrim = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(
                    prim => prim.Value.ID == target
                );

                var primitive = targetPrim.Value;

                if (primitive != null)
                {
                    Client.Self.Touch(primitive.LocalID);
                    return "Touched prim " + primitive.LocalID;
                }
            }

            return "Couldn't find a prim to touch with UUID " + args[0];
		}
    }
}
