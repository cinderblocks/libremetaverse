using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class SetHomeCommand : Command
    {
        public SetHomeCommand(TestClient testClient)
        {
            Name = "sethome";
            Description = "Sets home to the current location.";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            Client.Self.SetHome();
            return Task.FromResult("Home Set");
        }
    }
}
