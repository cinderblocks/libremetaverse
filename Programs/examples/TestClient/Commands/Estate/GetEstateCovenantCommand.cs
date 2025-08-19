using System;
using System.Text;
using System.Threading;
using OpenMetaverse.Assets;

namespace OpenMetaverse.TestClient
{
    public class GetEstateCovenantCommand : Command
    {
        private readonly AutoResetEvent waitEvent = new AutoResetEvent(false);
        private static StringBuilder result = new StringBuilder();
        
        public GetEstateCovenantCommand(TestClient client)
        {
            Name = "getestatecovenant";
            Description = "Retrieve estate covenant information. Usage: getestatecovenant [timeout in seconds]";
            Category = CommandCategory.Simulator;
        }
        
        public override string Execute(string[] args, UUID fromAgentID)
        {
            var timeout = 20;
            if (args.Length > 0 && int.TryParse(args[0], out timeout) != true)
                return "Usage: getestatecovenant [timeout]";

            Client.Estate.EstateCovenantReply += CovenantReceived;
            
            Client.Estate.RequestCovenant();
            
            // wait for reply or timeout
            if (!waitEvent.WaitOne(TimeSpan.FromSeconds(timeout), false))
            {
                result.Append("Timeout waiting for covenant info.");
            }

            Client.Estate.EstateCovenantReply -= CovenantReceived;

            return result.ToString();
        }

        private void CovenantReceived(object sender, EstateCovenantReplyEventArgs e)
        {
            result.AppendFormat("Estate name: {0}\nEstate owner: {1}\n", 
                e.EstateName, e.EstateOwnerID);
            if (e.CovenantID == UUID.Zero)
            {
                waitEvent.Set();
            }
            else
            {
                result.AppendFormat("Estate Covenant ID: {0}\nEstate Covenant Update Time: {1}\n", 
                    e.CovenantID, Utils.UnixTimeToDateTime(Convert.ToInt32(e.Timestamp)));
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

                    waitEvent.Set();
                });
            }
        }
    }
}