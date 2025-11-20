using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    public class GridMapCommand : Command
    {
        public GridMapCommand(TestClient testClient)
        {
            Name = "gridmap";
            Description = "Downloads all visible information about the grid map";
            Category = CommandCategory.Simulator;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            Client.Grid.RequestMainlandSims(GridLayerType.Objects);
            return Task.FromResult("Sent.");
        }
    }
}
