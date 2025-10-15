using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;

namespace TestClient.Commands.Prims
{
    public class ExportCommand : Command
    {
        private readonly List<UUID> Textures = new List<UUID>();
        private readonly AutoResetEvent GotPermissionsEvent = new AutoResetEvent(false);
        private Primitive.ObjectProperties Properties;
        private bool GotPermissions = false;
        private UUID SelectedObject = UUID.Zero;

        private readonly Dictionary<UUID, Primitive> PrimsWaiting = new Dictionary<UUID, Primitive>();
        private readonly AutoResetEvent AllPropertiesReceived = new AutoResetEvent(false);

        public ExportCommand(TestClient testClient)
        {
            testClient.Objects.ObjectPropertiesFamily += Objects_OnObjectPropertiesFamily;

            testClient.Objects.ObjectProperties += Objects_OnObjectProperties;
            testClient.Avatars.ViewerEffectPointAt += Avatars_ViewerEffectPointAt;

            Name = "export";
            Description = "Exports an object to an xml file. Usage: export uuid outputfile.xml";
            Category = CommandCategory.Objects;
        }

        private void Avatars_ViewerEffectPointAt(object sender, ViewerEffectPointAtEventArgs e)
        {
            if (e.SourceID == Client.MasterKey)
            {
                //Client.DebugLog("Master is now selecting " + targetID.ToString());
                SelectedObject = e.TargetID;
            }
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 2 && !(args.Length == 1 && SelectedObject != UUID.Zero))
                return "Usage: export uuid outputfile.xml";

            UUID id;
            string file;

            if (args.Length == 2)
            {
                file = args[1];
                if (!UUID.TryParse(args[0], out id))
                    return "Usage: export uuid outputfile.xml";
            }
            else
            {
                file = args[0];
                id = SelectedObject;
            }

            var kvp = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(
                prim => prim.Value.ID == id);

            if (kvp.Value == null)
            {
                return $"Couldn't find UUID {id} in the objects currently indexed in the current simulator";
            }

            var exportPrim = kvp.Value;
            var localId = exportPrim.ParentID != 0 ? exportPrim.ParentID : exportPrim.LocalID;

            // Check for export permission first
            Client.Objects.RequestObjectPropertiesFamily(Client.Network.CurrentSim, id);
            GotPermissionsEvent.WaitOne(TimeSpan.FromSeconds(20), false);

            if (!GotPermissions)
            {
                return "Couldn't fetch permissions for the requested object, try again";
            }

            GotPermissions = false;

            if (Properties.OwnerID != Client.Self.AgentID && 
                Properties.OwnerID != Client.MasterKey)
            {
                return "That object is owned by " + Properties.OwnerID + ", we don't have permission " +
                       "to export it";
            }

            var prims = (from kvprim in Client.Network.CurrentSim.ObjectsPrimitives 
                where kvprim.Value != null select kvprim.Value into prim 
                where prim.LocalID == localId || prim.ParentID == localId select prim).ToList();

            bool complete = RequestObjectProperties(prims, 250);

            if (!complete)
            {
                Logger.Log("Warning: Unable to retrieve full properties for:", Helpers.LogLevel.Warning, Client);
                foreach (UUID uuid in PrimsWaiting.Keys)
                    Logger.Log(uuid.ToString(), Helpers.LogLevel.Warning, Client);
            }

            string output = OSDParser.SerializeLLSDXmlString(Helpers.PrimListToOSD(prims));
            try { File.WriteAllText(file, output); }
            catch (Exception e) { return e.Message; }

            Logger.Log("Exported " + prims.Count + " prims to " + file, Helpers.LogLevel.Info, Client);

            // Create a list of all the textures to download
            List<ImageRequest> textureRequests = new List<ImageRequest>();

            lock (Textures)
            {
                foreach (var prim in prims)
                {
                    if (prim.Textures.DefaultTexture.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
                        !Textures.Contains(prim.Textures.DefaultTexture.TextureID))
                    {
                        Textures.Add(prim.Textures.DefaultTexture.TextureID);
                    }

                    foreach (var face in prim.Textures.FaceTextures)
                    {
                        if (face != null &&
                            face.TextureID != Primitive.TextureEntry.WHITE_TEXTURE &&
                            !Textures.Contains(face.TextureID))
                        {
                            Textures.Add(face.TextureID);
                        }
                    }

                    if (prim.Sculpt != null && prim.Sculpt.SculptTexture != UUID.Zero && !Textures.Contains(prim.Sculpt.SculptTexture))
                    {
                        Textures.Add(prim.Sculpt.SculptTexture);
                    }
                }

                // Create a request list from all the images
                textureRequests.AddRange(Textures.Select(t => new ImageRequest(t, ImageType.Normal, 1013000.0f, 0)));
            }

            // Download all the textures in the export list
            foreach (var request in textureRequests)
            {
                Client.Assets.RequestImage(request.ImageID, request.Type, Assets_OnImageReceived);
            }

            return $"XML exported, downloading {Textures.Count} textures";
        }

        private bool RequestObjectProperties(List<Primitive> objects, int msPerRequest)
        {
            // Create an array of the local IDs of all the prims we are requesting properties for
            uint[] localIds = new uint[objects.Count];
            
            lock (PrimsWaiting)
            {
                PrimsWaiting.Clear();

                for (int i = 0; i < objects.Count; ++i)
                {
                    localIds[i] = objects[i].LocalID;
                    PrimsWaiting.Add(objects[i].ID, objects[i]);
                }
            }

            Client.Objects.SelectObjects(Client.Network.CurrentSim, localIds);

            return AllPropertiesReceived.WaitOne(2000 + msPerRequest * objects.Count, false);
        }

        private void Assets_OnImageReceived(TextureRequestState state, AssetTexture asset)
        {

            if (state == TextureRequestState.Finished && Textures.Contains(asset.AssetID))
            {
                lock (Textures)
                    Textures.Remove(asset.AssetID);

                try { File.WriteAllBytes(asset.AssetID + ".jp2", asset.AssetData); }
                catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }

                if (asset.Decode())
                {
                    try { File.WriteAllBytes(asset.AssetID + ".tga", OpenMetaverse.Imaging.Targa.Encode(asset.Image)); }
                    catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client); }
                }
                else
                {
                    Logger.Log("Failed to decode image " + asset.AssetID, Helpers.LogLevel.Error, Client);
                }

                Logger.Log("Finished downloading image " + asset.AssetID, Helpers.LogLevel.Info, Client);
            }
        }

        private void Objects_OnObjectPropertiesFamily(object sender, ObjectPropertiesFamilyEventArgs e)
        {
            Properties = new Primitive.ObjectProperties();
            Properties.SetFamilyProperties(e.Properties);
            GotPermissions = true;
            GotPermissionsEvent.Set();
        }

        private void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            lock (PrimsWaiting)
            {
                PrimsWaiting.Remove(e.Properties.ObjectID);

                if (PrimsWaiting.Count == 0)
                    AllPropertiesReceived.Set();
            }
        }
    }
}
