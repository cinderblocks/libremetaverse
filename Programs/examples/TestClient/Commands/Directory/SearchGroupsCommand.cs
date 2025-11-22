using System;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class SearchGroupsCommand : Command
    {
        int resultCount = 0;

        public SearchGroupsCommand(TestClient testClient)
        {
            Name = "searchgroups";
            Description = "Searches groups. Usage: searchgroups [search text]";
            Category = CommandCategory.Groups;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            // process command line arguments
            if (args.Length < 1)
                return "Usage: searchgroups [search text]";

            string searchText = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            searchText = searchText.TrimEnd();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<DirGroupsReplyEventArgs> handler = null;
            handler = (sender, e) =>
            {
                if (e.MatchedGroups.Count > 0)
                {
                    foreach (DirectoryManager.GroupSearchData group in e.MatchedGroups)
                    {
                        Console.WriteLine("Group {1} ({0}) has {2} members", group.GroupID, group.GroupName, group.Members);
                    }
                }
                else
                {
                    Console.WriteLine("Didn't find any groups that matched your query :(");
                }

                resultCount = e.MatchedGroups.Count;
                tcs.TrySetResult(true);
            };

            try
            {
                Client.Directory.DirGroupsReply += handler;

                // send the request to the directory manager
                Client.Directory.StartGroupSearch(searchText, 0);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);

                string result;
                if (completed == tcs.Task && Client.Network.Connected)
                {
                    result = "Your query '" + searchText + "' matched " + resultCount + " Groups. ";
                }
                else
                {
                    result = "Timeout waiting for simulator to respond.";
                }

                return result;
            }
            finally
            {
                Client.Directory.DirGroupsReply -= handler;
            }
        }
    }
}
