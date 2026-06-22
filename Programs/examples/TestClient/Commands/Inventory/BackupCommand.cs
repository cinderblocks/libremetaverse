using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                    sbResult.AppendFormat("\r\n{0} : {1} items in Queue.", Name, PendingDownloads.Count);
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
            lock (PendingDownloads) PendingDownloads.Clear();

            // Capture the walker task locally so QueueRunnerAsync checks the same
            // Task instance we started — avoids a race if ExecuteAsync is re-entered.
            var walkerTask = BackupTask = Task.Run(() => BackupWorkerAsync(args, cts.Token), cts.Token);
            QueueTask = Task.Run(() => QueueRunnerAsync(cts.Token, walkerTask), cts.Token);

            return "Started background operations.";
        }

        private async Task QueueRunnerAsync(CancellationToken token, Task walkerTask)
        {
            TextItemErrors = TextItemsTransferred = 0;

            using var semaphore = new SemaphoreSlim(MAX_TRANSFERS);
            var activeTasks = new List<Task>();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    QueuedDownloadInfo? qdi = null;
                    bool shouldExit;
                    lock (PendingDownloads)
                    {
                        if (PendingDownloads.Count > 0)
                            qdi = PendingDownloads.Dequeue();
                        // Check walker-done and queue-empty atomically to avoid the
                        // TOCTOU race where BackupFolder enqueues a final item and
                        // BackupTask completes between the two checks.
                        shouldExit = qdi == null && walkerTask.IsCompleted && PendingDownloads.Count == 0;
                    }

                    if (qdi != null)
                    {
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                        activeTasks.Add(ProcessDownloadAsync(qdi, semaphore, token));
                    }
                    else if (shouldExit && activeTasks.Count == 0)
                    {
                        Logger.DebugLog(Name + ": all downloads complete", Client);
                        return;
                    }
                    else
                    {
                        activeTasks.RemoveAll(t => t.IsCompleted);
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }

            // Drain remaining tasks; observe exceptions so they are counted as errors.
            foreach (var t in activeTasks)
            {
                try { await t.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch { TextItemErrors++; }
            }
        }

        private async Task ProcessDownloadAsync(QueuedDownloadInfo qdi, SemaphoreSlim semaphore, CancellationToken token)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                var asset = await Client.Assets.RequestInventoryAssetAsync(
                    qdi.AssetID, qdi.ItemID, qdi.TaskID, Client.Self.AgentID, qdi.Type, true, UUID.Random(), cts.Token)
                    .ConfigureAwait(false);

                if (asset != null)
                {
                    var dir = Path.GetDirectoryName(qdi.FileName);
                    if (!string.IsNullOrEmpty(dir))
                        global::System.IO.Directory.CreateDirectory(dir);
                    File.WriteAllBytes(qdi.FileName, asset.AssetData);
                    Logger.DebugLog(Name + " Wrote: " + qdi.FileName, Client);
                    TextItemsTransferred++;
                }
                else
                {
                    TextItemErrors++;
                    Console.WriteLine($"{Name}: Download of asset {qdi.FileName} ({qdi.AssetID}) failed");
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task BackupWorkerAsync(string[] args, CancellationToken token)
        {
            TextItemsFound = 0;

            DirectoryInfo di = new DirectoryInfo(args[1]);

            // recurse on the root folder into the entire inventory
            var store = Client.Inventory?.Store;
            if (store == null) return;

            await Task.Run(() => BackupFolder(store.RootNode, di.FullName, token), token).ConfigureAwait(false);
        }

        /// <summary>
        /// BackupFolder - recurse through the inventory nodes sending scripts and notecards to the transfer queue
        /// </summary>
        /// <param name="folder">The current leaf in the inventory tree</param>
        /// <param name="sPathSoFar">path so far, in the form @"c:\here" -- this needs to be "clean" for the current filesystem</param>
        /// <param name="token">Cancellation token for the operation</param>
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
                else if (i.Data is LibreMetaverse.InventoryFolder)
                    BackupFolder(i, sPathSoFar + "\\" + MakeValid(i.Data.Name.Trim()), token);
            }
        }

        private string MakeValid(string path)
        {
            // Use central sanitizer for filenames/dir names
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return FileHelper.SafeFileName(path.Trim());
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
