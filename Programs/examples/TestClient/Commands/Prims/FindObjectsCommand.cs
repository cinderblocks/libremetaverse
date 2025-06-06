using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;


namespace OpenMetaverse.TestClient
{
    public class FindObjectsCommand : Command
    {
        Dictionary<UUID, Primitive> PrimsWaiting = new Dictionary<UUID, Primitive>();
        AutoResetEvent AllPropertiesReceived = new AutoResetEvent(false);

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
                where kvp.Value != null select kvp.Value into prim let pos = prim.Position 
                where prim.ParentID == 0 && pos != Vector3.Zero && Vector3.Distance(pos, location) < radius select prim).ToList();

            // *** request properties of these objects ***
            var complete = RequestObjectProperties(prims, 250);

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

        private bool RequestObjectProperties(List<Primitive> objects, int msPerRequest)
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

            Client.Objects.SelectObjects(Client.Network.CurrentSim, localids);

            return AllPropertiesReceived.WaitOne(2000 + msPerRequest * objects.Count, false);
        }

        void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            lock (PrimsWaiting)
            {
                if (PrimsWaiting.TryGetValue(e.Properties.ObjectID, out var prim))
                {
                    prim.Properties = e.Properties;
                }
                PrimsWaiting.Remove(e.Properties.ObjectID);

                if (PrimsWaiting.Count == 0)
                    AllPropertiesReceived.Set();
            }
        }
    }
}
