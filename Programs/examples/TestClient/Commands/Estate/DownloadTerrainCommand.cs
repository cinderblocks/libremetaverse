﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Request the raw terrain file from the simulator, save it as a file.
    /// 
    /// Can only be used by the Estate Owner
    /// </summary>
    public class DownloadTerrainCommand : Command
    {
        /// <summary>
        /// Create a Synchronization event object
        /// </summary>
        private static AutoResetEvent xferTimeout = new AutoResetEvent(false);

        /// <summary>A string we use to report the result of the request with.</summary>
        private static System.Text.StringBuilder result = new System.Text.StringBuilder();

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
            int timeout = 120000; // default the timeout to 2 minutes
            fileName = Client.Network.CurrentSim.Name + ".raw";

            if (args.Length > 0 && int.TryParse(args[0], out timeout) != true)
                return "Usage: downloadterrain [timeout]";

            // Create a delegate which will be fired when the simulator receives our download request
            // Starts the actual transfer request
            EventHandler<InitiateDownloadEventArgs> initiateDownloadDelegate =
                delegate(object sender, InitiateDownloadEventArgs e)
                {
                    Client.Assets.RequestAssetXfer(e.SimFileName, false, false, UUID.Zero, AssetType.Unknown, false);
                };

            // Subscribe to the event that will tell us the status of the download
            Client.Assets.XferReceived += Assets_XferReceived;
            // subscribe to the event which tells us when the simulator has received our request
            Client.Assets.InitiateDownload += initiateDownloadDelegate;

            // configure request to tell the simulator to send us the file
            List<string> parameters = new List<string>
            {
                "download filename",
                fileName
            };
            // send the request
            Client.Estate.EstateOwnerMessage("terrain", parameters);

            // wait for (timeout) seconds for the request to complete (defaults 2 minutes)
            if (!xferTimeout.WaitOne(timeout, false))
            {
                result.Append("Timeout while waiting for terrain data");
            }

            // unsubscribe from events
            Client.Assets.InitiateDownload -= initiateDownloadDelegate;
            Client.Assets.XferReceived -= Assets_XferReceived;

            // return the result
            return result.ToString();
        }

        /// <summary>
        /// Handle the reply to the OnXferReceived event
        /// </summary>
        private void Assets_XferReceived(object sender, XferReceivedEventArgs e)
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

                // tell the application we've gotten the file
                xferTimeout.Set();
            }
        }
    }
}
