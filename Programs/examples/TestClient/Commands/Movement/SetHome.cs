using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class SetHomeCommand : Command
    {
		public SetHomeCommand(TestClient testClient)
        {
            Name = "sethome";
            Description = "Sets home to the current location.";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
			Client.Self.SetHome();
            return "Home Set";
        }
    }
}
