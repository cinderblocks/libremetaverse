using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class SearchPlacesCommand : Command
    {
        public SearchPlacesCommand(TestClient testClient)
        {
            Name = "searchplaces";
            Description = "Searches Places. Usage: searchplaces [search text]";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: searchplaces [search text]";

            string searchText = string.Join(" ", args).TrimEnd();

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<PlacesReplyEventArgs> callback = null;
            callback = (object sender, PlacesReplyEventArgs e) =>
            {
                var result = new StringBuilder();
                result.AppendFormat("Your search string '{0}' returned {1} results" + global::System.Environment.NewLine,
                    searchText, e.MatchedPlaces.Count);
                foreach (DirectoryManager.PlacesSearchData place in e.MatchedPlaces)
                {
                    result.AppendLine(place.ToString());
                }

                tcs.TrySetResult(result.ToString());
            };

            try
            {
                Client.Directory.PlacesReply += callback;
                Client.Directory.StartPlacesSearch(searchText);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
                if (completed != tcs.Task && Client.Network.Connected)
                {
                    return "Timeout waiting for simulator to respond to query.";
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                Client.Directory.PlacesReply -= callback;
            }
        }
    }
}
