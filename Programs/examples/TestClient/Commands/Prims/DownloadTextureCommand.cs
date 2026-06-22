using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;

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

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var asset = await Client.Assets.RequestImageAsync(textureID, ImageType.Normal, cts.Token).ConfigureAwait(false);

            if (asset == null)
                return "Download failed or texture not found: " + textureID;

            if (asset.Decode())
            {
                try { File.WriteAllBytes(asset.AssetID + ".jp2", asset.AssetData); }
                catch (Exception ex) { Logger.Error(ex.Message, ex, Client); }

                return $"Saved {asset.AssetID}.jp2 ({asset.Image.Width}x{asset.Image.Height})";
            }
            else
            {
                return "Failed to decode texture " + textureID;
            }
        }
    }
}

