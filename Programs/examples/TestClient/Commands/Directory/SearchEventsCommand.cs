using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class SearchEventsCommand : Command
    {
        int resultCount;

        public SearchEventsCommand(TestClient testClient)
        {
            Name = "searchevents";
            Description = "Searches Events list. Usage: searchevents [search text]";
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
                return "Usage: searchevents [search text]";

            string searchText = string.Join(" ", args).TrimEnd();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<DirEventsReplyEventArgs> handler = null;
            handler = (sender, e) =>
            {
                if (e.MatchedEvents[0].ID == 0 && e.MatchedEvents.Count == 1)
                {
                    Console.WriteLine("No Results matched your search string");
                }
                else
                {
                    foreach (DirectoryManager.EventsSearchData ev in e.MatchedEvents)
                    {
                        Console.WriteLine("Event ID: {0} Event Name: {1} Event Date: {2}", ev.ID, ev.Name, ev.Date);
                    }
                }
                resultCount = e.MatchedEvents.Count;
                tcs.TrySetResult(true);
            };

            try
            {
                Client.Directory.DirEventsReply += handler;

                // send the request to the directory manager
                Client.Directory.StartEventsSearch(searchText, 0);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
                if (completed != tcs.Task && Client.Network.Connected)
                {
                    return "Timeout waiting for simulator to respond.";
                }

                return "Your query '" + searchText + "' matched " + resultCount + " Events. ";
            }
            finally
            {
                Client.Directory.DirEventsReply -= handler;
            }
        }
    }
}
