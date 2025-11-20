using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class FlyCommand : Command
    {
        public FlyCommand(TestClient testClient)
        {
            Name = "fly";
            Description = "Starts or stops flying. Usage: fly [start/stop]";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            bool start = !(args.Length == 1 && args[0].ToLower() == "stop");

            if (start)
            {
                Client.Self.Fly(true);
                return Task.FromResult("Started flying");
            }
            else
            {
                Client.Self.Fly(false);
                return Task.FromResult("Stopped flying");
            }
        }
    }
}
