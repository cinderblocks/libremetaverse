using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class LogoutCommand : Command
    {
        public LogoutCommand(TestClient testClient)
        {
            Name = "logout";
            Description = "Log this avatar out";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            string name = Client.ToString();
			Client.ClientManager.Logout(Client);
            return "Logged " + name + " out";
        }
    }
}
