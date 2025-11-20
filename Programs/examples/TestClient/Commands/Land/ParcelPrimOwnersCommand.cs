using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    public class ParcelPrimOwnersCommand : Command
    {
        public ParcelPrimOwnersCommand(TestClient testClient)
        {
            Name = "primowners";
            Description = "Displays a list of prim owners and prim counts on a parcel. Usage: primowners parcelID";
            Category = CommandCategory.Parcel;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: primowners parcelID (use parcelinfo to get ID)";

            if (!int.TryParse(args[0], out var parcelID) || !Client.Network.CurrentSim.Parcels.TryGetValue(parcelID, out var parcel))
            {
                return $"Unable to find Parcel {args[0]} in Parcels Dictionary, Did you run parcelinfo to populate the dictionary first?";
            }

            var tcs = new TaskCompletionSource<ParcelObjectOwnersReplyEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<ParcelObjectOwnersReplyEventArgs> callback = null;
            callback = (sender, e) => tcs.TrySetResult(e);

            try
            {
                Client.Parcels.ParcelObjectOwnersReply += callback;
                Client.Parcels.RequestObjectOwners(Client.Network.CurrentSim, parcelID);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (completed != tcs.Task)
                    return "Timed out waiting for packet.";

                var eargs = await tcs.Task.ConfigureAwait(false);
                var result = new StringBuilder();
                for (int i = 0; i < eargs.PrimOwners.Count; i++)
                {
                    result.AppendFormat("Owner: {0} Count: {1}" + global::System.Environment.NewLine, eargs.PrimOwners[i].OwnerID, eargs.PrimOwners[i].Count);
                }

                return result.ToString();
            }
            finally
            {
                Client.Parcels.ParcelObjectOwnersReply -= callback;
            }
        }
    }
}
