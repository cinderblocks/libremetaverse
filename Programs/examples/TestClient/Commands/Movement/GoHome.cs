using System.Threading.Tasks;
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            return Task.FromResult(Client.Self.GoHome() ? "Teleport Home Succesful" : "Teleport Home Failed");
        }
    }
}
