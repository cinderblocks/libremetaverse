using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Estate
{
    public class GetEstateCovenantCommand : Command
    {
        private static StringBuilder result = new StringBuilder();

        public GetEstateCovenantCommand(TestClient client)
        {
            Name = "getestatecovenant";
            Description = "Retrieve estate covenant information. Usage: getestatecovenant [timeout in seconds]";
            Category = CommandCategory.Simulator;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            var timeout = 20;
            if (args.Length > 0 && int.TryParse(args[0], out timeout) != true)
                return "Usage: getestatecovenant [timeout]";

            result.Clear();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<EstateCovenantReplyEventArgs> handler = null;
            handler = (s, e) =>
            {
                result.AppendFormat("Estate name: {0}\nEstate owner: {1}\n", e.EstateName, e.EstateOwnerID);
                if (e.CovenantID == UUID.Zero)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    result.AppendFormat("Estate Covenant ID: {0}\nEstate Covenant Update Time: {1}\n", e.CovenantID, Utils.UnixTimeToDateTime(Convert.ToInt32(e.Timestamp)));

                    Client.Estate.RequestCovenantNotecard(e.CovenantID, (transfer, asset) =>
                    {
                        if (transfer.Success)
                        {
                            result.AppendFormat("Covenant:\n{0}", Utils.BytesToString(asset.AssetData));
                        }
                        else
                        {
                            result.Append("Could not retrieve covenant notecard.");
                        }

                        tcs.TrySetResult(true);
                    });
                }
            };

            try
            {
                Client.Estate.EstateCovenantReply += handler;

                Client.Estate.RequestCovenant();

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeout))).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    return "Timeout waiting for covenant info.";
                }

                return result.ToString();
            }
            finally
            {
                Client.Estate.EstateCovenantReply -= handler;
            }
        }
    }
}