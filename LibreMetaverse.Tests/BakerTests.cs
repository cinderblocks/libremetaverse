/*
 * Unit tests for Baker
 */

using System.Reflection;
using NUnit.Framework;
using LibreMetaverse.Imaging;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    [Category("Imaging")]
    public class BakerTests
    {
        [Test]
        public void Bake_GrayscaleLayerTexture_DoesNotThrow()
        {
            var layerImage = new ManagedImage(4, 4,
                ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < layerImage.Red.Length; i++) layerImage.Red[i] = 128;
            for (int i = 0; i < layerImage.Alpha.Length; i++) layerImage.Alpha[i] = 255;

            var textureData = new AppearanceManager.TextureData
            {
                Texture = new Assets.AssetTexture(layerImage),
                Color = new Color4(0.5f, 0.25f, 0.75f, 1f),
                TextureIndex = AvatarTextureIndex.UpperShirt
            };

            var baker = new Baker(BakeType.UpperBody);
            baker.AddTexture(textureData);

            Assert.DoesNotThrow(() => baker.Bake());
            Assert.That(baker.BakedTexture, Is.Not.Null);
        }

        [Test]
        public void Bake_GrayscaleHairTextureNoSkin_DoesNotThrow()
        {
            var hairImage = new ManagedImage(4, 4,
                ManagedImage.ImageChannels.Gray | ManagedImage.ImageChannels.Alpha);
            for (int i = 0; i < hairImage.Red.Length; i++) hairImage.Red[i] = 200;
            for (int i = 0; i < hairImage.Alpha.Length; i++) hairImage.Alpha[i] = 255;

            var textureData = new AppearanceManager.TextureData
            {
                Texture = new Assets.AssetTexture(hairImage),
                Color = Color4.White,
                TextureIndex = AvatarTextureIndex.Hair
            };

            // No HeadBodypaint texture is added, so skinTexture.Texture stays null and
            // Bake() takes the head-bake hair branch that calls MultiplyLayerFromAlpha
            // against the (grayscale) hair texture.
            var baker = new Baker(BakeType.Head);
            baker.AddTexture(textureData);

            Assert.DoesNotThrow(() => baker.Bake());
            Assert.That(baker.BakedTexture, Is.Not.Null);
        }

        [Test]
        public void LoadResourceLayer_HeadColorTga_LoadsFromCharacterDirectory()
        {
            var image = Baker.LoadResourceLayer("head_color.tga");
            Assert.That(image, Is.Not.Null);
        }

        [Test]
        public void LoadResourceLayer_CachedResourceNotCorruptedByCallerMutation()
        {
            // First call populates the cache; it's the second (cache-hit) call whose return
            // value is at risk of aliasing the cached instance.
            Baker.LoadResourceLayer("head_color.tga");

            var second = Baker.LoadResourceLayer("head_color.tga");
            Assert.That(second, Is.Not.Null);
            var originalWidth = second!.Width;
            var originalHeight = second.Height;

            // Callers (e.g. ApplyAlpha/SanitizeLayers) resize a loaded resource image in place.
            // LoadResourceLayer must hand back a clone on every call - including cache hits -
            // rather than the cached instance, or this mutation would corrupt every subsequent
            // load of the same file.
            second.ResizeNearestNeighbor(originalWidth / 2, originalHeight / 2);

            var third = Baker.LoadResourceLayer("head_color.tga");
            Assert.That(third, Is.Not.Null);
            Assert.That(third!.Width, Is.EqualTo(originalWidth));
            Assert.That(third.Height, Is.EqualTo(originalHeight));
        }

        [Test]
        public void ResizeToBakeDimensions_SourceSmallerInOneAxisOnly_ScalesRatherThanTiles()
        {
            var method = typeof(Baker).GetMethod("ResizeToBakeDimensions", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            // Width (2) is smaller than the 4x4 target, but height (8) is larger - the mixed
            // case that used to always tile (wrapping/cropping the larger axis instead of
            // scaling it down). Fill Red with a gradient down the height axis so tiling
            // (which would only ever see source rows 0-3) is distinguishable from a proper
            // resize (which should sample across the full 0-7 source row range).
            var src = new ManagedImage(2, 8, ManagedImage.ImageChannels.Color);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    src.Red[y * 2 + x] = (byte)(y * 10);
                }
            }

            var result = (ManagedImage?)method!.Invoke(null, new object[] { src, 4, 4 });

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Width, Is.EqualTo(4));
            Assert.That(result.Height, Is.EqualTo(4));

            // Last output row should sample near the top of the source (value ~70), not be
            // capped at ~30 the way a wrap/crop of only the first 4 source rows would be.
            Assert.That(result.Red[3 * 4], Is.GreaterThan(50));
        }
    }
}
