using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Groups
{
    /// <summary>
    /// dumps group roles to console
    /// </summary>
    public class GroupRolesCommand : Command
    {
        private string GroupName;
        private UUID GroupUUID;
        private UUID GroupRequestID;

        public GroupRolesCommand(TestClient testClient)
        {
            Name = "grouproles";
            Description = "Dump group roles to console. Usage: grouproles GroupName";
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

                EventHandler<GroupRolesDataReplyEventArgs> handler = null;
                handler = (sender, e) =>
                {
                    if (e.RequestID == GroupRequestID)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine();
                        sb.AppendFormat("GroupRole: RequestID {0}", e.RequestID).AppendLine();
                        sb.AppendFormat("GroupRole: GroupUUID {0}", GroupUUID).AppendLine();
                        sb.AppendFormat("GroupRole: GroupName {0}", GroupName).AppendLine();
                        if (e.Roles.Count > 0)
                            foreach (KeyValuePair<UUID, GroupRole> role in e.Roles)
                                sb.AppendFormat("GroupRole: Role {0} {1}|{2}", role.Value.ID, role.Value.Name, role.Value.Title).AppendLine();
                        sb.AppendFormat("GroupRole: RoleCount {0}", e.Roles.Count).AppendLine();
                        Console.WriteLine(sb.ToString());
                        tcs.TrySetResult(true);
                    }
                };

                try
                {
                    Client.Groups.GroupRoleDataReply += handler;
                    GroupRequestID = Client.Groups.RequestGroupRoles(GroupUUID);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                    if (completed != tcs.Task)
                        return "Timeout waiting for group roles";

                    return Client + " got group roles";
                }
                finally
                {
                    Client.Groups.GroupRoleDataReply -= handler;
                }
            }
            return Client + " doesn't seem to have any roles in the group " + GroupName;
        }
    }
}
