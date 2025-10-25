using OpenMetaverse;
using OpenMetaverse.Packets;

namespace TestClient.Commands.System
{
    public class SleepCommand : Command
    {
        uint sleepSerialNum = 1;

        public SleepCommand(TestClient testClient)
        {
            Name = "sleep";
            Description = "Uses AgentPause/AgentResume and sleeps for a given number of seconds. Usage: sleep [seconds]";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            int seconds;
            if (args.Length != 1 || !int.TryParse(args[0], out seconds))
                return "Usage: sleep [seconds]";

            AgentPausePacket pause = new AgentPausePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    SerialNum = sleepSerialNum++
                }
            };

            Client.Network.SendPacket(pause);

            // Sleep
            global::System.Threading.Thread.Sleep(seconds * 1000);

            AgentResumePacket resume = new AgentResumePacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID,
                    SerialNum = pause.AgentData.SerialNum
                }
            };

            Client.Network.SendPacket(resume);

            return "Paused, slept for " + seconds + " second(s), and resumed";
        }
    }
}
