using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Groups
{
    /// <summary>
    /// dumps group members to console
    /// </summary>
    public class GroupMembersCommand : Command
    {
        private string GroupName;
        private UUID GroupUUID;
        private UUID GroupRequestID;

        public GroupMembersCommand(TestClient testClient)
        {
            Name = "groupmembers";
            Description = "Dump group members to console. Usage: groupmembers GroupName";
            Category = CommandCategory.Groups;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return Description;

            GroupName = string.Join(" ", args).Trim();

            GroupUUID = await Client.GroupName2UUIDAsync(GroupName).ConfigureAwait(false);
            if (UUID.Zero != GroupUUID)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                EventHandler<GroupMembersReplyEventArgs> handler = null;
                handler = (sender, e) =>
                {
                    if (e.RequestID == GroupRequestID)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine();
                        sb.AppendFormat("GroupMembers: RequestID {0}", e.RequestID).AppendLine();
                        sb.AppendFormat("GroupMembers: GroupUUID {0}", GroupUUID).AppendLine();
                        sb.AppendFormat("GroupMembers: GroupName {0}", GroupName).AppendLine();
                        if (e.Members.Count > 0)
                            foreach (KeyValuePair<UUID, GroupMember> member in e.Members)
                                sb.AppendFormat("GroupMembers: MemberUUID {0}", member.Key.ToString()).AppendLine();
                        sb.AppendFormat("GroupMembers: MemberCount {0}", e.Members.Count).AppendLine();
                        Console.WriteLine(sb.ToString());
                        tcs.TrySetResult(true);
                    }
                };

                try
                {
                    Client.Groups.GroupMembersReply += handler;
                    GroupRequestID = Client.Groups.RequestGroupMembers(GroupUUID);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                    if (completed != tcs.Task)
                        return "Timeout waiting for group members";

                    return Client + " got group members";
                }
                finally
                {
                    Client.Groups.GroupMembersReply -= handler;
                }
            }

            return Client + " doesn't seem to be member of the group " + GroupName;
        }
    }
}
