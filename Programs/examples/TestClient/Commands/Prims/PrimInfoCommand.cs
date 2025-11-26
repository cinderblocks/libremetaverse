using System;
using System.Linq;
using System.Threading.Tasks;
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: priminfo [prim-uuid]";
            }

            if (!UUID.TryParse(args[0], out var primID))
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
                Logger.Info("Text: " + target.Text, Client);
            }
            if (target.Light != null)
            {
                Logger.Info("Light: " + target.Light, Client);
            }
            if (target.ParticleSys.CRC != 0)
            {
                Logger.Info("Particles: " + target.ParticleSys, Client);
            }

            if (target.Textures != null)
            {
                Logger.Info($"Default texture: {target.Textures.DefaultTexture.TextureID.ToString()}");

                for (int i = 0; i < target.Textures.FaceTextures.Length; i++)
                {
                    if (target.Textures.FaceTextures[i] != null)
                    {
                        Logger.Info($"Face {i}: {target.Textures.FaceTextures[i].TextureID.ToString()}", Client);
                    }
                }
            }
            else
            {
                Logger.Info("null", Client);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<ObjectPropertiesEventArgs> propsCallback = null;
            propsCallback = (sender, e) =>
            {
                try
                {
                    Logger.Info($"Category: {e.Properties.Category}\nFolderID: {e.Properties.FolderID}\nFromTaskID: {e.Properties.FromTaskID}\nInventorySerial: {e.Properties.InventorySerial}\nItemID: {e.Properties.ItemID}\nCreationDate: {e.Properties.CreationDate}");
                }
                finally
                {
                    tcs.TrySetResult(true);
                }
            };

            try
            {
                Client.Objects.ObjectProperties += propsCallback;

                Client.Objects.SelectObject(Client.Network.CurrentSim, target.LocalID, true);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                // ignore timeout case, just proceed
            }
            finally
            {
                Client.Objects.ObjectProperties -= propsCallback;
            }

            return "Done.";
        }
    }
}

