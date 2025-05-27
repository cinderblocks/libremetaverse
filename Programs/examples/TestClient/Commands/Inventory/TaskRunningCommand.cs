using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenMetaverse.TestClient
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

            List<InventoryBase> items = Client.Inventory.GetTaskInventory(objectID, objectLocalID, TimeSpan.FromSeconds(30));

            //bool wantSet = false;
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
                bool wasRunning = false;

                EventHandler<ScriptRunningReplyEventArgs> callback;
                using (AutoResetEvent OnScriptRunningReset = new AutoResetEvent(false))
                {
                    callback = ((sender, e) =>
                    {
                        if (e.ObjectID == objectID)
                        {
                            result += $" IsMono: {e.IsMono} IsRunning: {e.IsRunning}";
                            wasRunning = e.IsRunning;
                            OnScriptRunningReset.Set();
                        }
                    });

                    Client.Inventory.ScriptRunningReply += callback;

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
                                OnScriptRunningReset.Reset();
                                Client.Inventory.RequestGetScriptRunning(objectID, item.UUID);
                                if (!OnScriptRunningReset.WaitOne(10000, true))
                                {
                                    result += " (no script info)";
                                }
                                if (setAny && item.Name.Contains(matching))
                                {
                                    if (wasRunning != setTaskTo)
                                    {
                                        OnScriptRunningReset.Reset();
                                        result += " Setting " + setTaskTo + " => ";
                                        Client.Inventory.RequestSetScriptRunning(objectID, item.UUID, setTaskTo);
                                        if (!OnScriptRunningReset.WaitOne(10000, true))
                                        {
                                            result += " (was not set)";
                                        }
                                    }
                                }
                            }

                            result += Environment.NewLine;
                        }
                    }
                }
                Client.Inventory.ScriptRunningReply -= callback;
                return result;
            }
            else
            {
                return "Failed to download task inventory for " + objectLocalID;
            }
        }
    }
}
