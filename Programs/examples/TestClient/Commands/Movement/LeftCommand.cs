using System;

namespace OpenMetaverse.TestClient.Commands.Movement
{
    class LeftCommand : Command
    {
        public LeftCommand(TestClient client)
        {
            Name = "left";
            Description = "Sends the move left command to the server for a single packet or a given number of seconds. Usage: left [seconds]";
            Category = CommandCategory.Movement;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length > 1)
                return "Usage: left [seconds]";

            if (args.Length == 0)
            {
                Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis, Client.Self.Movement.Camera.UpAxis,
                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation, Client.Self.Movement.Camera.Far, AgentFlags.None,
                    AgentState.None, true);
            }
            else
            {
                // Parse the number of seconds
                int duration;
                if (!int.TryParse(args[0], out duration))
                    return "Usage: left [seconds]";
                // Convert to milliseconds
                duration *= 1000;

                int start = Environment.TickCount;

                Client.Self.Movement.LeftPos = true;

                while (Environment.TickCount - start < duration)
                {
                    // The movement timer will do this automatically, but we do it here as an example
                    // and to make sure updates are being sent out fast enough
                    Client.Self.Movement.SendUpdate(false);
                    System.Threading.Thread.Sleep(100);
                }

                Client.Self.Movement.LeftPos = false;
            }

            return "Moved left";
        }
    }
}
