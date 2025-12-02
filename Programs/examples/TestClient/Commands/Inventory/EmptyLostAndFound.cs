using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class EmptyLostAndCommand : Command
    {
        /// <summary>
        /// TestClient command to download and display a notecard asset
        /// </summary>
        /// <param name="testClient"></param>
        public EmptyLostAndCommand(TestClient testClient)
        {
            Name = "emptylostandfound";
            Description = "Empty inventory Lost And Found folder";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public async override Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            try
            {
                await Client.Inventory.EmptyLostAndFoundAsync();
                return "Lost And Found Emptied";
            }
            catch
            {
                return "Failed to empty Lost And Found";
            }
        }
    }
}
