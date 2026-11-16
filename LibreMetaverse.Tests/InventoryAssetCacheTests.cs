using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class InventoryAssetCacheTests
    {
        private GridClient _client;
        private string _cacheDir;

        [SetUp]
        public void SetUp()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "lmv-inventory-asset-cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cacheDir);
            _client = new GridClient();
            _client.Settings.AssetCache.Enabled = true;
            _client.Settings.AssetCache.Dir = _cacheDir;
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            try { Directory.Delete(_cacheDir, true); }
            catch (IOException) { }
        }

        [Test]
        public async Task InventoryAsset_WithNullAssetID_IsNotAnsweredFromCache()
        {
            // The simulator can send a null asset ID for an inventory item. Whatever sits in the cache
            // under UUID.Zero belongs to some other item, so it must never be handed back for this one.
            _client.Assets.Cache.SaveAssetToCache(UUID.Zero, Encoding.UTF8.GetBytes("another item's text"));

            // No simulator here, so a real request can't complete before the token runs out.
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            Asset asset = null;
            try
            {
                asset = await _client.Assets.RequestInventoryAssetAsync(UUID.Zero, UUID.Random(), UUID.Zero,
                    UUID.Random(), AssetType.LSLText, true, UUID.Random(), cts.Token);
            }
            catch (OperationCanceledException) { }

            Assert.That(asset, Is.Null);
        }

        [Test]
        public async Task InventoryAsset_WithKnownAssetID_IsAnsweredFromCache()
        {
            var assetID = UUID.Random();
            var text = Encoding.UTF8.GetBytes("default { state_entry() { } }");
            _client.Assets.Cache.SaveAssetToCache(assetID, text);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var asset = await _client.Assets.RequestInventoryAssetAsync(assetID, UUID.Random(), UUID.Zero,
                UUID.Random(), AssetType.LSLText, true, UUID.Random(), cts.Token);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.AssetData, Is.EqualTo(text));
        }
    }
}
