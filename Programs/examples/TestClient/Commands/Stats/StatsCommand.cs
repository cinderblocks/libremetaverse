using System;
using System.Text;

namespace OpenMetaverse.TestClient
{
    public class StatsCommand : Command
    {
        public StatsCommand(TestClient testClient)
        {
            Name = "stats";
            Description = "Provide connection figures and statistics";
            Category = CommandCategory.Simulator;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            StringBuilder output = new StringBuilder();

            lock (Client.Network.Simulators)
            {
                foreach (var sim in Client.Network.Simulators)
                {
                    output.AppendLine(
                        $"[{sim}] Dilation: {sim.Stats.Dilation} InBPS: {sim.Stats.IncomingBPS} OutBPS: {sim.Stats.OutgoingBPS} ResentOut: {sim.Stats.ResentPackets}  ResentIn: {sim.Stats.ReceivedResends}");
                }
            }

            Simulator csim = Client.Network.CurrentSim;

            output.Append("Packets in the queue: " + Client.Network.InboxCount);
			output.AppendLine(
                $"FPS : {csim.Stats.FPS} PhysicsFPS : {csim.Stats.PhysicsFPS} AgentUpdates : {csim.Stats.AgentUpdates} Objects : {csim.Stats.Objects} Scripted Objects : {csim.Stats.ScriptedObjects}");
			output.AppendLine(
                $"Frame Time : {csim.Stats.FrameTime} Net Time : {csim.Stats.NetTime} Image Time : {csim.Stats.ImageTime} Physics Time : {csim.Stats.PhysicsTime} Script Time : {csim.Stats.ScriptTime} Other Time : {csim.Stats.OtherTime}");
			output.AppendLine(
                $"Agents : {csim.Stats.Agents} Child Agents : {csim.Stats.ChildAgents} Active Scripts : {csim.Stats.ActiveScripts}");

            return output.ToString();
        }
    }
}
