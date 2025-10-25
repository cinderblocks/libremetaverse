using System;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace TestClient.Commands.Prims
{
    public class PrimInfoCommand : Command
    {
        public PrimInfoCommand(TestClient testClient)
        {
            Name = "priminfo";
            Description = "Dumps information about a specified prim. " + "Usage: priminfo [prim-uuid]";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            UUID primID;

            if (args.Length != 1)
            {
                return "Usage: priminfo [prim-uuid]";
            }

            if (!UUID.TryParse(args[0], out primID))
            {
                return $"{args[0]} is not a valid UUID";
            }

            var kvp = Client.Network.CurrentSim.ObjectsPrimitives.FirstOrDefault(
                prim => prim.Value.ID == primID);

            if (kvp.Value == null)
            {
                return $"Could not find object {primID}";
            }

            var target = kvp.Value;
            if (target.Text != string.Empty)
            {
                Logger.Log("Text: " + target.Text, Helpers.LogLevel.Info, Client);
            }
            if (target.Light != null)
            {
                Logger.Log("Light: " + target.Light, Helpers.LogLevel.Info, Client);
            }
            if (target.ParticleSys.CRC != 0)
            {
                Logger.Log("Particles: " + target.ParticleSys, Helpers.LogLevel.Info, Client);
            }

            if (target.Textures != null)
            {
                Logger.Log($"Default texture: {target.Textures.DefaultTexture.TextureID.ToString()}",
                    Helpers.LogLevel.Info);

                for (int i = 0; i < target.Textures.FaceTextures.Length; i++)
                {
                    if (target.Textures.FaceTextures[i] != null)
                    {
                        Logger.Log($"Face {i}: {target.Textures.FaceTextures[i].TextureID.ToString()}",
                            Helpers.LogLevel.Info, Client);
                    }
                }
            }
            else
            {
                Logger.Log("null", Helpers.LogLevel.Info, Client);
            }

            AutoResetEvent propsEvent = new AutoResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> propsCallback =
                delegate(object sender, ObjectPropertiesEventArgs e)
                {
                    Logger.Log(
                        $"Category: {e.Properties.Category}\nFolderID: {e.Properties.FolderID}\nFromTaskID: {e.Properties.FromTaskID}\nInventorySerial: {e.Properties.InventorySerial}\nItemID: {e.Properties.ItemID}\nCreationDate: {e.Properties.CreationDate}", Helpers.LogLevel.Info);
                    propsEvent.Set();
                };

            Client.Objects.ObjectProperties += propsCallback;

            Client.Objects.SelectObject(Client.Network.CurrentSim, target.LocalID, true);

            propsEvent.WaitOne(TimeSpan.FromSeconds(10), false);
            Client.Objects.ObjectProperties -= propsCallback;

            return "Done.";
        }
    }
}
