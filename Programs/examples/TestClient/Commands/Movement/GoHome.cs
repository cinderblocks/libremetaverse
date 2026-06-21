using System.Threading.Tasks;
using LibreMetaverse;

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

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            return await Client.Self.GoHomeAsync().ConfigureAwait(false)
                ? "Teleport Home Succesful"
                : "Teleport Home Failed";
        }
    }
}
