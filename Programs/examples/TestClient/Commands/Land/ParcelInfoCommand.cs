using System;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    public class ParcelInfoCommand : Command
    {
        public ParcelInfoCommand(TestClient testClient)
        {
            Name = "parcelinfo";
            Description = "Prints out info about all the parcels in this simulator";
            Category = CommandCategory.Parcel;

            testClient.Network.Disconnected += Network_OnDisconnected;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            StringBuilder sb = new StringBuilder();
            string result;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<SimParcelsDownloadedEventArgs> handler = null;
            handler = (sender, e) => tcs.TrySetResult(true);

            Client.Parcels.SimParcelsDownloaded += handler;
            await Client.Parcels.RequestAllSimParcelsAsync(Client.Network.CurrentSim);

            if (Client.Network.CurrentSim.IsParcelMapFull())
                tcs.TrySetResult(true);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
            if (completed == tcs.Task && Client.Network.Connected)
            {
                sb.AppendFormat("Downloaded {0} Parcels in {1} " + global::System.Environment.NewLine,
                    Client.Network.CurrentSim.Parcels.Count, Client.Network.CurrentSim.Name);

                Client.Network.CurrentSim.Parcels.ForEach(delegate (Parcel parcel)
                {
                    sb.AppendFormat("Parcel[{0}]: Name: \"{1}\", Description: \"{2}\" ACLBlacklist Count: {3}, ACLWhiteList Count: {5} Traffic: {4}" + global::System.Environment.NewLine,
                        parcel.LocalID, parcel.Name, parcel.Desc, parcel.AccessBlackList.Count, parcel.Dwell, parcel.AccessWhiteList.Count);
                });

                result = sb.ToString();
            }
            else
                result = "Failed to retrieve information on all the simulator parcels";

            Client.Parcels.SimParcelsDownloaded -= handler;
            return result;
        }

        void Network_OnDisconnected(object sender, DisconnectedEventArgs e)
        {
            // best effort to unblock any waiting task
        }
    }
}
