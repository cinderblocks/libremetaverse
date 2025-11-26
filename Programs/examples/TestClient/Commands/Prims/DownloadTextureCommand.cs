using System;
using System.IO;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenMetaverse.Assets;

namespace TestClient.Commands.Prims
{
    public class DownloadTextureCommand : Command
    {
        public DownloadTextureCommand(TestClient testClient)
        {
            Name = "downloadtexture";
            Description = "Downloads the specified texture. Usage: downloadtexture [texture-uuid] [discardlevel]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1 && args.Length != 2)
                return "Usage: downloadtexture [texture-uuid] [discardlevel]";

            if (!UUID.TryParse(args[0], out var textureID))
                return "Usage: downloadtexture [texture-uuid] [discardlevel]";

            int discardLevel = 0;
            if (args.Length > 1 && !int.TryParse(args[1], out discardLevel))
                return "Usage: downloadtexture [texture-uuid] [discardlevel]";

            var tcs = new TaskCompletionSource<(TextureRequestState state, AssetTexture asset)>(TaskCreationOptions.RunContinuationsAsynchronously);

            void handler(TextureRequestState state, AssetTexture asset)
            {
                tcs.TrySetResult((state, asset));
            }

            // The client's RequestImage uses a callback delegate with signature (TextureRequestState, AssetTexture)
            Client.Assets.RequestImage(textureID, ImageType.Normal, (state, asset) => handler(state, asset));

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(2))).ConfigureAwait(false);

            if (completed != tcs.Task)
                return "Timed out waiting for texture download";

            var (resultState, Asset) = await tcs.Task.ConfigureAwait(false);

            if (resultState == TextureRequestState.Finished)
            {
                if (Asset != null && Asset.Decode())
                {
                    try { File.WriteAllBytes(Asset.AssetID + ".jp2", Asset.AssetData); }
                    catch (Exception ex) { Logger.Error(ex.Message, ex, Client); }

                    return $"Saved {Asset.AssetID}.jp2 ({Asset.Image.Width}x{Asset.Image.Height})";
                }
                else
                {
                    return "Failed to decode texture " + textureID;
                }
            }
            else if (resultState == TextureRequestState.NotFound)
            {
                return "Simulator reported texture not found: " + textureID;
            }
            else
            {
                return "Download failed for texture " + textureID + " " + resultState;
            }
        }
    }
}

