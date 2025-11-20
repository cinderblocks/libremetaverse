using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    class MovetoCommand : Command
    {
        public MovetoCommand(TestClient client)
        {
            Name = "moveto";
            Description = "Moves the avatar to the specified global position using simulator autopilot. Usage: moveto x y z";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 3)
                return Task.FromResult("Usage: moveto x y z");

            uint regionX, regionY;
            Utils.LongToUInts(Client.Network.CurrentSim.Handle, out regionX, out regionY);

            if (!double.TryParse(args[0], out var x) ||
                !double.TryParse(args[1], out var y) ||
                !double.TryParse(args[2], out var z))
            {
                return Task.FromResult("Usage: moveto x y z");
            }

            // Convert the local coordinates to global ones by adding the region handle parts to x and y
            x += (double)regionX;
            y += (double)regionY;

            Client.Self.AutoPilot(x, y, z);

            return Task.FromResult($"Attempting to move to <{x},{y},{z}>");
        }
    }
}
