using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.Assets;
using LibreMetaverse;

namespace TestClient.Commands.Inventory
{
    public class QueuedDownloadInfo
    {
        public UUID AssetID;
        public UUID ItemID;
        public UUID TaskID;
        public UUID OwnerID;
        public AssetType Type;
        public string FileName;
        public DateTime WhenRequested;
        public bool IsRequested;

        public QueuedDownloadInfo(string file, UUID asset, UUID item, UUID task, UUID owner, AssetType type)
        {
            FileName = file;
            AssetID = asset;
            ItemID = item;
            TaskID = task;
            OwnerID = owner;
            Type = type;
            WhenRequested = DateTime.Now;
            IsRequested = false;
        }
    }

    public class BackupCommand : Command
    {
        /// <summary>Maximum number of transfer requests to send to the server</summary>
        private const int MAX_TRANSFERS = 10;

        // all items here, fed by the inventory walking thread
        private Queue<QueuedDownloadInfo> PendingDownloads = new Queue<QueuedDownloadInfo>();

        // items sent to the server here
        private List<QueuedDownloadInfo> CurrentDownloads = new List<QueuedDownloadInfo>(MAX_TRANSFERS);

        // background tasks
        private CancellationTokenSource cts;
        private Task BackupTask;
        private Task QueueTask;

        // some stats
        private int TextItemsFound;
        private int TextItemsTransferred;
        private int TextItemErrors;

        #region Properties

        /// <summary>
        /// true if either of the background tasks is running
        /// </summary>
        private bool BackgroundBackupRunning => InventoryWalkerRunning || QueueRunnerRunning;

        /// <summary>
        /// true if the task walking inventory is running
        /// </summary>
        private bool InventoryWalkerRunning => BackupTask != null && !BackupTask.IsCompleted;

        /// <summary>
        /// true if the task feeding the queue to the server is running
        /// </summary>
        private bool QueueRunnerRunning => QueueTask != null && !QueueTask.IsCompleted;

        /// <summary>
        /// returns a string summarizing activity
        /// </summary>
        /// <returns></returns>
        private string BackgroundBackupStatus
        {
            get
            {
                StringBuilder sbResult = new StringBuilder();
                sbResult.AppendFormat("{0} is {1} running.", Name, BoolToNot(BackgroundBackupRunning));
                if (TextItemErrors != 0 || TextItemsFound != 0 || TextItemsTransferred != 0)
                {
                    sbResult.AppendFormat("\r\n{0} : Inventory walker ( {1} running ) has found {2} items.",
                                            Name, BoolToNot(InventoryWalkerRunning), TextItemsFound);
                    sbResult.AppendFormat("\r\n{0} : Server Transfers ( {1} running ) has transferred {2} items with {3} errors.",
                                            Name, BoolToNot(QueueRunnerRunning), TextItemsTransferred, TextItemErrors);
                    sbResult.AppendFormat("\r\n{0} : {1} items in Queue, {2} items requested from server.",
                                            Name, PendingDownloads.Count, CurrentDownloads.Count);
                }
                return sbResult.ToString();
            }
        }

        #endregion Properties

        public BackupCommand(TestClient testClient)
        {
            Name = "backuptext";
            Description = "Backup inventory to a folder on your hard drive. Usage: " + Name + " [to <directory>] | [abort] | [status]";
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length == 1 && args[0] == "status")
            {
                return BackgroundBackupStatus;
            }
            else if (args.Length == 1 && args[0] == "abort")
            {
                if (!BackgroundBackupRunning)
                    return BackgroundBackupStatus;

                cts?.Cancel();

                // give tasks a moment to stop
                var allTasks = Task.WhenAll(new[] { BackupTask ?? Task.CompletedTask, QueueTask ?? Task.CompletedTask });
                var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
                if (completed == allTasks)
                {
                    try { await allTasks.ConfigureAwait(false); } catch { }
                }

                return BackgroundBackupStatus;
            }
            else if (args.Length != 2)
            {
                return "Usage: " + Name + " [to <directory>] | [abort] | [status]";
            }
            else if (BackgroundBackupRunning)
            {
                return BackgroundBackupStatus;
            }

            // start background operations
            cts = new CancellationTokenSource();
            lock (CurrentDownloads) CurrentDownloads.Clear();
            lock (PendingDownloads) PendingDownloads.Clear();

            QueueTask = Task.Run(() => QueueRunnerAsync(cts.Token), cts.Token);
            BackupTask = Task.Run(() => BackupWorkerAsync(args, cts.Token), cts.Token);

            return "Started background operations.";
        }

