using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse;

namespace TestClient.Commands.Inventory
{
    public class DownloadCommand : Command
    {
        string usage = "Usage: download [uuid] [assetType]";

        public DownloadCommand(TestClient testClient)
        {
            Name = "download";
            Description = "Downloads the specified asset. Usage: download [uuid] [assetType]";
            Category = CommandCategory.Inventory;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 2)
                return usage;

            if (!UUID.TryParse(args[0], out var assetID))
                return usage;

            AssetType assetType;
            try
            {
                assetType = (AssetType)Enum.Parse(typeof(AssetType), args[1], true);
            }
            catch (ArgumentException)
            {
                return usage;
            }

            if (!Enum.IsDefined(typeof(AssetType), assetType))
                return usage;

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var asset = await Client.Assets.RequestAssetAsync(assetID, assetType, true, cts.Token).ConfigureAwait(false);

            if (asset != null)
            {
                try
                {
                    File.WriteAllBytes($"{assetID}.{assetType.ToString().ToLower()}", asset.AssetData);
                    return $"Saved {assetID}.{assetType.ToString().ToLower()}";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message, ex);
                    return $"Failed to save asset {assetID}: {ex.Message}";
                }
            }
            else
            {
                return $"Failed to download asset {assetID}, perhaps {assetType} is the incorrect asset type?";
            }
        }
    }
}

