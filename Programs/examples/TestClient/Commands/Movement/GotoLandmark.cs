using System;
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
            if (args.Length < 1)
            {
                return "Usage: goto_landmark [UUID]";
            }

            UUID landmark = new UUID();
            if (!UUID.TryParse(args[0], out landmark))
            {
                return "Invalid UUID";
            }

            Console.WriteLine("Teleporting to " + landmark);
            return Client.Self.Teleport(landmark) ? "Teleport Successful" : "Teleport Failed";
        }
    }
}
