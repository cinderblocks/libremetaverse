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
            //if (args.Length < 1)
            //    return "";

            Client.Grid.RequestMainlandSims(GridLayerType.Objects);
            
            return "Sent.";
        }
    }
}
