using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class LoginCommand : Command
    {
        public LoginCommand(TestClient testClient)
        {
            Name = "login";
            Description = "Logs in another avatar. Usage: login firstname lastname password [simname] [loginuri]";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            // This is a dummy command. Calls to it should be intercepted and handled specially
            return "This command should not be executed directly";
        }
    }
}
