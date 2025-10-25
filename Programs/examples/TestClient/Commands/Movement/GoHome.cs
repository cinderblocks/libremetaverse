using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class GoHomeCommand : Command
    {
		public GoHomeCommand(TestClient testClient)
        {
            Name = "gohome";
            Description = "Teleports home";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
	        return Client.Self.GoHome() ? "Teleport Home Succesful" : "Teleport Home Failed";
        }
    }
}
