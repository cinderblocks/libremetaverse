using System;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace TestClient.Commands.Groups
{
    public class LeaveGroupCommand : Command
    {
        ManualResetEvent GroupsEvent = new ManualResetEvent(false);
        private bool leftGroup;

        public LeaveGroupCommand(TestClient testClient)
        {
            Name = "leavegroup";
            Description = "Leave a group. Usage: leavegroup GroupName";
            Category = CommandCategory.Groups;
        }
        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return Description;

            string groupName = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            groupName = groupName.Trim();

            UUID groupUUID = Client.GroupName2UUID(groupName);
            if (UUID.Zero != groupUUID) {                
                Client.Groups.GroupLeaveReply += Groups_GroupLeft;
                Client.Groups.LeaveGroup(groupUUID);

                GroupsEvent.WaitOne(TimeSpan.FromSeconds(30), false);
                Client.Groups.GroupLeaveReply -= Groups_GroupLeft;

                GroupsEvent.Reset();
                Client.ReloadGroupsCache();

                if (leftGroup)
                    return Client + " has left the group " + groupName;
                return "failed to leave the group " + groupName;
            }
            return Client + " doesn't seem to be member of the group " + groupName;
        }

        void Groups_GroupLeft(object sender, GroupOperationEventArgs e)
        {
            Console.WriteLine(Client + (e.Success ? " has left group " : " failed to left group ") + e.GroupID);

            leftGroup = e.Success;
            GroupsEvent.Set();
        }

    }
}
