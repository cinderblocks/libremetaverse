using System;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class SearchPeopleCommand : Command
    {
        int resultCount = 0;

        public SearchPeopleCommand(TestClient testClient)
        {
            Name = "searchpeople";
            Description = "Searches for other avatars. Usage: searchpeople [search text]";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            // process command line arguments
            if (args.Length < 1)
                return "Usage: searchpeople [search text]";

            string searchText = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            searchText = searchText.TrimEnd();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<DirPeopleReplyEventArgs> handler = null;
            handler = (sender, e) =>
            {
                if (e.MatchedPeople.Count > 0)
                {
                    foreach (DirectoryManager.AgentSearchData agent in e.MatchedPeople)
                    {
                        Console.WriteLine("{0} {1} ({2})", agent.FirstName, agent.LastName, agent.AgentID);
                    }
                }
                else
                {
                    Console.WriteLine("Didn't find any people that matched your query :(");
                }

                resultCount = e.MatchedPeople.Count;
                tcs.TrySetResult(true);
            };

            try
            {
                Client.Directory.DirPeopleReply += handler;

                // send the request to the directory manager
                var queryId = Client.Directory.StartPeopleSearch(searchText, 0);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);

                string result;
                if (completed == tcs.Task && Client.Network.Connected)
                {
                    result = "Your query '" + searchText + "' matched " + resultCount + " People. ";
                }
                else
                {
                    result = "Timeout waiting for simulator to respond.";
                }

                return result;
            }
            finally
            {
                Client.Directory.DirPeopleReply -= handler;
            }
        }
    }
}
