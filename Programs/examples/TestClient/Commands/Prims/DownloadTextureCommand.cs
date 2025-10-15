using System;
using System.IO;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Assets;

namespace TestClient.Commands.Prims
{
    public class DownloadTextureCommand : Command
    {
        UUID TextureID;
        AutoResetEvent DownloadHandle = new AutoResetEvent(false);
        AssetTexture Asset;
        TextureRequestState resultState;

        public DownloadTextureCommand(TestClient testClient)
        {
            Name = "downloadtexture";
            Description = "Downloads the specified texture. " +
                "Usage: downloadtexture [texture-uuid] [discardlevel]";
            Category = CommandCategory.Inventory;

        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1 && args.Length != 2)
                return "Usage: downloadtexture [texture-uuid] [discardlevel]";

            TextureID = UUID.Zero;
            DownloadHandle.Reset();
            Asset = null;

            if (UUID.TryParse(args[0], out TextureID))
            {
                int discardLevel = 0;

                if (args.Length > 1)
                {
                    if (!int.TryParse(args[1], out discardLevel))
                        return "Usage: downloadtexture [texture-uuid] [discardlevel]";
                }

                Client.Assets.RequestImage(TextureID, ImageType.Normal, Assets_OnImageReceived);

                if (DownloadHandle.WaitOne(TimeSpan.FromMinutes(2), false))
                {
                    if (resultState == TextureRequestState.Finished)
                    {
                        if (Asset != null && Asset.Decode())
                        {
                            try { File.WriteAllBytes(Asset.AssetID + ".jp2", Asset.AssetData); }
                            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error, Client, ex); }

                            return $"Saved {Asset.AssetID}.jp2 ({Asset.Image.Width}x{Asset.Image.Height})";
                        }
                        else
                        {
                            return "Failed to decode texture " + TextureID;
                        }
                    }
                    else if (resultState == TextureRequestState.NotFound)
                    {
                        return "Simulator reported texture not found: " + TextureID;
                    }
                    else
                    {
                        return "Download failed for texture " + TextureID + " " +  resultState;
                    }
                }
                else
                {
                    return "Timed out waiting for texture download";
                }
            }
            else
            {
                return "Usage: downloadtexture [texture-uuid]";
            }
        }

        private void Assets_OnImageReceived(TextureRequestState state, AssetTexture asset)
        {
            resultState = state;
            Asset = asset;

            DownloadHandle.Set();
        }
    }
}
