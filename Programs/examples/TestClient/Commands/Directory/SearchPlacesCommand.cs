﻿using System;
using System.Linq;
using System.Text;

namespace OpenMetaverse.TestClient.Commands
{
    class SearchPlacesCommand : Command
    {
        System.Threading.AutoResetEvent waitQuery = new System.Threading.AutoResetEvent(false);

        public SearchPlacesCommand(TestClient testClient)
        {
            Name = "searchplaces";
            Description = "Searches Places. Usage: searchplaces [search text]";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: searchplaces [search text]";

            string searchText = args.Aggregate(string.Empty, (current, t) => current + (t + " "));
            searchText = searchText.TrimEnd();
            waitQuery.Reset();

            StringBuilder result = new StringBuilder();
         
            EventHandler<PlacesReplyEventArgs> callback = delegate(object sender, PlacesReplyEventArgs e)
            {
                result.AppendFormat("Your search string '{0}' returned {1} results" + System.Environment.NewLine,
                    searchText, e.MatchedPlaces.Count);
                foreach (DirectoryManager.PlacesSearchData place in e.MatchedPlaces)
                {
                    result.AppendLine(place.ToString());
                }

                waitQuery.Set();
            };

            Client.Directory.PlacesReply += callback;
            Client.Directory.StartPlacesSearch(searchText);            

            if (!waitQuery.WaitOne(TimeSpan.FromSeconds(20), false) && Client.Network.Connected)
            {
                result.AppendLine("Timeout waiting for simulator to respond to query.");
            }

            Client.Directory.PlacesReply -= callback;

            return result.ToString();
        }        
    }
}
