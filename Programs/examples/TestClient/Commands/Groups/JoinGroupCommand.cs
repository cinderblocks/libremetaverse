using System;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class JoinGroupCommand : Command
    {
        ManualResetEvent GetGroupsSearchEvent = new ManualResetEvent(false);
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
                foreach (var arg in args)
                    groupName += arg + " ";

                groupName = groupName.Trim();

                Client.Directory.DirGroupsReply += Directory_DirGroups;
                                
                queryID = Client.Directory.StartGroupSearch(groupName, 0);

                GetGroupsSearchEvent.WaitOne(TimeSpan.FromMinutes(1), false);

                Client.Directory.DirGroupsReply -= Directory_DirGroups;

                GetGroupsSearchEvent.Reset();
            }

            if (resolvedGroupID == UUID.Zero)
            {
                if (string.IsNullOrEmpty(resolvedGroupName))
                    return "Unable to obtain UUID for group " + groupName;
                else
                    return resolvedGroupName;
            }
            
            Client.Groups.GroupJoinedReply += Groups_OnGroupJoined;
            Client.Groups.RequestJoinGroup(resolvedGroupID);

            /* A.Biondi 
             * TODO: implement the pay to join procedure.
             */

            GetGroupsSearchEvent.WaitOne(TimeSpan.FromMinutes(1), false);

            Client.Groups.GroupJoinedReply -= Groups_GroupJoined;
            GetGroupsSearchEvent.Reset();
            Client.ReloadGroupsCache();

            if (joinedGroup)
                return "Joined the group " + resolvedGroupName;
            return "Unable to join the group " + resolvedGroupName;
        }

        void Groups_GroupJoined(object sender, GroupOperationEventArgs e)
        {
            throw new NotImplementedException();
        }

        void Directory_DirGroups(object sender, DirGroupsReplyEventArgs e)
        {
            if (queryID == e.QueryID)
            {
                queryID = UUID.Zero;
                if (e.MatchedGroups.Count < 1)
                {
                    Console.WriteLine("ERROR: Got an empty reply");
                }
                else
                {
                    if (e.MatchedGroups.Count > 1)
                    {
                        /* A.Biondi 
                         * The Group search doesn't work as someone could expect...
                         * It'll give back to you a long list of groups even if the 
                         * searchText (groupName) matches esactly one of the groups 
                         * names present on the server, so we need to check each result.
                         * UUIDs of the matching groups are written on the console.
                         */
                        Console.WriteLine("Matching groups are:\n");
                        foreach (DirectoryManager.GroupSearchData groupRetrieved in e.MatchedGroups)
                        {
                            Console.WriteLine(groupRetrieved.GroupName + "\t\t\t(" +
                                Name + " UUID " + groupRetrieved.GroupID + ")");

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
                GetGroupsSearchEvent.Set();
            }
        }

        void Groups_OnGroupJoined(object sender, GroupOperationEventArgs e)
        {
            Console.WriteLine(Client + (e.Success ? " joined " : " failed to join ") + e.GroupID);

            /* A.Biondi 
             * This code is not necessary because it is yet present in the 
             * GroupCommand.cs as well. So the new group will be activated by 
             * the mentioned command. If the GroupCommand.cs would change, 
             * just uncomment the following two lines.
                
            if (success)
            {
                Console.WriteLine(Client.ToString() + " setting " + groupID.ToString() + " as the active group");
                Client.Groups.ActivateGroup(groupID);
            }
                
            */

            joinedGroup = e.Success;
            GetGroupsSearchEvent.Set();
        }                        
    }
}
