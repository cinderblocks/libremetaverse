using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.System
{
    public class SetMasterCommand: Command
    {
        public DateTime Created = DateTime.Now;
        private UUID resolvedMasterKey = UUID.Zero;
        private UUID query = UUID.Zero;

        public SetMasterCommand(TestClient testClient)
        {
            Name = "setmaster";
            Description = "Sets the user name of the master user. The master user can IM to run commands. Usage: setmaster [name]";
            Category = CommandCategory.TestClient;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            string masterName = string.Join(" ", args).TrimEnd();

            if (masterName.Length == 0)
                return "Usage: setmaster [name]";

            var tcs = new TaskCompletionSource<UUID>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<DirPeopleReplyEventArgs> callback = null;
            callback = (sender, e) =>
            {
                if (query != e.QueryID) return;
                if (e.MatchedPeople != null && e.MatchedPeople.Count > 0)
                {
                    var id = e.MatchedPeople[0].AgentID;
                    tcs.TrySetResult(id);
                }
                else
                {
                    tcs.TrySetResult(UUID.Zero);
                }
            };

            try
            {
                Client.Directory.DirPeopleReply += callback;

                query = Client.Directory.StartPeopleSearch(masterName, 0);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(1))).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    return "Unable to obtain UUID for \"" + masterName + "\". Master unchanged.";
                }

                resolvedMasterKey = await tcs.Task.ConfigureAwait(false);

                if (resolvedMasterKey == UUID.Zero)
                    return "Unable to obtain UUID for \"" + masterName + "\". Master unchanged.";

                Client.MasterKey = resolvedMasterKey;
            }
            finally
            {
                Client.Directory.DirPeopleReply -= callback;
                query = UUID.Zero;
            }

            // Send an Online-only IM to the new master
            Client.Self.InstantMessage(
                Client.MasterKey, "You are now my master.  IM me with \"help\" for a command list.");

            return $"Master set to {masterName} ({Client.MasterKey.ToString()})";
        }
    }
}
