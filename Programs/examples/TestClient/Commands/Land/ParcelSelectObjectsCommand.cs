using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    public class ParcelSelectObjectsCommand : Command
    {
        public ParcelSelectObjectsCommand(TestClient testClient)
        {
            Name = "selectobjects";
            Description = "Displays a list of prim localIDs on a given parcel with a specific owner. Usage: selectobjects parcelID OwnerUUID";
            Category = CommandCategory.Parcel;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length < 2)
                return "Usage: selectobjects parcelID OwnerUUID (use parcelinfo to get ID, use parcelprimowners to get ownerUUID)";

            if (!int.TryParse(args[0], out var parcelID) || !UUID.TryParse(args[1], out var ownerUUID))
                return "Usage: selectobjects parcelID OwnerUUID (use parcelinfo to get ID, use parcelprimowners to get ownerUUID)";

            var tcs = new TaskCompletionSource<ForceSelectObjectsReplyEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<ForceSelectObjectsReplyEventArgs> callback = null;
            callback = (sender, e) =>
            {
                if (e.ObjectIDs.Count < 251)
                    tcs.TrySetResult(e);
            };

            try
            {
                Client.Parcels.ForceSelectObjectsReply += callback;
                Client.Parcels.RequestSelectObjects(parcelID, (ObjectReturnType)16, ownerUUID);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                if (completed != tcs.Task)
                    return "Timed out waiting for packet.";

                var eargs = await tcs.Task.ConfigureAwait(false);
                var result = new StringBuilder();
                int counter = 0;
                foreach (var id in eargs.ObjectIDs)
                {
                    result.Append(id + " ");
                    counter++;
                }

                result.AppendLine("Found a total of " + counter + " Objects");
                return result.ToString();
            }
            finally
            {
                Client.Parcels.ForceSelectObjectsReply -= callback;
            }
        }
    }
}
