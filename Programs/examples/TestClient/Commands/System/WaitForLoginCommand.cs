using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class WaitForLoginCommand : Command
    {
        public WaitForLoginCommand(TestClient testClient)
        {
            Name = "waitforlogin";
            Description = "Waits until all bots that are currently attempting to login have succeeded or failed";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            while (ClientManager.Instance.PendingLogins > 0)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                Logger.Info($"Pending logins: {ClientManager.Instance.PendingLogins}");
            }

            return $"All pending logins have completed, currently tracking {ClientManager.Instance.Clients.Count} bots";
        }
    }
}

