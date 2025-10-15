using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Agent
{
    public class BotsCommand : Command
    {
        private Dictionary<UUID, bool> m_AgentList = new Dictionary<UUID, bool>();

        public BotsCommand(TestClient testClient)
        {
            Name = "bots";
            Description = "detects avatars that appear to be bots.";
            Category = CommandCategory.Other;        
            testClient.Avatars.ViewerEffect += Avatars_ViewerEffect;
            testClient.Avatars.ViewerEffectLookAt += Avatars_ViewerEffectLookAt;
            testClient.Avatars.ViewerEffectPointAt += Avatars_ViewerEffectPointAt;
        }
        
        private void Avatars_ViewerEffectPointAt(object sender, ViewerEffectPointAtEventArgs e)
        {
            lock (m_AgentList)
            {
                m_AgentList[e.SourceID] = true;
            }
        }

        private void Avatars_ViewerEffectLookAt(object sender, ViewerEffectLookAtEventArgs e)
        {
            lock (m_AgentList)
            {
                m_AgentList[e.SourceID] = true;
            }
        }

        private void Avatars_ViewerEffect(object sender, ViewerEffectEventArgs e)
        {
            lock (m_AgentList)
            {
                m_AgentList[e.SourceID] = true;
            }
        }
        
        public override string Execute(string[] args, UUID fromAgentID)
        {
            StringBuilder result = new StringBuilder();

            lock (Client.Network.Simulators)
            {
                foreach (var av in from sim in Client.Network.Simulators 
                         from kvp in sim.ObjectsAvatars 
                         where kvp.Value != null select kvp.Value into av 
                         where !m_AgentList.ContainsKey(av.ID) select av)
                {
                    result.AppendLine();
                    result.AppendFormat("{0} (Group: {1}, Location: {2}, UUID: {3} LocalID: {4}) Is Probably a bot",
                        av.Name, av.GroupName, av.Position, av.ID, av.LocalID);
                }
            }

            return result.ToString();
        }
    }
}
