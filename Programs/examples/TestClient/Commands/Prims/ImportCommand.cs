using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using OpenMetaverse.StructuredData;

namespace OpenMetaverse.TestClient
{
    public class ImportCommand : Command
    {
        private enum ImporterState
        {
            RezzingParent,
            RezzingChildren,
            Linking,
            Idle
        }

        private class Linkset
        {
            public Primitive RootPrim;
            public List<Primitive> Children = new List<Primitive>();

            public Linkset()
            {
                RootPrim = new Primitive();
            }

            public Linkset(Primitive rootPrim)
            {
                RootPrim = rootPrim;
            }
        }

        Primitive currentPrim;
        Vector3 currentPosition;
        AutoResetEvent primDone = new AutoResetEvent(false);
        List<Primitive> primsCreated;
        List<uint> linkQueue;
        uint rootLocalID;
        ImporterState state = ImporterState.Idle;

        public ImportCommand(TestClient testClient)
        {
            Name = "import";
            Description = "Import prims from an exported xml file. Usage: import inputfile.xml [usegroup]";
            Category = CommandCategory.Objects;
            
            testClient.Objects.ObjectUpdate += Objects_OnNewPrim;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: import inputfile.xml [usegroup]";

            string filename = args[0];
            UUID GroupID = (args.Length > 1) ? Client.GroupID : UUID.Zero;
            string xml;
            List<Primitive> prims;

            try { xml = File.ReadAllText(filename); }
            catch (Exception e) { return e.Message; }

            try { prims = Helpers.OSDToPrimList(OSDParser.DeserializeLLSDXml(xml)); }
            catch (Exception e) { return "Failed to deserialize " + filename + ": " + e.Message; }

            // Build an organized structure from the imported prims
            Dictionary<uint, Linkset> linksets = new Dictionary<uint, Linkset>();
            foreach (var prim in prims)
            {
                if (prim.ParentID == 0)
                {
                    if (linksets.TryGetValue(prim.LocalID, out var linkset))
                        linkset.RootPrim = prim;
                    else
                        linksets[prim.LocalID] = new Linkset(prim);
                }
                else
                {
                    if (!linksets.ContainsKey(prim.ParentID))
                        linksets[prim.ParentID] = new Linkset();

                    linksets[prim.ParentID].Children.Add(prim);
                }
            }

            primsCreated = new List<Primitive>();
            Console.WriteLine("Importing " + linksets.Count + " structures.");

            foreach (Linkset linkset in linksets.Values)
            {
                if (linkset.RootPrim.LocalID != 0)
                {
                    state = ImporterState.RezzingParent;
                    currentPrim = linkset.RootPrim;
                    // HACK: Import the structure just above our head
                    // We need a more elaborate solution for importing with relative or absolute offsets
                    linkset.RootPrim.Position = Client.Self.SimPosition;
                    linkset.RootPrim.Position.Z += 3.0f;
                    currentPosition = linkset.RootPrim.Position;

                    // Rez the root prim with no rotation
                    Quaternion rootRotation = linkset.RootPrim.Rotation;
                    linkset.RootPrim.Rotation = Quaternion.Identity;

                    Client.Objects.AddPrim(Client.Network.CurrentSim, linkset.RootPrim.PrimData, GroupID,
                        linkset.RootPrim.Position, linkset.RootPrim.Scale, linkset.RootPrim.Rotation);

                    if (!primDone.WaitOne(TimeSpan.FromSeconds(10), false))
                        return "Rez failed, timed out while creating the root prim.";

                    Client.Objects.SetPosition(Client.Network.CurrentSim, primsCreated[primsCreated.Count - 1].LocalID, linkset.RootPrim.Position);

                    state = ImporterState.RezzingChildren;

                    // Rez the child prims
                    foreach (Primitive prim in linkset.Children)
                    {
                        currentPrim = prim;
                        currentPosition = prim.Position + linkset.RootPrim.Position;

                        Client.Objects.AddPrim(Client.Network.CurrentSim, prim.PrimData, GroupID, currentPosition,
                            prim.Scale, prim.Rotation);

                        if (!primDone.WaitOne(TimeSpan.FromSeconds(10), false))
                            return "Rez failed, timed out while creating child prim.";
                        Client.Objects.SetPosition(Client.Network.CurrentSim, primsCreated[primsCreated.Count - 1].LocalID, currentPosition);

                    }

                    // Create a list of the local IDs of the newly created prims
                    List<uint> primIDs = new List<uint>(primsCreated.Count) { rootLocalID /*Root prim is first in list.*/ };

                    if (linkset.Children.Count != 0)
                    {
                        // Add the rest of the prims to the list of local IDs
                        primIDs.AddRange(from prim in primsCreated where prim.LocalID != rootLocalID select prim.LocalID);
                        linkQueue = new List<uint>(primIDs.Count);
                        linkQueue.AddRange(primIDs);

                        // Link and set the permissions + rotation
                        state = ImporterState.Linking;
                        Client.Objects.LinkPrims(Client.Network.CurrentSim, linkQueue);

                        if (primDone.WaitOne(TimeSpan.FromSeconds(linkset.Children.Count), false))
                            Client.Objects.SetRotation(Client.Network.CurrentSim, rootLocalID, rootRotation);
                        else
                            Console.WriteLine("Warning: Failed to link {0} prims", linkQueue.Count);

                    }
                    else
                    {
                        Client.Objects.SetRotation(Client.Network.CurrentSim, rootLocalID, rootRotation);
                    }
                    
                    // Set permissions on newly created prims
                    Client.Objects.SetPermissions(Client.Network.CurrentSim, primIDs,
                        PermissionWho.Everyone | PermissionWho.Group | PermissionWho.NextOwner,
                        PermissionMask.All, true);
                    
                    state = ImporterState.Idle;
                }
                else
                {
                    // Skip linksets with a missing root prim
                    Console.WriteLine("WARNING: Skipping a linkset with a missing root prim");
                }

                // Reset everything for the next linkset
                primsCreated.Clear();
            }

            return "Import complete.";
        }

