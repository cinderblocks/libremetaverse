using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class SitCommand: Command
    {
        public SitCommand(TestClient testClient)
		{
			Name = "sit";
			Description = "Attempt to sit on the closest prim";
            Category = CommandCategory.Movement;
		}
			
        public override string Execute(string[] args, UUID fromAgentID)
		{
            Primitive closest = null;
		    double closestDistance = double.MaxValue;

            foreach (var kvp in Client.Network.CurrentSim.ObjectsPrimitives)
            {
                if (kvp.Value == null) { continue; }

                var prim = kvp.Value;
                var distance = Vector3.Distance(Client.Self.SimPosition, prim.Position);
                if (closest == null || distance < closestDistance)
                {
                    closest = prim;
                    closestDistance = distance;
                }
            }

            if (closest == null)
            {
                return "Couldn't find a nearby prim to sit on";
            }
            Client.Self.RequestSit(closest.ID, Vector3.Zero);
            Client.Self.Sit();

            return $"Sat on {closest.ID} ({closest.LocalID}). Distance: {closestDistance}";

        }
    }
}
