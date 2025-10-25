using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class StandCommand: Command
    {
        public StandCommand(TestClient testClient)
	{
		Name = "stand";
		Description = "Stand";
        Category = CommandCategory.Movement;
	}
	
        public override string Execute(string[] args, UUID fromAgentID)
	    {
            Client.Self.Stand();
		    return "Standing up.";  
	    }
    }
}
