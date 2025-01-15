using System;
using System.Linq;
using System.Threading;
using OpenMetaverse.Packets;

namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Changes Avatars currently active group
    /// </summary>
    public class ActivateGroupCommand : Command
    {
        private ManualResetEvent GroupsEvent = new ManualResetEvent(false);
        string activeGroup;

        public ActivateGroupCommand(TestClient testClient)
        {
            Name = "activategroup";
            Description = "Set a group as active. Usage: activategroup GroupName";
            Category = CommandCategory.Groups;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return Description;

            activeGroup = string.Empty;

            string groupName = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            groupName = groupName.Trim();

            UUID groupUUID = Client.GroupName2UUID(groupName);
            if (UUID.Zero != groupUUID) {
                EventHandler<PacketReceivedEventArgs> pcallback = AgentDataUpdateHandler;
                Client.Network.RegisterCallback(PacketType.AgentDataUpdate, pcallback);

                Console.WriteLine("setting " + groupName + " as active group");
                Client.Groups.ActivateGroup(groupUUID);
                GroupsEvent.WaitOne(TimeSpan.FromSeconds(30), false);

                Client.Network.UnregisterCallback(PacketType.AgentDataUpdate, pcallback);
                GroupsEvent.Reset();

                /* A.Biondi 
                 * TODO: Handle titles choosing.
                 */

                if (string.IsNullOrEmpty(activeGroup))
                    return Client + " failed to activate the group " + groupName;

                return "Active group is now " + activeGroup;
            }
            return Client + " doesn't seem to be member of the group " + groupName;
        }

        private void AgentDataUpdateHandler(object sender, PacketReceivedEventArgs e)
        {
            AgentDataUpdatePacket p = (AgentDataUpdatePacket)e.Packet;
            if (p.AgentData.AgentID == Client.Self.AgentID)
            {
                activeGroup = Utils.BytesToString(p.AgentData.GroupName) + " ( " + Utils.BytesToString(p.AgentData.GroupTitle) + " )";
                GroupsEvent.Set();
            }
        }
    }
}
