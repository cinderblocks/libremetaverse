using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class LocationCommand: Command
    {
        public LocationCommand(TestClient testClient)
		{
			Name = "location";
			Description = "Show current location of avatar.";
            Category = CommandCategory.Movement;
		}

		public override string Execute(string[] args, UUID fromAgentID)
		{
            return "CurrentSim: '" + Client.Network.CurrentSim + "' Position: " + 
                Client.Self.SimPosition;
		}
    }
}
