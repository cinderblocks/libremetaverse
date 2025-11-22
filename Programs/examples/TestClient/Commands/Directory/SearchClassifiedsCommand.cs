using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Directory
{
    class SearchClassifiedsCommand : Command
    {
        public SearchClassifiedsCommand(TestClient testClient)
        {
            Name = "searchclassifieds";
            Description = "Searches Classified Ads. Usage: searchclassifieds [search text]";
            Category = CommandCategory.Search;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: searchclassifieds [search text]";

            string searchText = string.Join(" ", args).TrimEnd();

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<DirClassifiedsReplyEventArgs> callback = null;
            callback = (sender, e) =>
            {
                var result = new StringBuilder();
                result.AppendFormat("Your search string '{0}' returned {1} classified ads" + global::System.Environment.NewLine,
                    searchText, e.Classifieds.Count);
                foreach (DirectoryManager.Classified ad in e.Classifieds)
                {
                    result.AppendLine(ad.ToString());
                }

                // classifieds are sent 16 ads at a time
                if (e.Classifieds.Count < 16)
                {
                    tcs.TrySetResult(result.ToString());
                }
            };

            try
            {
                Client.Directory.DirClassifiedsReply += callback;

                UUID searchID = Client.Directory.StartClassifiedSearch(searchText, DirectoryManager.ClassifiedCategories.Any, DirectoryManager.ClassifiedQueryFlags.Mature | DirectoryManager.ClassifiedQueryFlags.PG);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
                if (completed != tcs.Task && Client.Network.Connected)
                {
                    return "Timeout waiting for simulator to respond to query.";
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                Client.Directory.DirClassifiedsReply -= callback;
            }
        }
    }
}
