using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Prims
{
    public class ChangePermsCommand : Command
    {
        private readonly Dictionary<UUID, Primitive> Objects = new Dictionary<UUID, Primitive>();
        private PermissionMask Perms = PermissionMask.None;
        private bool PermsSent;
        private int PermCount;

        // TaskCompletionSource used to await permission propagation
        private TaskCompletionSource<bool> permsTcs;

        public ChangePermsCommand(TestClient testClient)
        {
            testClient.Objects.ObjectProperties += Objects_OnObjectProperties;

            Name = "changeperms";
            Description = "Recursively changes all of the permissions for child and task inventory objects. Usage prim-uuid [copy] [mod] [xfer]";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            var localIDs = new List<uint>();

            // Reset class-wide variables
            PermsSent = false;
            Objects.Clear();
            Perms = PermissionMask.None;
            PermCount = 0;

            if (args.Length < 1 || args.Length > 4)
                return "Usage prim-uuid [copy] [mod] [xfer]";

            if (!UUID.TryParse(args[0], out var rootID))
                return "Usage prim-uuid [copy] [mod] [xfer]";

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "copy":
                        Perms |= PermissionMask.Copy;
                        break;
                    case "mod":
                        Perms |= PermissionMask.Modify;
                        break;
                    case "xfer":
                        Perms |= PermissionMask.Transfer;
                        break;
                    default:
                        return "Usage prim-uuid [copy] [mod] [xfer]";
                }
            }

            Logger.DebugLog($"Using PermissionMask: {Perms}", Client);

            // Find the requested prim
            var reqkvp = Client.Network.CurrentSim.ObjectsPrimitives
                .FirstOrDefault(prim => prim.Value.ID == rootID);
            if (reqkvp.Value == null)
            {
                return $"Cannot find requested object {rootID}";

            }
            var rootPrim = reqkvp.Value;
            Logger.DebugLog($"Found requested object {rootPrim.ID}", Client);

            if (rootPrim.ParentID != 0)
            {
                // This is not actually a root prim, find the root
                if (!Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(rootPrim.ParentID, out rootPrim))
                {
                    return "Cannot find root prim for requested object";
                }

                Logger.DebugLog($"Set root prim to {rootPrim.ID}", Client);
            }

            // Find all the child primitives linked to the root
            var childPrims = (from kvp
                in Client.Network.CurrentSim.ObjectsPrimitives where kvp.Value != null
                select kvp.Value into child where child.ParentID == rootPrim.LocalID select child).ToList();

            // Build a dictionary of primitives for referencing later
            Objects[rootPrim.ID] = rootPrim;
            foreach (var p in childPrims)
                Objects[p.ID] = p;

            // Build a list of all the localIDs to set permissions for
            localIDs.Add(rootPrim.LocalID);
            localIDs.AddRange(childPrims.Select(t => t.LocalID));

            // Go through each of the three main permissions and enable or disable them
            #region Set Linkset Permissions

            PermCount = 0;
            permsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Client.Objects.SetPermissions(Client.Network.CurrentSim, localIDs, PermissionWho.NextOwner,
                PermissionMask.Modify, (Perms & PermissionMask.Modify) == PermissionMask.Modify);
            PermsSent = true;

            var completed = await Task.WhenAny(permsTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
            if (completed != permsTcs.Task)
                return "Failed to set the modify bit, permissions in an unknown state";

            PermCount = 0;
            permsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Client.Objects.SetPermissions(Client.Network.CurrentSim, localIDs, PermissionWho.NextOwner,
                PermissionMask.Copy, (Perms & PermissionMask.Copy) == PermissionMask.Copy);
            PermsSent = true;

            completed = await Task.WhenAny(permsTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
            if (completed != permsTcs.Task)
                return "Failed to set the copy bit, permissions in an unknown state";

            PermCount = 0;
            permsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Client.Objects.SetPermissions(Client.Network.CurrentSim, localIDs, PermissionWho.NextOwner,
                PermissionMask.Transfer, (Perms & PermissionMask.Transfer) == PermissionMask.Transfer);
            PermsSent = true;

            completed = await Task.WhenAny(permsTcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
            if (completed != permsTcs.Task)
                return "Failed to set the transfer bit, permissions in an unknown state";

            #endregion Set Linkset Permissions

            // Check each prim for task inventory and set permissions on the task inventory
            int taskItems = 0;
            foreach (Primitive prim in Objects.Values)
            {
                if ((prim.Flags & PrimFlags.InventoryEmpty) != 0) continue;
                var items = await Client.Inventory.GetTaskInventoryAsync(prim.ID, prim.LocalID).ConfigureAwait(false);

                if (items == null) continue;
                foreach (var item in items.Where(i => !(i is InventoryFolder)).Cast<InventoryItem>())
                {
                    item.Permissions.NextOwnerMask = Perms;

                    Client.Inventory.UpdateTaskInventory(prim.LocalID, item);
                    ++taskItems;
                }
            }

            return $"Set permissions to {Perms} on {localIDs.Count} objects and {taskItems} inventory items";
        }

        private void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            if (!PermsSent) { return; }

            if (Objects.ContainsKey(e.Properties.ObjectID))
            {
                // FIXME: Confirm the current operation against properties.Permissions.NextOwnerMask

                ++PermCount;
                if (PermCount >= Objects.Count)
                    permsTcs?.TrySetResult(true);
            }
        }
    }
}
