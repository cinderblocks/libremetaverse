using System.Threading.Tasks;
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
	    }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            Client.Self.Stand();
            return Task.FromResult("Standing up.");
        }
    }
}
