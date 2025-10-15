using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    public class CrouchCommand : Command
    {
        public CrouchCommand(TestClient testClient)
        {
            Name = "crouch";
            Description = "Starts or stops crouching. Usage: crouch [start/stop]";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            bool start = !(args.Length == 1 && args[0].ToLower() == "stop");

            if (start)
            {
                Client.Self.Crouch(true);
                return "Started crouching";
            }
            else
            {
                Client.Self.Crouch(false);
                return "Stopped crouching";
            }
        }
    }
}
