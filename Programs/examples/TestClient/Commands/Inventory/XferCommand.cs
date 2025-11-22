using System;
using System.IO;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class XferCommand : Command
    {
        const int FETCH_ASSET_TIMEOUT = 1000 * 10;

        public XferCommand(TestClient testClient)
        {
            Name = "xfer";
            Description = "Downloads the specified asset using the Xfer system. Usage: xfer [uuid]";
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1 || !UUID.TryParse(args[0], out var assetID))
                return "Usage: xfer [uuid]";

            string filename;
            var assetData = await RequestXferAsync(assetID, AssetType.Object).ConfigureAwait(false);

            if (assetData != null)
            {
                try
                {
                    filename = assetID + ".asset";
                    File.WriteAllBytes(filename, assetData);
                    return "Saved asset " + filename;
                }
                catch (Exception ex)
                {
                    return "Failed to save asset " + assetID + ": " + ex.Message;
                }
            }
            else
            {
                return "Failed to xfer asset " + assetID;
            }
        }

        private Task<byte[]> RequestXferAsync(UUID assetID, AssetType type)
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            ulong xferID = 0;

            EventHandler<XferReceivedEventArgs> xferCallback = null;
            xferCallback = (sender, e) =>
            {
                if (e.Xfer.XferID == xferID)
                {
                    if (e.Xfer.Success)
                        tcs.TrySetResult(e.Xfer.AssetData);
                    else
                        tcs.TrySetResult(null);
                }
            };

            Client.Assets.XferReceived += xferCallback;

            try
            {
                var filename = assetID + ".asset";
                xferID = Client.Assets.RequestAssetXfer(filename, false, true, assetID, type, false);

                var completed = Task.WhenAny(tcs.Task, Task.Delay(FETCH_ASSET_TIMEOUT)).GetAwaiter().GetResult();
                return completed == tcs.Task ? tcs.Task : Task.FromResult<byte[]>(null);
            }
            finally
            {
                Client.Assets.XferReceived -= xferCallback;
            }
        }
    }
}
