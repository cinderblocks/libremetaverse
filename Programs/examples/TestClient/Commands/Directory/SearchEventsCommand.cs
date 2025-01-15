using System;
using System.Linq;

namespace OpenMetaverse.TestClient.Commands
{
    class SearchEventsCommand : Command
    {
        System.Threading.AutoResetEvent waitQuery = new System.Threading.AutoResetEvent(false);
        int resultCount;

        public SearchEventsCommand(TestClient testClient)
        {
            Name = "searchevents";
            Description = "Searches Events list. Usage: searchevents [search text]";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            // process command line arguments
            if (args.Length < 1)
                return "Usage: searchevents [search text]";

            string searchText = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            searchText = searchText.TrimEnd();

            waitQuery.Reset();
            
            Client.Directory.DirEventsReply += Directory_DirEvents;

            // send the request to the directory manager
            Client.Directory.StartEventsSearch(searchText, 0);
            string result;
            if (waitQuery.WaitOne(TimeSpan.FromSeconds(20), false) && Client.Network.Connected)
            {
                result =  "Your query '" + searchText + "' matched " + resultCount + " Events. ";
            }
            else
            {
                result =  "Timeout waiting for simulator to respond.";
            }

            Client.Directory.DirEventsReply -= Directory_DirEvents;
            
            return result;
        }

        void Directory_DirEvents(object sender, DirEventsReplyEventArgs e)
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
            waitQuery.Set();
        }
    }
}
