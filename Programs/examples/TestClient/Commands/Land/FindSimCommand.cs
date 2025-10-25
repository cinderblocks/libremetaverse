using System.Linq;
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
            if (args.Length < 1)
                return "Usage: findsim [Simulator Name]";

            // Build the simulator name from the args list
            string simName = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            simName = simName.TrimEnd().ToLower();

            //if (!GridDataCached[Client])
            //{
            //    Client.Grid.RequestAllSims(GridManager.MapLayerType.Objects);
            //    System.Threading.Thread.Sleep(5000);
            //    GridDataCached[Client] = true;
            //}

            GridRegion region;

            if (Client.Grid.GetGridRegion(simName, GridLayerType.Objects, out region))
                return $"{region.Name}: handle={region.RegionHandle} ({region.X},{region.Y})";
            else
                return "Lookup of " + simName + " failed";
        }
    }
}
