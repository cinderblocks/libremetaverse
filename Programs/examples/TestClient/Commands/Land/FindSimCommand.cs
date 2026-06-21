using System.Threading.Tasks;
using LibreMetaverse;

namespace TestClient.Commands.Land
{
    public class FindSimCommand : Command
    {
        public FindSimCommand(TestClient testClient)
        {
            Name = "findsim";
            Description = "Searches for a simulator and returns information about it. Usage: findsim [Simulator Name]";
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: findsim [Simulator Name]";

            // Build the simulator name from the args list
            string simName = string.Join(" ", args).TrimEnd().ToLower();

            var region = await Client.Grid.GetGridRegionAsync(simName, GridLayerType.Objects).ConfigureAwait(false);

            if (region != null)
                return $"{region.Value.Name}: handle={region.Value.RegionHandle} ({region.Value.X},{region.Value.Y})";
            else
                return "Lookup of " + simName + " failed";
        }
    }
}