        private async Task QueueRunnerAsync(CancellationToken token)
        {
            TextItemErrors = TextItemsTransferred = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // have any timed out?
                    lock (CurrentDownloads)
                    {
                        if (CurrentDownloads.Count > 0)
                        {
                            foreach (QueuedDownloadInfo qdi in CurrentDownloads.ToArray())
                            {
                                if ((qdi.WhenRequested + TimeSpan.FromSeconds(60)) < DateTime.Now)
                                {
                                    Logger.DebugLog(Name + ": timeout on asset " + qdi.AssetID, Client);
                                    // submit request again
                                    var transferID = UUID.Random();
                                    Client.Assets.RequestInventoryAsset(
                                        qdi.AssetID, qdi.ItemID, qdi.TaskID, qdi.OwnerID, qdi.Type, true, transferID, Assets_OnAssetReceived);
                                    qdi.WhenRequested = DateTime.Now;
                                    qdi.IsRequested = true;
                                }
                            }
                        }
                    }

                    if (token.IsCancellationRequested) break;

                    if (PendingDownloads.Count != 0)
                    {
                        // room in the server queue?
                        if (CurrentDownloads.Count < MAX_TRANSFERS)
                        {
                            QueuedDownloadInfo qdi = null;
                            lock (PendingDownloads)
                            {
                                if (PendingDownloads.Count != 0)
                                    qdi = PendingDownloads.Dequeue();
                            }

                            if (qdi != null)
                            {
                                qdi.WhenRequested = DateTime.Now;
                                qdi.IsRequested = true;
                                var transferID = UUID.Random();
                                Client.Assets.RequestInventoryAsset(
                                    qdi.AssetID, qdi.ItemID, qdi.TaskID, qdi.OwnerID, qdi.Type, true, transferID, Assets_OnAssetReceived);

                                lock (CurrentDownloads) CurrentDownloads.Add(qdi);
                            }
                        }
                    }

                    if (CurrentDownloads.Count == 0 && PendingDownloads.Count == 0 && (BackupTask == null || BackupTask.IsCompleted))
                    {
                        Logger.DebugLog(Name + ": both transfer queues empty AND inventory walking task is done", Client);
                        return;
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task BackupWorkerAsync(string[] args, CancellationToken token)
        {
            TextItemsFound = 0;

            lock (CurrentDownloads) CurrentDownloads.Clear();

            DirectoryInfo di = new DirectoryInfo(args[1]);

            // recurse on the root folder into the entire inventory
            await Task.Run(() => BackupFolder(Client.Inventory.Store.RootNode, di.FullName, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// BackupFolder - recurse through the inventory nodes sending scripts and notecards to the transfer queue
        /// </summary>
        /// <param name="folder">The current leaf in the inventory tree</param>
        /// <param name="sPathSoFar">path so far, in the form @"c:\here" -- this needs to be "clean" for the current filesystem</param>
        private void BackupFolder(InventoryNode folder, string sPathSoFar, CancellationToken token)
        {
            // first scan this folder for text
            foreach (InventoryNode iNode in folder.Nodes.Values)
            {
                if (token.IsCancellationRequested) return;
                if (!(iNode.Data is InventoryItem ii)) continue;
                if (ii.AssetType != AssetType.LSLText && ii.AssetType != AssetType.Notecard) continue;
                // check permissions on scripts
                if (ii.AssetType == AssetType.LSLText)
                {
                    if ((ii.Permissions.OwnerMask & PermissionMask.Modify) == PermissionMask.None)
                    {
                        // skip this one
                        continue;
                    }
                }

                string sExtension = (ii.AssetType == AssetType.LSLText) ? ".lsl" : ".txt";
                // make the output file
                string sPath = sPathSoFar + "\\" + MakeValid(ii.Name.Trim()) + sExtension;

                // create the new qdi
                QueuedDownloadInfo qdi = new QueuedDownloadInfo(sPath, ii.AssetUUID, ii.UUID, UUID.Zero,
                    Client.Self.AgentID, ii.AssetType);

                // add it to the queue
                lock (PendingDownloads)
                {
                    TextItemsFound++;
                    PendingDownloads.Enqueue(qdi);
                }
            }

            // now run any subfolders
            foreach (InventoryNode i in folder.Nodes.Values)
            {
                if (token.IsCancellationRequested) return;
                else if (i.Data is OpenMetaverse.InventoryFolder)
                    BackupFolder(i, sPathSoFar + "\\" + MakeValid(i.Data.Name.Trim()), token);
            }
        }

        private string MakeValid(string path)
        {
            // Use central sanitizer for filenames/dir names
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return FileHelper.SafeFileName(path.Trim());
        }

        private void Assets_OnAssetReceived(AssetDownload asset, Asset blah)
        {
            lock (CurrentDownloads)
            {
                // see if we have this in our transfer list
                QueuedDownloadInfo r = CurrentDownloads.Find(q => q.AssetID == asset.AssetID);

                if (r != null && r.AssetID == asset.AssetID)
                {
                    if (asset.Success)
                    {
                        // create the directory to put this in
                        global::System.IO.Directory.CreateDirectory(Path.GetDirectoryName(r.FileName));

                        // write out the file
                        File.WriteAllBytes(r.FileName, asset.AssetData);
                        Logger.DebugLog(Name + " Wrote: " + r.FileName, Client);
                        TextItemsTransferred++;
                    }
                    else
                    {
                        TextItemErrors++;
                        Console.WriteLine("{0}: Download of asset {1} ({2}) failed with status {3}", Name, r.FileName,
                            r.AssetID.ToString(), asset.Status.ToString());
                    }

                    // remove the entry
                    CurrentDownloads.Remove(r);
                }
            }
        }

        /// <summary>
        /// returns blank or "not" if false
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        private static string BoolToNot(bool b)
        {
            return b ? string.Empty : "not";
        }
    }
}
