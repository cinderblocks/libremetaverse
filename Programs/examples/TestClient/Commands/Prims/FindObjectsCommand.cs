using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenMetaverse;
using Vector3 = OpenMetaverse.Vector3;
using LibreMetaverse;

namespace TestClient.Commands.Prims
{
    public class FindObjectsCommand : Command
    {
        Dictionary<UUID, Primitive> PrimsWaiting = new Dictionary<UUID, Primitive>();

        public FindObjectsCommand(TestClient testClient)
        {
            testClient.Objects.ObjectProperties += Objects_OnObjectProperties;

            Name = "findobjects";
            Description = "Finds all objects, which name contains search-string. " +
                "Usage: findobjects [radius] <search-string>";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            // *** parse arguments ***
            if (args.Length < 1 || args.Length > 2)
            {
                return "Usage: findobjects [radius] <search-string>";
            }

            var radius = float.Parse(args[0]);
            var searchString = (args.Length > 1) ? args[1] : string.Empty;

            // *** get current location ***
            var location = Client.Self.SimPosition;

            // *** find all objects in radius ***
            var prims = (from kvp 
                in Client.Network.CurrentSim.ObjectsPrimitives 
                where kvp.Value != null select kvp.Value into prim let pos = PositionHelper.GetPrimPosition(Client.Network.CurrentSim, prim) 
                where pos != Vector3.Zero && Vector3.Distance(pos, location) < radius select prim).ToList();

            // *** request properties of these objects ***
            var complete = await RequestObjectPropertiesAsync(prims, 250).ConfigureAwait(false);

            foreach (var p in prims)
            {
                var name = p.Properties?.Name;
                if (string.IsNullOrEmpty(searchString) || ((name != null) && (name.Contains(searchString))))
                    Console.WriteLine("Object '{0}': {1}", name, p.ID.ToString());
            }

            if (complete) return "Done searching";
            Console.WriteLine("Warning: Unable to retrieve full properties for:");
            foreach (var uuid in PrimsWaiting.Keys)
                Console.WriteLine(uuid);

            return "Done searching";
        }

        private async Task<bool> RequestObjectPropertiesAsync(List<Primitive> objects, int msPerRequest)
        {
            // Create an array of the local IDs of all the prims we are requesting properties for
            uint[] localids = new uint[objects.Count];

            lock (PrimsWaiting)
            {
                PrimsWaiting.Clear();

                for (int i = 0; i < objects.Count; ++i)
                {
                    localids[i] = objects[i].LocalID;
                    PrimsWaiting.Add(objects[i].ID, objects[i]);
                }
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Wire up a temporary watcher to complete the TCS when all properties are received
            void LocalHandler(object s, ObjectPropertiesEventArgs e)
            {
                lock (PrimsWaiting)
                {
                    if (PrimsWaiting.TryGetValue(e.Properties.ObjectID, out var prim))
                    {
                        prim.Properties = e.Properties;
                    }
                    PrimsWaiting.Remove(e.Properties.ObjectID);

                    if (PrimsWaiting.Count == 0)
                    {
                        tcs.TrySetResult(true);
                    }
                }
            }

            try
            {
                // Temporary subscribe
                Client.Objects.ObjectProperties += LocalHandler;

                Client.Objects.SelectObjects(Client.Network.CurrentSim, localids);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000 + msPerRequest * objects.Count)).ConfigureAwait(false);
                return completed == tcs.Task;
            }
            finally
            {
                Client.Objects.ObjectProperties -= LocalHandler;
            }
        }

        void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            // Keep the global handler to populate properties when received by other flows
            lock (PrimsWaiting)
            {
                if (PrimsWaiting.TryGetValue(e.Properties.ObjectID, out var prim))
                {
                    prim.Properties = e.Properties;
                }
                PrimsWaiting.Remove(e.Properties.ObjectID);
            }
        }
    }
}
