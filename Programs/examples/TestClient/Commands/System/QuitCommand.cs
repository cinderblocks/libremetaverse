using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class QuitCommand: Command
    {
        public QuitCommand(TestClient testClient)
		{
			Name = "quit";
			Description = "Log all avatars out and shut down";
            Category = CommandCategory.TestClient;
		}

        public override string Execute(string[] args, UUID fromAgentID)
		{
            // This is a dummy command. Calls to it should be intercepted and handled specially
            return "This command should not be executed directly";
		}
    }
}