        void Objects_OnNewPrim(object sender, PrimEventArgs e)
        {
            Primitive prim = e.Prim;

            if ((prim.Flags & PrimFlags.CreateSelected) == 0)
                return; // We received an update for an object we didn't create

            switch (state)
            {
                case ImporterState.RezzingParent:
                    rootLocalID = prim.LocalID;
                    goto case ImporterState.RezzingChildren;
                case ImporterState.RezzingChildren:
                    if (!primsCreated.Contains(prim))
                    {
                        Console.WriteLine("Setting properties for " + prim.LocalID);
                        // TODO: Is there a way to set all of this at once, and update more ObjectProperties stuff?
                        Client.Objects.SetPosition(e.Simulator, prim.LocalID, currentPosition);
                        Client.Objects.SetTextures(e.Simulator, prim.LocalID, currentPrim.Textures);

                        if (currentPrim.Light != null && currentPrim.Light.Intensity > 0)
                        {
                            Client.Objects.SetLight(e.Simulator, prim.LocalID, currentPrim.Light);
                        }

                        if (currentPrim.Flexible != null)
                        {
                            Client.Objects.SetFlexible(e.Simulator, prim.LocalID, currentPrim.Flexible);
                        }

                        if (currentPrim.Sculpt != null && currentPrim.Sculpt.SculptTexture != UUID.Zero)
                        {
                            Client.Objects.SetSculpt(e.Simulator, prim.LocalID, currentPrim.Sculpt);
                        }

                        if (currentPrim.Properties!= null && !string.IsNullOrEmpty(currentPrim.Properties.Name))
                        {
                            Client.Objects.SetName(e.Simulator, prim.LocalID, currentPrim.Properties.Name);
                        }

                        if (currentPrim.Properties != null && !string.IsNullOrEmpty(currentPrim.Properties.Description))
                        {
                            Client.Objects.SetDescription(e.Simulator, prim.LocalID, currentPrim.Properties.Description);
                        }

                        primsCreated.Add(prim);
                        primDone.Set();
                    }
                    break;
                case ImporterState.Linking:
                    lock (linkQueue)
                    {
                        int index = linkQueue.IndexOf(prim.LocalID);
                        if (index != -1)
                        {
                            linkQueue.RemoveAt(index);
                            if (linkQueue.Count == 0)
                                primDone.Set();
                        }
                    }
                    break;
            }
        }
    }
}
