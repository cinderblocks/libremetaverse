using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Estate
{
    /// <summary>
    /// Request the raw terrain file from the simulator, save it as a file.
    /// 
    /// Can only be used by the Estate Owner
    /// </summary>
    public class DownloadTerrainCommand : Command
    {
        /// <summary>A string we use to report the result of the request with.</summary>
        private static global::System.Text.StringBuilder result = new global::System.Text.StringBuilder();

        private static string fileName;

        /// <summary>
        /// Download a simulators raw terrain data and save it to a file
        /// </summary>
        /// <param name="testClient"></param>
        public DownloadTerrainCommand(TestClient testClient)
        {
            Name = "downloadterrain";
            Description = "Download the RAW terrain file for this estate. Usage: downloadterrain [timeout]";
            Category = CommandCategory.Simulator;
        }

        /// <summary>
        /// Execute the application
        /// </summary>
        /// <param name="args">arguments passed to this module</param>
        /// <param name="fromAgentID">The ID of the avatar sending the request</param>
        /// <returns></returns>
        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            int timeout = 120000; // default the timeout to 2 minutes
            fileName = Client.Network.CurrentSim.Name + ".raw";

            if (args.Length > 0 && int.TryParse(args[0], out timeout) != true)
                return "Usage: downloadterrain [timeout]";

            result.Clear();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Create a delegate which will be fired when the simulator receives our download request
            // Starts the actual transfer request
            EventHandler<InitiateDownloadEventArgs> initiateDownloadDelegate =
                delegate(object sender, InitiateDownloadEventArgs e)
                {
                    Client.Assets.RequestAssetXfer(e.SimFileName, false, false, UUID.Zero, AssetType.Unknown, false);
                };

            // Subscribe to the event that will tell us the status of the download
            EventHandler<XferReceivedEventArgs> xferHandler = null;
            xferHandler = (s, e) =>
            {
                if (e.Xfer.Success)
                {
                    // set the result message
                    result.AppendFormat("Terrain file {0} ({1} bytes) downloaded successfully, written to {2}", e.Xfer.Filename, e.Xfer.Size, fileName);

                    // write the file to disk
                    FileStream stream = new FileStream(fileName, FileMode.Create);
                    BinaryWriter w = new BinaryWriter(stream);
                    w.Write(e.Xfer.AssetData);
                    w.Close();

                    tcs.TrySetResult(true);
                }
            };

            try
            {
                Client.Assets.InitiateDownload += initiateDownloadDelegate;
                Client.Assets.XferReceived += xferHandler;

                // configure request to tell the simulator to send us the file
                List<string> parameters = new List<string>
                {
                    "download filename",
                    fileName
                };
                // send the request
                Client.Estate.EstateOwnerMessage("terrain", parameters);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    return "Timeout while waiting for terrain data";
                }

                return result.ToString();
            }
            finally
            {
                Client.Assets.InitiateDownload -= initiateDownloadDelegate;
                Client.Assets.XferReceived -= xferHandler;
            }
        }
    }
}
