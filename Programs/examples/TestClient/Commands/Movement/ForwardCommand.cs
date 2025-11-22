using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Movement
{
    class ForwardCommand : Command
    {
        public ForwardCommand(TestClient client)
        {
            Name = "forward";
            Description = "Sends the move forward command to the server for a single packet or a given number of seconds. Usage: forward [seconds]";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length > 1)
                return "Usage: forward [seconds]";

            if (args.Length == 0)
            {
                Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_AT_POS, Client.Self.Movement.Camera.Position,
                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis, Client.Self.Movement.Camera.UpAxis,
                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation, Client.Self.Movement.Camera.Far, AgentFlags.None,
                    AgentState.None, true);
            }
            else
            {
                // Parse the number of seconds
                if (!int.TryParse(args[0], out var duration))
                    return "Usage: forward [seconds]";

                int ms = duration * 1000;
                int start = Environment.TickCount;

                Client.Self.Movement.AtPos = true;

                try
                {
                    while (Environment.TickCount - start < ms)
                    {
                        Client.Self.Movement.SendUpdate(false);
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Client.Self.Movement.AtPos = false;
                }
            }

            return "Moved forward";
        }
    }
}
