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
            while (ClientManager.Instance.PendingLogins > 0)
            {
                global::System.Threading.Thread.Sleep(1000);
                Logger.Log("Pending logins: " + ClientManager.Instance.PendingLogins, Helpers.LogLevel.Info);
            }

            return "All pending logins have completed, currently tracking " + ClientManager.Instance.Clients.Count + " bots";
        }
    }
}
