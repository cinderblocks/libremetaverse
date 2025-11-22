using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class TaskRunningCommand : Command
    {
        public TaskRunningCommand(TestClient testClient)
        {
            Name = "taskrunning";
            Description = "Retrieves or set IsRunning flag on items inside an object (task inventory). Usage: taskrunning objectID [[scriptName] true|false]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: taskrunning objectID [[scriptName] true|false]";
            }

            if (!UUID.TryParse(args[0], out var objectID))
            {
                return "Usage: taskrunning objectID [[scriptName] true|false]";
            }

            var found = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(prim => prim.Value.ID == objectID);
            if (found.Value == null)
            {
                return $"Couldn't find object {objectID}";
            }

            var objectLocalID = found.Value.LocalID;

            List<InventoryBase> items = await Client.Inventory.GetTaskInventoryAsync(objectID, objectLocalID).ConfigureAwait(false);

            bool setTaskTo = false;
            if (items != null)
            {
                string result = string.Empty;
                string matching = string.Empty;
                bool setAny = false;
                if (args.Length > 1)
                {
                    matching = args[1];

                    var tf = args.Length > 2 ? args[2] : matching.ToLower();
                    switch (tf)
                    {
                        case "true":
                            setAny = true;
                            setTaskTo = true;
                            break;
                        case "false":
                            setAny = true;
                            setTaskTo = false;
                            break;
                    }

                }

                foreach (var t in items)
                {
                    if (t is InventoryFolder)
                    {
                        // this shouldn't happen this year
                        result += $"[Folder] Name: {t.Name}" + Environment.NewLine;
                    }
                    else
                    {
                        InventoryItem item = (InventoryItem)t;
                        AssetType assetType = item.AssetType;
                        result += $"[Item] Name: {item.Name} Desc: {item.Description} Type: {assetType}";
                        if (assetType == AssetType.LSLBytecode || assetType == AssetType.LSLText)
                        {
                            // Query script running state asynchronously
                            var wasRunning = await QueryScriptRunningAsync(objectID, item.UUID).ConfigureAwait(false);
                            result += $" IsRunning: {wasRunning}";

                            if (setAny && item.Name.Contains(matching))
                            {
                                if (wasRunning != setTaskTo)
                                {
                                    // Set script running and then re-query
                                    Client.Inventory.RequestSetScriptRunning(objectID, item.UUID, setTaskTo);
                                    var newState = await QueryScriptRunningAsync(objectID, item.UUID).ConfigureAwait(false);
                                    result += $" Setting {setTaskTo} => {newState}";
                                }
                            }
                        }

                        result += Environment.NewLine;
                    }
                }

                return result;
            }
            else
            {
                return "Failed to download task inventory for " + objectLocalID;
            }
        }

        private Task<bool> QueryScriptRunningAsync(UUID objectID, UUID scriptID)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<ScriptRunningReplyEventArgs> callback = null;
            callback = (sender, e) =>
            {
                if (e.ObjectID == objectID && e.ScriptID == scriptID)
                {
                    tcs.TrySetResult(e.IsRunning);
                }
            };

            Client.Inventory.ScriptRunningReply += callback;

            // Request status
            Client.Inventory.RequestGetScriptRunning(objectID, scriptID);

            // Time out after 10 seconds
            var delay = Task.Delay(TimeSpan.FromSeconds(10));

            return Task.WhenAny(tcs.Task, delay).ContinueWith(t =>
            {
                Client.Inventory.ScriptRunningReply -= callback;
                if (t.Result == delay)
                    return false;
                if (t.Result == tcs.Task && tcs.Task.Status == TaskStatus.RanToCompletion)
                    return tcs.Task.Result;
                return false;
            }, TaskScheduler.Default);
        }
    }
}
