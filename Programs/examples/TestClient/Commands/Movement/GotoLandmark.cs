using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class GotoLandmarkCommand : Command
    {
        public GotoLandmarkCommand(TestClient testClient)
        {
            Name = "goto_landmark";
            Description = "Teleports to a Landmark. Usage: goto_landmark [UUID]";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
            {
                return Task.FromResult("Usage: goto_landmark [UUID]");
            }

            if (!UUID.TryParse(args[0], out var landmark))
            {
                return Task.FromResult("Invalid UUID");
            }

            Console.WriteLine("Teleporting to " + landmark);
            return Task.FromResult(Client.Self.Teleport(landmark) ? "Teleport Successful" : "Teleport Failed");
        }
    }
}
