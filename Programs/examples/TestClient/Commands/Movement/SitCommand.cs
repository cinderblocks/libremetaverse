using System;

namespace OpenMetaverse.TestClient
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

            foreach (var primPair in Client.Network.CurrentSim.ObjectsPrimitives)
            {
                float distance = Vector3.Distance(Client.Self.SimPosition, primPair.Value.Position);

                if (closest == null || distance < closestDistance)
                {
                    closest = primPair.Value;
                    closestDistance = distance;
                }
            }

            if (closest != null)
            {
                Client.Self.RequestSit(closest.ID, Vector3.Zero);
                Client.Self.Sit();

                return "Sat on " + closest.ID + " (" + closest.LocalID + "). Distance: " + closestDistance;
            }
            else
            {
                return "Couldn't find a nearby prim to sit on";
            }
		}
    }
}
