using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class EmptyTrashCommand : Command
    {
        /// <summary>
        /// TestClient command to download and display a notecard asset
        /// </summary>
        /// <param name="testClient"></param>
        public EmptyTrashCommand(TestClient testClient)
        {
            Name = "emptytrash";
            Description = "Empty inventory Trash folder";
            Category = CommandCategory.Inventory;
        }

        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="args"></param>
        /// <param name="fromAgentID"></param>
        /// <returns></returns>
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            try
            {
                await Client.Inventory.EmptyTrashAsync();
                return "Trash Emptied";
            }
            catch
            {
                return "Failed to empty trash";
            }
        }
    }
}
