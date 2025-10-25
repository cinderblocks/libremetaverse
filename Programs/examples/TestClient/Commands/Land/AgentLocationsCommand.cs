using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    /// <summary>
    /// Display a list of all agent locations in a specified region
    /// </summary>
    public class AgentLocationsCommand : Command
    {
        public AgentLocationsCommand(TestClient testClient)
        {
            Name = "agentlocations";
            Description = "Downloads all of the agent locations in a specified region. Usage: agentlocations [regionhandle]";
            Category = CommandCategory.Simulator;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            ulong regionHandle;

            if (args.Length == 0)
                regionHandle = Client.Network.CurrentSim.Handle;
            else if (!(args.Length == 1 && ulong.TryParse(args[0], out regionHandle)))
                return "Usage: agentlocations [regionhandle]";

            List<MapItem> items = Client.Grid.MapItems(regionHandle, GridItemType.AgentLocations, 
                GridLayerType.Objects, TimeSpan.FromSeconds(20));

            if (items != null)
            {
                StringBuilder ret = new StringBuilder();
                ret.AppendLine("Agent locations:");

                foreach (var location in items.Cast<MapAgentLocation>())
                {
                    ret.AppendLine($"{location.AvatarCount} avatar(s) at {location.LocalX},{location.LocalY}");
                }

                return ret.ToString();
            }
            else
            {
                return "Failed to fetch agent locations";
            }
        }
    }
}
