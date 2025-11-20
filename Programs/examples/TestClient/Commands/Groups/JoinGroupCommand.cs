using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Groups
{
    public class JoinGroupCommand : Command
    {
        private UUID queryID = UUID.Zero;
        private UUID resolvedGroupID;
        private string groupName;
        private string resolvedGroupName;
        private bool joinedGroup;

        public JoinGroupCommand(TestClient testClient)
        {
            Name = "joingroup";
            Description = "join a group. Usage: joingroup GroupName | joingroup UUID GroupId";
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

            groupName = string.Empty;
            resolvedGroupID = UUID.Zero;
            resolvedGroupName = string.Empty;

            if (args[0].ToLower() == "uuid")
            {
                if (args.Length < 2)
                    return Description;

                if (!UUID.TryParse((resolvedGroupName = groupName = args[1]), out resolvedGroupID))
                    return resolvedGroupName + " doesn't seem a valid UUID";
            }
            else
            {
                groupName = string.Join(" ", args).Trim();

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                EventHandler<DirGroupsReplyEventArgs> handler = null;
                handler = (sender, e) =>
                {
                    if (e.QueryID == queryID)
                    {
                        if (e.MatchedGroups.Count < 1)
                        {
                            Console.WriteLine("ERROR: Got an empty reply");
                        }
                        else
                        {
                            if (e.MatchedGroups.Count > 1)
                            {
                                Console.WriteLine("Matching groups are:\n");
                                foreach (var groupRetrieved in e.MatchedGroups)
                                {
                                    Console.WriteLine(groupRetrieved.GroupName + "\t\t\t(" + groupRetrieved.GroupID + ")");
                                    if (string.Equals(groupRetrieved.GroupName, groupName, StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        resolvedGroupID = groupRetrieved.GroupID;
                                        resolvedGroupName = groupRetrieved.GroupName;
                                        break;
                                    }
                                }
                                if (string.IsNullOrEmpty(resolvedGroupName))
                                    resolvedGroupName = "Ambiguous name. Found " + e.MatchedGroups.Count + " groups (UUIDs on console)";
                            }
                        }

                        tcs.TrySetResult(true);
                    }
                };

                try
                {
                    Client.Directory.DirGroupsReply += handler;
                    queryID = Client.Directory.StartGroupSearch(groupName, 0);

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(1))).ConfigureAwait(false);
                    if (completed != tcs.Task)
                    {
                        return "Timeout waiting for group search";
                    }
                }
                finally
                {
                    Client.Directory.DirGroupsReply -= handler;
                }
            }

            if (resolvedGroupID == UUID.Zero)
            {
                if (string.IsNullOrEmpty(resolvedGroupName))
                    return "Unable to obtain UUID for group " + groupName;
                else
                    return resolvedGroupName;
            }

            var joinTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<GroupOperationEventArgs> joinHandler = null;
            joinHandler = (sender, e) =>
            {
                Console.WriteLine(Client + (e.Success ? " joined " : " failed to join ") + e.GroupID);
                joinedGroup = e.Success;
                joinTcs.TrySetResult(true);
            };

            try
            {
                Client.Groups.GroupJoinedReply += joinHandler;
                Client.Groups.RequestJoinGroup(resolvedGroupID);

                var completed = await Task.WhenAny(joinTcs.Task, Task.Delay(TimeSpan.FromMinutes(1))).ConfigureAwait(false);
                if (completed != joinTcs.Task)
                    return "Timeout waiting to join group";
            }
            finally
            {
                Client.Groups.GroupJoinedReply -= joinHandler;
            }

            Client.ReloadGroupsCache();

            if (joinedGroup)
                return "Joined the group " + resolvedGroupName;
            return "Unable to join the group " + resolvedGroupName;
        }
    }
}
