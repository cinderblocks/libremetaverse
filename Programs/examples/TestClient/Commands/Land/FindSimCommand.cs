using System.Threading.Tasks;
using OpenMetaverse;

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

            var result = await Task.Run(() =>
            {
                GridRegion r;
                bool g = Client.Grid.GetGridRegion(simName, GridLayerType.Objects, out r);
                return (got: g, region: r);
            }).ConfigureAwait(false);

            if (result.got)
                return $"{result.region.Name}: handle={result.region.RegionHandle} ({result.region.X},{result.region.Y})";
            else
                return "Lookup of " + simName + " failed";
        }
    }
}
