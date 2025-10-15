using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class JumpCommand: Command
    {
        public JumpCommand(TestClient testClient)
		{
			Name = "jump";
			Description = "Jumps or flies up";
            Category = CommandCategory.Movement;
		}

        public override string Execute(string[] args, UUID fromAgentID)
		{
            Client.Self.Jump(true);
            return "Jumped";
		}
    }
}
